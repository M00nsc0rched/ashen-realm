using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AshenRealm.Combat;
using AshenRealm.Player;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AshenRealm.EditorTools
{
    /// <summary>
    /// Measures how much of the frame the held items cover at rest: the real camera renders each class's kit twice, with the
    /// hand renderers on and off, and every pixel that changes counts. The crosshair and the middle of the frame must stay clear.
    /// </summary>
    internal static class HandCoveragePlaytestChecks
    {
        public const float MaxWideShare = 0.15f, MaxSquareShare = 0.18f;
        const int Height = 360;
        public static readonly List<string> Report = new List<string>();

        public static IEnumerable<float> Run(Action<bool, string> check)
        {
            var controller = Object.FindAnyObjectByType<FirstPersonController>();
            var zones = Object.FindAnyObjectByType<ZoneManager>();
            var session = GameSession.Instance;
            var growth = PlayerProgression.Instance;
            var combat = controller != null ? controller.GetComponent<PlayerCombat>() : null;
            if (controller == null || zones == null || session == null || growth == null || combat == null)
            { check(false, "Hand coverage needs the player, zones and a soul"); yield break; }
            Camera camera = controller.GetComponentsInChildren<Camera>(true).First(c => c.CompareTag("MainCamera"));
            Transform rig = camera.transform.Find("Weapon Rig");
            Transform torchMount = camera.transform.Find("Carried Torch");
            CharacterClass oldClass = session.Class.Id;
            bool oldTorch = growth.TorchEquipped;
            Directory.CreateDirectory(PreviewCapture.OutputFolder);
            Report.Clear();
            try
            {
                zones.Place(ZoneNumbers.Valley, Vector3.zero, 0f, false);
                for (int classId = 0; classId < 6; classId++)
                {
                    session.ChooseClass((CharacterClass)classId);
                    foreach (bool torch in new[] { false, true })
                    {
                        if (growth.TorchEquipped != torch) growth.ToggleTorch();
                        // The same calm spot every time: the valley road looking north at the ruins.
                        Vector3 spot = Ground(0f, -30f);
                        controller.Teleport(spot + Vector3.up * 0.05f, 0f, 0f);
                        combat.SetArmed(true);
                        yield return 1.2f; // the weapon animator settles into its rest pose
                        var renderers = new List<Renderer>();
                        renderers.AddRange(rig.GetComponentsInChildren<Renderer>(false));
                        renderers.AddRange(torchMount.GetComponentsInChildren<Renderer>(false));
                        string name = $"{(CharacterClass)classId}{(torch ? "_torch" : "")}";
                        Measure(camera, renderers, Height * 16 / 9, out float wide, out bool wideCentre, "hands_" + name + "_16x9");
                        Measure(camera, renderers, Height * 4 / 3, out float square, out bool squareCentre, "hands_" + name + "_4x3");
                        string line = $"{name}: {wide * 100f:F1}% at 16:9, {square * 100f:F1}% at 4:3, centre {(wideCentre && squareCentre ? "clear" : "covered")}";
                        Report.Add(line);
                        check(wide <= MaxWideShare && square <= MaxSquareShare && wideCentre && squareCentre,
                            $"Held items stay peripheral at rest for {line} (limits {MaxWideShare * 100f:F0}%/{MaxSquareShare * 100f:F0}%)");
                    }
                }
            }
            finally
            {
                if (growth.TorchEquipped != oldTorch) growth.ToggleTorch();
                session.ChooseClass(oldClass);
            }
        }

        /// <summary>The share of pixels that the held items change, and whether the crosshair and middle stay untouched.</summary>
        internal static void Measure(Camera camera, List<Renderer> renderers, int width, out float share, out bool centreClear, string file) =>
            Measure(camera, renderers, width, out share, out centreClear, out _, file);

        /// <summary>The same, with the highest changed row as a share of the height (0 bottom, 1 top).</summary>
        internal static void Measure(Camera camera, List<Renderer> renderers, int width, out float share, out bool centreClear, out float top, string file)
        {
            Color32[] with = Render(camera, width, Height);
            foreach (Renderer r in renderers) r.enabled = false;
            Color32[] without;
            try { without = Render(camera, width, Height); }
            finally { foreach (Renderer r in renderers) r.enabled = true; }
            int covered = 0;
            centreClear = true;
            int highest = -1;
            var mask = new Color32[with.Length];
            // The middle: a third of the width and of the height around the crosshair.
            int x0 = width / 3, x1 = width * 2 / 3, y0 = Height / 3, y1 = Height * 2 / 3;
            for (int i = 0; i < with.Length; i++)
            {
                Color32 a = with[i], b = without[i];
                bool changed = Mathf.Abs(a.r - b.r) > 3 || Mathf.Abs(a.g - b.g) > 3 || Mathf.Abs(a.b - b.b) > 3;
                mask[i] = changed ? new Color32((byte)(a.r / 2 + 127), (byte)(a.g / 3), (byte)(a.b / 3), 255) : a;
                if (!changed) continue;
                covered++;
                int x = i % width, y = i / width;
                highest = Mathf.Max(highest, y);
                if (x >= x0 && x < x1 && y >= y0 && y < y1) centreClear = false;
            }
            share = covered / (float)with.Length;
            top = (highest + 1) / (float)Height;
            var image = new Texture2D(width, Height, TextureFormat.RGBA32, false);
            image.SetPixels32(with);
            image.Apply();
            File.WriteAllBytes(Path.Combine(PreviewCapture.OutputFolder, file + ".png"), image.EncodeToPNG());
            image.SetPixels32(mask);
            image.Apply();
            File.WriteAllBytes(Path.Combine(PreviewCapture.OutputFolder, file + "_mask.png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        static Color32[] Render(Camera camera, int width, int height)
        {
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.aspect = width / (float)height;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                Color32[] pixels = image.GetPixels32();
                Object.DestroyImmediate(image);
                return pixels;
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                camera.ResetAspect();
                RenderTexture.ReleaseTemporary(target);
            }
        }

        static Vector3 Ground(float x, float z) =>
            Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : new Vector3(x, 0f, z);
    }
}
