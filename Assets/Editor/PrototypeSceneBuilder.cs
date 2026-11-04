using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Ashen Realm > Generate Prototype: builds Assets/Scenes/Prototype_Valley.unity and all of its generated
    /// assets from scratch. Safe to run repeatedly: the scene is recreated, assets are updated in place (GUIDs kept)
    /// and stale generated meshes are removed.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string LogPrefix = "[AshenRealm]";

        [MenuItem("Ashen Realm/Generate Prototype", priority = 0)]
        public static void GenerateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            bool ok = Generate();
            if (ok && !Application.isBatchMode)
                EditorUtility.DisplayDialog("Ashen Realm", "Prototype_Valley generated and saved.\n\nPress Play to explore the valley.", "OK");
        }

        [MenuItem("Ashen Realm/Open Prototype Scene", priority = 1)]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(GeneratedAssets.ScenePath);
        }

        public static bool Generate()
        {
            var timer = Stopwatch.StartNew();
            try
            {
                Progress("Preparing folders", 0.02f);
                GeneratedAssets.EnsureFolders();
                GeneratedAssets.BeginMeshPass();

                Progress("Pixel-art textures, sky and UI sprites", 0.06f);
                var detailArray = GeneratedAssets.SaveTextureArray(PixelTextures.CreateArray(), GeneratedAssets.DetailArrayPath);
                var sky = GeneratedAssets.WriteTexture<Texture2D>(PixelTextures.PaintSky(1024, 512, -(SunRotation * Vector3.forward), out Color horizon),
                    GeneratedAssets.SkyPath, false, FilterMode.Point, true, TextureWrapMode.Repeat);
                var leaf = GeneratedAssets.WriteTexture<Texture2D>(LeafTexture(), GeneratedAssets.TextureFolder + "/T_Leaf.png", false, FilterMode.Point);
                var sprites = HudBuilder.CreateSprites();

                Progress("Materials", 0.1f);
                var materials = CreateMaterials(detailArray, leaf);
                Material skybox = CreateSkybox(sky);
                VolumeProfile volumeProfile = CreateVolumeProfile();
                ConfigureRenderPipelineAssets();

                Progress("Creating scene", 0.14f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateSceneServices(skybox, volumeProfile, horizon);

                Progress("Generating valley (terrain, forest, ruins, hamlet)", 0.2f);
                ValleyLayout layout = new ValleyGenerator(materials.World, materials.Foliage).Generate();

                Progress("Actors", 0.75f);
                var impacts = CreateImpactEffects(materials.Sparks);

                var spawn = new GameObject("Player Spawn").transform;
                spawn.SetPositionAndRotation(layout.PlayerSpawn, Quaternion.Euler(0f, layout.PlayerYaw, 0f));

                PlayerRig player = ActorFactory.CreatePlayer(layout.PlayerSpawn, layout.PlayerYaw, materials);
                CursedKnightAI knight = ActorFactory.CreateKnight(layout.KnightSpawn, layout.KnightYaw, materials);
                var chest = ActorFactory.CreateChest(layout.ChestPosition, layout.ChestYaw, materials);

                Progress("Prefabs", 0.82f);
                SavePrefab(player.Root, "Player");
                SavePrefab(knight.gameObject, "CursedKnight");
                SavePrefab(chest.gameObject, "TreasureChest");

                // Scene-level references (stored as prefab instance overrides).
                SceneWiring.Set(player.Lifecycle, "spawnPoint", spawn);
                SceneWiring.Set(player.Combat, "impactEffects", impacts);
                SceneWiring.Set(knight, "target", player.Root.transform);
                SceneWiring.Set(knight, "impactEffects", impacts);
                SceneWiring.Set(knight, "patrolPoints", layout.PatrolPoints);

                Progress("HUD, touch controls and pixel screen", 0.88f);
                HudBuilder.Build(sprites, player.Input, player.Health, player.Stamina, player.Interactor, player.Lifecycle, knight,
                    player.CameraEffects.transform, layout.ChestPosition);
                var pixelScreen = HudBuilder.BuildPixelScreen(materials.Composite);
                pixelScreen.enabled = false; // PixelCamera enables it at runtime; edit mode shows the world camera directly
                SceneWiring.Set(player.PixelCamera, "output", pixelScreen);
                SceneWiring.Set(player.PixelCamera, "screenCamera", CreateScreenCamera());

                Progress("Saving", 0.94f);
                int stale = GeneratedAssets.DeleteStaleMeshes();
                if (!EditorSceneManager.SaveScene(scene, GeneratedAssets.ScenePath))
                {
                    Debug.LogError($"{LogPrefix} Failed to save {GeneratedAssets.ScenePath}");
                    return false;
                }
                RegisterSceneInBuildSettings();
                AssetDatabase.SaveAssets();

                Debug.Log($"{LogPrefix} Prototype generated in {timer.Elapsed.TotalSeconds:F1}s → {GeneratedAssets.ScenePath} " +
                          $"(stale meshes removed: {stale})");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{LogPrefix} Prototype generation failed: {e}");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Progress(string step, float t)
        {
            Debug.Log($"{LogPrefix} {step}...");
            if (!Application.isBatchMode) EditorUtility.DisplayProgressBar("Ashen Realm — Generate Prototype", step, t);
        }

        // ---- Materials & rendering -----------------------------------------------------------------------

        static GeneratedMaterials CreateMaterials(Texture2DArray detail, Texture2D leaf)
        {
            Shader pixelLit = Shader.Find("AshenRealm/PixelLit");
            Shader composite = Shader.Find("AshenRealm/PixelComposite");
            Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (pixelLit == null || composite == null)
                throw new System.InvalidOperationException("AshenRealm shaders not found (Assets/Art/Shaders). Did they fail to compile?");
            if (particles == null)
                throw new System.InvalidOperationException("URP shaders not found. Is the Universal Render Pipeline package installed and active?");

            Material PixelLit(string name, bool foliage)
            {
                var m = GeneratedAssets.GetOrCreateMaterial(name, pixelLit);
                m.SetTexture("_DetailArray", detail);
                m.SetColor("_Tint", Color.white);
                m.SetFloat("_LightBands", 3f);
                m.SetFloat("_BandSoftness", 0.08f);
                m.SetColor("_ShadowColor", new Color(0.84f, 0.85f, 0.97f));
                m.SetFloat("_AlphaClip", foliage ? 1f : 0f);
                m.SetFloat("_Wind", foliage ? 1f : 0f);
                m.SetFloat("_Cull", foliage ? (float)CullMode.Off : (float)CullMode.Back);
                m.SetFloat("_Cutoff", 0.5f);
                if (foliage)
                {
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.EnableKeyword("_WIND_ON");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                }
                else
                {
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.DisableKeyword("_WIND_ON");
                    m.renderQueue = (int)RenderQueue.Geometry;
                }
                m.enableInstancing = true;
                return m;
            }

            var materials = new GeneratedMaterials
            {
                World = PixelLit("M_World", false),
                Foliage = PixelLit("M_Foliage", true),
            };

            materials.Composite = GeneratedAssets.GetOrCreateMaterial("M_PixelComposite", composite);
            materials.Composite.SetFloat("_Levels", 18f);
            materials.Composite.SetFloat("_Dither", 0.55f);
            materials.Composite.SetFloat("_Saturation", 1.1f);
            materials.Composite.SetFloat("_Contrast", 1.04f);

            materials.DriftingLeaves = GeneratedAssets.GetOrCreateMaterial("M_DriftingLeaves", particles);
            MakeTransparent(materials.DriftingLeaves, false);
            materials.DriftingLeaves.SetTexture("_BaseMap", leaf);
            materials.DriftingLeaves.SetColor("_BaseColor", Color.white);

            materials.Trail = GeneratedAssets.GetOrCreateMaterial("M_SwordTrail", particles);
            MakeTransparent(materials.Trail, false);
            materials.Trail.SetColor("_BaseColor", new Color(1f, 0.96f, 0.9f, 0.6f));

            materials.Sparks = GeneratedAssets.GetOrCreateMaterial("M_ImpactSparks", particles);
            MakeTransparent(materials.Sparks, true);
            materials.Sparks.SetColor("_BaseColor", Color.white);
            return materials;
        }

        static void MakeTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        /// <summary>8×8 pixel-art leaf for the drifting-leaves particles.</summary>
        static Texture2D LeafTexture()
        {
            string[] rows =
            {
                "........",
                "....##..",
                "..####..",
                ".#####+.",
                ".####+..",
                "..##+...",
                "..+.....",
                ".+......",
            };
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                char c = rows[7 - y][x];
                texture.SetPixel(x, y, c == '#' ? Color.white : c == '+' ? new Color(0.7f, 0.6f, 0.45f, 1f) : new Color(1f, 1f, 1f, 0f));
            }
            texture.Apply();
            return texture;
        }

        static Material CreateSkybox(Texture2D panorama)
        {
            Shader shader = Shader.Find("Skybox/Panoramic");
            var sky = GeneratedAssets.GetOrCreateMaterial("M_Skybox_Painted", shader);
            sky.SetTexture("_MainTex", panorama);
            sky.SetFloat("_Mapping", 1f);      // latitude-longitude layout
            sky.SetFloat("_ImageType", 0f);    // 360°
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 0f);
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            return sky;
        }

        /// <summary>Golden-hour sun from the west-north-west, low enough for long painterly shadows.</summary>
        static readonly Quaternion SunRotation = Quaternion.Euler(24f, 110f, 0f);

        static VolumeProfile CreateVolumeProfile()
        {
            string path = GeneratedAssets.SettingsFolder + "/VP_AshenValley.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                foreach (var component in profile.components.ToArray())
                {
                    AssetDatabase.RemoveObjectFromAsset(component);
                    Object.DestroyImmediate(component, true);
                }
                profile.components.Clear();
            }

            T Add<T>() where T : VolumeComponent
            {
                var component = profile.Add<T>(false);
                component.name = typeof(T).Name;
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
                return component;
            }

            var tonemapping = Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = Add<ColorAdjustments>();
            color.postExposure.Override(0.15f);
            color.contrast.Override(10f);
            color.saturation.Override(8f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.92f));

            var split = Add<SplitToning>();
            split.shadows.Override(new Color(0.46f, 0.47f, 0.56f));
            split.highlights.Override(new Color(0.76f, 0.64f, 0.46f));
            split.balance.Override(-10f);

            // No bloom: soft glow fights the crisp pixel look. A light vignette frames the low-res image.
            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.06f, 0.04f, 0.06f));

            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// The pixel look comes from PixelCamera's fixed low-resolution target. URP applies its render scale even when
        /// rendering into a texture (which would blur that target), so every URP asset used by a quality level gets
        /// render scale 1. Only this field is touched; each change is logged.
        /// </summary>
        static void ConfigureRenderPipelineAssets()
        {
            var seen = new HashSet<UniversalRenderPipelineAsset>();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (!(QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp) || !seen.Add(urp)) continue;
                if (!Mathf.Approximately(urp.renderScale, 1f))
                {
                    Debug.Log($"{LogPrefix} {urp.name}: renderScale {urp.renderScale} → 1");
                    urp.renderScale = 1f;
                    EditorUtility.SetDirty(urp);
                }
            }
        }

        /// <summary>
        /// Camera that draws only the screen-space UI (including the pixel-world RawImage) to the display.
        /// Starts disabled; PixelCamera enables it at runtime.
        /// </summary>
        static Camera CreateScreenCamera()
        {
            var go = new GameObject("Screen Camera");
            var camera = go.AddComponent<Camera>();
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.depth = 10f;
            camera.orthographic = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            camera.enabled = false;
            return camera;
        }

        static LightingSettings GetLightingSettings()
        {
            string path = GeneratedAssets.SettingsFolder + "/LS_PrototypeValley.lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null)
            {
                settings = new LightingSettings { name = "LS_PrototypeValley" };
                AssetDatabase.CreateAsset(settings, path);
            }
            // Fully realtime: nothing to bake, so the scene works straight after generation.
            settings.bakedGI = false;
            settings.realtimeGI = false;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static void CreateSceneServices(Material skybox, VolumeProfile profile, Color horizon)
        {
            new GameObject("Game Bootstrap").AddComponent<GameBootstrap>();

            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = SunRotation;
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.66f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;

            RenderSettings.sun = sun;
            RenderSettings.skybox = skybox;
            Lightmapping.lightingSettings = GetLightingSettings();

            // Cool ambient against the warm sun; fog matches the painted sky's horizon for atmospheric depth.
            var atmosphereGo = new GameObject("Atmosphere");
            var atmosphere = atmosphereGo.AddComponent<AtmosphereController>();
            SceneWiring.Set(atmosphere, "ambientColor", new Color(0.6f, 0.58f, 0.64f));
            SceneWiring.Set(atmosphere, "fogColor", horizon);
            SceneWiring.Set(atmosphere, "fogStart", 30f);
            SceneWiring.Set(atmosphere, "fogEnd", 320f);
            atmosphere.Apply();

            var volume = atmosphereGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static ImpactEffects CreateImpactEffects(Material material)
        {
            var go = new GameObject("Impact Effects");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = 0.4f;
            main.startSpeed = 0f;
            main.startSize = 0.05f;
            main.gravityModifier = 1.4f;
            main.maxParticles = 256;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1.5f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go.AddComponent<ImpactEffects>();
        }

        static void SavePrefab(GameObject instance, string name)
        {
            string path = $"{GeneratedAssets.PrefabFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, path, InteractionMode.AutomatedAction);
        }

        static void RegisterSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != GeneratedAssets.ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(GeneratedAssets.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
