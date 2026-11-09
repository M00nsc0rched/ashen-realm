using System;
using System.Collections.Generic;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.AI
{
    /// <summary>Bestiary-driven attacks with committed aim, visible warnings and distinct movement patterns.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class EnemyAI : MonoBehaviour, ICombatant, ISavedState, IDamageFilter
    {
        public enum State { Idle, Patrol, Chase, Attack, Dead }
        const float BoltRange = 20f, ConfrontRange = 11f;
        [SerializeField] string enemyId;
        [SerializeField] string saveKey = "";
        [SerializeField] Health health;
        [SerializeField] StatusEffects status;
        [SerializeField] Component animatorComponent;
        [SerializeField] Transform target;
        [SerializeField] ProjectileLibrary projectiles;
        [SerializeField, Optional] ImpactEffects impactEffects;
        [SerializeField] EnemyAttackTelegraph telegraph;
        [Header("Senses")]
        [SerializeField] float detectionRadius = 13f;
        [SerializeField] float closeDetectionRadius = 5f;
        [SerializeField] float fieldOfView = 150f;
        [SerializeField] float loseTargetRadius = 28f;
        [SerializeField] float eyeHeight = 1.5f;
        [Header("Movement")]
        [SerializeField] float turnSpeed = 320f;
        [SerializeField] float gravity = -20f;
        [Header("Patrol")]
        [SerializeField] Vector3[] patrolPoints = Array.Empty<Vector3>();
        [SerializeField] float idleDuration = 2.2f;

        readonly Collider[] overlaps = new Collider[24];
        readonly Dictionary<EnemyMove, float> nextMoves = new Dictionary<EnemyMove, float>();
        EnemyDefinition definition;
        ICreatureAnimator animator;
        IEnemyAttackAnimator attackAnimator;
        CharacterController controller;
        IDamageable targetDamageable;
        State state;
        float stateTime, nextAttackTime, nextHealTime, nextBuffTime;
        float staggerUntil, verticalVelocity, windup, recovery, activeTime;
        float stuckTime, avoidTime, avoidSign = 1f;
        Vector3 knockback, lastPosition, home, lockedTarget, lockedAimPoint, lockedDirection, attackOrigin;
        EnemyMove currentMove, previousMove;
        bool confronted, aimLocked, attackResolved, contactHit, counterTriggered, relocated, burrowArrived;
        int shots, patrolIndex;

        public static event Action<EnemyAI> ConfrontationRequested;
        public static event Action<EnemyAI> Slain;
        public EnemyDefinition Definition => definition;
        public string EnemyId => enemyId;
        public string SaveKey => saveKey;
        public State CurrentState => state;
        public Health Health => health;
        public string DisplayName => definition != null ? definition.Name : enemyId;
        public bool IsEngaged => state == State.Chase || state == State.Attack;
        public EnemyMove CurrentMove => currentMove;
        public EnemyMove LastResolvedMove { get; private set; }
        public int ResolvedMoves { get; private set; }
        public bool TelegraphActive => telegraph != null && telegraph.Visible;
        public int Phase => definition == null || definition.Rank < EnemyRank.Elite ? 1 :
            definition.PhaseThreeAt > 0f && health.Normalized <= definition.PhaseThreeAt ? 3 :
            health.Normalized <= definition.PhaseTwoAt ? 2 : 1;
        bool WaitsToBeConfronted => definition.Dialogue != null && !confronted;
        float MeleeReach => 1.3f + controller.radius;

        void Awake()
        {
            definition = Catalog.Enemy(enemyId);
            animator = (ICreatureAnimator)animatorComponent;
            attackAnimator = animatorComponent as IEnemyAttackAnimator;
            controller = GetComponent<CharacterController>();
            if (target != null) targetDamageable = target.GetComponentInParent<IDamageable>();
            health.SetMax(definition.Health, true);
            lastPosition = home = transform.position;
        }
        void OnEnable() { health.Damaged += OnDamaged; health.Died += OnDied; }
        void OnDisable()
        {
            health.Damaged -= OnDamaged; health.Died -= OnDied;
            if (telegraph != null) telegraph.Hide();
            attackAnimator?.SetBurrow(0f);
        }
        void SetState(State next)
        {
            if (state == next) return;
            state = next; stateTime = 0f;
            if (next == State.Chase) CombatFocus.Set(this);
        }
        public void ApplyCleared() => gameObject.SetActive(false);
        public void Engage() { confronted = true; if (state != State.Dead) SetState(State.Chase); }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            if (state == State.Dead) { animator.SetLocomotion(0f); return; }
            Vector3 velocity = Vector3.zero, look = transform.forward;
            bool held = Time.time < staggerUntil || status.Stunned;
            if (status.Stunned && state == State.Attack && definition.Rank < EnemyRank.Guardian && !attackResolved) CancelAttack();
            bool targetAlive = targetDamageable != null && targetDamageable.IsAlive;
            if (state == State.Idle)
            {
                if (targetAlive && Notices()) BeginChase();
                else if (patrolPoints.Length > 0 && stateTime >= idleDuration)
                { patrolIndex = (patrolIndex + 1) % patrolPoints.Length; SetState(State.Patrol); }
            }
            else if (state == State.Patrol)
            {
                if (targetAlive && Notices()) BeginChase();
                else if (patrolPoints.Length > 0)
                {
                    Vector3 towards = Flat(patrolPoints[patrolIndex] - transform.position);
                    if (towards.magnitude < 0.8f) SetState(State.Idle);
                    else { velocity = towards.normalized * definition.Speed * 0.45f; look = towards; }
                }
            }
            else if (state == State.Chase)
            {
                if (!targetAlive || Flat(target.position - transform.position).magnitude > loseTargetRadius) RecoverAfterTargetLost();
                else
                {
                    Vector3 towards = Flat(target.position - transform.position);
                    float distance = towards.magnitude;
                    look = towards;
                    bool sight = HasLineOfSight();
                    if (definition.PreferredRange > 0f && sight)
                    {
                        if (distance < definition.PreferredRange * 0.5f) velocity = -towards.normalized * definition.Speed * 0.65f;
                        else if (distance > definition.PreferredRange) velocity = towards.normalized * definition.Speed;
                        else velocity = Vector3.Cross(Vector3.up, towards.normalized) * (definition.Speed * definition.Strafe);
                    }
                    else if (distance > MeleeReach * 0.85f) velocity = towards.normalized * definition.Speed;
                    else if (sight) velocity = Vector3.Cross(Vector3.up, towards.normalized) * (definition.Speed * definition.Strafe * 0.4f);
                    if (!held && Time.time >= nextAttackTime && FacingTarget(towards, 35f))
                    {
                        EnemyMove move = PickMove(distance, sight);
                        if (move != null) BeginMove(move);
                    }
                }
            }
            else if (state == State.Attack)
            {
                if (held && currentMove.Kind == MoveKind.Heal) CancelAttack();
                else UpdateAttack(ref velocity, ref look);
            }
            if (held) velocity = Vector3.zero;
            Move(velocity, look, dt);
        }

        void BeginChase()
        {
            if (WaitsToBeConfronted) { confronted = true; ConfrontationRequested?.Invoke(this); return; }
            SetState(State.Chase);
        }
        void RecoverAfterTargetLost()
        {
            CancelVisuals();
            if (definition.Rank >= EnemyRank.Guardian)
            {
                health.ResetHealth(); status.Clear(); nextMoves.Clear(); previousMove = null;
                controller.enabled = false; transform.position = home; controller.enabled = true;
            }
            SetState(patrolPoints.Length > 0 ? State.Patrol : State.Idle);
        }
        EnemyMove PickMove(float distance, bool sight)
        {
            bool alternate = false;
            foreach (var move in definition.Moves)
                if (move != previousMove && Allowed(move, distance, sight)) { alternate = true; break; }
            int total = 0;
            foreach (var move in definition.Moves)
                if ((!alternate || move != previousMove) && Allowed(move, distance, sight)) total += move.Weight;
            if (total <= 0) return null;
            int roll = UnityEngine.Random.Range(0, total);
            foreach (var move in definition.Moves)
            {
                if ((alternate && move == previousMove) || !Allowed(move, distance, sight)) continue;
                roll -= move.Weight;
                if (roll < 0) return move;
            }
            return null;
        }
        bool Allowed(EnemyMove move, float distance, bool sight)
        {
            if (move.MinPhase > Phase || (nextMoves.TryGetValue(move, out float until) && Time.time < until)) return false;
            if (move.Kind == MoveKind.Heal) return health.Normalized < 0.6f && Time.time >= nextHealTime;
            if (move.Kind == MoveKind.Buff || move.Kind == MoveKind.Ward) return Time.time >= nextBuffTime;
            float range = move.Range > 0f ? move.Range : move.Kind == MoveKind.Melee || move.Kind == MoveKind.Counter ? MeleeReach : BoltRange;
            return sight && distance >= move.MinRange && distance <= range;
        }
        void BeginMove(EnemyMove move)
        {
            currentMove = previousMove = move;
            attackResolved = contactHit = counterTriggered = aimLocked = relocated = false;
            burrowArrived = false;
            shots = 0;
            windup = Mathf.Max(0.2f, move.Windup);
            recovery = Mathf.Max(0.2f, move.Recovery);
            activeTime = move.Kind == MoveKind.Charge || move.Kind == MoveKind.Pounce || move.Kind == MoveKind.Burrow ? Mathf.Max(0.15f, move.ActiveTime) :
                move.Kind == MoveKind.Volley ? Mathf.Max(0f, move.Hits - 1) * 0.28f : move.Kind == MoveKind.Counter ? 0.65f : 0f;
            nextAttackTime = Time.time + definition.AttackInterval;
            nextMoves[move] = Time.time + move.Cooldown;
            if (move.Kind == MoveKind.Heal) nextHealTime = Time.time + 9f;
            if (move.Kind == MoveKind.Buff || move.Kind == MoveKind.Ward) nextBuffTime = Time.time + 11f;
            attackOrigin = transform.position;
            CaptureAim();
            SetState(State.Attack);
            attackAnimator?.SetAttackStyle(move.Kind);
            animator.PlayAttack(windup, recovery + activeTime);
            telegraph?.Show(move, attackOrigin, lockedTarget, lockedDirection, MeleeReach, 0f);
        }
        void CaptureAim()
        {
            Vector3 towards = target != null ? Flat(target.position - transform.position) : transform.forward;
            lockedDirection = towards.sqrMagnitude > 0.001f ? towards.normalized : transform.forward;
            lockedAimPoint = target != null ? target.position : transform.position + transform.forward * 4f;
            lockedTarget = lockedAimPoint;
            if (currentMove.Kind == MoveKind.Teleport && target != null)
                lockedTarget = target.position + Vector3.Cross(Vector3.up, lockedDirection) * Mathf.Max(3f, currentMove.TravelDistance);
            lockedTarget.y = attackOrigin.y;
        }
        void UpdateAttack(ref Vector3 velocity, ref Vector3 look)
        {
            if (!aimLocked)
            {
                CaptureAim(); look = lockedDirection;
                if (stateTime >= windup * 0.45f) aimLocked = true;
            }
            else look = lockedDirection;
            if (stateTime < windup)
            {
                // A hit can push the capsule during windup. Its own sweep or rushing lane must move with it;
                // targeted ground attacks and rings keep their original, committed floor position.
                Vector3 warningSource = IsRush(currentMove.Kind) || currentMove.Kind == MoveKind.Sweep || currentMove.Kind == MoveKind.Counter
                    ? transform.position : attackOrigin;
                telegraph?.Show(currentMove, warningSource, lockedTarget, lockedDirection, MeleeReach, stateTime / windup);
            }
            else
            {
                telegraph?.Hide();
                float active = stateTime - windup;
                if (currentMove.Kind == MoveKind.Charge || currentMove.Kind == MoveKind.Pounce)
                {
                    if (active <= activeTime)
                    {
                        // Once the rush starts, lateral hit impulses must not move it outside the advertised lane.
                        knockback = Vector3.zero;
                        velocity = lockedDirection * (currentMove.TravelDistance / activeTime);
                        if (!contactHit && Vector3.Dot(lockedDirection, Flat(target.position - transform.position)) >= 0f &&
                            TargetNear(transform.position + lockedDirection * 0.55f, controller.radius + 0.9f, false))
                        { contactHit = true; Deal(currentMove, transform.position); }
                    }
                    if (!attackResolved && active >= activeTime) MarkResolved();
                }
                else if (currentMove.Kind == MoveKind.Burrow)
                {
                    float progress = Mathf.Clamp01(active / activeTime);
                    attackAnimator?.SetBurrow(Mathf.Sin(progress * Mathf.PI));
                    if (!relocated && progress >= 0.55f)
                    { relocated = true; burrowArrived = Relocate(lockedTarget - lockedDirection * (controller.radius + 0.9f)); }
                    if (!attackResolved && progress >= 1f)
                    { MarkResolved(); if (burrowArrived) ResolveArea(currentMove, lockedTarget, false); attackAnimator?.SetBurrow(0f); }
                }
                else if (currentMove.Kind == MoveKind.Volley)
                {
                    if (!attackResolved) MarkResolved();
                    while (shots < currentMove.Hits && active >= shots * 0.28f) { Launch(currentMove, shots++); }
                }
                else if (currentMove.Kind == MoveKind.Counter)
                {
                    if (counterTriggered && !attackResolved) { MarkResolved(); ResolveArea(currentMove, transform.position, false, true); }
                    if (active >= activeTime && !attackResolved) MarkResolved();
                }
                else if (!attackResolved) { MarkResolved(); Resolve(currentMove); }
            }
            if (stateTime >= windup + activeTime + recovery)
            { CancelVisuals(); SetState(State.Chase); }
        }
        void MarkResolved() { attackResolved = true; LastResolvedMove = currentMove; ResolvedMoves++; }
        void Resolve(EnemyMove move)
        {
            switch (move.Kind)
            {
                case MoveKind.Melee: ResolveArea(move, transform.position, false, true); break;
                case MoveKind.Sweep: ResolveArea(move, transform.position, false, true); break;
                case MoveKind.Ground: ResolveArea(move, lockedTarget, false); break;
                case MoveKind.Ring: ResolveArea(move, attackOrigin, true); break;
                case MoveKind.Teleport:
                    if (Relocate(lockedTarget)) lockedDirection = Flat(lockedAimPoint - transform.position).normalized;
                    Launch(move, 0); break;
                case MoveKind.Bolt:
                    for (int i = 0; i < move.Hits; i++) Launch(move, i);
                    break;
                case MoveKind.Heal: health.Heal(move.Heal); Announce(move); break;
                case MoveKind.Buff: status.ApplyFury(7f); Announce(move); break;
                case MoveKind.Ward: status.ApplyWard(4f); Announce(move); break;
            }
        }
        void Announce(EnemyMove move) => GameMessages.Notify(string.Format(GameText.EnemyActs, definition.Name, move.Name));
        void ResolveArea(EnemyMove move, Vector3 centre, bool annulus, bool frontal = false)
        {
            float radius = move.Kind == MoveKind.Melee ? MeleeReach : move.Kind == MoveKind.Counter ? MeleeReach + 0.4f : move.Radius;
            if (!TargetNear(centre, radius, !frontal)) return;
            Vector3 towards = Flat(target.position - centre);
            if (annulus && towards.magnitude < radius * 0.53f) return;
            if (frontal && Vector3.Angle(lockedDirection, towards) > (move.Kind == MoveKind.Melee ? 45f : 105f)) return;
            Deal(move, centre);
        }
        bool TargetNear(Vector3 centre, float radius, bool ground)
        {
            if (targetDamageable == null || !targetDamageable.IsAlive || target == null) return false;
            if (Flat(target.position - centre).magnitude > radius + 0.3f) return false;
            if (ground && target.position.y - centre.y > 0.8f) return false;
            Vector3 start = centre + Vector3.up * 1.05f, end = target.position + Vector3.up * 1.05f;
            if (Physics.Linecast(start, end, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(target);
            return true;
        }
        void Deal(EnemyMove move, Vector3 centre)
        {
            if (targetDamageable == null || !targetDamageable.IsAlive) return;
            float damage = move.Damage * status.DamageMultiplier;
            Vector3 direction = Flat(target.position - centre);
            if (direction.sqrMagnitude < 0.001f) direction = lockedDirection;
            float applied = targetDamageable.ApplyDamage(new DamageInfo(damage, target.position + Vector3.up, direction, gameObject));
            if (applied < damage * 0.5f)
            {
                float reel = definition.Rank == EnemyRank.Common ? 0.7f : 0.3f;
                staggerUntil = Time.time + reel; animator.PlayRecoil();
                nextAttackTime = Mathf.Max(nextAttackTime, Time.time + reel + 0.5f);
            }
            else
            {
                impactEffects?.Emit(target.position + Vector3.up, -direction, ImpactEffects.Kind.Flesh);
                EnemyMoveEffects.Apply(move, target.GetComponentInParent<StatusEffects>());
            }
            if (move.Lifesteal > 0f) health.Heal(applied * move.Lifesteal);
        }
        void Launch(EnemyMove move, int shot)
        {
            if (target == null) return;
            Vector3 origin = transform.position + Vector3.up * (controller.height * 0.7f) + transform.forward * (controller.radius + 0.3f);
            Vector3 aim = lockedAimPoint + Vector3.up * 1.2f - origin;
            // A volley brackets the committed target point; individual bolts remain physically dodgeable.
            float spread = move.Kind == MoveKind.Volley ? (shot - (move.Hits - 1) * 0.5f) * 6f : (shot - (move.Hits - 1) * 0.5f) * 8f;
            Action<float> drain = move.Lifesteal > 0f ? applied => { if (health.IsAlive) health.Heal(applied * move.Lifesteal); } : (Action<float>)null;
            projectiles.Launch(move.Bolt, origin, Quaternion.Euler(0f, spread, 0f) * aim, move.Damage * status.DamageMultiplier, gameObject, false, move, drain);
        }
        bool Relocate(Vector3 destination)
        {
            if (!Physics.Raycast(destination + Vector3.up * 2f, Vector3.down, out RaycastHit ground, 4f, ~0, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.7f) return false;
            destination = ground.point + Vector3.up * 0.06f;
            int count = Physics.OverlapCapsuleNonAlloc(destination + Vector3.up * (controller.radius + 0.05f),
                destination + Vector3.up * (controller.height - controller.radius), controller.radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (!overlaps[i].transform.IsChildOf(transform)) return false;
            controller.enabled = false; transform.position = destination; controller.enabled = true;
            return true;
        }
        void CancelVisuals() { telegraph?.Hide(); attackAnimator?.SetBurrow(0f); }
        void CancelAttack() { CancelVisuals(); animator.PlayRecoil(); nextAttackTime = Time.time + 0.8f; SetState(State.Chase); }
        public float FilterDamage(in DamageInfo info, float amount)
        {
            if (state != State.Attack || currentMove == null) return amount;
            if (currentMove.Kind == MoveKind.Counter && stateTime <= windup + activeTime && info.Source != null)
            {
                Vector3 toSource = Flat(info.Source.transform.position - transform.position);
                if (Vector3.Angle(transform.forward, toSource) < 75f) { counterTriggered = true; return amount * 0.25f; }
            }
            if (currentMove.Kind == MoveKind.Burrow && stateTime > windup && stateTime < windup + activeTime) return amount * 0.35f;
            return amount;
        }
        void Move(Vector3 velocity, Vector3 look, float dt)
        {
            if (state != State.Attack && velocity.sqrMagnitude > 0.01f)
            {
                float moved = Flat(transform.position - lastPosition).magnitude;
                stuckTime = moved < velocity.magnitude * dt * 0.3f ? stuckTime + dt : Mathf.Max(0f, stuckTime - dt);
                if (stuckTime > 0.4f) { stuckTime = 0f; avoidTime = 0.8f; avoidSign = -avoidSign; }
                if (avoidTime > 0f) { avoidTime -= dt; velocity = Quaternion.Euler(0f, 70f * avoidSign, 0f) * velocity; }
            }
            lastPosition = transform.position;
            knockback = Vector3.Lerp(knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
            if (controller.enabled) controller.Move((velocity + knockback + Vector3.up * verticalVelocity) * dt);
            look = Flat(look);
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), turnSpeed * dt);
            animator.SetLocomotion(velocity.magnitude / Mathf.Max(0.1f, definition.Speed));
        }
        bool Notices()
        {
            if (target == null) return false;
            Vector3 towards = target.position - transform.position;
            float distance = Flat(towards).magnitude;
            if (distance > (WaitsToBeConfronted ? ConfrontRange : detectionRadius)) return false;
            if (distance > closeDetectionRadius && !WaitsToBeConfronted && Vector3.Angle(transform.forward, towards) > fieldOfView * 0.5f) return false;
            return HasLineOfSight();
        }
        bool HasLineOfSight()
        {
            if (target == null) return false;
            if (Physics.Linecast(transform.position + Vector3.up * eyeHeight, target.position + Vector3.up * 1.5f, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(target);
            return true;
        }
        bool FacingTarget(Vector3 towards, float tolerance) => towards.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, towards) <= tolerance;
        void OnDamaged(DamageInfo info, float amount)
        {
            if (state == State.Dead) return;
            CombatFocus.Set(this);
            if (info.Source == null) return;
            animator.PlayHit(info.Direction);
            bool heavy = definition.Rank >= EnemyRank.Guardian;
            bool rushing = state == State.Attack && IsRush(currentMove.Kind) && stateTime >= windup && stateTime <= windup + activeTime;
            if (!heavy && !rushing) knockback = Flat(info.Direction).normalized * (definition.Rank == EnemyRank.Elite ? 1.2f : 3f);
            if (state == State.Attack && currentMove.Kind == MoveKind.Heal && !attackResolved) { CancelAttack(); return; }
            if (state != State.Attack && !heavy) staggerUntil = Time.time + (definition.Rank == EnemyRank.Elite ? 0.18f : 0.35f);
            if (state == State.Idle || state == State.Patrol) Engage();
        }
        void OnDied(Health _)
        {
            CancelVisuals(); SetState(State.Dead); controller.enabled = false; animator.PlayDeath(); Slain?.Invoke(this);
        }
        static bool IsRush(MoveKind kind) => kind == MoveKind.Charge || kind == MoveKind.Pounce;
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
