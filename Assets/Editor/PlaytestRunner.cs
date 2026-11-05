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
    /// Ashen Realm > Run Automated Playtest: enters Play mode in Prototype_Valley and drives the player through
    /// the input hub (the same entry points the touch controls use): walk, sprint, jump, block a knight attack,
    /// defeat the knight, open the chest, die and respawn. Any logged error or exception fails the run.
    /// State survives the Play-mode domain reload through SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaytestRunner
    {
        const string StateKey = "AshenRealm.Playtest.State";   // 0 idle, 1 entering, 2 running, 3 finished
        const string ResultKey = "AshenRealm.Playtest.Result";
        const string BatchKey = "AshenRealm.Playtest.Batch";
        const string LogPrefix = "[AshenRealm][Playtest]";
        const double TimeoutSeconds = 240.0;

        static IEnumerator routine;
        static float waitUntil;
        static double startedAt;
        static readonly List<string> results = new List<string>();
        static readonly List<string> loggedErrors = new List<string>();
        static int failures;

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

            var input = Object.FindAnyObjectByType<PlayerInputHub>();
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var combat = Object.FindAnyObjectByType<PlayerCombat>();
            var lifecycle = Object.FindAnyObjectByType<PlayerLifecycle>();
            var knight = Object.FindAnyObjectByType<CursedKnightAI>();
            var chest = Object.FindAnyObjectByType<TreasureChest>();
            if (!Check(input && controller && combat && lifecycle && knight && chest, "Scene actors found at runtime")) yield break;

            // The class choice opens the game: two pages, then time starts.
            var choice = Object.FindAnyObjectByType<DialoguePanel>();
            Check(choice != null && choice.IsOpen, "The class choice opens the game");
            choice.Choose((int)CharacterClass.Knight);
            Check(choice.IsOpen, "A class shows its story before it is chosen");
            choice.Choose(0);
            Check(!choice.IsOpen && GameSession.Instance.ClassChosen && GameSession.Instance.Class.Id == CharacterClass.Knight, "The knight is chosen");
            Check(GameSession.Instance.Honor == Catalog.ClassOf(CharacterClass.Knight).StartHonor, "The class sets the starting honor");
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
            dialogue.Choose(0);
            Check(session.Potions == potionsBefore + 1 && session.Gold < goldBefore, $"The merchant sells a potion ({session.Potions} potions, {session.Gold} gold)");
            dialogue.Choose(0);
            Check(!dialogue.IsOpen && Time.timeScale == 1f, "Closing the dialogue resumes time");

            zones.Travel(2, false);
            yield return 1.6f;
            Check(zones.CurrentIndex == 2 && zones.Current.Underground, $"The stair leads to the first depth (zone {zones.CurrentIndex})");
            Check(RenderSettings.sun != null && !RenderSettings.sun.enabled, "No sun underground");
            Check(controller.GetComponentInChildren<Light>(false) != null, "The torch burns underground");

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
                float focusBefore = abilities.Focus;
                float foeHealth = next.Health.Current;
                bool used = abilities.TryUse(0);
                Check(used && abilities.Focus < focusBefore, "Shield Bash spends focus");
                Check(next.Health.Current < foeHealth && next.GetComponent<StatusEffects>().Stunned, "Shield Bash hurts and stuns");
            }

            // The Legendary Guardian: speech first, then the stair opens when it falls.
            EnemyAI keeper = System.Array.Find(foes, e => e.EnemyId == "morr");
            ZonePortal stairDown = System.Array.Find(Object.FindObjectsByType<ZonePortal>(), p => p.TargetZone == 3);
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
            zones.Travel(6, false);
            yield return 1.6f;
            Check(zones.CurrentIndex == 6, $"Travel reaches the last depth (zone {zones.CurrentIndex})");
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
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
