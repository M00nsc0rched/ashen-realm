namespace AshenRealm.Core
{
    /// <summary>
    /// Every player-facing string (Hungarian). Scene builders and runtime scripts read from here so the language
    /// lives in one file. The font the WebGL build embeds (LegacyRuntime) covers only U+0020-U+007E and
    /// U+00A0-U+017F: no em dash, ellipsis or minus sign. The validator enforces this.
    /// </summary>
    public static class GameText
    {
        public const string EnterHome = "BELÉPÉS A HÁZBA";
        public const string InspectArrivalTunnel = "A SÖTÉT ALAGÚT";
        public const string ArrivalTunnelLore = "A hegyi út innen vezetett a völgybe. A karaván itt tett le. Odabent csak feketeség látszik; egyelőre nem térhetsz vissza.";
        public const string InspectCaravan = "AZ ÉRKEZŐ KARAVÁN";
        public const string CaravanLore = "Ezzel a karavánnal érkeztél. A hajtó éjszakára itt veri fel a tábort. Üregkapu kapuja északon vár.";
        // Interactions (prompts are shown as written: upper case, no runtime case mapping)
        public const string InteractKey = "[E]  ";
        public const string OpenChest = "LÁDA KINYITÁSA";
        public const string ChestReward = "48 aranyat és egy gyógyitalt találtál (+40 életerő).";
        public const string Inspect = "MEGVIZSGÁLÁS";
        public const string InspectBlade = "PENGE MEGVIZSGÁLÁSA";
        public const string BladeLore = "Egy lovag kardja, markolatig a földbe döfve. Markolatgombján megtört nap: a Hamvas Eskü jele.";

        // Discoveries in the redesigned places.
        public const string SpringLore = "A forrás kövei közé régi útjelzőket falaztak. Valaki minden ősszel friss virágot hoz ide, pedig az ösvény már sehová sem vezet.";
        public const string WellLore = "A kút kávájába neveket karcoltak. Az újabb betűk mélyebbek: azokat vésték ide, akiknek már senki sem tudott sírt ásni.";
        public const string WeaverLore = "A szövőszéken félbemaradt vászon feszül. A szegélybe apró napokat hímeztek, ugyanazt a jelet, amelyet az út menti kardok viselnek.";
        public const string ProvisionerLore = "A polcon minden útravaló két adagra van osztva. A második csomaghoz kötött cédulán csak ennyi áll: annak, aki utánad érkezik.";
        public const string ScribeLore = "A leveleken nincs címzett. Mind ugyanazzal a mondattal kezdődik: ha egyszer újra járható lesz a hágó...";
        public const string TavernTokenLore = "A falra szegezett úti jelvényeket senki sem váltja már vissza. Aggie minden este letörli róluk a port, aztán eggyel több poharat készít ki.";
        public const string ForgePlanLore = "A kohó falán régi páncél javítási rajza függ. Minden repedést kijelöltek rajta; a szív fölötti lyukat tinta fedi.";
        public const string ChapelVotiveLore = "A kis oltárnál a gyertyák alatt üres névtáblák állnak. Itt azokért ég a fény, akiknek még a nevét is elvitte a mélység.";
        public static readonly string[] DepthLandmarkLore =
        {
            "A sírfülkékbe nem rangot, hanem mesterséget véstek. Kovács, molnár, szövő: egy egész város alszik a másik alatt.",
            "A gombák a régi lámpatartók köré nőttek. Hideg fényük ugyanazt az utat követi, amelyen egykor a sírásók jártak.",
            "A csónak oldalába belülről karcoltak rovásokat. Még akkor is számolták az utasokat, amikor már nem volt part, ahol kiköthettek volna.",
            "A kohót tartó láncokat egyenként nevezték el. A legvastagabbra ezt ütötték: hazatérés. Ez az egy szakadt el.",
            "A csontkoronát nem egyetlen koponyából faragták. A trón minden viselője hozzátett egy darabot; az utolsó hely még üres."
        };

        // Enemy
        public const string KnightName = "ÁTKOZOTT LOVAG";
        public const string KnightDefeated = "Az Átkozott Lovag elesett. Esküje végre megtört.";

        // Death
        public const string DeathTitle = "ELESTÉL";
        public const string DeathSubtitle = "A hamu emlékszik. Újra feltámadsz...";

        // Objectives (shown on the pause menu)
        public const string Objective = "Az út Üregkapuba északra vezet, a romos kápolna mögött.\nGyőzd le az Átkozott Lovagot, aki elzárja.";
        public const string ObjectiveValleyOpen = "A kapu szabad. Menj északra, a kápolna mögé, Üregkapuba.";
        public const string ObjectiveForge = "A kovács acélt ad pénzért, és beléd veri, amit az acél elbír.\nAz utca a tér felé vezet vissza.";
        public const string ObjectiveChapel = "A könyvvivő lelket vesz és lelket kér, a korlát mögött pedig ereklyék állnak.\nAz utca a tér felé vezet vissza.";
        public const string ObjectiveCity = "A tértől keletre a Hideg Kohó, nyugatra a Szürke Kápolna, északra a Kötél és Lámpás.\nA Mélysötétbe vezető lépcső a kocsmában van.";
        public const string ObjectiveTavern = "Beszélj a kocsma népével. A lépcső a hátsó ajtó mögött vár,\nés a Mélysötétbe visz.";
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
        public const string EnterTavern = "BE A KOCSMÁBA";
        public const string EnterForge = "BE A KOHÓBA";
        public const string EnterChapel = "BE A KÁPOLNÁBA";
        public const string LeaveHouse = "KI AZ UTCÁRA";
        public const string BrowseRack = "A FEGYVERÁLLVÁNY";
        public const string BrowseRelics = "AZ EREKLYÉK";
        public const string InspectGallows = "AZ AKASZTÓFA";
        public const string GallowsLore = "Két sor stand, semmi rajtuk, és egy akasztófa, amit senki nem bontott le, mert a lebontása döntés lenne, és itt már senki nem hoz döntéseket.";
        public const string WeaponsDown = "A fegyvereidet az ajtónál hagyod.";
        public const string StairUp = "FEL A LÉPCSŐN";
        public const string GateBarred = "Az Átkozott Lovag még áll. Az út csak akkor nyílik meg, ha elesett.";
        public const string StairBarred = "A lépcső nem enged, amíg az őrzője él.";
        public const string ChestGold = "{0} aranyat találtál.";

        // Story flow
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
        public const string SteelOption = "Az acél";
        public const string RelicsOption = "A korlát mögött";
        public const string OfferingOption = "Az adomány";
        public const string OfferingLine = "{0} arany: {1} lélek a könyvbe";
        public const string CoinBackLine = "Pénzt kérni vissza: {0} lélekért {1} arany";
        public const string OfferingDone = "A Kórus elveszi a pénzt, és {0} lelket ír a könyvbe.";
        public const string CoinBackDone = "A korlát kinyílik, és negyven arany jön vissza rajta.";
        public const string HonorRises = "A becsületed {0} ponttal nő.";
        public const string NotEnoughSouls = "Nincs elég lelked.";
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
