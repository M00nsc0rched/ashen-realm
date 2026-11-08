using System.Collections.Generic;
using AshenRealm.AI;
using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.UI;
using AshenRealm.World;
using UnityEngine;

namespace AshenRealm.Story
{
    /// <summary>
    /// Runs the story around the fighting: choosing a class, conversations and services, encounters, guardian speeches,
    /// the rewards for what falls, the whispers of the deep and the ending. Everything that talks goes through here.
    /// </summary>
    public sealed class StoryDirector : MonoBehaviour, IShopCounter
    {
        /// <summary>Set while a soul has had the chapel's words; a descent clears it, as a stay in the city ends in Graveborne.</summary>
        public const string BlessedFlag = "chapel_blessed";

        const int ForgeBaseCost = 40;
        const int RestCost = 12;
        const int HealCost = 10;
        const int AbsolutionCost = 25;
        const int AbsolutionHonor = 15;
        const int PotionCost = 18;
        const float WhisperMin = 45f, WhisperMax = 90f;
        const int KnightBlessingHonor = 6;
        const int ThiefBlessingHonor = 4;
        const int CoinBackSouls = 6;
        const int CoinBackGold = 40;

        // Graveborne's offerings: gold given, Souls written down.
        static readonly int[,] Offerings = { { 60, 4 }, { 150, 12 }, { 400, 36 } };

        [SerializeField] GameSession session;
        [SerializeField] SoulKeeper keeper;
        [SerializeField] DialoguePanel dialogue;
        [SerializeField] ZoneManager zones;
        [SerializeField] PauseMenu pauseMenu;
        [SerializeField] PortraitLibrary portraits;
        [SerializeField] ShopPanel shop;
        [SerializeField] PlayerClassRig classRig;
        [SerializeField] AshenRealm.Combat.Health playerHealth;
        [SerializeField] AshenRealm.Combat.PlayerAbilities playerAbilities;

        float nextWhisper;

        /// <summary>True once the Gloamlord has fallen; the flag makes it survive a load.</summary>
        public bool Finished => session.HasFlag(DeadFlag("gloamlord"));

        void OnEnable()
        {
            EnemyAI.ConfrontationRequested += OnConfrontation;
            EnemyAI.Slain += OnEnemySlain;
            CursedKnightAI.Slain += OnKnightSlain;
        }

        void OnDisable()
        {
            EnemyAI.ConfrontationRequested -= OnConfrontation;
            EnemyAI.Slain -= OnEnemySlain;
            CursedKnightAI.Slain -= OnKnightSlain;
        }

        void Start()
        {
            zones.Arrived += OnZoneArrived;
            nextWhisper = Time.time + WhisperMin;
            UpdateObjective();
            keeper.Loaded += OnSoulLoaded;
            session.EquipmentChanged += UpdateObjective;
        }

        void Update()
        {
            if (zones.Current == null || !zones.Current.Underground || dialogue.IsOpen) return;
            if (Time.time < nextWhisper) return;
            nextWhisper = Time.time + Random.Range(WhisperMin, WhisperMax);
            GameMessages.Notify(StoryText.Whispers[Random.Range(0, StoryText.Whispers.Length)]);
        }

        // ---- A new soul ------------------------------------------------------------------------------

        /// <summary>A class was taken from the row of cards.</summary>
        public void StartSoul(CharacterClass id)
        {
            if (!keeper.IsClassUnlocked(id)) return;
            session.ChooseClass(id);
            keeper.Begin();
            int testZone = ZoneFromUrl();
            if (testZone > 0)
            {
                zones.Travel(testZone, false);
                return;
            }
            zones.ShowTitle();
            GameMessages.Notify(CurrentObjective());
            UpdateObjective();
        }

        /// <summary>Test link: "?zone=4" starts there, so deep zones can be reviewed in the browser without playing down to them.</summary>
        static int ZoneFromUrl()
        {
            const string key = "zone=";
            string url = Application.absoluteURL;
            int at = url.IndexOf(key, System.StringComparison.Ordinal);
            return at >= 0 && int.TryParse(url.Substring(at + key.Length), out int zone) ? zone : 0;
        }

        // ---- People and services ---------------------------------------------------------------------

        public void TalkTo(NpcDefinition npc)
        {
            keeper.Discover(CodexBook.PersonId(npc.Id));
            OpenConversation(npc);
        }

