using System.Collections.Generic;
using AshenRealm.AI;
using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.UI;
using AshenRealm.World;
using UnityEngine;

namespace AshenRealm.Story
{
    /// <summary>
    /// Runs the story around the fighting: choosing a class, conversations and services, encounters, guardian speeches,
    /// the rewards for what falls, the whispers of the deep and the ending. Everything that talks goes through here.
    /// </summary>
    public sealed class StoryDirector : MonoBehaviour
    {
        const int ForgeBaseCost = 40;
        const int RestCost = 12;
        const int HealCost = 10;
        const int AbsolutionCost = 25;
        const int AbsolutionHonor = 15;
        const int PotionCost = 18;
        const float WhisperMin = 45f, WhisperMax = 90f;
        const int ValleyZone = 0;
        const int CityZone = 1;

        [SerializeField] GameSession session;
        [SerializeField] DialoguePanel dialogue;
        [SerializeField] ZoneManager zones;
        [SerializeField] PauseMenu pauseMenu;
        [SerializeField] AshenRealm.Combat.Health playerHealth;
        [SerializeField] AshenRealm.Combat.PlayerAbilities playerAbilities;

        float nextWhisper;

        /// <summary>True once the Gloamlord has fallen.</summary>
        public bool Finished { get; private set; }

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
            ShowClassChoice();
        }

        void Update()
        {
            if (zones.Current == null || !zones.Current.Underground || dialogue.IsOpen) return;
            if (Time.time < nextWhisper) return;
            nextWhisper = Time.time + Random.Range(WhisperMin, WhisperMax);
            GameMessages.Notify(StoryText.Whispers[Random.Range(0, StoryText.Whispers.Length)]);
        }

        // ---- Class choice ----------------------------------------------------------------------------

        void ShowClassChoice()
        {
            var options = new string[Catalog.Classes.Length];
            for (int i = 0; i < options.Length; i++)
                options[i] = $"{Catalog.Classes[i].Name} - {Catalog.Classes[i].Role}";
            dialogue.Show(GameText.ChooseSoul, StoryText.Prologue, options, ShowClassDetails);
        }

        void ShowClassDetails(int index)
        {
            ClassDefinition c = Catalog.Classes[index];
            string body = $"{c.Backstory}\n\n{GameText.GoalLabel}: {c.Goal}\n\n" +
                          $"{c.Ability1.Name}: {c.Ability1.Description}\n{c.Ability2.Name}: {c.Ability2.Description}";
            dialogue.Show(c.Name, body, new[] { GameText.Begin, GameText.Back }, answer =>
            {
                if (answer != 0)
                {
                    ShowClassChoice();
                    return;
                }
                session.ChooseClass(c.Id);
                int testZone = ZoneFromUrl();
                if (testZone > 0)
                {
                    zones.Travel(testZone, false);
                    return;
                }
                zones.ShowTitle();
                GameMessages.Notify(CurrentObjective());
                UpdateObjective();
            });
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
                    Add(string.Format(GameText.ForgeOption, ForgeCost), () => Temper(npc));
                    break;
                case NpcService.Chapel:
                    Add(string.Format(GameText.AbsolutionOption, AbsolutionCost, AbsolutionHonor), () => Absolve(npc));
                    Add(string.Format(GameText.HealOption, HealCost), () => Mend(npc));
                    break;
                case NpcService.Market:
                    Add(string.Format(GameText.PotionOption, PotionCost), () => BuyPotion(npc));
                    Add(string.Format(GameText.HealOption, HealCost), () => Mend(npc));
                    break;
            }
            Add(GameText.Leave, null);

            dialogue.Show(npc.Name, $"{npc.Intro}\n\n{LineFor(npc)}", options.ToArray(), i => actions[i]?.Invoke());
        }

        string LineFor(NpcDefinition npc)
        {
            string classLine = session.Class.Id == CharacterClass.Knight ? npc.Knight
                : session.Class.Id == CharacterClass.Gravethief ? npc.Gravethief
                : session.Class.Id == CharacterClass.Witch ? npc.Witch : npc.Warden;
            if (session.Alignment == Alignment.Marked && npc.Marked != null) return npc.Marked;
            if (session.Alignment == Alignment.Hallowed && npc.Hallowed != null) return npc.Hallowed;
            return classLine ?? npc.Fallback;
        }

        int ForgeCost => ForgeBaseCost + session.WeaponLevel * 30;

        void Paid(NpcDefinition npc, int cost, System.Action effect, string message)
        {
            if (!session.TrySpend(cost))
            {
                dialogue.Show(npc.Name, GameText.NotEnoughGold, new[] { GameText.Leave }, _ => { });
                return;
            }
            effect();
            dialogue.Show(npc.Name, message, new[] { GameText.Leave }, _ => { });
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

        // ---- Encounters ------------------------------------------------------------------------------

        public void StartEncounter(EncounterDefinition encounter, EncounterSite site)
        {
            EncounterVariant variant = session.SeesWarped && encounter.Warped != null ? encounter.Warped : encounter.Clear;
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

            if (d.Rank == EnemyRank.Boss)
            {
                ShowEnding(d);
            }
            else if (d.Rank == EnemyRank.Guardian)
            {
                GameMessages.Notify($"{d.Dialogue.Defeat} {GameText.StairUnbarred}");
                session.AddPotions(1);
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
            session.AddGold(25);
            UpdateObjective();
        }

        void ShowEnding(EnemyDefinition boss)
        {
            Finished = true;
            string body = $"{boss.Dialogue.Defeat}\n\n{session.Class.Ending}\n\n{GameText.EndingStats(session.Honor, session.Gold)}";
            dialogue.Show(GameText.EndingTitle, body, new[] { GameText.BackToCity }, _ => zones.Travel(CityZone, false));
        }

        // ---- Objective -------------------------------------------------------------------------------

        void OnZoneArrived(Zone zone) => UpdateObjective();

        void UpdateObjective()
        {
            if (pauseMenu != null) pauseMenu.SetObjective(CurrentObjective());
        }

        public string CurrentObjective()
        {
            int zone = zones.CurrentIndex;
            if (zone == ValleyZone)
                return session.HasFlag(DeadFlag("knight")) ? GameText.ObjectiveValleyOpen : GameText.Objective;
            if (zone == CityZone) return GameText.ObjectiveCity;
            if (Finished) return GameText.ObjectiveDone;
            return GameText.ObjectiveDepth;
        }
    }
}
