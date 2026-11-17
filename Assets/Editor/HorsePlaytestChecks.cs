using System;
using System.Collections.Generic;
using System.Linq;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.UI;
using AshenRealm.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Stage B, the horse, driven through the input hub (touch buttons and joystick, a virtual keyboard and gamepad): buying it
    /// from Hilda, mounting, the paces and their stamina, turning, stopping before walls, trees and the lake, the whole main road,
    /// dismounting, doors and the city gate, being thrown off, death, saving in the saddle, touch mode and the frame it covers.
    /// </summary>
    internal static class HorsePlaytestChecks
    {
        public const float MaxCoverage = 0.18f;

        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var input = Object.FindAnyObjectByType<PlayerInputHub>();
            var dialogue = Object.FindAnyObjectByType<DialoguePanel>();
            var keeper = Object.FindAnyObjectByType<SoulKeeper>();
            var horse = Object.FindAnyObjectByType<RidingHorse>(FindObjectsInactive.Include);
            var session = GameSession.Instance;
            if (controller == null || zones == null || input == null || dialogue == null || keeper == null || horse == null || session == null)
            { check(false, "The horse checks need the player, zones, input, the dialogue, the keeper, the horse and a soul"); yield break; }
            Transform player = controller.transform;
            var stamina = controller.GetComponent<Stamina>();
            var combat = controller.GetComponent<PlayerCombat>();
            var health = controller.GetComponent<Health>();
            var lifecycle = controller.GetComponent<PlayerLifecycle>();
            var interactor = controller.GetComponent<PlayerInteractor>();
            var body = controller.GetComponent<CharacterController>();
            Transform eye = controller.GetComponentsInChildren<Camera>(true).First(c => c.CompareTag("MainCamera")).transform.parent;
            Camera camera = eye.GetComponentInChildren<Camera>();
            var bodyText = new UnityEditor.SerializedObject(dialogue).FindProperty("bodyText").objectReferenceValue as Text;
            var wiring = new UnityEditor.SerializedObject(horse);
            GameObject model = wiring.FindProperty("model").objectReferenceValue as GameObject;
            var horseBody = wiring.FindProperty("body").objectReferenceValue as BoxCollider;
            Transform[] spots = Enumerable.Range(0, 4).Select(i => wiring.FindProperty("posts").GetArrayElementAtIndex(i).objectReferenceValue as Transform).ToArray();
            Transform neck = horse.transform.Find("Model/Body/Neck"), ears = neck.Find("Ears");
            string heard = null;
            void Hear(string message) => heard = message;
            void Press(PlayerButton button) { input.SetTouchButton(button, true); input.SetTouchButton(button, false); }
            CanvasGroup Group(PlayerButton button) => Object.FindObjectsByType<TouchButton>(FindObjectsInactive.Include)
                .First(b => b.Button == button).GetComponent<CanvasGroup>();
            bool Near(Transform spot) => Flat(horse.transform.position - spot.position).magnitude < 0.1f;
            Transform NearestSpot(Vector3 at) => spots.OrderBy(s => Flat(s.position - at).sqrMagnitude).First();
            void Ride(Vector3 feet, float yaw) => controller.Teleport(feet + Vector3.up * 0.1f, yaw, 0f);
            // The parts of the head that come first: the muzzle, the poll and the ear tips.
            Vector3[] Head() => new[] { neck.TransformPoint(new Vector3(0f, 0.07f, -0.93f)), neck.TransformPoint(new Vector3(0f, 0.62f, -0.5f)), ears.TransformPoint(Vector3.up * 0.17f) };

            float footHeight = body.height, footRadius = body.radius, footStep = body.stepOffset, footEye = eye.localPosition.y;
            Vector3 footCenter = body.center;
            bool OnFoot() => !controller.Mounted && body.height == footHeight && body.radius == footRadius && body.center == footCenter &&
                body.stepOffset == footStep && eye.localPosition.y == footEye;

            var settings = InputSystem.settings;
            var focusBehaviour = settings.editorInputBehaviorInPlayMode;
            var backgroundBehaviour = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>("Horse Keyboard");
            var pad = InputSystem.AddDevice<Gamepad>("Horse Gamepad");
            bool touchMode = input.TouchMode;
            GameMessages.Notification += Hear;
            void Release()
            {
                input.SetTouchMove(Vector2.zero);
                if (input.SprintToggled) input.CancelSprint();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(pad, new GamepadState());
            }

            // Walks to the horse, aims at it and takes the saddle with Use (touch, or the keyboard's E).
            IEnumerable<float> Mount(bool keys = false)
            {
                Release();
                if (!PeoplePlaytestChecks.TryApproach(horseBody, out Vector3 feet, out Quaternion look))
                { check(false, "There is a place to stand beside the horse"); yield break; }
                Vector3 angles = look.eulerAngles;
                controller.Teleport(feet, angles.y, angles.x > 180f ? angles.x - 360f : angles.x);
                Physics.SyncTransforms();
                yield return 0.2f;
                if (keys)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    yield return 0.1f;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                }
                else Press(PlayerButton.Interact);
                yield return 0.3f;
            }

            try
            {
                session.SetFlag(StoryDirector.DeadFlag("knight"));
                zones.Place(ZoneNumbers.Valley, Vector3.zero, 0f, false);
                yield return 0.3f;
                check(!session.HasFlag(RidingHorse.OwnedFlag) && !model.activeSelf && !horseBody.enabled,
                    "Before it is bought there is no horse to ride");

                // ---- 1. Buying from Hilda: refused with 30 gold, sold once for 60, then no longer offered ----
                NpcActor hilda = zones.Current.GetComponentsInChildren<NpcActor>().First(n => n.NpcId == "stablemaster");
                if (!PeoplePlaytestChecks.TryApproach(hilda.GetComponent<Collider>(), out Vector3 hildaFeet, out Quaternion hildaLook))
                { check(false, "There is a place to stand before Hilda"); yield break; }
                IEnumerable<float> TalkToHilda()
                {
                    Vector3 a = hildaLook.eulerAngles;
                    controller.Teleport(hildaFeet, a.y, a.x > 180f ? a.x - 360f : a.x);
                    Physics.SyncTransforms();
                    yield return 0.2f;
                    Press(PlayerButton.Interact);
                    yield return 0.15f;
                }
                session.TrySpend(session.Gold);
                session.AddGold(30);
                foreach (float w in TalkToHilda()) yield return w;
                bool offered = dialogue.IsOpen;
                dialogue.Choose(0);
                yield return 0.15f;
                check(offered && dialogue.IsOpen && bodyText.text == GameText.NotEnoughGold && session.Gold == 30 && !session.HasFlag(RidingHorse.OwnedFlag),
                    $"With 30 gold Hilda's horse ({StoryDirector.HorsePrice}) is refused and nothing changes");
                dialogue.Choose(0);
                yield return 0.15f;
                session.AddGold(StoryDirector.HorsePrice - 30);
                foreach (float w in TalkToHilda()) yield return w;
                dialogue.Choose(0);
                yield return 0.15f;
                bool sold = dialogue.IsOpen && bodyText.text == GameText.HorseBought && session.Gold == 0 && session.HasFlag(RidingHorse.OwnedFlag);
                dialogue.Choose(0);
                yield return 0.2f;
                check(sold && model.activeSelf && horseBody.enabled && Near(spots[2]),
                    "With 60 gold the horse is bought: the flag is set, the gold spent, and the saddled horse stands at the stable's rail");
                session.AddGold(10);
                foreach (float w in TalkToHilda()) yield return w;
                dialogue.Choose(0);
                yield return 0.15f;
                check(!dialogue.IsOpen && session.Gold == 10, "Once the horse is owned Hilda offers only farewell");

                // ---- On foot, the reference: walking and running two seconds up the straight before the city gate ----
                Vector3 lane = WorldPlaytestChecks.Ground(0f, 500f);
                Release();
                controller.Teleport(lane + Vector3.up * 0.1f, 0f, 0f);
                stamina.Refill();
                yield return 0.4f;
                Vector3 from = player.position;
                input.SetTouchMove(Vector2.up);
                yield return 2f;
                float walked = Flat(player.position - from).magnitude;
                Release();
                yield return 0.3f;
                controller.Teleport(lane + Vector3.up * 0.1f, 0f, 0f);
                stamina.Refill();
                yield return 0.4f;
                from = player.position;
                float before = stamina.Current;
                Press(PlayerButton.Sprint);
                input.SetTouchMove(Vector2.up);
                yield return 2f;
                float ran = Flat(player.position - from).magnitude, footPerMetre = (before - stamina.Current) / Mathf.Max(0.01f, ran);
                Release();

                // ---- 2. Aiming at the horse shows FELÜLÉS; the keyboard's Use mounts ----
                if (!PeoplePlaytestChecks.TryApproach(horseBody, out Vector3 standBy, out Quaternion lookAt)) { check(false, "There is a place to stand beside the horse"); yield break; }
                Vector3 lookAngles = lookAt.eulerAngles;
                controller.Teleport(standBy, lookAngles.y, lookAngles.x > 180f ? lookAngles.x - 360f : lookAngles.x);
                Physics.SyncTransforms();
                yield return 0.2f;
                check(ReferenceEquals(interactor.Current, horse) && interactor.CurrentPrompt == GameText.Mount, $"Aiming at the horse shows {GameText.Mount}");
                foreach (float w in Mount(true)) yield return w;
                check(controller.Mounted && horse.Ridden && Mathf.Abs(eye.localPosition.y - 2.45f) < 0.01f && Mathf.Abs(body.height - 2.6f) < 0.01f &&
                    Mathf.Abs(body.radius - 0.5f) < 0.01f && !combat.Armed && horse.transform.parent == player,
                    $"Use mounts: eye {eye.localPosition.y:F2} m, body {body.height:F2} x {body.radius:F2} m, weapons away");
                check(new[] { PlayerButton.Attack, PlayerButton.Block, PlayerButton.Ability1, PlayerButton.Ability2 }.All(b => Group(b).alpha == 0f && !Group(b).blocksRaycasts),
                    "In the saddle the touch fighting buttons are gone");

                // ---- 14. Mounted at rest the horse keeps to the bottom of the frame ----
                Ride(lane, 0f);
                yield return 1.2f;
                Renderer[] parts = horse.GetComponentsInChildren<Renderer>();
                ShadowCastingMode[] shadows = parts.Select(p => p.shadowCastingMode).ToArray();
                foreach (Renderer p in parts) p.shadowCastingMode = ShadowCastingMode.Off; // its shadow on the road is not the horse in view
                try
                {
                    HandCoveragePlaytestChecks.Measure(camera, parts.ToList(), 360 * 16 / 9, out float wide, out _, out float wideTop, "horse_mounted_16x9");
                    HandCoveragePlaytestChecks.Measure(camera, parts.ToList(), 360 * 4 / 3, out float square, out _, out float squareTop, "horse_mounted_4x3");
                    check(wide > 0.005f && square > 0.005f && wide <= MaxCoverage && square <= MaxCoverage && wideTop <= 0.5f && squareTop <= 0.5f,
                        $"From the saddle the horse covers {wide * 100f:F1}% at 16:9 and {square * 100f:F1}% at 4:3 (limit {MaxCoverage * 100f:F0}%), " +
                        $"reaching {wideTop * 100f:F0}%/{squareTop * 100f:F0}% of the height: the crosshair and the upper half stay clear");
                }
                finally
                {
                    for (int i = 0; i < parts.Length; i++) parts[i].shadowCastingMode = shadows[i];
                }

                // ---- 3. The paces: a canter beats walking, a gallop (gamepad) beats running and costs less per metre ----
                Ride(lane, 0f);
                stamina.Refill();
                yield return 0.4f;
                from = player.position;
                input.SetTouchMove(Vector2.up);
                yield return 2f;
                float cantered = Flat(player.position - from).magnitude;
                Release();
                check(cantered > walked * 1.4f, $"Riding two seconds covers {cantered:F1} m, walking {walked:F1} m");
                yield return 0.5f;
                Ride(lane, 0f);
                stamina.Refill();
                yield return 0.4f;
                from = player.position;
                before = stamina.Current;
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up }.WithButton(GamepadButton.LeftStick));
                yield return 2f;
                float galloped = Flat(player.position - from).magnitude, top = Flat(controller.Velocity).magnitude;
                float ridePerMetre = (before - stamina.Current) / Mathf.Max(0.01f, galloped);
                Release();
                check(galloped > ran * 1.3f && top > 11f && ridePerMetre > 0f && ridePerMetre < footPerMetre,
                    $"A gallop covers {galloped:F1} m in two seconds at {top:F1} m/s for {ridePerMetre:F2} stamina a metre; running {ran:F1} m for {footPerMetre:F2}");
                yield return 0.5f;
                Ride(lane, 0f);
                stamina.Drain(stamina.Max);
                Press(PlayerButton.Sprint);
                input.SetTouchMove(Vector2.up);
                yield return 1.2f;
                float tired = Flat(controller.Velocity).magnitude;
                check(stamina.IsExhausted && !controller.IsSprinting && tired < 7.5f, $"An exhausted rider cannot gallop ({tired:F1} m/s)");
                Release();
                stamina.Refill();
                yield return 0.6f;

                // ---- 4. Sideways input turns the horse instead of stepping aside ----
                Ride(lane, 0f);
                yield return 0.4f;
                from = player.position;
                float yawBefore = player.eulerAngles.y, started = Time.time;
                input.SetTouchMove(Vector2.right);
                yield return 0.5f;
                float turned = Mathf.DeltaAngle(yawBefore, player.eulerAngles.y), rate = turned / Mathf.Max(0.01f, Time.time - started);
                Release();
                check(turned > 30f && Flat(player.position - from).magnitude < 0.3f, $"Sideways input turns the horse ({rate:F0} degrees a second) without moving it");

                // ---- 5. Walls, trees and the lake shore stop the horse before its head touches them ----
                Zone valley = zones.Current;
                Collider wall = valley.GetComponentsInChildren<Collider>().First(c => c.name == "Gate Wall" && Mathf.Abs(c.bounds.center.z - 93f) < 3f);
                Collider tree = valley.GetComponentsInChildren<CapsuleCollider>().Where(c => c.name.StartsWith("Tree Colliders"))
                    .OrderBy(c => Flat(c.bounds.center - new Vector3(-40f, 0f, 300f)).sqrMagnitude).Take(40).FirstOrDefault(c => FindApproach(c, out _, out _));
                Collider shore = valley.GetComponentsInChildren<Collider>().Where(c => c.name == "Lake Shore")
                    .OrderBy(c => Flat(c.bounds.center - new Vector3(100f, 0f, 380f)).sqrMagnitude).Take(40).FirstOrDefault(c => FindApproach(c, out _, out _));
                foreach (var (what, target) in new[] { ("the toll gate's wall", wall), ("a tree", tree), ("the lake shore", shore) })
                {
                    if (target == null || !FindApproach(target, out Vector3 start, out float yaw)) { check(false, $"There is a run-up to {what}"); continue; }
                    Ride(start, yaw);
                    stamina.Refill();
                    yield return 0.3f;
                    Press(PlayerButton.Sprint);
                    input.SetTouchMove(Vector2.up);
                    float closest = float.MaxValue;
                    for (float t = 0f; t < 3f; t += 0.05f)
                    {
                        yield return 0.05f;
                        foreach (Vector3 p in Head()) closest = Mathf.Min(closest, Vector3.Distance(target.ClosestPoint(p), p));
                    }
                    float still = Flat(controller.Velocity).magnitude;
                    Release();
                    check(still < 0.5f && closest > 0.01f, $"Riding into {what} stops the horse ({still:F2} m/s) with its head {closest:F2} m short of it");
                }

                // ---- 6. Through the Old Toll Gate and up the whole main road to the city gate ----
                Vector2[] road = ValleyGenerator.MainRoad;
                Ride(WorldPlaytestChecks.Ground(road[0].x, road[0].y), 0f);
                yield return 0.3f;
                bool stuck = false, throughGate = false;
                float rideStarted = Time.time;
                for (int i = 1; i < road.Length && !stuck; i++)
                {
                    Vector3 waypoint = new Vector3(road[i].x, 0f, road[i].y);
                    Vector3 lastPlace = player.position;
                    float lastCheck = Time.time;
                    while (Flat(waypoint - player.position).magnitude > (i == road.Length - 1 ? 7f : 4f))
                    {
                        float error = Vector3.SignedAngle(player.forward, Flat(waypoint - player.position), Vector3.up);
                        input.SetTouchMove(new Vector2(Mathf.Clamp(error / 25f, -1f, 1f), Mathf.Abs(error) > 60f ? 0.3f : 1f));
                        yield return 0.05f;
                        throughGate |= player.position.z > 96f;
                        if (Time.time - lastCheck < 2f) continue;
                        if (Flat(player.position - lastPlace).magnitude < 2f) { stuck = true; break; }
                        lastPlace = player.position;
                        lastCheck = Time.time;
                    }
                }
                Release();
                yield return 1f;
                check(!stuck && throughGate && player.position.z > 578f && controller.Mounted,
                    $"Mounted, the rider passes the Old Toll Gate and rides the whole main road to the city gate in {Time.time - rideStarted:F0} s (stopped at {player.position})");

                // ---- 7. Dismounting while still: on foot exactly as before, weapons back, the horse stays ----
                check(interactor.Current == (IInteractable)horse && interactor.CurrentPrompt == GameText.Dismount, $"Still in the saddle, Use offers {GameText.Dismount}");
                Vector3 stood = horse.transform.position;
                Press(PlayerButton.Interact);
                yield return 0.3f;
                check(OnFoot() && combat.Armed && !horse.Ridden && horse.transform.parent != player && Vector3.Distance(horse.transform.position, stood) < 0.05f &&
                    Flat(player.position - stood).magnitude > 0.9f && Group(PlayerButton.Attack).alpha > 0f,
                    "Dismounting puts the rider down beside the horse with the on-foot body and eye, the weapons and buttons back, and the horse stays");
                foreach (float w in Mount()) yield return w;
                var walls = new List<GameObject>();
                foreach (Vector3 side in new[] { Vector3.left * 1.2f, Vector3.right * 1.2f, Vector3.back * 1.7f })
                {
                    var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    block.name = "Horse Test Block";
                    block.transform.SetPositionAndRotation(player.TransformPoint(side) + Vector3.up * 1.5f, player.rotation);
                    block.transform.localScale = new Vector3(0.6f, 3f, 0.6f);
                    walls.Add(block);
                }
                Physics.SyncTransforms();
                heard = null;
                Press(PlayerButton.Interact);
                yield return 0.3f;
                check(controller.Mounted && heard == GameText.NoRoomToDismount, "With no room on either side or behind, dismounting is refused and says so");
                foreach (GameObject block in walls) Object.Destroy(block);
                yield return 0.1f;
                Physics.SyncTransforms();

                // ---- 8. A home door and the city gate from the saddle; back in the valley the horse waits at the nearest post ----
                ZonePortal homeDoor = valley.GetComponentsInChildren<ZonePortal>().First(p => p.TargetZone == ZoneNumbers.FirstHome);
                Transform homeReturn = valley.GetComponentsInChildren<Transform>().First(t => t.name == "Hamlet Home 0 Return");
                Ride(homeReturn.position, homeReturn.eulerAngles.y + 180f);
                Physics.SyncTransforms();
                yield return 0.3f;
                bool aimedDoor = ReferenceEquals(interactor.Current, homeDoor);
                Press(PlayerButton.Interact);
                yield return 1.6f;
                check(aimedDoor && zones.CurrentIndex == ZoneNumbers.FirstHome && OnFoot() && !combat.Armed && horse.transform.parent != player,
                    "Using a home door from the saddle arrives inside on foot, weapons away");
                zones.Current.GetComponentsInChildren<ZonePortal>().First(p => p.TargetZone == ZoneNumbers.Valley).Interact(controller.gameObject);
                yield return 1.6f;
                check(zones.CurrentIndex == ZoneNumbers.Valley && Near(spots[3]) && combat.Armed && Flat(player.position - horse.transform.position).magnitude < 25f,
                    $"Out of the home the horse waits at Horse Post 4, {Flat(player.position - horse.transform.position).magnitude:F1} m away");
                foreach (float w in Mount()) yield return w;
                Ride(WorldPlaytestChecks.Ground(0f, 587.2f), 0f);
                Physics.SyncTransforms();
                yield return 0.3f;
                bool aimedGate = interactor.Current is ZonePortal gate && gate.TargetZone == ZoneNumbers.City;
                Press(PlayerButton.Interact);
                yield return 1.6f;
                check(aimedGate && zones.CurrentIndex == ZoneNumbers.City && OnFoot() && !combat.Armed && Group(PlayerButton.Attack).alpha == 0f,
                    "Using the city gate from the saddle arrives in Hollowgate on foot, weapons away");
                zones.Current.GetComponentsInChildren<ZonePortal>().First(p => p.TargetZone == ZoneNumbers.Valley).Interact(controller.gameObject);
                yield return 1.6f;
                check(zones.CurrentIndex == ZoneNumbers.Valley && Near(spots[0]) && combat.Armed && Group(PlayerButton.Attack).alpha > 0f,
                    "Back through the gate the horse waits at Horse Post 1 and the weapons are back");

                // ---- 9. A hit throws the rider off ----
                foreach (float w in Mount()) yield return w;
                heard = null;
                health.ApplyDamage(new DamageInfo(5f, player.position, Vector3.forward, null));
                yield return 0.2f;
                check(OnFoot() && !horse.Ridden && heard == GameText.ThrownFromSaddle && combat.Armed, "A hit in the saddle throws the rider off");

                // ---- 10. Death in the saddle: the rider respawns on foot, the horse at the post nearest the spawn ----
                foreach (float w in Mount()) yield return w;
                health.ApplyDamage(new DamageInfo(100000f, player.position, Vector3.forward, null));
                yield return 0.3f;
                bool fell = lifecycle.IsDead && !controller.Mounted;
                yield return 4.6f;
                check(fell && !lifecycle.IsDead && OnFoot() && Near(NearestSpot(zones.Current.Entry.position)),
                    $"After a death in the saddle the rider respawns on foot and the horse is at {NearestSpot(zones.Current.Entry.position).parent.name}");

                // ---- 11. Saved in the saddle, a soul loads on foot with its horse at the nearest post; without the flag there is none ----
                foreach (float w in Mount()) yield return w;
                bool savedMounted = controller.Mounted;
                keeper.Save();
                keeper.Load(keeper.LatestSoul());
                yield return 0.3f;
                check(savedMounted && OnFoot() && session.HasFlag(RidingHorse.OwnedFlag) && model.activeSelf && Near(NearestSpot(player.position)),
                    "A soul saved in the saddle loads on foot, still owning the horse, which waits at the nearest post");
                SoulRecord original = JsonUtility.FromJson<SoulRecord>(JsonUtility.ToJson(keeper.LatestSoul()));
                SoulRecord unhorsed = JsonUtility.FromJson<SoulRecord>(JsonUtility.ToJson(original));
                unhorsed.Id = "horse_test_" + Guid.NewGuid().ToString("N");
                unhorsed.Flags = (unhorsed.Flags ?? new List<string>()).Where(f => f != RidingHorse.OwnedFlag).ToList();
                SaveStore.Put(unhorsed);
                keeper.Load(SaveStore.Find(unhorsed.Id));
                yield return 0.3f;
                check(!session.HasFlag(RidingHorse.OwnedFlag) && !model.activeSelf && !horseBody.enabled && !horse.CanInteract(controller.gameObject),
                    "A soul without the flag has no horse to ride anywhere");
                SaveStore.Delete(unhorsed.Id);
                SaveStore.Put(original);
                keeper.Load(SaveStore.Find(original.Id));
                yield return 0.3f;

                // ---- 13. Touch mode: Use mounts and dismounts, and the attack button does nothing in the saddle ----
                input.SetTouchMode(true);
                yield return 0.2f;
                foreach (float w in Mount()) yield return w;
                bool mountedByTouch = controller.Mounted;
                float staminaBefore = stamina.Current;
                Press(PlayerButton.Attack);
                yield return 0.4f;
                bool idle = !combat.IsAttacking && stamina.Current >= staminaBefore - 0.01f;
                Press(PlayerButton.Interact);
                yield return 0.3f;
                check(mountedByTouch && idle && OnFoot(), "In touch mode Use mounts and dismounts, and the attack button does nothing in the saddle");
            }
            finally
            {
                Release();
                if (controller.Mounted) horse.Release();
                input.SetTouchMode(touchMode);
                GameMessages.Notification -= Hear;
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(pad);
                settings.editorInputBehaviorInPlayMode = focusBehaviour;
                settings.backgroundBehavior = backgroundBehaviour;
            }
        }

        /// <summary>
        /// A run-up of about seven metres to <paramref name="target"/> on open, dry ground, where the mounted body and the head
        /// would meet nothing but the target on the way.
        /// </summary>
        static bool FindApproach(Collider target, out Vector3 start, out float yaw)
        {
            start = default;
            yaw = 0f;
            Vector3 centre = target.bounds.center;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12f;
                Vector3 near = target.ClosestPoint(centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 30f);
                Vector3 at = WorldPlaytestChecks.Ground(near.x + Mathf.Sin(angle) * 7f, near.z + Mathf.Cos(angle) * 7f);
                if (ValleyGenerator.InLake(at.x, at.z, 2f)) continue;
                Vector3 toward = Flat(near - at).normalized;
                if (toward.sqrMagnitude < 0.5f || Physics.OverlapCapsule(at + Vector3.up * 0.6f, at + Vector3.up * 2.1f, 0.5f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => !c.name.StartsWith("Terrain_") && !c.transform.root.CompareTag("Player"))) continue;
                if (!FirstObstacle(at, toward, 0.5f, 0.6f, 2.1f, target) || !FirstObstacle(at, toward, 0.3f, 0.9f, 1.9f, target)) continue;
                start = at;
                yaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
                return true;
            }
            return false;
        }

        static bool FirstObstacle(Vector3 at, Vector3 toward, float radius, float low, float high, Collider target)
        {
            RaycastHit[] hits = Physics.CapsuleCastAll(at + Vector3.up * low, at + Vector3.up * high, radius, toward, 12f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit first = hits.Where(h => h.distance > 0f && h.normal.y < 0.6f && !h.collider.transform.root.CompareTag("Player"))
                .OrderBy(h => h.distance).FirstOrDefault();
            return first.collider == target;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