        void OpenConversation(NpcDefinition npc)
        {
            var options = new List<string>();
            var actions = new List<System.Action>();
            void Add(string label, System.Action action)
            {
                options.Add(label);
                actions.Add(action);
            }

            switch (npc.Service)
            {
                case NpcService.Tavern:
                    Add(string.Format(GameText.RestOption, RestCost), () => Rest(npc));
                    break;
                case NpcService.Forge:
                    Add(GameText.SteelOption, () => OpenShop(ItemSlot.Weapon));
                    Add(string.Format(GameText.ForgeOption, ForgeCost), () => Temper(npc));
                    break;
                case NpcService.Chapel:
                    Add(GameText.RelicsOption, () => OpenShop(ItemSlot.Relic));
                    Add(GameText.OfferingOption, () => Offering(npc));
                    Add(string.Format(GameText.AbsolutionOption, AbsolutionCost, AbsolutionHonor), () => Absolve(npc));
                    Add(string.Format(GameText.HealOption, HealCost), () => Mend(npc));
                    break;
                case NpcService.Market:
                    Add(string.Format(GameText.PotionOption, PotionCost), () => BuyPotion(npc));
                    Add(string.Format(GameText.HealOption, HealCost), () => Mend(npc));
                    break;
            }
            Add(GameText.Leave, null);

            string line = npc.Service == NpcService.Chapel ? Blessing() ?? LineFor(npc) : LineFor(npc);
            dialogue.Show(npc.Name, $"{npc.Intro}\n\n{line}", options.ToArray(), i => actions[i]?.Invoke(), FaceOf(npc));
        }

        /// <summary>
        /// Two classes get something out of walking into the chapel that the others do not: one because the order still means
        /// something to them, one because they are the only person in Hollowgate who thinks they need to be there.
        /// </summary>
        string Blessing()
        {
            bool knight = session.Class.Id == CharacterClass.Knight;
            if (session.HasFlag(BlessedFlag) || (!knight && session.Class.Id != CharacterClass.Gravethief)) return null;
            session.SetFlag(BlessedFlag);
            int gain = knight ? KnightBlessingHonor : ThiefBlessingHonor;
            session.AddHonor(gain);
            return $"{(knight ? StoryText.KnightBlessing : StoryText.ThiefBlessing)}\n\n{string.Format(GameText.HonorRises, gain)}";
        }

        string LineFor(NpcDefinition npc)
        {
            string classLine = session.Class.Id == CharacterClass.Knight ? npc.Knight
                : session.Class.Id == CharacterClass.Gravethief ? npc.Gravethief
                : session.Class.Id == CharacterClass.Witch ? npc.Witch
                : session.Class.Id == CharacterClass.Warden ? npc.Warden : npc.Fallback;
            if (session.Alignment == Alignment.Marked && npc.Marked != null) return npc.Marked;
            if (session.Alignment == Alignment.Hallowed && npc.Hallowed != null) return npc.Hallowed;
            return classLine ?? npc.Fallback;
        }

        /// <summary>How they look at this soul: the three faces follow honor, as the city's moods do in Graveborne.</summary>
        Sprite FaceOf(NpcDefinition npc) => portraits.Face(npc.Id, session.Alignment);

        int ForgeCost => ForgeBaseCost + session.WeaponLevel * 30;

        void Paid(NpcDefinition npc, int cost, System.Action effect, string message)
        {
            if (!session.TrySpend(cost))
            {
                dialogue.Show(npc.Name, GameText.NotEnoughGold, new[] { GameText.Leave }, _ => { }, FaceOf(npc));
                return;
            }
            effect();
            dialogue.Show(npc.Name, message, new[] { GameText.Leave }, _ => { }, FaceOf(npc));
        }

        void Rest(NpcDefinition npc) => Paid(npc, RestCost, () =>
        {
            playerHealth.ResetHealth();
            playerAbilities.AddFocus(99f);
        }, GameText.RestDone);

        void Temper(NpcDefinition npc) => Paid(npc, ForgeCost, session.UpgradeWeapon, GameText.ForgeDone);
        void Absolve(NpcDefinition npc) => Paid(npc, AbsolutionCost, () => session.AddHonor(AbsolutionHonor), GameText.AbsolutionDone);
        void Mend(NpcDefinition npc) => Paid(npc, HealCost, playerHealth.ResetHealth, GameText.HealDone);
        void BuyPotion(NpcDefinition npc) => Paid(npc, PotionCost, () => session.AddPotions(1), GameText.PotionDone);

