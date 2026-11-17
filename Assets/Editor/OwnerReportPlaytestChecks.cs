using System;
using System.Collections.Generic;
using System.Linq;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Input;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.UI;
using AshenRealm.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// The owner's play reports: every class runs from every run control in the valley, the city and a depth; the torch is
    /// put away under a roof but not underground; and Hollowgate is a peaceful street where nothing can be swung or cast.
    /// </summary>
    internal static class OwnerReportPlaytestChecks
    {
        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var session = GameSession.Instance;
            var growth = PlayerProgression.Instance;
            var input = Object.FindAnyObjectByType<PlayerInputHub>();
            if (controller == null || zones == null || session == null || growth == null || input == null)
            { check(false, "The report checks need the player, zones, input and a soul"); yield break; }
            var stamina = controller.GetComponent<Stamina>();
            var combat = controller.GetComponent<PlayerCombat>();
            var abilities = controller.GetComponent<PlayerAbilities>();
            Transform camera = controller.GetComponentsInChildren<Camera>(true).First(c => c.CompareTag("MainCamera")).transform;
            Transform torchMount = camera.Find("Carried Torch");
            Transform swordPivot = camera.Find("Weapon Rig/Sword Pivot (Right Hand)");
            CharacterClass oldClass = session.Class.Id;
            ProgressRecord oldProgress = growth.Capture();
            bool oldTorch = growth.TorchEquipped;
            var settings = InputSystem.settings;
            var focusBehaviour = settings.editorInputBehaviorInPlayMode;
            var backgroundBehaviour = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>("Report Keyboard");
            var pad = InputSystem.AddDevice<Gamepad>("Report Gamepad");
            bool TorchShown() => torchMount.gameObject.activeInHierarchy && PlayerTorch.Instance.Light.enabled;
            CanvasGroup Group(PlayerButton button) => Object.FindObjectsByType<TouchButton>(FindObjectsInactive.Include)
                .First(b => b.Button == button).GetComponent<CanvasGroup>();
            void Release()
            {
                input.SetTouchMove(Vector2.zero);
                if (input.SprintToggled) input.CancelSprint();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(pad, new GamepadState());
            }

            try
            {
                // ---- 3. Every class runs from every run control, wherever it can walk ----
                foreach (int zoneIndex in new[] { ZoneNumbers.Valley, ZoneNumbers.City, ZoneNumbers.FirstDepth })
                {
                    zones.Place(zoneIndex, Vector3.zero, 0f, false);
                    yield return 0.3f;
                    Vector3 start = controller.transform.position;
                    float yaw = OpenLane(start, out float free);
                    check(free >= 7f, $"Zone {zoneIndex} has an open lane from its entry for the run test ({free:F1} m)");
                    for (int classId = 0; classId < 6; classId++)
                    {
                        session.ChooseClass((CharacterClass)classId);
                        string who = $"{(CharacterClass)classId} in zone {zoneIndex}";
                        float walk = 0f;
                        foreach (string device in new[] { "walk", "touch", "keyboard", "gamepad" })
                        {
                            controller.Teleport(start, yaw, 0f);
                            stamina.Refill();
                            Release();
                            yield return 0.15f;
                            if (device == "walk") input.SetTouchMove(Vector2.up);
                            else if (device == "touch") { input.SetTouchButton(PlayerButton.Sprint, true); input.SetTouchButton(PlayerButton.Sprint, false); input.SetTouchMove(Vector2.up); }
                            else if (device == "keyboard") InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                            else InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up }.WithButton(GamepadButton.LeftStick));
                            yield return 0.35f;
                            Vector3 from = controller.transform.position; float t0 = Time.time;
                            bool sprinting = false;
                            for (int i = 0; i < 4; i++) { yield return 0.1f; sprinting |= controller.IsSprinting; }
                            float speed = Flat(controller.transform.position - from).magnitude / Mathf.Max(0.01f, Time.time - t0);
                            float spent = stamina.Max - stamina.Current;
                            Release();
                            if (device == "walk") { walk = speed; check(walk > 2.5f, $"{who} walks ({walk:F2} m/s)"); continue; }
                            check(sprinting && speed > walk * 1.4f && spent > 1f,
                                $"{who} runs from the {device} run control ({speed:F2} m/s against {walk:F2} m/s walking, {spent:F0} stamina)");
                        }
                    }
                }
                // Out of stamina the run stops.
                zones.Place(ZoneNumbers.Valley, Vector3.zero, 0f, false);
                session.ChooseClass(CharacterClass.Witch);
                yield return 0.3f;
                Vector3 tired = controller.transform.position;
                controller.Teleport(tired, OpenLane(tired, out _), 0f);
                stamina.Drain(stamina.Max);
                input.SetTouchButton(PlayerButton.Sprint, true); input.SetTouchButton(PlayerButton.Sprint, false);
                input.SetTouchMove(Vector2.up);
                yield return 0.5f;
                check(!controller.IsSprinting && controller.NormalizedSpeed < 1.2f, $"An exhausted witch cannot run ({controller.NormalizedSpeed:F2} of walking speed)");
                Release();

                // ---- 4. The torch waits at the door of every house, and comes back outside ----
                session.ChooseClass(CharacterClass.Knight);
                if (!growth.TorchEquipped) growth.ToggleTorch();
                int[] houses = { ZoneNumbers.Tavern, ZoneNumbers.Forge, ZoneNumbers.Chapel, ZoneNumbers.FirstHome, ZoneNumbers.FirstHome + 1, ZoneNumbers.FirstHome + 2 };
                foreach (int house in houses)
                {
                    zones.Place(house, Vector3.zero, 0f, false);
                    yield return 0.2f;
                    check(zones.Current.Indoors && !TorchShown() && growth.TorchEquipped && PlayerTorch.Instance.Equipped,
                        $"Zone {house}: the torch and its light are put away indoors, while the saved choice stays equipped");
                    int back = house >= ZoneNumbers.FirstHome ? ZoneNumbers.Valley : ZoneNumbers.City;
                    zones.Place(back, Vector3.zero, 0f, false);
                    yield return 0.2f;
                    check(TorchShown(), $"Zone {house}: leaving for zone {back} puts the equipped torch back in hand");
                }
                // Through the real doors: street, tavern, stair down, and back up.
                ZonePortal Door(int zone) => Object.FindObjectsByType<ZonePortal>().FirstOrDefault(p => p.TargetZone == zone);
                zones.Place(ZoneNumbers.City, Vector3.zero, 0f, false);
                yield return 0.2f;
                Door(ZoneNumbers.Tavern).Interact(controller.gameObject);
                yield return 1.8f;
                check(zones.CurrentIndex == ZoneNumbers.Tavern && !TorchShown() && growth.TorchEquipped, "Walking into the tavern puts the torch away");
                Door(ZoneNumbers.FirstDepth).Interact(controller.gameObject);
                yield return 1.8f;
                check(zones.CurrentIndex == ZoneNumbers.FirstDepth && TorchShown() && combat.Armed, "The depths are no house: the torch burns there");
                Door(ZoneNumbers.Tavern).Interact(controller.gameObject);
                yield return 1.8f;
                Door(ZoneNumbers.City).Interact(controller.gameObject);
                yield return 1.8f;
                check(zones.CurrentIndex == ZoneNumbers.City && TorchShown(), "Back on the evening street the torch is in hand again");
                growth.ToggleTorch();
                zones.Place(ZoneNumbers.Forge, Vector3.zero, 0f, false);
                yield return 0.2f;
                zones.Place(ZoneNumbers.City, Vector3.zero, 0f, false);
                yield return 0.2f;
                check(!growth.TorchEquipped && !TorchShown(), "A torch left in the pack stays there after a house visit");

                // ---- 5. Hollowgate is peaceful: weapons away, nothing swings or casts, the torch stays usable ----
                Zone city = zones.Current;
                check(city.GetComponentsInChildren<EnemyAI>(true).Length == 0 && city.GetComponentsInChildren<CursedKnightAI>(true).Length == 0,
                    "Nothing in Hollowgate needs a weapon: no foe stands in the city");
                session.ChooseClass(CharacterClass.Witch);
                growth.Restore(new ProgressRecord
                {
                    SkillSchemaVersion = PlayerProgression.SkillSchema,
                    Skills = new List<SkillProgressRecord> { new SkillProgressRecord { Id = "firebolt", Tier = 1 }, new SkillProgressRecord { Id = "meditate", Tier = 1 } },
                    EquippedSkillIds = new[] { "firebolt", "meditate" }, Passive = new PassiveProgressRecord { DescentId = session.Descents }
                });
                abilities.SetFocus(abilities.MaxFocus);
                yield return 0.8f;
                check(!combat.Armed && !swordPivot.gameObject.activeInHierarchy, "Hollowgate's street puts the weapons away");
                check(new[] { PlayerButton.Attack, PlayerButton.Block, PlayerButton.Ability1, PlayerButton.Ability2 }.All(b => Group(b).alpha == 0f && !Group(b).blocksRaycasts),
                    "The touch fighting buttons leave on the city street");
                float focus = abilities.Focus;
                int bolts = Object.FindObjectsByType<Projectile>().Length;
                input.SetTouchButton(PlayerButton.Attack, true); input.SetTouchButton(PlayerButton.Attack, false);
                input.SetTouchButton(PlayerButton.Ability1, true); input.SetTouchButton(PlayerButton.Ability1, false);
                input.SetTouchButton(PlayerButton.Ability2, true); input.SetTouchButton(PlayerButton.Ability2, false);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.R));
                InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f }.WithButton(GamepadButton.RightShoulder));
                bool swung = false;
                for (int i = 0; i < 6; i++) { yield return 0.1f; swung |= combat.IsAttacking; }
                Release();
                check(!swung && Object.FindObjectsByType<Projectile>().Length == bolts, "Neither the touch, keyboard nor gamepad attack swings or throws in the city");
                check(Mathf.Approximately(abilities.Focus, focus) && abilities.CooldownFraction(0) <= 0f && abilities.CooldownFraction(1) <= 0f,
                    "Neither a combat skill nor a self skill can be cast in the city");
                growth.ToggleTorch();
                yield return 0.2f;
                check(growth.TorchEquipped && TorchShown(), "The torch can still be drawn on the evening street");
                Door(ZoneNumbers.Tavern).Interact(controller.gameObject);
                yield return 1.8f;
                Door(ZoneNumbers.FirstDepth).Interact(controller.gameObject);
                yield return 1.8f;
                check(combat.Armed && Group(PlayerButton.Attack).alpha > 0f, "Below the tavern the weapons and fighting buttons come back");
                zones.Place(ZoneNumbers.Valley, Vector3.zero, 0f, false);
                yield return 0.2f;
                check(combat.Armed, "The valley road is armed too");

                // ---- A full bar pays for any skill: the cost is capped at the caster's maximum focus ----
                session.ChooseClass(CharacterClass.Knight);
                abilities.ApplyClass(session.Class);
                growth.Restore(null);
                bool rally = growth.TryEquipAbility(1, "rally");
                yield return 0.1f;
                abilities.SetFocus(abilities.MaxFocus - 1f);
                bool refused = !abilities.TryUse(1) && abilities.CooldownFraction(1) <= 0f;
                abilities.SetFocus(abilities.MaxFocus);
                bool cast = abilities.TryUse(1);
                check(rally && AbilityBook.Find("rally").Cost > abilities.MaxFocus && refused && cast && abilities.Focus < 0.05f && abilities.CooldownFraction(1) > 0f,
                    $"A fresh Knight's full bar ({abilities.MaxFocus}) pays for Rally ({AbilityBook.Find("rally").Cost}) and ends empty ({abilities.Focus:F2}); a bar short of full is refused with the cooldown untouched");
                session.ChooseClass(CharacterClass.Witch);
                abilities.ApplyClass(session.Class);
                growth.Restore(new ProgressRecord
                {
                    SkillSchemaVersion = PlayerProgression.SkillSchema,
                    Skills = new List<SkillProgressRecord> { new SkillProgressRecord { Id = "firebolt", Tier = 1 }, new SkillProgressRecord { Id = "meditate", Tier = 1 } },
                    EquippedSkillIds = new[] { "firebolt", "meditate" }, Passive = new PassiveProgressRecord { DescentId = session.Descents }
                });
                yield return 0.1f;
                abilities.SetFocus(abilities.MaxFocus);
                float full = abilities.Focus;
                bool bolt = abilities.TryUse(0);
                int boltCost = AbilityBook.Find("firebolt").Cost;
                check(bolt && boltCost < abilities.MaxFocus && Mathf.Abs(full - abilities.Focus - boltCost) < 0.01f,
                    $"Below the maximum a cast spends exactly the workbook cost (Hollow Fire {boltCost}: {full:F1} to {abilities.Focus:F1})");
                abilities.ApplyClass(session.Class);
            }
            finally
            {
                Release();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(pad);
                settings.editorInputBehaviorInPlayMode = focusBehaviour;
                settings.backgroundBehavior = backgroundBehaviour;
                session.ChooseClass(oldClass);
                growth.Restore(oldProgress);
                if (growth.TorchEquipped != oldTorch) growth.ToggleTorch();
            }
        }

        /// <summary>The yaw of the longest open run from a point, and how long it is.</summary>
        static float OpenLane(Vector3 feet, out float free)
        {
            free = 0f;
            float yaw = 0f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                float run = Physics.SphereCast(feet + Vector3.up * 0.9f, 0.45f, d, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 12f;
                if (run > free) { free = run; yaw = i * 22.5f; }
            }
            return yaw;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
