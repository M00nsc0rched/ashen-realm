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
        public const string Objective = "Keresd meg a romos kápolnát északon.\nGyőzd le az Átkozott Lovagot, és szerezd meg, amit őriz.";

        // Touch buttons
        public const string Attack = "TÁMADÁS";
        public const string Block = "VÉDÉS";
        public const string Jump = "UGRÁS";
        public const string Use = "HASZNÁL";
        public const string Run = "FUTÁS";

        public const string DesktopHint =
            "Kattints a nézéshez  ·  WASD: mozgás  ·  Shift: futás  ·  Szóköz: ugrás  ·  Bal egérgomb: támadás  ·  " +
            "Jobb egérgomb: védés  ·  E: használat  ·  Esc: szünet  ·  F1: érintőgombok";

        /// <summary>Compass labels every 45 degrees, starting at north and turning clockwise.</summary>
        public static readonly string[] CompassPoints = { "É", "ÉK", "K", "DK", "D", "DNY", "NY", "ÉNY" };

        /// <summary>Decimal number with a Hungarian decimal comma (WebGL has no culture data).</summary>
        public static string Decimal(float value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>True if the embedded web font can draw the character.</summary>
        public static bool IsRenderable(char c) => c == '\n' || (c >= ' ' && c <= '~') || (c >= ' ' && c <= 'ſ');
    }
}
