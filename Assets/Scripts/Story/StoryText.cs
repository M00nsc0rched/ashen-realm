using System.Collections.Generic;
using System.Linq;

namespace AshenRealm.Story
{
    /// <summary>
    /// The world's words, ported from Graveborne's Hungarian tables (js/i18n-data*.js, js/city-data.js): the people of
    /// Hollowgate, the honor-shaped encounters and the whispers of the Deep Dark. Speech uses » « because the web font
    /// has no curly quotes; dashes are plain hyphens (see GameText.IsRenderable).
    /// </summary>
    public static class StoryText
    {
        public const string Prologue = "Üregkapu az utolsó város a Mélysötét fölött. Megállt a növekedésben, és még nem egyezett bele, hogy megszűnjön létezni.";

        /// <summary>Zone title cards: the valley, the city, then the five depths of the Deep Dark.</summary>
        public static readonly string[] ZoneTitles =
        {
            "HAMUVÖLGY", "ÜREGKAPU", "A KATAKOMBÁK", "A GOMBAMÉLY", "A SÜLLYEDT KIKÖTŐ", "A PARÁZS-SZAKADÉK", "A FEHÉRVELŐ",
        };

        public static readonly string[] ZoneSubtitles =
        {
            "Az út a város felé.",
            "Az utolsó város a Mélysötét fölött.",
            "1. mélység. A régi sötét - kő, só, és a türelmes holtak.",
            "2. mélység. A spórafény úgy száll, mint a hó. Itt minden türelmesen felfalja a többit.",
            "3. mélység. Mocsárrá lett vízbe fúlt kikötő - korhadt mólók és fekete víz.",
            "4. mélység. A hőség a világ repedéseiből lélegzik fel. A Kórus fanatikusai imádják ezt a helyet.",
            "5. mélység. Rakott csontfalak, imával habarcsolva. Az itteni holtak sosem fejezték be a halált.",
        };

