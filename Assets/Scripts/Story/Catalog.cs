using System.Collections.Generic;
using System.Linq;

namespace AshenRealm.Story
{
    /// <summary>
    /// Classes, abilities and the bestiary, ported from Graveborne's data tables (js/data.js) and rescaled for real-time
    /// first-person combat: turn-based power becomes damage per hit, enemy speed becomes metres per second, and turns
    /// of an effect become seconds. Text is Hungarian and must stay inside the web font's range (see GameText).
    /// </summary>
    public static class Catalog
    {
        // ---- Classes ---------------------------------------------------------------------------------

        public static readonly ClassDefinition[] Classes =
        {
            new ClassDefinition
            {
                Id = CharacterClass.Knight, Name = "Hamvas Lovag", Role = "Vasba öltözött élcsapat",
                Flavor = "Egy halott rend felesküdöttje. Pajzzsal és fogadalommal utasítja rendre a sötétet.",
                Backstory = "A rend hamu, a fogadalmak túlélték. Egyedül jött végig az úton, olyan páncélban, amit már senki nem fényesít, abból kiindulva, hogy a Mélysötét egy seb, és a sebeket az zárja be, aki hajlandó belenyúlni. A város kedveli ezért. A város tizenegyet kedvelt már belőle.",
                Goal = "Megtisztítani a Mélysötétet.",
                Ending = "A seb nem zárult be, de a kezed benne volt, és vissza is jött. A tizenkettedik hamuszürke páncél felért a lépcsőn. Aggie Voss nem húzza át a nevedet.",
                MaxHealth = 100f, MaxFocus = 6, FocusPerSecond = 0.4f, StartHonor = 30,
                Damage = 25f, AttackCooldown = 0.6f, AttackDuration = 0.5f, BlockReduction = 0.85f, MoveMultiplier = 1f,
                Ability1 = new AbilityDefinition { Effect = AbilityEffect.ShieldBash, Name = "Pajzsdöfés", Description = "Kábító taszítás.", Cost = 1, Cooldown = 4f },
                Ability2 = new AbilityDefinition { Effect = AbilityEffect.Rally, Name = "Gyülekező", Description = "Regeneráció és nagyobb sebzés.", Cost = 2, Cooldown = 14f },
            },
            new ClassDefinition
            {
                Id = CharacterClass.Gravethief, Name = "Sírtolvaj", Role = "Árnyék és acél",
                Flavor = "Nem tartozik sem isteneknek, sem királyoknak. Gyors, halálos, és erkölcsileg még megíratlan.",
                Backstory = "Négy évszázadnyi ember ment le a Mélysötétbe mindenével, amije volt, és szinte semmi nem jött vissza. Ezt nem tragédiának tartja. Leltárnak tartja. Nem hazudik az indokairól, amit a város sértőbbnek talál, mintha hazudna.",
                Goal = "Gazdagabban feljönni.",
                Ending = "Négy évszázad leltára a hátadon, és a Mélysötét legalsó széke üres. A város sértve érzi magát. A város fizet, ha eladsz neki valamit.",
                MaxHealth = 80f, MaxFocus = 8, FocusPerSecond = 0.6f, StartHonor = 0,
                Damage = 17f, AttackCooldown = 0.36f, AttackDuration = 0.32f, BlockReduction = 0.6f, MoveMultiplier = 1.12f,
                Ability1 = new AbilityDefinition { Effect = AbilityEffect.Backstab, Name = "Hátbaszúrás", Description = "Kegyetlen döfés. Hátulról háromszoros.", Cost = 3, Cooldown = 5f },
                Ability2 = new AbilityDefinition { Effect = AbilityEffect.PoisonBlade, Name = "Mérgezett Penge", Description = "A pengéd méreggel vonja be a sebeket.", Cost = 2, Cooldown = 12f },
            },
            new ClassDefinition
            {
                Id = CharacterClass.Witch, Name = "Üreges Boszorkány", Role = "Sötét művészetek tudora",
                Flavor = "Olyasmivel üzletel, amit jobb lett volna eltemetve hagyni. Már beszennyezve kezd.",
                Backstory = "Van valami a legmélyebb szinten, amiről a Kórus négy könyvtárat égetett fel, hogy senki ne olvashasson róla - ő pedig mégis olvasott róla. Nem gyógyírt keres, és nem fegyvert. Magát a dolgot keresi, és pontosan senkinek nem mondta el őszintén, mihez kezd vele, ha egyszer a kezében tartja.",
                Goal = "Elérni a legalsó szint elátkozott hatalmát.",
                Ending = "A trón alatt ott volt a dolog, amiért a Kórus négy könyvtárat égetett el. A kezedben tartod. Senkinek nem mondod el, mi az. Soha nem is tervezted.",
                MaxHealth = 70f, MaxFocus = 12, FocusPerSecond = 1f, StartHonor = -20,
                Damage = 22f, AttackCooldown = 0.75f, AttackDuration = 0.45f, BlockReduction = 0.6f, MoveMultiplier = 0.95f, Ranged = true,
                Ability1 = new AbilityDefinition { Effect = AbilityEffect.LifeDrain, Name = "Életszívás", Description = "Elszívod az ellenfél életerejét.", Cost = 6, Cooldown = 6f },
                Ability2 = new AbilityDefinition { Effect = AbilityEffect.Hex, Name = "Rontás", Description = "Megátkozod az ellenfelet; gyengébben üt.", Cost = 3, Cooldown = 8f },
            },
            new ClassDefinition
            {
                Id = CharacterClass.Warden, Name = "Esküőr", Role = "Szent oltalmazó",
                Flavor = "Lámpás a mélyben. A kegyelem fegyver. A legbecsületesebben kezd.",
                Backstory = "A rend megnevezett egy bűnöst és kiadott egy lámpást, ő pedig mindkettőt elfogadta, második kérdés nélkül. A Mélysötét minden szörnyetege a Nekromanta műve; így szól a tanítás. Tiszta magyarázat, és lassan feltűnik neki, hogy a tiszta magyarázatokat utólag szokták megírni. Minél mélyebbre jut, annál kevesebb szörnyeteg néz ki feltámasztottnak.",
                Goal = "Megölni a Nekromantát. Rájönni, hogy rossz az ítélet.",
                Ending = "A Gloamlord nem nekromanta volt, csak egy ember, akit odaültettek. A tanítás hazudott. A lámpás nem. Visszaviszed, és elmondod nekik, mit láttál a fényénél.",
                MaxHealth = 90f, MaxFocus = 9, FocusPerSecond = 0.5f, StartHonor = 40,
                Damage = 22f, AttackCooldown = 0.65f, AttackDuration = 0.5f, BlockReduction = 0.7f, MoveMultiplier = 1f,
                Ability1 = new AbilityDefinition { Effect = AbilityEffect.Smite, Name = "Sújtás", Description = "Szent tűz; ostorozza az élőholtakat.", Cost = 3, Cooldown = 5f },
                Ability2 = new AbilityDefinition { Effect = AbilityEffect.Mend, Name = "Gyógyítás", Description = "Sebeket gyógyítasz.", Cost = 3, Cooldown = 9f },
            },
        };

