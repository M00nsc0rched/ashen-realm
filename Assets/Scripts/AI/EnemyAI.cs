using System;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.AI
{
    /// <summary>
    /// Data-driven foe for the Deep Dark: the same Idle/Patrol/Chase/Attack loop as the Cursed Knight, but its moves
    /// come from its <see cref="EnemyDefinition"/>. Each attack opportunity rolls one of the moves the situation allows,
    /// weighted like Graveborne's turn-based AI: blows in reach, bolts in sight, mending when hurt.
    /// Guardians wait until confronted, so their speech always comes before the first blow.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class EnemyAI : MonoBehaviour, ICombatant
    {
        public enum State { Idle, Patrol, Chase, Attack, Dead }

        const float BoltRange = 20f;
        const float BuffCooldown = 11f;
        const float HealCooldown = 9f;
        const float HealBelow = 0.6f;
        const float FuryTime = 7f;
        const float WardTime = 4f;
        const float ConfrontRange = 11f;

        [SerializeField] string enemyId;
        [SerializeField] Health health;
        [SerializeField] StatusEffects status;
        [SerializeField] Component animatorComponent;
        [SerializeField] Transform target;
        [SerializeField] ProjectileLibrary projectiles;
        [SerializeField, Optional] ImpactEffects impactEffects;

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

        readonly Collider[] overlapBuffer = new Collider[8];
        EnemyDefinition definition;
        ICreatureAnimator animator;
        CharacterController controller;
        IDamageable targetDamageable;
        State state = State.Idle;
        float stateTime;
        int patrolIndex;
        float nextAttackTime, nextHealTime, nextBuffTime;
        float staggerUntil;
        Vector3 knockback;
        float verticalVelocity;
        bool attackResolved;
        EnemyMove currentMove;
        float windup, recovery;
        float stuckTime, avoidTime, avoidSign = 1f;
        Vector3 lastPosition, home;
        bool confronted;

        /// <summary>A guardian wants to speak. The story director answers with <see cref="Engage"/>.</summary>
        public static event Action<EnemyAI> ConfrontationRequested;
        /// <summary>Any foe fell; the story director pays out and tracks guardians.</summary>
        public static event Action<EnemyAI> Slain;

        public EnemyDefinition Definition => definition;
        public string EnemyId => enemyId;
        public State CurrentState => state;
        public Health Health => health;
        public string DisplayName => definition != null ? definition.Name : enemyId;
        public bool IsEngaged => state == State.Chase || state == State.Attack;

        bool WaitsToBeConfronted => definition.Dialogue != null && !confronted;

        void Awake()
        {
            definition = Catalog.Enemy(enemyId);
            animator = (ICreatureAnimator)animatorComponent;
            controller = GetComponent<CharacterController>();
            if (target != null) targetDamageable = target.GetComponentInParent<IDamageable>();
            health.SetMax(definition.Health, true);
            lastPosition = home = transform.position;
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        void SetState(State next)
        {
            if (state == next) return;
            state = next;
            stateTime = 0f;
            if (next == State.Chase) CombatFocus.Set(this);
        }

        /// <summary>Starts the fight (the confrontation is over, or a playtest skipped it).</summary>
        public void Engage()
        {
            confronted = true;
            if (state != State.Dead) SetState(State.Chase);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;

            if (state == State.Dead)
            {
                animator.SetLocomotion(0f);
                return;
            }

            Vector3 desiredVelocity = Vector3.zero;
            Vector3 lookDirection = transform.forward;
            bool held = Time.time < staggerUntil || status.Stunned;
            bool targetAlive = targetDamageable != null && targetDamageable.IsAlive;

            switch (state)
            {
                case State.Idle:
                    if (targetAlive && Notices()) { BeginChase(); break; }
                    if (patrolPoints.Length > 0 && stateTime >= idleDuration)
                    {
                        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                        SetState(State.Patrol);
                    }
                    break;

                case State.Patrol:
                {
                    if (targetAlive && Notices()) { BeginChase(); break; }
                    Vector3 toPoint = Flat(patrolPoints[patrolIndex] - transform.position);
                    if (toPoint.magnitude < 0.8f) { SetState(State.Idle); break; }
                    desiredVelocity = toPoint.normalized * (definition.Speed * 0.45f);
                    lookDirection = toPoint;
                    break;
                }

                case State.Chase:
                {
                    if (!targetAlive) { RecoverAfterTargetLost(); break; }
                    Vector3 toTarget = Flat(target.position - transform.position);
                    float distance = toTarget.magnitude;
                    if (distance > loseTargetRadius) { RecoverAfterTargetLost(); break; }

                    lookDirection = toTarget;
                    bool sight = HasLineOfSight();
                    float reach = MeleeReach;
                    if (definition.PreferredRange > 0f && sight)
                    {
                        // Casters hold their distance and give ground when rushed.
                        if (distance < definition.PreferredRange * 0.45f) desiredVelocity = -toTarget.normalized * (definition.Speed * 0.6f);
                        else if (distance > definition.PreferredRange) desiredVelocity = toTarget.normalized * definition.Speed;
                    }
                    else if (distance > reach * 0.85f)
                    {
                        desiredVelocity = toTarget.normalized * definition.Speed;
                    }

                    if (!held && Time.time >= nextAttackTime && FacingTarget(toTarget, 35f))
                    {
                        EnemyMove move = PickMove(distance, sight);
                        if (move != null) BeginMove(move);
                    }
                    break;
                }

                case State.Attack:
                    UpdateAttack(ref lookDirection);
                    break;
            }

            if (held) desiredVelocity = Vector3.zero;
            Move(desiredVelocity, lookDirection, held || state == State.Attack, dt);
        }

        void BeginChase()
        {
            if (WaitsToBeConfronted)
            {
                confronted = true;
                ConfrontationRequested?.Invoke(this);
                return;
            }
            SetState(State.Chase);
        }

        float MeleeReach => 1.3f + controller.radius;

        void RecoverAfterTargetLost()
        {
            // Bosses forget the fight and recover, so a respawned player faces them whole.
            if (definition.Rank >= EnemyRank.Guardian)
            {
                health.ResetHealth();
                status.Clear();
                controller.enabled = false;
                transform.position = home;
                controller.enabled = true;
            }
            SetState(patrolPoints.Length > 0 ? State.Patrol : State.Idle);
        }

        EnemyMove PickMove(float distance, bool sight)
        {
            int total = 0;
            foreach (var m in definition.Moves)
                if (Allowed(m, distance, sight)) total += m.Weight;
            if (total == 0) return null;

            int roll = UnityEngine.Random.Range(0, total);
            foreach (var m in definition.Moves)
            {
                if (!Allowed(m, distance, sight)) continue;
                roll -= m.Weight;
                if (roll < 0) return m;
            }
            return null;
        }

        bool Allowed(EnemyMove move, float distance, bool sight)
        {
            switch (move.Kind)
            {
                case MoveKind.Melee: return distance <= MeleeReach;
                case MoveKind.Bolt: return sight && distance <= BoltRange;
                case MoveKind.Heal: return health.Normalized < HealBelow && Time.time >= nextHealTime;
                default: return Time.time >= nextBuffTime;
            }
        }

        void BeginMove(EnemyMove move)
        {
            currentMove = move;
            attackResolved = false;
            windup = definition.Rank == EnemyRank.Common ? 0.6f : 0.5f;
            recovery = 0.5f;
            nextAttackTime = Time.time + definition.AttackInterval;
            if (move.Kind == MoveKind.Heal) nextHealTime = Time.time + HealCooldown;
            if (move.Kind == MoveKind.Buff || move.Kind == MoveKind.Ward) nextBuffTime = Time.time + BuffCooldown;
            SetState(State.Attack);
            animator.PlayAttack(windup, recovery);
        }

        void UpdateAttack(ref Vector3 lookDirection)
        {
            if (target != null && stateTime < windup * 0.8f)
                lookDirection = Flat(target.position - transform.position);

            if (!attackResolved && stateTime >= windup)
            {
                attackResolved = true;
                Resolve(currentMove);
            }

            if (stateTime >= windup + recovery)
                SetState(State.Chase);
        }

        void Resolve(EnemyMove move)
        {
            float damage = move.Damage * status.DamageMultiplier;
            switch (move.Kind)
            {
                case MoveKind.Melee: ResolveMelee(move, damage); break;
                case MoveKind.Bolt: ResolveBolt(move, damage); break;
                case MoveKind.Heal:
                    health.Heal(move.Heal);
                    GameMessages.Notify(string.Format(GameText.EnemyActs, definition.Name, move.Name));
                    break;
                case MoveKind.Buff:
                    status.ApplyFury(FuryTime);
                    GameMessages.Notify(string.Format(GameText.EnemyActs, definition.Name, move.Name));
                    break;
                case MoveKind.Ward:
                    status.ApplyWard(WardTime);
                    GameMessages.Notify(string.Format(GameText.EnemyActs, definition.Name, move.Name));
                    break;
            }
        }

        /// <summary>Overlap sphere in front of the enemy at the moment the blow lands.</summary>
        void ResolveMelee(EnemyMove move, float damage)
        {
            Vector3 center = transform.position + transform.forward * (MeleeReach * 0.65f) + Vector3.up * (controller.height * 0.55f);
            float radius = 0.7f + controller.radius * 0.5f;
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = overlapBuffer[i];
                if (c.transform.IsChildOf(transform) || c.GetComponentInParent<EnemyAI>() != null) continue;
                var damageable = c.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                Vector3 point = c.ClosestPoint(center);
                float applied = damageable.ApplyDamage(new DamageInfo(damage, point, transform.forward, gameObject));
                if (applied < damage * 0.5f)
                {
                    // Blocked: the blow glances off and the attacker reels.
                    float reel = definition.Rank == EnemyRank.Common ? 0.7f : 0.3f;
                    staggerUntil = Time.time + reel;
                    animator.PlayRecoil();
                    nextAttackTime = Mathf.Max(nextAttackTime, Time.time + reel + 0.5f);
                }
                else
                {
                    if (impactEffects != null) impactEffects.Emit(point, -transform.forward, ImpactEffects.Kind.Flesh);
                    EnemyMoveEffects.Apply(move, c.GetComponentInParent<StatusEffects>());
                }
                if (move.Lifesteal > 0f) health.Heal(applied * move.Lifesteal);
                break;
            }
        }

        void ResolveBolt(EnemyMove move, float damage)
        {
            if (target == null) return;
            Vector3 origin = transform.position + Vector3.up * (controller.height * 0.7f) + transform.forward * (controller.radius + 0.3f);
            Vector3 aim = target.position + Vector3.up * 1.2f - origin;
            for (int i = 0; i < move.Hits; i++)
            {
                Vector3 spread = Quaternion.Euler(0f, (i - (move.Hits - 1) * 0.5f) * 8f, 0f) * aim;
                System.Action<float> drain = null;
                if (move.Lifesteal > 0f) drain = applied => { if (health.IsAlive) health.Heal(applied * move.Lifesteal); };
                projectiles.Launch(move.Bolt, origin, spread, damage, gameObject, false, move, drain);
            }
        }

        void Move(Vector3 desiredVelocity, Vector3 lookDirection, bool holdPosition, float dt)
        {
            // Side-step when walking into an obstacle.
            if (desiredVelocity.sqrMagnitude > 0.01f)
            {
                float moved = Flat(transform.position - lastPosition).magnitude;
                float expected = desiredVelocity.magnitude * dt;
                stuckTime = moved < expected * 0.3f ? stuckTime + dt : Mathf.Max(0f, stuckTime - dt);
                if (stuckTime > 0.4f)
                {
                    stuckTime = 0f;
                    avoidTime = 0.8f;
                    avoidSign = -avoidSign;
                }
                if (avoidTime > 0f)
                {
                    avoidTime -= dt;
                    desiredVelocity = Quaternion.Euler(0f, 70f * avoidSign, 0f) * desiredVelocity;
                }
            }
            lastPosition = transform.position;

            knockback = Vector3.Lerp(knockback, Vector3.zero, 1f - Mathf.Exp(-8f * dt));
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;

            Vector3 velocity = (holdPosition ? Vector3.zero : desiredVelocity) + knockback;
            if (controller.enabled)
                controller.Move((velocity + Vector3.up * verticalVelocity) * dt);

            lookDirection = Flat(lookDirection);
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * dt);
            }

            animator.SetLocomotion(holdPosition ? 0f : Flat(desiredVelocity).magnitude / Mathf.Max(0.1f, definition.Speed));
        }

        bool Notices()
        {
            if (target == null) return false;
            Vector3 toTarget = target.position - transform.position;
            float distance = Flat(toTarget).magnitude;
            float radius = WaitsToBeConfronted ? ConfrontRange : detectionRadius;
            if (distance > radius) return false;
            if (distance > closeDetectionRadius && !WaitsToBeConfronted && Vector3.Angle(transform.forward, Flat(toTarget)) > fieldOfView * 0.5f)
                return false;
            return HasLineOfSight();
        }

        bool HasLineOfSight()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 targetEye = target.position + Vector3.up * 1.5f;
            if (Physics.Linecast(eye, targetEye, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(target);
            return true;
        }

        bool FacingTarget(Vector3 toTarget, float tolerance) =>
            toTarget.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, toTarget) <= tolerance;

        void OnDamaged(DamageInfo info, float amount)
        {
            if (state == State.Dead) return;
            CombatFocus.Set(this);
            if (info.Source == null) return; // poison does not flinch

            animator.PlayHit(info.Direction);
            bool heavy = definition.Rank >= EnemyRank.Guardian;
            if (!heavy) knockback = Flat(info.Direction).normalized * (definition.Rank == EnemyRank.Elite ? 1.2f : 3f);

            // Committed attacks have hyper-armor; otherwise a foe flinches, big ones less.
            if (state != State.Attack && !heavy)
                staggerUntil = Time.time + (definition.Rank == EnemyRank.Elite ? 0.18f : 0.35f);
            if (state == State.Idle || state == State.Patrol) Engage();
        }

        void OnDied(Health _)
        {
            SetState(State.Dead);
            controller.enabled = false;
            animator.PlayDeath();
            Slain?.Invoke(this);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
