using AshenRealm.AI;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Story;
using AshenRealm.Player;
using AshenRealm.World;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>
    /// The class kit beyond sword and shield: two abilities paid with focus (Graveborne's SP), each on a cooldown because
    /// real time has no turns to spend, plus the potion. Focus trickles back on its own and returns faster with kills.
    /// </summary>
    public sealed class PlayerAbilities : MonoBehaviour
    {
        const float BackstabDamage = 30f;
        const float BackstabBehindMultiplier = 3f;
        const float SmiteDamage = 32f;
        const float DrainDamage = 26f;
        const float MendAmount = 38f;
        const int FocusPerKill = 1;

        [SerializeField] PlayerInputHub inputSource;
        [SerializeField] PlayerCombat combat;
        [SerializeField] Health health;
        [SerializeField] StatusEffects status;
        [SerializeField] Transform aimOrigin;
        [SerializeField] ImpactEffects visualEffects;
        [SerializeField] ZoneManager zones;

        IPlayerInput input;
        ClassDefinition definition;
        float focus;
        int gearFocus;
        int growthFocus;
        int activeTier = 1;
        readonly int[] tiers = { 1, 1, 1, 1 };
        readonly int[] equipped = { 0, 1 };
        readonly float[] readyAt = new float[AbilityBook.KnownCount];
        readonly Collider[] nearby = new Collider[64];
        readonly RaycastHit[] traces = new RaycastHit[32];
        readonly HashSet<Health> hitOnce = new HashSet<Health>();
        Zone lastZone;

        public float Focus => focus;
        public float MaxFocus => definition != null ? Mathf.Max(1, definition.MaxFocus + gearFocus + growthFocus) : 1f;
        public float FocusNormalized => focus / MaxFocus;

        void Awake() => input = inputSource;

        void OnEnable() => EnemyAI.Slain += OnEnemySlain;
        void OnDisable() => EnemyAI.Slain -= OnEnemySlain;

        void OnEnemySlain(EnemyAI _) => AddFocus(FocusPerKill);

        public void ApplyClass(ClassDefinition chosen)
        {
            definition = chosen;
            focus = chosen.MaxFocus;
            for (int i = 0; i < readyAt.Length; i++) readyAt[i] = 0f;
            StopAllCoroutines();
        }

        /// <summary>What the weapon in hand and the relic add to the pool.</summary>
        public void ApplyGear(int bonusFocus)
        {
            gearFocus = bonusFocus;
            focus = Mathf.Min(focus, MaxFocus);
        }

        public void ApplyProgression(int bonusFocus, int firstTier, int secondTier)
        {
            growthFocus = bonusFocus;
            tiers[0] = Mathf.Clamp(firstTier, 1, 3);
            tiers[1] = Mathf.Clamp(secondTier, 1, 3);
            focus = Mathf.Min(focus, MaxFocus);
        }

        public void ApplyProgression(int bonusFocus, int[] knownTiers, int[] loadout)
        {
            growthFocus = bonusFocus;
            for (int i = 0; i < tiers.Length; i++) tiers[i] = Mathf.Clamp(knownTiers[i], 1, 3);
            for (int i = 0; i < equipped.Length; i++) equipped[i] = Mathf.Clamp(loadout[i], 0, AbilityBook.KnownCount - 1);
            focus = Mathf.Min(focus, MaxFocus);
        }

        public void AddFocus(float amount) => focus = Mathf.Min(MaxFocus, focus + amount);

        public void SetFocus(float amount) => focus = Mathf.Clamp(amount, 0f, MaxFocus);

        /// <summary>0 when the slot is usable, 1 right after use.</summary>
        public float CooldownFraction(int slot)
        {
            AbilityDefinition ability = Slot(slot);
            return ability == null ? 0f : Mathf.Clamp01((readyAt[equipped[slot]] - Time.time) / ability.Cooldown);
        }

        public bool Affordable(int slot)
        {
            AbilityDefinition ability = Slot(slot);
            return ability != null && focus >= ability.Cost;
        }

        public AbilityDefinition ActiveAbility(int slot) => slot < 0 || slot >= 2 ? null : AbilityBook.At(definition, equipped[slot]);
        public AbilityDefinition AbilityAt(int index) => AbilityBook.At(definition, index);
        public int EquippedAbilityIndex(int slot) => equipped[Mathf.Clamp(slot, 0, 1)];
        AbilityDefinition Slot(int slot) => ActiveAbility(slot);

        void Update()
        {
            if (zones.Current != lastZone) { StopAllCoroutines(); lastZone = zones.Current; }
            if (definition == null || !health.IsAlive) { StopAllCoroutines(); return; }
            focus = Mathf.Min(MaxFocus, focus + definition.FocusPerSecond * Time.deltaTime);

            for (int slot = 0; slot < 2; slot++)
                if (input.ConsumeAbility(slot)) TryUse(slot);
            if (input.ConsumePotion()) DrinkPotion();
        }

        /// <summary>Spends focus and fires the ability, if it is ready.</summary>
        public bool TryUse(int slot)
        {
            AbilityDefinition ability = Slot(slot);
            if (ability == null || !combat.Armed || !health.IsAlive || status.Stunned) return false;
            int known = equipped[slot];
            if (Time.time < readyAt[known]) return false;
            if (focus < ability.Cost)
            {
                GameMessages.Notify(GameText.NoFocus);
                return false;
            }

            focus -= ability.Cost;
            readyAt[known] = Time.time + ability.Cooldown;
            activeTier = tiers[known];
            Fire(ability.Effect);
            if (visualEffects != null) visualEffects.EmitAbility(aimOrigin.position + aimOrigin.forward * 0.75f, EffectColor(ability.Effect), 0.25f);
            return true;
        }

        void Fire(AbilityEffect effect)
        {
            switch (effect)
            {
                case AbilityEffect.ShieldBash:
                {
                    combat.PlayAbilitySwing();
                    GameObject foe = combat.Strike(Power(14f), 3f, 0.55f);
                    if (foe != null && foe.TryGetComponent(out StatusEffects s)) s.ApplyStun(2.2f);
                    break;
                }
                case AbilityEffect.Rally:
                    status.ApplyFury(8f + (activeTier - 1) * 3f);
                    status.ApplyRegen(Power(5f), 6f);
                    GameMessages.Notify(GameText.RallyMessage);
                    break;
                case AbilityEffect.Backstab:
                    combat.PlayAbilitySwing();
                    combat.Strike(Power(BackstabDamage) * (FoeFacingAway() ? BackstabBehindMultiplier : 1f), 2.8f, 0.45f);
                    break;
                case AbilityEffect.PoisonBlade:
                    combat.CoatBlade(10f + (activeTier - 1) * 5f);
                    GameMessages.Notify(GameText.PoisonMessage);
                    break;
                case AbilityEffect.LifeDrain:
                {
                    combat.PlayAbilitySwing();
                    float before = health.Current;
                    GameObject foe = combat.Strike(Power(DrainDamage), 9f, 0.6f);
                    if (foe != null) health.Heal(Power(DrainDamage));
                    if (foe == null && Mathf.Approximately(before, health.Current)) GameMessages.Notify(GameText.NoTarget);
                    break;
                }
                case AbilityEffect.Hex:
                {
                    combat.PlayAbilitySwing();
                    GameObject foe = combat.Strike(Power(6f), 11f, 0.6f);
                    if (foe != null && foe.TryGetComponent(out StatusEffects s)) s.ApplyWeaken(9f);
                    else GameMessages.Notify(GameText.NoTarget);
                    break;
                }
                case AbilityEffect.Smite:
                {
                    combat.PlayAbilitySwing();
                    GameObject foe = FoeInReach(3.4f);
                    bool undead = foe != null && foe.TryGetComponent(out EnemyAI ai) && ai.Definition.Undead;
                    combat.Strike(Power(undead ? SmiteDamage * 2f : SmiteDamage), 3.4f, 0.5f);
                    break;
                }
                case AbilityEffect.Mend:
                    health.Heal(Power(MendAmount));
                    GameMessages.Notify(GameText.MendMessage);
                    break;
                case AbilityEffect.BoneSpear:
                    combat.PlayAbilitySwing();
                    combat.Strike(Power(36f), 16f, 0.45f);
                    break;
                case AbilityEffect.BoneArmor:
                    status.ApplyWard(8f + (activeTier - 1) * 4f);
                    health.Heal(Power(10f));
                    break;
                case AbilityEffect.AcidFlask:
                {
                    combat.PlayAbilitySwing();
                    GameObject foe = combat.Strike(Power(22f), 12f, 0.7f);
                    if (foe != null && foe.TryGetComponent(out StatusEffects s)) s.ApplyPoison(Power(5f), 5f);
                    break;
                }
                case AbilityEffect.HealingBrew:
                    health.Heal(Power(28f));
                    status.ApplyRegen(Power(3f), 5f);
                    break;
                case AbilityEffect.Whirlwind:
                    combat.PlayAbilitySwing();
                    Area(transform.position, 3.6f + activeTier * 0.25f, Power(27f), 360f, 0.35f, 0f, false);
                    Ring(transform.position, 3.6f, EffectColor(effect));
                    break;
                case AbilityEffect.IronSkin:
                    status.ApplyWard(7f + activeTier * 2f);
                    status.ApplyFury(2f + activeTier);
                    GetComponent<Stamina>()?.Refill();
                    Ring(transform.position, 1.2f, EffectColor(effect));
                    break;
                case AbilityEffect.FanOfKnives:
                {
                    combat.PlayAbilitySwing();
                    int count = 1 + activeTier * 2;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 direction = Quaternion.AngleAxis(Mathf.Lerp(-16f, 16f, i / (float)(count - 1)), Vector3.up) * aimOrigin.forward;
                        Health victim = Trace(direction, 12f, out Vector3 point);
                        Hurt(victim, Power(11f), 0f, 0f);
                        visualEffects?.EmitAbilityRay(aimOrigin.position + aimOrigin.forward * 0.5f, point, EffectColor(effect));
                    }
                    break;
                }
                case AbilityEffect.Caltrops:
                    StartCoroutine(Field(transform.position, 2.9f, 7f + activeTier, Power(4f), effect, false));
                    break;
                case AbilityEffect.FrostLance:
                {
                    combat.PlayAbilitySwing();
                    Health victim = Trace(aimOrigin.forward, 17f, out Vector3 point);
                    Hurt(victim, Power(32f), 1.1f + activeTier * 0.3f, 4f);
                    visualEffects?.EmitAbilityRay(aimOrigin.position + aimOrigin.forward * 0.5f, point, EffectColor(effect));
                    break;
                }
                case AbilityEffect.ChainLightning:
                {
                    combat.PlayAbilitySwing();
                    Health victim = Trace(aimOrigin.forward, 15f, out _);
                    if (victim != null) StartCoroutine(Chain(victim, 2 + activeTier, Power(25f), definition, zones.Current));
                    else GameMessages.Notify(GameText.NoTarget);
                    break;
                }
                case AbilityEffect.Sanctuary:
                    StartCoroutine(Field(transform.position, 3.3f, 6f + activeTier * 2f, Power(5f), effect, true));
                    break;
                case AbilityEffect.Consecration:
                    StartCoroutine(Field(GroundTarget(9f), 3.8f, 5f + activeTier, Power(7f), effect, false));
                    break;
                case AbilityEffect.BoneNova:
                    combat.PlayAbilitySwing();
                    Area(transform.position, 4.3f, Power(26f), 360f, 0.9f + activeTier * 0.3f, 0f, false);
                    Ring(transform.position, 4.3f, EffectColor(effect));
                    break;
                case AbilityEffect.SoulHarvest:
                {
                    combat.PlayAbilitySwing();
                    float dealt = Area(transform.position, 5f, Power(24f), 360f, 0f, 4f, false);
                    health.Heal(dealt * 0.35f);
                    AddFocus(Mathf.Min(3f, dealt / 35f));
                    Ring(transform.position, 5f, EffectColor(effect));
                    break;
                }
                case AbilityEffect.FireBomb:
                {
                    combat.PlayAbilitySwing();
                    Vector3 at = GroundTarget(12f);
                    Area(at, 3.3f, Power(29f), 360f, 0f, 0f, false);
                    StartCoroutine(Field(at, 3.3f, 3f + activeTier, Power(5f), effect, false));
                    break;
                }
                case AbilityEffect.FlashPowder:
                    combat.PlayAbilitySwing();
                    Area(transform.position, 6.5f, Power(8f), 105f, 1.8f + activeTier * 0.4f, 5f, false);
                    Ring(transform.position + aimOrigin.forward * 2f, 2.6f, EffectColor(effect));
                    break;
            }
        }

        public static Color EffectColor(AbilityEffect effect)
        {
            switch (effect)
            {
                case AbilityEffect.FrostLance: return new Color(0.45f, 0.85f, 1f);
                case AbilityEffect.ChainLightning: return new Color(0.65f, 0.5f, 1f);
                case AbilityEffect.Caltrops: case AbilityEffect.PoisonBlade: case AbilityEffect.AcidFlask: return new Color(0.4f, 0.85f, 0.35f);
                case AbilityEffect.BoneNova: case AbilityEffect.SoulHarvest: case AbilityEffect.BoneSpear: case AbilityEffect.BoneArmor: return new Color(0.7f, 0.55f, 1f);
                case AbilityEffect.FireBomb: return new Color(1f, 0.4f, 0.12f);
                case AbilityEffect.FlashPowder: return new Color(1f, 0.87f, 0.62f);
                default: return new Color(1f, 0.74f, 0.35f);
            }
        }

        Health Trace(Vector3 direction, float range, out Vector3 point)
        {
            point = aimOrigin.position + direction * range;
            int count = Physics.SphereCastNonAlloc(aimOrigin.position, 0.17f, direction, traces, range, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float distance = float.MaxValue;
            Health found = null;
            for (int i = 0; i < count; i++)
            {
                Health candidate = traces[i].collider.GetComponentInParent<Health>();
                if (candidate == health || traces[i].distance >= distance) continue;
                distance = traces[i].distance;
                point = traces[i].point;
                found = IsFoe(candidate) ? candidate : null;
            }
            return found;
        }

        bool IsFoe(Health candidate) => candidate != null && candidate != health && candidate.IsAlive &&
            (candidate.GetComponent<EnemyAI>() != null || candidate.GetComponent<CursedKnightAI>() != null);

        bool Visible(Vector3 from, Health victim)
        {
            Vector3 to = victim.transform.position + Vector3.up - from;
            int count = Physics.RaycastNonAlloc(from, to.normalized, traces, to.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Health first = victim;
            for (int i = 0; i < count; i++)
            {
                Health body = traces[i].collider.GetComponentInParent<Health>();
                if (body == health || traces[i].distance >= nearest) continue;
                nearest = traces[i].distance;
                first = body;
            }
            return first == victim;
        }

        float Hurt(Health victim, float amount, float stun, float weaken)
        {
            if (!IsFoe(victim)) return 0f;
            float dealt = victim.ApplyDamage(new DamageInfo(amount, victim.transform.position + Vector3.up,
                victim.transform.position - transform.position, gameObject));
            if (victim.TryGetComponent(out StatusEffects conditions))
            {
                if (stun > 0f) conditions.ApplyStun(stun);
                if (weaken > 0f) conditions.ApplyWeaken(weaken);
            }
            return dealt;
        }

        float Area(Vector3 centre, float radius, float amount, float cone, float stun, float weaken, bool undeadBonus, bool poison = false)
        {
            int count = Physics.OverlapSphereNonAlloc(centre + Vector3.up * 0.8f, radius, nearby, ~0, QueryTriggerInteraction.Ignore);
            hitOnce.Clear();
            float dealt = 0f;
            for (int i = 0; i < count; i++)
            {
                Health victim = nearby[i].GetComponentInParent<Health>();
                if (!IsFoe(victim) || !hitOnce.Add(victim)) continue;
                Vector3 direction = victim.transform.position - transform.position;
                direction.y = 0f;
                Vector3 facing = aimOrigin.forward; facing.y = 0f;
                if (cone < 360f && Vector3.Angle(facing, direction) > cone * 0.5f) continue;
                if (!Visible(centre + Vector3.up, victim)) continue;
                bool undead = victim.TryGetComponent(out EnemyAI ai) && ai.Definition.Undead;
                dealt += Hurt(victim, amount * (undeadBonus && undead ? 1.7f : 1f), stun, weaken);
                if (poison && victim.TryGetComponent(out StatusEffects conditions)) conditions.ApplyPoison(amount * 0.6f, 3f);
            }
            return dealt;
        }

        Vector3 GroundTarget(float range)
        {
            Trace(aimOrigin.forward, range, out Vector3 point);
            point -= aimOrigin.forward * 0.35f;
            if (Physics.Raycast(point + Vector3.up * 1.8f, Vector3.down, out RaycastHit ground, 8f, ~0, QueryTriggerInteraction.Ignore))
                return ground.point + Vector3.up * 0.06f;
            return transform.position + transform.forward * Mathf.Min(range, 4f);
        }

        void Ring(Vector3 centre, float radius, Color color) => visualEffects?.EmitAbility(centre + Vector3.up * 0.15f, color, radius);

        IEnumerator Field(Vector3 centre, float radius, float seconds, float power, AbilityEffect effect, bool healing)
        {
            Zone startedIn = zones.Current;
            ClassDefinition startedAs = definition;
            for (float elapsed = 0f; elapsed < seconds; elapsed += 1f)
            {
                if (!health.IsAlive || zones.Current != startedIn || definition != startedAs) yield break;
                Ring(centre, radius, EffectColor(effect));
                if (healing)
                {
                    if ((transform.position - centre).sqrMagnitude <= radius * radius)
                    {
                        health.Heal(power);
                        status.ApplyWard(1.2f);
                    }
                }
                else
                    Area(centre, radius, power, 360f, effect == AbilityEffect.Caltrops ? 0.28f : 0f,
                        effect == AbilityEffect.Consecration ? 1.2f : 0f, effect == AbilityEffect.Consecration, effect == AbilityEffect.Caltrops);
                yield return new WaitForSeconds(1f);
            }
        }

        IEnumerator Chain(Health first, int jumps, float power, ClassDefinition startedAs, Zone startedIn)
        {
            var struck = new HashSet<Health>();
            Health current = first;
            Vector3 from = aimOrigin.position;
            for (int i = 0; i < jumps && current != null; i++)
            {
                if (!health.IsAlive || zones.Current != startedIn || definition != startedAs) yield break;
                Vector3 to = current.transform.position + Vector3.up;
                visualEffects?.EmitAbilityRay(from, to, EffectColor(AbilityEffect.ChainLightning));
                Hurt(current, power * Mathf.Pow(0.85f, i), 0.2f, 0f);
                struck.Add(current);
                from = to;
                int count = Physics.OverlapSphereNonAlloc(to, 5.5f, nearby, ~0, QueryTriggerInteraction.Ignore);
                Health next = null;
                float nearest = float.MaxValue;
                for (int n = 0; n < count; n++)
                {
                    Health candidate = nearby[n].GetComponentInParent<Health>();
                    if (!IsFoe(candidate) || struck.Contains(candidate) || !Visible(to, candidate)) continue;
                    float distance = (candidate.transform.position - to).sqrMagnitude;
                    if (distance < nearest) { nearest = distance; next = candidate; }
                }
                current = next;
                yield return new WaitForSeconds(0.12f);
            }
        }

        float Power(float amount) => amount * combat.AbilityMultiplier * (1f + (activeTier - 1) * 0.5f);

        public void DrinkPotion()
        {
            GameSession session = GameSession.Instance;
            if (session == null) return;
            if (session.Potions <= 0)
            {
                GameMessages.Notify(GameText.NoPotion);
                return;
            }
            if (health.Normalized >= 1f)
            {
                GameMessages.Notify(GameText.FullHealth);
                return;
            }
            session.AddPotions(-1);
            health.Heal(GameSession.PotionHeal);
        }

        GameObject FoeInReach(float reach)
        {
            if (!Physics.SphereCast(aimOrigin.position, 0.5f, aimOrigin.forward, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore)) return null;
            var enemy = hit.collider.GetComponentInParent<EnemyAI>();
            return enemy != null ? enemy.gameObject : null;
        }

        /// <summary>True when the foe under the crosshair has its back to the player, or has not noticed yet.</summary>
        bool FoeFacingAway()
        {
            GameObject foe = FoeInReach(3f);
            if (foe == null) return false;
            Vector3 toPlayer = transform.position - foe.transform.position;
            toPlayer.y = 0f;
            return Vector3.Angle(foe.transform.forward, toPlayer) > 110f;
        }
    }
}
