using System.Collections.Generic;

namespace AshenRealm.Story
{
    public enum CharacterClass { Knight, Gravethief, Witch, Warden, Necromancer, Alchemist }
    public enum Alignment { Marked, Mortal, Hallowed }
    public enum AbilityEffect
    {
        ShieldBash = 0,
        Rally = 1,
        Backstab = 2,
        PoisonBlade = 3,
        LifeDrain = 4,
        Hex = 5,
        Smite = 6,
        Mend = 7,
        BoneSpear = 8,
        BoneArmor = 9,
        AcidFlask = 10,
        HealingBrew = 11,
        Whirlwind = 12,
        IronSkin = 13,
        FanOfKnives = 14,
        Caltrops = 15,
        FrostLance = 16,
        ChainLightning = 17,
        Sanctuary = 18,
        Consecration = 19,
        BoneNova = 20,
        SoulHarvest = 21,
        FireBomb = 22,
        FlashPowder = 23,
        Strike = 24,
        Guard = 25,
        Execute = 26,
        WarCry = 27,
        Zeal = 28,
        Aelanwyr = 29,
        Bash = 30,
        LeapSlam = 31,
        Taunt = 32,
        IgnorePain = 33,
        FuriousCharge = 34,
        SeismicSlam = 35,
        Revenge = 36,
        StalwartWard = 37,
        UndyingRage = 38,
        Bloodthirst = 39,
        ShieldCharge = 40,
        Rend = 41,
        Cleave = 42,
        PlagueTouch = 43,
        SiphonCut = 44,
        ShadowStrike = 45,
        Impale = 46,
        SmokeScreen = 47,
        VenomStrike = 48,
        GrimHarvest = 49,
        Puncture = 50,
        BladeFury = 51,
        Nightstalker = 52,
        ExploitWeakness = 53,
        DeathBlossom = 54,
        ShadowDance = 55,
        Lacerate = 56,
        HollowFire = 57,
        Meditate = 58,
        FrozenOrb = 59,
        Blizzard = 60,
        Inferno = 61,
        WitchBoneSpear = 62,
        CorpseExplosion = 63,
        Teeth = 64,
        AmplifyDamage = 65,
        IronMaiden = 66,
        EnergyShield = 67,
        StaticField = 68,
        GlacialSpike = 69,
        FireWall = 70,
        LowerResist = 71,
        Convince = 72,
        WardingPrayer = 73,
        ZealotSRally = 74,
        HolyBolt = 75,
        BlessedHammer = 76,
        FistOfTheHeavens = 77,
        Prayer = 78,
        Cleansing = 79,
        Vigor = 80,
        Redemption = 81,
        Conviction = 82,
        HolyShield = 83,
        Salvation = 84,
        ZealotSOath = 85,
        DivineGrace = 86,
        LastRites = 87,
        AuraOfLight = 88,
        NecromancerTeeth = 89,
        NecromancerCorpseExplosion = 90,
        NecromancerBoneSpirit = 91,
        NecromancerAmplifyDamage = 92,
        NecromancerLowerResist = 93,
        NecromancerBoneWall = 94,
        NecromancerBonePrison = 95,
        NecromancerClayGolem = 96,
        NecromancerDimVision = 97,
        NecromancerWeaken = 98,
        NecromancerLifeTap = 99,
        NecromancerDecrepify = 100,
        Charm = 101,
        BitterTonic = 102,
        WardingTonic = 103,
        NourishingStew = 104,
        MiasmaVial = 105,
        BrewOfVigor = 106,
        SolventVial = 107,
        ShatterGlass = 108,
        DraughtOfFervor = 109,
        MarrowCordial = 110,
    }
    public enum AbilityKind { Physical, Magic, Defense, Buff, Heal, Charm }
    public enum AbilityDelivery { Melee, Bolt, Fan, Chain, Nova, Field, Charge, Leap, Self, Target, Orb }
    public enum NecromancerPath { None, Attack, Defense }
    public enum EnemyBody { Humanoid, Beast, Flyer, Blob }
    public enum EnemyRank { Common, Elite, Guardian, Boss }
    public enum MoveKind { Melee, Bolt, Heal, Buff, Ward, Charge, Sweep, Pounce, Ground, Teleport, Volley, Ring, Counter, Burrow }
    public enum BoltKind { Arrow, Fire, Shadow, Holy, Venom, Brine }
    public enum NpcService { None, Tavern, Forge, Chapel, Market, Stable }
    public enum ItemSlot { Weapon, Relic }

