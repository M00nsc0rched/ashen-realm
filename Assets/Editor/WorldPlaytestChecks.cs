using System;
using System.Collections.Generic;
using System.Linq;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// The bigger valley at runtime: the city portal at the new gate both ways, trees that stop the player, instanced scatter
    /// that is drawn near and culled far, and an older save that arrives at the zone's entry with its flags.
    /// </summary>
    internal static class WorldPlaytestChecks
    {
        public static Vector3 Ground(float x, float z) =>
            Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : new Vector3(x, 0f, z);

        static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var input = Object.FindAnyObjectByType<PlayerInputHub>();
            var keeper = SoulKeeper.Instance;
            var scatter = Object.FindAnyObjectByType<ScatterRenderer>(FindObjectsInactive.Include);
            var tollGate = Object.FindAnyObjectByType<TollGate>(FindObjectsInactive.Include);
            if (controller == null || zones == null || input == null || keeper == null || !keeper.HasSoul || scatter == null || tollGate == null)
            { check(false, "The world checks need the player, zones, a soul, the scatter and the toll gate"); yield break; }
            Camera camera = controller.GetComponentsInChildren<Camera>(true).First(c => c.CompareTag("MainCamera"));
            Transform player = controller.transform;

            // ---- The portal at the new city gate travels both ways and brings the player back to that gate ----
            zones.Place(ZoneNumbers.Valley, Vector3.zero, 0f, false);
            yield return 0.2f;
            Zone valley = zones.Current;
            ZonePortal toCity = valley.GetComponentsInChildren<ZonePortal>().FirstOrDefault(p => p.TargetZone == ZoneNumbers.City);
            check(toCity != null && Mathf.Abs(toCity.transform.position.z - 590f) < 2f && string.IsNullOrEmpty(toCity.RequiredFlag),
                "The valley's portal to Hollowgate stands at the new city gate and is always open");
            if (toCity == null) yield break;
            toCity.Interact(controller.gameObject);
            yield return 1.6f;
            check(zones.CurrentIndex == ZoneNumbers.City, $"The new city gate leads to Hollowgate (zone {zones.CurrentIndex})");
            ZonePortal toValley = zones.Current.GetComponentsInChildren<ZonePortal>().FirstOrDefault(p => p.TargetZone == ZoneNumbers.Valley);
            toValley?.Interact(controller.gameObject);
            yield return 1.6f;
            check(zones.CurrentIndex == ZoneNumbers.Valley && Vector3.Distance(player.position, valley.Exit.position) < 1f && player.position.z > 580f,
                $"Coming back from the city puts the player at the new gate ({player.position})");

            // ---- A tree stops the player ----
            CapsuleCollider tree = null;
            Vector3 approach = default;
            foreach (CapsuleCollider capsule in valley.GetComponentsInChildren<CapsuleCollider>().Where(c => c.transform.name.StartsWith("Tree Colliders")))
            {
                Vector3 foot = capsule.transform.TransformPoint(capsule.center) - Vector3.up * capsule.height * 0.5f;
                if (foot.z < 150f) continue;
                foreach (float angle in new[] { 180f, 0f, 90f, 270f })
                {
                    Vector3 from = foot + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 2.6f;
                    from = Ground(from.x, from.z) + Vector3.up * 0.05f;
                    if (Physics.OverlapCapsule(from + Vector3.up * 0.45f, from + Vector3.up * 1.5f, 0.4f, ~0, QueryTriggerInteraction.Ignore)
                        .Any(c => !c.transform.IsChildOf(player))) continue;
                    Vector3 to = new Vector3(foot.x - from.x, 0f, foot.z - from.z);
                    if (!Physics.SphereCast(from + Vector3.up * 1f, 0.3f, to.normalized, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) || hit.collider != capsule)
                        continue;
                    tree = capsule;
                    approach = from;
                    break;
                }
                if (tree != null) break;
            }
            check(tree != null, "A tree in the north land can be walked up to");
            if (tree != null)
            {
                Vector3 axis = tree.transform.TransformPoint(tree.center);
                Vector3 to = new Vector3(axis.x - approach.x, 0f, axis.z - approach.z);
                controller.Teleport(approach, Quaternion.LookRotation(to).eulerAngles.y, 0f);
                Physics.SyncTransforms();
                yield return 0.2f;
                input.SetTouchMove(Vector2.up);
                yield return 1.5f;
                input.SetTouchMove(Vector2.zero);
                Vector3 now = player.position;
                float gap = new Vector2(now.x - axis.x, now.z - axis.z).magnitude;
                float advanced = Vector3.Dot(new Vector3(now.x - approach.x, 0f, now.z - approach.z), to.normalized);
                check(gap > tree.radius + 0.25f && advanced < to.magnitude, $"Walking into a tree stops at its trunk ({gap:F2} m from its axis, radius {tree.radius:F2})");
            }

            // ---- The scatter is drawn near the player and culled far away, as whole cells ----
            ScatterData data = scatter.Data;
            bool[] grassKinds = data.Kinds.Select(k => k.Name.StartsWith("Grass")).ToArray();
            foreach (Vector2 spot in new[] { new Vector2(0f, -60f), new Vector2(-4f, 400f) })
            {
                Vector3 feet = Ground(spot.x, spot.y) + Vector3.up * 0.1f;
                controller.Teleport(feet, 0f, 0f);
                Physics.SyncTransforms();
                yield return 0.2f;
                camera.Render();
                Vector3 eye = scatter.LastCameraPosition;
                int nearDrawn = 0, farDrawn = 0, nearGroups = 0, culledFar = 0;
                for (int g = 0; g < data.Groups.Length; g++)
                {
                    var group = data.Groups[g];
                    Bounds b = group.Bounds;
                    float distance = new Vector2(Mathf.Clamp(eye.x, b.min.x, b.max.x) - eye.x, Mathf.Clamp(eye.z, b.min.z, b.max.z) - eye.z).magnitude;
                    // A cell just past its distance stays listed until the camera has moved enough to rebuild.
                    float cull = data.Kinds[group.Kind].CullDistance + ScatterRenderer.RebuildMove;
                    bool drawn = scatter.GroupDrawn(g);
                    if (drawn && distance >= cull) farDrawn++;
                    if (!drawn && distance > cull) culledFar++;
                    if (grassKinds[group.Kind] && distance < 1f) { nearGroups++; if (drawn) nearDrawn++; }
                }
                int grassDrawn = Enumerable.Range(0, data.Kinds.Length).Where(k => grassKinds[k]).Sum(k => scatter.Drawn(k));
                check(scatter.LastDrawFrame == Time.frameCount && scatter.DrawCalls > 0 && grassDrawn > 0 && nearGroups > 0 && nearDrawn == nearGroups,
                    $"At {spot} the instanced scatter is drawn around the player ({grassDrawn} grass, {scatter.DrawCalls} instanced calls)");
                check(farDrawn == 0 && culledFar > 0, $"At {spot} every cell beyond its kind's distance is culled ({culledFar} culled, {farDrawn} drawn too far)");

                // Turning just under the rebuild angle keeps the lists, and they must already hold every cell the turn brings into view.
                scatter.Refresh(camera);
                Quaternion facing = camera.transform.rotation;
                var before = new Plane[6];
                GeometryUtility.CalculateFrustumPlanes(camera, before);
                int refreshes = scatter.Refreshes;
                camera.transform.rotation = Quaternion.AngleAxis(ScatterRenderer.RebuildTurn - 1f, Vector3.up) * facing;
                camera.Render();
                var after = new Plane[6];
                GeometryUtility.CalculateFrustumPlanes(camera, after);
                Vector3 turnedEye = camera.transform.position;
                int entering = 0, missing = 0;
                for (int g = 0; g < data.Groups.Length; g++)
                {
                    Bounds b = data.Groups[g].Bounds;
                    float distance = new Vector2(Mathf.Clamp(turnedEye.x, b.min.x, b.max.x) - turnedEye.x, Mathf.Clamp(turnedEye.z, b.min.z, b.max.z) - turnedEye.z).magnitude;
                    if (distance >= data.Kinds[data.Groups[g].Kind].CullDistance || !GeometryUtility.TestPlanesAABB(after, b)) continue;
                    if (!GeometryUtility.TestPlanesAABB(before, b)) entering++;
                    if (!scatter.GroupDrawn(g)) missing++;
                }
                camera.transform.rotation = facing;
                check(scatter.Refreshes == refreshes && entering > 0 && missing == 0,
                    $"At {spot} a {ScatterRenderer.RebuildTurn - 1f:F0} degree turn without a rebuild leaves no visible cell undrawn ({entering} came into view, {missing} missing)");
            }

            // ---- The jetty can be walked to its end; the lake's bank stops the player short of the water ----
            Vector2 jetty = ValleyGenerator.JettyAxis;
            float jettyYaw = Mathf.Atan2(jetty.x, jetty.y) * Mathf.Rad2Deg;
            Vector3 jettyFoot = Ground(ValleyGenerator.JettyStart.x - jetty.x * 2f, ValleyGenerator.JettyStart.y - jetty.y * 2f) + Vector3.up * 0.1f;
            controller.Teleport(jettyFoot, jettyYaw, 0f);
            Physics.SyncTransforms();
            yield return 0.2f;
            input.SetTouchMove(Vector2.up);
            yield return 9f;
            input.SetTouchMove(Vector2.zero);
            yield return 0.2f;
            Vector2 walked = new Vector2(player.position.x, player.position.z) - ValleyGenerator.JettyStart;
            float along = Vector2.Dot(walked, jetty), aside = Mathf.Abs(Vector2.Dot(walked, new Vector2(-jetty.y, jetty.x)));
            // What carries the feet: a short ray down from inside the capsule, which ignores the player's own collider.
            string underfoot = Physics.RaycastAll(player.position + Vector3.up * 0.3f, Vector3.down, 0.8f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !h.collider.transform.IsChildOf(player)).Select(h => h.collider.name).FirstOrDefault() ?? "nothing";
            check(along > ValleyGenerator.JettyLength - 2f && aside < ValleyGenerator.JettyHalfWidth && underfoot == "Jetty Deck",
                $"The jetty can be walked to its end over the water ({along:F1} m along, {aside:F2} m aside, standing on {underfoot})");
            input.SetTouchMove(new Vector2(1f, 0f));
            yield return 2f;
            input.SetTouchMove(Vector2.zero);
            yield return 0.2f;
            walked = new Vector2(player.position.x, player.position.z) - ValleyGenerator.JettyStart;
            check(Mathf.Abs(Vector2.Dot(walked, new Vector2(-jetty.y, jetty.x))) < ValleyGenerator.JettyHalfWidth, "The jetty's rail stops the player stepping off it");
            foreach (Vector2 bank in new[] { new Vector2(185f, 268f), new Vector2(246f, 360f), new Vector2(185f, 452f) })
            {
                Vector2 toCentre = new Vector2(185f, 360f) - bank;
                controller.Teleport(Ground(bank.x, bank.y) + Vector3.up * 0.1f, Mathf.Atan2(toCentre.x, toCentre.y) * Mathf.Rad2Deg, 0f);
                Physics.SyncTransforms();
                yield return 0.2f;
                input.SetTouchMove(Vector2.up);
                yield return 2.5f;
                input.SetTouchMove(Vector2.zero);
                check(!ValleyGenerator.InLake(player.position.x, player.position.z, -1.5f), $"Walking into Tükörtó from {bank} stops on the bank ({player.position})");
            }

            // ---- The north land's discoveries: aimed at, prompted, told, and still there and told after a save and a load ----
            var interactor = controller.GetComponent<PlayerInteractor>();
            LoreInspectable[] north = valley.GetComponentsInChildren<LoreInspectable>().Where(d => d.name.StartsWith("Discovery ") && d.transform.position.z > 90f).ToArray();
            check(north.Length >= 11, $"The north land has its discoveries ({north.Length})");
            string heard = null;
            void Hear(string value) => heard = value;
            GameMessages.Notification += Hear;
            var expected = new Dictionary<string, string>();
            foreach (var site in north)
            {
                if (!ScenicAccess.TryApproach(site, out Vector3 feet, out Quaternion look)) { check(false, $"{site.name}: no standing place"); continue; }
                Vector3 angles = look.eulerAngles;
                controller.Teleport(feet, angles.y, angles.x > 180f ? angles.x - 360f : angles.x);
                yield return 0.2f;
                heard = null;
                bool aimed = ReferenceEquals(interactor.Current, site) && interactor.CurrentPrompt == GameText.Inspect;
                site.Interact(controller.gameObject);
                expected[site.name] = heard;
                check(aimed && !string.IsNullOrEmpty(heard), $"{site.name}: the crosshair finds it, shows {GameText.Inspect} and it tells its story");
            }
            keeper.Save();
            keeper.Load(keeper.LatestSoul());
            yield return 1.7f;
            foreach (var site in zones.Current.GetComponentsInChildren<LoreInspectable>().Where(d => expected.ContainsKey(d.name)))
            {
                ScenicAccess.TryApproach(site, out Vector3 feet, out Quaternion look);
                Vector3 angles = look.eulerAngles;
                controller.Teleport(feet, angles.y, angles.x > 180f ? angles.x - 360f : angles.x);
                yield return 0.2f;
                heard = null;
                bool aimed = interactor.Current is LoreInspectable current && current.name == site.name;
                site.Interact(controller.gameObject);
                check(aimed && heard == expected[site.name] && !string.IsNullOrEmpty(heard), $"{site.name}: after a save and a load it still tells the same story");
                expected.Remove(site.name);
            }
            check(expected.Count == 0, $"Every north discovery is still there after loading ({expected.Count} missing)");
            GameMessages.Notification -= Hear;

            // ---- An older save keeps its story but not its spot: it arrives at the valley's entry ----
            keeper.Save();
            SoulRecord original = Copy(keeper.LatestSoul());
            SoulRecord old = Copy(original);
            old.Id = "world_test_" + Guid.NewGuid().ToString("N");
            old.WorldVersion = 5;
            old.Zone = ZoneNumbers.Valley;
            old.X = 40f; old.Y = 30f; old.Z = 300f;
            old.Flags = new List<string>(old.Flags ?? new List<string>()) { StoryDirector.DeadFlag("knight") };
            SaveStore.Put(old);
            keeper.Load(SaveStore.Find(old.Id));
            yield return 0.3f;
            check(SaveStore.WorldVersion == 6 && zones.CurrentIndex == ZoneNumbers.Valley && Vector3.Distance(player.position, zones.Current.Entry.position) < 1.5f &&
                GameSession.Instance.HasFlag(StoryDirector.DeadFlag("knight")) && tollGate.IsOpen,
                $"A version 5 save arrives at the valley's entry with its flags, and the slain knight's gate stands open ({player.position})");
            SaveStore.Delete(old.Id);
            SaveStore.Put(original);
            keeper.Load(SaveStore.Find(original.Id));
            yield return 0.3f;
        }
    }
}
