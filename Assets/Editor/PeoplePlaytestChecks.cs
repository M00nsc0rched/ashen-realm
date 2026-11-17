using System;
using System.Collections.Generic;
using System.Linq;
using AshenRealm.AI;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.UI;
using AshenRealm.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// The people of the valley and the city at runtime: every one of them can be aimed at from where a player can stand, the
    /// new speakers show the talk prompt and answer in each of the three moods, and a person far away is hidden and still
    /// for the frame, yet stays solid.
    /// </summary>
    internal static class PeoplePlaytestChecks
    {
        static readonly string[] NewSpeakers = { "stablemaster", "farmer", "fisher", "watchman", "woodcutter", "gateguard", "citylamplighter" };

        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var input = Object.FindAnyObjectByType<PlayerInputHub>();
            var dialogue = Object.FindAnyObjectByType<DialoguePanel>();
            var session = GameSession.Instance;
            if (controller == null || zones == null || input == null || dialogue == null || session == null)
            { check(false, "The people checks need the player, zones, input, the dialogue and a session"); yield break; }
            var interactor = controller.GetComponent<PlayerInteractor>();
            var body = new SerializedObject(dialogue).FindProperty("bodyText").objectReferenceValue as Text;
            int honor = session.Honor;

            foreach (int zoneIndex in new[] { ZoneNumbers.Valley, ZoneNumbers.City })
            {
                zones.Place(zoneIndex, Vector3.zero, 0f, false);
                yield return 0.2f;
                foreach (NpcActor npc in zones.Current.GetComponentsInChildren<NpcActor>())
                {
                    if (!TryApproach(npc.GetComponent<Collider>(), out Vector3 feet, out Quaternion look))
                    { check(false, $"{npc.NpcId}: no place to stand that can target them"); continue; }
                    Vector3 angles = look.eulerAngles;
                    controller.Teleport(feet, angles.y, angles.x > 180f ? angles.x - 360f : angles.x);
                    Physics.SyncTransforms();
                    yield return 0.2f;
                    bool aimed = ReferenceEquals(interactor.Current, npc) && interactor.CurrentPrompt == GameText.Talk;
                    check(aimed, $"{npc.NpcId}: the crosshair finds them from a place a player can stand and shows {GameText.Talk}");
                    if (!aimed || !NewSpeakers.Contains(npc.NpcId)) continue;

                    NpcDefinition words = StoryText.Npc(npc.NpcId);
                    foreach (var (mood, value, line) in new[] { (Alignment.Marked, -60, words.Marked), (Alignment.Mortal, 0, words.Fallback), (Alignment.Hallowed, 60, words.Hallowed) })
                    {
                        session.AddHonor(value - session.Honor);
                        input.SetTouchButton(PlayerButton.Interact, true);
                        input.SetTouchButton(PlayerButton.Interact, false);
                        yield return 0.15f;
                        bool told = dialogue.IsOpen && body != null && body.text.StartsWith(words.Intro) && body.text.EndsWith(line);
                        check(session.Alignment == mood && told, $"{words.Name}: talking while {mood} tells their {mood} line");
                        // Leave: the last answer, after Hilda's offer of a horse while there is none.
                        dialogue.Choose(words.Service == NpcService.Stable && !session.HasFlag(RidingHorse.OwnedFlag) ? 1 : 0);
                        yield return 0.15f;
                    }
                }
            }
            session.AddHonor(honor - session.Honor);
            check(!dialogue.IsOpen && Time.timeScale == 1f, "Every conversation with the new people can be left");

            // ---- Far away a person is hidden and still, near they come back, and hidden they still stop the feet ----
            zones.Place(ZoneNumbers.Valley, Vector3.zero, 0f, false);
            yield return 0.2f;
            NpcActor hilda = zones.Current.GetComponentsInChildren<NpcActor>().FirstOrDefault(n => n.NpcId == "stablemaster");
            if (hilda == null) { check(false, "Patkós Hilda stands by the stable"); yield break; }
            var idle = hilda.GetComponent<NpcIdleAnimator>();
            Renderer[] parts = hilda.GetComponentsInChildren<Renderer>(true);
            Collider solid = hilda.GetComponent<Collider>();
            Transform figure = hilda.transform.Find("Figure");
            controller.Teleport(WorldPlaytestChecks.Ground(0f, 300f) + Vector3.up * 0.1f, 0f, 0f);
            Physics.SyncTransforms();
            yield return 0.2f;
            Quaternion pose = figure.localRotation;
            yield return 0.4f;
            float far = Vector3.Distance(Camera.main.transform.position, hilda.transform.position);
            Vector3 beside = hilda.transform.position + Vector3.left * 2f;
            bool blocks = Physics.CapsuleCast(beside + Vector3.up * 0.4f, beside + Vector3.up * 1.5f, 0.35f, Vector3.right, out RaycastHit hit, 3f, ~0,
                QueryTriggerInteraction.Ignore) && hit.collider == solid;
            check(far > NpcIdleAnimator.HideDistance && idle.Hidden && parts.All(p => !p.enabled) && figure.localRotation == pose,
                $"{far:F0} m away Hilda is neither drawn nor animated");
            check(solid.enabled && blocks, "Hidden, Hilda still stands in the way of a player-sized capsule");
            controller.Teleport(WorldPlaytestChecks.Ground(18f, 114f) + Vector3.up * 0.1f, 90f, 0f);
            Physics.SyncTransforms();
            yield return 0.2f;
            pose = figure.localRotation;
            yield return 0.4f;
            check(!idle.Hidden && parts.All(p => p.enabled) && figure.localRotation != pose, "Near again, Hilda is drawn and breathing");
        }

        /// <summary>A standing place 1.4 to 2.4 m away, on floor at about their height, from which the interaction probe reaches them.</summary>
        internal static bool TryApproach(Collider target, out Vector3 feet, out Quaternion look)
        {
            feet = default;
            look = Quaternion.identity;
            if (target == null) return false;
            int mask = ~(1 << PlayerInteractor.SeeThroughLayer);
            float height = target.transform.position.y;
            foreach (float radius in new[] { 1.4f, 1.9f, 2.4f })
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Vector3 at = target.bounds.center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                if (!Physics.Raycast(new Vector3(at.x, height + 1.2f, at.z), Vector3.down, out RaycastHit ground, 2.4f, mask, QueryTriggerInteraction.Ignore) ||
                    ground.normal.y < 0.7f || Mathf.Abs(ground.point.y - height) > 1f) continue;
                at = ground.point + Vector3.up * 0.05f;
                if (Physics.OverlapCapsule(at + Vector3.up * 0.4f, at + Vector3.up * 1.5f, 0.35f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => !c.transform.root.CompareTag("Player"))) continue;
                Vector3 eye = at + Vector3.up * 1.62f, direction = target.bounds.center - eye;
                if (!Physics.SphereCast(eye, 0.2f, direction.normalized, out RaycastHit hit, 3f, mask, QueryTriggerInteraction.Collide) || hit.collider != target) continue;
                feet = at;
                look = Quaternion.LookRotation(direction);
                return true;
            }
            return false;
        }
    }
}
