using System.Collections;
using System.Collections.Generic;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.UI;
using AshenRealm.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Ashen Realm > Run Automated Playtest: enters Play mode in Prototype_Valley, starts a soul from the main menu and
    /// drives the player through the input hub (the same entry points the touch controls use): walk, sprint, jump, block a
    /// knight attack, defeat the knight, open the chest, die and respawn, go down the story, change the settings, move a
    /// button, then save, load and release the soul. Any logged error or exception fails the run.
    /// State survives the Play-mode domain reload through SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaytestRunner
    {
        const string StateKey = "AshenRealm.Playtest.State";   // 0 idle, 1 entering, 2 running, 3 finished
        const string ResultKey = "AshenRealm.Playtest.Result";
        const string BatchKey = "AshenRealm.Playtest.Batch";
        const string LogPrefix = "[AshenRealm][Playtest]";
        const double TimeoutSeconds = 300.0;

        static IEnumerator routine;
        static float waitUntil;
        static double startedAt;
        static readonly List<string> results = new List<string>();
        static readonly List<string> loggedErrors = new List<string>();
        static int failures;

        /// <summary>Before the first Awake, so the run reads and writes only its own, empty save data.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void UseOwnSaveData()
        {
            if (SessionState.GetInt(StateKey, 0) != 1) return;
            GameSettings.Prefix = "AshenRealm.Playtest.";
            ClearOwnSaveData();
        }

        static void ClearOwnSaveData()
        {
            SaveStore.Clear();
            GameSettings.ResetAll();
            GameSettings.TouchLayoutJson = string.Empty;
            PlayerPrefs.DeleteKey(GameSettings.Prefix + "LookSensitivity");
            GameSettings.Flush();
        }

        static PlaytestRunner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetInt(StateKey, 0) == 3 && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += Report;
        }

        [MenuItem("Ashen Realm/Run Automated Playtest", priority = 22)]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Start(false);
        }

        public static void Start(bool batch)
        {
            SessionState.SetBool(BatchKey, batch);
            SessionState.SetInt(StateKey, 1);
            SessionState.EraseString(ResultKey);
            EditorSceneManager.OpenScene(GeneratedAssets.ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(StateKey, 0) == 1)
            {
                SessionState.SetInt(StateKey, 2);
                results.Clear();
                loggedErrors.Clear();
                failures = 0;
                startedAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                routine = Run();
                waitUntil = 0f;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(StateKey, 0) == 3)
            {
                Report();
            }
        }

        static void OnLog(string message, string stackTrace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !message.StartsWith(LogPrefix))
                loggedErrors.Add(message);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            bool timedOut = EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds;
            if (!timedOut && Time.unscaledTime < waitUntil) return; // unscaled: dialogues freeze game time

            bool more;
            try
            {
                more = !timedOut && routine.MoveNext();
                if (more && routine.Current is float seconds) waitUntil = Time.unscaledTime + seconds;
            }
            catch (System.Exception e)
            {
                Check(false, "Playtest threw: " + e.Message);
                more = false;
            }
            if (timedOut) Check(false, $"Playtest timed out after {TimeoutSeconds}s");
            if (more) return;

            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            foreach (string error in loggedErrors) Check(false, "Logged error during play: " + error);
            SessionState.SetString(ResultKey, $"{failures}\n" + string.Join("\n", results));
            SessionState.SetInt(StateKey, 3);
            EditorApplication.ExitPlaymode();
        }

        static void Report()
        {
            if (SessionState.GetInt(StateKey, 0) != 3) return;
            SessionState.SetInt(StateKey, 0);
            string[] lines = SessionState.GetString(ResultKey, "1\nNo result recorded").Split('\n');
            int failed = int.TryParse(lines[0], out int f) ? f : 1;
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("FAIL")) Debug.LogError($"{LogPrefix} {lines[i]}");
                else Debug.Log($"{LogPrefix} {lines[i]}");
            }
            Debug.Log(failed == 0 ? $"{LogPrefix} PLAYTEST PASSED" : $"{LogPrefix} PLAYTEST FAILED ({failed} failures)");
            if (SessionState.GetBool(BatchKey, false)) EditorApplication.Exit(failed == 0 ? 0 : 3);
        }

        static bool Check(bool condition, string description)
        {
            results.Add((condition ? "PASS  " : "FAIL  ") + description);
            if (!condition) failures++;
            return condition;
        }

        // ---- Scenario ---------------------------------------------------------------------------------

        static IEnumerator Run()
        {
            yield return 1.0f; // let physics settle
            if (!Check(GameSettings.Prefix.Contains("Playtest"), "The playtest runs on its own save data")) yield break;

            var input = Object.FindAnyObjectByType<PlayerInputHub>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var combat = Object.FindAnyObjectByType<PlayerCombat>();
            var lifecycle = Object.FindAnyObjectByType<PlayerLifecycle>();
            var knight = Object.FindAnyObjectByType<CursedKnightAI>();
            var chest = Object.FindAnyObjectByType<TreasureChest>();
            if (!Check(input && controller && combat && lifecycle && knight && chest, "Scene actors found at runtime")) yield break;

            // The game opens on the main menu, over the paused world.
            var menu = Object.FindAnyObjectByType<MainMenu>();
            if (!Check(menu != null && menu.IsOpen, "The game opens on the main menu")) yield break;
            Check(Time.timeScale == 0f && input.Paused, "The world waits behind the main menu");
            Check(menu.SelectedSoul == null, "A first start has no souls");

            // "Új lélek" leads to the row of cards: a card, its story, then time starts. Back goes one step at a time.
            var carousel = Object.FindAnyObjectByType<ClassCarousel>();
            menu.NewSoul();
            Check(carousel != null && carousel.IsOpen && menu.IsOpen, "A new soul begins with the class cards");
            Check(Time.timeScale == 0f && carousel.Selected == 0, "The row opens on its first card, with the world still waiting");
            carousel.Back();
            Check(menu.IsOpen && !carousel.IsOpen, "The class choice can go back to the main menu");
            menu.NewSoul();
            carousel.Previous();
            Check(carousel.Selected == 0, "The row stops at its first card");
            carousel.Next();
            yield return 0.6f;
            Check(carousel.Selected == 1 && carousel.ShownCard == 1, $"An arrow turns the row and the sheet follows (card {carousel.ShownCard})");

            var swipe = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = new Vector2(800f, 300f) };
            Canvas menuCanvas = carousel.GetComponentInParent<Canvas>();
            carousel.OnBeginDrag(swipe);
            swipe.position += new Vector2(-100f, 0f) * menuCanvas.scaleFactor;
            carousel.OnDrag(swipe);
            carousel.OnEndDrag(swipe);
            yield return 0.6f;
            Check(carousel.Selected == 2 && carousel.ShownCard == 2, "A swipe turns the row one card");
            carousel.OnBeginDrag(swipe);
            swipe.position += new Vector2(5000f, 0f);
            carousel.OnDrag(swipe);
            carousel.OnEndDrag(swipe);
            Check(carousel.Selected == 0, "A long swipe stops at the end of the row");

            carousel.Jump(Catalog.Cards.Length - 1);
            carousel.Next();
            Check(carousel.Selected == Catalog.Cards.Length - 1, "The row stops at its last card");
            Check(Catalog.Cards[carousel.Selected].Class == null, "The last card is a class that is not in the game yet");
            carousel.Confirm();
            Check(carousel.IsOpen && !carousel.ShowingStory, "A class that is not in the game cannot be chosen");
            carousel.Jump(System.Array.FindIndex(Catalog.Cards, c => c.Class != null && c.Class.Id == CharacterClass.Knight));
            carousel.Confirm();
            Check(carousel.ShowingStory && !GameSession.Instance.ClassChosen, "A class shows its story before it is chosen");
            carousel.Next();
            Check(carousel.Selected == carousel.ShownCard, "The row stays still under the story");
            carousel.Back();
            Check(carousel.IsOpen && !carousel.ShowingStory, "Back returns from the story to the cards");
            carousel.Confirm();
            carousel.Confirm();
            Check(!carousel.IsOpen && !menu.IsOpen && GameSession.Instance.ClassChosen && GameSession.Instance.Class.Id == CharacterClass.Knight, "The knight is chosen");
            Check(GameSession.Instance.Honor == Catalog.ClassOf(CharacterClass.Knight).StartHonor, "The class sets the starting honor");
            Check(SaveStore.LoadRoster().Souls.Count == 1, "The chosen class joins the roster");
            Check(Time.timeScale == 1f && !input.Paused, "The game runs after the class is chosen");
            yield return 0.3f;

            var player = controller.transform;
            var health = controller.GetComponent<Health>();
            var stamina = controller.GetComponent<Stamina>();
            Vector3 spawn = player.position;

            Check(controller.IsGrounded, $"Player grounded at spawn (y = {spawn.y:F2})");
            Check(knight.CurrentState == CursedKnightAI.State.Idle || knight.CurrentState == CursedKnightAI.State.Patrol,
                $"Knight starts idle/patrolling (state {knight.CurrentState})");

            // Walk forward.
            input.SetTouchMove(Vector2.up);
            yield return 1.5f;
            input.SetTouchMove(Vector2.zero);
            float walked = Vector3.Distance(Flat(spawn), Flat(player.position));
            Check(walked > 3.5f && walked < 8f, $"Walking 1.5 s covers a plausible distance ({walked:F1} m)");
            Check(Mathf.Approximately(stamina.Current, stamina.Max), "Walking does not use stamina");
            yield return 0.4f;

            // Sprint (touch toggle).
            Vector3 beforeSprint = player.position;
            input.SetTouchButton(PlayerButton.Sprint, true);
            input.SetTouchButton(PlayerButton.Sprint, false);
            input.SetTouchMove(Vector2.up);
            yield return 1.5f;
            float sprinted = Vector3.Distance(Flat(beforeSprint), Flat(player.position));
            Check(sprinted > walked * 1.3f, $"Sprinting is faster than walking ({sprinted:F1} m vs {walked:F1} m)");
            Check(stamina.Current < stamina.Max - 10f, $"Sprinting drains stamina ({stamina.Current:F0})");
            input.SetTouchMove(Vector2.zero);
            yield return 0.8f;
            Check(!input.SprintToggled, "Sprint toggle switches off after stopping");

            // Jump.
            float groundY = player.position.y;
            input.SetTouchButton(PlayerButton.Jump, true);
            input.SetTouchButton(PlayerButton.Jump, false);
            float peak = groundY;
            for (int i = 0; i < 20; i++)
            {
                yield return 0.025f;
                peak = Mathf.Max(peak, player.position.y);
            }
            Check(peak > groundY + 0.6f, $"Jump rises above the ground ({peak - groundY:F2} m)");
            yield return 1.2f;
            Check(controller.IsGrounded, "Player lands after jumping");

            // In touch mode a mouse press (a drag to look, or a browser's tap-emulated click) must not swing the sword.
            var mouse = InputSystem.AddDevice<Mouse>("Playtest Mouse");
            var settings = InputSystem.settings;
            var focusBehaviour = settings.editorInputBehaviorInPlayMode;
            var backgroundBehaviour = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var probe = new InputAction("Probe", InputActionType.Button, "<Mouse>/leftButton");
            int probePresses = 0;
            probe.performed += _ => probePresses++;
            probe.Enable();
            bool touchModeBefore = input.TouchMode;
            input.SetTouchMode(true);
            int swings = 0;
            for (int i = 0; i < 3; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40f, 0f) }.WithButton(MouseButton.Left));
                for (float t = 0f; t < 0.3f; t += 0.05f)
                {
                    yield return 0.05f;
                    if (combat.IsAttacking) swings++;
                }
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 0.1f;
            }
            Check(probePresses >= 3, $"Simulated mouse presses reach Play mode ({probePresses} seen)");
            Check(swings == 0 && !input.AttackHeld, $"Mouse presses in touch mode do not attack ({swings} swing frames)");
            input.SetTouchMode(touchModeBefore);
            probe.Dispose();
            InputSystem.RemoveDevice(mouse);
            settings.editorInputBehaviorInPlayMode = focusBehaviour;
            settings.backgroundBehavior = backgroundBehaviour;

            // Block a Cursed Knight attack.
            stamina.Refill();
            int blocks = 0;
            combat.Blocked += _ => blocks++;
            Vector3 front = knight.transform.position + knight.transform.forward * 1.9f + Vector3.up * 0.1f;
            float yawToKnight = Quaternion.LookRotation(Flat(knight.transform.position - front)).eulerAngles.y;
            controller.Teleport(front, yawToKnight);
            input.SetTouchButton(PlayerButton.Block, true);
            float healthBeforeBlock = health.Current;
            for (float t = 0f; t < 6f && blocks == 0; t += 0.1f) yield return 0.1f;
            input.SetTouchButton(PlayerButton.Block, false);
            Check(knight.IsEngaged || knight.CurrentState == CursedKnightAI.State.Attack, $"Knight engages the player (state {knight.CurrentState})");
            Check(blocks > 0, $"Shield blocks a knight attack ({blocks} blocked)");
            Check(health.Current > healthBeforeBlock - 8f, $"Blocked attack only chips health ({healthBeforeBlock:F0} → {health.Current:F0})");

            // Defeat the knight.
            stamina.Refill();
            int hits = 0;
            combat.HitLanded += (_, __) => hits++;
            input.SetTouchButton(PlayerButton.Attack, true);
            for (float t = 0f; t < 12f && knight.Health.IsAlive && health.IsAlive; t += 0.1f)
            {
                // Keep facing the knight while it shuffles (knockback / re-approach).
                Vector3 to = Flat(knight.transform.position - player.position);
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(player.forward, to) > 10f)
                    controller.Teleport(player.position, Quaternion.LookRotation(to).eulerAngles.y);
                yield return 0.1f;
            }
            input.SetTouchButton(PlayerButton.Attack, false);
            Check(hits >= 4, $"Sword hits land on the knight ({hits} hits)");
            Check(!knight.Health.IsAlive && knight.CurrentState == CursedKnightAI.State.Dead, $"Knight defeated (health {knight.Health.Current:F0}, state {knight.CurrentState})");
            Check(health.IsAlive, $"Player survives the duel ({health.Current:F0} HP)");
            Check(health.Current < health.Max || blocks > 0, "Player took damage or blocked during combat");

            // Open the chest.
            string notification = null;
            System.Action<string> onNotify = m => notification = m;
            GameMessages.Notification += onNotify;
            Vector3 chestFront = chest.transform.position + chest.transform.rotation * new Vector3(0f, 0.1f, -1.4f);
            controller.Teleport(chestFront, chest.transform.eulerAngles.y, 45f);
            yield return 0.3f;
            var interactor = controller.GetComponent<PlayerInteractor>();
            Check(interactor.CurrentPrompt == GameText.OpenChest, $"Aiming at the chest shows '{GameText.OpenChest}' (prompt '{interactor.CurrentPrompt}')");
            float healthBeforeChest = health.Current;
            input.SetTouchButton(PlayerButton.Interact, true);
            input.SetTouchButton(PlayerButton.Interact, false);
            yield return 1.0f;
            GameMessages.Notification -= onNotify;
            Check(chest.IsOpen, "Chest opens on interact");
            Check(!string.IsNullOrEmpty(notification), $"Reward notification shown ('{notification}')");
            Check(health.Current >= healthBeforeChest, "Chest reward does not reduce health");
            Check(string.IsNullOrEmpty(interactor.CurrentPrompt), "Opened chest no longer offers interaction");

            // Death and respawn.
            health.ApplyDamage(new DamageInfo(1000f, player.position, Vector3.forward, null));
            yield return 0.2f;
            Check(lifecycle.IsDead, "Lethal damage kills the player");
            yield return 4.5f;
            Check(!lifecycle.IsDead && health.IsAlive && Mathf.Approximately(health.Current, health.Max), "Player respawns with full health");
            Check(Vector3.Distance(player.position, spawn) < 1.5f, $"Player respawns at the spawn point ({Vector3.Distance(player.position, spawn):F1} m away)");

            // The story from the gate down.
            var session = Object.FindAnyObjectByType<GameSession>();
            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var dialogue = Object.FindAnyObjectByType<DialoguePanel>();
            var director = Object.FindAnyObjectByType<StoryDirector>();
            var abilities = controller.GetComponent<PlayerAbilities>();
            var status = controller.GetComponent<StatusEffects>();
            if (!Check(session && zones && dialogue && director && abilities && status, "Story services found at runtime")) yield break;

            Check(session.HasFlag(StoryDirector.DeadFlag("knight")), "Slaying the knight is recorded");
            Check(session.Gold >= 48 + 25, $"Knight and chest paid out (gold {session.Gold})");

            var gate = Object.FindAnyObjectByType<ZonePortal>();
            Check(gate != null && gate.TargetZone == 1, "The valley has a gate to Hollowgate");
            gate.Interact(controller.gameObject);
            yield return 1.6f;
            Check(zones.CurrentIndex == 1 && !zones.Travelling, $"The gate leads to Hollowgate (zone {zones.CurrentIndex})");
            Check(RenderSettings.sun != null && RenderSettings.sun.enabled, "The sun shines on the city");

            NpcActor npc = System.Array.Find(Object.FindObjectsByType<NpcActor>(), n => n.NpcId == "merchant");
            Check(npc != null, "Hollowgate has its merchant");
            int potionsBefore = session.Potions;
            int goldBefore = session.Gold;
            npc.Interact(controller.gameObject);
            Check(dialogue.IsOpen && Time.timeScale == 0f, "Talking opens the dialogue and freezes time");
            Check(dialogue.Face == null, "Someone with no painting talks without a face");
            dialogue.Choose(0);
            Check(session.Potions == potionsBefore + 1 && session.Gold < goldBefore, $"The merchant sells a potion ({session.Potions} potions, {session.Gold} gold)");
            dialogue.Choose(0);
            Check(!dialogue.IsOpen && Time.timeScale == 1f, "Closing the dialogue resumes time");

            Light torch = System.Array.Find(controller.GetComponentsInChildren<Light>(true), l => l.name == "Torch");
            ZonePortal Door(int zone) => System.Array.Find(Object.FindObjectsByType<ZonePortal>(), p => p.TargetZone == zone);
            Transform swordPivot = player.Find("Camera Pivot/Main Camera/Weapon Rig/Sword Pivot (Right Hand)");
            CanvasGroup attackGroup = System.Array.Find(Object.FindObjectsByType<TouchButton>(), b => b.Button == PlayerButton.Attack).GetComponent<CanvasGroup>();

            // The Cold Forge: a house of its own, with the goods on the wall and the smith to sell them.
            var keeperOfSouls = Object.FindAnyObjectByType<SoulKeeper>();
            var shop = Object.FindAnyObjectByType<ShopPanel>();
            if (!Check(shop != null, "The shop panel exists")) yield break;
            ZonePortal forgeDoor = Door(ZoneNumbers.Forge);
            if (!Check(forgeDoor != null && forgeDoor.InteractionPrompt == GameText.EnterForge, "The forge has a door on the street")) yield break;
            Check(combat.Armed && swordPivot.gameObject.activeSelf && attackGroup.alpha > 0f, "Outdoors the weapons are in hand");
            forgeDoor.Interact(controller.gameObject);
            yield return 1.8f;
            Check(zones.CurrentIndex == ZoneNumbers.Forge && zones.Current.Indoors, $"The door leads into the forge (zone {zones.CurrentIndex})");
            Check(!combat.Armed, "The weapons stay at the forge's door");
            Check(!swordPivot.gameObject.activeSelf, "Nothing is held under the roof");
            Check(attackGroup.alpha == 0f && !attackGroup.blocksRaycasts, "The fighting buttons leave with the weapons");
            input.SetTouchButton(PlayerButton.Attack, true);
            input.SetTouchButton(PlayerButton.Attack, false);
            yield return 0.1f;
            Check(!combat.IsAttacking, "Nothing swings indoors");
            Check(!abilities.TryUse(0), "Abilities stay at the door too");

            // Not only the objects themselves: what a player can really aim at from where a player can really stand.
            Vector3 house = zones.Current.transform.position;
            controller.Teleport(house + new Vector3(4.6f, 0.05f, 0.4f), 90f);
            Physics.SyncTransforms();
            yield return 0.2f;
            Check(interactor.Current is ShopFixture, $"The rack can be aimed at ({interactor.CurrentPrompt})");
            controller.Teleport(house + new Vector3(-2.4f, 0.05f, -0.4f), 0f);
            Physics.SyncTransforms();
            yield return 0.2f;
            Check(interactor.Current is NpcActor actorHere && actorHere.NpcId == "smith", $"The smith can be aimed at ({interactor.CurrentPrompt})");

            ShopFixture rack = Object.FindAnyObjectByType<ShopFixture>();
            Check(rack != null && rack.Goods == ItemSlot.Weapon && rack.InteractionPrompt == GameText.BrowseRack, "The forge has its rack");
            rack.Interact(controller.gameObject);
            Check(shop.IsOpen && shop.StockCount == 4 && Time.timeScale == 0f, "The rack opens the counter and freezes time");
            shop.Close();
            Check(!shop.IsOpen && Time.timeScale == 1f, "The counter closes");

            NpcActor smith = System.Array.Find(Object.FindObjectsByType<NpcActor>(), n => n.NpcId == "smith");
            if (!Check(smith != null, "The smith stands at the anvil")) yield break;
            session.AddGold(300);
            smith.Interact(controller.gameObject);
            Check(dialogue.IsOpen && dialogue.Face != null && dialogue.Face.name == "smith_0", "The smith talks with his painted face");
            dialogue.Choose(0);
            Check(shop.IsOpen && shop.Chosen.Id == "rusted_blade", "The smith shows his steel");
            shop.Select(1);
            ItemDefinition sword = shop.Chosen;
            float bareDamage = combat.Damage;
            goldBefore = session.Gold;
            shop.Buy();
            Check(session.Weapon == sword && session.Gold == goldBefore - sword.Price && sword.Price == 44, $"The Iron Sword costs what Graveborne asks ({session.Gold})");
            Check(Mathf.Approximately(combat.Damage, bareDamage + 9f), $"The sword bites harder ({bareDamage} to {combat.Damage})");
            Check(!shop.CanBuy, "What is already in the hand cannot be bought again");
            Check(swordPivot.Find("Bought Iron Sword").gameObject.activeSelf && !swordPivot.Find("Sword").gameObject.activeSelf, "The bought sword replaces the class's");
            shop.Select(2);
            goldBefore = session.Gold;
            shop.Buy();
            Check(session.Weapon.Id == "grave_dagger" && session.Gold == goldBefore - 39 + 12, $"The old sword is traded in ({session.Gold})");
            Check(shop.Notice.Contains(sword.Name), $"The counter says what happened ({shop.Notice})");
            shop.Close();

            ZonePortal forgeOut = Door(ZoneNumbers.City);
            if (!Check(forgeOut != null && forgeOut.InteractionPrompt == GameText.LeaveHouse, "The forge has a door back out")) yield break;
            controller.Teleport(house + new Vector3(0f, 0.05f, -4.2f), 180f);
            Physics.SyncTransforms();
            yield return 0.2f;
            Check(ReferenceEquals(interactor.Current, forgeOut), $"The door out can be aimed at ({interactor.CurrentPrompt})");
            forgeOut.Interact(controller.gameObject);
            yield return 1.8f;
            Check(zones.CurrentIndex == ZoneNumbers.City && combat.Armed, "The weapons are taken up again on the street");
            Check(swordPivot.gameObject.activeSelf && attackGroup.alpha > 0f, "The weapons and the buttons come back");
            Check(Vector3.Distance(player.position, forgeDoor.transform.position) < 5f, $"The street door lets out by the forge ({player.position})");
            controller.Teleport(player.position, 90f);
            Physics.SyncTransforms();
            yield return 0.2f;
            Check(ReferenceEquals(interactor.Current, forgeDoor), $"The forge door can be aimed at ({interactor.CurrentPrompt})");

            // The market's people and the gallows, from the road.
            house = zones.Current.transform.position;
            foreach (var (who, from, yaw) in new[] { ("widow", new Vector3(4.4f, 0.05f, -49.6f), 90f), ("gravedigger", new Vector3(-4.2f, 0.05f, -51.4f), 270f), ("crier", new Vector3(-2f, 0.05f, -44f), 270f) })
            {
                controller.Teleport(house + from, yaw);
                Physics.SyncTransforms();
                yield return 0.2f;
                Check(interactor.Current is NpcActor person && person.NpcId == who, $"{who} can be aimed at from the road ({interactor.CurrentPrompt})");
            }
            controller.Teleport(house + new Vector3(3.5f, 0.05f, -8.6f), 0f);
            Physics.SyncTransforms();
            yield return 0.2f;
            Check(interactor.Current is LoreInspectable, $"The gallows can be inspected ({interactor.CurrentPrompt})");

            // The Grey Chapel: the book, the rail and what is behind it.
            ZonePortal chapelDoor = Door(ZoneNumbers.Chapel);
            if (!Check(chapelDoor != null && chapelDoor.InteractionPrompt == GameText.EnterChapel, "The chapel has a door on the street")) yield break;
            chapelDoor.Interact(controller.gameObject);
            yield return 1.8f;
            Check(zones.CurrentIndex == ZoneNumbers.Chapel && !combat.Armed, "The chapel takes the weapons too");
            Check(Object.FindObjectsByType<ShopFixture>().Length == 2, "The relics can be reached on both sides of the book");
            house = zones.Current.transform.position;
            controller.Teleport(house + new Vector3(0f, 0.45f, 3f), 0f);
            Physics.SyncTransforms();
            yield return 0.2f;
            Check(interactor.Current is NpcActor actorThere && actorThere.NpcId == "choir", $"The book-keeper can be aimed at across the rail ({interactor.CurrentPrompt})");
            foreach (float across in new[] { -3.2f, 3.3f })
            {
                controller.Teleport(house + new Vector3(across, 0.45f, 3f), 0f);
                Physics.SyncTransforms();
                yield return 0.2f;
                Check(interactor.Current is ShopFixture, $"The relics can be aimed at across the rail ({interactor.CurrentPrompt})");
            }
            Vector3 walkInto = house + new Vector3(0f, 0.45f, 3f);
            controller.Teleport(walkInto, 0f);
            Physics.SyncTransforms();
            input.SetTouchMove(Vector2.up);
            yield return 0.6f;
            input.SetTouchMove(Vector2.zero);
            Check(player.position.z < house.z + 3.5f, $"The rail stops the feet ({player.position.z - house.z:F2})");
            NpcActor scribe = System.Array.Find(Object.FindObjectsByType<NpcActor>(), n => n.NpcId == "choir");
            if (!Check(scribe != null, "The Choir keeps the ledger")) yield break;
            int honorBefore = session.Honor;
            scribe.Interact(controller.gameObject);
            Check(session.Honor == honorBefore + 6 && session.HasFlag(StoryDirector.BlessedFlag), $"The chapel blesses a knight once ({session.Honor})");
            Check(dialogue.Face != null && dialogue.Face.name == "choir_0", "The book-keeper has a picture of his own");
            dialogue.Choose(1);
            int soulsBefore = keeperOfSouls.Souls;
            goldBefore = session.Gold;
            dialogue.Choose(0);
            Check(keeperOfSouls.Souls == soulsBefore + 4 && session.Gold == goldBefore - 60, "An offering turns coin into Souls");
            dialogue.Choose(0);
            scribe.Interact(controller.gameObject);
            Check(session.Honor == honorBefore + 6, "The blessing does not repeat");
            dialogue.Choose(0);
            Check(shop.IsOpen && shop.StockCount == 3, "Three relics stand behind the rail");
            shop.Select(0);
            string noSouls = keeperOfSouls.TrySpendSouls(100000) ? "" : GameText.NotEnoughSouls;
            Check(noSouls == GameText.NotEnoughSouls, "Souls cannot go below nothing");
            keeperOfSouls.AddSouls(100);
            soulsBefore = keeperOfSouls.Souls;
            shop.Buy();
            Check(session.Relic != null && session.Relic.Id == "bloodstone" && keeperOfSouls.Souls == soulsBefore - 32, $"The Bloodstone is paid for in Souls ({keeperOfSouls.Souls})");
            float focusBefore = abilities.MaxFocus;
            shop.Select(1);
            shop.Buy();
            Check(Mathf.Approximately(abilities.MaxFocus, focusBefore + 3f), $"The Witch Eye widens the focus ({abilities.MaxFocus})");
            shop.Select(2);
            float healthBefore = health.Max;
            shop.Buy();
            Check(health.Max > healthBefore + 12f && session.Relic.Id == "grave_idol", $"The Grave Idol goes round the neck ({health.Max})");
            shop.Close();

            Door(ZoneNumbers.City).Interact(controller.gameObject);
            yield return 1.8f;
            Check(zones.CurrentIndex == ZoneNumbers.City && Vector3.Distance(player.position, chapelDoor.transform.position) < 5f, "The chapel lets out on its own door");
            session.ChooseClass(CharacterClass.Knight);
            session.Equip(Catalog.Item("bloodstone"));

            // The tavern stands between the square and the stair.
            ZonePortal tavernDoor = Door(ZoneNumbers.Tavern);
            if (!Check(tavernDoor != null && tavernDoor.InteractionPrompt == GameText.EnterTavern, "The tavern has a door on the square")) yield break;
            tavernDoor.Interact(controller.gameObject);
            yield return 1.8f;
            Check(zones.CurrentIndex == ZoneNumbers.Tavern && zones.Current.Indoors && !zones.Current.Underground, $"The door leads into the tavern (zone {zones.CurrentIndex})");
            Check(RenderSettings.sun != null && !RenderSettings.sun.enabled, "No sun under the tavern's roof");
            Check(torch != null && !torch.enabled, "No torch by the tavern's fire");
            Check(!combat.Armed, "The weapons stay at the tavern's door");
            NpcActor[] regulars = Object.FindObjectsByType<NpcActor>();
            Check(regulars.Length == 3, $"Three people sit in the tavern ({regulars.Length})");

            NpcActor aggie = System.Array.Find(regulars, n => n.NpcId == "landlord");
            if (!Check(aggie != null, "Aggie Voss keeps the bar")) yield break;
            aggie.Interact(controller.gameObject);
            string moodFace = session.Alignment == Alignment.Hallowed ? "landlord_1" : session.Alignment == Alignment.Marked ? "landlord_2" : "landlord_0";
            Check(dialogue.IsOpen && dialogue.Face != null && dialogue.Face.name == moodFace, $"Aggie talks with the face honor earns ({(dialogue.Face != null ? dialogue.Face.name : "none")})");
            dialogue.Choose(1);
            Check(!dialogue.IsOpen, "The conversation can be left");

            ZonePortal streetDoor = Door(ZoneNumbers.City);
            ZonePortal stair = Door(ZoneNumbers.FirstDepth);
            if (!Check(streetDoor != null && stair != null, "The tavern has a street door and the stair")) yield break;
            Check(stair.InteractionPrompt == GameText.StairDown, "The stair is open to anyone");
            stair.Interact(controller.gameObject);
            yield return 1.6f;
            Check(zones.CurrentIndex == ZoneNumbers.FirstDepth && zones.Current.Underground, $"The stair leads to the first depth (zone {zones.CurrentIndex})");
            Check(Door(ZoneNumbers.Tavern) != null, "The stair up leads back into the tavern");
            Check(RenderSettings.sun != null && !RenderSettings.sun.enabled, "No sun underground");
            Check(torch.enabled, "The torch burns underground");
            Check(combat.Armed, "The weapons are back in hand on the stair");

            // Every foe in the depth is alive and awake; fight one with the sword.
            EnemyAI[] foes = Object.FindObjectsByType<EnemyAI>();
            Check(foes.Length >= 8, $"The depth is inhabited ({foes.Length} foes)");
            EnemyAI foe = System.Array.Find(foes, e => e.Definition != null && e.Definition.Rank == EnemyRank.Common);
            if (!Check(foe != null, "A common foe exists")) yield break;
            status.Clear();
            Vector3 foeFront = foe.transform.position - foe.transform.forward * 2f + Vector3.up * 0.1f;
            controller.Teleport(foeFront, foe.transform.eulerAngles.y);
            Physics.SyncTransforms();
            goldBefore = session.Gold;
            for (float t = 0f; t < 10f && foe.Health.IsAlive && health.IsAlive; t += 0.1f)
            {
                Vector3 to = Flat(foe.transform.position - player.position);
                if (to.sqrMagnitude > 0.01f) controller.Teleport(foe.transform.position - to.normalized * 1.8f, Quaternion.LookRotation(to).eulerAngles.y);
                input.SetTouchButton(PlayerButton.Attack, true);
                input.SetTouchButton(PlayerButton.Attack, false);
                yield return 0.1f;
            }
            Check(!foe.Health.IsAlive, $"{foe.DisplayName} falls to the sword (health {foe.Health.Current:F0})");
            Check(session.Gold > goldBefore, "Slain foes drop gold");
            Check(abilities.Focus > 0f, "Focus is available");

            // Class ability: spends focus and hits what is in front.
            EnemyAI next = System.Array.Find(foes, e => e != foe && e.Health.IsAlive && e.Definition.Rank == EnemyRank.Common);
            if (next != null)
            {
                controller.Teleport(next.transform.position - next.transform.forward * 1.9f, next.transform.eulerAngles.y);
                Physics.SyncTransforms();
                yield return 0.2f;
                float focusNow = abilities.Focus;
                float foeHealth = next.Health.Current;
                health.SetCurrent(40f);
                bool used = abilities.TryUse(0);
                Check(used && abilities.Focus < focusNow, "Shield Bash spends focus");
                Check(next.Health.Current < foeHealth && next.GetComponent<StatusEffects>().Stunned, "Shield Bash hurts and stuns");
                Check(health.Current > 40f, $"The Bloodstone drinks from the blow ({health.Current})");
            }

            // The Legendary Guardian: speech first, then the stair opens when it falls.
            EnemyAI keeper = System.Array.Find(foes, e => e.EnemyId == "morr");
            ZonePortal stairDown = Door(ZoneNumbers.FirstDepth + 1);
            if (!Check(keeper != null && stairDown != null, "Morr bars the stair down")) yield break;
            Check(stairDown.InteractionPrompt == GameText.Barred, "The stair is barred while the guardian lives");
            controller.Teleport(keeper.transform.position - keeper.transform.forward * 8f, keeper.transform.eulerAngles.y);
            Physics.SyncTransforms();
            yield return 0.6f;
            Check(dialogue.IsOpen, "The guardian speaks before the fight");
            dialogue.Choose(0);
            yield return 0.2f;
            Check(keeper.IsEngaged, "The guardian fights after the speech");
            keeper.Health.ApplyDamage(new DamageInfo(10000f, keeper.transform.position, Vector3.forward, controller.gameObject));
            yield return 0.2f;
            Check(session.HasFlag(StoryDirector.DeadFlag("morr")), "The guardian's death is recorded");
            Check(stairDown.InteractionPrompt == GameText.StairDown, "The stair opens when the guardian falls");

            // Enemy bolts: a caster on the last depth shoots, and the Gloamlord's death ends the story.
            int lastDepth = zones.ZoneCount - 1;
            zones.Travel(lastDepth, false);
            yield return 1.6f;
            Check(zones.CurrentIndex == lastDepth, $"Travel reaches the last depth (zone {zones.CurrentIndex})");
            EnemyAI lord = System.Array.Find(Object.FindObjectsByType<EnemyAI>(), e => e.EnemyId == "gloamlord");
            if (!Check(lord != null, "The Gloamlord sits his throne")) yield break;
            lord.Engage();
            lord.Health.ApplyDamage(new DamageInfo(10000f, lord.transform.position, Vector3.forward, controller.gameObject));
            yield return 0.2f;
            Check(director.Finished && dialogue.IsOpen, "The Gloamlord's death opens the ending");
            dialogue.Choose(0);
            yield return 1.6f;
            Check(zones.CurrentIndex == 1, "The ending returns to Hollowgate");
            Check(RenderSettings.fogEndDistance > 100f, "Surface fog restored");

            // The witch throws fire instead of swinging.
            combat.ApplyClass(Catalog.ClassOf(CharacterClass.Witch));
            input.SetTouchButton(PlayerButton.Attack, true);
            input.SetTouchButton(PlayerButton.Attack, false);
            yield return 0.4f;
            Check(Object.FindAnyObjectByType<Projectile>() != null, "A ranged class throws a bolt");

            // Each class holds its own weapons, in both hands, and nobody else's.
            bool Held(string hand, string weapon)
            {
                Transform t = player.Find($"Camera Pivot/Main Camera/Weapon Rig/{hand}/{weapon}");
                return t != null && t.gameObject.activeInHierarchy;
            }
            const string right = "Sword Pivot (Right Hand)", left = "Shield Pivot (Left Hand)";
            Check(Held(right, "Sword") && Held(left, "Shield"), "The knight holds sword and shield");
            session.ChooseClass(CharacterClass.Gravethief);
            Check(Held(right, "Dagger Right") && Held(left, "Dagger Left"), "The Gravethief holds two daggers");
            Check(!Held(right, "Sword") && !Held(left, "Shield"), "The Gravethief does not hold the knight's weapons");

            // Settings, from the pause menu: everything applies at once.
            var pause = Object.FindAnyObjectByType<PauseMenu>();
            var panel = Object.FindAnyObjectByType<SettingsPanel>();
            var layout = Object.FindAnyObjectByType<TouchLayout>();
            var controls = Object.FindAnyObjectByType<TouchControlsRoot>();
            var hudGroup = Object.FindAnyObjectByType<HudAppearance>().GetComponent<CanvasGroup>();
            var touchGroup = controls.GetComponent<CanvasGroup>();
            if (!Check(pause && panel && layout, "Pause and settings found at runtime")) yield break;

            // A controller reaches the menus without a pointer: Start pauses, the selection lands on a button, B steps back.
            var inputSettings = InputSystem.settings;
            var padFocusBehaviour = inputSettings.editorInputBehaviorInPlayMode;
            var padBackgroundBehaviour = inputSettings.backgroundBehavior;
            inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var pad = InputSystem.AddDevice<Gamepad>("Playtest Gamepad");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start));
            yield return 0.15f;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return 0.4f;
            Check(pause.IsOpen, "Start on a gamepad pauses the game");
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            Check(eventSystem.currentSelectedGameObject != null && eventSystem.currentSelectedGameObject.name == "Resume",
                $"A controller lands on the first pause button ({(eventSystem.currentSelectedGameObject != null ? eventSystem.currentSelectedGameObject.name : "nothing")})");
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
            yield return 0.15f;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return 0.4f;
            Check(!pause.IsOpen && Time.timeScale == 1f, "B on a gamepad steps back out of the pause menu");
            InputSystem.RemoveDevice(pad);
            inputSettings.editorInputBehaviorInPlayMode = padFocusBehaviour;
            inputSettings.backgroundBehavior = padBackgroundBehaviour;
            eventSystem.SetSelectedGameObject(null);

            pause.Toggle();
            Check(pause.IsOpen && Time.timeScale == 0f, "Pause stops the game");
            pause.OpenSettings();
            Check(panel.IsOpen, "Settings open from the pause menu");
            panel.OnHudOpacityChanged(0.4f);
            Check(Mathf.Approximately(hudGroup.alpha, 0.4f), $"The HUD fades with its slider (alpha {hudGroup.alpha:F2})");
            panel.HideHud();
            Check(hudGroup.alpha == 0f, "The HUD can be switched off");
            panel.ShowHud();
            Check(Mathf.Approximately(hudGroup.alpha, 0.4f), "The HUD comes back at the chosen opacity");
            panel.ShowButtons();
            panel.OnButtonOpacityChanged(0.5f);
            Check(input.TouchMode && Mathf.Approximately(touchGroup.alpha, 0.5f), $"The buttons fade with their slider (alpha {touchGroup.alpha:F2})");
            panel.HideButtons();
            Check(!input.TouchMode && touchGroup.alpha == 0f, "The buttons can be switched off");
            panel.ShowButtons();
            panel.OnSensitivityChanged(0.75f);
            Check(Mathf.Approximately(input.LookSensitivity, 2f), $"The slider sets the look speed ({input.LookSensitivity:F2})");

            // Moving a button: the ring moves the control, and the place outlives a reload (checked below).
            RectTransform attackRect = null;
            foreach (var button in Object.FindObjectsByType<TouchButton>())
                if (button.Button == PlayerButton.Attack) attackRect = (RectTransform)button.transform;
            Vector2 attackHome = attackRect.anchoredPosition;
            panel.OpenLayoutEditor();
            Check(layout.IsOpen, "The layout editor opens");
            TouchLayoutHandle ring = System.Array.Find(Object.FindObjectsByType<TouchLayoutHandle>(), h => h.LayoutName == "attack");
            if (!Check(ring != null, "The editor puts a ring on the attack button")) yield break;
            Canvas hudCanvas = ring.GetComponentInParent<Canvas>();
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = new Vector2(600f, 300f) };
            ring.OnPointerDown(pointer);
            pointer.position += new Vector2(-120f, 60f) * hudCanvas.scaleFactor;
            ring.OnDrag(pointer);
            Vector2 attackMoved = attackRect.anchoredPosition;
            Check(Vector2.Distance(attackMoved, attackHome + new Vector2(-120f, 60f)) < 1f, $"Dragging the ring moves the button ({attackHome} to {attackMoved})");
            layout.Finish();
            Check(!layout.IsOpen && panel.IsOpen, "Done returns to the settings");
            panel.Close();
            Check(pause.IsOpen && !panel.IsOpen, "Closing the settings returns to the pause menu");
            yield return 0.4f;
            pause.Resume();
            Check(Time.timeScale == 1f, "Resume restarts the game");

            // Save, spend what was saved, load: everything comes back, and what was cleared stays cleared.
            var soulKeeper = Object.FindAnyObjectByType<SoulKeeper>();
            session.Equip(Catalog.Item("grave_dagger"));
            session.Equip(Catalog.Item("witch_eye"));
            int savedGold = session.Gold, savedHonor = session.Honor, savedZone = zones.CurrentIndex;
            Vector3 savedSpot = player.position;
            Check(soulKeeper.Save(), "Saving works during play");
            session.AddGold(1000);
            soulKeeper.Resume(soulKeeper.LatestSoul());
            yield return 2.0f;

            session = Object.FindAnyObjectByType<GameSession>();
            zones = Object.FindAnyObjectByType<ZoneManager>();
            menu = Object.FindAnyObjectByType<MainMenu>();
            controller = Object.FindAnyObjectByType<FirstPersonController>();
            Check(!menu.IsOpen && Time.timeScale == 1f, "Loading returns straight to the game");
            Check(session.Class.Id == CharacterClass.Gravethief && session.Gold == savedGold && session.Honor == savedHonor, $"The soul is back as saved ({session.Class.Name}, {session.Gold} gold)");
            Check(zones.CurrentIndex == savedZone, $"The soul is back in its zone ({zones.CurrentIndex})");
            Check(session.Weapon != null && session.Weapon.Id == "grave_dagger" && session.Relic != null && session.Relic.Id == "witch_eye", "What was bought is carried again");
            Check(Vector3.Distance(controller.transform.position, savedSpot) < 1.5f, "The soul is back on its spot");
            Check(session.HasFlag(StoryDirector.DeadFlag("morr")), "The saved story flags are back");
            TreasureChest valleyChest = System.Array.Find(Object.FindObjectsByType<TreasureChest>(FindObjectsInactive.Include), c => c.SaveKey == "chest_valley");
            Check(valleyChest != null && valleyChest.IsOpen, "An opened chest stays open after loading");
            Check(!Object.FindObjectsByType<CursedKnightAI>(FindObjectsInactive.Include)[0].gameObject.activeSelf, "The slain knight stays gone after loading");
            EnemyAI morr = System.Array.Find(Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include), e => e.EnemyId == "morr");
            Check(morr != null && !morr.gameObject.activeSelf, "The slain guardian stays gone after loading");
            foreach (var button in Object.FindObjectsByType<TouchButton>())
                if (button.Button == PlayerButton.Attack) Check(Vector2.Distance(((RectTransform)button.transform).anchoredPosition, attackMoved) < 1f, "The moved button stays moved after a reload");
            Check(Mathf.Approximately(Object.FindAnyObjectByType<HudAppearance>().GetComponent<CanvasGroup>().alpha, 0.4f), "The HUD opacity survives a reload");

            // The main menu shows the ledger, the soul and the Codex.
            soulKeeper = Object.FindAnyObjectByType<SoulKeeper>();
            soulKeeper.ReturnToMenu();
            yield return 2.0f;
            menu = Object.FindAnyObjectByType<MainMenu>();
            soulKeeper = Object.FindAnyObjectByType<SoulKeeper>();
            Check(menu.IsOpen, "Leaving the game returns to the main menu");
            Check(menu.SelectedSoul != null && menu.SelectedSoul.Class == CharacterClass.Gravethief, "The roster shows the soul");
            MetaRecord meta = soulKeeper.Meta;
            Check(meta.Souls > 0 && meta.Descents == 1 && meta.Deepest == 5, $"The ledger counts souls, descents and depth ({meta.Souls}, {meta.Descents}, {meta.Deepest})");
            Check(meta.Codex.Contains(CodexBook.PersonId("merchant")) && meta.Codex.Contains(CodexBook.FoeId("morr")) && meta.Codex.Contains(CodexBook.PlaceId(zones.ZoneCount - 1)), $"The Codex records people, foes and places ({meta.Codex.Count}/{CodexBook.Entries.Count})");
            var codex = Object.FindAnyObjectByType<CodexPanel>();
            menu.OpenCodex();
            yield return 0.4f;
            // The panel shares its canvas with the main menu, so look inside the list only.
            var codexRows = codex.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content.GetComponentsInChildren<UnityEngine.UI.Button>();
            Check(codex.IsOpen && codexRows.Length == CodexBook.Entries.Count, $"The Codex lists every entry ({codexRows.Length})");
            Check(codexRows[1].targetGraphic.canvasRenderer.GetColor().a < 0.3f, $"Codex rows open clear, not grey (alpha {codexRows[1].targetGraphic.canvasRenderer.GetColor().a:F2})");
            codex.Close();
            Check(!codex.IsOpen && menu.IsOpen, "The Codex closes back to the main menu");

            // On a gamepad the selection stays on the buttons, left and right turn the row, and B steps back.
            carousel = Object.FindAnyObjectByType<ClassCarousel>();
            menu.NewSoul();
            yield return 0.2f;
            var cardEvents = UnityEngine.EventSystems.EventSystem.current;
            cardEvents.SetSelectedGameObject(carousel.transform.parent.Find("Pager/Next").gameObject);
            yield return 0.2f;
            Check(cardEvents.currentSelectedGameObject != null && cardEvents.currentSelectedGameObject.GetComponent<ClassCarouselKeys>() != null, "A pressed arrow does not take the keys away from the row");
            var cardPad = InputSystem.AddDevice<Gamepad>("Playtest Gamepad");
            yield return 0.1f;
            InputSystem.QueueStateEvent(cardPad, new GamepadState().WithButton(GamepadButton.DpadRight));
            yield return 0.1f;
            InputSystem.QueueStateEvent(cardPad, new GamepadState());
            yield return 0.6f;
            Check(carousel.Selected == 1, $"The d-pad turns the row (card {carousel.Selected})");
            InputSystem.QueueStateEvent(cardPad, new GamepadState().WithButton(GamepadButton.East));
            yield return 0.1f;
            InputSystem.QueueStateEvent(cardPad, new GamepadState());
            yield return 0.4f;
            Check(!carousel.IsOpen && menu.IsOpen, "B on a gamepad leaves the row of cards");
            InputSystem.RemoveDevice(cardPad);

            menu.ContinueSoul();
            yield return 0.5f;
            Check(!menu.IsOpen && Time.timeScale == 1f && Object.FindAnyObjectByType<GameSession>().Class.Id == CharacterClass.Gravethief, "The roster continues the soul");
            Object.FindAnyObjectByType<SoulKeeper>().ReturnToMenu();
            yield return 2.0f;

            // Releasing a soul asks first, and keeps the ledger.
            menu = Object.FindAnyObjectByType<MainMenu>();
            var confirm = Object.FindAnyObjectByType<ConfirmPanel>();
            menu.ReleaseSoul();
            Check(confirm.IsOpen && SaveStore.LoadRoster().Souls.Count == 1, "Releasing a soul asks first");
            confirm.Cancel();
            Check(SaveStore.LoadRoster().Souls.Count == 1, "Cancel keeps the soul");
            menu.ReleaseSoul();
            confirm.Yes();
            Check(SaveStore.LoadRoster().Souls.Count == 0 && menu.SelectedSoul == null, "Yes releases the soul");
            Check(SaveStore.LoadMeta().Souls == meta.Souls, "The ledger outlives a released soul");

            ClearOwnSaveData();
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
