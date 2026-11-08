using System;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Player;
using AshenRealm.Story;
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
        [SerializeField, Optional] StatusEffects status;
        [SerializeField, Optional] ProjectileLibrary projectiles;

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

        // Graveborne's points in real-time numbers, scaled like the class sheets: 14 attack on the card is 25 damage here.
        const float DamagePerPoint = 1.5f;
        const float SpeedPerPoint = 0.02f;
        const float AbilityPowerPerPoint = 0.04f;

        readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        IPlayerInput input;
        float nextAttackTime;
        float attackStartTime = -999f;
        bool hitPending;
        int comboIndex;
        bool ranged;
        float moveMultiplier = 1f;
        float poisonUntil;
        float classDamage = 25f;
        float gearSpeed = 1f;
        float lifesteal;
        int gearMagic;
        int gearAttackPoints, gearSpeedPoints;
        int growthAttack, growthMagic, growthDefense, growthSpeed;

        public bool IsBlocking { get; private set; }
        /// <summary>False under a roof: the weapons stay at the door, and nothing swings, blocks or casts.</summary>
        public bool Armed { get; private set; } = true;
        public bool IsAttacking => Time.time < attackStartTime + attackDuration;
        public float Damage => damage;
        /// <summary>Sharper weapons from the forge and whatever fury or weakness the player is under.</summary>
        public float OutgoingMultiplier => (1f + 0.12f * (GameSession.Instance != null ? GameSession.Instance.WeaponLevel : 0)) * (status != null ? status.DamageMultiplier : 1f);
        public float Range => range;
        /// <summary>What the gear adds to the class's abilities.</summary>
        public float AbilityMultiplier => Mathf.Max(0.1f, 1f + (gearMagic + growthMagic) * AbilityPowerPerPoint);

        /// <summary>Raised when the sword damages something: (target, damage applied).</summary>
        public event Action<IDamageable, float> HitLanded;
        /// <summary>Raised when the shield absorbs a blow: (damage absorbed).</summary>
        public event Action<float> Blocked;
        /// <summary>Raised when the player puts the weapons down or takes them up.</summary>
        public event Action ArmedChanged;

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
            if (!Armed)
            {
                input.ConsumeAttack();
                controller.SpeedMultiplier = moveMultiplier * gearSpeed;
                return;
            }

            bool attacking = IsAttacking;
            IsBlocking = input.BlockHeld && !attacking && stamina.Current > 0f;
            weapons.SetBlocking(IsBlocking);
            float stance = IsBlocking ? blockMoveSpeedMultiplier : attacking ? attackMoveSpeedMultiplier : 1f;
            controller.SpeedMultiplier = stance * moveMultiplier * gearSpeed * (status != null && status.Stunned ? 0.35f : 1f);

            if (!IsBlocking && Time.time >= nextAttackTime && stamina.Current > 0f)
            {
                if (input.ConsumeAttack() || (holdToRepeat && input.AttackHeld))
                    StartAttack();
            }

            float hitTime = attackStartTime + attackDuration * (FirstPersonWeaponAnimator.WindupEnd + FirstPersonWeaponAnimator.StrikeEnd) * 0.5f;
            if (hitPending && Time.time >= hitTime)
            {
                hitPending = false;
                if (ranged) ThrowBolt();
                else PerformHit();
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

        /// <summary>Puts the weapons down at a door, or takes them up again.</summary>
        public void SetArmed(bool armed)
        {
            if (Armed == armed) return;
            Armed = armed;
            if (!armed)
            {
                IsBlocking = false;
                hitPending = false;
                weapons.SetBlocking(false);
            }
            weapons.SetPutAway(!armed);
            ArmedChanged?.Invoke();
        }

        /// <summary>Applies the class's opening stats. Called once, when the class is chosen.</summary>
        public void ApplyClass(ClassDefinition definition)
        {
            classDamage = definition.Damage;
            damage = definition.Damage;
            attackCooldown = definition.AttackCooldown;
            attackDuration = definition.AttackDuration;
            blockDamageReduction = definition.BlockReduction;
            moveMultiplier = definition.MoveMultiplier;
            ranged = definition.Ranged;
        }

        /// <summary>What the weapon in hand and the relic add. Bolts draw on magic, swings on attack.</summary>
        public void ApplyGear(int attack, int magic, int speed, float lifestealShare)
        {
            gearAttackPoints = attack;
            gearSpeedPoints = speed;
            damage = DamageWith(attack, magic);
            gearSpeed = Mathf.Max(0.2f, 1f + (speed + growthSpeed) * SpeedPerPoint);
            gearMagic = magic;
            lifesteal = lifestealShare;
        }

        public void ApplyProgression(int attack, int magic, int defense, int speed)
        {
            growthAttack = attack;
            growthMagic = magic;
            growthDefense = defense;
            growthSpeed = speed;
            damage = DamageWith(gearAttackPoints, gearMagic);
            gearSpeed = Mathf.Max(0.2f, 1f + (gearSpeedPoints + growthSpeed) * SpeedPerPoint);
        }

        /// <summary>The damage of one blow with these points, before forge work or fury.</summary>
        public float DamageWith(int attack, int magic) => Mathf.Max(1f, classDamage + (ranged ? magic + growthMagic : attack + growthAttack) * DamagePerPoint);

        /// <summary>For the next few seconds every landed blow also poisons (Poison Blade).</summary>
        public void CoatBlade(float seconds) => poisonUntil = Time.time + seconds;

        /// <summary>Plays a swing for an ability without spending stamina or starting a normal hit.</summary>
        public void PlayAbilitySwing()
        {
            attackStartTime = Time.time;
            nextAttackTime = Time.time + attackCooldown;
            weapons.PlaySlash(comboIndex++, attackDuration);
        }

        void ThrowBolt()
        {
            if (projectiles == null) return;
            Vector3 origin = aimOrigin.position + aimOrigin.forward * 0.8f + aimOrigin.right * 0.15f - aimOrigin.up * 0.1f;
            CharacterClass id = GameSession.Instance.Class.Id;
            BoltKind kind = id == CharacterClass.Necromancer ? BoltKind.Shadow : id == CharacterClass.Alchemist ? BoltKind.Venom : BoltKind.Fire;
            EnemyMove effect = id == CharacterClass.Alchemist ? new EnemyMove { PoisonDps = 3f, PoisonTime = 4f } : null;
            projectiles.Launch(kind, origin, AimPoint() - origin, damage * OutgoingMultiplier, gameObject, true, effect, Drink);
        }

        /// <summary>The Bloodstone's share of a blow that landed.</summary>
        void Drink(float dealt)
        {
            if (lifesteal > 0f) health.Heal(dealt * lifesteal);
        }

        /// <summary>Where the crosshair lands, so a bolt thrown from the hand flies to it.</summary>
        Vector3 AimPoint()
        {
            const float far = 30f;
            return Physics.Raycast(aimOrigin.position, aimOrigin.forward, out RaycastHit hit, far, hitMask, QueryTriggerInteraction.Ignore)
                ? hit.point : aimOrigin.position + aimOrigin.forward * far;
        }

        void PerformHit() => Strike(damage, range, hitRadius);

        /// <summary>
        /// Sphere-casts along the camera forward. The nearest valid collider wins: a damageable takes damage,
        /// solid geometry stops the blade. Returns the foe struck, if any, for an ability's extra effects.
        /// </summary>
        public GameObject Strike(float amount, float reach, float radius)
        {
            Vector3 origin = aimOrigin.position;
            Vector3 direction = aimOrigin.forward;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, hitBuffer, reach, hitMask, QueryTriggerInteraction.Ignore);

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
            if (best < 0) return null;

            RaycastHit target = hitBuffer[best];
            // Colliders already overlapping the sphere at the cast origin report distance 0 and no point.
            Vector3 point = target.distance <= 0f ? target.collider.ClosestPoint(origin) : target.point;
            Vector3 normal = target.distance <= 0f ? -direction : target.normal;

            var damageable = target.collider.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                float applied = damageable.ApplyDamage(new DamageInfo(amount * OutgoingMultiplier, point, direction, gameObject));
                weapons.PlayHitImpact();
                if (cameraEffects != null) cameraEffects.AddTrauma(0.18f);
                if (impactEffects != null) impactEffects.Emit(point, normal, ImpactEffects.Kind.Flesh);
                HitLanded?.Invoke(damageable, applied);
                Drink(applied);

                var struck = ((Component)damageable).gameObject;
                if (Time.time < poisonUntil && struck.TryGetComponent(out StatusEffects venom)) venom.ApplyPoison(5f, 5f);
                return struck;
            }
            if (damageable == null)
            {
                weapons.PlayWallImpact();
                if (cameraEffects != null) cameraEffects.AddTrauma(0.1f);
                if (impactEffects != null) impactEffects.Emit(point, normal, ImpactEffects.Kind.Stone);
            }
            return null;
        }

        public float FilterDamage(in DamageInfo info, float amount)
        {
            amount /= Mathf.Max(0.2f, 1f + growthDefense * 0.025f);
            if (!IsBlocking || info.Source == null) return amount; // poison ignores the shield

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