        public static readonly NpcDefinition[] Npcs =
        {
            new NpcDefinition
            {
                Id = "wanderer", Name = "A Sötét Vándor",
                Intro = "Nem iszik és nem megy el, a kocsmárosné pedig már a székért sem kér tőle pénzt. Aki lemegy a lépcsőn, előbb nála írja alá.",
                Knight = "»Még egy a hamuszürkében. Te vagy a tizenkettedik. A negyediket kedveltem - három szintet vitt, ami hárommal több, mint a tizenegyediké. Ülj le.«",
                Gravethief = "»Nem azért jöttél, hogy bezárd a sebet. Azért jöttél, hogy kiforgasd a zsebeit. Jó. Akik hazudnak nekem erről, sekélyebben halnak meg.«",
                Witch = "»Olvastál valamit, amit nem kellett volna, és most meg akarod nézni a saját szemeddel. Négyet láttam lemenni közületek ezért. Egyet sem láttam feljönni.«",
                Warden = "»A rend egy névvel és egy lámpással küldött. A lámpást tartsd meg. A név a negyedik szint táján fog gondot okozni.«",
                Marked = "»A Kórusnál tintával áll a neved. Az nem az én dolgom. Az enyém a lépcsőnél kezdődik.«",
                Hallowed = "»Tiszta kezek. A Mélysötétet nem érdekli, de én észreveszem. Valakinek illik.«",
                Fallback = "»A lépcső ott van, ahol mindig. Ami az alján van, nem mindig az, ami múltkor volt az alján. Ezt vedd komolyan.«",
            },
            new NpcDefinition
            {
                Id = "landlord", Name = "Aggie Voss, a kocsmárosné", Service = NpcService.Tavern,
                Intro = "A pult mögött palatáblát tart, nevekkel. Nagyjából a harmaduk át van húzva. A rendszert nem magyarázza, és senki nem kérdezett rá kétszer.",
                Knight = "»Hamuszürke. Ülj oda, ahol látlak, szívem, és ne mondj beszédet. Az előző beszédet mondott.«",
                Gravethief = "»Tudom, mi vagy. Itt mindenki tudja, mi vagy. Az érméd ugyanolyan színű, mint bárkié, szóval - mi legyen.«",
                Witch = "»A csuklyát le a házamban. Nem érdekel, mi van alatta; az érdekel, hogy lássam.«",
                Warden = "»Lámpáshordozó. Évek óta nem járt itt ilyen. Ülj a tűz mellé; nem számítok fel semmit.«",
                Marked = "»Elfogatóparancs van rajtad, és ital van előtted. Mindkét tényt elbírom.«",
                Hallowed = "»Fizettél annak, aki nem tudott. Egy ekkora ivóban egy perc alatt körbeér a szó.«",
                Fallback = "»Kötél és Lámpás. A kötél a felhúzásra való, a lámpás a megtalálásra. Egyik sem ígéret.«",
            },
            new NpcDefinition
            {
                Id = "quartermaster", Name = "Halloway őrmester", Service = NpcService.Forge,
                Intro = "A mellette álló tábla az egyetlen hivatalos dolog, ami Üregkapuban maradt. A kohó tüze el van fojtva, de neki még van szene.",
                Knight = "»Rendbéli. Akkor el tud olvasni egy hirdetményt anélkül, hogy tartanám. Vigye, amit elbír.«",
                Gravethief = "»Nem érdekel, ki hozza be a fejet. Már nem érdekel. Olvasd a táblát.«",
                Witch = "»Kötelességem ezeket nyilvánosan kifüggeszteni. Nem kötelességem örülni annak, ki olvassa őket.«",
                Warden = "»A felülről harmadik hirdetmény kell magának. Az, amelyre a rendje hat éve csendben nem néz rá.«",
                Marked = "»Maga is rajta van egy hirdetményen. Más városban ez számítana. Itt csak annyit jelent, hogy ismeri a formanyomtatványt.«",
                Fallback = "»Minden hirdetmény azon a táblán valaki, aki nem jött vissza. A vérdíj az, ami a zsoldjukból maradt.«",
            },
            new NpcDefinition
            {
                Id = "gravedigger", Name = "Vas Mabb, a Sírásó", Service = NpcService.Chapel,
                Intro = "A piac túlsó végén dolgozza a földet, ahol a standok elfogynak. A kápolna kulcsát is ő őrzi, mert senki más nem kérte.",
                Knight = "»Hamuszürke. Kilencet temettem el a fajtádból. Kettőt kétszer ástam ki, mert az első gödör nem volt elég mély ahhoz, ami feljött belőle.«",
                Gravethief = "»Az én földemben végzed, mint a többiek, és te leszel az egyetlen, aki őszinte volt afelől, miért jött. Ezért megkapod a jó sarkot.«",
                Witch = "»A te fajtádnak nem ások. Bármit is hordasz, amikor elmész, az fent marad, és megy a tűzbe.«",
                Warden = "»Mondd el a szavakat a névteleneknek, jó? Negyedik sor. Tizenegy éve rosszul mondom, és tudom is.«",
                Fallback = "»Mindenkinek jut sarok. A gödröt is én ások.«",
            },
            new NpcDefinition
            {
                Id = "merchant", Name = "Az Üreges Kereskedő", Service = NpcService.Market,
                Intro = "Minden lépcsőfordulónál ott áll, és soha senki nem látta jönni. Az áruja mindig pont annyiba kerül, amennyid még van.",
                Fallback = "»Friss a készlet. A tulajdonosai már nem tartanak rá igényt.«",
            },
        };

        public static NpcDefinition Npc(string id) => Npcs.FirstOrDefault(n => n.Id == id);

        static EncounterChoice C(string label, string result, int honor = 0, int gold = 0, int cost = 0, int heal = 0, int potions = 0, int focus = 0, int damage = 0) =>
            new EncounterChoice { Label = label, Result = result, Honor = honor, Gold = gold, GoldCost = cost, Heal = heal, Potions = potions, Focus = focus, Damage = damage };

