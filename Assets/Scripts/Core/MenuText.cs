using AshenRealm.Story;

namespace AshenRealm.Core
{
    /// <summary>
    /// Every string of the main menu, the Codex and the settings. These screens draw with Spectral, not the HUD's web font,
    /// so they may use the en dash; the validator checks them against Spectral's own glyphs.
    /// </summary>
    public static class MenuText
    {
        // Main menu
        public const string SoulsFormat = "{0} lélek";
        public const string StatsFormat = "Lemerülések: {0}   ·   Legmélyebb: {1}   ·   Kódex: {2}/{3}";
        public const string YourSouls = "A lelkeid";
        public const string NewSoul = "Új lélek";
        public const string Codex = "Kódex";
        public const string Settings = "Beállítások";
        public const string SlotTitleFormat = "{0} – Szint {1}";
        public const string Untouched = "Érintetlen";
        public const string DescentsFormat = "{0} lemerülés";
        public const string NoSouls = "Senki nem áll Üregkapuban. Valakinek állnia kell, mielőtt bármi lemehet a lépcsőn.";
        public const string PageFormat = "{0}/{1}";
        public const string ReleaseSoul = "Elengedés";
        public const string ReleaseQuestionFormat = "Elengeded ezt a lelket: {0}?\nA mentése végleg törlődik. A gyűjtött lelkek és a Kódex megmaradnak.";
        public const string ChevronLeft = "‹";
        public const string ChevronRight = "›";
        public const string Yes = "Igen";
        public const string Cancel = "Mégse";

        // The counters of the forge and the chapel
        public const string Buy = "Megveszem";
        public const string Carried = "Nálad van";
        public const string BalanceFormat = "{0} arany   ·   {1} lélek";
        public const string PriceGoldFormat = "{0} arany";
        public const string PriceSoulsFormat = "{0} lélek";
        public const string BoughtFormat = "{0} a tiéd.";
        public const string BoughtTradedFormat = "{0} a tiéd. Beszámítva: {1}, +{2} arany.";
        public const string DamageFormat = "Sebzés: {0} › {1}";
        public const string HealthFormat = "+{0} ÉP";
        public const string LifestealFormat = "Életszívás: {0}%";
        public const string GearFormat = "Fegyver: {0}   ·   Ereklye: {1}";
        public const string GearNothing = "semmi";

        // Class choice
        public const string ChooseSoul = "VÁLASZD KI AZ ELKÁRHOZOTTAD";
        public const string SwipeHint = "Lapozz oldalra";
        public const string StatHealth = "ÉP";
        public const string StatFocus = "FP";
        public const string StatIntellect = "INT";
        public const string StatAttack = "TÁM";
        public const string StatDefense = "VÉD";
        public const string StatMagic = "MÁG";
        public const string StatSpeed = "GYO";
        public const string StartHonor = "Kezdő becsület:";
        public const string HonorFormat = "{0} ({1})";
        public const string Choose = "Kiválasztás";
        public const string Locked = "Zárolva";
        public const string Begin = "Indulás";
        public const string GoalFormat = "Cél: {0}";
        public const string AbilityFormat = "{0}: {1}";

        // Honor tiers, Graveborne's names and thresholds
        public const string TierRadiant = "Ragyogó";
        public const string TierHonorable = "Becsületes";
        public const string TierNeutral = "Semleges";
        public const string TierTainted = "Beszennyezett";
        public const string TierVile = "Aljas";

        // Codex
        public const string CodexTitleFormat = "Kódex – {0}/{1}";
        public const string CodexFoes = "Ellenfelek";
        public const string CodexEncounters = "Találkozások";
        public const string CodexPeople = "Üregkapu népe";
        public const string CodexPlaces = "Helyek";
        public const string Undiscovered = "???";
        public const string UndiscoveredHint = "Még nem fedezted fel.";
        public const string CodexPick = "Válassz egy bejegyzést.";
        public const string RankCommon = "Közönséges";
        public const string RankElite = "Elit";
        public const string RankGuardian = "Őrző";
        public const string RankBoss = "A mélység ura";
        public const string FoeFormat = "{0}\nÉleterő: {1}\nFogásai: {2}";
        public const string KnightEntry = "Őrző\nA Hamvas Eskü lovagja, aki Hamuvölgyben elzárja az utat Üregkapu felé.";
        public const string WarpedSuffix = " (beszennyezve)";
        public const string CodexFound = "Kódex: {0}";

