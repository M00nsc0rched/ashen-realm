namespace AshenRealm.Core
{
    /// <summary>
    /// Every player-facing string (Hungarian). Scene builders and runtime scripts read from here so the language
    /// lives in one file. The font the WebGL build embeds (LegacyRuntime) covers only U+0020-U+007E and
    /// U+00A0-U+017F: no em dash, ellipsis or minus sign. The validator enforces this.
    /// </summary>
    public static class GameText
    {
        // Interactions (prompts are shown as written: upper case, no runtime case mapping)
        public const string InteractKey = "[E]  ";
        public const string OpenChest = "LÁDA KINYITÁSA";
        public const string ChestReward = "48 aranyat és egy gyógyitalt találtál (+40 életerő).";
        public const string Inspect = "MEGVIZSGÁLÁS";
        public const string InspectBlade = "PENGE MEGVIZSGÁLÁSA";
        public const string BladeLore = "Egy lovag kardja, markolatig a földbe döfve. Markolatgombján megtört nap: a Hamvas Eskü jele.";

        // Enemy
        public const string KnightName = "ÁTKOZOTT LOVAG";
        public const string KnightDefeated = "Az Átkozott Lovag elesett. Esküje végre megtört.";

        // Death
        public const string DeathTitle = "ELESTÉL";
        public const string DeathSubtitle = "A hamu emlékszik. Újra feltámadsz...";

        // Pause menu
        public const string Paused = "SZÜNET";
        public const string Resume = "FOLYTATÁS";
        public const string Fullscreen = "TELJES KÉPERNYŐ";
        public const string ExitFullscreen = "ABLAKOS MÓD";
        public const string LookSpeed = "ÉRZÉKENYSÉG";
        public const string Objective = "Az út Üregkapuba északra vezet, a romos kápolna mögött.\nGyőzd le az Átkozott Lovagot, aki elzárja.";
        public const string ObjectiveValleyOpen = "A kapu szabad. Menj északra, a kápolna mögé, Üregkapuba.";
        public const string ObjectiveCity = "Beszélj a város lakóival, aztán menj le a Kötél és Lámpás lépcsőjén a Mélysötétbe.";
        public const string ObjectiveDepth = "Keresd meg a lépcsőt, győzd le az őrzőjét, és menj tovább lefelé.\nA legmélyén a Gloamlord vár.";
        public const string ObjectiveDone = "A Gloamlord elesett. Térj vissza Üregkapuba.";

        // Touch buttons
        public const string Attack = "TÁMADÁS";
        public const string Block = "VÉDÉS";
        public const string Jump = "UGRÁS";
        public const string Use = "HASZNÁL";
        public const string Run = "FUTÁS";
        public const string Potion = "FŐZET";

        public const string DesktopHint =
            "Kattints a nézéshez  ·  WASD: mozgás  ·  Shift: futás  ·  Szóköz: ugrás  ·  Bal egér: támadás  ·  " +
            "Jobb egér: védés  ·  Q, R: képességek  ·  F: főzet  ·  E: használat  ·  Esc: szünet  ·  F1: érintőgombok";

        // HUD
        public const string HonorLabel = "BECSÜLET";
        public const string GoldLabel = "ARANY";
        public const string PotionLabel = "FŐZET";
        public const string AlignmentMarked = "MEGJELÖLT";
        public const string AlignmentMortal = "HALANDÓ";
        public const string AlignmentHallowed = "MEGSZENTELT";

        // Interactions
        public const string Talk = "BESZÉLGETÉS";
        public const string Approach = "ODALÉPSZ";
        public const string Barred = "ELZÁRVA";
        public const string GateToCity = "ÚT ÜREGKAPUBA";
        public const string GateToValley = "VISSZA A VÖLGYBE";
        public const string StairDown = "LE A LÉPCSŐN";
        public const string StairUp = "FEL A LÉPCSŐN";
        public const string GateBarred = "Az Átkozott Lovag még áll. Az út csak akkor nyílik meg, ha elesett.";
        public const string StairBarred = "A lépcső nem enged, amíg az őrzője él.";
        public const string ChestGold = "{0} aranyat találtál.";

        // Story flow
        public const string ChooseSoul = "VÁLASSZ LELKET";
        public const string GoalLabel = "Cél";
        public const string Begin = "Indulás";
        public const string Back = "Vissza";
        public const string Leave = "Távozás";
        public const string Continue = "Tovább";
        public const string DrawSteel = "Fegyvert ragadsz";
        public const string BackToCity = "Vissza Üregkapuba";
        public const string EndingTitle = "A GLOAMLORD ELESETT";
        public const string StairUnbarred = "A lépcső szabad.";
        public const string EliteFell = "{0} elesett. +{1} arany";
        public const string EnemyActs = "{0}: {1}";

        // Services
        public const string RestOption = "Pihenés a tűznél ({0} arany)";
        public const string ForgeOption = "Fegyver edzése ({0} arany)";
        public const string AbsolutionOption = "Feloldozás rítusa, +{1} becsület ({0} arany)";
        public const string HealOption = "Sebek bekötése ({0} arany)";
        public const string PotionOption = "Gyógyító Főzet ({0} arany)";
        public const string NotEnoughGold = "Nincs elég aranyad.";
        public const string RestDone = "Életerőd és fókuszod helyreállt.";
        public const string ForgeDone = "A fegyvered mélyebbre harap.";
        public const string AbsolutionDone = "Valaki megint kimondja a nevedet.";
        public const string HealDone = "A sebeid összeforrtak.";
        public const string PotionDone = "Egy fiolával nehezebb a csomagod.";

        // Abilities
        public const string NoFocus = "Nincs elég fókuszod.";
        public const string NoTarget = "Nincs célpont.";
        public const string NoPotion = "Nincs több főzeted.";
        public const string FullHealth = "Nem vagy megsebesülve.";
        public const string RallyMessage = "Összeszeded magad.";
        public const string PoisonMessage = "A pengéd méreggel van bevonva.";
        public const string MendMessage = "A sebeid gyógyulnak.";

        public static string EndingStats(int honor, int gold) => $"{HonorLabel}: {honor}   {GoldLabel}: {gold}";

        /// <summary>Compass labels every 45 degrees, starting at north and turning clockwise.</summary>
        public static readonly string[] CompassPoints = { "É", "ÉK", "K", "DK", "D", "DNY", "NY", "ÉNY" };

        /// <summary>Decimal number with a Hungarian decimal comma (WebGL has no culture data).</summary>
        public static string Decimal(float value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>True if the embedded web font can draw the character.</summary>
        public static bool IsRenderable(char c) => c == '\n' || (c >= ' ' && c <= '~') || (c >= ' ' && c <= 'ſ');
    }
}