        public static readonly EncounterDefinition[] Encounters =
        {
            new EncounterDefinition
            {
                Id = "well", Name = "Az asszony a kútnál",
                Clear = new EncounterVariant
                {
                    Text = "Egy mohával benőtt kútnál szürkébe öltözött asszony húz vizet. Felnéz - előbb riadtan, aztán megenyhülve. »Vándor. Ilyen mélyen ritka az élő. Sérültnek tűnsz.«",
                    Choices = new[]
                    {
                        C("Elfogadod a segítségét", "Tiszta ronggyal és suttogott szóval köti be a sebeidet. »Az irgalom lámpás idelent. Ne hagyd kialudni.« Megújultnak érzed magad.", honor: 8, heal: 30),
                        C("Megkérdezed, mi vár odalent", "»A Csont Őrzője tartja odalent a Gloamtrónt« - mormolja. »Egykor ember volt. Mint mindannyian.« Három érmét nyom a tenyeredbe.", honor: 3, gold: 12),
                        C("Békén hagyod", "Magad mögött hagyod a kutat. A sötét elnyeli a csepegő víz hangját."),
                    },
                },
                Warped = new EncounterVariant
                {
                    Text = "Egy mohával benőtt kútnál leplekbe burkolt SZILUETT görnyed a fekete víz fölé. Nem mozdul. Minden megmaradt ösztönöd azt üvölti, hogy abban a pillanatban ugrik, amint elfordulsz.",
                    Choices = new[]
                    {
                        C("Lecsapsz elsőként", "A pengéd hamarabb ér oda, mint a szemed. A »sziluett« csak egy rémült asszony volt, aki vizet húzott. A vére sötétre festi a kút peremét. Valami megalvad benned - de az erszénye nehéz.", honor: -14, gold: 22),
                        C("Lassan közelítesz, leengedett pengével", "Ahogy közelebb érsz, az árnyék úgy hámlik le, mint nedves ruha - egy reszkető asszony, vizestömlőt szorít a mellkasához. Az önuralmad megmentette az életét. Megáld.", honor: 12, heal: 20),
                        C("Hátrálsz a sötétbe", "Magad mögött hagyod a kutat. A sötét elnyeli a csepegő víz hangját."),
                    },
                },
            },
            new EncounterDefinition
            {
                Id = "beggar", Name = "A koldus a sötétben",
                Clear = new EncounterVariant
                {
                    Text = "Rongyos alak kuporog a falnál, kinyújtott kézzel. »Kérlek - egy érmét, egy héjat. Olyan rég eltévedtem, hogy elfelejtettem a nap színét.«",
                    Choices = new[]
                    {
                        C("Bőkezűen adsz (10 arany)", "Egy marék érmét nyomsz a kezébe. Sír. »Áldjon meg. Tessék - ezt túl régóta hordozom.« Egy kis fiolát csúsztat a tenyeredbe.", honor: 10, cost: 10, potions: 1),
                        C("Megosztasz egy keveset (4 arany)", "Megosztod azt a keveset, amit nélkülözni tudsz. Bólint, és valami meleget hajt a kezedbe. »Szerencsére, ahol nincs.«", honor: 5, cost: 4, heal: 10),
                        C("Nem veszel róla tudomást", "Ellépsz mellette. A suttogás követ, míg a sötét fel nem falja.", honor: -3),
                    },
                },
                Warped = new EncounterVariant
                {
                    Text = "Rongyos alak válik le a falról, keze feléd markol. »Adj« - hörgi. »Adj. Adj.« Az ujjai túl hosszúnak tűnnek. Koldul - vagy nyúl valamiért?",
                    Choices = new[]
                    {
                        C("Levágod", "A pengéd a könyörgés közepén éri. A rongyok alatt: kiéhezett öregember, érmék hullanak az ernyedt kezéből. A »nyúlás« csak szükség volt. Összeszeded az aranyat, amit talán magától is odaadott volna.", honor: -12, gold: 16),
                        C("Adsz neki egy érmét, és figyelsz (4 arany)", "Megosztod azt a keveset, amit nélkülözni tudsz. Bólint, és valami meleget hajt a kezedbe. »Szerencsére, ahol nincs.«", honor: 5, cost: 4, heal: 10),
                        C("Nem veszel róla tudomást", "Ellépsz mellette. A suttogás követ, míg a sötét fel nem falja.", honor: -3),
                    },
                },
            },
            new EncounterDefinition
            {
                Id = "cage", Name = "A ketrecbe zárt lény",
                Clear = new EncounterVariant
                {
                    Text = "Rozsdás ketrec lóg a mennyezetről. Bent sebesült farkas nyüszít, egyik mancsa leomlott kő alá szorult. A szeme követ téged - félve, nem ellenségesen.",
                    Choices = new[]
                    {
                        C("Kiszabadítod a farkast", "Félrefeszíted a követ. A farkas kisántikál, egyszer körbejár, aztán eltűnik a sötétben. Az elszántságod megkeményedik.", honor: 8, focus: 99),
                        C("Véget vetsz a szenvedésének", "Az acél véget vet neki. Bármi is volt, most csendben van. Kifeszítesz pár fogat - a megfelelő vevőnek ér valamit.", honor: -2, gold: 10),
                        C("Hagyod, ahogy van", "A ketrecében hagyod. A nyüszítése nagyon-nagyon sokáig követ.", honor: -4),
                    },
                },
                Warped = new EncounterVariant
                {
                    Text = "Rozsdás ketrec lóg a mennyezetről. Bent MORGÓ FENEVAD csapkod és habzik, a rácsokat feszíti feléd. Öld meg, mielőtt kiszabadul - biztosan kitépné a torkodat.",
                    Choices = new[]
                    {
                        C("Megölöd a rácsokon át", "Az acél véget vet neki. Bármi is volt, most csendben van. Kifeszítesz pár fogat - a megfelelő vevőnek ér valamit.", honor: -6, gold: 10),
                        C("Közelebbről megnézed, mielőtt lecsapsz", "Leengeded a pengét, és valóban odanézel. Nem szörnyeteg - farkas, félig éhen halva, rettegve. A szégyen lehűti a véred. Kiszabadítod.", honor: 10, focus: 99),
                        C("Hagyod, ahogy van", "A ketrecében hagyod. A nyüszítése nagyon-nagyon sokáig követ.", honor: -2),
                    },
                },
            },
            new EncounterDefinition
            {
                Id = "sporewife", Name = "A spóraasszony",
                Clear = new EncounterVariant
                {
                    Text = "Félig a micéliumfalba nőve egy asszony kalapok és lemezek kertjét gondozza. Spórafény száll a hajából, mint lassú hó. »Ülj le, vándor. A Mély gondoskodik, ha hagyod.«",
                    Choices = new[]
                    {
                        C("Elfogadod az orvosságát", "Lemezekből és árnyékból főz teát, amely régi fán megülő eső ízű. A sebeid összeforrnak, ahogy iszod. »A Mély megtartja azt, aki megtartja őt« - mormolja, és egy fiolát dug a csomagodba.", honor: 2, heal: 35, potions: 1),
                        C("Visszautasítod és továbbmész", "Meghagyod a türelmes kertjének. A spórafény elhalványul mögötted."),
                    },
                },
                Warped = new EncounterVariant
                {
                    Text = "Női alakot viselő gomba-TÖMEG leng a falnál, indák nyúlnak onnan, ahol a hajának kellene lennie. Zümmög. Ami idelent zümmög, az hív valamit.",
                    Choices = new[]
                    {
                        C("Levágod, mielőtt szétterjed", "Szétvágod a zümmögő lényt - és egy asszonyt találsz a virág belsejében, gyógynövényes tarsolyából orvosságok hullanak, amiket utazóknak nevelt. A Mély nagyon elcsendesedik körülötted.", honor: -14, gold: 15, potions: 1),
                        C("Figyeled még egy darabig", "Vársz, leengedett pengével. Az »indák« száradó gyógynövényekkel felfűzött hajjá állnak össze; a zümmögés altatódal. Kinyitja a szemét, rád mosolyog a türelmedért - és meggyógyít érte.", honor: 8, heal: 20),
                        C("Hátrálsz", "Meghagyod a türelmes kertjének. A spórafény elhalványul mögötted."),
                    },
                },
            },
            new EncounterDefinition
            {
                Id = "ferryman", Name = "A révész",
                Clear = new EncounterVariant
                {
                    Text = "Csónak siklik ki a fekete vízből, csuklyás alak tolja rúddal, lámpással az orrán. »Érme az átkelésért, kedvesség a vámért« - mondja, kövön átfolyó víz hangján.",
                    Choices = new[]
                    {
                        C("Megfizeted a vámot (8 arany)", "Nedves bőrtenyérbe ejted az érméket. A révész elárasztott csarnokok mellett tol el, amelyeken sosem tudtál volna átgyalogolni, és kipihenten tesz le egy távoli száraz lépcsőn.", honor: 4, cost: 8, heal: 14),
                        C("Megkérdezed, mi alszik a vízben", "»A város imádkozva fulladt meg« - mondja. »Az imák még mindig odalent úsznak.« Elmondja, hol sekély a víz; a térképtől kitisztul az elméd.", honor: 2, focus: 99),
                        C("Továbbinted", "A csónak fodrozódás nélkül csúszik vissza a sötét vízbe."),
                    },
                },
                Warped = new EncounterVariant
                {
                    Text = "Egy VÍZBE FÚLT LÉNY összekötözött csontokból álló tutajt tol a sekélyesben. Kis pénzesláda csillan a lábánál. Nem vett észre. A víz elnyelne minden hangot.",
                    Choices = new[]
                    {
                        C("Elveszed a pénzesládát", "Leemeled a pénzesládát a tutajról. A »vízbe fúlt lény« megfordul - révész, lehullott csuklyával, barázdált, fáradt arccal. Nem szól semmit. Csak néz rád, és ellöki magát. A láda nehéz mások viteldíjától.", honor: -11, gold: 25),
                        C("Nyíltan megszólítod", "Átkiáltasz a vízen. Az alak kiegyenesedik - csuklyás révész, tutaján a mély ellen fűzött csontamulettekkel. »Ez itt becsületes« - hörgi, és ingyen visz át.", honor: 6, heal: 14),
                        C("Elhagyod a partot", "A csónak fodrozódás nélkül csúszik vissza a sötét vízbe."),
                    },
                },
            },
            new EncounterDefinition
            {
                Id = "shrine", Name = "Az elesettek szentélye",
                Clear = new EncounterVariant
                {
                    Text = "Durva szentély rakott koponyákból, egyetlen pislákoló gyertya. Alá vésve: ADJ, ÉS ADATIK. A levegő régi, éhes erőtől zúg.",
                    Choices = new[]
                    {
                        C("Megtisztítod a szentélyt (10 arany)", "Érmét és lélegzetet költesz arra, hogy minden koponyát megnevezz és nyugalomra helyezz. A gyertya megszilárdul. A zúgás valami békeféleséggé halkul, és felmelegít.", honor: 12, cost: 10, heal: 16),
                        C("Csendben imádkozol", "Letérdelsz, és imádkozol azokért, akik itt estek el. Semmi nem válaszol - de kitisztul az elméd, és a tartalékaid visszatérnek.", honor: 3, focus: 99),
                        C("Meggyalázod ereklyékért", "Feltöröd a koponyákat a bennük rejtett csecsebecsékért. A gyertya kialszik. Gazdagabb vagy, és hidegebb.", honor: -15, gold: 20),
                    },
                },
            },
            new EncounterDefinition
            {
                Id = "forgewidow", Name = "A kovácsözvegy",
                Clear = new EncounterVariant
                {
                    Text = "Egy haldokló kovácstűznél, amely a szakadékfalba ékelődik, koromtól csíkos kovács pengét munkál a parázsban. »A Kórus elvitte a férjemet a háborúiba« - mondja fel sem nézve. »Becsületes munkát még mindig elvállalok.«",
                    Choices = new[]
                    {
                        C("Megrakod a kohót (8 arany)", "Érmével és szénnel eteted a kohóját. Hosszan méreget. »Kedvesség, idelent.« Mesterjegyet üt a felszerelésedbe szerencsére - a régi fajtából, ami működik.", honor: 6, cost: 8, focus: 99),
                        C("Meghagyod a munkájának", "A kalapácsének végigkísér a szakadékon, egyenletesen, mint egy szív."),
                    },
                },
                Warped = new EncounterVariant
                {
                    Text = "Egy KÓRUS-KOVÁCS acélt kalapál a vörös sötétben - nyilván a vadászaidat fegyverzi fel. A kohó fénye bearanyozza a kalapácsát. Egyetlen csapásod százat érne az övéiből.",
                    Choices = new[]
                    {
                        C("Megölöd a kovácsot", "A pengéd csapás közben vet véget a kalapácséneknek. A padján: ekevasak, ajtózsanérok, gyerekjáték-kard. Kórus-acél semmi. Elveszed a jó munkáit, és mész.", honor: -13, gold: 18),
                        C("Előbb figyeled, hogyan dolgozik", "A füstből figyeled. Befejezi - főzőedény, ajtózsanér, játékkard valami odafenti gyereknek. Egyetlen fegyver sincs az állványon. Szégyellve a bizonyosságodat, kilépsz; szó nélkül megfoltozza a páncélodat.", honor: 8, heal: 20),
                        C("Elhaladsz a füstben", "A kalapácsének végigkísér a szakadékon, egyenletesen, mint egy szív."),
                    },
                },
            },
            new EncounterDefinition
            {
                Id = "bonechoir", Name = "A csontkórus",
                Clear = new EncounterVariant
                {
                    Text = "Kilenc csontváz térdel körben, állkapcsuk nyitva egy hallásküszöb alatti himnuszban. A levegő a fogaidban rezeg. Aranyozott miseruhájuk száz éve tartja a színét.",
                    Choices = new[]
                    {
                        C("Letérdelsz és hozzáadod a hangod", "Letérdelsz a körben, és a lélegzeted a himnuszhoz adod. Egyetlen kitartott hang erejéig a Fehérvelő nem sír, hanem katedrális. Könnyebben állsz fel, mint ahogy letérdeltél.", honor: 8, focus: 99),
                        C("Megkérdezed, mit gyászolnak", "A legközelebbi koponya meghajol. Az elmédbe menet költözik: egy város, egy pestis, kilenc kántor, akik énekelve maradtak, míg mindenki más menekült. Mindenkit gyászolnak, akinek nem jutott ének.", honor: 4, heal: 20),
                        C("Meghagyod nekik az énekük", "Töretlenül hagyod a himnuszt. Végigkísér a csontcsarnokokon, mint egy áldás, amely még nem döntötte el magát."),
                    },
                },
                Warped = new EncounterVariant
                {
                    Text = "Kilenc zörgő HOLT sikolt hangtalanul egy fekete oltár körül, aranyozott díszek csillognak a csontjaikon. Az ilyen arany bárhol ugyanannyit ér.",
                    Choices = new[]
                    {
                        C("Szétvered őket az ereklyéikért", "Kilenc énekest törsz szét az aranyfonalukért és aranyozott csatjaikért. A himnusz elhallgat. Nem tudtad, mennyire az az ének tartott vissza valamit a Fehérvelő csendjéből.", honor: -14, gold: 24),
                        C("Hallgatózol, mielőtt lecsapsz", "Leengeded a fegyvered, és hallgatsz. A »sikoltozás« harmóniává áll össze - óvó himnusz, olyan régi, mint a körülötte lévő csontok. Az egyik kántor feléd fordítja a koponyáját, és valahogyan helyesel.", honor: 6, focus: 99),
                        C("Visszavonulsz", "Töretlenül hagyod a himnuszt. Végigkísér a csontcsarnokokon, mint egy áldás, amely még nem döntötte el magát."),
                    },
                },
            },
        };