        // ---- The forge's steel and the chapel's relics -------------------------------------------------

        /// <summary>Opens the counter of the place that sells these goods.</summary>
        public void OpenShop(ItemSlot goods)
        {
            int zone = goods == ItemSlot.Weapon ? ZoneNumbers.Forge : ZoneNumbers.Chapel;
            shop.Open(StoryText.ZoneTitles[zone], Catalog.ItemsOf(goods), this);
        }

        public string Balance => string.Format(MenuText.BalanceFormat, session.Gold, keeper.Souls);

        public bool IsCarried(ItemDefinition item) => session.IsEquipped(item);

        public string PriceOf(ItemDefinition item) =>
            string.Format(item.Slot == ItemSlot.Weapon ? MenuText.PriceGoldFormat : MenuText.PriceSoulsFormat, item.Price);

        /// <summary>Some goods do nothing for some classes: a sword does not sharpen a witch's bolts, and the shop says so.</summary>
        public string EffectOf(ItemDefinition item)
        {
            if (IsCarried(item)) return string.Empty;
            float now = classRig.Damage, then = classRig.DamageWith(item);
            if (item.Slot == ItemSlot.Relic && Mathf.Approximately(now, then)) return string.Empty;
            return string.Format(MenuText.DamageFormat, Mathf.RoundToInt(now), Mathf.RoundToInt(then));
        }

        public string Buy(ItemDefinition item)
        {
            bool paid = item.Slot == ItemSlot.Weapon ? session.TrySpend(item.Price) : keeper.TrySpendSouls(item.Price);
            if (!paid) return item.Slot == ItemSlot.Weapon ? GameText.NotEnoughGold : GameText.NotEnoughSouls;

            ItemDefinition replaced = session.Equip(item);
            string result = string.Format(MenuText.BoughtFormat, item.Name);
            if (replaced != null)
            {
                int coin = Catalog.TradeIn(replaced);
                session.AddGold(coin);
                result = string.Format(MenuText.BoughtTradedFormat, item.Name, replaced.Name, coin);
            }
            keeper.Save();
            return result;
        }

        /// <summary>The Choir takes coin and enters it in the ledger as Souls, and gives a little back for a price.</summary>
        void Offering(NpcDefinition npc)
        {
            var options = new List<string>();
            for (int i = 0; i < Offerings.GetLength(0); i++)
                options.Add(string.Format(GameText.OfferingLine, Offerings[i, 0], Offerings[i, 1]));
            options.Add(string.Format(GameText.CoinBackLine, CoinBackSouls, CoinBackGold));
            options.Add(GameText.Leave);

            dialogue.Show(npc.Name, Balance, options.ToArray(), choice =>
            {
                if (choice < Offerings.GetLength(0))
                {
                    int souls = Offerings[choice, 1];
                    Paid(npc, Offerings[choice, 0], () => keeper.AddSouls(souls), string.Format(GameText.OfferingDone, souls));
                }
                else if (choice == Offerings.GetLength(0))
                {
                    bool paid = keeper.TrySpendSouls(CoinBackSouls);
                    if (paid) session.AddGold(CoinBackGold);
                    dialogue.Show(npc.Name, paid ? GameText.CoinBackDone : GameText.NotEnoughSouls, new[] { GameText.Leave }, _ => { }, FaceOf(npc));
                }
            }, FaceOf(npc));
        }

        // ---- Encounters ------------------------------------------------------------------------------

