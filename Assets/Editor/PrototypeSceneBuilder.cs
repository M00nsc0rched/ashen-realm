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
        /// <summary>Render scale applied to the URP assets for the slightly pixelated presentation.</summary>
        const float PixelatedRenderScale = 0.65f;

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

                Progress("Palette and UI textures", 0.06f);
                var palette = GeneratedAssets.WriteTexture<Texture2D>(Palette.CreateTexture(), GeneratedAssets.PalettePath, false, FilterMode.Point);
                var sprites = HudBuilder.CreateSprites();

                Progress("Materials", 0.1f);
                var materials = CreateMaterials(palette);
                Material skybox = CreateSkybox();
                VolumeProfile volumeProfile = CreateVolumeProfile();
                ConfigureRenderPipelineAssets();

                Progress("Creating scene", 0.14f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateSceneServices(skybox, volumeProfile);

                Progress("Generating valley (terrain, forest, ruins)", 0.2f);
                ValleyLayout layout = new ValleyGenerator(materials.World, materials.Metal, materials.Fire).Generate();

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

                Progress("HUD and touch controls", 0.88f);
                HudBuilder.Build(sprites, player.Input, player.Health, player.Stamina, player.Interactor, player.Lifecycle, knight);

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

        static GeneratedMaterials CreateMaterials(Texture2D palette)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (lit == null || unlit == null || particles == null)
                throw new System.InvalidOperationException("URP shaders not found. Is the Universal Render Pipeline package installed and active?");

            Material Lit(string name, float smoothness, float metallic)
            {
                var m = GeneratedAssets.GetOrCreateMaterial(name, lit);
                m.SetTexture("_BaseMap", palette);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", metallic);
                // No reflection probes are baked; skip environment reflections so surfaces do not go murky.
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                m.enableInstancing = true;
                return m;
            }

            var materials = new GeneratedMaterials
            {
                World = Lit("M_World", 0.1f, 0f),
                Metal = Lit("M_Metal", 0.55f, 0.3f),
            };

            materials.Fire = GeneratedAssets.GetOrCreateMaterial("M_Fire", unlit);
            materials.Fire.SetTexture("_BaseMap", palette);
            materials.Fire.SetColor("_BaseColor", new Color(1.4f, 1.15f, 1f));

            materials.Glow = GeneratedAssets.GetOrCreateMaterial("M_KnightEyes", unlit);
            materials.Glow.SetTexture("_BaseMap", null);
            materials.Glow.SetColor("_BaseColor", new Color(1.6f, 0.45f, 0.12f));

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

        static Material CreateSkybox()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            var sky = GeneratedAssets.GetOrCreateMaterial("M_Skybox_Dusk", shader);
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.45f);
            sky.SetColor("_SkyTint", new Color(0.42f, 0.4f, 0.5f));
            sky.SetColor("_GroundColor", new Color(0.3f, 0.26f, 0.24f));
            sky.SetFloat("_Exposure", 1.05f);
            return sky;
        }

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
            color.postExposure.Override(0.2f);
            color.contrast.Override(16f);
            color.saturation.Override(-4f);
            color.colorFilter.Override(new Color(1f, 0.95f, 0.88f));

            var split = Add<SplitToning>();
            split.shadows.Override(new Color(0.36f, 0.4f, 0.58f));
            split.highlights.Override(new Color(0.8f, 0.63f, 0.42f));
            split.balance.Override(-12f);

            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.05f, 0.03f, 0.03f));

            var bloom = Add<Bloom>();
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.6f);
            bloom.downscale.Override(BloomDownscaleMode.Quarter);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// Gives every URP asset used by a quality level the pixelated look (reduced render scale, point upscaling).
        /// Only these two fields are touched; each change is logged.
        /// </summary>
        static void ConfigureRenderPipelineAssets()
        {
            var seen = new HashSet<UniversalRenderPipelineAsset>();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (!(QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp) || !seen.Add(urp)) continue;
                if (!Mathf.Approximately(urp.renderScale, PixelatedRenderScale))
                {
                    Debug.Log($"{LogPrefix} {urp.name}: renderScale {urp.renderScale} → {PixelatedRenderScale}");
                    urp.renderScale = PixelatedRenderScale;
                }
                if (urp.upscalingFilter != UpscalingFilterSelection.Point)
                {
                    Debug.Log($"{LogPrefix} {urp.name}: upscalingFilter {urp.upscalingFilter} → Point");
                    urp.upscalingFilter = UpscalingFilterSelection.Point;
                }
                EditorUtility.SetDirty(urp);
            }
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

        static void CreateSceneServices(Material skybox, VolumeProfile profile)
        {
            new GameObject("Game Bootstrap").AddComponent<GameBootstrap>();

            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = Quaternion.Euler(22f, 110f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.8f, 0.58f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;

            RenderSettings.sun = sun;
            RenderSettings.skybox = skybox;
            Lightmapping.lightingSettings = GetLightingSettings();

            var atmosphereGo = new GameObject("Atmosphere");
            var atmosphere = atmosphereGo.AddComponent<AtmosphereController>();
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
