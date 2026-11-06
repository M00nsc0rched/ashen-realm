using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Ashen Realm > Capture Menu Shots: renders the menu screens to Screenshots/ without entering Play mode, at a desktop
    /// and a phone aspect, for comparing them with the design sketches. Canvases show the sample text the builder left.
    /// </summary>
    public static class MenuCapture
    {
        const string OutputFolder = "Screenshots";

        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1688, 780) };

        [MenuItem("Ashen Realm/Capture Menu Shots", priority = 31)]
        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene(GeneratedAssets.ScenePath, OpenSceneMode.Single);
            Canvas Named(string name) => scene.GetRootGameObjects().Select(r => r.GetComponent<Canvas>()).First(c => c != null && c.name == name);
            Canvas menu = Named("Menu");
            Canvas hud = Named("HUD");

            var cameraGo = new GameObject("Menu Capture Camera", typeof(Camera));
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.15f, 0.18f);
            camera.orthographic = true;
            camera.cullingMask = 1 << 5; // UI only: the capture camera stands in the middle of the valley

            Directory.CreateDirectory(OutputFolder);
            foreach (Vector2Int size in Sizes)
            {
                string suffix = size.x == 1920 ? "desktop" : "phone";
                hud.gameObject.SetActive(false);
                foreach (string screen in new[] { "Main Menu", "Settings", "Codex", "Confirm" })
                {
                    Only(menu.transform, screen, screen == "Main Menu" ? null : "Main Menu");
                    Render(menu, camera, size, $"menu_{screen.Replace(' ', '_').ToLowerInvariant()}_{suffix}");
                }

                // Three cards of the row, then the story page behind the first one.
                var carousel = menu.GetComponentInChildren<AshenRealm.UI.ClassCarousel>(true);
                Only(menu.transform, "Class Choice", null);
                foreach (int card in new[] { 2, 5, 0 })
                {
                    carousel.Jump(card);
                    Render(menu, camera, size, $"menu_class_choice_{card}_{suffix}");
                }
                carousel.Confirm();
                Render(menu, camera, size, $"menu_class_story_{suffix}");
                carousel.Back();

                hud.gameObject.SetActive(true);
                menu.gameObject.SetActive(false);
                foreach (string screen in new[] { "Pause Menu", "Dialogue" })
                {
                    Only(hud.transform, screen, "Safe Area");
                    Render(hud, camera, size, $"hud_{screen.Replace(' ', '_').ToLowerInvariant()}_{suffix}");
                }

                // The layout editor: controls and rings together.
                Only(hud.transform, "Safe Area", null);
                Transform handles = hud.transform.Find("Safe Area/Layout Handles");
                foreach (Transform handle in handles)
                {
                    handle.gameObject.SetActive(true);
                    handle.GetComponent<AshenRealm.UI.TouchLayoutHandle>().Follow();
                }
                Render(hud, camera, size, $"hud_layout_{suffix}");
                foreach (Transform handle in handles) handle.gameObject.SetActive(false);
                menu.gameObject.SetActive(true);
            }

            Object.DestroyImmediate(cameraGo);
            EditorSceneManager.OpenScene(GeneratedAssets.ScenePath, OpenSceneMode.Single); // drop the temporary changes
            Debug.Log($"[AshenRealm] Menu shots written to {Path.GetFullPath(OutputFolder)}");
        }

        /// <summary>Shows one child of the canvas (and optionally one behind it), the rest off.</summary>
        static void Only(Transform canvas, string shown, string behind)
        {
            foreach (Transform child in canvas)
            {
                bool on = child.name == shown || child.name == behind;
                child.gameObject.SetActive(on);
                if (on && child.TryGetComponent(out CanvasGroup group)) group.alpha = 1f;
            }
        }

        static void Render(Canvas canvas, Camera camera, Vector2Int size, string name)
        {
            var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;

            // Nothing updates a canvas in batch mode: toggling the scaler applies the new size, then force the layouts.
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.enabled = false;
            scaler.enabled = true;
            foreach (var layout in canvas.GetComponentsInChildren<RectTransform>())
                UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(layout);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture.active = target;
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(OutputFolder, name + ".png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
