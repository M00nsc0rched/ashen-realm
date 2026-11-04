using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.UI;
using UnityEngine;
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
    }

    /// <summary>
    /// Builds the HUD canvas: vitals, crosshair, prompts, notifications, overlays, enemy bar and the
    /// multitouch control layer (floating joystick, look surface, action buttons) inside a safe area.
    /// Layout reference: 1920×1080 landscape, scaled by screen height.
    /// </summary>
    internal static class HudBuilder
    {
        static readonly Color Parchment = new Color(0.92f, 0.86f, 0.72f);
        static readonly Color FrameColor = new Color(0.16f, 0.12f, 0.08f, 0.92f);
        static readonly Color TroughColor = new Color(0.04f, 0.035f, 0.03f, 0.85f);
        static readonly Color BrassColor = new Color(0.72f, 0.56f, 0.28f, 1f);

        public static HudSprites CreateSprites()
        {
            return new HudSprites
            {
                Circle = GeneratedAssets.WriteTexture<Sprite>(Radial(128, r => 1f - Mathf.Clamp01((r - 0.94f) / 0.06f)), GeneratedAssets.TextureFolder + "/UI_Circle.png", true, FilterMode.Bilinear),
                Ring = GeneratedAssets.WriteTexture<Sprite>(Radial(128, r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.9f) / 0.07f) * 1.4f), GeneratedAssets.TextureFolder + "/UI_Ring.png", true, FilterMode.Bilinear),
                Vignette = GeneratedAssets.WriteTexture<Sprite>(Radial(256, r => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.45f) / 0.85f))), GeneratedAssets.TextureFolder + "/UI_Vignette.png", true, FilterMode.Bilinear),
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

        public static void Build(HudSprites sprites, PlayerInputHub input, Health health, Stamina stamina, PlayerInteractor interactor,
            PlayerLifecycle lifecycle, CursedKnightAI enemy)
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

            // Full-screen overlays sit outside the safe area so they cover the whole display.
            var vignette = Stretch("Damage Vignette", canvasGo.transform);
            var vignetteImage = Img(vignette.gameObject, sprites.Vignette, new Color(0.5f, 0.02f, 0.02f, 0f));

            var safe = Stretch("Safe Area", canvasGo.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // ---- Vitals (top-left) ----
            var vitals = Anchored("Vitals", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(480f, 80f));
            vitals.pivot = new Vector2(0f, 1f);
            vitals.anchoredPosition = new Vector2(44f, -40f);
            var healthBar = Bar("Health", vitals, new Vector2(30f, 0f), new Vector2(430f, 26f), new Color(0.62f, 0.08f, 0.07f), true,
                out RectTransform healthFill, out RectTransform healthChip, out _);
            var staminaBar = Bar("Stamina", vitals, new Vector2(30f, -38f), new Vector2(330f, 16f), new Color(0.72f, 0.64f, 0.32f), false,
                out RectTransform staminaFill, out _, out Image staminaImage);
            Diamond(vitals, new Vector2(14f, -13f), 26f);
            Diamond(vitals, new Vector2(14f, -46f), 18f);
            _ = healthBar;
            _ = staminaBar;

            // ---- Centre ----
            var crosshair = Anchored("Crosshair", safe, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9f, 9f));
            var crosshairImage = Img(crosshair.gameObject, sprites.Circle, new Color(1f, 1f, 1f, 0.55f));

            var prompt = Anchored("Interaction Prompt", safe, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(900f, 50f));
            var promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
            promptGroup.blocksRaycasts = false;
            var promptText = Label(prompt, "OPEN CHEST", 30, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);

            var notify = Anchored("Notification", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1300f, 110f));
            var notifyGroup = notify.gameObject.AddComponent<CanvasGroup>();
            notifyGroup.blocksRaycasts = false;
            var notifyText = Label(notify, "", 32, new Color(0.95f, 0.88f, 0.7f), TextAnchor.MiddleCenter, font, FontStyle.Italic);

            // ---- Enemy bar (bottom centre) ----
            var enemyBar = Anchored("Enemy Bar", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(680f, 60f));
            var enemyGroup = enemyBar.gameObject.AddComponent<CanvasGroup>();
            enemyGroup.blocksRaycasts = false;
            var enemyName = Anchored("Name", enemyBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(680f, 30f));
            Label(enemyName, "CURSED KNIGHT", 24, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);
            Bar("Health", enemyBar, new Vector2(20f, -36f), new Vector2(640f, 12f), new Color(0.55f, 0.07f, 0.06f), false, out RectTransform enemyFill, out _, out _);

            // ---- Desktop controls hint ----
            var hint = Anchored("Desktop Hint", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1700f, 34f));
            var hintText = Label(hint, "WASD move  ·  Shift run  ·  Space jump  ·  LMB strike  ·  RMB block  ·  E interact  ·  F1 touch controls  ·  Esc free cursor",
                20, new Color(0.85f, 0.8f, 0.7f, 0.65f), TextAnchor.MiddleCenter, font, FontStyle.Normal);
            var hintToggle = hint.gameObject.AddComponent<DesktopHint>();
            SceneWiring.Set(hintToggle, "input", input);
            _ = hintText;

            // ---- Touch controls ----
            var touchRoot = Stretch("Touch Controls", safe);
            touchRoot.gameObject.AddComponent<CanvasGroup>();
            var touchControls = touchRoot.gameObject.AddComponent<TouchControlsRoot>();
            SceneWiring.Set(touchControls, "input", input);

            var look = Rect("Look Area", touchRoot, new Vector2(0.4f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            Img(look.gameObject, null, new Color(0f, 0f, 0f, 0f), true);
            var lookArea = look.gameObject.AddComponent<TouchLookArea>();
            SceneWiring.Set(lookArea, "input", input);

            var moveZone = Rect("Move Zone", touchRoot, new Vector2(0f, 0f), new Vector2(0.4f, 0.78f), Vector2.zero, Vector2.zero);
            Img(moveZone.gameObject, null, new Color(0f, 0f, 0f, 0f), true);
            var stickBase = Anchored("Joystick Base", moveZone, Vector2.zero, Vector2.zero, new Vector2(270f, 250f), new Vector2(250f, 250f));
            stickBase.pivot = new Vector2(0.5f, 0.5f);
            var stickVisuals = stickBase.gameObject.AddComponent<CanvasGroup>();
            stickVisuals.alpha = 0.45f;
            stickVisuals.blocksRaycasts = false;
            Img(stickBase.gameObject, sprites.Circle, new Color(0.05f, 0.04f, 0.035f, 0.45f), false);
            var ring = Stretch("Ring", stickBase);
            Img(ring.gameObject, sprites.Ring, new Color(BrassColor.r, BrassColor.g, BrassColor.b, 0.7f), false);
            var knob = Anchored("Knob", stickBase, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112f, 112f));
            Img(knob.gameObject, sprites.Circle, new Color(0.78f, 0.7f, 0.55f, 0.85f), false);
            var joystick = moveZone.gameObject.AddComponent<TouchJoystick>();
            SceneWiring.Set(joystick, "input", input);
            SceneWiring.Set(joystick, "zone", moveZone);
            SceneWiring.Set(joystick, "baseRect", stickBase);
            SceneWiring.Set(joystick, "knob", knob);
            SceneWiring.Set(joystick, "visuals", stickVisuals);
            SceneWiring.Set(joystick, "radius", 105f);

            Button(touchRoot, "Attack", "STRIKE", PlayerButton.Attack, new Vector2(1f, 0f), new Vector2(-225f, 225f), 210f, 30, input, sprites, font);
            Button(touchRoot, "Block", "BLOCK", PlayerButton.Block, new Vector2(1f, 0f), new Vector2(-480f, 150f), 150f, 24, input, sprites, font);
            Button(touchRoot, "Jump", "JUMP", PlayerButton.Jump, new Vector2(1f, 0f), new Vector2(-150f, 470f), 130f, 22, input, sprites, font);
            var interactButton = Button(touchRoot, "Interact", "USE", PlayerButton.Interact, new Vector2(1f, 0f), new Vector2(-420f, 400f), 125f, 22, input, sprites, font);
            interactButton.gameObject.AddComponent<CanvasGroup>();
            Button(touchRoot, "Sprint", "RUN", PlayerButton.Sprint, new Vector2(0f, 0f), new Vector2(110f, 500f), 120f, 22, input, sprites, font);

            // ---- Death overlay (top-most) ----
            var death = Stretch("Death Overlay", canvasGo.transform);
            var deathGroup = death.gameObject.AddComponent<CanvasGroup>();
            deathGroup.blocksRaycasts = false;
            deathGroup.alpha = 0f;
            Img(death.gameObject, null, new Color(0.02f, 0.01f, 0.01f, 0.72f));
            var deathTitle = Anchored("Title", death, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1400f, 110f));
            Label(deathTitle, "YOU HAVE FALLEN", 76, new Color(0.62f, 0.1f, 0.08f), TextAnchor.MiddleCenter, font, FontStyle.Bold);
            var deathSub = Anchored("Subtitle", death, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(1400f, 50f));
            Label(deathSub, "The ash remembers. You rise again...", 28, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Italic);

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
        }

        // ---- Widgets -----------------------------------------------------------------------------------

        static RectTransform Bar(string name, RectTransform parent, Vector2 topLeft, Vector2 size, Color fillColor, bool withChip,
            out RectTransform fill, out RectTransform chip, out Image fillImage)
        {
            var frame = Anchored(name + " Bar", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, size);
            frame.pivot = new Vector2(0f, 1f);
            frame.anchoredPosition = topLeft;
            Img(frame.gameObject, null, FrameColor);

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
            var sheen = Rect("Sheen", fill, new Vector2(0f, 0.6f), Vector2.one, Vector2.zero, Vector2.zero);
            Img(sheen.gameObject, null, new Color(1f, 1f, 1f, 0.12f));
            return frame;
        }

        static void Diamond(RectTransform parent, Vector2 center, float size)
        {
            var d = Anchored("Ornament", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), center, new Vector2(size, size));
            d.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Img(d.gameObject, null, BrassColor);
            var inner = Rect("Inner", d, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            Img(inner.gameObject, null, new Color(0.35f, 0.06f, 0.05f, 1f));
        }

        static TouchButton Button(RectTransform parent, string name, string caption, PlayerButton action, Vector2 anchor, Vector2 position,
            float diameter, int fontSize, PlayerInputHub input, HudSprites sprites, Font font)
        {
            var rect = Anchored(name + " Button", parent, anchor, anchor, position, new Vector2(diameter, diameter));
            var background = Img(rect.gameObject, sprites.Circle, new Color(0.08f, 0.07f, 0.06f, 0.55f), true);
            var ring = Stretch("Ring", rect);
            Img(ring.gameObject, sprites.Ring, new Color(BrassColor.r, BrassColor.g, BrassColor.b, 0.75f), false);
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
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
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
