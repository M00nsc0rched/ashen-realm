using System;
using System.Collections.Generic;
using AshenRealm.Combat;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.Player
{
    [Serializable]
    public sealed class DebugStats
    {
        public int MaxHealth, Health, MaxFocus, Focus, Intellect, Attack, Defense, Magic, Speed, Food, Honor, Level, Gold, Souls;
    }

    /// <summary>Stable skill IDs, bought growth, alchemical stock and the current descent's passive state.</summary>
    public sealed class PlayerProgression : MonoBehaviour
    {
        public const int SkillSchema = 2, LearnCost = 30;
        ProgressRecord data = new ProgressRecord();
        Health health;
        PlayerCombat combat;
        PlayerAbilities abilities;
        bool normalizing, normalized;
        public static PlayerProgression Instance { get; private set; }
        GameSession Session => GameSession.Instance;
        ClassCard Card => Catalog.Cards[(int)Session.Class.Id];
        int GearHealth => (Session.Weapon?.Health ?? 0) + (Session.Relic?.Health ?? 0);
        int GearFocus => (Session.Weapon?.Focus ?? 0) + (Session.Relic?.Focus ?? 0);
        int GearAttack => (Session.Weapon?.Attack ?? 0) + (Session.Relic?.Attack ?? 0);
        int GearMagic => (Session.Weapon?.Magic ?? 0) + (Session.Relic?.Magic ?? 0);
        int GearSpeed => (Session.Weapon?.Speed ?? 0) + (Session.Relic?.Speed ?? 0);
        int IntellectFocus => Session.Class.Id == CharacterClass.Alchemist ? data.Intellect - Card.Focus : 0;

        public int Level => data.Level;
        public int MaxHealth => Mathf.RoundToInt(Session.Class.MaxHealth + GearHealth * 1.6f + data.HealthBonus + PassiveState.HealthBonus);
        public int MaxFocus => Mathf.Max(1, Session.Class.MaxFocus + GearFocus + data.FocusBonus + IntellectFocus + PassiveState.FocusBonus);
        public int Intellect => data.Intellect;
        public int Attack => Mathf.Max(0, Card.Attack + GearAttack + data.AttackBonus + PassiveState.AttackBonus);
        public int Defense => Mathf.Max(0, Card.Defense + data.DefenseBonus + PassiveState.DefenseBonus);
        public int Magic => Mathf.Max(0, Card.Magic + GearMagic + data.MagicBonus + PassiveState.MagicBonus);
        public int Speed => Mathf.Max(1, Card.Speed + GearSpeed + data.SpeedBonus + PassiveState.SpeedBonus);
        public int Food => data.Food;
        public bool TorchEquipped => data.TorchEquipped;
        public int Herbs { get { EnsureData(); return data.Herbs; } }
        public NecromancerPath ChosenPath { get { EnsureData(); return data.NecromancerPath; } }
        public PassiveProgressRecord PassiveState { get { EnsureData(); return data.Passive; } }
        public bool CanBuyHerbs => Session.Class.Id == CharacterClass.Alchemist && abilities != null && abilities.IsInTown && Session.Gold >= 5;
        public int UpgradeCost => Mathf.CeilToInt(Mathf.Min(1000000f, 12f * Mathf.Pow(1.5f, Mathf.Min(Level - 1, 29))));

        void Awake()
        {
            Instance = this;
            health = GetComponent<Health>();
            combat = GetComponent<PlayerCombat>();
            abilities = GetComponent<PlayerAbilities>();
        }
        void Start() { Session.ClassSelected += ResetForClass; ApplyRuntime(); }
        void OnDestroy()
        {
            if (Session != null) Session.ClassSelected -= ResetForClass;
            if (Instance == this) Instance = null;
        }
        ProgressRecord Fresh()
        {
            var result = new ProgressRecord
            {
                SkillSchemaVersion = SkillSchema, Skills = new List<SkillProgressRecord>(),
                EquippedSkillIds = AbilityBook.DefaultLoadoutIds(Session.Class.Id),
                Intellect = Card.UsesIntellect ? Card.Focus : 10,
                Passive = new PassiveProgressRecord { DescentId = Session.Descents },
                Herbs = Session.Class.Id == CharacterClass.Alchemist ? 6 : 0
            };
            foreach (string id in AbilityBook.StarterIds(Session.Class.Id))
                result.Skills.Add(new SkillProgressRecord { Id = id, Tier = 1, Stock = id == "starter_tonic" ? 2 : 0 });
            return result;
        }
        void ResetForClass(ClassDefinition _)
        {
            data = Fresh();
            normalized = false;
            ApplyRuntime();
            health.SetCurrent(MaxHealth);
            abilities.SetFocus(MaxFocus);
        }
        public ProgressRecord Capture()
        {
            EnsureData();
            if (ResetDescentIfChanged()) ApplyRuntime();
            return JsonUtility.FromJson<ProgressRecord>(JsonUtility.ToJson(data));
        }
        public void Restore(ProgressRecord saved)
        {
            data = saved ?? Fresh();
            normalized = false;
            data.Level = Mathf.Clamp(data.Level, 1, 999);
            data.Food = Mathf.Clamp(data.Food, 0, 100);
            data.Intellect = Mathf.Clamp(data.Intellect, 1, 9999);
            EnsureData();
            ResetDescentIfChanged();
            ApplyRuntime();
            ApplyMigrationRefund();
        }
        void EnsureData()
        {
            if (normalized || normalizing || Session == null) return;
            normalizing = true;
            try
            {
                if (data == null) data = Fresh();
                if (data.Skills == null) data.Skills = new List<SkillProgressRecord>();
                if (data.SkillSchemaVersion < SkillSchema) MigrateLegacy();
                if (data.Passive == null) data.Passive = new PassiveProgressRecord { DescentId = Session.Descents };
                data.Herbs = Mathf.Clamp(data.Herbs, 0, 999999);
                if (data.EquippedSkillIds == null || data.EquippedSkillIds.Length != 2)
                    data.EquippedSkillIds = AbilityBook.DefaultLoadoutIds(Session.Class.Id);
                for (int i = 0; i < data.Skills.Count; i++)
                {
                    SkillProgressRecord skill = data.Skills[i];
                    if (skill == null || string.IsNullOrEmpty(skill.Id)) { data.Skills.RemoveAt(i--); continue; }
                    skill.Tier = Mathf.Clamp(skill.Tier, 0, 3);
                    skill.Stock = Mathf.Clamp(skill.Stock, 0, 999999);
                    for (int j = data.Skills.Count - 1; j > i; j--)
                        if (data.Skills[j] != null && data.Skills[j].Id == skill.Id)
                        {
                            skill.Tier = Mathf.Max(skill.Tier, data.Skills[j].Tier);
                            skill.Stock = Mathf.Max(skill.Stock, data.Skills[j].Stock);
                            skill.PowerBonus = Mathf.Max(skill.PowerBonus, data.Skills[j].PowerBonus);
                            data.Skills.RemoveAt(j);
                        }
                }
                if (data.EquippedSkillIds[0] == data.EquippedSkillIds[1]) data.EquippedSkillIds[1] = string.Empty;
                normalized = true;
            }
            finally { normalizing = false; }
        }
        SkillProgressRecord Record(string id, bool create = false)
        {
            SkillProgressRecord record = data.Skills.Find(s => s != null && s.Id == id);
            if (record == null && create) { record = new SkillProgressRecord { Id = id }; data.Skills.Add(record); }
            return record;
        }
        void MigrateLegacy()
        {
            foreach (string id in AbilityBook.StarterIds(Session.Class.Id))
                Record(id, true).Tier = Mathf.Max(1, Record(id).Tier);
            var oldTiers = new[] { data.AbilityOneTier, data.AbilityTwoTier, 1, 1 };
            if (data.AbilityTiers != null)
                for (int i = 0; i < Mathf.Min(4, data.AbilityTiers.Length); i++) oldTiers[i] = data.AbilityTiers[i];
            for (int i = 0; i < 4; i++)
            {
                string id = AbilityBook.LegacyId(Session.Class.Id, i);
                AbilityDefinition ability = AbilityBook.Find(id);
                if (ability == null) continue;
                int tier = Mathf.Clamp(oldTiers[i], 1, 3);
                Record(id, true).Tier = Mathf.Max(Record(id).Tier, tier);
                // Recipes now rank through INT. Return paid upgrades rather than unlocking unrelated recipes.
                if (ability.IsRecipe) data.PendingSkillRefund += Investment(tier);
            }
            data.EquippedSkillIds = new string[2];
            for (int slot = 0; slot < 2; slot++)
            {
                int old = data.EquippedAbilities != null && data.EquippedAbilities.Length > slot ? data.EquippedAbilities[slot] : slot;
                data.EquippedSkillIds[slot] = AbilityBook.LegacyId(Session.Class.Id, old);
            }
            data.Passive = new PassiveProgressRecord { DescentId = Session.Descents };
            if (Session.Class.Id == CharacterClass.Alchemist)
            {
                data.Herbs = Mathf.Max(6, data.Herbs);
                bool hasStock = data.Skills.Exists(s => s != null && s.Stock > 0);
                if (!hasStock) Record("starter_tonic", true).Stock = 2;
            }
            if (Session.Class.Id == CharacterClass.Necromancer)
            {
                foreach (string id in data.EquippedSkillIds)
                {
                    AbilityDefinition skill = AbilityBook.Find(id);
                    if (skill != null && skill.Path != NecromancerPath.None) { data.NecromancerPath = skill.Path; break; }
                }
                if (data.NecromancerPath == NecromancerPath.None) data.NecromancerPath = NecromancerPath.Attack;
                foreach (SkillProgressRecord skill in data.Skills)
                {
                    AbilityDefinition ability = AbilityBook.Find(skill.Id);
                    if (ability == null || ability.Path == NecromancerPath.None || ability.Path == data.NecromancerPath) continue;
                    data.PendingSkillRefund += Investment(skill.Tier);
                    skill.Tier = Mathf.Min(skill.Tier, 1);
                }
                for (int slot = 0; slot < 2; slot++)
                {
                    AbilityDefinition ability = AbilityBook.Find(data.EquippedSkillIds[slot]);
                    if (ability != null && ability.Path != NecromancerPath.None && ability.Path != data.NecromancerPath)
                        data.EquippedSkillIds[slot] = string.Empty;
                }
            }
            data.SkillSchemaVersion = SkillSchema;
        }
        static int Investment(int tier) => (tier >= 2 ? 25 : 0) + (tier >= 3 ? 48 : 0);
        void ApplyMigrationRefund()
        {
            if (data.PendingSkillRefund <= 0 || SoulKeeper.Instance == null || !SoulKeeper.Instance.HasSoul) return;
            SoulKeeper keeper = SoulKeeper.Instance;
            SoulRecord soul = keeper.LatestSoul();
            if (soul == null) return;
            string key = soul.Id + ":skill-schema2";
            if (keeper.Meta.SkillMigrationRefunds == null) keeper.Meta.SkillMigrationRefunds = new List<string>();
            int refund = data.PendingSkillRefund;
            data.PendingSkillRefund = 0;
            if (!keeper.Meta.SkillMigrationRefunds.Contains(key))
            {
                // Receipt and Souls share one meta write: even reloading an old record cannot pay twice.
                keeper.Meta.SkillMigrationRefunds.Add(key);
                keeper.AddSouls(refund);
            }
            soul.Progress = Capture();
            SaveStore.Put(soul);
        }
        bool ResetDescentIfChanged()
        {
            if (data.Passive.DescentId == Session.Descents) return false;
            PassiveProgressRecord passive = data.Passive;
            passive.DescentId = Session.Descents;
            passive.HealthBonus = passive.FocusBonus = passive.AttackBonus = passive.DefenseBonus = passive.MagicBonus = passive.SpeedBonus = 0;
            passive.RogueLuck = 0;
            passive.VigilUsed = passive.VigilActive = false;
            passive.VigilRemaining = 0f;
            passive.VigilTarget = null;
            data.NecromancerPath = NecromancerPath.None;
            foreach (SkillProgressRecord skill in data.Skills) skill.PowerBonus = 0f;
            if (Session.Class.Id == CharacterClass.Necromancer)
                for (int slot = 0; slot < 2; slot++)
                {
                    AbilityDefinition skill = AbilityBook.Find(data.EquippedSkillIds[slot]);
                    if (skill != null && skill.Path != NecromancerPath.None) data.EquippedSkillIds[slot] = string.Empty;
                }
            return true;
        }
        public void SyncDescentState()
        {
            EnsureData();
            if (!ResetDescentIfChanged()) return;
            ApplyRuntime();
            SoulKeeper.Instance?.Save();
        }
        public void ApplyRuntime()
        {
            if (Session == null || combat == null || health == null || abilities == null) return;
            EnsureData();
            ResetDescentIfChanged();
            PassiveProgressRecord p = data.Passive;
            combat.ApplyProgression(data.AttackBonus + p.AttackBonus, data.MagicBonus + IntellectFocus + p.MagicBonus,
                data.DefenseBonus + p.DefenseBonus, data.SpeedBonus + p.SpeedBonus);
            AbilityDefinition[] book = AbilityBook.All(Session.Class);
            var tiers = new int[book.Length];
            for (int i = 0; i < book.Length; i++) tiers[i] = SkillTier(book[i].Id);
            var slots = new[] { AbilityBook.IndexOf(Session.Class, data.EquippedSkillIds[0]), AbilityBook.IndexOf(Session.Class, data.EquippedSkillIds[1]) };
            abilities.ApplyProgression(data.FocusBonus + IntellectFocus + p.FocusBonus, tiers, slots);
            health.SetMax(Mathf.Max(1, MaxHealth), false);
            if (PlayerTorch.Instance != null) PlayerTorch.Instance.SetEquipped(data.TorchEquipped);
        }
        public int SkillTier(string id)
        {
            EnsureData();
            AbilityDefinition skill = AbilityBook.Find(id);
            if (skill == null) return 0;
            if (skill.IsRecipe)
                return Session.Class.Id == CharacterClass.Alchemist && data.Intellect >= skill.IntellectRequired ?
                    Mathf.Clamp(1 + (data.Intellect - skill.IntellectRequired) / 10, 1, 3) : 0;
            SkillProgressRecord record = Record(id);
            return record == null ? 0 : Mathf.Min(record.Tier, MaxSkillTier(skill));
        }
        int MaxSkillTier(AbilityDefinition skill) => AbilityBook.IsNative(Session.Class, skill) ? Mathf.Min(3, skill.Tiers?.Length ?? 3) : 1;
        public float SkillPowerBonus(string id) { EnsureData(); return Record(id)?.PowerBonus ?? 0f; }
        public int KnownAbilityTier(int index) => SkillTier(AbilityBook.At(Session.Class, index)?.Id);
        public string EquippedAbilityId(int slot) { EnsureData(); return slot >= 0 && slot < 2 ? data.EquippedSkillIds[slot] : string.Empty; }
        public int EquippedAbilityIndex(int slot) => AbilityBook.IndexOf(Session.Class, EquippedAbilityId(slot));
        public int AbilityTier(int slot) => SkillTier(EquippedAbilityId(slot));
        public int KnownAbilityUpgradeCost(int index) => SkillUpgradeCost(AbilityBook.At(Session.Class, index)?.Id);
        public int AbilityUpgradeCost(int slot) => SkillUpgradeCost(EquippedAbilityId(slot));
        public int SkillUpgradeCost(string id)
        {
            AbilityDefinition skill = AbilityBook.Find(id);
            if (skill == null || skill.IsRecipe) return 0;
            int tier = SkillTier(id);
            return tier >= MaxSkillTier(skill) ? 0 : tier == 0 ? LearnCost : tier == 1 ? 25 : 48;
        }
        public bool CanLearnSkill(string id)
        {
            EnsureData();
            AbilityDefinition skill = AbilityBook.Find(id);
            if (skill == null || skill.Hidden || skill.IsRecipe || Session.Class.Id == CharacterClass.Alchemist ||
                (!AbilityBook.IsNative(Session.Class, skill) && skill.Signature) || !PathAllows(skill)) return false;
            return AbilityBook.IndexOf(Session.Class, id) >= 0 && SkillTier(id) < MaxSkillTier(skill);
        }
        bool PathAllows(AbilityDefinition skill) => Session.Class.Id != CharacterClass.Necromancer || skill.Path == NecromancerPath.None ||
            (data.NecromancerPath != NecromancerPath.None && skill.Path == data.NecromancerPath);
        public bool CanEquipSkill(string id)
        {
            EnsureData();
            AbilityDefinition skill = AbilityBook.Find(id);
            return skill != null && AbilityBook.IndexOf(Session.Class, id) >= 0 && SkillTier(id) > 0 && PathAllows(skill) &&
                (Session.Class.Id != CharacterClass.Alchemist || skill.IsRecipe) &&
                (AbilityBook.IsNative(Session.Class, skill) || !skill.Signature);
        }
        public bool TryLearnOrUpgradeSkill(string id)
        {
            if (!CanLearnSkill(id) || !SoulKeeper.Instance.TrySpendSouls(SkillUpgradeCost(id))) return false;
            Record(id, true).Tier = SkillTier(id) + 1;
            ApplyRuntime();
            SoulKeeper.Instance.Save();
            return true;
        }
        public bool TryUpgradeKnownAbility(int index) => TryLearnOrUpgradeSkill(AbilityBook.At(Session.Class, index)?.Id);
        public bool TryUpgradeAbility(int slot) => slot >= 0 && slot < 2 && TryLearnOrUpgradeSkill(EquippedAbilityId(slot));
        public bool TryEquipAbility(int slot, int index)
        {
            AbilityDefinition skill = AbilityBook.At(Session.Class, index);
            return skill != null && TryEquipAbility(slot, skill.Id);
        }
        public bool TryEquipAbility(int slot, string id)
        {
            if (slot < 0 || slot >= 2 || (!string.IsNullOrEmpty(id) && !CanEquipSkill(id))) return false;
            EnsureData();
            if (!string.IsNullOrEmpty(id) && data.EquippedSkillIds[1 - slot] == id)
                data.EquippedSkillIds[1 - slot] = data.EquippedSkillIds[slot];
            data.EquippedSkillIds[slot] = id ?? string.Empty;
            ApplyRuntime();
            SoulKeeper.Instance.Save();
            return true;
        }
        public bool TryChoosePath(NecromancerPath path)
        {
            EnsureData();
            if (Session.Class.Id != CharacterClass.Necromancer || (path != NecromancerPath.Attack && path != NecromancerPath.Defense) || data.NecromancerPath != NecromancerPath.None) return false;
            data.NecromancerPath = path;
            ApplyRuntime();
            SoulKeeper.Instance.Save();
            return true;
        }
        public int RecipeStock(string id) { EnsureData(); return Record(id)?.Stock ?? 0; }
        public bool TryCraftRecipe(string id)
        {
            EnsureData();
            AbilityDefinition skill = AbilityBook.Find(id);
            if (skill == null || !skill.IsRecipe || !CanEquipSkill(id) || data.Herbs < skill.PlantCost) return false;
            data.Herbs -= skill.PlantCost;
            Record(id, true).Stock++;
            SoulKeeper.Instance.Save();
            return true;
        }
        public bool TryConsumeRecipe(string id)
        {
            EnsureData();
            AbilityDefinition skill = AbilityBook.Find(id);
            SkillProgressRecord record = Record(id);
            if (skill == null || !skill.IsRecipe || !CanEquipSkill(id) || record == null || record.Stock <= 0) return false;
            record.Stock--;
            SoulKeeper.Instance.Save();
            return true;
        }
        public void AddHerbs(int amount) { EnsureData(); data.Herbs = Mathf.Clamp(data.Herbs + amount, 0, 999999); SoulKeeper.Instance?.Save(); }
        public void AddFood(int amount) { EnsureData(); data.Food = Mathf.Clamp(data.Food + amount, 0, 100); SoulKeeper.Instance?.Save(); }
        public bool TryBuyHerbs()
        {
            if (!CanBuyHerbs || !Session.TrySpend(5)) return false;
            AddHerbs(3);
            return true;
        }
        public void GrantAccretion()
        {
            EnsureData();
            if (Session.Class.Id != CharacterClass.Witch) return;
            PassiveProgressRecord p = data.Passive;
            switch (UnityEngine.Random.Range(0, 6))
            {
                case 0: p.HealthBonus += 5; break;
                case 1: p.FocusBonus += 5; break;
                case 2: p.AttackBonus += 5; break;
                case 3: p.DefenseBonus += 5; break;
                case 4: p.MagicBonus += 5; break;
                case 5: p.SpeedBonus += 5; break;
            }
            var choices = new List<AbilityDefinition>();
            foreach (AbilityDefinition skill in AbilityBook.All(Session.Class))
                if (SkillTier(skill.Id) > 0 && skill.Tier(SkillTier(skill.Id)).Power > 0f) choices.Add(skill);
            if (choices.Count > 0) Record(choices[UnityEngine.Random.Range(0, choices.Count)].Id, true).PowerBonus += 5f;
            ApplyRuntime();
            SoulKeeper.Instance.Save();
        }
        public bool TryRaiseStat(int statIndex)
        {
            if (statIndex < 0 || statIndex > 5 || !SoulKeeper.Instance.TrySpendSouls(UpgradeCost)) return false;
            switch (statIndex)
            {
                case 0: data.HealthBonus += 8; break;
                case 1: if (Session.Class.Id == CharacterClass.Alchemist) data.Intellect += 2; else data.FocusBonus += 2; break;
                case 2: data.AttackBonus += 2; break;
                case 3: data.DefenseBonus += 2; break;
                case 4: data.MagicBonus += 2; break;
                case 5: data.SpeedBonus += 2; break;
            }
            data.Level++;
            ApplyRuntime();
            if (statIndex == 0) health.Heal(8f);
            if (statIndex == 1) abilities.AddFocus(2f);
            SoulKeeper.Instance.Save();
            return true;
        }
        public void ToggleTorch() { data.TorchEquipped = !data.TorchEquipped; ApplyRuntime(); SoulKeeper.Instance.Save(); }
        public void OfferTorchAtDusk()
        {
            if (Session.HasFlag("torch_presented")) return;
            Session.SetFlag("torch_presented");
            data.TorchEquipped = true;
            ApplyRuntime();
        }
        public DebugStats ReadDebug() => new DebugStats
        {
            MaxHealth = MaxHealth, Health = Mathf.RoundToInt(health.Current), MaxFocus = Mathf.RoundToInt(abilities.MaxFocus),
            Focus = Mathf.RoundToInt(abilities.Focus), Intellect = Intellect, Attack = Attack, Defense = Defense,
            Magic = Magic, Speed = Speed, Food = Food, Honor = Session.Honor, Level = Level, Gold = Session.Gold, Souls = SoulKeeper.Instance.Souls
        };
        public void ApplyDebug(DebugStats stats)
        {
            EnsureData();
            data.Intellect = Mathf.Clamp(stats.Intellect, 1, 9999);
            data.HealthBonus = Mathf.Clamp(stats.MaxHealth, 1, 99999) - Mathf.RoundToInt(Session.Class.MaxHealth + GearHealth * 1.6f) - data.Passive.HealthBonus;
            data.FocusBonus = Mathf.Clamp(stats.MaxFocus, 1, 9999) - Session.Class.MaxFocus - GearFocus - IntellectFocus - data.Passive.FocusBonus;
            data.AttackBonus = Mathf.Clamp(stats.Attack, 0, 9999) - Card.Attack - GearAttack - data.Passive.AttackBonus;
            data.DefenseBonus = Mathf.Clamp(stats.Defense, 0, 9999) - Card.Defense - data.Passive.DefenseBonus;
            data.MagicBonus = Mathf.Clamp(stats.Magic, 0, 9999) - Card.Magic - GearMagic - data.Passive.MagicBonus;
            data.SpeedBonus = Mathf.Clamp(stats.Speed, 1, 999) - Card.Speed - GearSpeed - data.Passive.SpeedBonus;
            data.Food = Mathf.Clamp(stats.Food, 0, 100);
            data.Level = Mathf.Clamp(stats.Level, 1, 999);
            Session.AddHonor(Mathf.Clamp(stats.Honor, -100, 100) - Session.Honor);
            Session.AddGold(Mathf.Clamp(stats.Gold, 0, 9999999) - Session.Gold);
            SoulKeeper.Instance.AddSouls(Mathf.Clamp(stats.Souls, 0, 9999999) - SoulKeeper.Instance.Souls);
            ApplyRuntime();
            health.SetCurrent(stats.Health);
            abilities.SetFocus(stats.Focus);
            SoulKeeper.Instance.Save();
        }
    }
}
