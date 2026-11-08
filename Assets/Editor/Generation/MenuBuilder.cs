using System.Linq;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Story;
using AshenRealm.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AshenRealm.EditorTools
{
    internal sealed class MenuArt
    {
        public Font Regular;
        public Font Italic;
        public Font SemiBold;
        public Sprite Frame;
        public Sprite Button;
        public Sprite Glow;
        public Sprite Divider;
        public Sprite Title;
        public Sprite Diamond;
        public Sprite Star;
        public Sprite PanelFill;
        public Sprite CardGlow;
        public Sprite[] Cards;
        public Sprite LockedCard;
        public Texture2D Background;
        public Texture2D ChoiceBackground;
    }

    /// <summary>
    /// Builds the menu canvas after the design sketches: main menu, class choice, Codex, settings, the yes-or-no box and the bar
    /// of the button layout editor. Coordinates are the sketches' own (origin at each panel's top-left corner, 863 units tall),
    /// so a change in a sketch can be copied straight across. The art is cut from the sketches by tools/prepare-menu-art.ps1.
    /// </summary>
    internal static class MenuBuilder
    {
        const string ArtFolder = "Assets/Art/Menu";
        const string FontFolder = "Assets/Art/Fonts";
        const int UILayer = 5;
        const int ButtonBorder = 42;
        const int FrameBorder = 52;
        const float ButtonRim = 8f; // transparent margin of the button sprites, room for the glow
        const float BackgroundAspect = 1844f / 852f;
        const float ChoiceMarkerRoom = 40f;
        const int GlowBorder = 26;
        const int CardQuality = 85;
        const string LockedCardArt = "CardLocked";
        const int PortraitMoods = 3;
        static readonly string[] PortraitPeople = { "wanderer", "landlord", "quartermaster", "gravedigger", "crier", "widow" };
        static readonly string[] OneFacePeople = { "smith", "choir" };
        const float ChoiceLeft = 272f;    // where the class choice concept's content begins, in its own pixels
        const float ChoiceWidth = 1290f;
        static readonly Vector2 CardSize = new Vector2(421f, 616f); // the sprite with its margins, 395 wide at the frame

        static readonly Color Cream = new Color(0.93f, 0.88f, 0.77f);
        static readonly Color Gold = new Color(0.9f, 0.7f, 0.4f);
        static readonly Color BrightGold = new Color(0.97f, 0.8f, 0.52f);
        static readonly Color DimGold = new Color(0.62f, 0.45f, 0.24f);
        static readonly Color Ink = new Color(0.022f, 0.018f, 0.015f, 0.95f);
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        static readonly Color Violet = new Color(0.62f, 0.42f, 0.88f);
        static readonly Color FaintGold = new Color(0.62f, 0.45f, 0.24f, 0.5f);
        static readonly Color CardLight = new Color(1f, 0.6f, 0.22f, 0f);

        // ---- Art -----------------------------------------------------------------------------------------

        public static MenuArt LoadArt(HudSprites sprites) => new MenuArt
        {
            Regular = LoadFont("Spectral-Regular.ttf"),
            Italic = LoadFont("Spectral-Italic.ttf"),
            SemiBold = LoadFont("Spectral-SemiBold.ttf"),
            Frame = ImportSprite("MenuFrame.png", FrameBorder),
            Button = ImportSprite("MenuButton.png", ButtonBorder),
            Glow = ImportSprite("MenuButtonGlow.png", ButtonBorder),
            Divider = ImportSprite("MenuDivider.png", 0),
            Title = ImportSprite("MenuTitle.png", 0),
            Diamond = sprites.Diamond,
            Star = sprites.Star,
            PanelFill = sprites.PanelFill,
            CardGlow = ImportSprite("CardGlow.png", GlowBorder),
            Cards = Catalog.Cards.Select(card => ImportCard(card.Art)).ToArray(),
            LockedCard = ImportCard(LockedCardArt),
            Background = ImportBackground("MenuBackground.jpg"),
            ChoiceBackground = ImportBackground("ChoiceBackground.jpg"),
        };

        static Font LoadFont(string file)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{file}");
            if (font == null) throw new System.InvalidOperationException($"Missing font {FontFolder}/{file}");
            return font;
        }

        static Sprite ImportSprite(string file, int border)
        {
            string path = $"{ArtFolder}/{file}";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.InvalidOperationException($"Missing menu art {path}. Run tools/prepare-menu-art.ps1.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Every face of Hollowgate, for the dialogue box. Named person_mood, which is how the game finds them.</summary>
        public static Sprite[] LoadPortraits()
        {
            var faces = new System.Collections.Generic.List<Sprite>();
            foreach (string person in PortraitPeople)
                for (int mood = 0; mood < PortraitMoods; mood++)
                    faces.Add(ImportPainting($"{ArtFolder}/Portraits/{person}_{mood}.jpg"));
            foreach (string person in OneFacePeople)
                faces.Add(ImportPainting($"{ArtFolder}/Portraits/{person}_0.jpg"));
            return faces.ToArray();
        }

        /// <summary>The goods on the shop's tiles, named by their item ids.</summary>
        public static Sprite[] LoadItems()
        {
            var goods = new System.Collections.Generic.List<Sprite>();
            foreach (ItemDefinition item in Catalog.Items)
                goods.Add(ImportPainting($"{ArtFolder}/Items/{item.Id}.png"));
            return goods.ToArray();
        }

        /// <summary>The cards are paintings and six of them must stay light on a phone, so unlike the frames they are compressed.</summary>
        static Sprite ImportCard(string name) => ImportPainting($"{ArtFolder}/Cards/{name}.png");

        static Sprite ImportPainting(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.InvalidOperationException($"Missing art {path}. Run tools/prepare-menu-art.ps1.");

            // Crunching takes minutes, so only when the settings are not in place yet.
            if (importer.textureType != TextureImporterType.Sprite || !importer.crunchedCompression || importer.compressionQuality != CardQuality)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.crunchedCompression = true;
                importer.compressionQuality = CardQuality;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D ImportBackground(string file)
        {
            string path = $"{ArtFolder}/{file}";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.InvalidOperationException($"Missing menu art {path}. Run tools/prepare-menu-art.ps1.");
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- Canvas --------------------------------------------------------------------------------------

        public static void Build(MenuArt art, PlayerInputHub input, Camera worldCamera, SoulKeeper keeper, StoryDirector director,
            PauseMenu pause, TouchLayout layout)
        {
            var canvasGo = new GameObject("Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1500f, 863f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            MainMenu main = canvasGo.AddComponent<MainMenu>();
            SettingsPanel settings = canvasGo.AddComponent<SettingsPanel>();
            CodexPanel codex = canvasGo.AddComponent<CodexPanel>();
            ConfirmPanel confirm = canvasGo.AddComponent<ConfirmPanel>();

            BuildMainMenu(canvasGo.transform, art, main, input, worldCamera, keeper, director, settings, codex, confirm);
            BuildClassChoice(canvasGo.transform, art, main, canvas);
            BuildCodex(canvasGo.transform, art, codex, keeper);
            BuildSettings(canvasGo.transform, art, settings, input, keeper, confirm, layout, main);
            BuildConfirm(canvasGo.transform, art, confirm);
            BuildLayoutBar(canvasGo.transform, art, layout);
            InventoryMenuBuilder.Build(canvasGo.transform, art, keeper, pause);

            SceneWiring.Set(pause, "mainMenu", main);
            SceneWiring.Set(pause, "settings", settings);
            SceneWiring.Set(pause, "keeper", keeper);
            SceneWiring.Set(pause, "codex", codex);

            SetLayer(canvasGo.transform);
        }

        static void BuildMainMenu(Transform canvas, MenuArt art, MainMenu menu, PlayerInputHub input, Camera worldCamera, SoulKeeper keeper,
            StoryDirector director, SettingsPanel settings, CodexPanel codex, ConfirmPanel confirm)
        {
            var root = HudBuilder.Stretch("Main Menu", canvas);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var background = HudBuilder.Stretch("Background", root);
            var picture = background.gameObject.AddComponent<RawImage>();
            picture.texture = art.Background;
            picture.raycastTarget = true;
            var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = BackgroundAspect;

            var safe = HudBuilder.Stretch("Safe Area", root);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var panelGroup = safe.gameObject.AddComponent<CanvasGroup>();
            var panel = Panel("Panel", safe, new Vector2(0f, 0.5f), new Vector2(366f, 0f), new Vector2(628f, 745f), art);

            Picture("Title", panel, 42f, 26f, 544f, 76f, art.Title, Color.white);
            Picture("Divider", panel, 42f, 112f, 544f, 24f, art.Divider, Color.white);

            Picture("Soul Mark", panel, 40f, 158f, 30f, 30f, art.Diamond, Gold);
            var souls = Write(Box("Souls", panel, 84f, 144f, 500f, 58f), string.Format(MenuText.SoulsFormat, 0), 40, Cream, TextAnchor.MiddleLeft, art.Regular);
            var stats = Write(Box("Stats", panel, 40f, 206f, 560f, 34f), string.Format(MenuText.StatsFormat, 0, 0, 0, CodexBook.Entries.Count),
                22, Cream, TextAnchor.MiddleLeft, art.Regular);

            Write(Box("Heading", panel, 40f, 264f, 170f, 44f), MenuText.YourSouls, 30, Gold, TextAnchor.MiddleLeft, art.Italic);
            var rule = Box("Rule", panel, 205f, 288f, 395f, 2f);
            HudBuilder.Img(rule.gameObject, null, DimGold);

            // The roster: one soul at a time, with arrows once there are several.
            var slot = GoldButton("Soul Slot", panel, 28f, 325f, 574f, 126f, null, 0, art, new UnityAction(menu.ContinueSoul), out _);
            slot.GetComponentInChildren<Image>().sprite = art.Glow;
            var slotTitle = Write(Box("Title", slot.transform, 34f, 12f, 490f, 56f), string.Format(MenuText.SlotTitleFormat, MenuText.WithArticle(Catalog.Classes[1].Name), 1),
                36, BrightGold, TextAnchor.MiddleLeft, art.SemiBold);
            // "Az Üreges Boszorkány – Szint 12" would break onto a second line at full size.
            slotTitle.resizeTextForBestFit = true;
            slotTitle.resizeTextMinSize = 24;
            slotTitle.resizeTextMaxSize = 36;
            slotTitle.verticalOverflow = VerticalWrapMode.Truncate;
            var line = Box("Details", slot.transform, 34f, 72f, 410f, 36f);
            var row = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.spacing = 7f;
            var honorGold = Write(HudBuilder.Stretch("Honor And Gold", line), $"{MenuText.TierNeutral}   ·   20", 25, Cream, TextAnchor.MiddleLeft, art.Italic);
            var star = HudBuilder.Stretch("Star", line);
            HudBuilder.Img(star.gameObject, art.Star, Cream);
            var starSize = star.gameObject.AddComponent<LayoutElement>();
            starSize.minWidth = starSize.preferredWidth = 22f;
            starSize.minHeight = starSize.preferredHeight = 22f;
            var state = Write(HudBuilder.Stretch("State", line), "·   " + MenuText.Untouched, 25, Cream, TextAnchor.MiddleLeft, art.Italic);
            Write(Box("Chevron", slot.transform, 522f, 26f, 40f, 66f), MenuText.ChevronRight, 62, Gold, TextAnchor.MiddleCenter, art.SemiBold);

            var empty = Box("Empty Roster", panel, 44f, 330f, 542f, 116f);
            Write(empty, MenuText.NoSouls, 25, Cream, TextAnchor.MiddleCenter, art.Italic);
            empty.gameObject.SetActive(false);

            var release = TextButton("Release Soul", panel, 450f, 408f, 134f, 42f, MenuText.ReleaseSoul, 20, Cream, art.Italic, new UnityAction(menu.ReleaseSoul));

            var pager = Box("Pager", panel, 452f, 264f, 150f, 44f);
            TextButton("Previous", pager, 0f, 0f, 44f, 44f, MenuText.ChevronLeft, 36, Gold, art.SemiBold, new UnityAction(menu.PreviousSoul));
            var page = Write(Box("Page", pager, 44f, 0f, 62f, 44f), string.Format(MenuText.PageFormat, 1, 2), 22, Cream, TextAnchor.MiddleCenter, art.Regular);
            TextButton("Next", pager, 106f, 0f, 44f, 44f, MenuText.ChevronRight, 36, Gold, art.SemiBold, new UnityAction(menu.NextSoul));
            pager.gameObject.SetActive(false);

            var newSoul = GoldButton("New Soul", panel, 28f, 482f, 280f, 82f, MenuText.NewSoul, 36, art, new UnityAction(menu.NewSoul), out _);
            GoldButton("Codex", panel, 332f, 482f, 270f, 82f, MenuText.Codex, 36, art, new UnityAction(menu.OpenCodex), out _);
            GoldButton("Settings", panel, 28f, 588f, 574f, 84f, MenuText.Settings, 36, art, new UnityAction(menu.OpenSettings), out _);

            Picture("Bottom Divider", panel, 164f, 694f, 300f, 14f, art.Divider, Color.white);

            SceneWiring.Set(menu, "input", input);
            SceneWiring.Set(menu, "group", group);
            SceneWiring.Set(menu, "panelGroup", panelGroup);
            SceneWiring.Set(menu, "worldCamera", worldCamera);
            SceneWiring.Set(menu, "keeper", keeper);
            SceneWiring.Set(menu, "director", director);
            SceneWiring.Set(menu, "settings", settings);
            SceneWiring.Set(menu, "codex", codex);
            SceneWiring.Set(menu, "confirm", confirm);
            SceneWiring.Set(menu, "soulsLabel", souls);
            SceneWiring.Set(menu, "statsLabel", stats);
            SceneWiring.Set(menu, "slot", slot.gameObject);
            SceneWiring.Set(menu, "slotTitle", slotTitle);
            SceneWiring.Set(menu, "slotHonorGold", honorGold);
            SceneWiring.Set(menu, "slotState", state);
            SceneWiring.Set(menu, "emptyRoster", empty.gameObject);
            SceneWiring.Set(menu, "pager", pager.gameObject);
            SceneWiring.Set(menu, "pageLabel", page);
            SceneWiring.Set(menu, "releaseButton", release.gameObject);
            SceneWiring.Set(menu, "headingRule", rule);
            SceneWiring.Set(menu, "slotButton", slot);
            SceneWiring.Set(menu, "newSoulButton", newSoul);
        }

        /// <summary>The cards on the left, the sheet of the middle one on the right. Coordinates: the concept's, through X().</summary>
        static void BuildClassChoice(Transform canvas, MenuArt art, MainMenu menu, Canvas menuCanvas)
        {
            static float X(float concept) => concept - ChoiceLeft;

            var root = HudBuilder.Stretch("Class Choice", canvas);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var background = HudBuilder.Stretch("Background", root);
            var picture = background.gameObject.AddComponent<RawImage>();
            picture.texture = art.ChoiceBackground;
            picture.raycastTarget = true;
            var fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = BackgroundAspect;

            var safe = HudBuilder.Stretch("Safe Area", root);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var content = HudBuilder.Anchored("Content", safe, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ChoiceWidth, 863f));

            Picture("Title", content, X(712f), 10f, 424f, 59f, art.Title, Color.white);
            Picture("Divider", content, X(722f), 69f, 404f, 18f, art.Divider, Color.white);
            Write(Box("Subtitle", content, X(540f), 88f, 766f, 36f), MenuText.Tracked(MenuText.ChooseSoul), 24, Cream, TextAnchor.MiddleCenter, art.Regular);
            HudBuilder.Img(Box("Left Rule", content, X(405f), 105f, 130f, 2f).gameObject, null, DimGold);
            HudBuilder.Img(Box("Right Rule", content, X(1293f), 105f, 127f, 2f).gameObject, null, DimGold);

            // The row: a transparent surface takes the swipes, the cards themselves never catch a pointer.
            var deck = Box("Deck", content, X(284f), 123f, 560f, 624f);
            HudBuilder.Img(deck.gameObject, null, Clear, true);
            var carousel = deck.gameObject.AddComponent<ClassCarousel>();
            var row = HudBuilder.Stretch("Cards", deck);

            int count = Catalog.Cards.Length;
            var cards = new RectTransform[count];
            var cardArt = new Image[count];
            var cardGlow = new Image[count];
            for (int i = 0; i < count; i++)
            {
                cards[i] = HudBuilder.Anchored(Catalog.Cards[i].Art, row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, CardSize);
                var glow = HudBuilder.Rect("Glow", cards[i], Vector2.zero, Vector2.one, new Vector2(-7f, 4f), new Vector2(7f, -27f));
                cardGlow[i] = HudBuilder.Img(glow.gameObject, art.CardGlow, CardLight);
                cardGlow[i].type = Image.Type.Sliced;
                glow.gameObject.SetActive(Catalog.Cards[i].Class != null); // a rectangle of light does not fit the sealed card's arch
                cardArt[i] = HudBuilder.Img(HudBuilder.Stretch("Art", cards[i]).gameObject, art.Cards[i], Color.white);
            }

            var pager = HudBuilder.Stretch("Pager", content);
            var previous = TextButton("Previous", pager, X(353f), 730f, 64f, 64f, MenuText.ChevronLeft, 46, Gold, art.SemiBold, new UnityAction(carousel.Previous));
            var next = TextButton("Next", pager, X(711f), 730f, 64f, 64f, MenuText.ChevronRight, 46, Gold, art.SemiBold, new UnityAction(carousel.Next));
            previous.navigation = new Navigation { mode = Navigation.Mode.None };
            next.navigation = new Navigation { mode = Navigation.Mode.None };
            var dots = new Image[count];
            for (int i = 0; i < count; i++)
                dots[i] = Gem($"Dot {i}", pager, X(564f + (i - (count - 1) * 0.5f) * 31f), 762f, 12f, Gold);
            Write(Box("Hint", pager, X(444f), 785f, 240f, 32f), MenuText.SwipeHint, 23, new Color(Cream.r, Cream.g, Cream.b, 0.75f), TextAnchor.MiddleCenter, art.Italic);

            var prologue = Box("Prologue", content, X(326f), 748f, 476f, 100f);
            Write(prologue, StoryText.Prologue, 21, new Color(Cream.r, Cream.g, Cream.b, 0.8f), TextAnchor.UpperCenter, art.Italic);
            prologue.gameObject.SetActive(false);

            // The sheet.
            var panel = Panel("Panel", content, new Vector2(0f, 1f), Vector2.zero, new Vector2(618f, 623f), art);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(X(937f), -158f);

            var details = HudBuilder.Stretch("Details", panel);
            var detailsGroup = details.gameObject.AddComponent<CanvasGroup>();
            var name = Write(Box("Name", details, 52f, 14f, 524f, 62f), Catalog.Cards[0].Name, 46, Cream, TextAnchor.MiddleLeft, art.SemiBold);
            var role = Write(Box("Role", details, 52f, 72f, 524f, 38f), Catalog.Cards[0].Role, 29, Gold, TextAnchor.MiddleLeft, art.Italic);
            Picture("Divider", details, 42f, 112f, 534f, 20f, art.Divider, Color.white);

            var sheet = HudBuilder.Stretch("Sheet", details);
            var stats = new Text[6];
            Stat(sheet, MenuText.StatHealth, 52f, 146f, true, art, out stats[0], out _);
            Stat(sheet, MenuText.StatFocus, 240f, 146f, true, art, out stats[1], out Text focusLabel);
            Stat(sheet, MenuText.StatAttack, 428f, 146f, true, art, out stats[2], out _);
            Stat(sheet, MenuText.StatDefense, 52f, 196f, false, art, out stats[3], out _);
            Stat(sheet, MenuText.StatMagic, 240f, 196f, false, art, out stats[4], out _);
            Stat(sheet, MenuText.StatSpeed, 428f, 196f, false, art, out stats[5], out _);
            HudBuilder.Img(Box("Column Rule 1", sheet, 220f, 150f, 1f, 86f).gameObject, null, FaintGold);
            HudBuilder.Img(Box("Column Rule 2", sheet, 408f, 150f, 1f, 86f).gameObject, null, FaintGold);
            HudBuilder.Img(Box("Stats Rule", sheet, 52f, 248f, 524f, 1f).gameObject, null, DimGold);

            var honorLine = Box("Honor", sheet, 52f, 258f, 524f, 44f);
            var honorRow = honorLine.gameObject.AddComponent<HorizontalLayoutGroup>();
            honorRow.childAlignment = TextAnchor.MiddleLeft;
            honorRow.childControlWidth = true;
            honorRow.childControlHeight = true;
            honorRow.childForceExpandWidth = false;
            honorRow.childForceExpandHeight = false;
            honorRow.spacing = 12f;
            Write(HudBuilder.Stretch("Label", honorLine), MenuText.StartHonor, 26, Cream, TextAnchor.MiddleLeft, art.Regular);
            var honor = Write(HudBuilder.Stretch("Value", honorLine), string.Format(MenuText.HonorFormat, 0, MenuText.TierNeutral), 26, Gold, TextAnchor.MiddleLeft, art.Regular);
            HudBuilder.Img(Box("Honor Rule", sheet, 52f, 312f, 524f, 1f).gameObject, null, DimGold);

            Gem("Passive Mark", sheet, 68f, 352f, 17f, Violet);
            var passiveName = Write(Box("Passive Name", sheet, 94f, 326f, 484f, 50f), Catalog.Cards[0].PassiveName, 32, BrightGold, TextAnchor.MiddleLeft, art.Italic);
            var passive = Write(Box("Passive Text", sheet, 66f, 376f, 514f, 108f), Catalog.Cards[0].PassiveText, 25, Cream, TextAnchor.UpperLeft, art.Italic);
            Picture("Bottom Divider", sheet, 42f, 486f, 534f, 20f, art.Divider, Color.white);

            var story = Box("Story", details, 52f, 134f, 526f, 368f);
            var storyText = Write(story, "", 24, Cream, TextAnchor.UpperLeft, art.Regular);
            storyText.resizeTextForBestFit = true;
            storyText.resizeTextMinSize = 16;
            storyText.resizeTextMaxSize = 24;
            storyText.verticalOverflow = VerticalWrapMode.Truncate;
            story.gameObject.SetActive(false);

            var back = GoldButton("Back", panel, 30f, 518f, 254f, 74f, MenuText.Back, 32, art, new UnityAction(carousel.Back), out _);
            var choose = GoldButton("Choose", panel, 308f, 518f, 290f, 74f, MenuText.Choose, 32, art, new UnityAction(carousel.Confirm), out Text chooseLabel);
            var chooseFrame = (Image)choose.targetGraphic;
            chooseFrame.sprite = art.Glow;

            // Left and right belong to the row (ClassCarouselKeys), so the two buttons only answer up and down.
            choose.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = back, selectOnDown = back };
            back.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = choose, selectOnDown = choose };
            SceneWiring.Set(choose.gameObject.AddComponent<ClassCarouselKeys>(), "carousel", carousel);
            SceneWiring.Set(back.gameObject.AddComponent<ClassCarouselKeys>(), "carousel", carousel);

            SceneWiring.Set(carousel, "group", group);
            SceneWiring.Set(carousel, "menu", menu);
            SceneWiring.Set(carousel, "canvas", menuCanvas);
            SceneWiring.Set(carousel, "cards", cards);
            SceneWiring.Set(carousel, "cardArt", cardArt);
            SceneWiring.Set(carousel, "unlockedArt", art.Cards);
            SceneWiring.Set(carousel, "lockedArt", art.LockedCard);
            SceneWiring.Set(carousel, "cardGlow", cardGlow);
            SceneWiring.Set(carousel, "dots", dots);
            SceneWiring.Set(carousel, "previousButton", previous);
            SceneWiring.Set(carousel, "nextButton", next);
            SceneWiring.Set(carousel, "pager", pager.gameObject);
            SceneWiring.Set(carousel, "prologue", prologue.gameObject);
            SceneWiring.Set(carousel, "details", detailsGroup);
            SceneWiring.Set(carousel, "sheetPage", sheet.gameObject);
            SceneWiring.Set(carousel, "storyPage", story.gameObject);
            SceneWiring.Set(carousel, "nameLabel", name);
            SceneWiring.Set(carousel, "roleLabel", role);
            SceneWiring.Set(carousel, "focusLabel", focusLabel);
            SceneWiring.Set(carousel, "statValues", stats);
            SceneWiring.Set(carousel, "honorValue", honor);
            SceneWiring.Set(carousel, "passiveName", passiveName);
            SceneWiring.Set(carousel, "passiveText", passive);
            SceneWiring.Set(carousel, "storyText", storyText);
            SceneWiring.Set(carousel, "chooseButton", choose);
            SceneWiring.Set(carousel, "backButton", back);
            SceneWiring.Set(carousel, "chooseFrame", chooseFrame);
            SceneWiring.Set(carousel, "chooseLabel", chooseLabel);
            SceneWiring.Set(carousel, "plainSprite", art.Button);
            SceneWiring.Set(carousel, "litSprite", art.Glow);

            SceneWiring.Set(menu, "carousel", carousel);
        }

        /// <summary>A small solid diamond, placed by its centre.</summary>
        static Image Gem(string name, RectTransform parent, float centreX, float centreY, float size, Color color)
        {
            var gem = HudBuilder.Anchored(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(centreX, -centreY), new Vector2(size, size));
            gem.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return HudBuilder.Img(gem.gameObject, null, color);
        }

        static void Stat(RectTransform parent, string caption, float x, float y, bool underlined, MenuArt art, out Text value, out Text label)
        {
            const float width = 148f;
            label = Write(Box(caption + " Label", parent, x, y, 90f, 44f), caption, 26, Cream, TextAnchor.MiddleLeft, art.Regular);
            value = Write(Box(caption + " Value", parent, x + width - 90f, y, 90f, 44f), "0", 26, Cream, TextAnchor.MiddleRight, art.Regular);
            if (underlined) HudBuilder.Img(Box(caption + " Rule", parent, x, y + 44f, width, 1f).gameObject, null, FaintGold);
        }

        static void BuildCodex(Transform canvas, MenuArt art, CodexPanel codex, SoulKeeper keeper)
        {
            var root = HudBuilder.Stretch("Codex", canvas);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            HudBuilder.Img(root.gameObject, null, new Color(0f, 0f, 0f, 0.45f), true);
            var panel = Panel("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1330f, 760f), art);

            var title = Write(Box("Title", panel, 0f, 18f, 1330f, 72f), string.Format(MenuText.CodexTitleFormat, 0, CodexBook.Entries.Count),
                50, BrightGold, TextAnchor.MiddleCenter, art.SemiBold);
            Picture("Divider", panel, 393f, 90f, 544f, 24f, art.Divider, Color.white);

            // The list.
            var view = Box("List", panel, 44f, 126f, 570f, 520f);
            HudBuilder.Img(view.gameObject, null, Clear, true);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            var viewport = HudBuilder.Rect("Viewport", view, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-18f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = HudBuilder.Rect("Content", viewport, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            var stack = content.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;

            var bar = HudBuilder.Rect("Scrollbar", view, new Vector2(1f, 0f), Vector2.one, new Vector2(-8f, 0f), Vector2.zero);
            HudBuilder.Img(bar.gameObject, null, new Color(DimGold.r, DimGold.g, DimGold.b, 0.3f), true);
            var track = HudBuilder.Stretch("Track", bar);
            var handle = HudBuilder.Stretch("Handle", track);
            var handleImage = HudBuilder.Img(handle.gameObject, null, Gold, true);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            var section = HudBuilder.Stretch("Section Template", content);
            var sectionText = Write(section, MenuText.CodexFoes, 28, Gold, TextAnchor.LowerLeft, art.Italic);
            section.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
            section.gameObject.SetActive(false);

            var rowRect = HudBuilder.Stretch("Row Template", content);
            var rowImage = HudBuilder.Img(rowRect.gameObject, null, Color.white, true);
            var rowButton = rowRect.gameObject.AddComponent<Button>();
            rowButton.targetGraphic = rowImage;
            var colors = rowButton.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(0.9f, 0.68f, 0.36f, 0.1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.9f, 0.68f, 0.36f, 0.2f);
            rowButton.colors = colors;
            rowRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
            var rowLabel = HudBuilder.Rect("Label", rowRect, Vector2.zero, Vector2.one, new Vector2(12f, 0f), Vector2.zero);
            Write(rowLabel, MenuText.Undiscovered, 25, Cream, TextAnchor.MiddleLeft, art.Regular);
            rowRect.gameObject.SetActive(false);

            // The page.
            var rule = Box("Rule", panel, 638f, 130f, 2f, 512f);
            HudBuilder.Img(rule.gameObject, null, DimGold);
            var name = Write(Box("Name", panel, 668f, 126f, 618f, 64f), "", 36, BrightGold, TextAnchor.UpperLeft, art.SemiBold);
            var body = Write(Box("Body", panel, 668f, 198f, 618f, 440f), MenuText.CodexPick, 25, Cream, TextAnchor.UpperLeft, art.Regular);

            var close = GoldButton("Back", panel, 490f, 662f, 350f, 72f, MenuText.Back, 32, art, new UnityAction(codex.Close), out _);

            SceneWiring.Set(codex, "group", group);
            SceneWiring.Set(codex, "keeper", keeper);
            SceneWiring.Set(codex, "titleLabel", title);
            SceneWiring.Set(codex, "scroll", scroll);
            SceneWiring.Set(codex, "content", content);
            SceneWiring.Set(codex, "rowTemplate", rowButton);
            SceneWiring.Set(codex, "sectionTemplate", sectionText);
            SceneWiring.Set(codex, "detailName", name);
            SceneWiring.Set(codex, "detailBody", body);
            SceneWiring.Set(codex, "closeButton", close);
        }

        static void BuildSettings(Transform canvas, MenuArt art, SettingsPanel settings, PlayerInputHub input, SoulKeeper keeper, ConfirmPanel confirm,
            TouchLayout layout, MainMenu main)
        {
            var root = HudBuilder.Stretch("Settings", canvas);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            HudBuilder.Img(root.gameObject, null, new Color(0f, 0f, 0f, 0.35f), true);
            var panel = Panel("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1330f, 716f), art);

            Write(Box("Title", panel, 0f, 12f, 1330f, 84f), MenuText.SettingsTitle, 58, BrightGold, TextAnchor.MiddleCenter, art.SemiBold);
            Picture("Divider", panel, 393f, 94f, 544f, 24f, art.Divider, Color.white);
            var separator = Box("Separator", panel, 664f, 136f, 2f, 396f);
            HudBuilder.Img(separator.gameObject, null, DimGold);

            const float left = 44f, right = 696f, column = 590f, half = 284f;

            // Left: language, look speed, saving.
            Section(panel, left, 124f, column, MenuText.Language, art);
            var language = Choice("Language", panel, left, 172f, half, 60f, MenuText.Hungarian, art, null);

            Section(panel, left, 248f, column, MenuText.Sensitivity, art);
            var look = GoldSlider("Sensitivity", panel, left, 298f, column - 100f, 44f, 0f, 1f, art, new UnityAction<float>(settings.OnSensitivityChanged));
            var lookValue = Write(Box("Sensitivity Value", panel, left + column - 90f, 298f, 90f, 44f), GameText.Decimal(1f), 25, Cream, TextAnchor.MiddleRight, art.Regular);

            Section(panel, left, 364f, column, MenuText.SaveSection, art);
            var save = GoldButton("Save", panel, left, 414f, half, 62f, MenuText.Save, 28, art, new UnityAction(settings.SaveSoul), out _);
            GoldButton("Load", panel, left + column - half, 414f, half, 62f, MenuText.Load, 28, art, new UnityAction(settings.LoadSoul), out _);
            var status = Write(Box("Save Status", panel, left, 486f, column, 36f), "", 24, Gold, TextAnchor.MiddleLeft, art.Italic);

            // Right: on-screen buttons, then the HUD.
            Section(panel, right, 124f, column, MenuText.Controls, art);
            var buttonsShown = Choice("Buttons Shown", panel, right, 172f, half, 60f, MenuText.ButtonsShown, art, new UnityAction(settings.ShowButtons));
            var buttonsHidden = Choice("Buttons Hidden", panel, right + column - half, 172f, half, 60f, MenuText.ButtonsHidden, art, new UnityAction(settings.HideButtons));
            Write(Box("Button Opacity Label", panel, right, 244f, 270f, 44f), MenuText.ButtonOpacity, 24, Cream, TextAnchor.MiddleLeft, art.Regular);
            var buttons = GoldSlider("Button Opacity", panel, right + 276f, 244f, 234f, 44f, GameSettings.MinOpacity, 1f, art, new UnityAction<float>(settings.OnButtonOpacityChanged));
            var buttonsValue = Write(Box("Button Opacity Value", panel, right + 510f, 244f, 80f, 44f), string.Format(MenuText.PercentFormat, 100), 24, Cream, TextAnchor.MiddleRight, art.Regular);
            GoldButton("Move Buttons", panel, right, 296f, column, 60f, MenuText.MoveButtons, 27, art, new UnityAction(settings.OpenLayoutEditor), out _);

            Section(panel, right, 364f, column, MenuText.HudSection, art);
            var hudShown = Choice("HUD Shown", panel, right, 414f, half, 60f, MenuText.HudShown, art, new UnityAction(settings.ShowHud));
            var hudHidden = Choice("HUD Hidden", panel, right + column - half, 414f, half, 60f, MenuText.HudHidden, art, new UnityAction(settings.HideHud));
            Write(Box("HUD Opacity Label", panel, right, 486f, 270f, 44f), MenuText.HudOpacity, 24, Cream, TextAnchor.MiddleLeft, art.Regular);
            var hud = GoldSlider("HUD Opacity", panel, right + 276f, 486f, 234f, 44f, GameSettings.MinOpacity, 1f, art, new UnityAction<float>(settings.OnHudOpacityChanged));
            var hudValue = Write(Box("HUD Opacity Value", panel, right + 510f, 486f, 80f, 44f), string.Format(MenuText.PercentFormat, 100), 24, Cream, TextAnchor.MiddleRight, art.Regular);

            Picture("Bottom Divider", panel, 393f, 544f, 544f, 24f, art.Divider, Color.white);
            GoldButton("Defaults", panel, 84f, 592f, 560f, 76f, MenuText.Defaults, 32, art, new UnityAction(settings.RestoreDefaults), out _);
            GoldButton("Back", panel, 686f, 592f, 560f, 76f, MenuText.Back, 32, art, new UnityAction(settings.Close), out _);

            // The language has one answer so far; it shows chosen and does nothing.
            language.button.interactable = false;
            language.component.GetComponentInChildren<Image>().sprite = art.Glow;

            SceneWiring.Set(settings, "input", input);
            SceneWiring.Set(settings, "group", group);
            SceneWiring.Set(settings, "keeper", keeper);
            SceneWiring.Set(settings, "confirm", confirm);
            SceneWiring.Set(settings, "layout", layout);
            SceneWiring.Set(settings, "mainMenu", main);
            SceneWiring.Set(settings, "firstControl", look);
            SceneWiring.Set(settings, "sensitivity", look);
            SceneWiring.Set(settings, "sensitivityValue", lookValue);
            SceneWiring.Set(settings, "buttonOpacity", buttons);
            SceneWiring.Set(settings, "buttonOpacityValue", buttonsValue);
            SceneWiring.Set(settings, "hudOpacity", hud);
            SceneWiring.Set(settings, "hudOpacityValue", hudValue);
            SceneWiring.Set(settings, "buttonsShown", buttonsShown.component);
            SceneWiring.Set(settings, "buttonsHidden", buttonsHidden.component);
            SceneWiring.Set(settings, "hudShown", hudShown.component);
            SceneWiring.Set(settings, "hudHidden", hudHidden.component);
            SceneWiring.Set(settings, "saveStatus", status);
        }

        static void BuildConfirm(Transform canvas, MenuArt art, ConfirmPanel confirm)
        {
            var root = HudBuilder.Stretch("Confirm", canvas);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            HudBuilder.Img(root.gameObject, null, new Color(0f, 0f, 0f, 0.55f), true);
            var panel = Panel("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 320f), art);

            var question = Write(Box("Question", panel, 54f, 36f, 752f, 150f), MenuText.YourSouls, 28, Cream, TextAnchor.MiddleCenter, art.Regular);
            GoldButton("Yes", panel, 450f, 204f, 340f, 76f, MenuText.Yes, 30, art, new UnityAction(confirm.Yes), out _);
            // Mégse sits where the Elengedés button was pressed, so a double tap can never confirm a deletion.
            var cancel = GoldButton("Cancel", panel, 70f, 204f, 340f, 76f, MenuText.Cancel, 30, art, new UnityAction(confirm.Cancel), out _);

            SceneWiring.Set(confirm, "group", group);
            SceneWiring.Set(confirm, "questionText", question);
            SceneWiring.Set(confirm, "cancelButton", cancel);
        }

        /// <summary>A bar, not a screen: everything around it must stay reachable for dragging.</summary>
        static void BuildLayoutBar(Transform canvas, MenuArt art, TouchLayout layout)
        {
            var root = HudBuilder.Anchored("Layout Bar", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(1040f, 112f));
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var fill = HudBuilder.Stretch("Fill", root);
            HudBuilder.Img(fill.gameObject, null, Ink, true);

            Write(Box("Hint", root, 26f, 0f, 520f, 112f), MenuText.LayoutHint, 26, Cream, TextAnchor.MiddleLeft, art.Italic);
            GoldButton("Reset", root, 556f, 22f, 230f, 68f, MenuText.Defaults, 26, art, new UnityAction(layout.ResetLayout), out _);
            GoldButton("Done", root, 800f, 22f, 214f, 68f, MenuText.LayoutDone, 28, art, new UnityAction(layout.Finish), out _);

            SceneWiring.Set(layout, "editorBar", group);
        }

        // ---- Widgets (also used by the pause menu) -----------------------------------------------------------

        public static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, MenuArt art)
        {
            var panel = HudBuilder.Anchored(name, parent, anchor, anchor, position, size);
            var fill = HudBuilder.Rect("Fill", panel, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            HudBuilder.Img(fill.gameObject, art.PanelFill, Ink, true).type = Image.Type.Sliced;
            var frame = HudBuilder.Stretch("Frame", panel);
            HudBuilder.Img(frame.gameObject, art.Frame, Color.white).type = Image.Type.Sliced;
            return panel;
        }

        /// <summary>A rectangle placed as in the sketches: x and y from the parent's top-left corner, y growing downwards.</summary>
        public static RectTransform Box(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = HudBuilder.Anchored(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, height));
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            return rect;
        }

        public static Text Write(RectTransform rect, string text, int size, Color color, TextAnchor alignment, Font font)
        {
            var t = rect.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        static Image Picture(string name, Transform parent, float x, float y, float width, float height, Sprite sprite, Color color)
        {
            var image = HudBuilder.Img(Box(name, parent, x, y, width, height).gameObject, sprite, color);
            image.preserveAspect = false;
            return image;
        }

        public static Button GoldButton(string name, Transform parent, float x, float y, float width, float height, string caption, int fontSize,
            MenuArt art, UnityAction action, out Text label)
        {
            var rect = Box(name, parent, x, y, width, height);
            var frame = HudBuilder.Rect("Frame", rect, Vector2.zero, Vector2.one, new Vector2(-ButtonRim, -ButtonRim), new Vector2(ButtonRim, ButtonRim));
            var image = HudBuilder.Img(frame.gameObject, art.Button, Color.white, true);
            image.type = Image.Type.Sliced;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = art.Glow, pressedSprite = art.Glow, selectedSprite = art.Glow };

            label = null;
            if (caption != null) label = Write(HudBuilder.Stretch("Label", rect), caption, fontSize, Cream, TextAnchor.MiddleCenter, art.Regular);
            if (action != null) UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        static Button TextButton(string name, Transform parent, float x, float y, float width, float height, string caption, int fontSize, Color color,
            Font font, UnityAction action)
        {
            var rect = Box(name, parent, x, y, width, height);
            HudBuilder.Img(rect.gameObject, null, Clear, true); // a transparent image gives the small text a finger-sized target
            var label = Write(HudBuilder.Stretch("Label", rect), caption, fontSize, color, TextAnchor.MiddleCenter, font);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = label;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0.8f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            button.colors = colors;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        sealed class ChoiceRefs
        {
            public ChoiceButton component;
            public Button button;
        }

        static ChoiceRefs Choice(string name, Transform parent, float x, float y, float width, float height, string caption, MenuArt art, UnityAction action)
        {
            var button = GoldButton(name, parent, x, y, width, height, caption, 25, art, action, out Text label);
            label.rectTransform.offsetMin = new Vector2(ChoiceMarkerRoom, 0f);
            var mark = Box("Marker", button.transform, 16f, (height - 22f) * 0.5f, 22f, 22f);
            var markImage = HudBuilder.Img(mark.gameObject, art.Diamond, Gold);

            var component = button.gameObject.AddComponent<ChoiceButton>();
            SceneWiring.Set(component, "frame", button.targetGraphic);
            SceneWiring.Set(component, "marker", markImage);
            SceneWiring.Set(component, "normalSprite", art.Button);
            SceneWiring.Set(component, "chosenSprite", art.Glow);
            return new ChoiceRefs { component = component, button = button };
        }

        static void Section(RectTransform panel, float x, float y, float width, string caption, MenuArt art)
        {
            var heading = Box(caption + " Heading", panel, x, y, width, 46f);
            var text = Write(heading, caption, 30, Gold, TextAnchor.MiddleLeft, art.Italic);
            float used = text.preferredWidth + 16f;
            var rule = Box("Rule", heading, used, 28f, Mathf.Max(0f, width - used), 2f);
            HudBuilder.Img(rule.gameObject, null, DimGold);
        }

        static Slider GoldSlider(string name, Transform parent, float x, float y, float width, float height, float min, float max, MenuArt art, UnityAction<float> action)
        {
            var rect = Box(name, parent, x, y, width, height);
            HudBuilder.Img(rect.gameObject, null, Clear, true); // the whole height takes a tap, not only the thin line

            var line = HudBuilder.Rect("Line", rect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -2f), new Vector2(0f, 2f));
            HudBuilder.Img(line.gameObject, null, DimGold);
            var fillArea = HudBuilder.Rect("Fill Area", rect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -2f), new Vector2(0f, 2f));
            var fill = HudBuilder.Rect("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            HudBuilder.Img(fill.gameObject, null, BrightGold);

            var slideArea = HudBuilder.Rect("Handle Area", rect, Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, 0f));
            // The slider stretches its handle over the full height, so the height here is relative to that.
            var handle = HudBuilder.Anchored("Handle", slideArea, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(24f, 24f - height));
            handle.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var image = HudBuilder.Img(handle.gameObject, null, BrightGold, true);

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = image;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = max;
            UnityEventTools.AddPersistentListener(slider.onValueChanged, action);
            return slider;
        }

        static void SetLayer(Transform t)
        {
            t.gameObject.layer = UILayer;
            foreach (Transform child in t) SetLayer(child);
        }
    }
}
