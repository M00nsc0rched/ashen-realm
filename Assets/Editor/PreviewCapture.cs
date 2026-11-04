using System.IO;
using System.Linq;
using System.Reflection;
using AshenRealm.AI;
using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Ashen Realm > Capture Preview Shots: renders the generated scene from fixed vantage points into
    /// PNGs under Screenshots/ (project root). Works in batch mode, so visual changes can be reviewed
    /// without entering Play mode. Shots show the edit-mode state (rest poses, all HUD groups visible).
    /// </summary>
    public static class PreviewCapture
    {
        public const string OutputFolder = "Screenshots";

        [MenuItem("Ashen Realm/Capture Preview Shots", priority = 21)]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string folder = Capture();
            EditorUtility.RevealInFinder(folder);
        }

        public static string Capture(int width = 1600, int height = 900)
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
            var canvas = roots.Select(r => r.GetComponent<Canvas>()).FirstOrDefault(c => c != null);

            Directory.CreateDirectory(OutputFolder);
            Transform camTransform = camera.transform;
            Vector3 originalPos = camTransform.position;
            Quaternion originalRot = camTransform.rotation;

            void Shot(string name, Vector3 position, Vector3 lookAt, bool showWeapons, bool showHud)
            {
                if (weapons != null) weapons.gameObject.SetActive(showWeapons);
                camTransform.position = position;
                camTransform.rotation = Quaternion.LookRotation(lookAt - position);
                Render(camera, canvas, showHud, width, height, Path.Combine(OutputFolder, name + ".png"));
            }

            // First-person view from the spawn with weapons and HUD.
            Render(camera, canvas, true, width, height, Path.Combine(OutputFolder, "01_spawn_first_person.png"));
            Shot("02_road_to_ruins", Ground(out _, 0f, -4f) + Vector3.up * 1.7f, Ground(out _, 0f, 32f) + Vector3.up * 3f, false, false);
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
            Shot("05_graveyard", Ground(out _, -24f, -13f) + Vector3.up * 2f, Ground(out _, -32f, -6f) + Vector3.up * 0.5f, false, false);
            Shot("06_camp", Ground(out _, 19f, -47f) + Vector3.up * 1.8f, Ground(out _, 26f, -42f) + Vector3.up * 0.6f, false, false);
            Shot("07_overview", new Vector3(70f, 55f, -110f), new Vector3(0f, 0f, 10f), false, false);
            Shot("08_ruins_interior", Ground(out _, -5f, 23f) + Vector3.up * 1.7f, Ground(out _, 2f, 42f) + Vector3.up * 1.5f, false, false);

            camTransform.SetPositionAndRotation(originalPos, originalRot);
            if (weapons != null) weapons.gameObject.SetActive(true);
            Debug.Log($"[AshenRealm] Preview shots written to {Path.GetFullPath(OutputFolder)}");
            return Path.GetFullPath(OutputFolder);
        }

        static Vector3 Ground(out bool hit, float x, float z)
        {
            hit = Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out RaycastHit info, 600f, ~0, QueryTriggerInteraction.Ignore);
            return hit ? info.point : new Vector3(x, 0f, z);
        }

        static void Render(Camera camera, Canvas canvas, bool withHud, int width, int height, string path)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderMode originalMode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
            try
            {
                if (canvas != null && withHud)
                {
                    // Overlay canvases never reach a camera target, so composite through the camera for the shot.
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 0.2f;
                    var scaler = canvas.GetComponent<CanvasScaler>();
                    typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(scaler, null);
                    Canvas.ForceUpdateCanvases();
                }
                else if (canvas != null)
                {
                    canvas.enabled = false;
                }

                camera.targetTexture = rt;
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                if (canvas != null)
                {
                    canvas.renderMode = originalMode;
                    canvas.worldCamera = null;
                    canvas.enabled = true;
                }
            }
        }
    }
}
