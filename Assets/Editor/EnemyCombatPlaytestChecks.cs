using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Input;
using AshenRealm.Player;
using AshenRealm.Story;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>Real capsules, health filters and projectiles in a temporary arena, isolated from saved foes.</summary>
    internal static class EnemyCombatPlaytestChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        static void StartMove(EnemyAI enemy, EnemyMove move)
        {
            typeof(EnemyAI).GetMethod("CancelAttack", Private).Invoke(enemy, null);
            typeof(EnemyAI).GetMethod("BeginMove", Private).Invoke(enemy, new object[] { move });
        }

        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var sources = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include)
                .Where(e => e.gameObject.scene.IsValid()).GroupBy(e => e.EnemyId).ToDictionary(g => g.Key, g => g.First());
            if (player == null || !sources.ContainsKey("morr") || !sources.ContainsKey("mycel"))
            { check(false, "Enemy scenarios have a player and the regenerated bestiary"); yield break; }
            Health health = player.GetComponent<Health>();
            StatusEffects playerStatus = player.GetComponent<StatusEffects>();
            var input = player.GetComponent<PlayerInputHub>();
            Vector3 oldPosition = player.transform.position;
            float oldYaw = player.transform.eulerAngles.y, oldPitch = player.Pitch, oldMax = health.Max, oldHealth = health.Current;
            bool oldInput = player.InputEnabled;
            float oldScale = Time.timeScale;
            var oldProjectiles = new HashSet<EntityId>(Object.FindObjectsByType<Projectile>().Select(p => p.GetEntityId()));
            Vector3 origin = new Vector3(-1500f, 0f, -700f);
            var arena = new GameObject("Enemy Scenario Arena");
            arena.SetActive(false);
            var floor = new GameObject("Scenario Floor");
            floor.transform.SetParent(arena.transform, false);
            floor.transform.position = origin + Vector3.down * 0.5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(70f, 1f, 70f);
            GameObject activeClone = null;
            EnemyAI Spawn(string id, float distance = 6f)
            {
                CombatFocus.Set(null);
                if (activeClone != null) { activeClone.SetActive(false); Object.Destroy(activeClone); }
                arena.SetActive(false);
                activeClone = Object.Instantiate(sources[id].gameObject, arena.transform);
                activeClone.name = "Scenario " + id;
                activeClone.transform.SetPositionAndRotation(origin + Vector3.up * 0.06f, Quaternion.identity);
                activeClone.GetComponent<CharacterController>().enabled = true;
                activeClone.SetActive(true);
                player.Teleport(origin + new Vector3(0f, 0.06f, distance), 180f);
                playerStatus.Clear(); health.ResetHealth();
                arena.SetActive(true);
                EnemyAI result = activeClone.GetComponent<EnemyAI>();
                result.Engage(); Set(result, "nextAttackTime", float.PositiveInfinity);
                Physics.SyncTransforms();
                return result;
            }
            try
            {
                Time.timeScale = 1f;
                input.SetTouchMove(Vector2.zero);
                input.SetTouchButton(PlayerButton.Block, false);
                input.SetTouchButton(PlayerButton.Attack, false);
                player.InputEnabled = false;
                health.SetMax(999f, true);

                EnemyAI fungus = Spawn("mycel");
                fungus.Health.SetCurrent(fungus.Health.Max * 0.4f);
                yield return 0.2f;
                check(fungus.Phase == 2, "Mycel changes phase when wounded");
                check(fungus.transform.Find("Model").localScale.x > 2f, "Mycel's animated body keeps its authored guardian scale");
                EnemyAI lord = Spawn("gloamlord");
                lord.Health.SetCurrent(lord.Health.Max * 0.5f);
                check(lord.Phase == 2, "The Gloamlord enters its second phase");
                lord.Health.SetCurrent(lord.Health.Max * 0.2f);
                check(lord.Phase == 3, "The Gloamlord enters its third phase");

                EnemyAI cultist = Spawn("cultist");
                EnemyMove ground = cultist.Definition.Moves.First(m => m.Kind == MoveKind.Ground);
                StartMove(cultist, ground);
                yield return ground.Windup * 0.58f;
                var warning = cultist.GetComponentInChildren<EnemyAttackTelegraph>(true);
                Transform area = (Transform)typeof(EnemyAttackTelegraph).GetField("area", Private).GetValue(warning);
                Vector3 committed = area.position;
                check(cultist.TelegraphActive, "A ground attack shows its full warning during windup");
                float beforeDodge = health.Current;
                player.Teleport(origin + new Vector3(7f, 0.06f, 6f), 180f);
                yield return ground.Windup * 0.18f;
                check(Vector3.Distance(area.position, committed) < 0.05f, "The warning stops tracking after the attack commits");
                yield return ground.Windup * 0.35f;
                check(health.Current >= beforeDodge && cultist.LastResolvedMove == ground, "Stepping outside the committed ground attack avoids damage");

                EnemyAI ghoul = Spawn("ghoul");
                EnemyMove sweep = ghoul.Definition.Moves.First(m => m.Kind == MoveKind.Sweep);
                StartMove(ghoul, sweep);
                yield return 0.12f;
                Vector3 beforePush = ghoul.transform.position;
                ghoul.Health.ApplyDamage(new DamageInfo(1f, beforePush + Vector3.up, Vector3.right, player.gameObject));
                yield return 0.25f;
                var ghoulWarning = ghoul.GetComponentInChildren<EnemyAttackTelegraph>(true);
                Transform ghoulArea = (Transform)typeof(EnemyAttackTelegraph).GetField("area", Private).GetValue(ghoulWarning);
                check(PlanarDistance(ghoul.transform.position, beforePush) > 0.1f &&
                    PlanarDistance(ghoulArea.position, ghoul.transform.position) < 0.08f,
                    "A real hit pushes the winding-up foe and its sweep warning follows its capsule");

                ghoul = Spawn("ghoul");
                EnemyMove charge = ghoul.Definition.Moves.First(m => m.Kind == MoveKind.Charge);
                StartMove(ghoul, charge);
                yield return charge.Windup * 0.15f;
                beforePush = ghoul.transform.position;
                ghoul.Health.ApplyDamage(new DamageInfo(1f, beforePush + Vector3.up, Vector3.right, player.gameObject));
                yield return charge.Windup * 0.45f;
                ghoulWarning = ghoul.GetComponentInChildren<EnemyAttackTelegraph>(true);
                Transform lane = (Transform)typeof(EnemyAttackTelegraph).GetField("lane", Private).GetValue(ghoulWarning);
                Vector3 laneStart = lane.position - lane.forward * (lane.lossyScale.z * 0.5f);
                check(PlanarDistance(ghoul.transform.position, beforePush) > 0.1f &&
                    PlanarDistance(laneStart, ghoul.transform.position) < 0.08f,
                    "Knockback during charge windup also moves the marked lane to the real starting capsule");
                check(ghoul.TelegraphActive, "A charging foe marks its lane before moving");
                Vector3 chargeStart = ghoul.transform.position;
                player.Teleport(origin + new Vector3(8f, 0.06f, 6f), 180f);
                float beforeCharge = health.Current;
                yield return charge.Windup * 0.4f + charge.ActiveTime * 0.7f;
                check(Vector3.Distance(ghoul.transform.position, chargeStart) > 2f, "The charge moves the actual enemy capsule");
                check(health.Current >= beforeCharge, "A sidestep avoids the physical charging lane");
                float rushX = ghoul.transform.position.x;
                ghoul.Health.ApplyDamage(new DamageInfo(1f, ghoul.transform.position + Vector3.up, Vector3.right, player.gameObject));
                yield return charge.ActiveTime * 0.15f;
                check(Mathf.Abs(ghoul.transform.position.x - rushX) < 0.05f, "A hit during the active rush cannot push it sideways out of the marked lane");

                EnemyAI priest = Spawn("rotpriest", 1f);
                EnemyMove ring = priest.Definition.Moves.First(m => m.Kind == MoveKind.Ring);
                float beforeRing = health.Current;
                StartMove(priest, ring);
                yield return ring.Windup + 0.1f;
                check(priest.LastResolvedMove == ring && health.Current >= beforeRing, "The centre inside a ring's inner outline is a real safe area");
                player.Teleport(origin + new Vector3(0f, 0.06f, ring.Radius * 0.75f), 180f);
                StartMove(priest, ring);
                yield return ring.Windup + 0.1f;
                check(health.Current < beforeRing, "The band between the ring outlines really deals damage");

                EnemyAI ooze = Spawn("ooze");
                EnemyMove burrow = ooze.Definition.Moves.First(m => m.Kind == MoveKind.Burrow);
                var burrowWall = new GameObject("Scenario Burrow Wall");
                burrowWall.transform.SetParent(arena.transform, false);
                float landingZ = 6f - (ooze.GetComponent<CharacterController>().radius + 0.9f);
                burrowWall.transform.position = origin + new Vector3(0f, 2f, landingZ);
                burrowWall.AddComponent<BoxCollider>().size = new Vector3(2f, 4f, 1.5f);
                Physics.SyncTransforms();
                Vector3 beforeBurrow = ooze.transform.position;
                float beforeBurst = health.Current;
                StartMove(ooze, burrow);
                yield return burrow.Windup + burrow.ActiveTime + 0.1f;
                check(ooze.LastResolvedMove == burrow && PlanarDistance(ooze.transform.position, beforeBurrow) < 0.1f && health.Current >= beforeBurst,
                    "A blocked burrow neither relocates into a wall nor deals a burst from the wrong place");
                Object.Destroy(burrowWall);
                yield return 0.05f;
                StartMove(ooze, burrow);
                yield return burrow.Windup + burrow.ActiveTime + 0.1f;
                check(PlanarDistance(ooze.transform.position, beforeBurrow) > 3f && health.Current < beforeBurst,
                    "A clear burrow moves the real capsule and bursts at its committed warning");

                EnemyAI morr = Spawn("morr", 2f);
                EnemyMove counter = morr.Definition.Moves.First(m => m.Kind == MoveKind.Counter);
                StartMove(morr, counter);
                yield return 0.1f;
                float blocked = morr.Health.ApplyDamage(new DamageInfo(40f, morr.transform.position + Vector3.up, Vector3.back, player.gameObject));
                check(blocked > 0f && blocked < 20f, "Morr's frontal counter stance really mitigates an incoming hit");
                float beforeCounter = health.Current;
                yield return counter.Windup + 0.12f;
                check(morr.LastResolvedMove == counter && health.Current < beforeCounter, "Striking the counter stance triggers its retaliatory blow");

                cultist = Spawn("cultist");
                EnemyMove heal = cultist.Definition.Moves.First(m => m.Kind == MoveKind.Heal);
                cultist.Health.SetCurrent(20f);
                StartMove(cultist, heal);
                yield return 0.25f;
                cultist.Health.ApplyDamage(new DamageInfo(5f, cultist.transform.position + Vector3.up, Vector3.forward, player.gameObject));
                Set(cultist, "nextAttackTime", float.PositiveInfinity);
                check(cultist.CurrentState != EnemyAI.State.Attack, "A real hit interrupts an enemy's healing channel");
                yield return heal.Windup + 0.1f;
                check(cultist.Health.Current <= 15.1f, "An interrupted channel does not grant its delayed heal");

                EnemyAI shade = Spawn("shade");
                EnemyMove blink = shade.Definition.Moves.First(m => m.Kind == MoveKind.Teleport);
                var barrier = new GameObject("Scenario Blink Wall");
                barrier.transform.SetParent(arena.transform, false);
                barrier.transform.position = origin + new Vector3(blink.TravelDistance, 2f, 6f);
                barrier.AddComponent<BoxCollider>().size = new Vector3(3f, 4f, 3f);
                Physics.SyncTransforms();
                Vector3 beforeBlink = shade.transform.position;
                StartMove(shade, blink);
                yield return blink.Windup + 0.15f;
                check(Vector3.Distance(shade.transform.position, beforeBlink) < 0.4f, "Blink refuses a destination inside a solid wall");
                Object.Destroy(barrier);
                yield return 0.05f;
                StartMove(shade, blink);
                yield return blink.Windup + 0.15f;
                check(Vector3.Distance(shade.transform.position, beforeBlink) > 2.5f, "Blink actually relocates the enemy when its capsule fits");

                EnemyAI archer = Spawn("archer", 10f);
                EnemyMove volley = archer.Definition.Moves.First(m => m.Kind == MoveKind.Volley);
                var bolts = new HashSet<EntityId>();
                StartMove(archer, volley);
                for (float elapsed = 0f; elapsed < volley.Windup + 1f; elapsed += 0.1f)
                {
                    yield return 0.1f;
                    foreach (Projectile projectile in Object.FindObjectsByType<Projectile>())
                        if ((GameObject)typeof(Projectile).GetField("owner", Private).GetValue(projectile) == archer.gameObject)
                            bolts.Add(projectile.GetEntityId());
                }
                check(bolts.Count >= volley.Hits, "A volley emits separate physical projectiles over time");

                var knightSource = Object.FindObjectsByType<CursedKnightAI>(FindObjectsInactive.Include).FirstOrDefault();
                if (knightSource != null)
                {
                    activeClone.SetActive(false); Object.Destroy(activeClone); arena.SetActive(false);
                    activeClone = Object.Instantiate(knightSource.gameObject, arena.transform);
                    activeClone.transform.SetPositionAndRotation(origin + Vector3.up * 0.06f, Quaternion.identity);
                    activeClone.GetComponent<CharacterController>().enabled = true;
                    activeClone.SetActive(true); arena.SetActive(true);
                    var knight = activeClone.GetComponent<CursedKnightAI>();
                    var patterns = new HashSet<MoveKind>();
                    var begin = typeof(CursedKnightAI).GetMethod("BeginAttack", Private);
                    var state = typeof(CursedKnightAI).GetMethod("SetState", Private);
                    player.Teleport(origin + new Vector3(0f, 0.06f, 4f), 180f);
                    for (int i = 0; i < 3; i++)
                    {
                        state.Invoke(knight, new object[] { CursedKnightAI.State.Chase });
                        begin.Invoke(knight, null);
                        patterns.Add(knight.CurrentPattern);
                    }
                    Vector3 knightStart = knight.transform.position;
                    yield return 1.2f;
                    check(patterns.Count == 3, "The Cursed Knight alternates chop, sweep and shield rush");
                    check(Vector3.Distance(knight.transform.position, knightStart) > 0.6f, "The Cursed Knight's shield rush moves its capsule");
                }
            }
            finally
            {
                CombatFocus.Set(null);
                if (activeClone != null) activeClone.SetActive(false);
                Object.Destroy(arena);
                foreach (Projectile projectile in Object.FindObjectsByType<Projectile>())
                    if (!oldProjectiles.Contains(projectile.GetEntityId())) Object.Destroy(projectile.gameObject);
                playerStatus.Clear();
                health.SetMax(oldMax, false); health.SetCurrent(oldHealth);
                player.Teleport(oldPosition, oldYaw, oldPitch);
                player.InputEnabled = oldInput;
                Time.timeScale = oldScale;
            }
        }
        static float PlanarDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
    }
}