        // Settings
        public const string SettingsTitle = "Beállítások";
        public const string Language = "Nyelv";
        public const string Hungarian = "Magyar";
        public const string Sensitivity = "Érzékenység";
        public const string SaveSection = "Mentés";
        public const string Save = "Mentés";
        public const string Load = "Betöltés";
        public const string Saved = "Elmentve.";
        public const string NothingToSave = "Nincs játékban lévő lélek.";
        public const string NothingToLoad = "Még nincs mentésed.";
        public const string LoadQuestionFormat = "Betöltöd ezt a mentést: {0}?\nAmi azóta történt, elvész.";
        public const string Controls = "Irányítás";
        public const string ButtonsShown = "Gombok láthatók";
        public const string ButtonsHidden = "Gombok rejtve";
        public const string ButtonOpacity = "Gombok áttetszősége";
        public const string HudSection = "HUD";
        public const string HudShown = "HUD látható";
        public const string HudHidden = "HUD rejtve";
        public const string HudOpacity = "HUD áttetszősége";
        public const string MoveButtons = "Gombok mozgatása";
        public const string Defaults = "Alaphelyzet";
        public const string Back = "Vissza";
        public const string PercentFormat = "{0}%";

        // Button layout editor
        public const string LayoutHint = "Húzd a gombokat oda, ahol kényelmes.";
        public const string LayoutDone = "Kész";

        // Pause menu
        public const string Paused = "Szünet";
        public const string Resume = "Folytatás";
        public const string Fullscreen = "Teljes képernyő";
        public const string ExitFullscreen = "Ablakos mód";
        public const string MainMenu = "Főmenü";

        /// <summary>The honor tier a soul's card shows.</summary>
        public static string TierOf(int honor)
        {
            if (honor >= 60) return TierRadiant;
            if (honor >= 20) return TierHonorable;
            if (honor >= -19) return TierNeutral;
            return honor >= -59 ? TierTainted : TierVile;
        }

        /// <summary>Letter-spaced for a heading: the UI text has no tracking of its own.</summary>
        public static string Tracked(string text) => string.Join(" ", text.ToCharArray()).Replace("   ", "     ");

        /// <summary>"A Sírtolvaj", "Az Őrző": Hungarian picks the article by the sound that follows.</summary>
        public static string WithArticle(string name) =>
            ("AÁEÉIÍOÓÖŐUÚÜŰ".IndexOf(name[0]) >= 0 ? "Az " : "A ") + name;

        /// <summary>An item's bonuses as Graveborne prints them: "+4 TÁM   +1 GYO".</summary>
        public static string ModsOf(ItemDefinition item)
        {
            var parts = new System.Collections.Generic.List<string>();
            void Add(int value, string label)
            {
                if (value != 0) parts.Add($"+{value} {label}");
            }
            Add(item.Attack, StatAttack);
            Add(item.Magic, StatMagic);
            Add(item.Speed, StatSpeed);
            Add(item.Focus, StatFocus);
            Add(item.Health, StatHealth);
            if (item.Lifesteal > 0f) parts.Add(string.Format(LifestealFormat, UnityEngine.Mathf.RoundToInt(item.Lifesteal * 100f)));
            return string.Join("   ", parts);
        }

        public static string RankOf(EnemyRank rank)
        {
            switch (rank)
            {
                case EnemyRank.Elite: return RankElite;
                case EnemyRank.Guardian: return RankGuardian;
                case EnemyRank.Boss: return RankBoss;
                default: return RankCommon;
            }
        }
    }
}
