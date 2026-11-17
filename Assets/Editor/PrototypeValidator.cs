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
                GeneratedAssets.MeshFolder + "/Terrain_0_0.asset",
                "Assets/Art/Generated/Scatter_Valley.asset",
                GeneratedAssets.MeshFolder + "/Ruins.asset",
                GeneratedAssets.MeshFolder + "/WindmillSails.asset",
                GeneratedAssets.MeshFolder + "/Arrival_Road.asset",
                GeneratedAssets.MeshFolder + "/Hamlet_Home_0.asset",
                GeneratedAssets.MeshFolder + "/Hamlet_Home_1.asset",
                GeneratedAssets.MeshFolder + "/Hamlet_Home_2.asset",
                GeneratedAssets.MeshFolder + "/Player_TorchStrip.asset",
                GeneratedAssets.MaterialFolder + "/M_CarriedTorch.mat",
                "Assets/Art/Menu/Torch/TorchWarm.png",
                "Assets/Art/Menu/Torch/TorchCold.png",
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

            var torchMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.MaterialFolder + "/M_CarriedTorch.mat");
            if (torchMaterial != null)
                r.Check(torchMaterial.shader != null && torchMaterial.shader.name == "AshenRealm/TorchStrip" && torchMaterial.shader.isSupported,
                    "The carried torch must use the supported eight-frame TorchStrip shader");

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
            var cityLedger = One<CityActivityLedger>();
            r.Check(cityLedger != null && cityLedger.GetComponentInParent<Zone>() == null && cityLedger.gameObject.activeSelf,
                "City contracts must remain active while travelling underground");
            var cityStations = All<CityActivity>();
            r.Check(cityStations.Length == Enum.GetValues(typeof(CityActivityKind)).Length &&
                cityStations.Select(s => s.Kind).Distinct().Count() == cityStations.Length,
                "The city needs one working station for each of its five activities");
            r.Check(cityStations.Select(s => s.ActivityId).Distinct().Count() == cityStations.Length,
                "City activity identifiers must be unique");
            One<DialoguePanel>();
            One<ProjectileLibrary>();
            One<PlayerAbilities>();
            var classRig = One<PlayerClassRig>();
            One<PlayerProgression>();
            One<PlayerPassives>();
            var carriedTorch = One<PlayerTorch>();
            CheckPlayerKits(r, classRig, carriedTorch);
            CheckHudAbilityIcons(r, One<HUDController>());
            One<TouchJoystick>();
            One<TouchLookArea>();
            One<TouchControlsRoot>();
            r.Check(All<SafeAreaFitter>().Length == 3, "The HUD, the main menu and the class choice each need a safe area");
            One<MainMenu>();
            CheckClassCards(r, One<ClassCarousel>());
            PortraitLibrary portraits = One<PortraitLibrary>();
            if (portraits != null)
                // Everyone with a painting in Vázlat_Kocsma or Vázlat_Város; the merchant and the north land's people have none yet.
                foreach (NpcDefinition person in StoryText.Npcs.Where(n => MenuBuilder.PortraitPeople.Contains(n.Id) || MenuBuilder.OneFacePeople.Contains(n.Id)))
                    foreach (Alignment mood in Enum.GetValues(typeof(Alignment)))
                        r.Check(portraits.Face(person.Id, mood) != null, $"{person.Name} has no {mood} portrait");
            CheckGoods(r, One<ShopPanel>(), classRig);
            One<SettingsPanel>();
            var inventory = One<InventoryPanel>();
            var secret = One<SecretStatsPanel>();
            var holdMenu = One<HoldMenuButton>();
            CheckInventory(r, inventory, secret, holdMenu);
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
            r.Check(All<LoreInspectable>().Length >= 17, "Original lore and a discovery in every redesigned inhabited place and depth must be inspectable");
            var pixelCamera = One<PixelCamera>();
            var pause = One<PauseMenu>();
            if (pause != null)
                foreach (string field in new[] { "inventory", "secret", "keeper", "codex" })
                    r.Check(new SerializedObject(pause).FindProperty(field).objectReferenceValue != null, $"The descent menu needs its {field}");
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

            foreach (Zone zone in All<Zone>())
            {
                var permanentLights = zone.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Point && l.intensity > 0f).ToArray();
                r.Check(permanentLights.Length <= 3, $"{zone.name}: more than three permanent point lights");
                r.Check(permanentLights.All(l => l.shadows == LightShadows.None), $"{zone.name}: local lights must not cast realtime shadows");
            }
            var worldMaterials = All<Zone>().SelectMany(z => z.GetComponentsInChildren<MeshRenderer>(true))
                .Select(m => m.sharedMaterial).Where(m => m != null).Distinct().ToArray();
            r.Check(worldMaterials.Length <= 2 && worldMaterials.All(m => m.shader != null && m.shader.name == "AshenRealm/PixelLit"),
                "The generated world must stay within two PixelLit materials; the carried torch has its own viewmodel shader");

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
            CheckNorthLand(r, All<Zone>(), All<ZonePortal>(), All<TollGate>(), All<ScatterRenderer>(), roots);
            CheckNewPeople(r, All<Zone>());
            foreach (ClassDefinition definition in Catalog.Classes)
            {
                AbilityDefinition[] kit = AbilityBook.All(definition);
                r.Check(kit.Length == AbilityBook.Count(definition) && kit.All(a => a != null && !a.Hidden) && kit.Select(a => a.Id).Distinct().Count() == kit.Length,
                    $"{definition.Name}: the class catalog needs distinct, visible skill ids");
                r.Check(kit.All(a => AbilityBook.IsNative(definition, a) || !a.Signature && !a.IsRecipe),
                    $"{definition.Name}: a foreign signature skill or recipe leaked into the catalog");
                foreach (string id in AbilityBook.StarterIds(definition.Id))
                    r.Check(AbilityBook.IndexOf(definition, id) >= 0, $"{definition.Name}: starting skill {id} is missing");
                foreach (AbilityDefinition ability in kit)
                    r.Check(ability.Cost >= 0 && ability.Cooldown > 0f, $"{ability.Name}: invalid skill cost or cooldown");
            }
            r.Check(AbilityBook.AllDefinitions.Length == 111 && AbilityBook.AllDefinitions.Select(a => a.Id).Distinct().Count() == 111,
                "The workbook catalog must contain 98 skills and 13 recipes with stable unique ids");
            r.Check(AbilityBook.AllDefinitions.Count(a => a.IsRecipe) == 13 && AbilityBook.AllDefinitions.Count(a => a.Hidden) == 1,
                "Recipes and the hidden follower charm must keep their own access rules");
            foreach (AbilityDefinition ability in AbilityBook.AllDefinitions)
            {
                r.Check(ability.Tiers != null && ability.Tiers.Length > 0 && ability.Tiers.Length <= 3 && !string.IsNullOrEmpty(ability.Source),
                    $"{ability.Id}: workbook tiers or source reference missing");
                foreach (AbilityTierDefinition tier in ability.Tiers)
                    r.Check(tier.Hits > 0 && tier.Accuracy >= 0f && tier.Accuracy <= 1f && tier.CriticalChance >= 0f && tier.CriticalChance <= 1f &&
                        tier.StunChance >= 0f && tier.StunChance <= 1f && tier.ConvertChance >= 0f && tier.ConvertChance <= 1f,
                        $"{ability.Id}: invalid hit count or chance");
            }
            foreach (EnemyDefinition foe in Catalog.Enemies)
            {
                r.Check(foe.Moves != null && foe.Moves.Any(m => m.MinPhase == 1), $"{foe.Name}: no opening-phase move");
                foreach (EnemyMove move in foe.Moves)
                    r.Check(move.Windup > 0f && move.Recovery >= 0f && move.MinPhase >= 1 && move.MinPhase <= 3 && move.Radius > 0f,
                        $"{foe.Name}/{move.Name}: invalid attack timing, radius or phase");
                if (foe.Rank >= EnemyRank.Elite)
                    r.Check(foe.Moves.Select(m => m.Kind).Distinct().Count() >= 3 && foe.Moves.Any(m => m.MinPhase > 1),
                        $"{foe.Name}: a major foe needs distinct mechanics and a later-phase move");
            }
            foreach (EnemyAI foe in All<EnemyAI>())
                r.Check(foe.GetComponentInChildren<EnemyAttackTelegraph>(true) != null,
                    $"{foe.name}: readable attack telegraphs are missing");
        }

        /// <summary>Every player-facing string must be drawable by the font the WebGL build embeds.</summary>
        static void CheckText(Report r, GameObject[] roots)
        {
            var strings = new List<(string where, string text)>();
            foreach (Type source in new[] { typeof(GameText), typeof(CityText), typeof(AbilityText), typeof(SkillText) })
            foreach (FieldInfo field in source.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                object value = field.GetValue(null);
                if (value is string s) strings.Add(($"{source.Name}.{field.Name}", s));
                else if (value is string[] array) strings.AddRange(array.Select(a => ($"{source.Name}.{field.Name}", a)));
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
                foreach (Type textSource in new[] { typeof(MenuText), typeof(InventoryText), typeof(SkillText) })
                    foreach (FieldInfo field in textSource.GetFields(BindingFlags.Public | BindingFlags.Static))
                    {
                        object value = field.GetValue(null);
                        if (value is string menuText) CheckSpectral(r, font, $"{textSource.Name}.{field.Name}", menuText);
                        else if (value is string[] labels)
                            foreach (string label in labels) CheckSpectral(r, font, $"{textSource.Name}.{field.Name}", label);
                    }
                foreach (CodexEntry entry in CodexBook.Entries)
                    CheckSpectral(r, font, $"Codex {entry.Id}", entry.Name + entry.Text);
                foreach (ClassCard card in Catalog.Cards)
                    CheckSpectral(r, font, $"Card {card.Art}", card.Name + card.Role + card.PassiveName + card.PassiveText);
                foreach (ClassDefinition c in Catalog.Classes)
                {
                    CheckSpectral(r, font, $"Story of {c.Name}", c.Backstory + c.Goal + c.Ability1.Name + c.Ability1.Description + c.Ability2.Name + c.Ability2.Description);
                    foreach (AbilityDefinition skill in AbilityBook.All(c))
                        CheckSpectral(r, font, $"Ability {c.Name}/{skill.Name}", skill.Name + skill.Description + string.Join(" ", skill.Tiers.Select(t => t.Summary)));
                }
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
            strings.AddRange(Catalog.Classes.SelectMany(AbilityBook.All).Select(a => ("AbilityBook", a.Name + a.Description)));

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

        static void CheckPlayerKits(Report r, PlayerClassRig rig, PlayerTorch torch)
        {
            r.Check(Catalog.Classes.Length == 6 && Catalog.Cards.Length == 6, "All six class definitions and cards must be available");
            if (rig != null)
            {
                var models = new SerializedObject(rig).FindProperty("weaponSets");
                r.Check(models.arraySize == Catalog.Classes.Length * PlayerClassRig.HandsPerClass,
                    "Each of the six classes needs a distinct model for both hands");
                var seen = new HashSet<GameObject>();
                for (int i = 0; i < models.arraySize; i++)
                {
                    var model = models.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    r.Check(model != null && seen.Add(model), $"Class weapon {i} is missing or shares another class's model root");
                    if (model != null)
                        r.Check(model.GetComponentsInChildren<MeshFilter>(true).Any(f => f.sharedMesh != null && f.sharedMesh.vertexCount > 0),
                            $"Class weapon {i} has no visible geometry");
                }
            }
            if (torch == null) return;
            var settings = new SerializedObject(torch);
            foreach (string field in new[] { "warmStrip", "coldStrip" })
            {
                var strip = settings.FindProperty(field).objectReferenceValue as Texture2D;
                r.Check(strip != null && strip.width / 8f >= 128f && strip.width > strip.height,
                    $"Carried torch {field} must contain eight horizontal frames");
                if (strip == null) continue;
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(strip)) as TextureImporter;
                r.Check(importer != null && !importer.mipmapEnabled && importer.filterMode == FilterMode.Point &&
                    importer.wrapMode == TextureWrapMode.Clamp && importer.alphaIsTransparency,
                    $"Carried torch {field} must preserve transparent, crisp frame edges");
            }
            r.Check(torch.Light != null && torch.Light.shadows == LightShadows.None && torch.Light.range <= 24f,
                "The carried torch needs one local light without realtime shadows");
        }

        static void CheckHudAbilityIcons(Report r, HUDController hud)
        {
            if (hud == null) return;
            var settings = new SerializedObject(hud);
            var icons = settings.FindProperty("skillIcons");
            var effects = settings.FindProperty("skillIconEffects");
            var expected = Enum.GetValues(typeof(AbilityEffect)).Cast<AbilityEffect>().Select(e => (int)e).ToArray();
            if (!r.Check(icons != null && effects != null && icons.arraySize == expected.Length && effects.arraySize == icons.arraySize,
                "HUD symbols need an explicit effect id for every generated sprite")) return;
            var menuSprites = new HashSet<Sprite>();
            InventoryPanel inventory = Object.FindAnyObjectByType<InventoryPanel>(FindObjectsInactive.Include);
            if (inventory != null)
            {
                var menuIcons = new SerializedObject(inventory).FindProperty("skillIcons");
                for (int i = 0; i < menuIcons.arraySize; i++)
                    menuSprites.Add(menuIcons.GetArrayElementAtIndex(i).objectReferenceValue as Sprite);
            }
            var mapped = new HashSet<int>();
            var glyphs = new HashSet<ulong>();
            for (int i = 0; i < icons.arraySize; i++)
            {
                int effect = effects.GetArrayElementAtIndex(i).intValue;
                var sprite = icons.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                r.Check(mapped.Add(effect) && expected.Contains(effect) && sprite != null,
                    $"HUD effect {effect} needs one unique, nonempty symbol mapping");
                if (sprite == null) continue;
                r.Check(!menuSprites.Contains(sprite), $"HUD effect {effect} must not reuse the menu's framed icon");
                string path = AssetDatabase.GetAssetPath(sprite);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                r.Check(importer != null && importer.filterMode == FilterMode.Point && importer.alphaIsTransparency &&
                    !importer.mipmapEnabled && importer.wrapMode == TextureWrapMode.Clamp,
                    $"HUD effect {effect} must keep crisp transparent edges");
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    bool loaded = System.IO.File.Exists(path) && texture.LoadImage(System.IO.File.ReadAllBytes(path));
                    if (!r.Check(loaded, $"HUD effect {effect} must have a readable source image")) continue;
                    Color32[] pixels = texture.GetPixels32();
                    ulong fingerprint = 14695981039346656037UL;
                    foreach (Color32 pixel in pixels)
                    {
                        fingerprint = (fingerprint ^ pixel.r) * 1099511628211UL;
                        fingerprint = (fingerprint ^ pixel.g) * 1099511628211UL;
                        fingerprint = (fingerprint ^ pixel.b) * 1099511628211UL;
                        fingerprint = (fingerprint ^ pixel.a) * 1099511628211UL;
                    }
                    r.Check(glyphs.Add(fingerprint), $"HUD effect {effect} needs its own recognizable glyph");
                    bool circular = true;
                    int ink = 0;
                    Vector2 centre = new Vector2((texture.width - 1) * 0.5f, (texture.height - 1) * 0.5f);
                    float radius = Mathf.Min(texture.width, texture.height) * (26f / 64f);
                    for (int y = 0; y < texture.height; y++)
                    for (int x = 0; x < texture.width; x++)
                    {
                        if (pixels[y * texture.width + x].a <= 16) continue;
                        ink++;
                        if ((new Vector2(x, y) - centre).sqrMagnitude > radius * radius) circular = false;
                    }
                    r.Check(circular && ink > 40 && ink < pixels.Length / 2,
                        $"HUD effect {effect} must have visible artwork inside a transparent circular margin, without a square frame");
                }
                finally { Object.DestroyImmediate(texture); }
            }
            r.Check(expected.All(mapped.Contains), "The HUD symbol mapping must cover every ability effect, regardless of its numeric id");
            for (int slot = 1; slot <= 2; slot++)
            {
                var image = settings.FindProperty("ability" + slot + "Icon").objectReferenceValue as UnityEngine.UI.Image;
                var label = settings.FindProperty("ability" + slot + "Label").objectReferenceValue as UnityEngine.UI.Text;
                if (!r.Check(image != null && label != null, $"HUD ability slot {slot} needs its symbol and caption")) continue;
                var button = image.rectTransform.parent as RectTransform;
                r.Check(button != null && image.rectTransform.anchoredPosition == Vector2.zero && image.preserveAspect &&
                    image.rectTransform.sizeDelta.x <= button.sizeDelta.x * 0.65f && !image.raycastTarget,
                    $"HUD ability slot {slot} must centre the symbol with room inside the stone ring");
                if (button == null) continue;
                r.Check(label.rectTransform.parent == button && label.rectTransform.anchoredPosition.y + label.rectTransform.sizeDelta.y * 0.5f < -button.sizeDelta.y * 0.5f &&
                    label.resizeTextForBestFit && label.verticalOverflow == VerticalWrapMode.Truncate && !label.raycastTarget,
                    $"HUD ability slot {slot} must keep its readable caption below the stone ring without blocking touches");
            }
        }

        static void CheckInventory(Report r, InventoryPanel inventory, SecretStatsPanel secret, HoldMenuButton hold)
        {
            void Count(Object target, string field, int expected)
            {
                SerializedProperty array = new SerializedObject(target).FindProperty(field);
                r.Check(array != null && array.isArray && array.arraySize == expected, $"{target.name}.{field} must have {expected} entries");
            }
            if (inventory != null)
            {
                Count(inventory, "portraits", Catalog.Classes.Length);
                Count(inventory, "statistics", 10);
                Count(inventory, "statButtons", 6);
                Count(inventory, "skillIconEffects", Enum.GetValues(typeof(AbilityEffect)).Length);
                foreach (string field in new[] { "abilityLabels", "abilityDescriptions", "upgradeLabels", "upgradeButtons", "abilityIcons", "equipQButtons", "equipRButtons", "equipQLabels", "equipRLabels" }) Count(inventory, field, 4);
                Count(inventory, "skillIcons", Enum.GetValues(typeof(AbilityEffect)).Length);
                Count(inventory, "classWeaponIcons", Catalog.Classes.Length);
                Count(inventory, "goodsWeaponIcons", Catalog.ItemsOf(ItemSlot.Weapon).Length);
                var so = new SerializedObject(inventory);
                r.Check(so.FindProperty("inventoryPage").objectReferenceValue != so.FindProperty("abilitiesPage").objectReferenceValue,
                    "Inventory and abilities must have separate pages");
            }
            if (secret != null)
            {
                Count(secret, "fields", InventoryText.DebugLabels.Length);
                Count(secret, "unlockButtons", 2);
                Count(secret, "unlockLabels", 2);
                var fields = new SerializedObject(secret).FindProperty("fields");
                for (int i = 0; i < fields.arraySize; i++)
                {
                    var input = fields.GetArrayElementAtIndex(i).objectReferenceValue as UnityEngine.UI.InputField;
                    r.Check(input != null && input.contentType == UnityEngine.UI.InputField.ContentType.IntegerNumber,
                        $"Hidden stat {i} must accept whole numbers");
                }
            }
            if (hold == null) return;
            r.Check(Mathf.Approximately(HoldMenuButton.HoldSeconds, 5f), "The hidden editor must require a continuous five-second hold");
            var button = hold.GetComponent<UnityEngine.UI.Button>();
            r.Check(button != null && button.onClick.GetPersistentEventCount() == 1 && button.onClick.GetPersistentTarget(0) == hold &&
                button.onClick.GetPersistentMethodName(0) == nameof(HoldMenuButton.Click),
                "The menu button must route its click through the hold handler exactly once");
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

            // Ten boundary walls close the inverted T (CheckNorthLand proves they leave no gap).
            int walls = player.gameObject.scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<BoxCollider>(true))
                .Count(b => b.gameObject.name == "Boundary Wall");
            r.Check(walls == 10, $"Expected 10 world boundary walls, found {walls}");
        }

        /// <summary>
        /// The north land: the wall ring is closed, the Old Toll Gate is the only way through the ridge, the road to the city
        /// gate is open ground a horse fits along, the city portal stands at the new gate, and the instanced scatter is sound.
        /// </summary>
        static void CheckNorthLand(Report r, Zone[] zones, ZonePortal[] portals, TollGate[] tollGates, ScatterRenderer[] scatterRenderers, GameObject[] roots)
        {
            if (!r.Check(zones.Length > ZoneNumbers.City && tollGates.Length == 1 && scatterRenderers.Length == 1,
                "The valley needs one Old Toll Gate and one scatter renderer")) return;
            Zone valley = zones[ZoneNumbers.Valley];
            TollGate tollGate = tollGates[0];
            var barrier = new SerializedObject(tollGate).FindProperty("barrier").objectReferenceValue as BoxCollider;
            r.Check(tollGate.RequiredFlag == StoryDirector.DeadFlag("knight") && barrier != null && barrier.enabled && !barrier.isTrigger &&
                Mathf.Abs(barrier.bounds.center.x) < 0.5f && Mathf.Abs(barrier.bounds.center.z - 92.2f) < 1f,
                "The Old Toll Gate at (0, 92) must be barred by a solid door until the knight's flag");

            var hits = new RaycastHit[32];
            bool Crosses(Vector3 from, Vector3 to, Func<Collider, bool> blocks)
            {
                int count = Physics.RaycastNonAlloc(from, to - from, hits, (to - from).magnitude, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++) if (blocks(hits[i].collider)) return false;
                return true;
            }
            bool IsWall(Collider c) => c.name == "Boundary Wall";
            bool IsTerrain(Collider c) => c.name.StartsWith("Terrain_");

            // A flood over a 4 m grid from the spawn. Escape means reaching the grid's edge.
            const float step = 4f, x0 = -300f, z0 = -150f;
            const int nx = 151, nz = 204;
            int Flood(Func<int, int, Vector3> at, Func<Collider, bool> blocks, out bool escaped, out bool[,] seen)
            {
                seen = new bool[nx, nz];
                var queue = new Queue<Vector2Int>();
                var start = new Vector2Int(Mathf.RoundToInt((0f - x0) / step), Mathf.RoundToInt((-86f - z0) / step));
                seen[start.x, start.y] = true;
                queue.Enqueue(start);
                escaped = false;
                int count = 0;
                var offsets = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();
                    count++;
                    if (c.x == 0 || c.y == 0 || c.x == nx - 1 || c.y == nz - 1) { escaped = true; continue; }
                    foreach (Vector2Int o in offsets)
                    {
                        Vector2Int n = c + o;
                        if (seen[n.x, n.y] || !Crosses(at(c.x, c.y), at(n.x, n.y), blocks)) continue;
                        seen[n.x, n.y] = true;
                        queue.Enqueue(n);
                    }
                }
                return count;
            }
            Vector2Int Cell(float x, float z) => new Vector2Int(Mathf.RoundToInt((x - x0) / step), Mathf.RoundToInt((z - z0) / step));

            // 1. High above everything but the boundary walls, the ring alone must hold.
            int ringCells = Flood((i, j) => new Vector3(x0 + i * step, 100f, z0 + j * step), IsWall, out bool leaked, out _);
            float area = ringCells * step * step;
            r.Check(!leaked && area > 270000f && area < 310000f, $"The boundary walls must close the inverted T (escaped {leaked}, {area:F0} m2 inside)");

            // 2. On the ground, the walls, gate walls and shut doors part the valley from the north land; open doors join them.
            var ground = new float[nx, nz];
            var down = new RaycastHit[16];
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                int count = Physics.RaycastNonAlloc(new Vector3(x0 + i * step, 300f, z0 + j * step), Vector3.down, down, 600f, ~0, QueryTriggerInteraction.Ignore);
                ground[i, j] = -20f;
                for (int k = 0; k < count; k++) if (IsTerrain(down[k].collider)) ground[i, j] = down[k].point.y;
            }
            Vector3 OnGround(int i, int j) => new Vector3(x0 + i * step, ground[i, j] + 1f, z0 + j * step);
            bool GateBlocks(Collider c) => IsWall(c) || c.name == "Gate Wall" || c.name == "Gate Pillar";
            Vector2Int north = Cell(0f, 300f), pocket = Cell(0f, 100f);
            Flood(OnGround, c => GateBlocks(c) || c == barrier, out bool shutLeak, out bool[,] shut);
            r.Check(!shutLeak && !shut[north.x, north.y] && !shut[pocket.x, pocket.y], "With the toll gate shut, no way on the ground leads from the valley to the north land");
            Flood(OnGround, GateBlocks, out bool openLeak, out bool[,] open);
            Vector2Int cityGate = Cell(0f, 584f);
            r.Check(!openLeak && open[north.x, north.y] && open[cityGate.x, cityGate.y], "Through the open toll gate the north land and the city gate are reachable");

            // 3. The main road and the side roads, sampled every 2 m: terrain under them, no slope over 35 degrees, and a horse-wide
            // sweep that hits nothing. The lake road is swept to the water's edge, where the jetty takes over.
            Vector2[] road = ValleyGenerator.CatmullRom(ValleyGenerator.MainRoad);
            var sweep = new RaycastHit[16];
            string SweepRoad(Vector2[] spline, out int sampleCount)
            {
                var samples = new List<Vector3>();
                for (int i = 0; i < spline.Length - 1; i++)
                {
                    float length = Vector2.Distance(spline[i], spline[i + 1]);
                    for (float s = 0f; s < length; s += 2f)
                    {
                        Vector2 p = Vector2.Lerp(spline[i], spline[i + 1], s / length);
                        if (ValleyGenerator.InLake(p.x, p.y, 0f)) break;
                        int count = Physics.RaycastNonAlloc(new Vector3(p.x, 300f, p.y), Vector3.down, down, 600f, ~0, QueryTriggerInteraction.Ignore);
                        float y = float.NaN;
                        for (int k = 0; k < count; k++) if (IsTerrain(down[k].collider)) y = down[k].point.y;
                        samples.Add(new Vector3(p.x, y, p.y));
                    }
                }
                sampleCount = samples.Count;
                for (int i = 0; i < samples.Count - 1; i++)
                {
                    Vector3 a = samples[i], b = samples[i + 1];
                    if (float.IsNaN(a.y) || float.IsNaN(b.y)) return $"no ground at {a}";
                    Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                    if (flat.sqrMagnitude < 0.01f) continue;
                    float slope = Mathf.Atan2(Mathf.Abs(b.y - a.y), flat.magnitude) * Mathf.Rad2Deg;
                    if (slope > 35f) return $"a {slope:F0} degree slope at {a}";
                    int count = Physics.CapsuleCastNonAlloc(a + Vector3.up * 1.2f, a + Vector3.up * 2.2f, 0.6f, flat.normalized, sweep, flat.magnitude, ~0, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < count; k++)
                    {
                        Collider c = sweep[k].collider;
                        if (IsTerrain(c) || c == barrier) continue;
                        return $"{c.name} at {a}";
                    }
                }
                return null;
            }
            string roadProblem = SweepRoad(road, out int mainSamples);
            r.Check(mainSamples > 200 && roadProblem == null, $"The road from the toll gate to the city gate must be open ground ({mainSamples} samples; {roadProblem ?? "clear"})");
            foreach (Vector2[] spur in ValleyGenerator.Spurs)
            {
                string spurProblem = SweepRoad(ValleyGenerator.CatmullRom(spur), out int spurSamples);
                r.Check(spurSamples > 10 && spurProblem == null, $"The side road from {spur[0]} to {spur[spur.Length - 1]} must be open ground ({spurSamples} samples; {spurProblem ?? "clear"})");
            }

            // 3b. The places: every building and landmark reaches the ground at each corner and is not buried to its roof.
            float TerrainAt(Vector3 p)
            {
                int count = Physics.RaycastNonAlloc(new Vector3(p.x, 300f, p.z), Vector3.down, down, 600f, ~0, QueryTriggerInteraction.Ignore);
                float y = float.NaN;
                for (int k = 0; k < count; k++) if (IsTerrain(down[k].collider)) y = down[k].point.y;
                return y;
            }
            Transform footingRoot = valley.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Footings");
            if (r.Check(footingRoot != null && footingRoot.childCount >= 30, "The north land's places must record their footprints"))
                foreach (Transform footing in footingRoot)
                {
                    float low = float.MaxValue, high = float.MinValue;
                    foreach (Vector3 corner in new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f), Vector3.zero })
                    {
                        float y = TerrainAt(footing.TransformPoint(corner));
                        low = Mathf.Min(low, y);
                        high = Mathf.Max(high, y);
                    }
                    float baseY = footing.position.y, height = footing.lossyScale.y;
                    r.Check(baseY <= low + 0.05f && baseY + height - high >= Mathf.Min(2f, height * 0.5f),
                        $"{footing.name} at {footing.position} must rest on the ground (base {baseY:F2}, ground {low:F2}..{high:F2}, height {height:F1})");
                }

            // 3c. Tükörtó: with the gate open, the flood from the spawn, stopped only by walls, the shore ring and the jetty, never
            // reaches the water except along the jetty's deck.
            Flood(OnGround, c => GateBlocks(c) || c.name == "Lake Shore" || c.name.StartsWith("Jetty"), out bool lakeLeak, out bool[,] onFoot);
            int wet = 0;
            Vector2 firstWet = default;
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if (!onFoot[i, j]) continue;
                var p = new Vector2(x0 + i * step, z0 + j * step);
                if (!ValleyGenerator.InLake(p.x, p.y, -3f)) continue;
                Vector2 rel = p - ValleyGenerator.JettyStart;
                float along = Vector2.Dot(rel, ValleyGenerator.JettyAxis), across = Mathf.Abs(Vector2.Dot(rel, new Vector2(-ValleyGenerator.JettyAxis.y, ValleyGenerator.JettyAxis.x)));
                if (across < ValleyGenerator.JettyHalfWidth && along < ValleyGenerator.JettyLength + 0.5f) continue;
                wet++;
                firstWet = p;
            }
            r.Check(!lakeLeak && wet == 0 && valley.GetComponentsInChildren<Collider>(true).Count(c => c.name == "Lake Shore") > 100,
                $"The lake shore must be closed: no way on foot into the water ({wet} cells reached, e.g. {firstWet})");

            // 3d. Four horse posts, each with a free 3 m standing spot beside it.
            Transform postRoot = valley.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Horse Posts");
            for (int i = 1; i <= 4; i++)
            {
                Transform post = postRoot != null ? postRoot.Find($"Horse Post {i}") : null;
                Transform spot = post != null ? post.Find("Standing Spot") : null;
                if (!r.Check(post != null && spot != null && post.GetComponent<Collider>() != null, $"Horse Post {i} with its standing spot is missing")) continue;
                float spotGround = TerrainAt(spot.position);
                var blockers = Physics.OverlapBox(spot.position + Vector3.up * 1.45f, new Vector3(1.5f, 1f, 1.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => !IsTerrain(c)).ToArray();
                r.Check(Mathf.Abs(spotGround - spot.position.y) < 0.3f && Vector3.Distance(post.position, spot.position) < 4f && blockers.Length == 0,
                    $"Horse Post {i}'s standing spot must be free ground ({string.Join(", ", blockers.Select(c => c.name))})");
            }
            CheckHorse(r, zones, portals, roots, postRoot);

            // 4. The city portal stands at the new gate, with its arrival just south of it, and it is always open.
            ZonePortal cityPortal = portals.FirstOrDefault(p => p.TargetZone == ZoneNumbers.City && p.transform.IsChildOf(valley.transform));
            r.Check(cityPortal != null && Mathf.Abs(cityPortal.transform.position.z - 590f) < 2f && string.IsNullOrEmpty(cityPortal.RequiredFlag) &&
                valley.Exit != null && Mathf.Abs(valley.Exit.position.z - 585f) < 1f && valley.Objective != null && Mathf.Abs(valley.Objective.position.z - 590f) < 1f,
                "The always-open portal to Hollowgate, its arrival and its objective must be at the new city gate (0, 590)");
            var atmosphere = roots.SelectMany(go => go.GetComponentsInChildren<AtmosphereController>(true)).FirstOrDefault();
            var evening = atmosphere != null ? new SerializedObject(atmosphere) : null;
            r.Check(evening != null && evening.FindProperty("eveningStartZ").floatValue < -80f && Mathf.Abs(evening.FindProperty("eveningEndZ").floatValue - 590f) < 2f,
                "Evening must spread over the whole walk, from the spawn to the city gate");

            // 5. The scatter: instancing materials, caps, every instance on the ground, trees off the road, and a capsule for every tree inside the walls.
            ScatterData data = new SerializedObject(scatterRenderers[0]).FindProperty("data").objectReferenceValue as ScatterData;
            if (!r.Check(data != null && data.Instances.Length > 0 && AssetDatabase.Contains(data), "The scatter renderer needs its generated data asset")) return;
            r.Check(data.Kinds.All(k => k.Meshes.Length == k.Materials.Length && k.Meshes.All(m => m != null && AssetDatabase.Contains(m)) &&
                k.Materials.All(m => m != null && m.enableInstancing && AssetDatabase.Contains(m))), "Every scatter kind needs saved meshes and instancing-enabled materials");
            var byKind = new int[data.Kinds.Length];
            foreach (var g in data.Groups) byKind[g.Kind] += g.Count;
            int Count(string prefix) => data.Kinds.Select((k, i) => k.Name.StartsWith(prefix) ? byKind[i] : 0).Sum();
            int trees = Count("Pine") + Count("Autumn"), patches = Count("Grass") + Count("Flowers") + Count("Wheat");
            Debug.Log($"[AshenRealm][World] Scatter: {Count("Pine")} pines, {Count("Autumn")} broadleaf trees, {Count("Grass")} grass, " +
                $"{Count("Flowers")} flowers, {Count("Wheat")} wheat in {data.Groups.Length} groups of {data.Kinds.Length} kinds");
            r.Check(trees > 1000 && trees <= 4000 && patches > 5000 && patches <= 25000 && Count("Wheat") > 1000, $"Scatter within its caps ({trees} trees, {patches} patches)");
            r.Check(data.Groups.Sum(g => g.Count) == data.Instances.Length && data.Groups.All(g => g.Start >= 0 && g.Start + g.Count <= data.Instances.Length),
                "Scatter groups must cover the instances exactly");
            int floating = 0, onRoad = 0, unblocked = 0;
            Vector3 firstBad = default;
            foreach (var g in data.Groups)
            {
                bool tree = data.Kinds[g.Kind].Meshes.Length == 2;
                for (int i = g.Start; i < g.Start + g.Count; i++)
                {
                    Vector3 p = data.Instances[i].Position;
                    int count = Physics.RaycastNonAlloc(new Vector3(p.x, p.y + 30f, p.z), Vector3.down, down, 60f, ~0, QueryTriggerInteraction.Ignore);
                    float best = float.MaxValue;
                    for (int k = 0; k < count; k++) if (IsTerrain(down[k].collider)) best = Mathf.Min(best, Mathf.Abs(down[k].point.y - p.y));
                    if (best > 0.3f) { floating++; firstBad = p; }
                    if (!tree) continue;
                    var flatP = new Vector2(p.x, p.z);
                    for (int s = 0; s < road.Length - 1; s++)
                    {
                        Vector2 a = road[s], ab = road[s + 1] - a;
                        float t = Mathf.Clamp01(Vector2.Dot(flatP - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
                        if ((a + ab * t - flatP).sqrMagnitude < 9f) { onRoad++; break; }
                    }
                    if (ValleyGenerator.Walkable(p.x, p.z) && !Physics.OverlapSphere(p + Vector3.up * 1f, 0.05f, ~0, QueryTriggerInteraction.Ignore)
                        .Any(c => c is CapsuleCollider && c.transform.name.StartsWith("Tree Colliders")))
                        unblocked++;
                }
            }
            r.Check(floating == 0, $"Every scatter instance must rest on the terrain ({floating} do not, e.g. {firstBad})");
            r.Check(onRoad == 0, $"No tree may stand within 3 m of the main road ({onRoad} do)");
            r.Check(unblocked == 0, $"Every tree inside the walls needs its capsule ({unblocked} have none)");
            r.Check(valley.GetComponentsInChildren<CapsuleCollider>(true).Count(c => c.transform.name.StartsWith("Tree Colliders")) >= trees / 2 &&
                valley.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Tree Colliders")) <= ValleyGenerator.ColliderCells,
                "Tree capsules are gathered on one object per 66 m cell");
            long vertices = valley.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).Sum(m => (long)m.sharedMesh.vertexCount);
            r.Check(vertices <= 450000, $"The valley's merged meshes must stay within 450,000 vertices ({vertices})");
        }

        /// <summary>
        /// The north land's people and the city's newcomers: each on the ground or the deck, inside nothing, one renderer, out
        /// of doorways and away from what can be used; look-alikes share a mesh, and all their meshes stay small.
        /// </summary>
        /// <summary>
        /// The horse: one, in the valley, wired to the rider, the zones and the interactor's dismount; every post's standing
        /// spot takes the mounted body and keeps 4 m from where doors let the player out; Hilda sells it; its mesh stays small.
        /// </summary>
        static void CheckHorse(Report r, Zone[] zones, ZonePortal[] portals, GameObject[] roots, Transform postRoot)
        {
            RidingHorse[] horses = roots.SelectMany(go => go.GetComponentsInChildren<RidingHorse>(true)).ToArray();
            if (!r.Check(horses.Length == 1 && postRoot != null, $"Expected one riding horse and the horse posts, found {horses.Length}")) return;
            RidingHorse horse = horses[0];
            Zone valley = zones[ZoneNumbers.Valley];
            var wiring = new SerializedObject(horse);
            Transform[] spots = Enumerable.Range(0, wiring.FindProperty("posts").arraySize)
                .Select(i => wiring.FindProperty("posts").GetArrayElementAtIndex(i).objectReferenceValue as Transform).ToArray();
            Transform[] expected = Enumerable.Range(1, 4).Select(i => postRoot.Find($"Horse Post {i}/Standing Spot")).ToArray();
            var interactor = roots.SelectMany(go => go.GetComponentsInChildren<PlayerInteractor>(true)).FirstOrDefault();
            var zoneManager = roots.SelectMany(go => go.GetComponentsInChildren<ZoneManager>(true)).FirstOrDefault();
            r.Check(horse.transform.IsChildOf(valley.transform) && spots.SequenceEqual(expected) &&
                wiring.FindProperty("rail").objectReferenceValue == expected[2] &&
                interactor != null && new SerializedObject(interactor).FindProperty("fallback").objectReferenceValue == horse &&
                zoneManager != null && new SerializedObject(zoneManager).FindProperty("horse").objectReferenceValue == horse,
                "The riding horse lives in the valley, knows the four standing spots and the stable's rail, and is the interactor's dismount");
            var model = wiring.FindProperty("model").objectReferenceValue as GameObject;
            var body = wiring.FindProperty("body").objectReferenceValue as BoxCollider;
            r.Check(model != null && !model.activeSelf && body != null && !body.enabled && !body.isTrigger,
                "The riding horse is hidden and not solid until it is bought");
            int vertices = horse.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh != null ? f.sharedMesh.vertexCount : 0);
            int renderers = horse.GetComponentsInChildren<Renderer>(true).Length;
            r.Check(vertices > 0 && vertices <= HorseMeshes.MaxRiddenVertices && renderers <= 9,
                $"The riding horse stays within {HorseMeshes.MaxRiddenVertices} vertices and nine renderers ({vertices}, {renderers})");
            r.Check(StoryText.Npc("stablemaster").Service == NpcService.Stable &&
                valley.GetComponentsInChildren<NpcActor>(true).Any(n => n.NpcId == "stablemaster"),
                "Patkós Hilda sells the horse at the stable");
            Transform[] paddock = valley.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Paddock Horse ")).ToArray();
            r.Check(paddock.Length == 3 && paddock.Select(t => t.GetComponent<MeshFilter>().sharedMesh).Distinct().Count() >= 2 &&
                valley.GetComponentsInChildren<Collider>(true).Count(c => c.name == "Paddock Horse") == 3,
                "Three still horses stand in the paddock, in more than one coat, each solid");

            // Where a door, the gate or a respawn puts the player in the valley.
            var arrivals = new List<Transform> { valley.Entry, valley.Exit };
            foreach (ZonePortal portal in portals.Where(p => p.TargetZone == ZoneNumbers.Valley))
                if (new SerializedObject(portal).FindProperty("arrival").objectReferenceValue is Transform at) arrivals.Add(at);
            foreach (Transform spot in expected.Where(s => s != null))
            {
                Vector3 feet = spot.position;
                Collider[] blockers = Physics.OverlapCapsule(feet + Vector3.up * 0.55f, feet + Vector3.up * 2.1f, 0.5f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => !c.name.StartsWith("Terrain_") && !c.transform.IsChildOf(horse.transform)).ToArray();
                float nearest = arrivals.Min(a => Vector3.Distance(a.position, feet));
                r.Check(blockers.Length == 0 && nearest >= 4f,
                    $"{spot.parent.name}'s standing spot takes a mounted rider and is {nearest:F1} m (at least 4) from every arrival ({string.Join(", ", blockers.Select(c => c.name))})");
            }
            var homeReturns = portals.Where(p => p.TargetZone == ZoneNumbers.Valley && p.GetComponentInParent<Zone>().name.StartsWith("Zone Hamlet Home"))
                .Select(p => new SerializedObject(p).FindProperty("arrival").objectReferenceValue as Transform).Where(t => t != null).ToArray();
            float farthest = homeReturns.Length > 0 && expected[3] != null ? homeReturns.Max(t => Vector3.Distance(t.position, expected[3].position)) : float.MaxValue;
            r.Check(homeReturns.Length == 3 && farthest <= 25f, $"Leaving any hamlet home, the horse at Horse Post 4 is within 25 m ({farthest:F1} m)");
        }

        static void CheckNewPeople(Report r, Zone[] zones)
        {
            if (zones.Length <= ZoneNumbers.City) return;
            string SpeakerId(Component person) => person.TryGetComponent(out NpcActor speaker) ? speaker.NpcId : null;
            string[] oldCityPeople = { "merchant", "gravedigger", "crier", "widow" };
            NpcIdleAnimator[] people = zones[ZoneNumbers.Valley].GetComponentsInChildren<NpcIdleAnimator>(true).Where(p => p.transform.position.z > 90f)
                .Concat(zones[ZoneNumbers.City].GetComponentsInChildren<NpcIdleAnimator>(true)
                    .Where(p => !oldCityPeople.Contains(SpeakerId(p)))).ToArray();
            int talkers = people.Count(p => p.GetComponent<NpcActor>() != null);
            r.Check(talkers == 7 && people.Length - talkers >= 18, $"The valley and the city need their seven new speakers and their other people ({talkers}, {people.Length - talkers})");

            Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);
            var uses = zones[ZoneNumbers.Valley].GetComponentsInChildren<Collider>(true).Concat(zones[ZoneNumbers.City].GetComponentsInChildren<Collider>(true))
                .Where(c => c.GetComponent<ZonePortal>() != null || c.GetComponent<CityActivity>() != null || c.GetComponent<LoreInspectable>() != null ||
                    c.GetComponent<NpcActor>() != null).ToArray();
            Transform[] doorways = zones[ZoneNumbers.Valley].GetComponentsInChildren<Transform>(true).Where(t => t.name == "Doorway").ToArray();
            r.Check(doorways.Length == 3, $"The stable, barn and watchtower doors must be marked ({doorways.Length})");
            foreach (NpcIdleAnimator person in people)
            {
                Vector3 feet = person.transform.position;
                Collider own = person.GetComponent<Collider>();
                RaycastHit under = Physics.RaycastAll(feet + Vector3.up * 0.6f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider != own).OrderBy(h => h.distance).FirstOrDefault();
                bool deck = SpeakerId(person) == "fisher" || person.name == "Net Mender";
                r.Check(under.collider != null && Mathf.Abs(under.point.y - feet.y) < 0.05f && (!deck || under.collider.name == "Jetty Deck"),
                    $"{person.name} must stand on the {(deck ? "jetty's deck" : "ground")} ({(under.collider != null ? $"{under.collider.name} {under.point.y - feet.y:F3} m" : "nothing")} under the feet)");
                var inside = Physics.OverlapCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.5f, 0.3f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => c != own && !c.name.StartsWith("Terrain_") && c.name != "Ground").Select(c => c.name).ToArray();
                r.Check(inside.Length == 0, $"{person.name} must not stand inside {string.Join(", ", inside)}");
                r.Check(person.GetComponentsInChildren<Renderer>(true).Length == 1, $"{person.name} must be drawn by one renderer");
                Collider near = uses.Where(c => c != own).OrderBy(c => Vector2.Distance(Flat(c.ClosestPoint(feet + Vector3.up)), Flat(feet))).FirstOrDefault();
                float gap = near != null ? Vector2.Distance(Flat(near.ClosestPoint(feet + Vector3.up)), Flat(feet)) : float.MaxValue;
                r.Check(gap >= 1f, $"{person.name} stands {gap:F2} m from {near?.name}, in the way of using it");
                float door = doorways.Select(d => Vector2.Distance(Flat(d.position), Flat(feet))).DefaultIfEmpty(float.MaxValue).Min();
                r.Check(door >= 2f, $"{person.name} stands {door:F1} m from a doorway");
            }

            Mesh MeshOf(NpcIdleAnimator p) => p.GetComponentInChildren<MeshFilter>(true)?.sharedMesh;
            int Looks(string prefix) => people.Where(p => p.name.StartsWith(prefix)).Select(MeshOf).Distinct().Count();
            r.Check(people.Count(p => p.name.StartsWith("Reaper")) == 3 && Looks("Reaper") == 1 && people.Count(p => p.name.StartsWith("Pilgrim")) == 2 && Looks("Pilgrim") == 1,
                "The three reapers and the two pilgrims must each share one mesh");
            Mesh[] meshes = people.Select(MeshOf).Where(m => m != null).Distinct().ToArray();
            long vertices = meshes.Sum(m => (long)m.vertexCount);
            Debug.Log($"[AshenRealm][World] New people: {people.Length} ({talkers} speak) on {meshes.Length} mesh assets, {vertices} vertices, " +
                $"largest {meshes.Select(m => m.vertexCount).DefaultIfEmpty(0).Max()}");
            r.Check(meshes.All(m => AssetDatabase.Contains(m)) && vertices <= 60000, $"The new people's meshes must be saved and stay within 60,000 vertices ({vertices})");
        }

        /// <summary>The zones, their portals and everything that stands in them.</summary>
        static void CheckWorld(Report r, Zone[] zones, ZonePortal[] portals, EnemyAI[] enemies, NpcActor[] npcs, EncounterSite[] encounters)
        {
            r.Check(zones.Length == StoryText.ZoneTitles.Length, $"Expected {StoryText.ZoneTitles.Length} zones, found {zones.Length}");
            r.Check(zones.Length == ZoneNumbers.FirstHome + ZoneNumbers.HomeCount, "The valley, city, three public houses, five depths and three cottages need 13 zones");

            bool OnGround(Vector3 p) => Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.point.y - p.y) < 1f;

            foreach (var zone in zones)
            {
                var meshes = zone.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null).ToArray();
                Debug.Log($"[AshenRealm][World] {zone.name}: {meshes.Length} meshes, {meshes.Sum(m => (long)m.sharedMesh.vertexCount)} vertices, " +
                    $"{zone.GetComponentsInChildren<Collider>(true).Length} colliders");
                r.Check(!string.IsNullOrEmpty(zone.Title) && !string.IsNullOrEmpty(zone.Subtitle), $"{zone.name} has no title card");
                r.Check(zone.Entry != null && OnGround(zone.Entry.position), $"{zone.name}: entry is not on the ground");
                r.Check(zone.Exit != null && OnGround(zone.Exit.position), $"{zone.name}: stair arrival is not on the ground");
                r.Check(zone.Objective != null, $"{zone.name} has no compass objective");
                if (zone.Entry == null) continue;

                // Arriving players must not stand inside a wall.
                Vector3 feet = zone.Entry.position + Vector3.up * 0.4f;
                var overlaps = Physics.OverlapCapsule(feet, feet + Vector3.up * 1.1f, 0.33f, ~0, QueryTriggerInteraction.Ignore);
                r.Check(overlaps.All(c => c.GetComponent<CharacterController>() != null), $"{zone.name}: entry overlaps {string.Join(", ", overlaps.Select(c => c.name))}");
                if (zone.Exit != null)
                {
                    Vector3 returnFeet = zone.Exit.position + Vector3.up * 0.4f;
                    var returnOverlaps = Physics.OverlapCapsule(returnFeet, returnFeet + Vector3.up * 1.1f, 0.33f, ~0, QueryTriggerInteraction.Ignore);
                    r.Check(returnOverlaps.All(c => c.GetComponent<CharacterController>() != null),
                        $"{zone.name}: returning player overlaps {string.Join(", ", returnOverlaps.Select(c => c.name))}");
                }

                Transform[] views = zone.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.parent != null && t.parent.name == "Vistas").ToArray();
                r.Check(views.Length >= 2, $"{zone.name}: visual review needs at least two authored views");
                r.Check(views.Select(v => v.name).Distinct().Count() == views.Length, $"{zone.name}: visual review names must be unique");
                foreach (Transform view in views)
                {
                    Vector3 p = view.position;
                    r.Check(!float.IsNaN(p.sqrMagnitude) && !float.IsInfinity(p.sqrMagnitude), $"{zone.name}/{view.name}: invalid camera position");
                    var obstructions = Physics.OverlapSphere(p, 0.12f, ~0, QueryTriggerInteraction.Ignore)
                        .Where(c => c.GetComponentInParent<Health>() == null).ToArray();
                    r.Check(obstructions.Length == 0,
                        $"{zone.name}/{view.name}: review camera is inside {string.Join(", ", obstructions.Select(c => c.name))}");
                }
                var discoveries = zone.GetComponentsInChildren<LoreInspectable>(true).Where(s => s.name.StartsWith("Discovery ")).ToArray();
                r.Check(discoveries.Length > 0, $"{zone.name}: the redesigned place has no environmental discovery");
                foreach (var discovery in discoveries)
                    r.Check(ScenicAccess.TryApproach(discovery, out _, out _), $"{zone.name}/{discovery.name}: no standing position can target the discovery within interaction reach");
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

            foreach (var npc in npcs)
            {
                r.Check(StoryText.Npc(npc.NpcId) != null, $"{npc.name}: unknown npc id '{npc.NpcId}'");
                r.Check(npc.GetComponent<NpcIdleAnimator>() != null, $"{npc.name} needs a living idle pose");
            }
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
            CheckArrivalAndHomes(r, zones, portals);
        }

        static void CheckArrivalAndHomes(Report r, Zone[] zones, ZonePortal[] portals)
        {
            if (zones.Length < ZoneNumbers.FirstHome + ZoneNumbers.HomeCount) return;
            Zone valley = zones[ZoneNumbers.Valley];
            Transform[] valleyObjects = valley.GetComponentsInChildren<Transform>(true);
            var blocker = valleyObjects.FirstOrDefault(t => t.name == "Closed Arrival Tunnel")?.GetComponent<BoxCollider>();
            if (r.Check(blocker != null && !blocker.isTrigger, "The dark arrival tunnel needs a solid threshold"))
            {
                Vector3 eye = new Vector3(blocker.bounds.center.x, blocker.bounds.min.y + 1.6f, blocker.bounds.max.z + 4f);
                r.Check(Physics.Raycast(eye, Vector3.back, out RaycastHit hit, 9f, ~0, QueryTriggerInteraction.Ignore) && hit.collider == blocker,
                    "The tunnel must stop a player approaching from the valley");
                var darkRecess = valleyObjects.FirstOrDefault(t => t.name == "Arrival Road Geometry")?.GetComponent<MeshRenderer>();
                r.Check(darkRecess != null && darkRecess.sharedMaterial != null && darkRecess.sharedMaterial.renderQueue < 2500 &&
                    darkRecess.bounds.Contains(blocker.bounds.center), "The arrival tunnel must have an opaque dark recess behind its threshold");
            }
            r.Check(valleyObjects.Count(t => t.name == "Caravan Wagon") == 2, "Two waiting wagons must mark the caravan's arrival");
            foreach (string name in new[] { "Caravan Driver", "Caravan Guard", "Caravan Traveller" })
            {
                Transform person = valleyObjects.FirstOrDefault(t => t.name == name);
                r.Check(person != null && person.GetComponent<NpcIdleAnimator>() != null && person.GetComponent<CapsuleCollider>() != null,
                    $"{name} must wait beside the caravan with a visible idle pose and a collider");
            }

            for (int i = 0; i < ZoneNumbers.HomeCount; i++)
            {
                int index = ZoneNumbers.FirstHome + i;
                Zone home = zones[index];
                r.Check(home.Indoors && !home.Underground, $"{home.name} must be an above-ground interior");
                ZonePortal[] doorsIn = valley.GetComponentsInChildren<ZonePortal>(true).Where(p => p.TargetZone == index).ToArray();
                ZonePortal[] doorsOut = home.GetComponentsInChildren<ZonePortal>(true).Where(p => p.TargetZone == ZoneNumbers.Valley).ToArray();
                r.Check(doorsIn.Length == 1 && doorsOut.Length == 1, $"{home.name} needs one entrance and one return door");
                if (doorsIn.Length == 1 && doorsOut.Length == 1)
                {
                    var arrival = new SerializedObject(doorsOut[0]).FindProperty("arrival").objectReferenceValue as Transform;
                    if (r.Check(arrival != null && arrival.IsChildOf(valley.transform), $"{home.name} must return to its own valley doorstep"))
                    {
                        Vector3 feet = arrival.position + Vector3.up * 0.4f;
                        var overlaps = Physics.OverlapCapsule(feet, feet + Vector3.up * 1.1f, 0.33f, ~0, QueryTriggerInteraction.Ignore);
                        r.Check(overlaps.All(c => c.GetComponent<CharacterController>() != null), $"{home.name}: return point intersects world geometry");
                        Vector3 eye = arrival.position + Vector3.up * 1.6f;
                        Vector3 towards = doorsIn[0].transform.position - eye;
                        r.Check(Physics.SphereCast(eye, 0.04f, towards.normalized, out RaycastHit aim, towards.magnitude + 0.3f, ~0,
                            QueryTriggerInteraction.Collide) && aim.collider.GetComponent<ZonePortal>() == doorsIn[0], $"{home.name}: the exterior door cannot be aimed at from its doorstep");
                    }
                }
                var furniture = new HashSet<string>(home.GetComponentsInChildren<Collider>(true).Select(c => c.name));
                foreach (string piece in new[] { "Floor", "Ceiling", "West Wall", "East Wall", "South Wall", "North Wall", "Bed", "Table", "Shelves", "Storage Chest" })
                    r.Check(furniture.Contains(piece), $"{home.name} is missing {piece}");
            }
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
