using System;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Player;
using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>
    /// Player sword attacks and shield blocking.
    /// Attacks sphere-cast from the camera at the strike moment of the swing; blocking mitigates frontal damage
    /// as an <see cref="IDamageFilter"/> on the player's <see cref="Health"/>, paid for with stamina.
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour, IDamageFilter
    {
        [Header("References")]
        [SerializeField] PlayerInputHub inputSource;
        [SerializeField] Health health;
        [SerializeField] Stamina stamina;
        [SerializeField] FirstPersonController controller;
        [SerializeField] FirstPersonWeaponAnimator weapons;
        [SerializeField] Transform aimOrigin;
        [SerializeField, Optional] CameraEffects cameraEffects;
        [SerializeField, Optional] ImpactEffects impactEffects;

        [Header("Sword")]
        [SerializeField] float damage = 25f;
        [SerializeField] float range = 2.5f;
        [SerializeField] float hitRadius = 0.4f;
        [Tooltip("Total duration of the swing animation.")]
        [SerializeField] float attackDuration = 0.5f;
        [Tooltip("Minimum time between the starts of two attacks.")]
        [SerializeField] float attackCooldown = 0.6f;
        [SerializeField] float attackStaminaCost = 12f;
        [SerializeField] bool holdToRepeat = true;
        [SerializeField] float attackMoveSpeedMultiplier = 0.8f;
        [SerializeField] LayerMask hitMask = ~0;

        [Header("Shield")]
        [Range(0f, 1f)]
        [SerializeField] float blockDamageReduction = 0.85f;
        [Tooltip("Max angle between facing and attacker for a successful block.")]
        [SerializeField] float blockAngle = 75f;
        [Tooltip("Stamina spent per point of damage absorbed.")]
        [SerializeField] float blockStaminaPerDamage = 1.2f;
        [SerializeField] float blockMoveSpeedMultiplier = 0.55f;

        readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        IPlayerInput input;
        float nextAttackTime;
        float attackStartTime = -999f;
        bool hitPending;
        int comboIndex;

        public bool IsBlocking { get; private set; }
        public bool IsAttacking => Time.time < attackStartTime + attackDuration;
        public float Damage => damage;
        public float Range => range;

        /// <summary>Raised when the sword damages something: (target, damage applied).</summary>
        public event Action<IDamageable, float> HitLanded;
        /// <summary>Raised when the shield absorbs a blow: (damage absorbed).</summary>
        public event Action<float> Blocked;

        void Awake() => input = inputSource;

        void OnDisable()
        {
            IsBlocking = false;
            hitPending = false;
            if (weapons != null) weapons.SetBlocking(false);
            if (controller != null) controller.SpeedMultiplier = 1f;
        }

        void Update()
        {
            if (input == null || (health != null && !health.IsAlive)) return;

            bool attacking = IsAttacking;
            IsBlocking = input.BlockHeld && !attacking && stamina.Current > 0f;
            weapons.SetBlocking(IsBlocking);
            controller.SpeedMultiplier = IsBlocking ? blockMoveSpeedMultiplier : attacking ? attackMoveSpeedMultiplier : 1f;

            if (!IsBlocking && Time.time >= nextAttackTime && stamina.Current > 0f)
            {
                if (input.ConsumeAttack() || (holdToRepeat && input.AttackHeld))
                    StartAttack();
            }

            float hitTime = attackStartTime + attackDuration * (FirstPersonWeaponAnimator.WindupEnd + FirstPersonWeaponAnimator.StrikeEnd) * 0.5f;
            if (hitPending && Time.time >= hitTime)
            {
                hitPending = false;
                PerformHit();
            }
        }

        void StartAttack()
        {
            stamina.Drain(attackStaminaCost);
            attackStartTime = Time.time;
            nextAttackTime = Time.time + attackCooldown;
            hitPending = true;
            weapons.PlaySlash(comboIndex++, attackDuration);
        }

        /// <summary>
        /// Sphere-casts along the camera forward. The nearest valid collider wins: a damageable takes damage,
        /// solid geometry stops the blade.
        /// </summary>
        void PerformHit()
        {
            Vector3 origin = aimOrigin.position;
            Vector3 direction = aimOrigin.forward;
            int count = Physics.SphereCastNonAlloc(origin, hitRadius, direction, hitBuffer, range, hitMask, QueryTriggerInteraction.Ignore);

            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = hitBuffer[i];
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = i;
                }
            }
            if (best < 0) return;

            RaycastHit target = hitBuffer[best];
            // Colliders already overlapping the sphere at the cast origin report distance 0 and no point.
            Vector3 point = target.distance <= 0f ? target.collider.ClosestPoint(origin) : target.point;
            Vector3 normal = target.distance <= 0f ? -direction : target.normal;

            var damageable = target.collider.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                float applied = damageable.ApplyDamage(new DamageInfo(damage, point, direction, gameObject));
                weapons.PlayHitImpact();
                if (cameraEffects != null) cameraEffects.AddTrauma(0.18f);
                if (impactEffects != null) impactEffects.Emit(point, normal, ImpactEffects.Kind.Flesh);
                HitLanded?.Invoke(damageable, applied);
            }
            else if (damageable == null)
            {
                weapons.PlayWallImpact();
                if (cameraEffects != null) cameraEffects.AddTrauma(0.1f);
                if (impactEffects != null) impactEffects.Emit(point, normal, ImpactEffects.Kind.Stone);
            }
        }

        public float FilterDamage(in DamageInfo info, float amount)
        {
            if (!IsBlocking) return amount;

            Vector3 toAttacker = info.Source != null ? info.Source.transform.position - transform.position : -info.Direction;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f || Vector3.Angle(transform.forward, toAttacker) > blockAngle)
                return amount;

            float absorbed = amount * blockDamageReduction;
            float cost = absorbed * blockStaminaPerDamage;
            float paid = stamina.Drain(cost);
            if (paid < cost)
            {
                // Guard broken: only the part paid for is absorbed and the shield drops.
                absorbed *= cost > 0f ? paid / cost : 0f;
                IsBlocking = false;
                weapons.SetBlocking(false);
            }

            weapons.PlayBlockImpact(amount / 20f);
            if (cameraEffects != null) cameraEffects.AddTrauma(0.12f);
            if (impactEffects != null) impactEffects.Emit(info.Point, -info.Direction, ImpactEffects.Kind.Metal);
            Blocked?.Invoke(absorbed);
            return amount - absorbed;
        }
    }
}
