using System.IO;
using System.Linq;
using System.Reflection;
using AshenRealm.AI;
using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Ashen Realm > Capture Preview Shots: renders the generated scene from fixed vantage points into PNGs under
    /// Screenshots/ (project root), emulating the runtime pixel pipeline: low-resolution render, the composite
    /// shader's posterise + Bayer dither, nearest-neighbour upscale and (optionally) the HUD on top.
    /// Works in batch mode. Shots show the edit-mode state (rest poses, all HUD groups visible).
    /// </summary>
    public static class PreviewCapture
    {
        public const string OutputFolder = "Screenshots";
        const int LowHeight = 360;
        const int Scale = 3;

        [MenuItem("Ashen Realm/Capture Preview Shots", priority = 21)]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string folder = Capture();
            EditorUtility.RevealInFinder(folder);
        }

        public static string Capture()
        {
            var scene = EditorSceneManager.OpenScene(GeneratedAssets.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            Physics.SyncTransforms();
            T Find<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

            var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
            var atmosphere = Find<AtmosphereController>();
            if (atmosphere != null) atmosphere.Apply();
            var weapons = Find<FirstPersonWeaponAnimator>();
            var knight = Find<CursedKnightAI>();
            var chest = Find<TreasureChest>();
            var hud = roots.Select(r => r.GetComponent<Canvas>()).FirstOrDefault(c => c != null && c.name == "HUD");
            var composite = AssetDatabase.LoadAssetAtPath<Material>(GeneratedAssets.MaterialFolder + "/M_PixelComposite.mat");

            Directory.CreateDirectory(OutputFolder);
            // The first render after opening a scene in batch mode can miss lighting setup; render once and discard.
            RenderCamera(camera, 64, 36, false);
            Transform camTransform = camera.transform;
            Vector3 originalPos = camTransform.position;
            Quaternion originalRot = camTransform.rotation;

            void Shot(string name, Vector3 position, Vector3 lookAt, bool showWeapons, bool showHud)
            {
                if (weapons != null) weapons.gameObject.SetActive(showWeapons);
                camTransform.position = position;
                camTransform.rotation = Quaternion.LookRotation(lookAt - position);
                Save(camera, showHud ? hud : null, composite, Path.Combine(OutputFolder, name + ".png"));
            }

            // Someone from the front, or from the nearest angle where nothing stands between the camera and them.
            void Portrait(string name, Component person)
            {
                Transform t = person.transform;
                Collider body = person.GetComponent<Collider>();
                Vector3 chest = t.position + Vector3.up * 1.1f, from = chest;
                foreach (float angle in new[] { 0f, 35f, -35f, 70f, -70f })
                {
                    from = t.position + Quaternion.Euler(0f, angle, 0f) * t.forward * 2.6f + Vector3.up * 1.6f;
                    if (Physics.Linecast(from, chest, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider == body &&
                        !Physics.CheckSphere(from, 0.2f, ~0, QueryTriggerInteraction.Ignore)) break;
                }
                Shot(name + "_" + person.name.Replace(" ", "_"), from, chest, false, false);
            }

            void Vistas(Zone zone, string prefix)
            {
                foreach (Transform group in zone.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Vistas"))
                foreach (Transform view in group)
                {
                    string safeName = string.Concat(view.name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
                    Shot(prefix + "_vista_" + safeName, view.position, view.position + view.forward * 10f, false, false);
                }
            }

            var valley = roots.SelectMany(r => r.GetComponentsInChildren<Zone>(true)).First(z => !z.Indoors && !z.Underground && z.name == "Zone Valley");
            Vistas(valley, "00_Valley");
            camTransform.SetPositionAndRotation(originalPos, originalRot);
            if (weapons != null) weapons.gameObject.SetActive(true);

            Save(camera, hud, composite, Path.Combine(OutputFolder, "01_spawn_first_person.png"));
            Vector3 arrivalGround = Ground(0f, -86f);
            Shot("01b_arrival_tunnel", arrivalGround + Vector3.up * 1.7f, new Vector3(0f, arrivalGround.y + 2.8f, -97f), false, false);
            Shot("01c_waiting_caravan", new Vector3(-1.5f, arrivalGround.y + 1.7f, -77.5f), new Vector3(-7.4f, arrivalGround.y + 1.6f, -84.8f), false, false);
            Shot("02_road_to_ruins", Ground(0f, -4f) + Vector3.up * 1.7f, Ground(0f, 32f) + Vector3.up * 3f, false, false);
            if (knight != null)
            {
                Transform k = knight.transform;
                Shot("03_cursed_knight", k.position + k.forward * 3.2f + k.right * 0.8f + Vector3.up * 1.6f, k.position + Vector3.up * 1.2f, true, false);
            }
            if (chest != null)
            {
                Transform c = chest.transform;
                Shot("04_chest_altar", c.position + c.rotation * new Vector3(0.6f, 1.7f, -2.6f), c.position + Vector3.up * 0.5f, true, true);
            }
            Shot("05_graveyard", Ground(-24f, -13f) + Vector3.up * 1.8f, Ground(-32f, -6f) + Vector3.up * 0.8f, true, false);
            Shot("06_camp", Ground(19f, -47f) + Vector3.up * 1.8f, Ground(26f, -42f) + Vector3.up * 0.6f, false, false);
            Shot("07_overview", new Vector3(70f, 55f, -110f), new Vector3(0f, 0f, 10f), false, false);
            Shot("08_ruins_interior", Ground(-5f, 23f) + Vector3.up * 1.7f, Ground(2f, 42f) + Vector3.up * 1.5f, false, false);
            Shot("09_hamlet", Ground(14f, -15f) + Vector3.up * 1.7f, Ground(36f, -14f) + Vector3.up * 2.5f, true, false);
            Shot("10_windmill_fields", Ground(34f, -30f) + Vector3.up * 1.7f, Ground(54f, 8f) + Vector3.up * 5f, false, false);

            // The north land: overview, the pass from both sides, the road, the city gate, the ridge seam and the instanced cover.
            Shot("07n_north_overview", new Vector3(110f, 55f, 150f), new Vector3(-20f, 0f, 260f), false, false);
            Shot("07n_north_overview_west", new Vector3(-30f, 60f, 260f), new Vector3(-150f, 0f, 380f), false, false);
            Shot("07n_toll_gate_from_valley", Ground(0f, 70f) + Vector3.up * 1.7f, Ground(0f, 92f) + Vector3.up * 4f, false, false);
            Shot("07n_toll_gate_from_north", Ground(4f, 120f) + Vector3.up * 1.7f, Ground(0f, 92f) + Vector3.up * 3f, false, false);
            Shot("07n_road_north", Ground(-3f, 128f) + Vector3.up * 1.7f, Ground(5f, 190f) + Vector3.up * 2f, false, false);
            Shot("07n_road_north_far", Ground(0f, 300f) + Vector3.up * 1.7f, Ground(8f, 380f) + Vector3.up * 2f, false, false);
            Shot("07n_city_gate_approach", Ground(2f, 530f) + Vector3.up * 1.7f, Ground(0f, 590f) + Vector3.up * 5f, false, false);
            Shot("07n_ridge_from_valley", Ground(40f, 55f) + Vector3.up * 1.7f, Ground(40f, 97f) + Vector3.up * 4f, false, false);
            Shot("07n_scatter_forest_trees", Ground(-62f, 300f) + Vector3.up * 1.7f, Ground(-100f, 296f) + Vector3.up * 3f, false, false);
            Shot("07n_scatter_wheat_fields", Ground(28f, 150f) + Vector3.up * 1.7f, Ground(55f, 175f) + Vector3.up * 0.5f, false, false);
            Shot("07n_scatter_grass_close", Ground(8f, -62f) + Vector3.up * 1.7f, Ground(16f, -50f), false, false);
            foreach (NpcIdleAnimator person in valley.GetComponentsInChildren<NpcIdleAnimator>(true).Where(p => p.transform.position.z > 90f))
                Portrait("07p", person);
            Shot("07p_wolf_den_mouth", Ground(-196.5f, 231.5f) + Vector3.up * 1.5f, Ground(-200.1f, 230f) + Vector3.up * 0.5f, false, false);
            CaptureHorse(Find<RidingHorse>(), Shot, camera, hud, composite);

            // Every class's weapons in hand: the scene only has the knight's enabled.
            var classRig = Find<PlayerClassRig>();
            var weaponSets = new SerializedObject(classRig).FindProperty("weaponSets");
            void ShowClass(int index)
            {
                for (int s = 0; s < weaponSets.arraySize; s++)
                    ((GameObject)weaponSets.GetArrayElementAtIndex(s).objectReferenceValue).SetActive(s / PlayerClassRig.HandsPerClass == index);
            }
            foreach (var definition in Catalog.Classes)
            {
                ShowClass((int)definition.Id);
                Shot("10_class_" + definition.Id, Ground(0f, -30f) + Vector3.up * 1.62f, Ground(0f, 0f) + Vector3.up * 1.6f, true, false);
            }
            ShowClass(0);

            // What the forge sells, in the hand.
            var bought = new SerializedObject(classRig).FindProperty("boughtWeapons");
            ((GameObject)weaponSets.GetArrayElementAtIndex(0).objectReferenceValue).SetActive(false);
            for (int s = 0; s < bought.arraySize; s++)
            {
                var weapon = (GameObject)bought.GetArrayElementAtIndex(s).objectReferenceValue;
                weapon.SetActive(true);
                Shot($"10_bought_{s}", Ground(0f, -30f) + Vector3.up * 1.62f, Ground(0f, 0f) + Vector3.up * 1.6f, true, false);
                weapon.SetActive(false);
            }
            ((GameObject)weaponSets.GetArrayElementAtIndex(0).objectReferenceValue).SetActive(true);

            // Each other zone in its own light: arrival, then its keeper or its people.
            var sun = RenderSettings.sun;
            var torch = camera.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.name == "Torch");
            var zones = roots.SelectMany(r => r.GetComponentsInChildren<Zone>(true)).ToArray();
            for (int i = 1; i < zones.Length; i++)
            {
                Zone zone = zones[i];
                atmosphere.ApplyZone(zone, null, i == ZoneNumbers.City);
                if (sun != null) sun.enabled = !zone.OutOfTheSun;
                if (torch != null) torch.enabled = zone.Underground;
                camera.clearFlags = zone.OutOfTheSun ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                camera.backgroundColor = zone.Fog;

                string prefix = $"{10 + i}_{zone.name.Replace("Zone ", "")}";
                Vector3 entry = zone.Entry.position + Vector3.up * 1.62f;
                Shot(prefix + "_a_arrival", entry, entry + zone.Entry.forward * 10f, !zone.Indoors, i == 2);
                // Review the architecture in its own light, without a weapon obscuring the composition.
                bool torchWasEnabled = torch != null && torch.enabled;
                if (torch != null) torch.enabled = false;
                Vistas(zone, prefix);
                if (torch != null) torch.enabled = torchWasEnabled;

                if (i == ZoneNumbers.FirstDepth)
                {
                    // The supplied strips in hand, including a second frame, without entering Play mode.
                    var carried = Find<PlayerTorch>();
                    var serializedTorch = new SerializedObject(carried);
                    var mount = (Transform)serializedTorch.FindProperty("mount").objectReferenceValue;
                    var flame = (Renderer)serializedTorch.FindProperty("flame").objectReferenceValue;
                    var block = new MaterialPropertyBlock();
                    mount.gameObject.SetActive(true);
                    for (int style = 0; style < 2; style++)
                    {
                        ShowClass(style == 0 ? 0 : 2);
                        int left = (style == 0 ? 0 : 2) * PlayerClassRig.HandsPerClass + 1;
                        ((GameObject)weaponSets.GetArrayElementAtIndex(left).objectReferenceValue).SetActive(false);
                        block.SetTexture("_MainTex", (Texture)serializedTorch.FindProperty(style == 0 ? "warmStrip" : "coldStrip").objectReferenceValue);
                        if (torch != null) torch.color = style == 0 ? new Color(1f, 0.67f, 0.32f) : new Color(0.28f, 0.62f, 1f);
                        foreach (int frame in new[] { 0, 4 })
                        {
                            block.SetFloat("_Frame", frame);
                            flame.SetPropertyBlock(block);
                            Shot(prefix + (style == 0 ? "_torch_warm_" : "_torch_cold_") + frame,
                                entry, entry + zone.Entry.forward * 10f, true, false);
                        }
                    }
                    mount.gameObject.SetActive(false);
                    flame.SetPropertyBlock(null);
                    if (torch != null) torch.color = new Color(1f, 0.67f, 0.32f);
                    ShowClass(0);
                }

                var foes = zone.GetComponentsInChildren<EnemyAI>(true);
                EnemyAI keeper = foes.FirstOrDefault(e => Catalog.Enemy(e.EnemyId).Rank >= EnemyRank.Guardian);
                if (keeper != null)
                {
                    Vector3 k = keeper.transform.position;
                    Vector3 from = k + keeper.transform.forward * 7f + Vector3.up * 1.62f;
                    Shot(prefix + "_b_keeper", from, k + Vector3.up * 1.6f, true, false);
                    for (int f = 0; f < foes.Length && f < 2; f++)
                    {
                        Vector3 p = foes[f].transform.position;
                        Shot($"{prefix}_c_foe{f}", p + foes[f].transform.forward * 3.4f + Vector3.up * 1.5f, p + Vector3.up * 0.9f, false, false);
                    }
                }
                else if (zone.Indoors)
                {
                    Vector3 far = zone.Objective.position + Vector3.up * 1.62f;
                    if (i == ZoneNumbers.Tavern)
                    {
                        Shot(prefix + "_b_from_the_stair", zone.Exit.position + Vector3.up * 1.62f, zone.Entry.position + Vector3.up * 1.2f, false, false);
                        Shot(prefix + "_b_hearth", zone.Entry.position + new Vector3(1f, 1.62f, 7f), far + new Vector3(-5f, -0.4f, 0f), false, false);
                        Shot(prefix + "_b_bar", zone.Entry.position + new Vector3(-1f, 1.62f, 5f), far + new Vector3(4.5f, -0.5f, -4f), false, false);
                    }
                    else if (i == ZoneNumbers.Forge)
                    {
                        Vector3 at = zone.Entry.position;
                        Shot(prefix + "_b_rack", at + new Vector3(1.5f, 1.62f, 5f), at + new Vector3(6.4f, 1.9f, 4.6f), false, false);
                        Shot(prefix + "_b_hearth", at + new Vector3(1.6f, 1.62f, 3.4f), at + new Vector3(-1.8f, 1.2f, 8.8f), false, false);
                        Shot(prefix + "_b_bench", at + new Vector3(-0.6f, 1.62f, 3.6f), at + new Vector3(-6f, 1f, 2.2f), false, false);
                    }
                    else if (i == ZoneNumbers.Chapel)
                    {
                        Vector3 at = zone.Entry.position;
                        Shot(prefix + "_b_rail", at + new Vector3(0f, 2f, 10.6f), at + new Vector3(0f, 2f, 14f), false, false);
                        Shot(prefix + "_b_relics", at + new Vector3(2.6f, 2f, 11f), at + new Vector3(3.2f, 1.5f, 13.2f), false, false);
                        Shot(prefix + "_b_back", at + new Vector3(0f, 2f, 10.6f), at + new Vector3(0f, 1.4f, 0f), false, false);
                    }
                    else if (i >= ZoneNumbers.FirstHome && i < ZoneNumbers.FirstHome + ZoneNumbers.HomeCount)
                    {
                        Vector3 at = zone.transform.position;
                        Shot(prefix + "_b_hearth_and_table", at + new Vector3(0.8f, 1.6f, -0.8f), at + new Vector3(-2f, 1f, 1.6f), false, false);
                        Shot(prefix + "_c_bed_and_work", at + new Vector3(-0.5f, 1.6f, 0.5f), at + new Vector3(2.6f, 1f, 0f), false, false);
                        Shot(prefix + "_d_return_door", at + new Vector3(0f, 1.6f, 0.9f), zone.Objective.position + Vector3.up * 1.6f, false, false);
                    }
                    foreach (NpcActor seated in zone.GetComponentsInChildren<NpcActor>(true))
                        Shot($"{prefix}_c_{seated.NpcId}", seated.transform.position + seated.transform.forward * 2.8f + Vector3.up * 1.6f, seated.transform.position + Vector3.up * 1.15f, false, false);
                }
                else
                {
                    Vector3 stair = zone.Objective.position;
                    Shot(prefix + "_b_square", zone.Exit.position + new Vector3(10f, 1.7f, -18f), stair + Vector3.up * 3f, false, false);
                    if (i == ZoneNumbers.City)
                    {
                        Vector3 o = zone.transform.position;
                        Shot(prefix + "_b_market", o + new Vector3(-0.8f, 1.7f, -54f), o + new Vector3(0f, 3f, 0f), false, false);
                        Shot(prefix + "_b_gallows", o + new Vector3(4f, 1.7f, -22f), o + new Vector3(0f, 3.2f, -3f), false, false);
                        Shot(prefix + "_b_forge_door", o + new Vector3(19f, 1.7f, 5f), o + new Vector3(28f, 2.2f, 6f), false, false);
                        Shot(prefix + "_b_chapel_door", o + new Vector3(-15f, 1.7f, 5f), o + new Vector3(-25f, 3.4f, 6f), false, false);
                        // The keep on its crag from every corner a player can reach, and once from above the walls.
                        Vector3 keep = o + new Vector3(0f, 60f, 197f);
                        foreach (var (where, from) in new[] { ("gate", new Vector3(0f, 1.7f, -60f)), ("well", new Vector3(-9f, 1.7f, 11f)),
                            ("court", new Vector3(8f, 1.7f, 12f)), ("tavern_street", new Vector3(0f, 1.7f, 30f)), ("southwest", new Vector3(-48f, 1.7f, -50f)),
                            ("southeast", new Vector3(48f, 1.7f, -50f)), ("northwest", new Vector3(-48f, 1.7f, 50f)), ("northeast", new Vector3(48f, 1.7f, 50f)),
                            ("high_overview", new Vector3(90f, 45f, -40f)) })
                            Shot(prefix + "_keep_from_" + where, o + from, keep, false, false);
                        foreach (NpcActor standing in zone.GetComponentsInChildren<NpcActor>(true))
                            Shot($"{prefix}_c_{standing.NpcId}", standing.transform.position + standing.transform.forward * 2.8f + Vector3.up * 1.7f, standing.transform.position + Vector3.up * 1.1f, false, false);
                        foreach (NpcIdleAnimator ambient in zone.GetComponentsInChildren<NpcIdleAnimator>(true).Where(p => p.GetComponent<NpcActor>() == null))
                            Portrait(prefix + "_p", ambient);
                    }
                    var person = zone.GetComponentInChildren<NpcActor>(true);
                    if (person != null)
                        Shot(prefix + "_c_person", person.transform.position + person.transform.forward * 2.6f + Vector3.up * 1.6f, person.transform.position + Vector3.up * 1.2f, false, false);
                }

                var site = zone.GetComponentInChildren<EncounterSite>(true);
                if (site != null)
                    Shot(prefix + "_d_encounter", site.transform.position + new Vector3(0.8f, 1.6f, -3.2f), site.transform.position + Vector3.up * 1.1f, false, false);
            }
            if (sun != null) sun.enabled = true;
            if (torch != null) torch.enabled = false;
            camera.clearFlags = CameraClearFlags.Skybox;

            camTransform.SetPositionAndRotation(originalPos, originalRot);
            if (weapons != null) weapons.gameObject.SetActive(true);
            Debug.Log($"[AshenRealm] Preview shots written to {Path.GetFullPath(OutputFolder)}");
            return Path.GetFullPath(OutputFolder);
        }

        /// <summary>
        /// The paddock, the bought mare at the city-gate post, the view from her saddle looking north with the HUD (16:9 and a
        /// phone's 19.5:9), and her gallop from outside, posed by hand since edit mode does not animate.
        /// </summary>
        static void CaptureHorse(RidingHorse horse, System.Action<string, Vector3, Vector3, bool, bool> shot, Camera camera, Canvas hud, Material composite)
        {
            if (horse == null) return;
            Transform t = horse.transform, model = t.Find("Model");
            Transform Part(string path) => model.Find(path);
            Vector3 restPosition = t.position;
            Quaternion restRotation = t.rotation;
            model.gameObject.SetActive(true);
            try
            {
                shot("07h_paddock_horses", Ground(36f, 110f) + Vector3.up * 1.7f, Ground(47f, 119f) + Vector3.up * 1f, false, false);

                Transform post = t.parent.GetComponentsInChildren<Transform>(true).First(p => p.name == "Horse Post 1").Find("Standing Spot");
                t.SetPositionAndRotation(post.position, post.rotation);
                Physics.SyncTransforms();
                shot("07h_horse_at_city_gate_post", post.position + post.right * 4.2f - post.forward * 1.5f + Vector3.up * 1.7f, post.position + Vector3.up * 1.2f, false, false);

                // In the saddle the horse rides under the player with its head forward; the eye is 2.45 m above the hooves.
                Vector3 road = Ground(0f, 500f);
                t.SetPositionAndRotation(road, Quaternion.identity);
                Physics.SyncTransforms();
                var fighting = Object.FindObjectsByType<AshenRealm.UI.TouchButton>(FindObjectsInactive.Include)
                    .Where(b => b.Button == AshenRealm.Input.PlayerButton.Attack || b.Button == AshenRealm.Input.PlayerButton.Block ||
                        b.Button == AshenRealm.Input.PlayerButton.Ability1 || b.Button == AshenRealm.Input.PlayerButton.Ability2)
                    .Select(b => b.GetComponent<CanvasGroup>()).Where(g => g != null).ToArray();
                float[] alphas = fighting.Select(g => g.alpha).ToArray();
                foreach (CanvasGroup g in fighting) g.alpha = 0f;
                Transform eye = camera.transform;
                eye.SetPositionAndRotation(road + Vector3.up * 2.45f, Quaternion.identity);
                var weapons = Object.FindAnyObjectByType<FirstPersonWeaponAnimator>(FindObjectsInactive.Include);
                if (weapons != null) weapons.gameObject.SetActive(false);
                Save(camera, hud, composite, Path.Combine(OutputFolder, "07h_mounted_view_north_16x9.png"));
                Save(camera, hud, composite, Path.Combine(OutputFolder, "07h_mounted_view_north_phone.png"), 19.5f / 9f);
                for (int i = 0; i < fighting.Length; i++) fighting[i].alpha = alphas[i];

                // Mid-gallop: the hind legs reach under, the front pair stretches out, the body lifted and the tail streaming.
                Transform frame = Part("Body");
                frame.localPosition = Vector3.up * 0.06f;
                frame.localRotation = Quaternion.Euler(2.5f, 0f, 0f);
                foreach (var (leg, angle) in new[] { ("Leg Front Left", 32f), ("Leg Front Right", 20f), ("Leg Hind Left", -30f), ("Leg Hind Right", -16f) })
                    Part("Body/" + leg).localRotation = Quaternion.Euler(angle, 0f, 0f);
                Part("Body/Neck").localRotation = Quaternion.Euler(6f, 0f, 0f);
                Part("Body/Tail").localRotation = Quaternion.Euler(-32f, 0f, 0f);
                shot("07h_horse_gallop_side", road + Vector3.right * 6.5f + Vector3.forward * 0.6f + Vector3.up * 1.6f, road + Vector3.up * 1.1f, false, false);
                shot("07h_horse_gallop_front", road + Vector3.forward * 5.5f + Vector3.right * 2.5f + Vector3.up * 1.9f, road + Vector3.up * 1.3f, false, false);
            }
            finally
            {
                model.gameObject.SetActive(false);
                t.SetPositionAndRotation(restPosition, restRotation);
            }
        }

        static Vector3 Ground(float x, float z)
        {
            return Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out RaycastHit info, 600f, ~0, QueryTriggerInteraction.Ignore)
                ? info.point
                : new Vector3(x, 0f, z);
        }

        static void Save(Camera camera, Canvas hud, Material composite, string path, float aspect = 16f / 9f)
        {
            int lowWidth = Mathf.RoundToInt(LowHeight * aspect);
            Color[] world = RenderCamera(camera, lowWidth, LowHeight, false);
            Posterise(world, lowWidth, LowHeight, composite);

            int width = lowWidth * Scale, height = LowHeight * Scale;
            var output = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                output[y * width + x] = world[(y / Scale) * lowWidth + x / Scale];

            if (hud != null) OverlayHud(hud, output, width, height);

            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.SetPixels(output);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        /// <summary>Renders a camera into a temporary target and returns display-space pixels.</summary>
        static Color[] RenderCamera(Camera camera, int width, int height, bool transparent)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.filterMode = FilterMode.Point;
            var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var image = new Texture2D(width, height, transparent ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                Color[] pixels = image.GetPixels();
                Object.DestroyImmediate(image);
                return pixels;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>CPU copy of AshenRealm/PixelComposite (pixels are already in display space).</summary>
        static void Posterise(Color[] pixels, int width, int height, Material composite)
        {
            float levels = composite != null ? composite.GetFloat("_Levels") : 18f;
            float dither = composite != null ? composite.GetFloat("_Dither") : 0.55f;
            float saturation = composite != null ? composite.GetFloat("_Saturation") : 1.1f;
            float contrast = composite != null ? composite.GetFloat("_Contrast") : 1.04f;
            int[] bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color c = pixels[y * width + x];
                float luma = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                float threshold = (bayer[(y % 4) * 4 + x % 4] / 16f - 0.5f) * dither;
                float Channel(float v)
                {
                    v = luma + (v - luma) * saturation;
                    v = (v - 0.5f) * contrast + 0.5f;
                    return Mathf.Floor(Mathf.Clamp01(v) * levels + 0.5f + threshold) / levels;
                }
                pixels[y * width + x] = new Color(Channel(c.r), Channel(c.g), Channel(c.b), 1f);
            }
        }

        /// <summary>Renders the HUD canvas through a temporary UI camera and alpha-blends it over the image.</summary>
        static void OverlayHud(Canvas canvas, Color[] output, int width, int height)
        {
            var cameraGo = new GameObject("Preview UI Camera");
            var uiCamera = cameraGo.AddComponent<Camera>();
            uiCamera.cullingMask = 1 << 5;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            uiCamera.orthographic = true;
            uiCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            RenderMode originalMode = canvas.renderMode;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 1f;
                uiCamera.nearClipPlane = 0.1f;
                uiCamera.farClipPlane = 10f;
                var scaler = canvas.GetComponent<CanvasScaler>();
                var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                uiCamera.targetTexture = rt;
                typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(scaler, null);
                Canvas.ForceUpdateCanvases();
                uiCamera.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);

                Color[] ui = RenderCamera(uiCamera, width, height, true);
                for (int i = 0; i < output.Length; i++)
                {
                    Color u = ui[i];
                    float a = Mathf.Sqrt(Mathf.Clamp01(u.a));
                    Color o = output[i];
                    output[i] = new Color(Mathf.Clamp01(u.r + o.r * (1f - a)), Mathf.Clamp01(u.g + o.g * (1f - a)), Mathf.Clamp01(u.b + o.b * (1f - a)), 1f);
                }
            }
            finally
            {
                canvas.renderMode = originalMode;
                canvas.worldCamera = null;
                Object.DestroyImmediate(cameraGo);
            }
        }
    }
}
