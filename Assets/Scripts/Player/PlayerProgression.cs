using System;
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

    /// <summary>Run growth and the carried torch. Purchases use the same Souls ledger as the town.</summary>
    public sealed class PlayerProgression : MonoBehaviour
    {
        ProgressRecord data = new ProgressRecord();
        Health health;
        PlayerCombat combat;
        PlayerAbilities abilities;
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
        public int MaxHealth => Mathf.RoundToInt(Session.Class.MaxHealth + GearHealth * 1.6f + data.HealthBonus);
        public int MaxFocus => Mathf.Max(1, Session.Class.MaxFocus + GearFocus + data.FocusBonus + IntellectFocus);
        public int Intellect => data.Intellect;
        public int Attack => Mathf.Max(0, Card.Attack + GearAttack + data.AttackBonus);
        public int Defense => Mathf.Max(0, Card.Defense + data.DefenseBonus);
        public int Magic => Mathf.Max(0, Card.Magic + GearMagic + data.MagicBonus);
        public int Speed => Mathf.Max(1, Card.Speed + GearSpeed + data.SpeedBonus);
        public int Food => data.Food;
        public bool TorchEquipped => data.TorchEquipped;
        public int UpgradeCost => Mathf.CeilToInt(Mathf.Min(1000000f, 12f * Mathf.Pow(1.5f, Mathf.Min(Level - 1, 29))));

        void Awake()
        {
            Instance = this;
            health = GetComponent<Health>();
            combat = GetComponent<PlayerCombat>();
            abilities = GetComponent<PlayerAbilities>();
        }

        void Start()
        {
            Session.ClassSelected += ResetForClass;
            ApplyRuntime();
        }

        void OnDestroy()
        {
            if (Session != null) Session.ClassSelected -= ResetForClass;
            if (Instance == this) Instance = null;
        }

        void ResetForClass(ClassDefinition _)
        {
            data = new ProgressRecord { Intellect = Card.UsesIntellect ? Card.Focus : 10 };
            ApplyRuntime();
            health.SetCurrent(MaxHealth);
            abilities.SetFocus(MaxFocus);
        }

        public ProgressRecord Capture() => JsonUtility.FromJson<ProgressRecord>(JsonUtility.ToJson(data));

        public void Restore(ProgressRecord saved)
        {
            data = saved ?? new ProgressRecord { Intellect = Card.UsesIntellect ? Card.Focus : 10 };
            data.Level = Mathf.Clamp(data.Level, 1, 999);
            data.Food = Mathf.Clamp(data.Food, 0, 100);
            data.AbilityOneTier = Mathf.Clamp(data.AbilityOneTier, 1, 3);
            data.AbilityTwoTier = Mathf.Clamp(data.AbilityTwoTier, 1, 3);
            ApplyRuntime();
        }

        public void ApplyRuntime()
        {
            if (Session == null || combat == null || health == null || abilities == null) return;
            combat.ApplyProgression(data.AttackBonus, data.MagicBonus + IntellectFocus, data.DefenseBonus, data.SpeedBonus);
            abilities.ApplyProgression(data.FocusBonus + IntellectFocus, data.AbilityOneTier, data.AbilityTwoTier);
            health.SetMax(Mathf.Max(1, MaxHealth), false);
            if (PlayerTorch.Instance != null) PlayerTorch.Instance.SetEquipped(data.TorchEquipped);
        }

        public int AbilityTier(int slot) => slot == 0 ? data.AbilityOneTier : data.AbilityTwoTier;
        public int AbilityUpgradeCost(int slot) => AbilityTier(slot) >= 3 ? 0 : AbilityTier(slot) == 1 ? 25 : 48;

        public bool TryRaiseStat(int statIndex)
        {
            if (statIndex < 0 || statIndex > 5 || !SoulKeeper.Instance.TrySpendSouls(UpgradeCost)) return false;
            switch (statIndex)
            {
                case 0: data.HealthBonus += 8; break;
                case 1: data.FocusBonus += 2; break;
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

        public bool TryUpgradeAbility(int slot)
        {
            if (slot < 0 || slot > 1 || AbilityTier(slot) >= 3 || !SoulKeeper.Instance.TrySpendSouls(AbilityUpgradeCost(slot))) return false;
            if (slot == 0) data.AbilityOneTier++; else data.AbilityTwoTier++;
            ApplyRuntime();
            SoulKeeper.Instance.Save();
            return true;
        }

        public void ToggleTorch()
        {
            data.TorchEquipped = !data.TorchEquipped;
            ApplyRuntime();
            SoulKeeper.Instance.Save();
        }

        public void OfferTorchAtDusk()
        {
            if (Session.HasFlag("torch_presented")) return;
            Session.SetFlag("torch_presented");
            data.TorchEquipped = true;
            ApplyRuntime();
        }

        public DebugStats ReadDebug() => new DebugStats
        {
            MaxHealth = MaxHealth, Health = Mathf.RoundToInt(health.Current), MaxFocus = MaxFocus,
            Focus = Mathf.RoundToInt(abilities.Focus), Intellect = Intellect, Attack = Attack, Defense = Defense,
            Magic = Magic, Speed = Speed, Food = Food, Honor = Session.Honor, Level = Level, Gold = Session.Gold,
            Souls = SoulKeeper.Instance.Souls,
        };

        public void ApplyDebug(DebugStats stats)
        {
            data.Intellect = Mathf.Clamp(stats.Intellect, 1, 9999);
            data.HealthBonus = Mathf.Clamp(stats.MaxHealth, 1, 99999) - Mathf.RoundToInt(Session.Class.MaxHealth + GearHealth * 1.6f);
            data.FocusBonus = Mathf.Clamp(stats.MaxFocus, 1, 9999) - Session.Class.MaxFocus - GearFocus - IntellectFocus;
            data.AttackBonus = Mathf.Clamp(stats.Attack, 0, 9999) - Card.Attack - GearAttack;
            data.DefenseBonus = Mathf.Clamp(stats.Defense, 0, 9999) - Card.Defense;
            data.MagicBonus = Mathf.Clamp(stats.Magic, 0, 9999) - Card.Magic - GearMagic;
            data.SpeedBonus = Mathf.Clamp(stats.Speed, 1, 999) - Card.Speed - GearSpeed;
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
