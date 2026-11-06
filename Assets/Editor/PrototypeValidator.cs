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
using AshenRealm.Story;
using AshenRealm.UI;
using AshenRealm.World;
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
                GeneratedAssets.DetailArrayPath,
                GeneratedAssets.SkyPath,
                GeneratedAssets.PrefabFolder + "/Player.prefab",
                GeneratedAssets.PrefabFolder + "/CursedKnight.prefab",
                GeneratedAssets.PrefabFolder + "/TreasureChest.prefab",
                GeneratedAssets.MaterialFolder + "/M_World.mat",
                GeneratedAssets.MaterialFolder + "/M_Foliage.mat",
                GeneratedAssets.MaterialFolder + "/M_PixelComposite.mat",
                GeneratedAssets.SettingsFolder + "/VP_AshenValley.asset",
                GeneratedAssets.MeshFolder + "/Terrain.asset",
                GeneratedAssets.MeshFolder + "/Ruins.asset",
                GeneratedAssets.MeshFolder + "/WindmillSails.asset",
                "Assets/Art/Menu/MenuBackground.jpg",
                "Assets/Art/Menu/MenuTitle.png",
                "Assets/Art/Menu/MenuFrame.png",
                "Assets/Art/Menu/MenuButton.png",
                "Assets/Art/Menu/MenuButtonGlow.png",
            };
            foreach (string path in required)
                r.Check(AssetDatabase.LoadMainAssetAtPath(path) != null, $"Missing generated asset: {path}");

            foreach (string name in new[] { "M_World", "M_Foliage" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedAssets.MaterialFolder}/{name}.mat");
                if (material == null) continue;
                r.Check(material.shader != null && material.shader.name == "AshenRealm/PixelLit" && material.shader.isSupported,
                    $"{name} must use a supported AshenRealm/PixelLit shader");
                r.Check(material.GetTexture("_DetailArray") is Texture2DArray array && array.depth == System.Enum.GetValues(typeof(TexLayer)).Length,
                    $"{name} has no complete detail texture array assigned");
            }
            var foliage = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.MaterialFolder + "/M_Foliage.mat");
            if (foliage != null)
                r.Check(foliage.IsKeywordEnabled("_ALPHATEST_ON") && foliage.IsKeywordEnabled("_WIND_ON"), "M_Foliage must enable alpha clip and wind");

            foreach (var urp in UniversalAssets())
                r.Check(Mathf.Approximately(urp.renderScale, 1f), $"{urp.name} render scale must be 1 (PixelCamera controls resolution)");

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
            var chest = All<TreasureChest>().FirstOrDefault(c => c.transform.root.name == "Zone Valley");
            r.Check(chest != null, "The valley chest is missing");
            One<ZoneManager>();
            One<GameSession>();
            One<StoryDirector>();
            One<DialoguePanel>();
            One<ProjectileLibrary>();
            One<PlayerAbilities>();
            var classRig = One<PlayerClassRig>();
            One<HUDController>();
            One<TouchJoystick>();
            One<TouchLookArea>();
            One<TouchControlsRoot>();
            r.Check(All<SafeAreaFitter>().Length == 3, "The HUD, the main menu and the class choice each need a safe area");
            One<MainMenu>();
            CheckClassCards(r, One<ClassCarousel>());
            PortraitLibrary portraits = One<PortraitLibrary>();
            if (portraits != null)
                foreach (NpcDefinition person in StoryText.Npcs.Where(n => n.Id != "merchant"))
                    foreach (Alignment mood in Enum.GetValues(typeof(Alignment)))
                        r.Check(portraits.Face(person.Id, mood) != null, $"{person.Name} has no {mood} portrait");
            CheckGoods(r, One<ShopPanel>(), classRig);
            One<SettingsPanel>();
            One<CodexPanel>();
            One<ConfirmPanel>();
            One<SoulKeeper>();
            One<HudAppearance>();
            One<TouchLayout>();
            One<EventSystem>();
            One<InputSystemUIInputModule>();
            One<AtmosphereController>();
            One<GameBootstrap>();
            One<ImpactEffects>();
            One<Volume>();
            r.Check(All<LoreInspectable>().Length == 2, "The blade in the valley and the gallows are the two things to inspect");
            var pixelCamera = One<PixelCamera>();
            One<PauseMenu>();
            One<CompassStrip>();
            One<Rotator>();
            if (pixelCamera != null)
                r.Check(pixelCamera.GetComponent<Camera>() != null && pixelCamera.CompareTag("MainCamera"), "PixelCamera must sit on the main camera");

            var buttons = All<TouchButton>();
            foreach (PlayerButton action in Enum.GetValues(typeof(PlayerButton)))
                r.Check(buttons.Count(b => b.Button == action) == 1, $"Expected one touch button for {action}");

            // The layout editor moves every on-screen control: the buttons, pause and the joystick.
            var handles = All<TouchLayoutHandle>();
            r.Check(handles.Length == buttons.Length + 2, $"Expected {buttons.Length + 2} layout handles, found {handles.Length}");
            r.Check(handles.Select(h => h.LayoutName).Distinct().Count() == handles.Length, "Layout handles need unique names");

            // A saved soul remembers what it cleared by these keys.
            string[] saveKeys = All<MonoBehaviour>().OfType<ISavedState>().Select(s => s.SaveKey).ToArray();
            r.Check(saveKeys.All(k => !string.IsNullOrEmpty(k)), "Something that can be cleared has no save key");
            r.Check(saveKeys.Distinct().Count() == saveKeys.Length, "Save keys must be unique");

            r.Check(All<Camera>().Count(c => c.CompareTag("MainCamera")) == 1, "Expected exactly one camera tagged MainCamera");
            r.Check(All<Light>().Count(l => l.type == LightType.Directional) == 1, "Expected exactly one directional light");
            r.Check(RenderSettings.fog, "Fog is disabled in the scene");
            r.Check(RenderSettings.skybox != null, "Scene has no skybox material");

            if (controller != null)
                r.Check(controller.CompareTag("Player"), "Player root is not tagged 'Player'");

            CheckReferences(r, roots);
            CheckRenderers(r, roots);
            CheckText(r, roots);

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
            CheckWorld(r, All<Zone>(), All<ZonePortal>(), All<EnemyAI>(), All<NpcActor>(), All<EncounterSite>());
        }

        /// <summary>Every player-facing string must be drawable by the font the WebGL build embeds.</summary>
        static void CheckText(Report r, GameObject[] roots)
        {
            var strings = new List<(string where, string text)>();
            foreach (FieldInfo field in typeof(GameText).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                object value = field.GetValue(null);
                if (value is string s) strings.Add(($"GameText.{field.Name}", s));
                else if (value is string[] array) strings.AddRange(array.Select(a => ($"GameText.{field.Name}", a)));
            }
            foreach (var text in roots.SelectMany(go => go.GetComponentsInChildren<UnityEngine.UI.Text>(true)))
            {
                if (text.font != null && text.font.dynamic && text.font.name.StartsWith("Spectral")) CheckSpectral(r, text.font, PathOf(text.transform), text.text);
                else strings.Add((PathOf(text.transform), text.text));
            }

            // Menu strings can land in any of the three faces.
            foreach (string face in new[] { "Regular", "Italic", "SemiBold" })
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>($"Assets/Art/Fonts/Spectral-{face}.ttf");
                if (!r.Check(font != null && font.dynamic, $"Spectral-{face} must be a dynamic font")) continue;
                foreach (FieldInfo field in typeof(MenuText).GetFields(BindingFlags.Public | BindingFlags.Static))
                    if (field.GetValue(null) is string menuText) CheckSpectral(r, font, $"MenuText.{field.Name}", menuText);
                foreach (CodexEntry entry in CodexBook.Entries)
                    CheckSpectral(r, font, $"Codex {entry.Id}", entry.Name + entry.Text);
                foreach (ClassCard card in Catalog.Cards)
                    CheckSpectral(r, font, $"Card {card.Art}", card.Name + card.Role + card.PassiveName + card.PassiveText);
                foreach (ClassDefinition c in Catalog.Classes)
                    CheckSpectral(r, font, $"Story of {c.Name}", c.Backstory + c.Goal + c.Ability1.Name + c.Ability1.Description + c.Ability2.Name + c.Ability2.Description);
                CheckSpectral(r, font, "Prologue", StoryText.Prologue);
            }

            foreach (var component in roots.SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c is IInteractable))
            {
                SerializedProperty it = new SerializedObject(component).GetIterator();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.String)
                        strings.Add(($"{PathOf(component.transform)}.{it.name}", it.stringValue));
            }

            strings.AddRange(Catalog.AllStrings().Where(s => s != null).Select(s => ("Catalog", s)));
            strings.AddRange(StoryText.AllStrings().Where(s => s != null).Select(s => ("StoryText", s)));

            foreach (var (where, text) in strings)
            {
                char bad = text.FirstOrDefault(c => !GameText.IsRenderable(c));
                r.Check(bad == default(char), $"{where}: character U+{(int)bad:X4} is missing from the web font");
            }
        }

        /// <summary>The row in the scene follows the table: one card each, and no class without one.</summary>
        static void CheckClassCards(Report r, ClassCarousel carousel)
        {
            if (carousel == null) return;
            var so = new SerializedObject(carousel);
            foreach (string field in new[] { "cards", "cardArt", "cardGlow", "dots" })
                r.Check(so.FindProperty(field).arraySize == Catalog.Cards.Length, $"Class choice: {field} does not match the {Catalog.Cards.Length} cards");
            r.Check(so.FindProperty("statValues").arraySize == 6, "Class choice: a card shows six stats");
            foreach (ClassDefinition c in Catalog.Classes)
                r.Check(Catalog.Cards.Count(card => card.Class == c) == 1, $"{c.Name} needs exactly one card");
        }

        /// <summary>Everything for sale has a picture, a price and, if it is held in the hand, a model; the shop draws with Spectral.</summary>
        static void CheckGoods(Report r, ShopPanel shop, PlayerClassRig rig)
        {
            if (shop == null || rig == null) return;
            var art = new SerializedObject(shop).FindProperty("art");
            var pictures = new HashSet<string>();
            for (int i = 0; i < art.arraySize; i++)
            {
                var sprite = art.GetArrayElementAtIndex(i).objectReferenceValue;
                if (sprite != null) pictures.Add(sprite.name);
            }
            Font font = shop.GetComponentsInChildren<UnityEngine.UI.Text>(true).First(t => t.name == "Name").font;
            foreach (ItemDefinition item in Catalog.Items)
            {
                r.Check(pictures.Contains(item.Id), $"{item.Name} has no picture");
                r.Check(item.Price > 0, $"{item.Name} has no price");
                CheckSpectral(r, font, item.Id, item.Name + item.Description + MenuText.ModsOf(item));
            }
            r.Check(new SerializedObject(rig).FindProperty("boughtWeapons").arraySize == Catalog.ItemsOf(ItemSlot.Weapon).Length, "Every weapon for sale needs a model in the hand");
        }

        static void CheckSpectral(Report r, Font font, string where, string text)
        {
            char bad = text.FirstOrDefault(c => c != '\n' && !font.HasCharacter(c));
            r.Check(bad == default(char), $"{where}: {font.name} has no character U+{(int)bad:X4}");
        }

        static float GetFloat(Object target, string field) => new SerializedObject(target).FindProperty(field).floatValue;

        static IEnumerable<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset> UniversalAssets()
        {
            var seen = new HashSet<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>();
            for (int i = 0; i < QualitySettings.names.Length; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp && seen.Add(urp))
                    yield return urp;
        }

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

        /// <summary>The zones, their portals and everything that stands in them.</summary>
        static void CheckWorld(Report r, Zone[] zones, ZonePortal[] portals, EnemyAI[] enemies, NpcActor[] npcs, EncounterSite[] encounters)
        {
            r.Check(zones.Length == StoryText.ZoneTitles.Length, $"Expected {StoryText.ZoneTitles.Length} zones, found {zones.Length}");

            bool OnGround(Vector3 p) => Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - p.y) < 1f;

            foreach (var zone in zones)
            {
                r.Check(!string.IsNullOrEmpty(zone.Title) && !string.IsNullOrEmpty(zone.Subtitle), $"{zone.name} has no title card");
                r.Check(zone.Entry != null && OnGround(zone.Entry.position), $"{zone.name}: entry is not on the ground");
                r.Check(zone.Exit != null && OnGround(zone.Exit.position), $"{zone.name}: stair arrival is not on the ground");
                r.Check(zone.Objective != null, $"{zone.name} has no compass objective");
                if (zone.Entry == null) continue;

                // Arriving players must not stand inside a wall.
                Vector3 feet = zone.Entry.position + Vector3.up * 0.4f;
                var overlaps = Physics.OverlapCapsule(feet, feet + Vector3.up * 1.1f, 0.33f, ~0, QueryTriggerInteraction.Ignore);
                r.Check(overlaps.All(c => c.GetComponent<CharacterController>() != null), $"{zone.name}: entry overlaps {string.Join(", ", overlaps.Select(c => c.name))}");
            }

            foreach (var portal in portals)
            {
                r.Check(portal.TargetZone >= 0 && portal.TargetZone < zones.Length, $"{portal.name} points at zone {portal.TargetZone}");
                r.Check(!string.IsNullOrEmpty(portal.InteractionPrompt), $"{portal.name} has no prompt");
            }

            // One stair down per depth but the last, each barred by a keeper that really exists.
            foreach (var portal in portals.Where(p => !string.IsNullOrEmpty(p.RequiredFlag) && p.RequiredFlag != StoryDirector.DeadFlag("knight")))
                r.Check(enemies.Any(e => StoryDirector.DeadFlag(e.EnemyId) == portal.RequiredFlag), $"{portal.name}: nobody holds the flag {portal.RequiredFlag}");

            foreach (var enemy in enemies)
            {
                r.Check(Catalog.Enemy(enemy.EnemyId) != null, $"{enemy.name}: unknown enemy id '{enemy.EnemyId}'");
                r.Check(OnGround(enemy.transform.position), $"{enemy.name} at {enemy.transform.position} is not on the ground");
                r.Check(enemy.GetComponent<AshenRealm.AI.ICreatureAnimator>() != null, $"{enemy.name} has no animator");
            }
            foreach (var definition in Catalog.Enemies)
                r.Check(enemies.Any(e => e.EnemyId == definition.Id), $"{definition.Name} never appears in the world");
            r.Check(enemies.Count(e => e.EnemyId == "gloamlord") == 1, "Exactly one Gloamlord");

            foreach (var npc in npcs) r.Check(StoryText.Npc(npc.NpcId) != null, $"{npc.name}: unknown npc id '{npc.NpcId}'");
            foreach (var npc in StoryText.Npcs) r.Check(npcs.Any(n => n.NpcId == npc.Id), $"{npc.Name} never appears in the world");
            foreach (var site in encounters) r.Check(StoryText.Encounter(site.EncounterId) != null, $"{site.name}: unknown encounter '{site.EncounterId}'");
            foreach (var encounter in StoryText.Encounters) r.Check(encounters.Any(s => s.EncounterId == encounter.Id), $"{encounter.Name} never appears in the world");

            foreach (var enemy in Catalog.Enemies)
                foreach (var move in enemy.Moves)
                    r.Check(move.Kind != MoveKind.Bolt || (int)move.Bolt < Enum.GetValues(typeof(BoltKind)).Length, $"{enemy.Id}: bad bolt on {move.Name}");

            // Every house has a way out, and the goods can be reached from where a player can stand.
            foreach (int house in new[] { ZoneNumbers.Tavern, ZoneNumbers.Forge, ZoneNumbers.Chapel })
            {
                Zone zone = zones[house];
                r.Check(zone.Indoors && !zone.Underground, $"{zone.name} should be indoors");
                r.Check(zone.GetComponentsInChildren<ZonePortal>(true).Any(p => p.TargetZone == ZoneNumbers.City), $"{zone.name} has no door to the street");
            }
            r.Check(zones[ZoneNumbers.Forge].GetComponentsInChildren<ShopFixture>(true).Any(f => f.Goods == ItemSlot.Weapon), "The forge has no rack");
            r.Check(zones[ZoneNumbers.Chapel].GetComponentsInChildren<ShopFixture>(true).Any(f => f.Goods == ItemSlot.Relic), "The chapel has no relics");
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
