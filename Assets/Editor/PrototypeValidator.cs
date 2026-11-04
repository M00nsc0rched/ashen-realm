using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Ashen Realm > Validate Prototype: automated checks for generated assets, scene structure, serialized
    /// references, world placement (physics queries) and core combat/stamina logic.
    /// </summary>
    public static class PrototypeValidator
    {
        const string LogPrefix = "[AshenRealm][Validate]";

        [MenuItem("Ashen Realm/Validate Prototype", priority = 20)]
        public static void ValidateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var errors = Run(out int checks);
            string summary = errors.Count == 0
                ? $"All {checks} checks passed."
                : $"{errors.Count} of {checks} checks failed:\n\n" + string.Join("\n", errors.Take(20));
            EditorUtility.DisplayDialog("Ashen Realm — Validation", summary, "OK");
        }

        /// <summary>Runs every check and returns the failures. Leaves Prototype_Valley open.</summary>
        public static List<string> Run(out int checks)
        {
            var report = new Report();
            // Open the prototype first: an additive temp scene cannot sit next to an untitled scene.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GeneratedAssets.ScenePath) != null)
                EditorSceneManager.OpenScene(GeneratedAssets.ScenePath, OpenSceneMode.Single);
            RunLogicTests(report);
            CheckAssets(report);
            CheckScene(report);

            foreach (string error in report.Errors) Debug.LogError($"{LogPrefix} FAIL: {error}");
            Debug.Log(report.Errors.Count == 0
                ? $"{LogPrefix} VALIDATION PASSED ({report.Checks} checks)"
                : $"{LogPrefix} VALIDATION FAILED ({report.Errors.Count}/{report.Checks} checks failed)");
            checks = report.Checks;
            return report.Errors;
        }

        sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public int Checks;

            public bool Check(bool condition, string description)
            {
                Checks++;
                if (!condition) Errors.Add(description);
                return condition;
            }
        }

        // ---- Assets ---------------------------------------------------------------------------------------

        static void CheckAssets(Report r)
        {
            string[] required =
            {
                GeneratedAssets.ScenePath,
                GeneratedAssets.PalettePath,
                GeneratedAssets.PrefabFolder + "/Player.prefab",
                GeneratedAssets.PrefabFolder + "/CursedKnight.prefab",
                GeneratedAssets.PrefabFolder + "/TreasureChest.prefab",
                GeneratedAssets.MaterialFolder + "/M_World.mat",
                GeneratedAssets.MaterialFolder + "/M_Metal.mat",
                GeneratedAssets.SettingsFolder + "/VP_AshenValley.asset",
                GeneratedAssets.MeshFolder + "/Terrain.asset",
                GeneratedAssets.MeshFolder + "/Ruins.asset",
            };
            foreach (string path in required)
                r.Check(AssetDatabase.LoadMainAssetAtPath(path) != null, $"Missing generated asset: {path}");

            var world = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.MaterialFolder + "/M_World.mat");
            if (world != null)
                r.Check(world.GetTexture("_BaseMap") != null, "M_World has no palette texture assigned");

            r.Check(EditorBuildSettings.scenes.Length > 0 && EditorBuildSettings.scenes[0].path == GeneratedAssets.ScenePath && EditorBuildSettings.scenes[0].enabled,
                "Prototype_Valley is not the first enabled scene in Build Settings");
        }

        // ---- Scene ------------------------------------------------------------------------------------------

        static void CheckScene(Report r)
        {
            if (!r.Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(GeneratedAssets.ScenePath) != null, "Scene file missing")) return;
            Scene scene = EditorSceneManager.OpenScene(GeneratedAssets.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            GameObject[] roots = scene.GetRootGameObjects();

            T[] All<T>() where T : Component => roots.SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();
            T One<T>() where T : Component
            {
                T[] found = All<T>();
                r.Check(found.Length == 1, $"Expected exactly one {typeof(T).Name} in scene, found {found.Length}");
                return found.FirstOrDefault();
            }

            var input = One<PlayerInputHub>();
            var controller = One<FirstPersonController>();
            var combat = One<PlayerCombat>();
            var stamina = One<Stamina>();
            One<PlayerInteractor>();
            var lifecycle = One<PlayerLifecycle>();
            One<CameraEffects>();
            One<FirstPersonWeaponAnimator>();
            var knight = One<CursedKnightAI>();
            One<KnightAnimator>();
            var chest = One<TreasureChest>();
            One<HUDController>();
            One<TouchJoystick>();
            One<TouchLookArea>();
            One<TouchControlsRoot>();
            One<SafeAreaFitter>();
            One<EventSystem>();
            One<InputSystemUIInputModule>();
            One<AtmosphereController>();
            One<GameBootstrap>();
            One<ImpactEffects>();
            One<Volume>();
            One<LoreInspectable>();

            var buttons = All<TouchButton>();
            foreach (PlayerButton action in Enum.GetValues(typeof(PlayerButton)))
                r.Check(buttons.Count(b => b.Button == action) == 1, $"Expected one touch button for {action}");

            r.Check(All<Camera>().Count(c => c.CompareTag("MainCamera")) == 1, "Expected exactly one camera tagged MainCamera");
            r.Check(All<Light>().Count(l => l.type == LightType.Directional) == 1, "Expected exactly one directional light");
            r.Check(RenderSettings.fog, "Fog is disabled in the scene");
            r.Check(RenderSettings.skybox != null, "Scene has no skybox material");

            if (controller != null)
                r.Check(controller.CompareTag("Player"), "Player root is not tagged 'Player'");

            CheckReferences(r, roots);
            CheckRenderers(r, roots);

            // Gameplay tuning required by the milestone.
            if (combat != null)
            {
                r.Check(Mathf.Approximately(GetFloat(combat, "damage"), 25f), "Sword damage must be 25");
                r.Check(Mathf.Approximately(GetFloat(combat, "range"), 2.5f), "Sword range must be 2.5");
                var playerHealth = combat.GetComponent<Health>();
                r.Check(playerHealth != null && Mathf.Approximately(playerHealth.Max, 100f), "Player health must be 100");
                r.Check(combat.GetComponent<Health>() != null, "PlayerCombat must share a GameObject with the player's Health (block filter)");
            }
            if (stamina != null) r.Check(Mathf.Approximately(stamina.Max, 100f), "Player stamina must be 100");

            CheckPlacement(r, controller, knight, chest, lifecycle);
            CheckReach(r, combat, knight);
        }

        static float GetFloat(Object target, string field) => new SerializedObject(target).FindProperty(field).floatValue;

        /// <summary>Every object reference on an AshenRealm component must be assigned unless marked [Optional].</summary>
        static void CheckReferences(Report r, GameObject[] roots)
        {
            foreach (var go in roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject))
            {
                r.Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0, $"Missing script on '{PathOf(go.transform)}'");
                foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null) continue;
                    Type type = behaviour.GetType();
                    if (type.Namespace == null || !type.Namespace.StartsWith("AshenRealm")) continue;

                    var so = new SerializedObject(behaviour);
                    SerializedProperty p = so.GetIterator();
                    bool enterChildren = true;
                    while (p.NextVisible(enterChildren))
                    {
                        enterChildren = p.propertyType != SerializedPropertyType.String;
                        if (p.propertyType != SerializedPropertyType.ObjectReference || p.name == "m_Script") continue;
                        if (p.objectReferenceValue != null) continue;
                        string fieldName = p.propertyPath.Split('.')[0];
                        FieldInfo field = FindField(type, fieldName);
                        if (field != null && field.GetCustomAttribute<OptionalAttribute>() != null) continue;
                        r.Check(false, $"{type.Name}.{p.propertyPath} is not assigned on '{PathOf(go.transform)}'");
                    }
                }
            }
        }

        static FieldInfo FindField(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field;
            }
            return null;
        }

        static void CheckRenderers(Report r, GameObject[] roots)
        {
            foreach (var filter in roots.SelectMany(x => x.GetComponentsInChildren<MeshFilter>(true)))
            {
                bool ok = filter.sharedMesh != null && AssetDatabase.Contains(filter.sharedMesh);
                r.Check(ok, $"MeshFilter on '{PathOf(filter.transform)}' has no persistent mesh asset");
            }
            foreach (var renderer in roots.SelectMany(x => x.GetComponentsInChildren<MeshRenderer>(true)))
                r.Check(renderer.sharedMaterial != null && AssetDatabase.Contains(renderer.sharedMaterial), $"MeshRenderer on '{PathOf(renderer.transform)}' has no material asset");
            foreach (var collider in roots.SelectMany(x => x.GetComponentsInChildren<MeshCollider>(true)))
                r.Check(collider.sharedMesh != null, $"MeshCollider on '{PathOf(collider.transform)}' has no mesh");
        }

        static void CheckPlacement(Report r, FirstPersonController player, CursedKnightAI knight, TreasureChest chest, PlayerLifecycle lifecycle)
        {
            if (player == null || knight == null || chest == null) return;
            Transform playerRoot = player.transform;

            bool Grounded(Vector3 position, Transform ignore, out float gap)
            {
                gap = float.MaxValue;
                var hits = Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
                foreach (var hit in hits.OrderBy(h => h.distance))
                {
                    if (ignore != null && hit.collider.transform.IsChildOf(ignore)) continue;
                    gap = position.y - hit.point.y;
                    return true;
                }
                return false;
            }

            r.Check(Grounded(playerRoot.position, playerRoot, out float playerGap) && playerGap > -0.05f && playerGap < 0.6f,
                $"Player spawn is not resting on ground (gap {playerGap:F2})");
            r.Check(Grounded(knight.transform.position, knight.transform, out float knightGap) && knightGap > -0.05f && knightGap < 0.6f,
                $"Cursed Knight is not resting on ground (gap {knightGap:F2})");
            r.Check(Grounded(chest.transform.position + Vector3.up * 0.02f, chest.transform, out float chestGap) && Mathf.Abs(chestGap) < 0.35f,
                $"Chest is not resting on a surface (gap {chestGap:F2})");

            // Spawn capsule must be free of world geometry.
            var cc = player.GetComponent<CharacterController>();
            Vector3 bottom = playerRoot.position + Vector3.up * (cc.radius + 0.05f);
            Vector3 top = playerRoot.position + Vector3.up * (cc.height - cc.radius);
            var overlaps = Physics.OverlapCapsule(bottom, top, cc.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !c.transform.IsChildOf(playerRoot)).ToArray();
            r.Check(overlaps.Length == 0, $"Player spawn overlaps: {string.Join(", ", overlaps.Select(c => c.name))}");

            float detection = GetFloat(knight, "detectionRadius");
            float spawnDistance = Vector3.Distance(playerRoot.position, knight.transform.position);
            r.Check(spawnDistance > detection + 10f, $"Knight is too close to the spawn ({spawnDistance:F1} m)");

            // The chest must be aimable from the front at standing eye height.
            Vector3 eye = chest.transform.position + chest.transform.rotation * new Vector3(0f, 1.6f, -2.2f);
            Vector3 target = chest.transform.position + Vector3.up * 0.4f;
            bool aimable = Physics.SphereCast(eye, 0.2f, (target - eye).normalized, out RaycastHit aimHit, 3f, ~0, QueryTriggerInteraction.Collide)
                           && aimHit.collider.GetComponentInParent<TreasureChest>() == chest;
            r.Check(aimable, "Chest cannot be targeted by the interaction probe from in front of it");

            var patrol = new SerializedObject(knight).FindProperty("patrolPoints");
            r.Check(patrol != null && patrol.arraySize >= 2, "Knight has fewer than two patrol points");

            r.Check(lifecycle != null && new SerializedObject(lifecycle).FindProperty("spawnPoint").objectReferenceValue != null, "Player has no spawn point");

            // Four boundary walls keep the player inside the valley.
            int walls = player.gameObject.scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<BoxCollider>(true))
                .Count(b => b.gameObject.name == "Boundary Wall");
            r.Check(walls == 4, $"Expected 4 world boundary walls, found {walls}");
        }

        static void CheckReach(Report r, PlayerCombat combat, CursedKnightAI knight)
        {
            if (combat == null || knight == null) return;
            float knightRadius = knight.GetComponent<CharacterController>().radius;
            float playerRadius = combat.GetComponent<CharacterController>().radius;
            float stopDistance = GetFloat(knight, "attackRange") * 0.85f;
            float swordReach = GetFloat(combat, "range") + GetFloat(combat, "hitRadius");
            r.Check(swordReach >= stopDistance - knightRadius + 0.2f, "Player sword cannot reach the knight at its stopping distance");

            float knightReach = GetFloat(knight, "attackReach") * 0.65f + GetFloat(knight, "attackHitRadius");
            r.Check(knightReach >= GetFloat(knight, "attackRange") - playerRadius, "Knight attack sphere cannot reach the player at attack range");
        }

        // ---- Logic tests --------------------------------------------------------------------------------

        static void RunLogicTests(Report r)
        {
            Scene temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                // Health.
                var target = new GameObject("Test Target");
                SceneManager.MoveGameObjectToScene(target, temp);
                var health = target.AddComponent<Health>();
                bool died = false;
                health.Died += _ => died = true;
                float applied = health.ApplyDamage(new DamageInfo(25f, Vector3.zero, Vector3.forward, null));
                r.Check(Mathf.Approximately(applied, 25f) && Mathf.Approximately(health.Current, 75f), "Health: 25 damage should leave 75");
                health.ApplyDamage(new DamageInfo(500f, Vector3.zero, Vector3.forward, null));
                r.Check(died && !health.IsAlive && health.Current == 0f, "Health: lethal damage should kill and clamp at 0");
                r.Check(health.ApplyDamage(new DamageInfo(10f, Vector3.zero, Vector3.forward, null)) == 0f, "Health: dead targets take no damage");
                health.ResetHealth();
                health.ApplyDamage(new DamageInfo(50f, Vector3.zero, Vector3.forward, null));
                health.Heal(30f);
                r.Check(Mathf.Approximately(health.Current, 80f), "Health: heal should restore up to max");

                // Stamina.
                var staminaGo = new GameObject("Test Stamina");
                SceneManager.MoveGameObjectToScene(staminaGo, temp);
                var stamina = staminaGo.AddComponent<Stamina>();
                r.Check(stamina.TrySpend(30f) && Mathf.Approximately(stamina.Current, 70f), "Stamina: spending 30 should leave 70");
                r.Check(!stamina.TrySpend(80f) && Mathf.Approximately(stamina.Current, 70f), "Stamina: TrySpend must not overdraw");
                r.Check(Mathf.Approximately(stamina.Drain(100f), 70f) && stamina.IsExhausted, "Stamina: draining to zero should exhaust");

                // Shield block: frontal hit with the block raised is mostly absorbed and costs stamina.
                var player = new GameObject("Test Player");
                SceneManager.MoveGameObjectToScene(player, temp);
                var playerStamina = player.AddComponent<Stamina>();
                var combat = player.AddComponent<PlayerCombat>();
                var playerHealth = player.AddComponent<Health>();
                var weaponsGo = new GameObject("Test Weapons");
                SceneManager.MoveGameObjectToScene(weaponsGo, temp);
                var weapons = weaponsGo.AddComponent<FirstPersonWeaponAnimator>();
                SceneWiring.Set(combat, "stamina", playerStamina);
                SceneWiring.Set(combat, "weapons", weapons);
                SceneWiring.Set(combat, "health", playerHealth);
                typeof(PlayerCombat).GetProperty(nameof(PlayerCombat.IsBlocking))?.GetSetMethod(true)?.Invoke(combat, new object[] { true });

                var attacker = new GameObject("Test Attacker");
                SceneManager.MoveGameObjectToScene(attacker, temp);
                attacker.transform.position = player.transform.position + Vector3.forward * 2f;
                float frontal = playerHealth.ApplyDamage(new DamageInfo(20f, Vector3.zero, Vector3.back, attacker));
                r.Check(frontal > 0f && frontal < 5f && playerStamina.Current < 100f, $"Block: frontal 20 damage should be reduced to ~3 (got {frontal:F1})");

                attacker.transform.position = player.transform.position + Vector3.back * 2f;
                float behind = playerHealth.ApplyDamage(new DamageInfo(20f, Vector3.zero, Vector3.forward, attacker));
                r.Check(Mathf.Approximately(behind, 20f), $"Block: attacks from behind must not be blocked (got {behind:F1})");
            }
            catch (Exception e)
            {
                r.Check(false, $"Logic tests threw: {e.Message}");
            }
            finally
            {
                EditorSceneManager.CloseScene(temp, true);
            }
        }

        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }
}
