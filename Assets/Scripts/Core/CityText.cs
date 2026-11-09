namespace AshenRealm.Core
{
    /// <summary>Hollowgate's working services and the watch's saved contracts.</summary>
    public static class CityText
    {
        public const string WellPrompt = "KÚTVÍZ: 25 ÉP, 3 FP (3 ARANY)";
        public const string BoardPrompt = "ŐRSÉGI MEGBÍZÁS";
        public const string MarketPrompt = "ÚTI CSOMAG: 2 GYÓGYITAL (30 ARANY)";
        public const string ShrinePrompt = "GYERTYA: +8 BECSÜLET (10 ARANY)";
        public const string RestPrompt = "TEA ÉS PIHENŐ: 15 ÉP, ÖSSZES FP (8 ARANY)";
        public const string NoGold = "Nincs elég aranyad ehhez a szolgáltatáshoz.";
        public const string AlreadyUsed = "Erre az útra már igénybe vetted. Egy újabb alászállás után térj vissza.";
        public const string NothingToRestore = "Most nincs szükséged gyógyításra vagy fókuszra.";
        public const string WellUsed = "A hideg kútvíz felfrissít: +25 ÉP és +3 FP.";
        public const string MarketUsed = "A kereskedő két gyógyitalt csomagol az utadra.";
        public const string ShrineUsed = "Gyertyád a névtelen halottakért ég. +8 becsület.";
        public const string RestUsed = "A lámpásfa alatt megpihensz: +15 ÉP és teljes fókusz.";
        public const string ContractsComplete = "Az őrség mindhárom megbízását teljesítetted. Üregkapu emlékezni fog rád.";
        public static string ContractAccepted(int number, int goal, int gold, int souls) =>
            $"Őrségi megbízás {number}/3: győzz le {goal} ellenséget a mélységekben. Jutalom: {gold} arany és {souls} lélek. A táblánál veheted át.";
        public static string ContractProgress(int count, int goal) => $"Őrségi megbízás: {count}/{goal}.";
        public static string ContractReady(int gold, int souls) => $"A megbízás kész. Az őrség táblájánál {gold} arany és {souls} lélek vár.";
        public static string ContractReward(int gold, int souls) => $"Az őrség kifizette a megbízást: +{gold} arany, +{souls} lélek.";
    }
}
