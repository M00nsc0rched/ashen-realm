using System;
using System.Collections.Generic;

namespace AshenRealm.Story
{
    /// <summary>Stable workbook IDs own learning and equipment. Indices are class-local presentation only.</summary>
    public static class AbilityBook
    {
        public const float TurnSeconds = 2f;
        public const int DefaultShieldTurns = 3;
        public const int DefaultStunTurns = 1;
        public const float CriticalMultiplier = 2f;
        // Compatibility allocation ceiling. New callers must use Count(definition).
        public const int KnownCount = 111;
        public static readonly AbilityDefinition[] AllDefinitions = WorkbookAbilityData.Create();
        static readonly Dictionary<string, AbilityDefinition> ById = BuildLookup();
        static readonly Dictionary<CharacterClass, AbilityDefinition[]> ClassCatalogs = new Dictionary<CharacterClass, AbilityDefinition[]>();

        static Dictionary<string, AbilityDefinition> BuildLookup()
        {
            var result = new Dictionary<string, AbilityDefinition>(StringComparer.Ordinal);
            foreach (AbilityDefinition ability in AllDefinitions) result.Add(ability.Id, ability);
            return result;
        }
        public static AbilityDefinition Find(string id)
        {
            AbilityDefinition ability;
            return id != null && ById.TryGetValue(id, out ability) ? ability : null;
        }
        public static AbilityDefinition[] All(ClassDefinition definition)
        {
            if (definition == null) return new AbilityDefinition[0];
            AbilityDefinition[] result;
            if (ClassCatalogs.TryGetValue(definition.Id, out result)) return result;
            var items = new List<AbilityDefinition>();
            foreach (AbilityDefinition ability in AllDefinitions)
            {
                if (ability.Hidden) continue;
                if (definition.Id == CharacterClass.Alchemist)
                {
                    if (ability.IsRecipe) items.Add(ability);
                }
                else if (!ability.IsRecipe && IsNative(definition, ability)) items.Add(ability);
            }
            if (definition.Id != CharacterClass.Alchemist)
                foreach (AbilityDefinition ability in AllDefinitions)
                    if (!ability.Hidden && !ability.IsRecipe && !ability.Signature && !IsNative(definition, ability)) items.Add(ability);
            result = items.ToArray();
            ClassCatalogs.Add(definition.Id, result);
            return result;
        }
        public static int Count(ClassDefinition definition) => All(definition).Length;
        public static AbilityDefinition At(ClassDefinition definition, int index)
        {
            AbilityDefinition[] items = All(definition);
            return index >= 0 && index < items.Length ? items[index] : null;
        }
        public static int IndexOf(ClassDefinition definition, string id)
        {
            AbilityDefinition[] items = All(definition);
            for (int i = 0; i < items.Length; i++) if (string.Equals(items[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }
        public static bool IsNative(ClassDefinition definition, AbilityDefinition ability) =>
            definition != null && ability != null && !ability.Hidden && (ability.Common || ability.Owner == definition.Id);
        public static float PowerFactor(ClassDefinition definition, AbilityDefinition ability) => IsNative(definition, ability) ? 1f : 0.4f;
        public static string[] StarterIds(CharacterClass id)
        {
            switch (id)
            {
                case CharacterClass.Knight: return new[] { "strike", "guard", "shieldbash", "rally" };
                case CharacterClass.Gravethief: return new[] { "strike", "backstab", "poisonblade", "bleed" };
                case CharacterClass.Witch: return new[] { "firebolt", "hex", "drain", "meditate" };
                case CharacterClass.Warden: return new[] { "strike", "smite", "prayer", "heal" };
                case CharacterClass.Necromancer: return new[] { "strike" };
                case CharacterClass.Alchemist: return new[] { "starter_tonic" };
                default: return new string[0];
            }
        }
        public static string[] DefaultLoadoutIds(CharacterClass id)
        {
            switch (id)
            {
                case CharacterClass.Knight: return new[] { "shieldbash", "rally" };
                case CharacterClass.Gravethief: return new[] { "backstab", "poisonblade" };
                case CharacterClass.Witch: return new[] { "firebolt", "hex" };
                case CharacterClass.Warden: return new[] { "smite", "prayer" };
                case CharacterClass.Necromancer: return new[] { "strike", string.Empty };
                case CharacterClass.Alchemist: return new[] { "starter_tonic", string.Empty };
                default: return new[] { string.Empty, string.Empty };
            }
        }
        public static string PassiveName(CharacterClass id) => WorkbookAbilityData.PassiveNames[ClassIndex(id)];
        public static string PassiveDescription(CharacterClass id) => WorkbookAbilityData.PassiveDescriptions[ClassIndex(id)];
        public static string PassiveName(ClassDefinition definition) => definition == null ? string.Empty : PassiveName(definition.Id);
        public static string PassiveDescription(ClassDefinition definition) => definition == null ? string.Empty : PassiveDescription(definition.Id);
        static int ClassIndex(CharacterClass id) => Math.Max(0, Math.Min((int)id, 5));

        /// <summary>Version-one souls used four indices per class. Keep this mapping explicit forever.</summary>
        public static string LegacyId(CharacterClass id, int oldIndex)
        {
            if (oldIndex < 0 || oldIndex > 3) return string.Empty;
            switch (id)
            {
                case CharacterClass.Knight: return new[] { "shieldbash", "rally", "whirlwind", "ironskin" }[oldIndex];
                case CharacterClass.Gravethief: return new[] { "backstab", "poisonblade", "fanofknives", "caltrops" }[oldIndex];
                case CharacterClass.Witch: return new[] { "drain", "hex", "frostlance", "chainlightning" }[oldIndex];
                case CharacterClass.Warden: return new[] { "smite", "heal", "sanctuary", "consecration" }[oldIndex];
                case CharacterClass.Necromancer: return new[] { "n_bonespear", "n_bonearmor", "n_poisonnova", "n_bloodstar" }[oldIndex];
                case CharacterClass.Alchemist: return new[] { "flask_vitriol", "elixir_mend", "flask_pyre", "vial_torpor" }[oldIndex];
                default: return string.Empty;
            }
        }
    }
}
