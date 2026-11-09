using System.Collections.Generic;
using System.Linq;
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
using UnityEngine;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    public static partial class PlaytestRunner
    {
        // Own test soul: the bounty needs fifteen living common foes, independently of the earlier combat scenario.
        static IEnumerable<float> RunCityNpcScenario()
        {
            if (!Check(GameSettings.Prefix.Contains("Playtest"), "City services use isolated playtest saves")) yield break;
            SoulKeeper.Instance.ReturnToMenu();
            yield return 2f;
            var menu = Object.FindAnyObjectByType<MainMenu>();
            menu.NewSoul();
            var carousel = Object.FindAnyObjectByType<ClassCarousel>();
            carousel.Jump(0);
            carousel.Confirm();
            carousel.Confirm();
            yield return 0.4f;

            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var session = GameSession.Instance;
            var keeper = SoulKeeper.Instance;
            var health = player.GetComponent<Health>();
            var abilities = player.GetComponent<PlayerAbilities>();
            var ledger = CityActivityLedger.Instance;
            if (!Check(ledger != null && ledger.transform.parent == null, "The city contract ledger stays active outside every zone")) yield break;

            zones.Place(ZoneNumbers.City, Vector3.zero, 0f, false);
            yield return 0.15f;
            CityActivity[] stations = zones.Current.GetComponentsInChildren<CityActivity>();
            if (!Check(stations.Length == 5 && stations.Select(s => s.ActivityId).Distinct().Count() == 5,
                "Hollowgate has five distinct working service targets")) yield break;
            foreach (CityActivity station in stations)
                foreach (float wait in AimCityNpcTarget(station, player)) yield return wait;

            CityActivity Station(CityActivityKind kind) => Object.FindObjectsByType<CityActivity>().First(s => s.Kind == kind);

            // Healthy visitors retain both their money and the opportunity to use the well later.
            session.AddGold(200 - session.Gold);
            health.SetCurrent(health.Max);
            abilities.SetFocus(abilities.MaxFocus);
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.Well), player)) yield return wait;
            Check(session.Gold == 200 && !session.HasFlag(CityActivityLedger.ServiceFlag(CityActivityKind.Well, session.Descents)),
                "A healthy visitor is not charged and does not consume the well's use");

            foreach (CityActivityKind kind in new[] { CityActivityKind.Well, CityActivityKind.MarketSupplies,
                CityActivityKind.MemorialShrine, CityActivityKind.LanternRest })
            {
                session.AddGold(-session.Gold);
                health.SetCurrent(20f);
                abilities.SetFocus(0f);
                int potionsBefore = session.Potions, honorBefore = session.Honor;
                foreach (float wait in UseCityNpcTarget(Station(kind), player)) yield return wait;
                Check(session.Gold == 0 && Mathf.Approximately(health.Current, 20f) && session.Potions == potionsBefore && session.Honor == honorBefore &&
                    !session.HasFlag(CityActivityLedger.ServiceFlag(kind, session.Descents)), $"{kind}: insufficient gold grants nothing and leaves the service available");
            }

            session.AddGold(200);
            health.SetCurrent(30f);
            abilities.SetFocus(0f);
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.Well), player)) yield return wait;
            Check(session.Gold == 197 && Mathf.Approximately(health.Current, 55f) && abilities.Focus >= 3f && abilities.Focus < 3.6f,
                "The targeted well trades three gold for twenty-five health and three focus");
            health.SetCurrent(20f);
            abilities.SetFocus(0f);
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.Well), player)) yield return wait;
            Check(session.Gold == 197 && Mathf.Approximately(health.Current, 20f), "The well cannot be bought twice on the same descent");

            int startingPotions = session.Potions;
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.MarketSupplies), player)) yield return wait;
            Check(session.Gold == 167 && session.Potions == startingPotions + 2, "The market supplies two real healing potions for thirty gold");
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.MarketSupplies), player)) yield return wait;
            Check(session.Gold == 167 && session.Potions == startingPotions + 2, "The market package is not duplicated by another interaction");
            int startingHonor = session.Honor;
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.MemorialShrine), player)) yield return wait;
            Check(session.Gold == 157 && session.Honor == startingHonor + 8, "Lighting a memorial candle pays its cost and restores honor");
            health.SetCurrent(20f);
            abilities.SetFocus(0f);
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.LanternRest), player)) yield return wait;
            Check(session.Gold == 149 && Mathf.Approximately(health.Current, 35f) && Mathf.Approximately(abilities.Focus, abilities.MaxFocus),
                "Rest under the lantern tree restores fifteen health and all focus for eight gold");
            int oldDescent = session.Descents;
            SoulRecord servicesSave = keeper.LatestSoul();
            Check(new[] { CityActivityKind.Well, CityActivityKind.MarketSupplies, CityActivityKind.MemorialShrine, CityActivityKind.LanternRest }
                .All(kind => servicesSave.Flags.Contains(CityActivityLedger.ServiceFlag(kind, oldDescent))), "Service limits are written into the saved soul");

            keeper.Resume(servicesSave);
            yield return 2f;
            zones = Object.FindAnyObjectByType<ZoneManager>();
            player = Object.FindAnyObjectByType<FirstPersonController>();
            session = GameSession.Instance;
            keeper = SoulKeeper.Instance;
            ledger = CityActivityLedger.Instance;
            health = player.GetComponent<Health>();
            abilities = player.GetComponent<PlayerAbilities>();
            int restoredGold = session.Gold;
            health.SetCurrent(20f);
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.Well), player)) yield return wait;
            Check(session.Gold == restoredGold && Mathf.Approximately(health.Current, 20f), "Reloading does not reset a consumed city service");

            // Use the actual travel event which increments Descents, rather than changing the counter in the test.
            zones.Travel(ZoneNumbers.Tavern, false);
            yield return 1.3f;
            zones.Travel(ZoneNumbers.FirstDepth, false);
            yield return 1.3f;
            Check(session.Descents == oldDescent + 1, "Taking the tavern stair begins a new service allowance");
            zones.Place(ZoneNumbers.City, Vector3.zero, 0f, false);
            health.SetCurrent(20f);
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.Well), player)) yield return wait;
            Check(session.Gold == restoredGold - 3 && Mathf.Approximately(health.Current, 45f) &&
                session.HasFlag(CityActivityLedger.ServiceFlag(CityActivityKind.Well, session.Descents)), "A new descent makes the well available again without erasing the old allowance");

            var merchantMeshes = new HashSet<Mesh>();
            foreach (int zoneIndex in new[] { ZoneNumbers.City, ZoneNumbers.FirstDepth, ZoneNumbers.FirstDepth + 1,
                ZoneNumbers.FirstDepth + 2, ZoneNumbers.FirstDepth + 3, ZoneNumbers.FirstDepth + 4 })
            {
                zones.Place(zoneIndex, Vector3.zero, 0f, false);
                yield return 0.1f;
                NpcActor merchant = zones.Current.GetComponentsInChildren<NpcActor>().FirstOrDefault(n => n.NpcId == "merchant");
                if (!Check(merchant != null, $"Zone {zoneIndex}: a visible merchant waits by the route")) continue;
                var figure = merchant.transform.Find("Figure");
                var mesh = figure != null ? figure.GetComponent<MeshFilter>()?.sharedMesh : null;
                Check(mesh != null && mesh.vertexCount > 150 && merchantMeshes.Add(mesh), $"Zone {zoneIndex}: the merchant has a distinct dressed mesh");
                var idle = merchant.GetComponent<NpcIdleAnimator>();
                Transform arm = figure != null ? figure.Find("Counting Shoulder") : null;
                Check(idle != null && idle.Working && idle.Routine == NpcRoutine.Trading && arm != null,
                    $"Zone {zoneIndex}: the merchant has a visible working arm");
                foreach (float wait in AimCityNpcTarget(merchant, player)) yield return wait;
                if (arm == null) continue;
                Quaternion firstPose = arm.localRotation;
                Vector3 anchor = merchant.GetComponent<Collider>().bounds.center;
                yield return 0.65f;
                Quaternion middlePose = arm.localRotation;
                yield return 0.65f;
                Check(Quaternion.Angle(firstPose, middlePose) > 0.2f || Quaternion.Angle(middlePose, arm.localRotation) > 0.2f,
                    $"Zone {zoneIndex}: the merchant counts coins instead of holding a frozen pose");
                Check(Vector3.Distance(anchor, merchant.GetComponent<Collider>().bounds.center) < 0.005f,
                    $"Zone {zoneIndex}: work animation leaves the interaction collider anchored");
                if (zoneIndex >= ZoneNumbers.FirstDepth)
                {
                    Transform camp = zones.Current.transform.Find("Merchant Camp " + zones.Current.name.Replace("Zone ", ""));
                    Check(camp != null && camp.GetComponentsInChildren<MeshFilter>().Any(f => f.sharedMesh != null) && camp.GetComponentsInChildren<Collider>().Length == 0,
                        $"Zone {zoneIndex}: a furnished merchant workshop leaves the dungeon route clear");
                }
            }

            var encounterBodies = new HashSet<Mesh>();
            foreach (EncounterSite site in Object.FindObjectsByType<EncounterSite>(FindObjectsInactive.Include))
            {
                zones.Place(CityNpcZoneIndex(zones, site.GetComponentInParent<Zone>(true)), Vector3.zero, 0f, false);
                session.AddHonor(30 - session.Honor);
                yield return 0.1f;
                Transform clear = site.transform.Find("Clear"), warped = site.transform.Find("Warped");
                var action = clear != null ? clear.GetComponent<EncounterIdleAnimator>() : null;
                var body = clear != null ? clear.Find("Body") : null;
                var visual = body != null ? body.GetComponentInChildren<MeshFilter>()?.sharedMesh : null;
                Check(action != null && action.Working && visual != null && visual.vertexCount > 80 && encounterBodies.Add(visual),
                    site.EncounterId + ": the encounter has a distinct furnished figure and working animation");
                foreach (float wait in AimCityNpcTarget(site, player)) yield return wait;
                if (action == null) continue;
                Transform work = body.Find("Work Pivot");
                Vector3 anchored = site.GetComponent<Collider>().bounds.center;
                Quaternion poseOne = work.localRotation;
                Vector3 scaleOne = work.localScale;
                yield return 0.65f;
                Quaternion poseTwo = work.localRotation;
                Vector3 scaleTwo = work.localScale;
                yield return 0.65f;
                Check(Quaternion.Angle(poseOne, poseTwo) > 0.2f || Quaternion.Angle(poseTwo, work.localRotation) > 0.2f ||
                    Vector3.Distance(scaleOne, scaleTwo) > 0.005f || Vector3.Distance(scaleTwo, work.localScale) > 0.005f,
                    site.EncounterId + ": the visible person, animal or flame is animated");
                Check(Vector3.Distance(anchored, site.GetComponent<Collider>().bounds.center) < 0.005f,
                    site.EncounterId + ": animation leaves its interaction approach stable");
                if (site.EncounterId == "cage")
                    Check(action.Motion == EncounterMotion.Wolf && visual.bounds.max.y < 1f && body.Find("Tail Pivot") != null,
                        "The caged creature is an injured quadruped with an animated head and tail");
                if (warped == null) continue;
                session.AddHonor(-20 - session.Honor);
                yield return 0.1f;
                Check(warped.gameObject.activeInHierarchy && !clear.gameObject.activeSelf && warped.GetComponent<EncounterIdleAnimator>()?.Working == true &&
                    warped.GetComponentsInChildren<MeshFilter>().Any(m => m.sharedMesh != null && m.sharedMesh != visual),
                    site.EncounterId + ": dishonor reveals an individually authored moving figure, not a shared blank silhouette");
            }
            session.AddHonor(30 - session.Honor);
            EncounterSite clearedSite = Object.FindObjectsByType<EncounterSite>(FindObjectsInactive.Include).First(s => s.EncounterId == "well");
            clearedSite.Resolve();
            Check(!clearedSite.GetComponent<Collider>().enabled && session.HasFlag(clearedSite.SaveKey),
                "A resolved encounter saves its departure and removes its invisible collision");

            zones.Place(ZoneNumbers.City, Vector3.zero, 0f, false);
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.BountyBoard), player)) yield return wait;
            Check(ledger.ContractAccepted && ledger.ContractKills == 0 && ledger.CurrentContract == 0, "The notice board accepts the first watch contract");
            int boardGold = session.Gold, boardSouls = keeper.Souls;
            foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.BountyBoard), player)) yield return wait;
            Check(session.Gold == boardGold && keeper.Souls == boardSouls, "An unfinished contract cannot be claimed");

            // A real EnemyAI death in the city must not satisfy a dungeon objective.
            EnemyAI sourceFoe = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include)
                .First(e => Catalog.Enemy(e.EnemyId).Rank == EnemyRank.Common);
            GameObject cityFoe = Object.Instantiate(sourceFoe.gameObject, zones.Current.transform);
            cityFoe.transform.position = zones.Current.Entry.position + Vector3.right * 20f;
            cityFoe.SetActive(true);
            SceneWiring.Set(cityFoe.GetComponent<EnemyAI>(), "saveKey", "playtest_city_noncontract");
            yield return 0.05f;
            cityFoe.GetComponent<Health>().ApplyDamage(new DamageInfo(100000f, cityFoe.transform.position, Vector3.forward, player.gameObject));
            yield return 0.1f;
            Check(ledger.ContractKills == 0, "A city enemy death does not count toward a dungeon contract");
            Object.Destroy(cityFoe);

            for (int contract = 0; contract < CityActivityLedger.ContractCount; contract++)
            {
                zones.Place(ZoneNumbers.City, Vector3.zero, 0f, false);
                if (!ledger.ContractAccepted)
                    foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.BountyBoard), player)) yield return wait;
                Check(ledger.CurrentContract == contract && ledger.ContractAccepted, $"Watch contract {contract + 1} can be accepted in order");
                for (int slain = 0; slain < CityActivityLedger.Goal(contract); slain++)
                {
                    EnemyAI foe = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include).FirstOrDefault(e => e.Health.IsAlive &&
                        Catalog.Enemy(e.EnemyId).Rank == EnemyRank.Common && !session.HasFlag(e.SaveKey));
                    if (!Check(foe != null, $"Watch contract {contract + 1}: another distinct living enemy is available")) yield break;
                    Zone foeZone = foe.GetComponentInParent<Zone>(true);
                    zones.Place(CityNpcZoneIndex(zones, foeZone), Vector3.zero, 0f, false);
                    yield return 0.05f;
                    string key = foe.SaveKey;
                    foe.Health.ApplyDamage(new DamageInfo(100000f, foe.transform.position, Vector3.forward, player.gameObject));
                    yield return 0.1f;
                    Check(!foe.Health.IsAlive && ledger.ContractKills == slain + 1 && session.HasFlag(key),
                        $"Watch contract {contract + 1}: a real dungeon death advances progress and saves the foe");
                    foe.Health.ApplyDamage(new DamageInfo(100000f, foe.transform.position, Vector3.forward, player.gameObject));
                    Check(ledger.ContractKills == slain + 1, "Damaging an already slain foe cannot count it twice");
                    if (contract != 0 || slain != 0) continue;
                    SoulRecord progressSave = keeper.LatestSoul();
                    Check(progressSave.Flags.Contains(CityActivityLedger.ContractFlag(0, "kill_" + key)) && progressSave.Flags.Contains(key),
                        "Ordinary kills autosave contract progress together with the cleared enemy");
                    keeper.Resume(progressSave);
                    yield return 2f;
                    zones = Object.FindAnyObjectByType<ZoneManager>();
                    player = Object.FindAnyObjectByType<FirstPersonController>();
                    session = GameSession.Instance;
                    keeper = SoulKeeper.Instance;
                    ledger = CityActivityLedger.Instance;
                    var savedFoe = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include).FirstOrDefault(e => e.SaveKey == key);
                    Check(ledger.ContractAccepted && ledger.ContractKills == 1 && savedFoe != null && !savedFoe.gameObject.activeSelf,
                        "A saved contract resumes at one kill and its cleared foe stays gone");
                }
                zones.Place(ZoneNumbers.City, Vector3.zero, 0f, false);
                int goldBeforeClaim = session.Gold, soulsBeforeClaim = keeper.Souls;
                foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.BountyBoard), player)) yield return wait;
                Check(ledger.CurrentContract == contract + 1 && session.Gold == goldBeforeClaim + CityActivityLedger.GoldReward(contract) &&
                    keeper.Souls == soulsBeforeClaim + CityActivityLedger.SoulReward(contract), $"Watch contract {contract + 1} pays its actual gold and Souls reward");
                int paidGold = session.Gold, paidSouls = keeper.Souls;
                foreach (float wait in UseCityNpcTarget(Station(CityActivityKind.BountyBoard), player)) yield return wait;
                Check(session.Gold == paidGold && keeper.Souls == paidSouls, $"Watch contract {contract + 1} pays only once");
                if (contract != 0) continue;
                keeper.Resume(keeper.LatestSoul());
                yield return 2f;
                zones = Object.FindAnyObjectByType<ZoneManager>();
                player = Object.FindAnyObjectByType<FirstPersonController>();
                session = GameSession.Instance;
                keeper = SoulKeeper.Instance;
                ledger = CityActivityLedger.Instance;
                Check(session.HasFlag(CityActivityLedger.ContractFlag(0, "paid")) && ledger.CurrentContract == 1 && session.Gold == paidGold && keeper.Souls == paidSouls,
                    "The paid contract and exact reward survive a reload");
            }
            Check(ledger.CurrentContract == CityActivityLedger.ContractCount, "All three watch contracts can be completed");
        }

        static IEnumerable<float> AimCityNpcTarget(Component target, FirstPersonController player)
        {
            if (!Check(TryCityNpcApproach(target.GetComponent<Collider>(), out Vector3 feet, out Quaternion look),
                target.name + ": a free floor position can target it")) yield break;
            Vector3 angles = look.eulerAngles;
            player.Teleport(feet, angles.y, angles.x > 180f ? angles.x - 360f : angles.x);
            Physics.SyncTransforms();
            yield return 0.15f;
            Check(ReferenceEquals(player.GetComponent<PlayerInteractor>().Current, target), target.name + ": the real interaction probe targets it");
        }

        static int CityNpcZoneIndex(ZoneManager zones, Zone zone)
        {
            var array = new SerializedObject(zones).FindProperty("zones");
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == zone) return i;
            throw new System.InvalidOperationException("City/NPC test could not identify a zone.");
        }

        static IEnumerable<float> UseCityNpcTarget(CityActivity target, FirstPersonController player)
        {
            foreach (float wait in AimCityNpcTarget(target, player)) yield return wait;
            if (!ReferenceEquals(player.GetComponent<PlayerInteractor>().Current, target)) yield break;
            var hub = Object.FindAnyObjectByType<PlayerInputHub>();
            hub.SetTouchButton(PlayerButton.Interact, true);
            hub.SetTouchButton(PlayerButton.Interact, false);
            yield return 0.12f;
        }

        static bool TryCityNpcApproach(Collider target, out Vector3 feet, out Quaternion look)
        {
            feet = default;
            look = Quaternion.identity;
            if (target == null) return false;
            int mask = ~(1 << PlayerInteractor.SeeThroughLayer);
            foreach (float radius in new[] { 1.4f, 1.9f, 2.4f })
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Vector3 at = target.bounds.center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                if (!Physics.Raycast(at + Vector3.up * 0.6f, Vector3.down, out RaycastHit ground, 4f, mask, QueryTriggerInteraction.Ignore) ||
                    ground.normal.y < 0.7f || Mathf.Abs(ground.point.y - target.transform.root.position.y) > 0.35f) continue;
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