        public static EncounterDefinition Encounter(string id) => Encounters.FirstOrDefault(e => e.Id == id);

        public static readonly string[] Whispers =
        {
            "Az itteni falakat növesztették, nem építették.",
            "Valami odalent számolja a lépteidet.",
            "Régebb óta vagy idelent, mint hiszed.",
            "A sötét nem gyűlöl téged. Annál rosszabb. Éhes.",
            "Fordulj vissza - mondja egy hang, amely pontosan úgy szól, mint a tiéd.",
            "A kő emlékszik rá, amikor csont volt.",
            "»Friss hús« - sóhajtja valami, messze, vagy nagyon közel.",
            "Az árnyékod most előbb mozdult. Majdnem biztos vagy benne.",
            "Valahol odafent a nap feladta veled kapcsolatban.",
            "Az érme még mindig pörög valahol. Sosem ért földet.",
            "A régi mesék mind figyelmeztetések voltak. Egyre sem hallgatott senki.",
            "A kampók sosem maradnak sokáig üresen.",
            "A véred csak kölcsön van nálad. A mély főkönyvet vezet.",
            "Ne imádkozz megmentésért. Idelent válaszolhat valami.",
            "Idelent mindenből hiányzik egy darab. Pont beleillesz majd.",
            "A kar szerszám. A láb ígéret. A fej döntés.",
        };

        /// <summary>Every player-facing string in this table, for the validator's font check.</summary>
        public static IEnumerable<string> AllStrings()
        {
            yield return Prologue;
            foreach (string s in ZoneTitles.Concat(ZoneSubtitles)) yield return s;
            foreach (string s in Npcs.SelectMany(n => n.Strings())) yield return s;
            foreach (var e in Encounters)
            {
                yield return e.Name;
                foreach (var v in new[] { e.Clear, e.Warped })
                {
                    if (v == null) continue;
                    yield return v.Text;
                    foreach (var c in v.Choices)
                    {
                        yield return c.Label;
                        yield return c.Result;
                    }
                }
            }
            foreach (string s in Whispers) yield return s;
        }
    }
}
