using System;
using System.Collections.Generic;

namespace AshenRealm.Story
{
    // What SaveStore writes as JSON (grouped here on purpose: plain records, like StoryTypes).

    /// <summary>One soul in the roster: everything needed to put the player back where they stopped.</summary>
    [Serializable]
    public sealed class SoulRecord
    {
        public string Id;
        public int ClassId;
        public long SavedAt;
        public int WorldVersion;
        public int Honor, Gold, Potions, WeaponLevel, Descents;
        public string WeaponId, RelicId;
        public int Zone;
        public float X, Y, Z, Yaw;
        public float Health, Focus;
        public ProgressRecord Progress;
        public List<string> Flags = new List<string>();

        public CharacterClass Class => (CharacterClass)ClassId;
        public int Level => Progress != null ? Progress.Level : 1 + WeaponLevel;
    }

    [Serializable]
    public sealed class SoulRoster
    {
        public List<SoulRecord> Souls = new List<SoulRecord>();
    }

    /// <summary>What outlives every soul: the Souls currency, the records and the Codex.</summary>
    [Serializable]
    public sealed class MetaRecord
    {
        public int Souls, Descents, Deepest;
        public List<string> Codex = new List<string>();
        public List<int> UnlockedClasses = new List<int>();
        public List<string> SkillMigrationRefunds = new List<string>();
    }

    [Serializable]
    public sealed class ProgressRecord
    {
        public int Level = 1, Food = 100, Intellect = 10;
        public int HealthBonus, FocusBonus, AttackBonus, DefenseBonus, MagicBonus, SpeedBonus;
        public int AbilityOneTier = 1, AbilityTwoTier = 1;
        public int[] AbilityTiers;
        public int[] EquippedAbilities;
        public bool TorchEquipped;
        public int SkillSchemaVersion;
        public List<SkillProgressRecord> Skills;
        public string[] EquippedSkillIds;
        public NecromancerPath NecromancerPath;
        public int Herbs, PendingSkillRefund;
        public PassiveProgressRecord Passive;
    }

    [Serializable]
    public sealed class SkillProgressRecord
    {
        public string Id;
        public int Tier, Stock;
        public float PowerBonus;
    }

    [Serializable]
    public sealed class PassiveProgressRecord
    {
        public int DescentId;
        public int HealthBonus, FocusBonus, AttackBonus, DefenseBonus, MagicBonus, SpeedBonus;
        public int RogueLuck;
        public bool VigilUsed, VigilActive;
        public float VigilRemaining;
        public string VigilTarget;
    }
}
