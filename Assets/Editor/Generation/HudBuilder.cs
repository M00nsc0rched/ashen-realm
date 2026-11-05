using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.UI;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AshenRealm.EditorTools
{
    internal sealed class HudSprites
    {
        public Sprite Circle;
        public Sprite Ring;
        public Sprite Vignette;
        public Sprite StoneRing;
        public Sprite StoneDisc;
        public Sprite SwordIcon;
    }

    internal sealed class HudRefs
    {
        public PauseMenu Pause;
        public CompassStrip Compass;
    }

    /// <summary>
    /// Builds the HUD canvas in the style of the reference images: long thin bars and a compass at the top centre,
    /// a round weapon emblem top-left, pause top-right, stone-ring touch controls, overlays and the pause menu.
    /// Layout reference: 1920×1080 landscape, scaled by screen height.
    /// </summary>
    internal static class HudBuilder
    {
        const int UILayer = 5;
        static readonly Color Parchment = new Color(0.93f, 0.87f, 0.72f);
        static readonly Color FrameColor = new Color(0.12f, 0.1f, 0.09f, 0.95f);
        static readonly Color TroughColor = new Color(0.04f, 0.035f, 0.03f, 0.8f);
        static readonly Color MetalColor = new Color(0.62f, 0.6f, 0.58f, 1f);
        static readonly Color BrassColor = new Color(0.78f, 0.62f, 0.3f, 1f);

        // ---- Sprites ---------------------------------------------------------------------------------

        public static HudSprites CreateSprites()
        {
            string folder = GeneratedAssets.TextureFolder;
            return new HudSprites
            {
                Circle = GeneratedAssets.WriteTexture<Sprite>(Radial(128, r => 1f - Mathf.Clamp01((r - 0.94f) / 0.06f)), folder + "/UI_Circle.png", true, FilterMode.Bilinear),
                Ring = GeneratedAssets.WriteTexture<Sprite>(Radial(128, r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.9f) / 0.07f) * 1.4f), folder + "/UI_Ring.png", true, FilterMode.Bilinear),
                Vignette = GeneratedAssets.WriteTexture<Sprite>(Radial(256, r => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.45f) / 0.85f))), folder + "/UI_Vignette.png", true, FilterMode.Bilinear),
                StoneRing = GeneratedAssets.WriteTexture<Sprite>(StoneRingTexture(64, true), folder + "/UI_StoneRing.png", true, FilterMode.Point),
                StoneDisc = GeneratedAssets.WriteTexture<Sprite>(StoneRingTexture(32, false), folder + "/UI_StoneDisc.png", true, FilterMode.Point),
                SwordIcon = GeneratedAssets.WriteTexture<Sprite>(SwordIconTexture(), folder + "/UI_SwordIcon.png", true, FilterMode.Point),
            };
        }

        static Texture2D Radial(int size, System.Func<float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(r))));
            }
            texture.Apply();
            return texture;
        }

        /// <summary>Pixel-art stone ring (segmented blocks with bevel light from the top-left) or a solid stone disc.</summary>
        static Texture2D StoneRingTexture(int size, bool ring)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var rng = new System.Random(size);
            const int segments = 12;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);
                float px = 2f / size;
                Color c = new Color(0f, 0f, 0f, 0f);
                float inner = ring ? 0.7f : 0f;
                if (r <= 1f - px && r >= inner)
                {
                    float seg = (angle / (Mathf.PI * 2f) + 1f) * segments;
                    bool mortar = ring && Mathf.Abs(seg - Mathf.Round(seg)) * (Mathf.PI * 2f * r / segments) < px * 0.9f;
                    float light = Vector2.Dot(new Vector2(u, v).normalized, new Vector2(-0.6f, 0.8f));
                    float shade = 0.42f + (float)rng.NextDouble() * 0.06f + (ring ? light * 0.1f : (v - u) * 0.08f);
                    bool outerEdge = r > 1f - px * 2.5f, innerEdge = ring && r < inner + px * 2f;
                    if (outerEdge || innerEdge) shade -= 0.14f;
                    if (mortar) shade = 0.2f;
                    c = new Color(shade, shade * 0.97f, shade * 0.93f, 1f);
                }
                else if (ring && r < inner)
                {
                    c = new Color(0.04f, 0.04f, 0.05f, 0.42f);
                }
                else if (r <= 1f)
                {
                    c = new Color(0.08f, 0.07f, 0.06f, 1f);
                }
                texture.SetPixel(x, y, c);
            }
            texture.Apply();
            return texture;
        }

        static Texture2D SwordIconTexture()
        {
            const int size = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, clear);
            var outline = new Color(0.08f, 0.08f, 0.1f, 1f);
            var steel = new Color(0.82f, 0.85f, 0.9f, 1f);
            var steelDark = new Color(0.55f, 0.58f, 0.66f, 1f);
            var gold = new Color(0.85f, 0.66f, 0.3f, 1f);
            var leather = new Color(0.45f, 0.28f, 0.16f, 1f);
            // Blade on the diagonal from (6,6) to (21,21); guard across it near the bottom-left.
            for (int i = 6; i <= 21; i++)
            {
                texture.SetPixel(i, i, steel);
                texture.SetPixel(i + 1, i, steelDark);
                texture.SetPixel(i, i + 1, outline);
                texture.SetPixel(i + 1, i - 1, outline);
            }
            for (int k = -3; k <= 3; k++) texture.SetPixel(6 + k, 6 - k, gold);
            for (int i = 2; i <= 5; i++) texture.SetPixel(i, i, leather);
            texture.SetPixel(1, 1, gold);
            texture.Apply();
            return texture;
        }

        // ---- Pixel screen ------------------------------------------------------------------------------

        /// <summary>Full-screen RawImage behind the HUD that shows the low-resolution world render.</summary>
        public static RawImage BuildPixelScreen(Material composite)
        {
            var go = new GameObject("Pixel Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = UILayer;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;
            var image = Stretch("World", go.transform).gameObject.AddComponent<RawImage>();
            image.gameObject.layer = UILayer;
            image.material = composite;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        // ---- HUD ---------------------------------------------------------------------------------------

        public static HudRefs Build(HudSprites sprites, PlayerInputHub input, Health health, Stamina stamina, PlayerInteractor interactor,
            PlayerLifecycle lifecycle, CursedKnightAI enemy, Transform viewer, Vector3 objective)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<EventSystem>().pixelDragThreshold = 8;

            var canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var hud = canvasGo.AddComponent<HUDController>();

            var vignette = Stretch("Damage Vignette", canvasGo.transform);
            var vignetteImage = Img(vignette.gameObject, sprites.Vignette, new Color(0.5f, 0.02f, 0.02f, 0f));

            var safe = Stretch("Safe Area", canvasGo.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // ---- Top centre: compass, health, stamina ----
            var top = Anchored("Top Bar", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 120f));
            var compass = BuildCompass(top, font, viewer, objective);
            var healthBar = Bar("Health", top, new Vector2(40f, -50f), new Vector2(820f, 14f), new Color(0.66f, 0.1f, 0.08f), true,
                out RectTransform healthFill, out RectTransform healthChip, out _);
            var staminaBar = Bar("Stamina", top, new Vector2(140f, -72f), new Vector2(620f, 10f), new Color(0.24f, 0.44f, 0.86f), false,
                out RectTransform staminaFill, out _, out Image staminaImage);
            EndCaps(healthBar);
            EndCaps(staminaBar);

            // ---- Top-left weapon emblem, top-right pause ----
            var emblem = Anchored("Weapon Emblem", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -86f), new Vector2(120f, 120f));
            Img(emblem.gameObject, sprites.StoneRing, Color.white);
            var sword = Anchored("Sword", emblem, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            Img(sword.gameObject, sprites.SwordIcon, Color.white);

            var pauseRect = Anchored("Pause Button", safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-80f, -80f), new Vector2(96f, 96f));
            var pauseImage = Img(pauseRect.gameObject, sprites.StoneDisc, new Color(1f, 1f, 1f, 0.85f), true);
            foreach (float x in new[] { -13f, 13f })
            {
                var bar = Anchored("Bar", pauseRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(12f, 40f));
                Img(bar.gameObject, null, Parchment);
            }
            var pauseButton = pauseRect.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pauseImage;
            pauseButton.navigation = new Navigation { mode = Navigation.Mode.None };

            // ---- Centre ----
            var crosshair = Anchored("Crosshair", safe, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9f, 9f));
            var crosshairImage = Img(crosshair.gameObject, sprites.Circle, new Color(1f, 1f, 1f, 0.55f));

            var prompt = Anchored("Interaction Prompt", safe, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(900f, 50f));
            var promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
            promptGroup.blocksRaycasts = false;
            var promptText = Label(prompt, GameText.OpenChest, 30, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);

            var notify = Anchored("Notification", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1300f, 110f));
            var notifyGroup = notify.gameObject.AddComponent<CanvasGroup>();
            notifyGroup.blocksRaycasts = false;
            var notifyText = Label(notify, "", 32, new Color(0.96f, 0.9f, 0.72f), TextAnchor.MiddleCenter, font, FontStyle.Italic);

            // ---- Enemy bar (bottom centre) ----
            var enemyBar = Anchored("Enemy Bar", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(680f, 60f));
            var enemyGroup = enemyBar.gameObject.AddComponent<CanvasGroup>();
            enemyGroup.blocksRaycasts = false;
            var enemyName = Anchored("Name", enemyBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(680f, 30f));
            Label(enemyName, GameText.KnightName, 24, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);
            var enemyFrame = Bar("Health", enemyBar, new Vector2(20f, -36f), new Vector2(640f, 12f), new Color(0.55f, 0.07f, 0.06f), false, out RectTransform enemyFill, out _, out _);
            EndCaps(enemyFrame);

            // ---- Desktop controls hint ----
            var hint = Anchored("Desktop Hint", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1760f, 34f));
            Label(hint, GameText.DesktopHint,
                20, new Color(0.9f, 0.85f, 0.75f, 0.7f), TextAnchor.MiddleCenter, font, FontStyle.Normal);
            SceneWiring.Set(hint.gameObject.AddComponent<DesktopHint>(), "input", input);

            // ---- Touch controls ----
            var touchRoot = Stretch("Touch Controls", safe);
            touchRoot.gameObject.AddComponent<CanvasGroup>();
            SceneWiring.Set(touchRoot.gameObject.AddComponent<TouchControlsRoot>(), "input", input);

            var look = Rect("Look Area", touchRoot, new Vector2(0.4f, 0f), new Vector2(1f, 0.86f), Vector2.zero, Vector2.zero);
            Img(look.gameObject, null, new Color(0f, 0f, 0f, 0f), true);
            SceneWiring.Set(look.gameObject.AddComponent<TouchLookArea>(), "input", input);

            var moveZone = Rect("Move Zone", touchRoot, new Vector2(0f, 0f), new Vector2(0.4f, 0.78f), Vector2.zero, Vector2.zero);
            Img(moveZone.gameObject, null, new Color(0f, 0f, 0f, 0f), true);
            var stickBase = Anchored("Joystick Base", moveZone, Vector2.zero, Vector2.zero, new Vector2(280f, 260f), new Vector2(270f, 270f));
            var stickVisuals = stickBase.gameObject.AddComponent<CanvasGroup>();
            stickVisuals.alpha = 0.6f;
            stickVisuals.blocksRaycasts = false;
            Img(stickBase.gameObject, sprites.StoneRing, Color.white, false);
            var knob = Anchored("Knob", stickBase, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118f, 118f));
            Img(knob.gameObject, sprites.StoneDisc, new Color(0.85f, 0.82f, 0.78f, 0.95f), false);
            var joystick = moveZone.gameObject.AddComponent<TouchJoystick>();
            SceneWiring.Set(joystick, "input", input);
            SceneWiring.Set(joystick, "zone", moveZone);
            SceneWiring.Set(joystick, "baseRect", stickBase);
            SceneWiring.Set(joystick, "knob", knob);
            SceneWiring.Set(joystick, "visuals", stickVisuals);
            SceneWiring.Set(joystick, "radius", 100f);
            SceneWiring.Set(joystick, "idleAlpha", 0.6f);

            TouchButtonWidget(touchRoot, "Attack", GameText.Attack, PlayerButton.Attack, new Vector2(1f, 0f), new Vector2(-225f, 225f), 210f, 30, input, sprites, font);
            TouchButtonWidget(touchRoot, "Block", GameText.Block, PlayerButton.Block, new Vector2(1f, 0f), new Vector2(-480f, 150f), 150f, 24, input, sprites, font);
            TouchButtonWidget(touchRoot, "Jump", GameText.Jump, PlayerButton.Jump, new Vector2(1f, 0f), new Vector2(-150f, 470f), 130f, 22, input, sprites, font);
            var interactButton = TouchButtonWidget(touchRoot, "Interact", GameText.Use, PlayerButton.Interact, new Vector2(1f, 0f), new Vector2(-420f, 400f), 125f, 22, input, sprites, font);
            interactButton.gameObject.AddComponent<CanvasGroup>();
            TouchButtonWidget(touchRoot, "Sprint", GameText.Run, PlayerButton.Sprint, new Vector2(0f, 0f), new Vector2(110f, 500f), 120f, 22, input, sprites, font);

            // ---- Death overlay ----
            var death = Stretch("Death Overlay", canvasGo.transform);
            var deathGroup = death.gameObject.AddComponent<CanvasGroup>();
            deathGroup.blocksRaycasts = false;
            deathGroup.alpha = 0f;
            Img(death.gameObject, null, new Color(0.02f, 0.01f, 0.01f, 0.72f));
            var deathTitle = Anchored("Title", death, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1400f, 110f));
            Label(deathTitle, GameText.DeathTitle, 76, new Color(0.62f, 0.1f, 0.08f), TextAnchor.MiddleCenter, font, FontStyle.Bold);
            var deathSub = Anchored("Subtitle", death, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(1400f, 50f));
            Label(deathSub, GameText.DeathSubtitle, 28, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Italic);

            // ---- Pause menu (top-most) ----
            var pause = BuildPauseMenu(canvasGo.transform, input, font, sprites);
            UnityEventTools.AddPersistentListener(pauseButton.onClick, new UnityAction(pause.Toggle));

            // ---- Wire the HUD controller ----
            SceneWiring.Set(hud, "playerHealth", health);
            SceneWiring.Set(hud, "playerStamina", stamina);
            SceneWiring.Set(hud, "interactor", interactor);
            SceneWiring.Set(hud, "lifecycle", lifecycle);
            SceneWiring.Set(hud, "input", input);
            SceneWiring.Set(hud, "healthFill", healthFill);
            SceneWiring.Set(hud, "healthChip", healthChip);
            SceneWiring.Set(hud, "staminaFill", staminaFill);
            SceneWiring.Set(hud, "staminaFillImage", staminaImage);
            SceneWiring.Set(hud, "staminaColor", new Color(0.24f, 0.44f, 0.86f));
            SceneWiring.Set(hud, "crosshair", crosshairImage);
            SceneWiring.Set(hud, "promptGroup", promptGroup);
            SceneWiring.Set(hud, "promptText", promptText);
            SceneWiring.Set(hud, "interactButton", interactButton);
            SceneWiring.Set(hud, "notificationGroup", notifyGroup);
            SceneWiring.Set(hud, "notificationText", notifyText);
            SceneWiring.Set(hud, "damageVignette", vignetteImage);
            SceneWiring.Set(hud, "deathGroup", deathGroup);
            SceneWiring.Set(hud, "enemy", enemy);
            SceneWiring.Set(hud, "enemyBarGroup", enemyGroup);
            SceneWiring.Set(hud, "enemyFill", enemyFill);

            SetLayer(canvasGo.transform, UILayer);
            return new HudRefs { Pause = pause, Compass = compass };
        }

        static CompassStrip BuildCompass(RectTransform parent, Font font, Transform viewer, Vector3 objective)
        {
            var strip = Anchored("Compass", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(560f, 30f));
            var line = Anchored("Line", strip, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(560f, 2f));
            Img(line.gameObject, null, new Color(BrassColor.r, BrassColor.g, BrassColor.b, 0.7f));
            var centre = Anchored("Centre", strip, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(10f, 10f));
            centre.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Img(centre.gameObject, null, BrassColor);

            var graphics = new System.Collections.Generic.List<Graphic>();
            var headings = new System.Collections.Generic.List<float>();
            string[] names = GameText.CompassPoints;
            for (int i = 0; i < names.Length; i++)
            {
                var label = Anchored(names[i], strip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(60f, 30f));
                bool cardinal = i % 2 == 0;
                graphics.Add(Label(label, names[i], cardinal ? 24 : 17, cardinal ? Parchment : new Color(0.8f, 0.76f, 0.66f, 0.8f), TextAnchor.MiddleCenter, font, FontStyle.Bold));
                headings.Add(i * 45f);
            }
            for (int t = 0; t < 24; t++)
            {
                if (t % 3 == 0) continue;
                var tick = Anchored("Tick", strip, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(2f, 8f));
                graphics.Add(Img(tick.gameObject, null, new Color(0.9f, 0.85f, 0.72f, 0.55f)));
                headings.Add(t * 15f);
            }
            var marker = Anchored("Objective", strip, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(12f, 12f));
            marker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var markerImage = Img(marker.gameObject, null, new Color(0.9f, 0.3f, 0.2f, 1f));

            var compass = strip.gameObject.AddComponent<CompassStrip>();
            SceneWiring.Set(compass, "viewer", viewer);
            SceneWiring.Set(compass, "strip", strip);
            SceneWiring.Set(compass, "markers", graphics.ToArray());
            var so = new UnityEditor.SerializedObject(compass);
            var headingProp = so.FindProperty("headings");
            headingProp.arraySize = headings.Count;
            for (int i = 0; i < headings.Count; i++) headingProp.GetArrayElementAtIndex(i).floatValue = headings[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            SceneWiring.Set(compass, "objectiveMarker", markerImage);
            SceneWiring.Set(compass, "objectivePosition", objective);
            return compass;
        }

        static PauseMenu BuildPauseMenu(Transform canvas, PlayerInputHub input, Font font, HudSprites sprites)
        {
            var root = Stretch("Pause Menu", canvas);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Img(root.gameObject, null, new Color(0.02f, 0.02f, 0.03f, 0.7f), true);

            var panel = Anchored("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 560f));
            Img(panel.gameObject, null, FrameColor);
            var inner = Rect("Inner", panel, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            Img(inner.gameObject, null, new Color(0.17f, 0.14f, 0.12f, 0.97f));
            var title = Anchored("Title", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(560f, 70f));
            Label(title, GameText.Paused, 54, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);

            var menu = root.gameObject.AddComponent<PauseMenu>();
            Button MenuButton(string name, string caption, Vector2 position, Vector2 size, UnityAction action, out Text label)
            {
                var rect = Anchored(name, panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, size);
                var image = Img(rect.gameObject, null, new Color(0.3f, 0.25f, 0.2f, 1f), true);
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.15f, 1f);
                colors.pressedColor = new Color(0.75f, 0.7f, 0.65f);
                button.colors = colors;
                label = Label(Stretch("Label", rect), caption, 30, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);
                UnityEventTools.AddPersistentListener(button.onClick, action);
                return button;
            }

            MenuButton("Resume", GameText.Resume, new Vector2(0f, -160f), new Vector2(420f, 72f), menu.Resume, out _);
            MenuButton("Fullscreen", GameText.Fullscreen, new Vector2(0f, -250f), new Vector2(420f, 72f), menu.ToggleFullscreen, out Text fullscreenLabel);
            MenuButton("Look Down", "-", new Vector2(-215f, -340f), new Vector2(80f, 72f), menu.SensitivityDown, out _);
            MenuButton("Look Up", "+", new Vector2(215f, -340f), new Vector2(80f, 72f), menu.SensitivityUp, out _);
            var sensitivity = Anchored("Look Speed", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(330f, 72f));
            var sensitivityLabel = Label(sensitivity, GameText.LookSpeed + "  " + GameText.Decimal(1f), 24, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);
            var help = Anchored("Help", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(560f, 100f));
            Label(help, GameText.Objective, 22,
                new Color(0.85f, 0.8f, 0.7f, 0.9f), TextAnchor.MiddleCenter, font, FontStyle.Italic);

            SceneWiring.Set(menu, "input", input);
            SceneWiring.Set(menu, "group", group);
            SceneWiring.Set(menu, "sensitivityLabel", sensitivityLabel);
            SceneWiring.Set(menu, "fullscreenLabel", fullscreenLabel);
            return menu;
        }

        // ---- Widgets -----------------------------------------------------------------------------------

        static RectTransform Bar(string name, RectTransform parent, Vector2 topLeft, Vector2 size, Color fillColor, bool withChip,
            out RectTransform fill, out RectTransform chip, out Image fillImage)
        {
            var frame = Anchored(name + " Bar", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, size);
            frame.pivot = new Vector2(0f, 1f);
            frame.anchoredPosition = topLeft;
            Img(frame.gameObject, null, FrameColor);
            var rim = Rect("Rim", frame, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f));
            Img(rim.gameObject, null, new Color(MetalColor.r * 0.55f, MetalColor.g * 0.55f, MetalColor.b * 0.55f, 1f));

            var trough = Rect("Trough", frame, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            Img(trough.gameObject, null, TroughColor);

            chip = null;
            if (withChip)
            {
                chip = Rect("Chip", trough, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Img(chip.gameObject, null, new Color(0.9f, 0.78f, 0.58f, 0.55f));
            }

            fill = Rect("Fill", trough, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fillImage = Img(fill.gameObject, null, fillColor);
            var sheen = Rect("Sheen", fill, new Vector2(0f, 0.55f), Vector2.one, Vector2.zero, Vector2.zero);
            Img(sheen.gameObject, null, new Color(1f, 1f, 1f, 0.16f));
            return frame;
        }

        /// <summary>Metal diamond caps on both ends of a bar.</summary>
        static void EndCaps(RectTransform bar)
        {
            float height = bar.sizeDelta.y + 12f;
            foreach (float x in new[] { 0f, 1f })
            {
                var cap = Anchored("Cap", bar, new Vector2(x, 0.5f), new Vector2(x, 0.5f), Vector2.zero, new Vector2(height, height));
                cap.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Img(cap.gameObject, null, MetalColor);
                var inner = Rect("Inner", cap, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
                Img(inner.gameObject, null, new Color(0.22f, 0.2f, 0.2f, 1f));
            }
        }

        static TouchButton TouchButtonWidget(RectTransform parent, string name, string caption, PlayerButton action, Vector2 anchor, Vector2 position,
            float diameter, int fontSize, PlayerInputHub input, HudSprites sprites, Font font)
        {
            var rect = Anchored(name + " Button", parent, anchor, anchor, position, new Vector2(diameter, diameter));
            var background = Img(rect.gameObject, sprites.Circle, new Color(0.08f, 0.07f, 0.06f, 0.55f), true);
            var ring = Stretch("Ring", rect);
            Img(ring.gameObject, sprites.StoneRing, new Color(1f, 1f, 1f, 0.95f), false);
            var label = Stretch("Label", rect);
            Label(label, caption, fontSize, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);

            var button = rect.gameObject.AddComponent<TouchButton>();
            SceneWiring.Set(button, "input", input);
            SceneWiring.Set(button, "button", action);
            SceneWiring.Set(button, "background", background);
            return button;
        }

        static Text Label(RectTransform rect, string text, int size, Color color, TextAnchor alignment, Font font, FontStyle style)
        {
            var t = rect.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        static Image Img(GameObject go, Sprite sprite, Color color, bool raycast = false)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayer(child, layer);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        static RectTransform Stretch(string name, Transform parent) => Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        static RectTransform Anchored(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
    }
}
