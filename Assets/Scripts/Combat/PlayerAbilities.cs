using System.Collections;
using System.Collections.Generic;
using AshenRealm.AI;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>Workbook tiers drive real-time deliveries; progression owns learning, recipes and Q/R.</summary>
    public sealed class PlayerAbilities : MonoBehaviour
    {
        [SerializeField] PlayerInputHub inputSource;
        [SerializeField] PlayerCombat combat;
        [SerializeField] Health health;
        [SerializeField] StatusEffects status;
        [SerializeField] Transform aimOrigin;
        [SerializeField] ImpactEffects visualEffects;
        [SerializeField] ZoneManager zones;
        readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();
        readonly Collider[] nearby = new Collider[64];
        readonly RaycastHit[] traces = new RaycastHit[32];
        readonly HashSet<Health> unique = new HashSet<Health>();
        readonly List<Projectile> bolts = new List<Projectile>();
        readonly List<Coroutine> fields = new List<Coroutine>();
        readonly int[] equipped = { -1, -1 };
        int[] tiers = new int[0];
        IPlayerInput input;
        ClassDefinition definition;
        PlayerProgression growth;
        CharacterController capsule;
        float focus;
        int gearFocus, growthFocus, generation;
        Zone lastZone;
        struct Cast
        {
            public AbilityDefinition Ability;
            public AbilityTierDefinition Tier;
            public float Factor, Bonus;
            public int Generation;
        }
        public float Focus => focus;
        public float MaxFocus => definition == null ? 1f : Mathf.Max(1, definition.MaxFocus + gearFocus + growthFocus);
        public float FocusNormalized => focus / MaxFocus;
        public bool IsInTown => zones != null && zones.CurrentIndex >= ZoneNumbers.City && zones.CurrentIndex <= ZoneNumbers.Chapel;
        void Awake() { input = inputSource; growth = GetComponent<PlayerProgression>(); capsule = GetComponent<CharacterController>(); }
        void OnEnable() => EnemyAI.Slain += OnEnemySlain;
        void OnDisable() { EnemyAI.Slain -= OnEnemySlain; CancelCasts(); }
        void OnEnemySlain(EnemyAI _) => AddFocus(1f);
        public void ApplyClass(ClassDefinition chosen)
        {
            CancelCasts(); definition = chosen; tiers = new int[AbilityBook.Count(chosen)];
            equipped[0] = equipped[1] = -1; readyAt.Clear(); focus = chosen.MaxFocus;
        }
        public void ApplyGear(int bonusFocus) { gearFocus = bonusFocus; focus = Mathf.Min(focus, MaxFocus); }
        public void ApplyProgression(int bonusFocus, int firstTier, int secondTier)
        {
            growthFocus = bonusFocus;
            if (tiers.Length > 0) tiers[0] = firstTier;
            if (tiers.Length > 1) tiers[1] = secondTier;
            focus = Mathf.Min(focus, MaxFocus);
        }
        public void ApplyProgression(int bonusFocus, int[] knownTiers, int[] loadout)
        {
            growthFocus = bonusFocus;
            int count = definition == null ? 0 : AbilityBook.Count(definition);
            if (tiers.Length != count) tiers = new int[count];
            for (int i = 0; i < count; i++) tiers[i] = knownTiers != null && i < knownTiers.Length ? Mathf.Clamp(knownTiers[i], 0, 3) : 0;
            for (int i = 0; i < 2; i++) equipped[i] = loadout != null && i < loadout.Length && loadout[i] >= 0 && loadout[i] < count ? loadout[i] : -1;
            focus = Mathf.Min(focus, MaxFocus);
        }
        // The owner's rule: a full bar always pays for a skill, whatever its workbook cost.
        public float CastCost(AbilityDefinition a) => Mathf.Min(a.Cost, MaxFocus);
        public void AddFocus(float amount) => focus = Mathf.Clamp(focus + amount, 0f, MaxFocus);
        public void SetFocus(float amount) => focus = Mathf.Clamp(amount, 0f, MaxFocus);
        public AbilityDefinition ActiveAbility(int slot) => slot < 0 || slot > 1 ? null : AbilityBook.At(definition, equipped[slot]);
        public AbilityDefinition AbilityAt(int index) => AbilityBook.At(definition, index);
        public int EquippedAbilityIndex(int slot) => slot < 0 || slot > 1 ? -1 : equipped[slot];
        public float CooldownFraction(int slot)
        {
            var a = ActiveAbility(slot);
            return a != null && readyAt.TryGetValue(a.Id, out float ready) ? Mathf.Clamp01((ready - Time.time) / Mathf.Max(0.1f, a.Cooldown)) : 0f;
        }
        public bool Affordable(int slot)
        {
            var a = ActiveAbility(slot);
            if (a == null || growth == null || !growth.CanEquipSkill(a.Id) || focus < CastCost(a)) return false;
            return health.Current > a.Tier(growth.SkillTier(a.Id)).SelfDamage * AbilityBook.PowerFactor(definition, a) && (!a.IsRecipe || growth.RecipeStock(a.Id) > 0);
        }
        void Update()
        {
            if (zones.Current != lastZone) { CancelCasts(); lastZone = zones.Current; }
            if (definition == null || !health.IsAlive) { CancelCasts(); return; }
            focus = Mathf.Min(MaxFocus, focus + definition.FocusPerSecond * Time.deltaTime);
            for (int i = 0; i < 2; i++) if (input.ConsumeAbility(i)) TryUse(i);
            if (input.ConsumePotion()) DrinkPotion();
        }
        public bool TryUse(int slot)
        {
            var a = ActiveAbility(slot);
            if (a == null || growth == null || !health.IsAlive || status.Stunned || Time.timeScale <= 0f) return false;
            // With the weapons away (houses, the city) the skill buttons are gone too, so no key may cast either.
            if (!combat.Armed) return false;
            if (!growth.CanEquipSkill(a.Id)) { GameMessages.Notify(AbilityText.NotLearned); return false; }
            if (readyAt.TryGetValue(a.Id, out float ready) && Time.time < ready) return false;
            if (focus < CastCost(a)) { GameMessages.Notify(GameText.NoFocus); return false; }
            var rank = a.Tier(growth.SkillTier(a.Id));
            float factor = AbilityBook.PowerFactor(definition, a);
            if (health.Current <= rank.SelfDamage * factor) { GameMessages.Notify(AbilityText.NotEnoughHealth); return false; }
            bool usesCorpse = a.Id == "corpseexplosion" || a.Id == "n_corpseburst";
            EnemyAI corpse = usesCorpse ? FindCorpse() : null;
            if (usesCorpse && corpse == null) { GameMessages.Notify(AbilityText.NoCorpse); return false; }
            if (a.IsRecipe && !growth.TryConsumeRecipe(a.Id)) { GameMessages.Notify(AbilityText.BrewFirst); return false; }
            focus -= CastCost(a); readyAt[a.Id] = Time.time + Mathf.Max(0.1f, a.Cooldown);
            if (rank.SelfDamage > 0f) health.ApplyDamage(new DamageInfo(rank.SelfDamage * factor, transform.position, Vector3.zero, gameObject, true));
            var cast = new Cast { Ability = a, Tier = rank, Factor = factor, Bonus = growth.SkillPowerBonus(a.Id), Generation = generation };
            if (a.Delivery == AbilityDelivery.Self) ApplySelf(cast);
            else
            {
                combat.PlayAbilitySwing();
                switch (a.Delivery)
                {
                    case AbilityDelivery.Bolt: case AbilityDelivery.Orb: Shoot(cast, aimOrigin.forward); break;
                    case AbilityDelivery.Fan: StartCoroutine(Fan(cast)); break;
                    case AbilityDelivery.Chain: StartCoroutine(Chain(cast)); break;
                    case AbilityDelivery.Nova:
                        Vector3? corpsePoint = corpse != null ? corpse.transform.position : (Vector3?)null;
                        if (corpse != null) corpse.gameObject.SetActive(false);
                        StartCoroutine(Nova(cast, corpsePoint)); break;
                    case AbilityDelivery.Field:
                        if (fields.Count >= 3) { StopCoroutine(fields[0]); fields.RemoveAt(0); }
                        fields.Add(StartCoroutine(Field(cast, GroundAim(a.Range)))); break;
                    case AbilityDelivery.Charge: case AbilityDelivery.Leap: StartCoroutine(Rush(cast)); break;
                    default: StartCoroutine(Strikes(cast)); break;
                }
            }
            Burst(aimOrigin.position + aimOrigin.forward * 0.65f, EffectColor(a.Effect), 0.25f);
            return true;
        }
        bool Valid(Cast cast) => cast.Generation == generation && health.IsAlive && definition != null;
        void CancelCasts()
        {
            generation++; StopAllCoroutines(); fields.Clear();
            foreach (var bolt in bolts) if (bolt != null) Destroy(bolt.gameObject);
            bolts.Clear();
        }
        void ApplySelf(Cast cast)
        {
            var r = cast.Tier; float f = cast.Factor;
            if (r.CleansePoison) status.CleansePoison();
            if (r.HealFlat > 0f || r.HealMagMultiplier > 0f) health.Heal((r.HealFlat + r.HealMagMultiplier * growth.Magic) * f);
            if (r.RestoreFocus > 0f) AddFocus(r.RestoreFocus * f);
            if (r.RegenerationPerTurn > 0f) status.ApplyRegen(r.RegenerationPerTurn * f / AbilityBook.TurnSeconds, r.RegenerationTurns * AbilityBook.TurnSeconds);
            if (r.ShieldDefMultiplier > 0f) status.ApplyShield((growth.Defense * r.ShieldDefMultiplier + cast.Bonus) * f, AbilityBook.DefaultShieldTurns * AbilityBook.TurnSeconds);
            if (r.AttackBonus > 0f) status.ApplyAttackBonus(r.AttackBonus * f, r.AttackBonusTurns * AbilityBook.TurnSeconds);
            if (cast.Ability.Id == "nourishing_stew") growth.AddFood(35);
            if (cast.Ability.Id == "cordial_marrow") growth.AddFood(50);
            Ring(transform.position, 1.1f, EffectColor(cast.Ability.Effect));
        }
        IEnumerator Strikes(Cast cast)
        {
            for (int i = 0; i < Mathf.Max(1, cast.Tier.Hits) && Valid(cast); i++)
            {
                var victim = Trace(aimOrigin.forward, cast.Ability.Range, cast.Ability.Kind == AbilityKind.Physical ? 0.5f : 0.22f);
                if (victim != null) Hurt(victim, cast, victim.transform.position + Vector3.up);
                else if (i == 0) GameMessages.Notify(GameText.NoTarget);
                if (i + 1 < cast.Tier.Hits) { combat.PlayAbilitySwing(); yield return new WaitForSeconds(0.1f); }
            }
        }
        IEnumerator Nova(Cast cast, Vector3? corpsePoint = null)
        {
            for (int i = 0; i < Mathf.Max(1, cast.Tier.Hits) && Valid(cast); i++)
            {
                Vector3 point = corpsePoint ?? transform.position;
                Area(cast, point, cast.Ability.Radius, null);
                Ring(point, cast.Ability.Radius, EffectColor(cast.Ability.Effect));
                if (i + 1 < cast.Tier.Hits) yield return new WaitForSeconds(0.09f);
            }
        }
        IEnumerator Fan(Cast cast)
        {
            int count = Mathf.Max(1, cast.Tier.Hits);
            // The first knife flies at the crosshair and the rest open out to either side, so an even count never splits
            // around the foe being aimed at.
            float step = count > 1 ? 13f / ((count) / 2) : 0f;
            for (int i = 0; i < count && Valid(cast); i++)
            {
                float angle = (i + 1) / 2 * step * (i % 2 == 1 ? -1f : 1f);
                Shoot(cast, Quaternion.AngleAxis(angle, Vector3.up) * aimOrigin.forward);
                yield return new WaitForSeconds(0.045f);
            }
        }
        void Shoot(Cast cast, Vector3 direction)
        {
            bolts.RemoveAll(p => p == null);
            Vector3 origin = aimOrigin.position + direction * 0.65f - aimOrigin.up * 0.08f;
            var bolt = combat.LaunchAbilityBolt(Bolt(cast.Ability), origin, direction, (victim, point) =>
            {
                if (!Valid(cast)) return 0f;
                float dealt = Hurt(victim, cast, point);
                if (cast.Ability.Delivery == AbilityDelivery.Orb)
                {
                    Area(cast, point, cast.Ability.Radius, victim);
                    Ring(point, cast.Ability.Radius, EffectColor(cast.Ability.Effect));
                }
                return dealt;
            });
            if (bolt != null) bolts.Add(bolt);
        }
        IEnumerator Chain(Cast cast)
        {
            var victim = Trace(aimOrigin.forward, cast.Ability.Range, 0.35f);
            var struck = new HashSet<Health>(); Vector3 previous = aimOrigin.position;
            for (int i = 0; i < cast.Tier.Hits && victim != null && Valid(cast); i++)
            {
                Vector3 point = victim.transform.position + Vector3.up;
                for (int p = 0; p < 8; p++) Burst(Vector3.Lerp(previous, point, p / 7f), EffectColor(cast.Ability.Effect), 0.12f);
                Hurt(victim, cast, point); struck.Add(victim); previous = point;
                yield return new WaitForSeconds(0.1f); victim = Nearest(previous, 6f, struck);
            }
        }
        IEnumerator Field(Cast cast, Vector3 point)
        {
            float duration = Mathf.Max(3f, cast.Tier.PoisonTurns * AbilityBook.TurnSeconds);
            var struck = new HashSet<Health>();
            for (float elapsed = 0f; elapsed < duration && Valid(cast); elapsed += 0.5f)
            {
                int count = Physics.OverlapSphereNonAlloc(point, cast.Ability.Radius, nearby, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var victim = nearby[i].GetComponentInParent<Health>();
                    if (!IsFoe(victim) || struck.Contains(victim) || !Visible(point + Vector3.up * 0.4f, victim)) continue;
                    Hurt(victim, cast, victim.transform.position + Vector3.up); struck.Add(victim);
                }
                Ring(point, cast.Ability.Radius, EffectColor(cast.Ability.Effect));
                yield return new WaitForSeconds(0.5f);
            }
        }
        IEnumerator Rush(Cast cast)
        {
            Vector3 direction = Vector3.ProjectOnPlane(aimOrigin.forward, Vector3.up).normalized;
            float duration = cast.Ability.Delivery == AbilityDelivery.Leap ? 0.4f : 0.3f;
            float previousArc = 0f;
            for (float t = 0f; t < duration && Valid(cast); t += Time.deltaTime)
            {
                float next = Mathf.Min(duration, t + Time.deltaTime);
                float arc = cast.Ability.Delivery == AbilityDelivery.Leap ? Mathf.Sin(next / duration * Mathf.PI) * 1.15f : 0f;
                if (capsule != null && capsule.enabled) capsule.Move(direction * (cast.Ability.Range / duration * (next - t)) + Vector3.up * (arc - previousArc));
                previousArc = arc;
                yield return null;
            }
            if (Valid(cast)) { Area(cast, transform.position, cast.Ability.Radius, null); Ring(transform.position, cast.Ability.Radius, EffectColor(cast.Ability.Effect)); }
        }
        float Hurt(Health victim, Cast cast, Vector3 point)
        {
            if (!Valid(cast) || !IsFoe(victim) || Random.value > cast.Tier.Accuracy) return 0f;
            var r = cast.Tier; bool magic = cast.Ability.Kind != AbilityKind.Physical;
            float stat = cast.Ability.IsRecipe ? 0f : magic ? growth.Magic : growth.Attack;
            float amount = status.ModifyOutgoing(r.Power + cast.Bonus + stat, magic) * cast.Factor;
            var passive = GetComponent<PlayerPassives>();
            if (r.CriticalChance > 0f && Random.value < r.CriticalChance) amount *= passive != null ? passive.CriticalMultiplier : AbilityBook.CriticalMultiplier;
            var foe = victim.GetComponent<EnemyAI>();
            if (r.Holy && foe != null && foe.Definition.Undead) amount *= 1.5f;
            bool execute = r.ExecuteThreshold > 0f && victim.Normalized <= r.ExecuteThreshold;
            if (execute) amount = victim.Current;
            float dealt = victim.ApplyDamage(new DamageInfo(amount, point, point - aimOrigin.position, gameObject, execute));
            if (dealt <= 0f) return 0f;
            combat.ReportAbilityHit(victim, dealt);
            if (r.Lifesteal > 0f) health.Heal(dealt * r.Lifesteal);
            var condition = victim.GetComponent<StatusEffects>();
            if (condition != null && victim.IsAlive)
            {
                if (r.PoisonPerTurn > 0f) condition.ApplyTurnPoison(r.PoisonPerTurn * cast.Factor, r.PoisonTurns, gameObject);
                if (r.AttackReduction > 0f) condition.ApplyAttackReduction(r.AttackReduction * cast.Factor, r.AttackReductionTurns * AbilityBook.TurnSeconds);
                if (r.StunChance > 0f && Random.value < r.StunChance) condition.ApplyStun(AbilityBook.DefaultStunTurns * AbilityBook.TurnSeconds);
                if (r.StealFraction > 0f) condition.ApplyStatDrain(foe != null && foe.Definition.Rank >= EnemyRank.Guardian ? r.StealBossFraction : r.StealFraction, r.StealTurns * AbilityBook.TurnSeconds);
                if (r.ConvertChance > 0f && Random.value < r.ConvertChance && foe != null && foe.Definition.Rank < EnemyRank.Guardian) condition.ApplyCharm(3f * AbilityBook.TurnSeconds, gameObject);
            }
            if (cast.Ability.IsRecipe && cast.Ability.Delivery != AbilityDelivery.Self && r.HealFlat > 0f) health.Heal(r.HealFlat * cast.Factor);
            Burst(point, EffectColor(cast.Ability.Effect), 0.2f); return dealt;
        }
        void Area(Cast cast, Vector3 point, float radius, Health skip)
        {
            unique.Clear(); int count = Physics.OverlapSphereNonAlloc(point, Mathf.Max(1f, radius), nearby, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var victim = nearby[i].GetComponentInParent<Health>();
                if (victim == skip || !IsFoe(victim) || !unique.Add(victim) || !Visible(point + Vector3.up * 0.5f, victim)) continue;
                Hurt(victim, cast, victim.transform.position + Vector3.up);
            }
        }
        Health Trace(Vector3 direction, float range, float radius)
        {
            int count = Physics.SphereCastNonAlloc(aimOrigin.position, radius, direction, traces, Mathf.Max(1f, range), ~0, QueryTriggerInteraction.Ignore);
            Health closest = null; float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (traces[i].collider.transform.IsChildOf(transform) || traces[i].distance >= distance) continue;
                distance = traces[i].distance; var candidate = traces[i].collider.GetComponentInParent<Health>(); closest = IsFoe(candidate) ? candidate : null;
            }
            return closest;
        }
        Health Nearest(Vector3 point, float radius, HashSet<Health> excluded)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, nearby, ~0, QueryTriggerInteraction.Ignore);
            Health closest = null; float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var candidate = nearby[i].GetComponentInParent<Health>();
                if (!IsFoe(candidate) || excluded.Contains(candidate) || !Visible(point, candidate)) continue;
                float next = (candidate.transform.position - point).sqrMagnitude; if (next < distance) { closest = candidate; distance = next; }
            }
            return closest;
        }
        EnemyAI FindCorpse()
        {
            EnemyAI closest = null; float distance = 12f * 12f;
            foreach (var body in FindObjectsByType<EnemyAI>())
            {
                if (!body.gameObject.activeInHierarchy || body.Health == null || body.Health.IsAlive) continue;
                Vector3 towards = body.transform.position + Vector3.up * 0.3f - aimOrigin.position;
                if (towards.sqrMagnitude >= distance || Vector3.Angle(aimOrigin.forward, towards) > 55f || !Visible(aimOrigin.position, body.Health)) continue;
                closest = body; distance = towards.sqrMagnitude;
            }
            return closest;
        }
        bool IsFoe(Health victim) => victim != null && victim != health && victim.IsAlive && victim.gameObject.activeInHierarchy &&
            (victim.GetComponent<EnemyAI>() != null || victim.GetComponent<CursedKnightAI>() != null) &&
            !(victim.GetComponent<StatusEffects>() is StatusEffects condition && condition.Charmed && condition.CharmSource == gameObject);
        bool Visible(Vector3 point, Health victim) => !Physics.Linecast(point, victim.transform.position + Vector3.up, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) ||
            hit.collider.transform.IsChildOf(victim.transform) || hit.collider.transform.IsChildOf(transform);
        Vector3 GroundAim(float range)
        {
            Vector3 point = aimOrigin.position + Vector3.ProjectOnPlane(aimOrigin.forward, Vector3.up).normalized * Mathf.Clamp(range * 0.45f, 2f, 6f);
            Vector3 travel = point - aimOrigin.position;
            int count = Physics.RaycastNonAlloc(aimOrigin.position, travel.normalized, traces, travel.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float wallDistance = travel.magnitude;
            for (int i = 0; i < count; i++)
                if (traces[i].collider.GetComponentInParent<Health>() == null && traces[i].distance < wallDistance)
                    wallDistance = Mathf.Max(0.3f, traces[i].distance - 0.35f);
            point = aimOrigin.position + travel.normalized * wallDistance;
            count = Physics.RaycastNonAlloc(point + Vector3.up * 3f, Vector3.down, traces, 8f, ~0, QueryTriggerInteraction.Ignore);
            Vector3 floor = transform.position;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (traces[i].collider.GetComponentInParent<Health>() != null || traces[i].collider is CharacterController ||
                    traces[i].normal.y < 0.4f || traces[i].distance >= nearest) continue;
                nearest = traces[i].distance; floor = traces[i].point;
            }
            return floor + Vector3.up * 0.04f;
        }
        static BoltKind Bolt(AbilityDefinition a) => a.Tier(1).Holy ? BoltKind.Holy : a.Owner == CharacterClass.Necromancer ? BoltKind.Shadow : a.IsRecipe || a.Owner == CharacterClass.Gravethief ? BoltKind.Venom : BoltKind.Fire;
        void Burst(Vector3 point, Color color, float radius) { if (visualEffects != null) visualEffects.EmitAbility(point, color, radius); }
        void Ring(Vector3 point, float radius, Color color)
        {
            for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6f; Burst(point + new Vector3(Mathf.Cos(a) * radius, 0.12f, Mathf.Sin(a) * radius), color, 0.13f); }
        }
        public static Color EffectColor(AbilityEffect effect)
        {
            AbilityDefinition a = null; foreach (var entry in AbilityBook.AllDefinitions) if (entry.Effect == effect) { a = entry; break; }
            if (a == null) return new Color(0.75f, 0.55f, 0.95f);
            string id = a.Id;
            if (id.Contains("frost") || id.Contains("frozen") || id.Contains("glacial") || id.Contains("blizzard")) return new Color(0.38f, 0.85f, 1f);
            if (id.Contains("lightning") || id.Contains("static")) return new Color(0.75f, 0.7f, 1f);
            if (a.Tier(1).Holy || a.Owner == CharacterClass.Warden) return new Color(1f, 0.82f, 0.36f);
            if (a.Owner == CharacterClass.Necromancer) return new Color(0.65f, 0.4f, 1f);
            if (a.Owner == CharacterClass.Gravethief || a.IsRecipe) return new Color(0.55f, 0.95f, 0.27f);
            return a.Owner == CharacterClass.Witch ? new Color(1f, 0.34f, 0.16f) : new Color(1f, 0.72f, 0.27f);
        }
        public void DrinkPotion()
        {
            var session = GameSession.Instance;
            if (session == null || !health.IsAlive) return;
            if (session.Potions <= 0) { GameMessages.Notify(GameText.NoPotion); return; }
            if (health.Normalized >= 1f) { GameMessages.Notify(GameText.FullHealth); return; }
            session.AddPotions(-1); health.Heal(GameSession.PotionHeal);
        }
    }
}
