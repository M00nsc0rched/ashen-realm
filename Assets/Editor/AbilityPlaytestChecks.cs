using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.UI;
using AshenRealm.World;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>Learning, migration and live menu tests use temporary souls and restore the caller's ledger.</summary>
    internal static class AbilityPlaytestChecks
    {
        static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);

        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var growth = controller.GetComponent<PlayerProgression>();
            var status = controller.GetComponent<StatusEffects>();
            var health = controller.GetComponent<Health>();
            var inventory = Object.FindAnyObjectByType<InventoryPanel>(FindObjectsInactive.Include);
            var session = GameSession.Instance;
            var keeper = SoulKeeper.Instance;
            keeper.Save();
            SoulRecord original = keeper.LatestSoul();
            if (original == null) { check(false, "Skill checks require an active test soul"); yield break; }
            original = Copy(original);
            MetaRecord oldMeta = Copy(keeper.Meta);
            var ownIds = new List<string>();
            MonoBehaviour[] guards = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(b => b is EnemyAI || b is CursedKnightAI || b.GetType().Name == "PlayerPassives")
                .OrderBy(b => b.GetType().Name == "PlayerPassives" ? 0 : 1).ToArray();
            bool[] wasEnabled = guards.Select(b => b.enabled).ToArray();
            bool inputEnabled = controller.InputEnabled;
            SoulRecord LoadCase(CharacterClass id, ProgressRecord progress)
            {
                SoulRecord soul = Copy(original);
                soul.Id = "skill_test_" + Guid.NewGuid().ToString("N");
                soul.ClassId = (int)id;
                soul.Zone = ZoneNumbers.City;
                soul.WorldVersion = 0;
                soul.Health = 10000f; soul.Focus = 10000f;
                soul.Progress = progress == null ? null : Copy(progress);
                ownIds.Add(soul.Id);
                SaveStore.Put(soul);
                keeper.Load(SaveStore.Find(soul.Id));
                // JsonUtility never stores a null record: an absent one comes back as an empty pre-skill save and migrates.
                // A case without progress stands for a soul begun today, so it starts from the fresh record instead.
                if (progress == null) growth.Restore(null);
                status.Clear();
                return soul;
            }
            void Wallet(int amount) => keeper.AddSouls(amount - keeper.Souls);
            try
            {
                foreach (MonoBehaviour guard in guards) guard.enabled = false;
                controller.InputEnabled = false;
                status.Clear();
                yield return 0.05f;

                LoadCase(CharacterClass.Knight, new ProgressRecord { AbilityOneTier = 2, AbilityTwoTier = 3 });
                check(growth.SkillTier("shieldbash") == 2 && growth.SkillTier("rally") == 3 &&
                    growth.EquippedAbilityId(0) == "shieldbash" && growth.EquippedAbilityId(1) == "rally",
                    "Pre-N two-tier saves migrate to stable IDs and retain both bought tiers");
                LoadCase(CharacterClass.Knight, new ProgressRecord
                {
                    AbilityTiers = new[] { 2, 3, 2, 3 }, EquippedAbilities = new[] { 2, 3 }
                });
                check(growth.SkillTier("shieldbash") == 2 && growth.SkillTier("rally") == 3 &&
                    growth.SkillTier("whirlwind") == 2 && growth.SkillTier("ironskin") == 3 &&
                    growth.EquippedAbilityId(0) == "whirlwind" && growth.EquippedAbilityId(1) == "ironskin",
                    "N's four-skill saves retain all tiers and their actual Q/R identities");
                ProgressRecord reordered = growth.Capture();
                reordered.Skills.Reverse();
                reordered.Skills.Add(new SkillProgressRecord { Id = "retired_test_skill", Tier = 3 });
                growth.Restore(Copy(reordered));
                keeper.Save();
                keeper.Load(SaveStore.Find(keeper.LatestSoul().Id));
                check(growth.Capture().SkillSchemaVersion == 2 && growth.SkillTier("whirlwind") == 2 &&
                    growth.EquippedAbilityId(1) == "ironskin" && growth.Capture().Skills.Any(s => s.Id == "retired_test_skill" && s.Tier == 3),
                    "Reordering records and retaining an unknown ID cannot resize away bought skills or change equipment");

                LoadCase(CharacterClass.Knight, null);
                Wallet(2000);
                int wallet = keeper.Souls;
                check(growth.SkillTier("whirlwind") == 0 && growth.TryLearnOrUpgradeSkill("whirlwind") &&
                    growth.SkillTier("whirlwind") == 1 && keeper.Souls == wallet - 30,
                    "An unlearned native skill costs exactly 30 Souls");
                wallet = keeper.Souls;
                check(growth.TryLearnOrUpgradeSkill("whirlwind") && growth.SkillTier("whirlwind") == 2 && keeper.Souls == wallet - 25,
                    "Native rank I to II costs exactly 25 Souls");
                wallet = keeper.Souls;
                check(growth.TryLearnOrUpgradeSkill("whirlwind") && growth.SkillTier("whirlwind") == 3 && keeper.Souls == wallet - 48 &&
                    !growth.TryLearnOrUpgradeSkill("whirlwind"), "Native rank II to III costs 48 Souls and cannot buy a fourth rank");
                AbilityDefinition foreign = AbilityBook.Find("backstab");
                wallet = keeper.Souls;
                check(growth.TryLearnOrUpgradeSkill(foreign.Id) && growth.SkillTier(foreign.Id) == 1 &&
                    !growth.TryLearnOrUpgradeSkill(foreign.Id) && keeper.Souls == wallet - 30 &&
                    Mathf.Approximately(AbilityBook.PowerFactor(session.Class, foreign), 0.4f) && growth.TryEquipAbility(0, foreign.Id),
                    "Open foreign skills can be learned and equipped only at rank I with 40 percent power");
                check(!growth.CanLearnSkill("n_bonespear") && !growth.TryEquipAbility(1, "n_bonespear") &&
                    !growth.TryEquipAbility(1, -1), "Foreign signature skills and invalid presentation indices cannot be equipped");

                int beforeAttackRefund = keeper.Souls;
                SoulRecord necroOld = LoadCase(CharacterClass.Necromancer, new ProgressRecord
                {
                    AbilityTiers = new[] { 2, 3, 2, 1 }, EquippedAbilities = new[] { 0, 1 }
                });
                int afterNecroRefund = keeper.Souls;
                check(growth.ChosenPath == NecromancerPath.Attack && growth.SkillTier("n_bonespear") == 2 &&
                    growth.SkillTier("n_bonearmor") == 1 && !growth.CanEquipSkill("n_bonearmor") &&
                    string.IsNullOrEmpty(growth.EquippedAbilityId(1)),
                    "Mixed legacy necromancers choose the first equipped path and retain the other learned branch at rank I");
                check(keeper.Souls == beforeAttackRefund + 73 && keeper.Meta.SkillMigrationRefunds.Count(k => k == necroOld.Id + ":skill-schema2") == 1 &&
                    SaveStore.Find(necroOld.Id).Progress.PendingSkillRefund == 0,
                    "Off-path purchases receive one persisted migration receipt");
                SaveStore.Put(Copy(necroOld));
                keeper.Load(SaveStore.Find(necroOld.Id));
                check(keeper.Souls == afterNecroRefund,
                    "Reloading the same pre-migration necromancer record cannot pay its refund twice");
                int beforeDefenseRefund = keeper.Souls;
                LoadCase(CharacterClass.Necromancer, new ProgressRecord
                {
                    AbilityTiers = new[] { 2, 3, 2, 1 }, EquippedAbilities = new[] { 1, 0 }
                });
                check(growth.ChosenPath == NecromancerPath.Defense && keeper.Souls == beforeDefenseRefund + 50,
                    "Reversed legacy equipment chooses Defense and refunds exactly the two off-path rank-II purchases");

                LoadCase(CharacterClass.Necromancer, null);
                Wallet(2000);
                check(!growth.CanLearnSkill("n_bonespear") && growth.TryChoosePath(NecromancerPath.Attack) &&
                    !growth.TryChoosePath(NecromancerPath.Defense), "A necromancer chooses one path once per descent");
                check(growth.TryLearnOrUpgradeSkill("n_bonespear") && growth.TryLearnOrUpgradeSkill("n_bonespear") &&
                    growth.TryEquipAbility(0, "n_bonespear") && !growth.CanLearnSkill("n_bonearmor"),
                    "Learning and equipping obey the chosen path while the opposite path stays closed");
                PassiveProgressRecord passive = growth.PassiveState;
                passive.HealthBonus = passive.FocusBonus = passive.AttackBonus = passive.DefenseBonus = passive.MagicBonus = passive.SpeedBonus = 5;
                passive.RogueLuck = 2; passive.VigilUsed = passive.VigilActive = true; passive.VigilRemaining = 3f; passive.VigilTarget = "saved_test_foe";
                growth.ApplyRuntime();
                keeper.Save();
                keeper.Load(keeper.LatestSoul());
                check(growth.PassiveState.HealthBonus == 5 && growth.PassiveState.VigilUsed && growth.PassiveState.VigilActive &&
                    Mathf.Approximately(growth.PassiveState.VigilRemaining, 3f) && growth.PassiveState.VigilTarget == "saved_test_foe" &&
                    growth.ChosenPath == NecromancerPath.Attack, "Passive bonuses, vigil timer/target and chosen path survive SoulKeeper loading");
                session.AddDescent();
                growth.SyncDescentState();
                check(growth.PassiveState.DescentId == session.Descents && growth.PassiveState.HealthBonus == 0 &&
                    growth.PassiveState.FocusBonus == 0 && growth.PassiveState.AttackBonus == 0 && growth.PassiveState.DefenseBonus == 0 &&
                    growth.PassiveState.MagicBonus == 0 && growth.PassiveState.SpeedBonus == 0 && !growth.PassiveState.VigilUsed &&
                    !growth.PassiveState.VigilActive && growth.SkillTier("n_bonespear") == 2 &&
                    growth.ChosenPath == NecromancerPath.None && growth.TryChoosePath(NecromancerPath.Defense) && !growth.CanEquipSkill("n_bonespear"),
                    "A new descent resets passive resources and path once, preserving learned skills while enforcing the new choice");

                LoadCase(CharacterClass.Witch, null);
                growth.GrantAccretion();
                ProgressRecord accrued = growth.Capture();
                PassiveProgressRecord accruedStats = growth.PassiveState;
                int statTotal = accruedStats.HealthBonus + accruedStats.FocusBonus + accruedStats.AttackBonus +
                    accruedStats.DefenseBonus + accruedStats.MagicBonus + accruedStats.SpeedBonus;
                string boostedId = accrued.Skills.FirstOrDefault(s => s.PowerBonus > 0f)?.Id;
                keeper.Save();
                keeper.Load(keeper.LatestSoul());
                check(statTotal == 5 && boostedId != null && Mathf.Approximately(growth.SkillPowerBonus(boostedId), 5f),
                    "Accretion grants one five-point stat bonus and one learned damaging skill bonus, both saved by ID");
                PassiveProgressRecord stablePassive = growth.PassiveState;
                session.AddDescent();
                growth.SyncDescentState();
                check(ReferenceEquals(stablePassive, growth.PassiveState) && Mathf.Approximately(growth.SkillPowerBonus(boostedId), 0f) &&
                    growth.SkillTier(boostedId) > 0, "Descent reset clears temporary skill power while preserving learning and the live passive-state reference");

                Wallet(2000);
                int beforeAlchRefund = keeper.Souls;
                SoulRecord alchOld = LoadCase(CharacterClass.Alchemist, new ProgressRecord
                {
                    Intellect = 8, AbilityTiers = new[] { 2, 3, 2, 3 }, EquippedAbilities = new[] { 0, 2 }
                });
                check(growth.Intellect == 8 && keeper.Souls == beforeAlchRefund + 196 && growth.RecipeStock("starter_tonic") == 2 &&
                    growth.Capture().Skills.Any(s => s.Id == "elixir_mend" && s.Tier == 3),
                    "Legacy Alchemist upgrades refund 25/48 exactly, keep their source records, and seed tonic without granting INT");
                int afterAlchRefund = keeper.Souls;
                SaveStore.Put(Copy(alchOld));
                keeper.Load(SaveStore.Find(alchOld.Id));
                check(keeper.Souls == afterAlchRefund && growth.RecipeStock("starter_tonic") == 2,
                    "Replaying a legacy Alchemist save cannot duplicate refunds or starter stock");

                LoadCase(CharacterClass.Alchemist, null);
                check(growth.Herbs == 6 && growth.RecipeStock("starter_tonic") == 2 && growth.SkillTier("starter_tonic") == 1 &&
                    !growth.CanLearnSkill("backstab") && growth.SkillTier("flask_vitriol") == 0 &&
                    !growth.TryCraftRecipe("flask_vitriol"), "New Alchemists receive usable tonic and herbs but cannot learn spells or brew locked recipes");
                Wallet(2000);
                int intellect = growth.Intellect, statCost = growth.UpgradeCost;
                wallet = keeper.Souls;
                check(growth.TryRaiseStat(1) && growth.Intellect == intellect + 2 && keeper.Souls == wallet - statCost &&
                    growth.SkillTier("flask_vitriol") == 1, "The Alchemist's second stat purchase raises INT by two and unlocks the threshold recipe");
                ProgressRecord rankedRecipe = growth.Capture();
                rankedRecipe.Intellect = 20;
                growth.Restore(rankedRecipe);
                bool secondRank = growth.SkillTier("flask_vitriol") == 2;
                rankedRecipe = growth.Capture(); rankedRecipe.Intellect = 30; growth.Restore(rankedRecipe);
                check(secondRank && growth.SkillTier("flask_vitriol") == 3, "Recipe ranks increase at INT threshold plus ten and twenty");
                int herbs = growth.Herbs, stock = growth.RecipeStock("flask_vitriol");
                check(growth.TryCraftRecipe("flask_vitriol") && growth.Herbs == herbs - 2 &&
                    growth.RecipeStock("flask_vitriol") == stock + 1 && growth.TryEquipAbility(0, "flask_vitriol"),
                    "Brewing consumes the recipe's plant cost, stores one real dose and permits Q equipment");
                check(growth.TryConsumeRecipe("flask_vitriol") && !growth.TryConsumeRecipe("flask_vitriol") &&
                    growth.CanEquipSkill("flask_vitriol"), "One stocked dose is consumed once; empty eligible recipes may stay equipped");
                growth.TryCraftRecipe("flask_vitriol");
                keeper.Save();
                // AddHerbs and TryConsumeRecipe save the soul themselves, so keep a copy of this save to load back.
                SoulRecord recipeSave = Copy(keeper.LatestSoul());
                herbs = growth.Herbs; stock = growth.RecipeStock("flask_vitriol");
                growth.AddHerbs(10); growth.TryConsumeRecipe("flask_vitriol");
                SaveStore.Put(recipeSave);
                keeper.Load(SaveStore.Find(recipeSave.Id));
                check(growth.Herbs == herbs && growth.RecipeStock("flask_vitriol") == stock &&
                    growth.EquippedAbilityId(0) == "flask_vitriol", $"Recipe stock, plants, INT ranks and Q equipment survive the real save/load route " +
                    $"(plants {growth.Herbs}/{herbs}, doses {growth.RecipeStock("flask_vitriol")}/{stock}, Q {growth.EquippedAbilityId(0)})");
                session.AddGold(100);
                zones.Place(ZoneNumbers.City, Vector3.zero, 0f, false);
                int gold = session.Gold; herbs = growth.Herbs;
                check(growth.CanBuyHerbs && growth.TryBuyHerbs() && session.Gold == gold - 5 && growth.Herbs == herbs + 3,
                    "Town purchases trade exactly five gold for three plants");
                zones.Place(ZoneNumbers.FirstDepth, Vector3.zero, 0f, false);
                gold = session.Gold; herbs = growth.Herbs;
                check(!growth.CanBuyHerbs && !growth.TryBuyHerbs() && session.Gold == gold && growth.Herbs == herbs,
                    "Plants cannot be bought remotely from a dungeon");
                zones.Place(ZoneNumbers.Forge, Vector3.zero, 0f, false);
                check(growth.CanBuyHerbs, "Town interiors retain access to the plant supply");
                growth.AddFood(-1000); bool emptyFood = growth.Food == 0; growth.AddFood(1000);
                check(emptyFood && growth.Food == 100, "Recipe food rewards stay inside the saved zero-to-hundred range");

                LoadCase(CharacterClass.Knight, null);
                Wallet(2000);
                inventory.Open(null);
                inventory.ShowAbilities();
                AbilityDefinition[] natives = AbilityBook.All(session.Class).Where(s => AbilityBook.IsNative(session.Class, s)).ToArray();
                int lastPage = (natives.Length - 1) / 4;
                Button next = Field<Button>(inventory, "pageNext"), previous = Field<Button>(inventory, "pagePrevious");
                for (int guard = 0; guard < 100 && next.interactable; guard++) next.onClick.Invoke();
                check(inventory.CurrentPage == lastPage && !next.interactable &&
                    inventory.VisibleSkillId(0) == natives[lastPage * 4].Id, "Real Next button callbacks reach the final native page without overflowing four rows");
                int row = 0;
                while (row < 4 && (string.IsNullOrEmpty(inventory.VisibleSkillId(row)) || growth.SkillTier(inventory.VisibleSkillId(row)) > 0)) row++;
                if (row < 4)
                {
                    string id = inventory.VisibleSkillId(row);
                    Button buy = Field<Button[]>(inventory, "upgradeButtons")[row];
                    wallet = keeper.Souls;
                    check(buy.interactable, "An affordable final-page native row exposes its actual learning button");
                    buy.onClick.Invoke();
                    Field<Button[]>(inventory, "equipQButtons")[row].onClick.Invoke();
                    check(growth.SkillTier(id) == 1 && keeper.Souls == wallet - 30 && growth.EquippedAbilityId(0) == id,
                        "Final-page learning and Q callbacks act on the displayed stable ID rather than row zero of the catalog");
                }
                else check(false, "The final native page needs an unlearned row for the live callback check");
                if (previous.interactable) previous.onClick.Invoke();
                check(inventory.CurrentPage == Mathf.Max(0, lastPage - 1), "Real Previous callbacks step back one page");
                Field<Button>(inventory, "foreignFilterButton").onClick.Invoke();
                check(inventory.ShowingForeignSkills && inventory.CurrentPage == 0 &&
                    !AbilityBook.IsNative(session.Class, AbilityBook.Find(inventory.VisibleSkillId(0))),
                    "The foreign filter resets pagination and displays an actual open foreign skill");
                for (int guard = 0; guard < 100 && next.interactable; guard++) next.onClick.Invoke();
                string foreignId = inventory.VisibleSkillId(0);
                Field<Button[]>(inventory, "upgradeButtons")[0].onClick.Invoke();
                Field<Button[]>(inventory, "equipRButtons")[0].onClick.Invoke();
                check(growth.SkillTier(foreignId) == 1 && growth.EquippedAbilityId(1) == foreignId &&
                    !Field<Button[]>(inventory, "upgradeButtons")[0].interactable,
                    "The last foreign page learns/equips its displayed row and disables a forbidden second rank");
                Field<Button>(inventory, "nativeFilterButton").onClick.Invoke();
                check(!inventory.ShowingForeignSkills && inventory.CurrentPage == 0, "Returning to native skills starts at their first page");
                inventory.Close();
                yield return 0.05f;
            }
            finally
            {
                if (inventory.IsOpen) inventory.Close();
                keeper.Load(original);
                session.ChooseClass(CharacterClass.Knight);
                status.Clear();
                health.SetCurrent(health.Max);
                keeper.Save();
                foreach (string id in ownIds) SaveStore.Delete(id);
                MetaRecord meta = keeper.Meta;
                meta.Souls = oldMeta.Souls; meta.Descents = oldMeta.Descents; meta.Deepest = oldMeta.Deepest;
                meta.Codex = oldMeta.Codex; meta.UnlockedClasses = oldMeta.UnlockedClasses;
                meta.SkillMigrationRefunds = oldMeta.SkillMigrationRefunds;
                SaveStore.SaveMeta(meta);
                controller.InputEnabled = inputEnabled;
                for (int i = guards.Length - 1; i >= 0; i--) if (guards[i] != null) guards[i].enabled = wasEnabled[i];
            }
        }
    }
}
