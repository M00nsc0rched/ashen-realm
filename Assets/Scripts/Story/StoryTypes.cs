using System.Collections.Generic;

namespace AshenRealm.Story
{
    public enum CharacterClass { Knight, Gravethief, Witch, Warden, Necromancer, Alchemist }
    public enum Alignment { Marked, Mortal, Hallowed }
    public enum AbilityEffect { ShieldBash, Rally, Backstab, PoisonBlade, LifeDrain, Hex, Smite, Mend, BoneSpear, BoneArmor, AcidFlask, HealingBrew }
    public enum EnemyBody { Humanoid, Beast, Flyer, Blob }
    public enum EnemyRank { Common, Elite, Guardian, Boss }
    public enum MoveKind { Melee, Bolt, Heal, Buff, Ward }
    public enum BoltKind { Arrow, Fire, Shadow, Holy, Venom, Brine }
    public enum NpcService { None, Tavern, Forge, Chapel, Market }
    public enum ItemSlot { Weapon, Relic }

    // Plain data records for the tables in Catalog and StoryText (grouped here on purpose: none of them is a Unity
    // object, so the one-type-per-file rule that serialization needs does not apply).

    public sealed class AbilityDefinition
    {
        public AbilityEffect Effect;
        public string Name;
        public string Description;
        public int Cost;
        public float Cooldown;
    }

    public sealed class ClassDefinition
    {
        public CharacterClass Id;
        public string Name;
        public string Role;
        public string Flavor;
        public string Backstory;
        public string Goal;
        public string Ending;
        public float MaxHealth;
        public int MaxFocus;
        public float FocusPerSecond;
        public int StartHonor;
        public float Damage;
        public float AttackCooldown;
        public float AttackDuration;
        public float BlockReduction;
        public float MoveMultiplier;
        /// <summary>The attack button throws a bolt instead of swinging.</summary>
        public bool Ranged;
        public AbilityDefinition Ability1;
        public AbilityDefinition Ability2;
    }

    /// <summary>
    /// One card of the class choice. The numbers are the class sheet of the design; the real-time values in
    /// <see cref="ClassDefinition"/> are scaled from it, so they are not the same figures.
    /// </summary>
    public sealed class ClassCard
    {
        /// <summary>Null while the class is not in the game: the sealed picture shows instead of the art, and it cannot be chosen.</summary>
        public ClassDefinition Class;
        /// <summary>Sprite name in Assets/Art/Menu/Cards, used once the class is in the game.</summary>
        public string Art;
        public string Name;
        public string Role;
        public int Honor;
        public int Health, Focus, Attack, Defense, Magic, Speed;
        /// <summary>The Alchemist brews on intellect, so the card's second number means that instead of focus.</summary>
        public bool UsesIntellect;
        public string PassiveName;
        public string PassiveText;
    }

    /// <summary>
    /// Something the Cold Forge or the Grey Chapel sells. The bonuses are Graveborne's points; PlayerClassRig turns them
    /// into real-time numbers. Steel is paid for in gold, relics in Souls.
    /// </summary>
    public sealed class ItemDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public ItemSlot Slot;
        public int Attack, Magic, Speed, Focus, Health;
        /// <summary>Share of the damage dealt that comes back as health.</summary>
        public float Lifesteal;
        public int Price;

        /// <summary>What Graveborne counts an item as worth: one point per bonus, half for health.</summary>
        public float Points => Attack + Magic + Speed + Focus + Health * 0.5f;
    }

    public sealed class EnemyMove
    {
        public string Name;
        public MoveKind Kind;
        public float Damage;
        public int Weight = 1;
        public BoltKind Bolt;
        public float PoisonDps;
        public float PoisonTime;
        public float StunTime;
        public float WeakenTime;
        public float Lifesteal;
        public float Heal;
        /// <summary>Bolts fired in one cast.</summary>
        public int Hits = 1;

        public EnemyMove Poison(float dps, float seconds) { PoisonDps = dps; PoisonTime = seconds; return this; }
        public EnemyMove Stun(float seconds) { StunTime = seconds; return this; }
        public EnemyMove Weaken(float seconds) { WeakenTime = seconds; return this; }
        public EnemyMove Drain(float fraction) { Lifesteal = fraction; return this; }
        public EnemyMove Volley(int hits) { Hits = hits; return this; }
    }

    public sealed class EnemyDialogue
    {
        public string Intro;
        public string Knight, Gravethief, Witch, Warden;
        public string Marked, Hallowed;
        public string Defeat;

        public string ForClass(CharacterClass id)
        {
            switch (id)
            {
                case CharacterClass.Knight: return Knight;
                case CharacterClass.Gravethief: return Gravethief;
                case CharacterClass.Witch: return Witch;
                case CharacterClass.Warden: return Warden;
                default: return string.Empty;
            }
        }
    }

    public sealed class EnemyDefinition
    {
        public string Id;
        public string Name;
        public EnemyRank Rank;
        public EnemyBody Body;
        public float Health;
        public float Speed;
        public float Scale = 1f;
        public int GoldMin, GoldMax;
        public bool Undead;
        public float AttackInterval = 2.2f;
        /// <summary>Bolt casters stop this far from the player instead of closing to melee.</summary>
        public float PreferredRange;
        public EnemyMove[] Moves;
        public EnemyDialogue Dialogue;
    }

    public sealed class EncounterChoice
    {
        public string Label;
        public string Result;
        public int Honor, Gold, GoldCost, Heal, Potions, Focus, Damage;
    }

    public sealed class EncounterVariant
    {
        public string Text;
        public EncounterChoice[] Choices;
    }

    public sealed class EncounterDefinition
    {
        public string Id;
        public string Name;
        public EncounterVariant Clear;
        /// <summary>What the dishonoured see instead. Null when the encounter looks the same to everyone.</summary>
        public EncounterVariant Warped;
    }

    public sealed class NpcDefinition
    {
        public string Id;
        public string Name;
        public string Intro;
        public string Knight, Gravethief, Witch, Warden;
        public string Marked, Hallowed;
        public string Fallback;
        public NpcService Service;

        public IEnumerable<string> Strings()
        {
            yield return Name;
            yield return Intro;
            yield return Knight;
            yield return Gravethief;
            yield return Witch;
            yield return Warden;
            yield return Marked;
            yield return Hallowed;
            yield return Fallback;
        }
    }
}