        public static ClassDefinition ClassOf(CharacterClass id) => Classes[(int)id];

        // ---- Enemies ---------------------------------------------------------------------------------

        static EnemyMove Hit(string name, int weight, float damage) => new EnemyMove { Name = name, Kind = MoveKind.Melee, Weight = weight, Damage = damage };
        static EnemyMove Bolt(string name, int weight, float damage, BoltKind bolt) => new EnemyMove { Name = name, Kind = MoveKind.Bolt, Weight = weight, Damage = damage, Bolt = bolt };
        static EnemyMove Mend(string name, int weight, float heal) => new EnemyMove { Name = name, Kind = MoveKind.Heal, Weight = weight, Heal = heal };
        static EnemyMove Rage(string name, int weight) => new EnemyMove { Name = name, Kind = MoveKind.Buff, Weight = weight };
        static EnemyMove Ward(string name, int weight) => new EnemyMove { Name = name, Kind = MoveKind.Ward, Weight = weight };

        public static readonly EnemyDefinition[] Enemies =
        {
            // ---- Common foes ----
            new EnemyDefinition { Id = "rat", Name = "Pestispatkány", Body = EnemyBody.Beast, Health = 30f, Speed = 3.4f, Scale = 0.7f, GoldMin = 2, GoldMax = 6, AttackInterval = 1.6f,
                Moves = new[] { Hit("Rágcsálás", 3, 7f), Hit("Mocskos Harapás", 1, 5f).Poison(2f, 4f) } },
            new EnemyDefinition { Id = "bat", Name = "Kriptadenevér", Body = EnemyBody.Flyer, Health = 22f, Speed = 4.4f, Scale = 0.55f, GoldMin = 2, GoldMax = 5, AttackInterval = 1.7f,
                Moves = new[] { Hit("Lecsapás", 3, 7f), Hit("Vérkortyolás", 2, 6f).Drain(0.5f), Hit("Vijjogás", 1, 3f).Weaken(4f) } },
            new EnemyDefinition { Id = "skeleton", Name = "Zörgő Csontváz", Body = EnemyBody.Humanoid, Health = 45f, Speed = 2.8f, GoldMin = 4, GoldMax = 9, Undead = true,
                Moves = new[] { Hit("Csontbunkó", 3, 10f), Hit("Rozsdás Suhintás", 2, 8f), Mend("Összeállás", 1, 12f) } },
            new EnemyDefinition { Id = "ooze", Name = "Sírnyálka", Body = EnemyBody.Blob, Health = 50f, Speed = 1.6f, Scale = 1.1f, GoldMin = 3, GoldMax = 7, AttackInterval = 2.4f,
                Moves = new[] { Hit("Elnyelés", 3, 8f), Hit("Maró Érintés", 2, 6f).Poison(3f, 5f), Mend("Újraformálódás", 1, 10f) } },
            new EnemyDefinition { Id = "ghoul", Name = "Sírgúl", Body = EnemyBody.Humanoid, Health = 55f, Speed = 3.2f, GoldMin = 5, GoldMax = 11, Undead = true,
                Moves = new[] { Hit("Felhasítás", 3, 11f), Hit("Rothadó Harapás", 2, 8f).Poison(4f, 5f) } },
            new EnemyDefinition { Id = "wolf", Name = "Rémfarkas", Body = EnemyBody.Beast, Health = 45f, Speed = 4.6f, Scale = 1.05f, GoldMin = 3, GoldMax = 8, AttackInterval = 1.8f,
                Moves = new[] { Hit("Marcangolás", 3, 12f), Hit("Kitörés", 2, 10f).Stun(1f), Rage("Vonyítás", 1) } },
            new EnemyDefinition { Id = "archer", Name = "Csontíjász", Body = EnemyBody.Humanoid, Health = 40f, Speed = 2.6f, GoldMin = 4, GoldMax = 10, Undead = true, PreferredRange = 11f,
                Moves = new[] { Bolt("Kilőtt Vessző", 3, 11f, BoltKind.Arrow), Bolt("Leszegező Lövés", 2, 7f, BoltKind.Arrow).Stun(1f), Bolt("Szakállas Nyíl", 1, 6f, BoltKind.Arrow).Poison(3f, 4f) } },
            new EnemyDefinition { Id = "drownedthrall", Name = "Vízbe Fúlt Szolga", Body = EnemyBody.Humanoid, Health = 58f, Speed = 2.4f, GoldMin = 5, GoldMax = 11, Undead = true,
                Moves = new[] { Hit("Vízzel Telt Ököl", 3, 11f), Hit("Lehúzás", 2, 9f).Stun(1f), Bolt("Sósvíz-köpés", 1, 8f, BoltKind.Brine).Weaken(4f) } },
            new EnemyDefinition { Id = "shade", Name = "Jajgató Árny", Body = EnemyBody.Flyer, Health = 50f, Speed = 3f, GoldMin = 7, GoldMax = 15, PreferredRange = 8f,
                Moves = new[] { Bolt("Dermesztő Érintés", 3, 11f, BoltKind.Shadow).Weaken(4f), Bolt("Lélekszívás", 2, 9f, BoltKind.Shadow).Drain(0.6f), Ward("Elhalványulás", 1) } },
            new EnemyDefinition { Id = "imp", Name = "Hamuimp", Body = EnemyBody.Flyer, Health = 42f, Speed = 3.8f, Scale = 0.75f, GoldMin = 6, GoldMax = 13, PreferredRange = 9f,
                Moves = new[] { Bolt("Parázsnyíl", 3, 11f, BoltKind.Fire), Bolt("Perzselés", 2, 7f, BoltKind.Fire).Poison(3f, 4f), Rage("Kacaj", 1) } },
            new EnemyDefinition { Id = "cultist", Name = "Hamvas Kultista", Body = EnemyBody.Humanoid, Health = 48f, Speed = 2.6f, GoldMin = 6, GoldMax = 14, PreferredRange = 10f,
                Moves = new[] { Bolt("Sötét Nyíl", 3, 12f, BoltKind.Shadow), Bolt("Rontás", 2, 6f, BoltKind.Shadow).Weaken(5f), Mend("Vérrítus", 1, 16f) } },
            new EnemyDefinition { Id = "rotpriest", Name = "Rothadáspap", Body = EnemyBody.Humanoid, Health = 55f, Speed = 2.4f, GoldMin = 7, GoldMax = 15, PreferredRange = 9f,
                Moves = new[] { Bolt("Rothadás Litániája", 3, 10f, BoltKind.Venom).Poison(4f, 5f), Hit("Görbe Bot", 2, 9f), Mend("Ocsmány Áldás", 1, 18f) } },
            new EnemyDefinition { Id = "bonehound", Name = "Csontkopó", Body = EnemyBody.Beast, Health = 62f, Speed = 4.4f, Scale = 1.1f, GoldMin = 7, GoldMax = 14, Undead = true, AttackInterval = 1.9f,
                Moves = new[] { Hit("Roppantás", 3, 14f), Hit("Velőkapás", 2, 11f).Stun(1f), Hit("Halálhörgés", 1, 6f).Weaken(4f) } },

            // ---- Named terrors ----
            new EnemyDefinition { Id = "gravemaw", Name = "Sírtorok, a Dögpatkány", Rank = EnemyRank.Elite, Body = EnemyBody.Beast, Health = 140f, Speed = 3.8f, Scale = 1.5f, GoldMin = 20, GoldMax = 34,
                Moves = new[] { Hit("Dögrágás", 3, 14f), Hit("Pestisharapás", 2, 9f).Poison(5f, 5f), Rage("Őrjöngés", 1) } },
            new EnemyDefinition { Id = "sporetyrant", Name = "A Spóratirannus", Rank = EnemyRank.Elite, Body = EnemyBody.Humanoid, Health = 165f, Speed = 2.6f, Scale = 1.4f, GoldMin = 22, GoldMax = 36,
                Moves = new[] { Hit("Zúzó Ág", 3, 15f), Bolt("Spórarobbanás", 2, 9f, BoltKind.Venom).Poison(6f, 5f), Mend("Újranövés", 1, 24f) } },
            new EnemyDefinition { Id = "undertow", Name = "Alsóáram, az Ár Őrzője", Rank = EnemyRank.Elite, Body = EnemyBody.Flyer, Health = 150f, Speed = 3.2f, Scale = 1.35f, GoldMin = 22, GoldMax = 36, PreferredRange = 7f,
                Moves = new[] { Hit("Fojtó Markolás", 2, 11f).Stun(1.2f), Bolt("Fekete Dagály", 3, 14f, BoltKind.Brine), Bolt("Alsóáram", 2, 10f, BoltKind.Brine).Drain(0.6f) } },
            new EnemyDefinition { Id = "cinderprophet", Name = "A Parázspróféta", Rank = EnemyRank.Elite, Body = EnemyBody.Humanoid, Health = 145f, Speed = 2.8f, Scale = 1.25f, GoldMin = 24, GoldMax = 38, PreferredRange = 9f,
                Moves = new[] { Bolt("Tűzprédikáció", 3, 15f, BoltKind.Fire), Bolt("Elhamvasztás", 2, 9f, BoltKind.Fire).Poison(6f, 5f), Mend("Hamvas Rítus", 1, 22f) } },
            new EnemyDefinition { Id = "marrowcantor", Name = "A Velőkántor", Rank = EnemyRank.Elite, Body = EnemyBody.Humanoid, Health = 175f, Speed = 2.7f, Scale = 1.4f, GoldMin = 24, GoldMax = 38, Undead = true,
                Moves = new[] { Hit("Velőtüske", 3, 14f), Bolt("Gyászdal", 2, 10f, BoltKind.Shadow).Weaken(5f), Ward("Csonthimnusz", 1) } },

            // ---- Legendary Guardians: one bars each stair ----
            new EnemyDefinition { Id = "morr", Name = "Morr, a Vámszedő", Rank = EnemyRank.Guardian, Body = EnemyBody.Humanoid, Health = 340f, Speed = 2.9f, Scale = 1.5f, GoldMin = 40, GoldMax = 60, Undead = true,
                Moves = new[] { Hit("Vámkasza", 3, 17f), Hit("Adós Láncai", 2, 13f).Stun(1.2f), Bolt("A Holtak Érméje", 2, 12f, BoltKind.Shadow).Weaken(5f), Bolt("Sírilleték", 2, 12f, BoltKind.Shadow).Drain(0.6f), Ward("Páncélterem-védelem", 1) },
                Dialogue = new EnemyDialogue
                {
                    Intro = "»Kilencszáz éve őrzöm ezt a lépcsőt, és mind fizet, aki leereszkedik. Gyere közelebb. Hadd lássam, milyen érméből verték a lelkedet.«",
                    Knight = "»Még egy hamura esküdött pajzs. Háromszázat őrzök belőlük odalent. Remek korlátot adnak.«",
                    Gravethief = "»Tolvajléptek a lépcsőmön. Hatvan elcsent vagyont tartok számon - az ujjaid kiegyenlítik a főkönyvet.«",
                    Witch = "»Üres szívű boszorkány. A kölcsönvett erő duplán fizet - ez a kölcsönvett dolgok szabálya.«",
                    Warden = "»Lámpás a mélyben. Rég volt sötét, mikor egy a fajtádból megvilágította a lépcsőmet. A vám ettől még áll.«",
                    Marked = "»Bűzlesz a Kórus tintájától, hajszolt. Az elítéltek előre fizetnek.«",
                    Hallowed = "»Tiszta kezek. Ritka valuta idelent. Majdnem sajnálom a vámot. Majdnem.«",
                    Defeat = "»Kifizetve... hiánytalanul...«",
                } },
            new EnemyDefinition { Id = "mycel", Name = "Mycel, a Türelmes Kert", Rank = EnemyRank.Guardian, Body = EnemyBody.Blob, Health = 390f, Speed = 2f, Scale = 2.4f, GoldMin = 40, GoldMax = 60, AttackInterval = 2.5f,
                Moves = new[] { Hit("Gyökérzúzás", 3, 16f), Hit("Gyökérrengés", 2, 14f).Stun(1f), Bolt("Spóradagály", 2, 10f, BoltKind.Venom).Poison(6f, 5f), Bolt("Fojtó Virágzás", 1, 8f, BoltKind.Venom).Weaken(5f), Mend("Újranövés", 1, 34f) },
                Dialogue = new EnemyDialogue
                {
                    Intro = "»Minden gyökér e lépcső alatt én vagyok. Megérkezésed óta a nyelvemen jársz, kis falat. Most megtaláltad a szájat is.«",
                    Knight = "»A vas rozsdásodik. A vas táplál. Emésztettem már nálad jobb pajzsokat.«",
                    Gravethief = "»Gyors láb, gyors hús.«",
                    Witch = "»A spóráimon éltél, boszorkány. Ettől már félig az enyém vagy. Gyere, vidd a másik felét is.«",
                    Warden = "»A fényed perzseli a kertjeimet. Elültetlek valahol sötétben és csendben.«",
                    Marked = "»A hajszolt édesebben rothad. A félelem erjed.«",
                    Hallowed = "»A kedvesség rosszul komposztálódik. Attól még elültetlek.«",
                    Defeat = "»A kert... emlékszik...«",
                } },
            new EnemyDefinition { Id = "brine", Name = "Brine Apátnő", Rank = EnemyRank.Guardian, Body = EnemyBody.Humanoid, Health = 350f, Speed = 2.8f, Scale = 1.5f, GoldMin = 42, GoldMax = 62, Undead = true, PreferredRange = 8f,
                Moves = new[] { Bolt("Keresztelés", 3, 16f, BoltKind.Brine), Bolt("Vízbe Fúlt Altató", 2, 10f, BoltKind.Brine).Stun(1.2f), Bolt("Alsóáram", 2, 11f, BoltKind.Brine).Drain(0.6f), Hit("Mélységi Hideg", 1, 9f).Weaken(5f), Ward("Kútvédelem", 1) },
                Dialogue = new EnemyDialogue
                {
                    Intro = "»Az alatta lévő lépcső keresztelőkút, zarándok, és minden kút keresztelést követel. Tartsd vissza a lélegzeted. A többit majd én tartom.«",
                    Knight = "»A vért gyönyörűen süllyed. Egyenesen le, mint egy ima.«",
                    Gravethief = "»A víz megtartja, ami elcsúszik. Te sehova nem csúszol, csak lefelé.«",
                    Witch = "»A mély suttogott neked, boszorkány? Én vagyok a száj, ahonnan suttogott.«",
                    Warden = "»Szenteltvíz? Gyermek. Minden víz az enyém.«",
                    Marked = "»A bűn lebeg. Csak lassabbá teszi a fulladást.«",
                    Hallowed = "»Tiszta lélek. A kút már szomjazott egyre.«",
                    Defeat = "»Végre... száraz...«",
                } },
            new EnemyDefinition { Id = "pyraxes", Name = "Pyraxes, a Kemenceszülött", Rank = EnemyRank.Guardian, Body = EnemyBody.Humanoid, Health = 370f, Speed = 3f, Scale = 1.7f, GoldMin = 44, GoldMax = 64, PreferredRange = 7f,
                Moves = new[] { Bolt("Kemencetűz", 3, 17f, BoltKind.Fire), Hit("Máz", 2, 14f).Stun(1f), Bolt("Parázskitörés", 2, 8f, BoltKind.Fire).Volley(3), Rage("Parázsszítás", 1), Ward("Salakvédelem", 1) },
                Dialogue = new EnemyDialogue
                {
                    Intro = "»Ebben a kemencében égettek ki, mielőtt a királyságaitok kihűltek volna. Minden, ami elhalad mellettem, agyagként megy be, és mázként jön ki. Gyere - a kemence nyitva áll.«",
                    Knight = "»Hamulovag. A maradékomat viseled címként.«",
                    Gravethief = "»Fürge kis szikra. A szikrák is az enyémek.«",
                    Witch = "»Te kölcsönzöd a tüzet, boszorkány. Én vagyok a hitelező, és a kamat esedékes.«",
                    Warden = "»A gyertyaistened térdel a kohóm előtt.«",
                    Marked = "»A Kórus az eretnekeit eteti velem. Téged még csak meg sem gyászolnak.«",
                    Hallowed = "»Elégetlen és becsületes. A kemence azt szereti legjobban, ami ellenáll neki.«",
                    Defeat = "»Hideg... milyen... különös...«",
                } },

            // ---- The throne ----
            new EnemyDefinition { Id = "gloamlord", Name = "Gloamlord, a Csont Őrzője", Rank = EnemyRank.Boss, Body = EnemyBody.Humanoid, Health = 720f, Speed = 3.1f, Scale = 2f, GoldMin = 80, GoldMax = 120, Undead = true, AttackInterval = 1.9f,
                Moves = new[]
                {
                    Hit("Sírhasítás", 3, 20f), Bolt("Nekrotikus Hullám", 2, 15f, BoltKind.Shadow).Poison(6f, 5f), Bolt("Lélektépés", 2, 14f, BoltKind.Shadow).Drain(0.6f),
                    Bolt("Velővihar", 2, 8f, BoltKind.Arrow).Volley(3), Hit("Koponyatörő", 1, 16f).Stun(1.4f), Bolt("Bénító Rettegés", 1, 10f, BoltKind.Shadow).Weaken(6f),
                    Mend("Lelkek Lakomája", 1, 50f), Ward("Csontvédelem", 1),
                },
                Dialogue = new EnemyDialogue
                {
                    Intro = "»Ember voltam, mielőtt ez a szék megtanított rá, hogy nem kell annak lennem. Négy évszázada ülök a csontok fölött, és eddig senki nem jutott el idáig azért, hogy leültessen. Állj fel elém.«",
                    Knight = "»A rended a lábamnál hever. Hamu a hamuhoz.«",
                    Gravethief = "»Ez a trón nem eladó. De próbáld meg ellopni.«",
                    Witch = "»Tudom, mit olvastál. Én írtam.«",
                    Warden = "»Nem vagyok a te Nekromantád. Azt ők találták ki. Engem csak idetettek.«",
                    Marked = "»A Kórus tintája rajtad. Nekem is megvan a magamé.«",
                    Hallowed = "»Tiszta kezek a Fehérvelőben. A csont majd megszokja.«",
                    Defeat = "»A csont... nem emlékszik... semmire...«",
                } },
        };

        public static EnemyDefinition Enemy(string id) => Enemies.FirstOrDefault(e => e.Id == id);

        /// <summary>Every player-facing string in this table, for the validator's font check.</summary>
        public static IEnumerable<string> AllStrings()
        {
            foreach (var c in Classes)
            {
                yield return c.Name;
                yield return c.Role;
                yield return c.Flavor;
                yield return c.Backstory;
                yield return c.Goal;
                yield return c.Ending;
                foreach (var a in new[] { c.Ability1, c.Ability2 })
                {
                    yield return a.Name;
                    yield return a.Description;
                }
            }
            foreach (var e in Enemies)
            {
                yield return e.Name;
                foreach (var m in e.Moves) yield return m.Name;
                if (e.Dialogue == null) continue;
                var d = e.Dialogue;
                foreach (string s in new[] { d.Intro, d.Knight, d.Gravethief, d.Witch, d.Warden, d.Marked, d.Hallowed, d.Defeat })
                    yield return s;
            }
        }
    }
}