        public void StartEncounter(EncounterDefinition encounter, EncounterSite site)
        {
            EncounterVariant variant = session.SeesWarped && encounter.Warped != null ? encounter.Warped : encounter.Clear;
            keeper.Discover(CodexBook.EncounterId(encounter.Id, variant == encounter.Warped));
            var labels = new string[variant.Choices.Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = variant.Choices[i].Label;
            dialogue.Show(encounter.Name, variant.Text, labels, i => Resolve(encounter, variant.Choices[i], site));
        }

        void Resolve(EncounterDefinition encounter, EncounterChoice choice, EncounterSite site)
        {
            if (choice.GoldCost > 0 && !session.TrySpend(choice.GoldCost))
            {
                dialogue.Show(encounter.Name, GameText.NotEnoughGold, new[] { GameText.Leave }, _ => { });
                return;
            }

            if (choice.Honor != 0) session.AddHonor(choice.Honor);
            if (choice.Gold != 0) session.AddGold(choice.Gold);
            if (choice.Potions != 0) session.AddPotions(choice.Potions);
            if (choice.Heal > 0) playerHealth.Heal(choice.Heal);
            if (choice.Focus > 0) playerAbilities.AddFocus(choice.Focus);
            site.Resolve();
            dialogue.Show(encounter.Name, choice.Result, new[] { GameText.Continue }, _ => { });
        }

        // ---- Guardians and the throne ----------------------------------------------------------------

        void OnConfrontation(EnemyAI enemy)
        {
            EnemyDialogue speech = enemy.Definition.Dialogue;
            string classLine = speech.ForClass(session.Class.Id);
            string aside = session.Alignment == Alignment.Marked ? speech.Marked : session.Alignment == Alignment.Hallowed ? speech.Hallowed : null;
            string body = aside != null ? $"{speech.Intro}\n\n{classLine}\n\n{aside}" : $"{speech.Intro}\n\n{classLine}";
            dialogue.Show(enemy.Definition.Name, body, new[] { GameText.DrawSteel }, _ => enemy.Engage());
        }

        void OnEnemySlain(EnemyAI enemy)
        {
            EnemyDefinition d = enemy.Definition;
            int gold = Random.Range(d.GoldMin, d.GoldMax + 1);
            session.AddGold(gold);
            session.SetFlag(DeadFlag(d.Id));
            session.SetFlag(enemy.SaveKey);
            keeper.Discover(CodexBook.FoeId(d.Id));
            keeper.GainSouls(d.Rank);

            if (d.Rank == EnemyRank.Boss)
            {
                ShowEnding(d);
            }
            else if (d.Rank == EnemyRank.Guardian)
            {
                GameMessages.Notify($"{d.Dialogue.Defeat} {GameText.StairUnbarred}");
                session.AddPotions(1);
                keeper.Save();
                UpdateObjective();
            }
            else if (d.Rank == EnemyRank.Elite)
            {
                GameMessages.Notify(string.Format(GameText.EliteFell, d.Name, gold));
            }
        }

        public static string DeadFlag(string enemyId) => "dead_" + enemyId;

        void OnKnightSlain()
        {
            session.SetFlag(DeadFlag("knight"));
            keeper.Discover(CodexBook.KnightId);
            keeper.GainSouls(EnemyRank.Guardian);
            session.AddGold(25);
            UpdateObjective();
        }

        void ShowEnding(EnemyDefinition boss)
        {
            string body = $"{boss.Dialogue.Defeat}\n\n{session.Class.Ending}\n\n{GameText.EndingStats(session.Honor, session.Gold)}";
            keeper.Save();
            dialogue.Show(GameText.EndingTitle, body, new[] { GameText.BackToCity }, _ => zones.Travel(ZoneNumbers.City, false));
        }

        // ---- Objective -------------------------------------------------------------------------------

        void OnZoneArrived(Zone zone)
        {
            UpdateObjective();
            if (zone.Indoors) GameMessages.Notify(GameText.WeaponsDown);
        }

        void OnSoulLoaded()
        {
            UpdateObjective();
            GameMessages.Notify(CurrentObjective());
        }

        void UpdateObjective()
        {
            if (pauseMenu == null) return;
            string gear = session.Weapon == null && session.Relic == null ? string.Empty
                : "\n\n" + string.Format(MenuText.GearFormat, session.Weapon?.Name ?? MenuText.GearNothing, session.Relic?.Name ?? MenuText.GearNothing);
            pauseMenu.SetObjective(CurrentObjective() + gear);
        }

        public string CurrentObjective()
        {
            int zone = zones.CurrentIndex;
            if (zone == ZoneNumbers.Valley)
                return session.HasFlag(DeadFlag("knight")) ? GameText.ObjectiveValleyOpen : GameText.Objective;
            if (zone == ZoneNumbers.City) return GameText.ObjectiveCity;
            if (zone == ZoneNumbers.Tavern) return GameText.ObjectiveTavern;
            if (zone == ZoneNumbers.Forge) return GameText.ObjectiveForge;
            if (zone == ZoneNumbers.Chapel) return GameText.ObjectiveChapel;
            if (zone >= ZoneNumbers.FirstHome) return GameText.LeaveHouse;
            if (Finished) return GameText.ObjectiveDone;
            return GameText.ObjectiveDepth;
        }
    }
}