    // Plain data records for the tables in Catalog and StoryText (grouped here on purpose: none of them is a Unity
    // object, so the one-type-per-file rule that serialization needs does not apply).

    public sealed class AbilityDefinition
    {
        public string Id;
        public string Source;
        public CharacterClass Owner;
        public bool Common, Hidden, Signature, IsRecipe;
        public NecromancerPath Path;
        public AbilityKind Kind;
        public AbilityDelivery Delivery;
        public int PlantCost, IntellectRequired;
        public float Range, Radius;
        public AbilityEffect Effect;
        public string Name;
        public string Description;
        public int Cost;
        public float Cooldown;
        public AbilityTierDefinition[] Tiers;

        /// <summary>Workbook tiers are numbered I-III. Single-tier follower skills and recipes clamp to I.</summary>
        public AbilityTierDefinition Tier(int tier)
        {
            if (Tiers == null || Tiers.Length == 0) return new AbilityTierDefinition();
            return Tiers[System.Math.Max(0, System.Math.Min(tier - 1, Tiers.Length - 1))];
        }
    }

    /// <summary>Workbook points and turn counts. Chance/ratio fields are fractions in the range 0-1.</summary>
    public sealed class AbilityTierDefinition
    {
        public float Power;
        public int Hits = 1;
        public float Accuracy = 1f;
        public float CriticalChance, StunChance, Lifesteal, ExecuteThreshold;
        public float PoisonPerTurn;
        public int PoisonTurns;
        public float AttackReduction;
        public int AttackReductionTurns;
        public float ShieldDefMultiplier, RegenerationPerTurn;
        public int RegenerationTurns;
        public float AttackBonus;
        public int AttackBonusTurns;
        public float HealFlat, HealMagMultiplier, SelfDamage, RestoreFocus, ConvertChance;
        public float StealFraction, StealBossFraction;
        public int StealTurns;
        public bool Holy, CleansePoison;
        public string Summary;
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
        public float Windup = 0.7f;
        public float Recovery = 0.65f;
        public float MinRange;
        public float Range;
        public float Radius = 2.5f;
        public float ActiveTime = 0.45f;
        public float TravelDistance = 6f;
        public float Cooldown;
        public int MinPhase = 1;

        public EnemyMove Poison(float dps, float seconds) { PoisonDps = dps; PoisonTime = seconds; return this; }
        public EnemyMove Stun(float seconds) { StunTime = seconds; return this; }
        public EnemyMove Weaken(float seconds) { WeakenTime = seconds; return this; }
        public EnemyMove Drain(float fraction) { Lifesteal = fraction; return this; }
        public EnemyMove Volley(int hits) { Hits = hits; return this; }
        public EnemyMove Timing(float windup, float recovery) { Windup = windup; Recovery = recovery; return this; }
        public EnemyMove Reach(float min, float max) { MinRange = min; Range = max; return this; }
        public EnemyMove Area(float radius) { Radius = radius; return this; }
        public EnemyMove Travel(float distance, float seconds) { TravelDistance = distance; ActiveTime = seconds; return this; }
        public EnemyMove Phase(int phase) { MinPhase = phase; return this; }
        public EnemyMove Rest(float seconds) { Cooldown = seconds; return this; }
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
        /// <summary>Circling while in reach; negative and positive values choose opposite sides.</summary>
        public float Strafe;
        public float PhaseTwoAt = 0.55f;
        /// <summary>Zero for a two-phase foe.</summary>
        public float PhaseThreeAt;
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
