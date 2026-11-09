using System;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.AI
{
    /// <summary>
    /// Finite-state melee enemy: Idle → Patrol → Chase → Attack → Dead.
    /// Moves with a CharacterController (no NavMesh) and steers around obstacles with a simple side-step.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CursedKnightAI : MonoBehaviour, ICombatant, ISavedState
    {
        public enum State { Idle, Patrol, Chase, Attack, Dead }

        [Header("References")]
        [SerializeField] Health health;
        [SerializeField] KnightAnimator animator;
        [SerializeField] Transform target;
        [SerializeField, Optional] ImpactEffects impactEffects;

        [Header("Senses")]
        [SerializeField] float detectionRadius = 14f;
        [SerializeField] float closeDetectionRadius = 5f;
        [SerializeField] float fieldOfView = 140f;
        [SerializeField] float loseTargetRadius = 26f;
        [SerializeField] float eyeHeight = 1.7f;
        [SerializeField] LayerMask sightMask = ~0;

        [Header("Movement")]
        [SerializeField] float patrolSpeed = 1.6f;
        [SerializeField] float chaseSpeed = 3.6f;
        [SerializeField] float turnSpeed = 300f;
        [SerializeField] float gravity = -20f;

        [Header("Patrol")]
        [SerializeField] Vector3[] patrolPoints = Array.Empty<Vector3>();
        [SerializeField] float idleDuration = 2.5f;

        [Header("Attack")]
        [SerializeField] float attackRange = 2.1f;
        [SerializeField] float attackDamage = 18f;
        [Tooltip("Seconds between the starts of two attacks.")]
        [SerializeField] float attackInterval = 2.2f;
        [SerializeField] float attackWindup = 0.65f;
        [SerializeField] float attackRecovery = 0.55f;
        [SerializeField] float attackReach = 1.9f;
        [SerializeField] float attackHitRadius = 0.85f;

        [Header("Hit Reaction")]
        [SerializeField] float staggerDuration = 0.35f;
        [SerializeField] float knockbackSpeed = 3f;
        [Tooltip("Recoil time when the player blocks a blow.")]
        [SerializeField] float blockedRecoil = 0.7f;

        readonly Collider[] overlapBuffer = new Collider[8];
        CharacterController controller;
        IDamageable targetDamageable;
        State state = State.Idle;
        float stateTime;
        int patrolIndex;
        float nextAttackTime;
        float staggerUntil;
        Vector3 knockback;
        float verticalVelocity;
        bool attackResolved;
        float stuckTime, avoidTime;
        float avoidSign = 1f;
        Vector3 lastPosition;
        Vector3 committedDirection;
        int attackSequence;
        MoveKind currentPattern;
        float moveWindup, moveRecovery;

        public State CurrentState => state;
        public Health Health => health;
        public MoveKind CurrentPattern => currentPattern;
        /// <summary>True while the knight is actively fighting the player.</summary>
        public bool IsEngaged => state == State.Chase || state == State.Attack;
        public string DisplayName => GameText.KnightName;
        public string SaveKey => StoryDirector.DeadFlag("knight");

        public void ApplyCleared() => gameObject.SetActive(false);

        /// <summary>The road to Hollowgate opens when the knight falls.</summary>
        public static event Action Slain;

        public event Action<State> StateChanged;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (target != null) targetDamageable = target.GetComponentInParent<IDamageable>();
            lastPosition = transform.position;
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
            StateChanged?.Invoke(next);
            if (next == State.Chase) CombatFocus.Set(this);
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
            bool staggered = Time.time < staggerUntil;
            bool targetAlive = targetDamageable != null && targetDamageable.IsAlive;

            switch (state)
            {
                case State.Idle:
                    if (targetAlive && CanSeeTarget()) { SetState(State.Chase); break; }
                    if (patrolPoints.Length > 0 && stateTime >= idleDuration)
                    {
                        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                        SetState(State.Patrol);
                    }
                    break;

                case State.Patrol:
                {
                    if (targetAlive && CanSeeTarget()) { SetState(State.Chase); break; }
                    Vector3 toPoint = Flat(patrolPoints[patrolIndex] - transform.position);
                    if (toPoint.magnitude < 0.8f) { SetState(State.Idle); break; }
                    desiredVelocity = toPoint.normalized * patrolSpeed;
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
                    if (distance > attackRange * 0.85f)
                        desiredVelocity = toTarget.normalized * chaseSpeed;
                    if (distance <= attackRange && Time.time >= nextAttackTime && !staggered && FacingTarget(toTarget, 30f))
                        BeginAttack();
                    break;
                }

                case State.Attack:
                    UpdateAttack(ref lookDirection, ref desiredVelocity);
                    break;
            }

            if (staggered) desiredVelocity = Vector3.zero;
            Move(desiredVelocity, lookDirection, staggered || (state == State.Attack && currentPattern != MoveKind.Charge), dt);
        }

        void RecoverAfterTargetLost()
        {
            // The knight forgets the fight and recovers, so the player can retry after respawning.
            health.ResetHealth();
            attackSequence = 0;
            SetState(patrolPoints.Length > 0 ? State.Patrol : State.Idle);
        }

        void BeginAttack()
        {
            attackResolved = false;
            currentPattern = attackSequence % 3 == 0 ? MoveKind.Melee : attackSequence % 3 == 1 ? MoveKind.Sweep : MoveKind.Charge;
            attackSequence++;
            moveWindup = currentPattern == MoveKind.Melee ? attackWindup : currentPattern == MoveKind.Sweep ? 0.95f : 1.05f;
            moveRecovery = currentPattern == MoveKind.Melee ? attackRecovery : 0.8f;
            committedDirection = transform.forward;
            nextAttackTime = Time.time + attackInterval;
            SetState(State.Attack);
            animator.SetAttackStyle(currentPattern);
            animator.PlayAttack(moveWindup, moveRecovery);
        }

        void UpdateAttack(ref Vector3 lookDirection, ref Vector3 desiredVelocity)
        {
            if (target != null && stateTime < moveWindup * 0.45f)
                committedDirection = Flat(target.position - transform.position).normalized;
            lookDirection = committedDirection;
            if (currentPattern == MoveKind.Charge && stateTime > moveWindup - 0.28f && stateTime <= moveWindup)
                desiredVelocity = committedDirection * 3.4f;

            if (!attackResolved && stateTime >= moveWindup)
            {
                attackResolved = true;
                ResolveAttackHit();
            }

            if (stateTime >= moveWindup + moveRecovery)
                SetState(State.Chase);
        }

        /// <summary>Overlap sphere in front of the knight at the moment the blade comes down.</summary>
        void ResolveAttackHit()
        {
            bool sweeping = currentPattern == MoveKind.Sweep;
            Vector3 center = transform.position + transform.forward * (sweeping ? 0.35f : attackReach * 0.65f) + Vector3.up * 1.2f;
            int count = Physics.OverlapSphereNonAlloc(center, sweeping ? 2f : attackHitRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = overlapBuffer[i];
                if (c.transform.IsChildOf(transform)) continue;
                if (sweeping && Vector3.Angle(committedDirection, Flat(c.transform.position - transform.position)) > 100f) continue;
                var damageable = c.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                Vector3 point = c.ClosestPoint(center);
                float applied = damageable.ApplyDamage(new DamageInfo(attackDamage, point, transform.forward, gameObject));
                if (applied < attackDamage * 0.5f)
                {
                    // Blocked: the blade glances off the shield and the knight reels.
                    staggerUntil = Time.time + blockedRecoil;
                    animator.PlayRecoil();
                    nextAttackTime = Mathf.Max(nextAttackTime, Time.time + blockedRecoil + 0.6f);
                }
                else if (impactEffects != null)
                {
                    impactEffects.Emit(point, -transform.forward, ImpactEffects.Kind.Flesh);
                }
                break;
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

            animator.SetLocomotion(holdPosition ? 0f : Flat(desiredVelocity).magnitude / chaseSpeed);
        }

        bool CanSeeTarget()
        {
            if (target == null) return false;
            Vector3 toTarget = target.position - transform.position;
            float distance = Flat(toTarget).magnitude;
            if (distance > detectionRadius) return false;
            if (distance > closeDetectionRadius && Vector3.Angle(transform.forward, Flat(toTarget)) > fieldOfView * 0.5f)
                return false;

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 targetEye = target.position + Vector3.up * 1.5f;
            if (Physics.Linecast(eye, targetEye, out RaycastHit hit, sightMask, QueryTriggerInteraction.Ignore))
            {
                bool hitSelf = hit.collider.transform.IsChildOf(transform);
                bool hitTarget = hit.collider.transform.IsChildOf(target);
                if (!hitSelf && !hitTarget) return false;
            }
            return true;
        }

        bool FacingTarget(Vector3 toTarget, float tolerance) =>
            toTarget.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, toTarget) <= tolerance;

        void OnDamaged(DamageInfo info, float amount)
        {
            if (state == State.Dead) return;
            CombatFocus.Set(this);
            animator.PlayHit(info.Direction);
            knockback = Flat(info.Direction).normalized * knockbackSpeed;

            // Committed attacks have hyper-armor; otherwise the knight flinches.
            if (state != State.Attack) staggerUntil = Time.time + staggerDuration;
            if (state == State.Idle || state == State.Patrol) SetState(State.Chase);
        }

        void OnDied(Health _)
        {
            SetState(State.Dead);
            controller.enabled = false;
            animator.PlayDeath();
            GameMessages.Notify(GameText.KnightDefeated);
            Slain?.Invoke();
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = Color.cyan;
            if (patrolPoints != null)
                foreach (var p in patrolPoints) Gizmos.DrawSphere(p, 0.3f);
        }
    }
}
