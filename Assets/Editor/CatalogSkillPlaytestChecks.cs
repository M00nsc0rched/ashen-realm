using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Input;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace AshenRealm.EditorTools
{
    /// <summary>Workbook casts hit real enemy capsules in an arena outside the generated world.</summary>
    internal static class CatalogSkillPlaytestChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const float TargetHealth = 10000f;
        static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);

        // Cosmetic flesh particles consume Unity's random stream; combat is seeded independently below.
        static void SeedHits(int hits, float accuracy)
        {
            for (int seed = 1; seed < 10000; seed++)
            {
                Random.InitState(seed);
                bool valid = true;
                for (int i = 0; i < hits; i++) if (Random.value > accuracy) { valid = false; break; }
                if (valid) { Random.InitState(seed); return; }
            }
        }

        static float ResolveTime(AbilityDefinition ability, int rank)
        {
            int hits = Mathf.Max(1, ability.Tier(rank).Hits);
            switch (ability.Delivery)
            {
                case AbilityDelivery.Self: return 0.15f;
                case AbilityDelivery.Bolt: case AbilityDelivery.Orb: return 0.4f;
                case AbilityDelivery.Fan: return 0.4f + hits * 0.05f;
                case AbilityDelivery.Chain: return 0.2f + hits * 0.11f;
                case AbilityDelivery.Charge: case AbilityDelivery.Leap: return 0.65f;
                case AbilityDelivery.Field: return 0.65f;
                default: return 0.2f + hits * 0.11f;
            }
        }

        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var source = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include)
                .FirstOrDefault(e => e.EnemyId == "ghoul" && e.gameObject.scene.IsValid());
            var keeper = SoulKeeper.Instance;
            if (player == null || source == null || keeper == null || !keeper.HasSoul)
            { check(false, "Catalog casts require the player, an active soul and a real ghoul capsule"); yield break; }
            var growth = player.GetComponent<PlayerProgression>();
            var health = player.GetComponent<Health>();
            var status = player.GetComponent<StatusEffects>();
            var combat = player.GetComponent<PlayerCombat>();
            var abilities = player.GetComponent<PlayerAbilities>();
            var input = player.GetComponent<PlayerInputHub>();
            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var session = GameSession.Instance;
            keeper.Save();
            SoulRecord original = Copy(keeper.LatestSoul());
            MetaRecord originalMeta = Copy(keeper.Meta);
            Vector3 oldPosition = player.transform.position;
            float oldYaw = player.transform.eulerAngles.y, oldPitch = player.Pitch;
            float oldMax = health.Max, oldHealth = health.Current, oldFocus = abilities.Focus;
            float oldScale = Time.timeScale;
            bool oldInput = player.InputEnabled, oldCombat = combat.enabled, oldAbilities = abilities.enabled;
            var oldRandom = Random.state;
            ImpactEffects oldImpact = Field<ImpactEffects>(combat, "impactEffects");
            var oldProjectiles = new HashSet<EntityId>(Object.FindObjectsByType<Projectile>().Select(p => p.GetEntityId()));
            MonoBehaviour[] guards = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(b => b is EnemyAI || b is CursedKnightAI || b is PlayerPassives || b is PlayerLifecycle ||
                    b is CameraEffects || b is StoryDirector)
                .OrderBy(b => b is PlayerPassives ? 0 : 1).ToArray();
            bool[] oldEnabled = guards.Select(b => b.enabled).ToArray();
            FieldInfo[] statusFields = typeof(StatusEffects).GetFields(Private);
            object[] oldConditions = statusFields.Select(f => f.GetValue(status)).ToArray();
            var tempSoul = Copy(original);
            tempSoul.Id = "catalog_cast_" + Guid.NewGuid().ToString("N");
            Vector3 origin = new Vector3(-1700f, 0f, -850f);
            var arena = new GameObject("Catalog Skill Arena");
            arena.SetActive(false);
            var floor = new GameObject("Catalog Skill Floor");
            floor.transform.SetParent(arena.transform, false);
            floor.transform.position = origin + Vector3.down * 0.5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(80f, 1f, 80f);
            var wall = new GameObject("Catalog Skill Occlusion Wall");
            wall.transform.SetParent(arena.transform, false);
            wall.AddComponent<BoxCollider>().size = new Vector3(12f, 6f, 0.3f);
            wall.SetActive(false);
            var targets = new List<Health>();

            void PositionTarget(int index, Vector3 point)
            {
                var target = targets[index];
                target.gameObject.SetActive(true);
                var body = target.GetComponent<CharacterController>();
                body.enabled = false;
                target.transform.SetPositionAndRotation(origin + point + Vector3.up * 0.06f, Quaternion.identity);
                body.enabled = true;
                target.GetComponent<StatusEffects>().Clear();
                target.SetMax(TargetHealth, true);
            }
            void ParkTargets()
            {
                for (int i = 0; i < targets.Count; i++) PositionTarget(i, new Vector3(20f + i * 2f, 0f, 20f));
            }
            void Arrange(AbilityDefinition ability)
            {
                ParkTargets();
                float distance = ability.Delivery == AbilityDelivery.Melee ? 1.9f :
                    ability.Delivery == AbilityDelivery.Field ? Mathf.Clamp(ability.Range * 0.45f, 2f, 6f) :
                    ability.Delivery == AbilityDelivery.Charge || ability.Delivery == AbilityDelivery.Leap ? ability.Range + 1f :
                    ability.Delivery == AbilityDelivery.Nova ? 2f : 3f;
                PositionTarget(0, Vector3.forward * distance);
                if (ability.Delivery == AbilityDelivery.Fan)
                {
                    PositionTarget(1, new Vector3(-1.1f, 0f, distance));
                    PositionTarget(2, new Vector3(1.1f, 0f, distance));
                }
                if (ability.Delivery == AbilityDelivery.Chain)
                {
                    PositionTarget(1, new Vector3(-2f, 0f, distance + 2f));
                    PositionTarget(2, new Vector3(2f, 0f, distance + 2f));
                    PositionTarget(3, new Vector3(-3f, 0f, distance + 4f));
                    PositionTarget(4, new Vector3(3f, 0f, distance + 4f));
                    PositionTarget(5, Vector3.forward * (distance + 6f));
                }
                if (ability.Id == "corpseexplosion" || ability.Id == "n_corpseburst")
                {
                    PositionTarget(0, Vector3.forward * 4.2f);
                    PositionTarget(1, Vector3.forward * 2.5f);
                    targets[1].SetCurrent(1f);
                    targets[1].ApplyDamage(new DamageInfo(2f, targets[1].transform.position, Vector3.zero, null, true));
                }
                Physics.SyncTransforms();
            }
            void Prepare(AbilityDefinition ability, int rank = 1, CharacterClass? owner = null)
            {
                session.ChooseClass(owner ?? ability.Owner);
                growth.Restore(new ProgressRecord
                {
                    SkillSchemaVersion = PlayerProgression.SkillSchema,
                    FocusBonus = 1000, Food = 0,
                    Intellect = ability.IsRecipe ? ability.IntellectRequired + (rank - 1) * 10 : 10,
                    NecromancerPath = ability.Path,
                    Skills = new List<SkillProgressRecord> { new SkillProgressRecord { Id = ability.Id, Tier = rank, Stock = ability.IsRecipe ? 3 : 0 } },
                    EquippedSkillIds = new[] { ability.Id, string.Empty },
                    Passive = new PassiveProgressRecord { DescentId = session.Descents }
                });
                combat.SetArmed(true);
                status.Clear();
                health.SetMax(10000f, true); health.SetCurrent(5000f);
                abilities.SetFocus(500f);
                player.Teleport(origin + Vector3.up * 0.06f, 0f, 0f);
                wall.SetActive(false);
                Arrange(ability);
                SeedHits(1, ability.Tier(growth.SkillTier(ability.Id)).Accuracy);
            }
            float DamageTaken() => targets.Sum(t => TargetHealth - t.Current);

            try
            {
                Time.timeScale = 1f;
                foreach (MonoBehaviour guard in guards) guard.enabled = false;
                player.InputEnabled = false;
                input.SetTouchMove(Vector2.zero);
                input.SetTouchButton(PlayerButton.Attack, false);
                input.SetTouchButton(PlayerButton.Block, false);
                combat.enabled = abilities.enabled = true;
                Set(combat, "impactEffects", null);
                SaveStore.Put(tempSoul);
                keeper.Load(tempSoul);
                for (int i = 0; i < 6; i++)
                {
                    var clone = Object.Instantiate(source.gameObject, arena.transform);
                    clone.name = "Catalog Target " + i;
                    Set(clone.GetComponent<EnemyAI>(), "saveKey", tempSoul.Id + "_target_" + i);
                    clone.GetComponent<EnemyAI>().enabled = false;
                    clone.GetComponent<CharacterController>().enabled = true;
                    clone.SetActive(true);
                    targets.Add(clone.GetComponent<Health>());
                }
                arena.SetActive(true);
                player.Teleport(origin + Vector3.up * 0.06f, 0f, 0f);
                yield return 0.1f;

                AbilityDefinition[] playable = AbilityBook.AllDefinitions.Where(a => !a.Hidden).ToArray();
                int castCount = 0;
                foreach (AbilityDefinition ability in playable)
                {
                    Prepare(ability);
                    var rank = ability.Tier(growth.SkillTier(ability.Id));
                    if (rank.CleansePoison) status.ApplyTurnPoison(1f, 3, targets[0].gameObject);
                    float beforeFocus = abilities.Focus, beforeHealth = health.Current;
                    float beforeEnemyDamage = DamageTaken();
                    int beforeStock = growth.RecipeStock(ability.Id);
                    bool used = abilities.TryUse(0);
                    float expectedFocus = beforeFocus - ability.Cost + rank.RestoreFocus;
                    bool paid = Mathf.Abs(abilities.Focus - expectedFocus) < 0.01f &&
                        (!ability.IsRecipe || growth.RecipeStock(ability.Id) == beforeStock - 1);
                    float afterFocus = abilities.Focus;
                    bool blockedRepeat = !abilities.TryUse(0) && Mathf.Approximately(abilities.Focus, afterFocus) &&
                        (!ability.IsRecipe || growth.RecipeStock(ability.Id) == beforeStock - 1);
                    yield return ResolveTime(ability, 1);
                    bool observed = ability.Delivery != AbilityDelivery.Self ? DamageTaken() > beforeEnemyDamage + 0.1f :
                        status.ShieldPoints > 0f || status.AttackBonus > 0f || health.Current > beforeHealth ||
                        (rank.SelfDamage > 0f && health.Current < beforeHealth && abilities.Focus > beforeFocus) ||
                        growth.Food > 0 || (rank.CleansePoison && !status.Poisoned);
                    check(used && paid && blockedRepeat && observed,
                        $"Workbook cast {ability.Id} ({ability.Source}) pays once, keeps its cooldown and changes real combat state");
                    if (used && observed) castCount++;
                }
                check(playable.Length == 110 && castCount == playable.Length,
                    $"All 110 player workbook records produce an observed runtime effect ({castCount}/{playable.Length})");

                foreach (string corpseId in new[] { "corpseexplosion", "n_corpseburst" })
                {
                    AbilityDefinition corpseAbility = AbilityBook.Find(corpseId);
                    Prepare(corpseAbility);
                    PositionTarget(1, new Vector3(22f, 0f, 20f)); Physics.SyncTransforms();
                    float missingFocus = abilities.Focus;
                    check(!abilities.TryUse(0) && Mathf.Approximately(abilities.Focus, missingFocus) && abilities.CooldownFraction(0) <= 0f,
                        $"{corpseId} requires a real corpse before spending focus or starting a cooldown");
                    Prepare(corpseAbility);
                    float livingBefore = targets[0].Current;
                    bool exploded = abilities.TryUse(0);
                    yield return ResolveTime(corpseAbility, 1);
                    bool consumed = !targets[1].gameObject.activeSelf;
                    abilities.ApplyClass(session.Class); growth.ApplyRuntime(); abilities.SetFocus(500f);
                    float reuseFocus = abilities.Focus;
                    check(exploded && consumed && targets[0].Current < livingBefore && !abilities.TryUse(0) &&
                        Mathf.Approximately(abilities.Focus, reuseFocus) && abilities.CooldownFraction(0) <= 0f,
                        $"{corpseId} explodes at a real dead body, consumes it and cannot reuse the same corpse");
                }

                AbilityDefinition fury = AbilityBook.Find("bladefury");
                for (int tier = 1; tier <= 3; tier++)
                {
                    Prepare(fury, tier);
                    int hits = 0;
                    void Count(DamageInfo info, float amount) { if (info.Source == player.gameObject && amount > 0f) hits++; }
                    targets[0].Damaged += Count;
                    SeedHits(fury.Tier(tier).Hits, fury.Tier(tier).Accuracy);
                    bool used = abilities.TryUse(0);
                    yield return ResolveTime(fury, tier);
                    targets[0].Damaged -= Count;
                    check(used && hits == tier + 3, $"Blade Fury rank {tier} resolves {tier + 3} separate real nova hits");
                }

                Prepare(AbilityBook.Find("guard"));
                bool shieldCast = abilities.TryUse(0);
                float shield = status.ShieldPoints, beforeShieldHealth = health.Current;
                health.ApplyDamage(new DamageInfo(shield * 0.5f, player.transform.position, Vector3.zero, null));
                bool halfShield = Mathf.Abs(status.ShieldPoints - shield * 0.5f) < 0.01f && Mathf.Approximately(health.Current, beforeShieldHealth);
                health.ApplyDamage(new DamageInfo(shield * 0.5f + 7f, player.transform.position, Vector3.zero, null));
                check(shieldCast && shield > 0f && halfShield && status.ShieldPoints < 0.01f && Mathf.Abs(health.Current - beforeShieldHealth + 7f) < 0.01f,
                    "A cast ward consumes its finite shield pool and only overflow reaches player health");

                AbilityDefinition poison = AbilityBook.Find("poisonblade");
                Prepare(poison);
                bool poisonCast = abilities.TryUse(0);
                float afterInitial = targets[0].Current;
                yield return AbilityBook.TurnSeconds - 0.2f;
                check(poisonCast && Mathf.Approximately(targets[0].Current, afterInitial), "Turn poison cannot tick before its first full two-second turn");
                yield return 0.35f;
                check(Mathf.Abs(afterInitial - targets[0].Current - 4f) < 0.01f, "Workbook poison pays exactly four damage on its first turn");
                yield return AbilityBook.TurnSeconds;
                check(Mathf.Abs(afterInitial - targets[0].Current - 8f) < 0.01f, "Workbook poison pays its second complete turn");
                yield return AbilityBook.TurnSeconds;
                check(Mathf.Abs(afterInitial - targets[0].Current - 12f) < 0.01f && !targets[0].GetComponent<StatusEffects>().Poisoned,
                    "Three-turn poison includes the final tick and then expires");
                yield return AbilityBook.TurnSeconds + 0.1f;
                check(Mathf.Abs(afterInitial - targets[0].Current - 12f) < 0.01f, "Expired turn poison cannot grant a fourth tick");

                AbilityDefinition foreign = AbilityBook.Find("bash");
                Prepare(foreign, 1, CharacterClass.Witch);
                float foreignExpected = (foreign.Tier(1).Power + growth.Attack) * 0.4f;
                bool foreignCast = abilities.TryUse(0);
                yield return ResolveTime(foreign, 1);
                check(foreignCast && growth.SkillTier(foreign.Id) == 1 && Mathf.Abs(DamageTaken() - foreignExpected) < 0.02f,
                    "A learned foreign rank-I physical skill deals exactly 40 percent of its real native-point formula");

                Prepare(AbilityBook.Find("strike"));
                ProgressRecord swapping = growth.Capture();
                swapping.Skills.Add(new SkillProgressRecord { Id = "bash", Tier = 1 });
                growth.Restore(swapping);
                bool first = abilities.TryUse(0);
                bool away = growth.TryEquipAbility(0, "bash"), back = growth.TryEquipAbility(1, "strike");
                float swapFocus = abilities.Focus;
                check(first && away && back && !abilities.TryUse(1) && abilities.CooldownFraction(1) > 0f && Mathf.Approximately(swapFocus, abilities.Focus),
                    "Moving a cooling skill from Q to R preserves the ID's cooldown and cannot charge another cast");

                foreach (string id in new[] { "strike", "firebolt", "whirlwind", "caltrops" })
                {
                    AbilityDefinition ability = AbilityBook.Find(id);
                    Prepare(ability);
                    float distance = ability.Delivery == AbilityDelivery.Melee ? 2.4f :
                        ability.Delivery == AbilityDelivery.Field ? 5.4f : 3.5f;
                    PositionTarget(0, Vector3.forward * distance);
                    wall.transform.position = origin + new Vector3(0f, 3f, ability.Delivery == AbilityDelivery.Melee ? 1.1f : distance * 0.5f);
                    wall.SetActive(true); Physics.SyncTransforms();
                    bool blockedCast = abilities.TryUse(0);
                    yield return ResolveTime(ability, 1);
                    check(blockedCast && DamageTaken() < 0.01f, $"A physical wall occludes the real {ability.Delivery} delivery ({id})");
                    Prepare(ability);
                    bool clearCast = abilities.TryUse(0);
                    yield return ResolveTime(ability, 1);
                    check(clearCast && DamageTaken() > 0.1f, $"Removing the wall restores the real {ability.Delivery} delivery ({id})");
                }

                AbilityDefinition field = AbilityBook.Find("caltrops");
                Prepare(field);
                Vector3 ground = (Vector3)typeof(PlayerAbilities).GetMethod("GroundAim", Private).Invoke(abilities, new object[] { field.Range });
                bool groundCast = abilities.TryUse(0);
                yield return 0.65f;
                check(groundCast && Mathf.Abs(ground.y - 0.04f) < 0.05f && DamageTaken() > 0f,
                    "A ground field finds the actual floor below the enemy's live capsule and damages that enemy");

                foreach (string cancellation in new[] { "class", "travel", "death" })
                {
                    Prepare(field); ParkTargets(); Physics.SyncTransforms();
                    bool fieldCast = abilities.TryUse(0);
                    if (cancellation == "class") session.ChooseClass(CharacterClass.Knight);
                    else if (cancellation == "travel")
                    {
                        zones.Place(zones.CurrentIndex == ZoneNumbers.Valley ? ZoneNumbers.City : ZoneNumbers.Valley, Vector3.zero, 0f, false);
                        player.Teleport(origin + Vector3.up * 0.06f, 0f, 0f);
                        combat.SetArmed(true);
                    }
                    else health.ApplyDamage(new DamageInfo(health.Current + 1f, player.transform.position, Vector3.zero, null, true));
                    yield return 0.15f;
                    health.SetMax(10000f, true);
                    PositionTarget(0, Vector3.forward * 5.4f); Physics.SyncTransforms();
                    yield return 0.65f;
                    check(fieldCast && DamageTaken() < 0.01f, $"A persistent field cannot damage a new arrival after {cancellation} cancellation");
                }
            }
            finally
            {
                CombatFocus.Set(null);
                arena.SetActive(false);
                Object.Destroy(arena);
                // Class application cancels every coroutine and owned bolt before the original soul is restored.
                abilities.ApplyClass(session.Class);
                foreach (Projectile projectile in Object.FindObjectsByType<Projectile>())
                    if (!oldProjectiles.Contains(projectile.GetEntityId())) Object.Destroy(projectile.gameObject);
                keeper.Load(original);
                health.SetMax(oldMax, false); health.SetCurrent(oldHealth);
                abilities.SetFocus(oldFocus);
                player.Teleport(oldPosition, oldYaw, oldPitch);
                for (int i = 0; i < statusFields.Length; i++) statusFields[i].SetValue(status, oldConditions[i]);
                Set(combat, "impactEffects", oldImpact);
                SaveStore.Delete(tempSoul.Id);
                MetaRecord meta = keeper.Meta;
                meta.Souls = originalMeta.Souls; meta.Descents = originalMeta.Descents; meta.Deepest = originalMeta.Deepest;
                meta.Codex = originalMeta.Codex; meta.UnlockedClasses = originalMeta.UnlockedClasses;
                meta.SkillMigrationRefunds = originalMeta.SkillMigrationRefunds;
                SaveStore.SaveMeta(meta);
                combat.enabled = oldCombat; abilities.enabled = oldAbilities;
                player.InputEnabled = oldInput;
                for (int i = guards.Length - 1; i >= 0; i--) if (guards[i] != null) guards[i].enabled = oldEnabled[i];
                Random.state = oldRandom;
                Time.timeScale = oldScale;
            }
        }
    }
}
