using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using AshenRealm.Story;
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
        public Sprite Diamond;
        public Sprite Star;
        public Sprite PanelFill;
    }

    internal sealed class HudRefs
    {
        public PauseMenu Pause;
        public CompassStrip Compass;
        public DialoguePanel Dialogue;
        public ShopPanel Shop;
        public TouchLayout Layout;
        public CanvasGroup Fade;
        public CanvasGroup ZoneTitle;
        public Text ZoneTitleText;
        public Text ZoneSubtitleText;
    }

    /// <summary>
    /// Builds the HUD canvas in the style of the reference images: long thin bars and a compass at the top centre,
    /// a round weapon emblem top-left, pause top-right, stone-ring touch controls, overlays and the pause menu.
    /// Layout reference: 1920×1080 landscape, scaled by screen height.
    /// </summary>
    internal static class HudBuilder
    {
        const int UILayer = 5;
        const int PanelCorner = 18;
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
                Diamond = GeneratedAssets.WriteTexture<Sprite>(Shape(64, (u, v) => Band(Mathf.Abs(u) + Mathf.Abs(v), 0.78f, 0.1f)), folder + "/UI_Diamond.png", true, FilterMode.Bilinear),
                Star = GeneratedAssets.WriteTexture<Sprite>(Shape(64, (u, v) => StarAlpha(u, v)), folder + "/UI_Star.png", true, FilterMode.Bilinear),
                PanelFill = GeneratedAssets.WriteTexture<Sprite>(Shape(64, CutCorners), folder + "/UI_PanelFill.png", true, FilterMode.Bilinear,
                    spriteBorder: PanelCorner + 2),
            };
        }

        static Texture2D Shape(int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v))));
            }
            texture.Apply();
            return texture;
        }

        /// <summary>Solid, with corners cut like the menu frame, so the dark fill never pokes out past the gold.</summary>
        static float CutCorners(float u, float v)
        {
            float fromEdgeX = (1f - Mathf.Abs(u)) * 32f, fromEdgeY = (1f - Mathf.Abs(v)) * 32f;
            return (fromEdgeX + fromEdgeY - PanelCorner) / 1.5f;
        }

        /// <summary>Soft-edged band around a value: 1 within half the width of the centre, 0 a little further out.</summary>
        static float Band(float value, float centre, float halfWidth) => Mathf.Clamp01((halfWidth - Mathf.Abs(value - centre)) / 0.04f);

        /// <summary>The four-pointed star of the gold counter: a circle pinched in by a low power.</summary>
        static float StarAlpha(float u, float v)
        {
            float shape = Mathf.Pow(Mathf.Abs(u), 0.55f) + Mathf.Pow(Mathf.Abs(v), 0.55f);
            return Mathf.Clamp01((0.98f - shape) / 0.08f);
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

        public static HudRefs Build(HudSprites sprites, MenuArt menuArt, PlayerInputHub input, Health health, Stamina stamina, PlayerInteractor interactor,
            PlayerLifecycle lifecycle, PlayerAbilities abilities, PlayerCombat combat, Transform viewer, Vector3 objective)
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

            // Everything the HUD settings fade or hide. Prompts and messages stay outside: see HudAppearance.
            var status = Stretch("HUD Elements", safe);
            var statusGroup = status.gameObject.AddComponent<CanvasGroup>();
            statusGroup.blocksRaycasts = false;
            SceneWiring.Set(status.gameObject.AddComponent<HudAppearance>(), "elements", statusGroup);

            // ---- Top centre: compass, health, stamina ----
            var top = Anchored("Top Bar", status, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 120f));
            var compass = BuildCompass(top, font, viewer, objective);
            var healthBar = Bar("Health", top, new Vector2(40f, -50f), new Vector2(820f, 14f), new Color(0.66f, 0.1f, 0.08f), true,
                out RectTransform healthFill, out RectTransform healthChip, out _);
            var staminaBar = Bar("Stamina", top, new Vector2(140f, -72f), new Vector2(620f, 10f), new Color(0.24f, 0.44f, 0.86f), false,
                out RectTransform staminaFill, out _, out Image staminaImage);
            var focusBar = Bar("Focus", top, new Vector2(240f, -88f), new Vector2(420f, 8f), new Color(0.62f, 0.3f, 0.82f), false,
                out RectTransform focusFill, out _, out _);
            EndCaps(healthBar);
            EndCaps(staminaBar);
            EndCaps(focusBar);

            // ---- Top-left weapon emblem, top-right pause ----
            var emblem = Anchored("Weapon Emblem", status, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -86f), new Vector2(120f, 120f));
            Img(emblem.gameObject, sprites.StoneRing, Color.white);
            var sword = Anchored("Sword", emblem, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            Image weaponEmblem = Img(sword.gameObject, EquipmentIconBuilder.ClassSprites()[0], Color.white);
            weaponEmblem.preserveAspect = true;

            var statusBox = Anchored("Status", status, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(290f, -92f), new Vector2(300f, 110f));
            var statusText = Label(statusBox, GameText.AlignmentMortal, 21, Parchment, TextAnchor.UpperLeft, font, FontStyle.Bold);

                        // ---- Centre ----
            var crosshair = Anchored("Crosshair", status, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9f, 9f));
            var crosshairImage = Img(crosshair.gameObject, sprites.Circle, new Color(1f, 1f, 1f, 0.55f));

            var prompt = Anchored("Interaction Prompt", safe, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(900f, 50f));
            var promptGroup = prompt.gameObject.AddComponent<CanvasGroup>();
            promptGroup.blocksRaycasts = false;
            var promptText = Label(prompt, GameText.OpenChest, 30, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);

            var notify = Anchored("Notification", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1300f, 110f));
            var notifyGroup = notify.gameObject.AddComponent<CanvasGroup>();
            notifyGroup.blocksRaycasts = false;
            var notifyText = Label(notify, "", 32, new Color(0.96f, 0.9f, 0.72f), TextAnchor.MiddleCenter, font, FontStyle.Italic);

            // ---- Enemy bar (under the player bars, clear of the touch buttons) ----
            var enemyBar = Anchored("Enemy Bar", status, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(680f, 60f));
            var enemyGroup = enemyBar.gameObject.AddComponent<CanvasGroup>();
            enemyGroup.blocksRaycasts = false;
            var enemyName = Anchored("Name", enemyBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(680f, 30f));
            var enemyNameText = Label(enemyName, GameText.KnightName, 24, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);
            var enemyFrame = Bar("Health", enemyBar, new Vector2(20f, -36f), new Vector2(640f, 12f), new Color(0.55f, 0.07f, 0.06f), false, out RectTransform enemyFill, out _, out _);
            EndCaps(enemyFrame);

            // ---- Desktop controls hint ----
            var hint = Anchored("Desktop Hint", status, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1760f, 34f));
            Label(hint, GameText.DesktopHint,
                20, new Color(0.9f, 0.85f, 0.75f, 0.7f), TextAnchor.MiddleCenter, font, FontStyle.Normal);
            SceneWiring.Set(hint.gameObject.AddComponent<DesktopHint>(), "input", input);

            // ---- Touch controls ----
            var touchRoot = Stretch("Touch Controls", safe);
            touchRoot.gameObject.AddComponent<CanvasGroup>();
            var touchControls = touchRoot.gameObject.AddComponent<TouchControlsRoot>();
            SceneWiring.Set(touchControls, "input", input);

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

            var attackButton = TouchButtonWidget(touchRoot, "Attack", GameText.Attack, PlayerButton.Attack, new Vector2(1f, 0f), new Vector2(-225f, 225f), 210f, 30, input, sprites, font);
            var blockButton = TouchButtonWidget(touchRoot, "Block", GameText.Block, PlayerButton.Block, new Vector2(1f, 0f), new Vector2(-480f, 150f), 150f, 24, input, sprites, font);
            var jumpButton = TouchButtonWidget(touchRoot, "Jump", GameText.Jump, PlayerButton.Jump, new Vector2(1f, 0f), new Vector2(-150f, 470f), 130f, 22, input, sprites, font);
            var interactButton = TouchButtonWidget(touchRoot, "Interact", GameText.Use, PlayerButton.Interact, new Vector2(1f, 0f), new Vector2(-420f, 400f), 125f, 22, input, sprites, font);
            interactButton.gameObject.AddComponent<CanvasGroup>();
            var sprintButton = TouchButtonWidget(touchRoot, "Sprint", GameText.Run, PlayerButton.Sprint, new Vector2(0f, 0f), new Vector2(110f, 500f), 120f, 22, input, sprites, font);
            var ability1Button = TouchButtonWidget(touchRoot, "Ability 1", GameText.Attack, PlayerButton.Ability1, new Vector2(1f, 0f), new Vector2(-690f, 130f), 140f, 19, input, sprites, font);
            var ability2Button = TouchButtonWidget(touchRoot, "Ability 2", GameText.Attack, PlayerButton.Ability2, new Vector2(1f, 0f), new Vector2(-650f, 320f), 140f, 19, input, sprites, font);
            Image AbilityGlyph(Transform button, AbilityEffect effect)
            {
                var glyph = Anchored("Ability Icon", button, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0f, 18f), new Vector2(64f, 64f));
                Text label = button.GetComponentInChildren<Text>();
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.one * 0.5f;
                label.rectTransform.anchoredPosition = new Vector2(0f, -34f);
                label.rectTransform.sizeDelta = new Vector2(115f, 42f);
                label.fontSize = 17;
                return Img(glyph.gameObject, AbilityIconBuilder.Create()[(int)effect], Color.white);
            }
            Image ability1Icon = AbilityGlyph(ability1Button.transform, AbilityEffect.ShieldBash);
            Image ability2Icon = AbilityGlyph(ability2Button.transform, AbilityEffect.Rally);
            var potionButton = TouchButtonWidget(touchRoot, "Potion", GameText.Potion, PlayerButton.Potion, new Vector2(0f, 0f), new Vector2(110f, 660f), 110f, 20, input, sprites, font);
            var ability1Shade = CooldownShade(ability1Button.transform, sprites);
            var ability2Shade = CooldownShade(ability2Button.transform, sprites);

            // ---- Pause button: after the touch layer, so it can be dragged anywhere and still be pressed ----
            var pauseRect = Anchored("Pause Button", safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-80f, -80f), new Vector2(96f, 96f));
            // Keep the same menu button above the pause backdrop, while inventory and other menu sheets still cover it.
            var pauseCanvas = pauseRect.gameObject.AddComponent<Canvas>();
            pauseCanvas.overrideSorting = true;
            pauseCanvas.sortingOrder = 20;
            pauseRect.gameObject.AddComponent<GraphicRaycaster>();
            var pauseImage = Img(pauseRect.gameObject, sprites.StoneDisc, new Color(1f, 1f, 1f, 0.85f), true);
            foreach (float x in new[] { -13f, 13f })
            {
                var bar = Anchored("Bar", pauseRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(12f, 40f));
                Img(bar.gameObject, null, Parchment);
            }
            var pauseButton = pauseRect.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pauseImage;
            pauseButton.navigation = new Navigation { mode = Navigation.Mode.None };
            SceneWiring.Set(touchControls, "pauseButton", pauseRect.gameObject.AddComponent<CanvasGroup>());
            SceneWiring.Set(touchControls, "combat", combat);
            var fightingButtons = new[] { attackButton, blockButton, ability1Button, ability2Button };
            SceneWiring.Set(touchControls, "fightingButtons", System.Array.ConvertAll(fightingButtons, b => b.gameObject.AddComponent<CanvasGroup>()));

            // ---- Layout editor rings: one per control, above them all, off until the editor opens ----
            var handleRoot = Stretch("Layout Handles", safe);
            var handlesCanvas = handleRoot.gameObject.AddComponent<Canvas>();
            handlesCanvas.overrideSorting = true;
            handlesCanvas.sortingOrder = 25;
            handleRoot.gameObject.AddComponent<GraphicRaycaster>();
            var handles = new System.Collections.Generic.List<TouchLayoutHandle>();
            void Movable(string layoutName, RectTransform target, RectTransform marker) =>
                handles.Add(LayoutHandle(layoutName, handleRoot, target, marker, safe, canvas, sprites));
            void MovableButton(string layoutName, Component button) => Movable(layoutName, (RectTransform)button.transform, (RectTransform)button.transform);
            MovableButton("attack", attackButton);
            MovableButton("block", blockButton);
            MovableButton("jump", jumpButton);
            MovableButton("interact", interactButton);
            MovableButton("sprint", sprintButton);
            MovableButton("ability1", ability1Button);
            MovableButton("ability2", ability2Button);
            MovableButton("potion", potionButton);
            MovableButton("pause", pauseRect);
            Movable("joystick", moveZone, stickBase);
            var touchLayout = touchRoot.gameObject.AddComponent<TouchLayout>();
            SceneWiring.Set(touchLayout, "controls", touchControls);
            SceneWiring.Set(touchLayout, "handles", handles.ToArray());

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

            // ---- Zone title card and the travel fade ----
            var title = Stretch("Zone Title", canvasGo.transform);
            var titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            titleGroup.blocksRaycasts = false;
            titleGroup.alpha = 0f;
            var titleRect = Anchored("Title", title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1500f, 100f));
            var titleText = Label(titleRect, "", 68, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);
            var subtitleRect = Anchored("Subtitle", title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(1400f, 60f));
            var subtitleText = Label(subtitleRect, "", 28, new Color(0.9f, 0.84f, 0.7f), TextAnchor.MiddleCenter, font, FontStyle.Italic);

            var fade = Stretch("Travel Fade", canvasGo.transform);
            var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.blocksRaycasts = false;
            fadeGroup.alpha = 0f;
            Img(fade.gameObject, null, Color.black);

            // ---- Pause menu and dialogue (top-most) ----
            var pause = BuildPauseMenu(canvasGo.transform, input, menuArt);
            SceneWiring.Set(pause, "controls", touchControls);
            var holdMenu = pauseButton.gameObject.AddComponent<HoldMenuButton>();
            SceneWiring.Set(holdMenu, "menu", pause);
            UnityEventTools.AddPersistentListener(pauseButton.onClick, new UnityAction(holdMenu.Click));
            var dialogue = BuildDialogue(canvasGo.transform, input, font);
            SceneWiring.Set(pause, "dialogue", dialogue);
            var shop = BuildShop(canvasGo.transform, input, menuArt);
            SceneWiring.Set(pause, "shop", shop);

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
            SceneWiring.Set(hud, "enemyName", enemyNameText);
            SceneWiring.Set(hud, "abilities", abilities);
            SceneWiring.Set(hud, "focusFill", focusFill);
            SceneWiring.Set(hud, "statusText", statusText);
            SceneWiring.Set(hud, "ability1Label", ability1Button.GetComponentInChildren<Text>());
            SceneWiring.Set(hud, "ability2Label", ability2Button.GetComponentInChildren<Text>());
            SceneWiring.Set(hud, "potionLabel", potionButton.GetComponentInChildren<Text>());
            SceneWiring.Set(hud, "ability1Cooldown", ability1Shade);
            SceneWiring.Set(hud, "ability2Cooldown", ability2Shade);
            SceneWiring.Set(hud, "ability1Icon", ability1Icon);
            SceneWiring.Set(hud, "ability2Icon", ability2Icon);
            SceneWiring.Set(hud, "skillIcons", AbilityIconBuilder.Create());
            SceneWiring.Set(hud, "weaponEmblem", weaponEmblem);
            SceneWiring.Set(hud, "classWeaponIcons", EquipmentIconBuilder.ClassSprites());
            SceneWiring.Set(hud, "goodsWeaponIcons", EquipmentIconBuilder.GoodsSprites());
            SceneWiring.Set(hud, "enemyBarGroup", enemyGroup);
            SceneWiring.Set(hud, "enemyFill", enemyFill);

            SetLayer(canvasGo.transform, UILayer);
            return new HudRefs
            {
                Pause = pause, Compass = compass, Dialogue = dialogue, Shop = shop, Layout = touchLayout, Fade = fadeGroup, ZoneTitle = titleGroup,
                ZoneTitleText = titleText, ZoneSubtitleText = subtitleText,
            };
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

        static PauseMenu BuildPauseMenu(Transform canvas, PlayerInputHub input, MenuArt art)
        {
            var root = Stretch("Pause Menu", canvas);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Img(root.gameObject, null, new Color(0.02f, 0.02f, 0.03f, 0.6f), true);

            const float width = 1250f, left = 442f, buttonWidth = 750f, buttonHeight = 74f;
            var panel = MenuBuilder.Panel("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 880f), art);
            MenuBuilder.Write(MenuBuilder.Box("Title", panel, 0f, 20f, width, 92f), InventoryText.DescentMenu, 58, new Color(0.97f, 0.8f, 0.52f), TextAnchor.MiddleCenter, art.SemiBold);
            var portrait = Img(MenuBuilder.Box("Soul Portrait", panel, 60f, 140f, 330f, 484f).gameObject, art.Cards[0], Color.white);
            portrait.preserveAspect = true;
            var portraitPresenter = portrait.gameObject.AddComponent<ClassPortrait>();
            SceneWiring.Set(portraitPresenter, "image", portrait);
            SceneWiring.Set(portraitPresenter, "portraits", art.Cards);

            var menu = root.gameObject.AddComponent<PauseMenu>();
            MenuBuilder.GoldButton("Inventory", panel, left, 140f, buttonWidth, buttonHeight, InventoryText.InventoryAndSkills, 36, art, new UnityAction(menu.OpenInventory), out _);
            MenuBuilder.GoldButton("Codex", panel, left, 229f, buttonWidth, buttonHeight, MenuText.Codex, 36, art, new UnityAction(menu.OpenCodex), out _);
            MenuBuilder.GoldButton("Settings", panel, left, 318f, buttonWidth, buttonHeight, MenuText.Settings, 36, art, new UnityAction(menu.OpenSettings), out _);
            MenuBuilder.GoldButton("Fullscreen", panel, left, 407f, buttonWidth, buttonHeight, MenuText.Fullscreen, 36, art, new UnityAction(menu.ToggleFullscreen), out Text fullscreenLabel);
            MenuBuilder.GoldButton("Save", panel, left, 496f, buttonWidth, buttonHeight, InventoryText.Save, 32, art, new UnityAction(menu.SaveSoul), out _);
            MenuBuilder.GoldButton("Main Menu", panel, left, 585f, buttonWidth, buttonHeight, MenuText.MainMenu, 36, art, new UnityAction(menu.ToMainMenu), out _);
            var helpText = MenuBuilder.Write(MenuBuilder.Box("Objective", panel, 56f, 682f, width - 112f, 75f), GameText.Objective, 25,
                new Color(0.88f, 0.83f, 0.72f), TextAnchor.UpperCenter, art.Italic);
            var resume = MenuBuilder.GoldButton("Resume", panel, 60f, 778f, width - 120f, 68f, MenuText.Resume, 40, art, new UnityAction(menu.Resume), out _);

            SceneWiring.Set(menu, "input", input);
            SceneWiring.Set(menu, "group", group);
            SceneWiring.Set(menu, "fullscreenLabel", fullscreenLabel);
            SceneWiring.Set(menu, "objectiveLabel", helpText);
            SceneWiring.Set(menu, "firstButton", resume);
            return menu;
        }

        /// <summary>The counter of the forge and the chapel, in the menus' frame: the goods in a row, the chosen one's words under them.</summary>
        static ShopPanel BuildShop(Transform canvas, PlayerInputHub input, MenuArt art)
        {
            var root = Stretch("Shop", canvas);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Img(root.gameObject, null, new Color(0.02f, 0.02f, 0.03f, 0.6f), true);

            const float width = 1260f, height = 960f, tileWidth = 268f, tileHeight = 322f;
            Color cream = new Color(0.93f, 0.88f, 0.77f), gold = new Color(0.97f, 0.8f, 0.52f);
            var panel = MenuBuilder.Panel("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height), art);
            var shop = root.gameObject.AddComponent<ShopPanel>();

            var title = MenuBuilder.Write(MenuBuilder.Box("Title", panel, 0f, 22f, width, 70f), "", 52, gold, TextAnchor.MiddleCenter, art.SemiBold);
            var balance = MenuBuilder.Write(MenuBuilder.Box("Balance", panel, 0f, 92f, width, 44f), "", 30, cream, TextAnchor.MiddleCenter, art.Italic);

            var tiles = new ShopTile[4];
            for (int i = 0; i < tiles.Length; i++)
            {
                var rect = Anchored("Tile " + i, panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -304f), new Vector2(tileWidth, tileHeight));
                var fill = Img(rect.gameObject, art.PanelFill, new Color(0.1f, 0.08f, 0.07f, 0.92f), true);
                fill.type = Image.Type.Sliced;
                var frame = Rect("Frame", rect, Vector2.zero, Vector2.one, new Vector2(-8f, -8f), new Vector2(8f, 8f));
                Img(frame.gameObject, art.Button, Color.white).type = Image.Type.Sliced;
                var glow = Rect("Chosen", rect, Vector2.zero, Vector2.one, new Vector2(-8f, -8f), new Vector2(8f, 8f));
                var highlight = Img(glow.gameObject, art.Glow, Color.white);
                highlight.type = Image.Type.Sliced;
                var picture = Img(MenuBuilder.Box("Picture", rect, 14f, 12f, tileWidth - 28f, tileHeight - 72f).gameObject, null, Color.white);
                picture.preserveAspect = true;
                var price = MenuBuilder.Write(MenuBuilder.Box("Price", rect, 0f, tileHeight - 58f, tileWidth, 50f), "", 30, gold, TextAnchor.MiddleCenter, art.SemiBold);

                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                UnityEventTools.AddIntPersistentListener(button.onClick, shop.Select, i);
                var tile = rect.gameObject.AddComponent<ShopTile>();
                SceneWiring.Set(tile, "panel", shop);
                SceneWiring.Set(tile, "index", i);
                SceneWiring.Set(tile, "button", button);
                SceneWiring.Set(tile, "picture", picture);
                SceneWiring.Set(tile, "priceText", price);
                SceneWiring.Set(tile, "highlight", highlight);
                tiles[i] = tile;
            }

            var itemName = MenuBuilder.Write(MenuBuilder.Box("Name", panel, 0f, 476f, width, 62f), "", 44, gold, TextAnchor.MiddleCenter, art.SemiBold);
            var mods = MenuBuilder.Write(MenuBuilder.Box("Mods", panel, 0f, 538f, width, 44f), "", 32, cream, TextAnchor.MiddleCenter, art.Regular);
            var description = MenuBuilder.Write(MenuBuilder.Box("Description", panel, 50f, 584f, width - 100f, 44f), "", 31, cream, TextAnchor.MiddleCenter, art.Italic);
            var effect = MenuBuilder.Write(MenuBuilder.Box("Effect", panel, 0f, 630f, width, 44f), "", 30, gold, TextAnchor.MiddleCenter, art.Regular);
            var notice = MenuBuilder.Write(MenuBuilder.Box("Notice", panel, 50f, 678f, width - 100f, 84f), "", 30, cream, TextAnchor.MiddleCenter, art.Italic);

            MenuBuilder.GoldButton("Back", panel, 70f, 790f, 520f, 120f, MenuText.Back, 40, art, new UnityAction(shop.Close), out _);
            var buy = MenuBuilder.GoldButton("Buy", panel, 670f, 790f, 520f, 120f, MenuText.Buy, 40, art, new UnityAction(shop.Buy), out Text buyLabel);

            SceneWiring.Set(shop, "input", input);
            SceneWiring.Set(shop, "group", group);
            SceneWiring.Set(shop, "titleText", title);
            SceneWiring.Set(shop, "balanceText", balance);
            SceneWiring.Set(shop, "nameText", itemName);
            SceneWiring.Set(shop, "modsText", mods);
            SceneWiring.Set(shop, "descriptionText", description);
            SceneWiring.Set(shop, "effectText", effect);
            SceneWiring.Set(shop, "noticeText", notice);
            SceneWiring.Set(shop, "tiles", tiles);
            SceneWiring.Set(shop, "buyButton", buy);
            SceneWiring.Set(shop, "buyLabel", buyLabel);
            SceneWiring.Set(shop, "art", MenuBuilder.LoadItems());
            return shop;
        }

        static DialoguePanel BuildDialogue(Transform canvas, PlayerInputHub input, Font font)
        {
            var root = Stretch("Dialogue", canvas);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Img(root.gameObject, null, new Color(0.02f, 0.02f, 0.03f, 0.55f), true);

            var panel = Anchored("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 860f));
            Img(panel.gameObject, null, FrameColor);
            var inner = Rect("Inner", panel, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            Img(inner.gameObject, null, new Color(0.17f, 0.14f, 0.12f, 0.97f));

            var speaker = Anchored("Speaker", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(1120f, 64f));
            var speakerText = Label(speaker, "", 40, BrassColor, TextAnchor.MiddleCenter, font, FontStyle.Bold);
            var body = Anchored("Body", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(1120f, 370f));
            var bodyText = Label(body, "", 30, Parchment, TextAnchor.UpperLeft, font, FontStyle.Normal);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.resizeTextForBestFit = true;
            bodyText.resizeTextMinSize = 20;
            bodyText.resizeTextMaxSize = 30;
            bodyText.verticalOverflow = VerticalWrapMode.Truncate;

            // The speaker's face, when they have one: the text steps aside for it.
            var portraitFrame = Anchored("Portrait", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-402f, -280f), new Vector2(330f, 330f));
            Img(portraitFrame.gameObject, null, BrassColor);
            var portraitRect = Rect("Face", portraitFrame, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var portrait = Img(portraitRect.gameObject, null, Color.white);
            portraitFrame.gameObject.SetActive(false);

            var menu = root.gameObject.AddComponent<DialoguePanel>();
            var buttons = new Button[5];
            var labels = new Text[5];
            for (int i = 0; i < buttons.Length; i++)
            {
                var rect = Anchored("Choice " + i, panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 352f - i * 78f), new Vector2(1120f, 68f));
                var image = Img(rect.gameObject, null, new Color(0.3f, 0.25f, 0.2f, 1f), true);
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.navigation = new Navigation { mode = Navigation.Mode.Vertical };
                var colors = button.colors;
                colors.highlightedColor = new Color(1.25f, 1.15f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(0.75f, 0.7f, 0.65f);
                button.colors = colors;
                labels[i] = Label(Stretch("Label", rect), "", 26, Parchment, TextAnchor.MiddleCenter, font, FontStyle.Bold);
                UnityEventTools.AddIntPersistentListener(button.onClick, menu.Choose, i);
                buttons[i] = button;
            }

            SceneWiring.Set(menu, "input", input);
            SceneWiring.Set(menu, "group", group);
            SceneWiring.Set(menu, "speakerText", speakerText);
            SceneWiring.Set(menu, "bodyText", bodyText);
            SceneWiring.Set(menu, "portraitFrame", portraitFrame.gameObject);
            SceneWiring.Set(menu, "portrait", portrait);
            SceneWiring.Set(menu, "buttons", buttons);
            SceneWiring.Set(menu, "labels", labels);
            return menu;
        }

        /// <summary>The gold ring the layout editor puts over a control. It follows the control, so its own place does not matter.</summary>
        static TouchLayoutHandle LayoutHandle(string layoutName, RectTransform parent, RectTransform target, RectTransform marker, RectTransform bounds, Canvas canvas, HudSprites sprites)
        {
            Vector2 centre = new Vector2(0.5f, 0.5f);
            float size = Mathf.Max(marker.sizeDelta.x, marker.sizeDelta.y) + 24f;
            var rect = Anchored(layoutName + " Handle", parent, centre, centre, Vector2.zero, new Vector2(size, size));
            Img(rect.gameObject, sprites.Ring, new Color(0.97f, 0.8f, 0.45f, 0.95f), true);
            var handle = rect.gameObject.AddComponent<TouchLayoutHandle>();
            SceneWiring.Set(handle, "layoutName", layoutName);
            SceneWiring.Set(handle, "target", target);
            SceneWiring.Set(handle, "marker", marker);
            SceneWiring.Set(handle, "bounds", bounds);
            SceneWiring.Set(handle, "canvas", canvas);
            rect.gameObject.SetActive(false);
            return handle;
        }

        /// <summary>Radial shade that sweeps away as an ability recovers.</summary>
        static Image CooldownShade(Transform button, HudSprites sprites)
        {
            var rect = Stretch("Cooldown", button);
            var image = Img(rect.gameObject, sprites.Circle, new Color(0f, 0f, 0f, 0.62f));
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = 2;
            image.fillAmount = 0f;
            return image;
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

        internal static Image Img(GameObject go, Sprite sprite, Color color, bool raycast = false)
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

        internal static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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

        internal static RectTransform Stretch(string name, Transform parent) => Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        internal static RectTransform Anchored(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
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
