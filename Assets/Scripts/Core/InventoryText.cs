namespace AshenRealm.Core
{
    public static class InventoryText
    {
        public const string DescentMenu = "Lemerülés menüje";
        public const string InventoryAndSkills = "Készlet és képességek";
        public const string Inventory = "KÉSZLET";
        public const string Abilities = "KÉPESSÉGEK";
        public const string Pack = "Csomag";
        public const string Properties = "Tulajdonságok";
        public const string Equipment = "Felszerelés";
        public const string Close = "Bezár";
        public const string Apply = "Alkalmaz";
        public const string Save = "Lemerülés letétele (mentés)";
        public const string Saved = "A lemerülést elmentetted.";
        public const string Torch = "Fáklya";
        public const string TorchTake = "Fáklya\nelővétele";
        public const string TorchPutAway = "Fáklya\nelrakása";
        public const string TorchDescription = "Fény a város utcáin és a mélységben. A bal kézben viszed.";
        public const string PotionFormat = "Gyógyfőzet\nx{0}";
        public const string HealthFormat = "ÉP   {0}/{1}";
        public const string FocusFormat = "FP   {0}/{1}";
        public const string StatFormat = "{0}   {1}";
        public const string HonorFormat = "Becsület   {0}";
        public const string FoodFormat = "Élelem   {0}/100";
        public const string GoldFormat = "{0} arany";
        public const string SoulsFormat = "{0} lélek";
        public const string LevelFormat = "{0}. SZINT";
        public const string Weapon = "Fegyver";
        public const string Relic = "Ereklye";
        public static readonly string[] ClassWeapons =
        {
            "Lovagbárd és pajzs", "Két sírtőr", "Boszorkánybot", "Buzogány és lámpás", "Csontbot és talizmán", "Reagensüveg és készlet"
        };
        public const string Empty = "Üres";
        public const string BasicAttack = "Csapás";
        public const string BasicBlock = "Védekezés";
        public const string BasicDescription = "Alapfogások: TÁMADÁS / VÉDÉS. Nem kerülnek fókuszba.";
        public const string AbilityFormat = "{0}   {1} FP   {2}/3";
        public const string UpgradeFormat = "Fejlesztés: {0} lélek";
        public const string TierMaximum = "Legmagasabb fokozat";
        public const string LevelCostFormat = "Tulajdonság fejlesztése: {0} lélek";
        public const string RaiseHealth = "Max ÉP +8";
        public const string RaiseFocus = "Max FP +2";
        public const string RaiseAttack = "TÁM +2";
        public const string RaiseDefense = "VÉD +2";
        public const string RaiseMagic = "MÁG +2";
        public const string RaiseSpeed = "GYO +2";
        public const string AbilityGuide = "A kaszt két fogása: Q / R, a vállgombok vagy az érintőgombok. A fejlesztések erre a lélekre érvényesek.";
        public const string CannotAfford = "Nincs elég lelked.";
        public const string Secret = "...";
        public const string SecretClasses = "JÁTSZHATÓ KASZTOK";
        public const string Unlock = "Feloldás";
        public const string Unlocked = "Feloldva";
        public const string DebugApplied = "A tulajdonságokat alkalmaztad és elmentetted.";
        public const string InvalidNumber = "Minden mezőbe egész számot írj.";
        public static readonly string[] DebugLabels =
        {
            "Max ÉP", "ÉP", "Max FP", "FP", "INT", "TÁM", "VÉD", "MÁG", "GYO", "Élelem", "Becsület", "Szint", "Arany", "Lélek"
        };
    }
}
