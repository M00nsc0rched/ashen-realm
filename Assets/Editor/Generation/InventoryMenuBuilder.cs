using AshenRealm.Core;
using AshenRealm.Story;
using AshenRealm.UI;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AshenRealm.EditorTools
{
    /// <summary>The violet and gold inventory sheets are real controls, so every class uses its own numbers and art.</summary>
    internal static class InventoryMenuBuilder
    {
        static readonly Color Cream = new Color(0.93f, 0.88f, 0.77f);
        static readonly Color Gold = new Color(0.97f, 0.8f, 0.52f);
        static readonly Color Violet = new Color(0.65f, 0.43f, 0.7f);
        static readonly Color Ink = new Color(0.032f, 0.022f, 0.045f, 0.98f);

        public static void Build(Transform canvas, MenuArt art, SoulKeeper keeper, PauseMenu pause)
        {
            InventoryPanel inventory = BuildInventory(canvas, art, keeper);
            SecretStatsPanel secret = BuildSecret(canvas, art, keeper);
            SceneWiring.Set(pause, "inventory", inventory);
            SceneWiring.Set(pause, "secret", secret);
        }

        static RectTransform Overlay(string name, Transform canvas, MenuArt art, out CanvasGroup group)
        {
            RectTransform root = HudBuilder.Stretch(name, canvas);
            HudBuilder.Img(root.gameObject, null, new Color(0.015f, 0.01f, 0.025f, 0.96f), true);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
            return MenuBuilder.Panel("Panel", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440f, 810f), art);
        }

        static RectTransform Frame(string name, Transform parent, float x, float y, float width, float height, MenuArt art)
        {
            RectTransform rect = MenuBuilder.Box(name, parent, x, y, width, height);
            HudBuilder.Img(rect.gameObject, art.PanelFill, Ink).type = Image.Type.Sliced;
            Image rim = HudBuilder.Img(HudBuilder.Stretch("Rim", rect).gameObject, art.Frame, Violet);
            rim.type = Image.Type.Sliced;
            return rect;
        }

        static Text Text(string name, Transform parent, float x, float y, float width, float height, string value, int size, MenuArt art,
            TextAnchor alignment = TextAnchor.MiddleLeft, bool heading = false) =>
            MenuBuilder.Write(MenuBuilder.Box(name, parent, x, y, width, height), value, size, heading ? Gold : Cream, alignment,
                heading ? art.SemiBold : art.Regular);

        static InventoryPanel BuildInventory(Transform canvas, MenuArt art, SoulKeeper keeper)
        {
            RectTransform panel = Overlay("Inventory", canvas, art, out CanvasGroup group);
            var inventory = group.gameObject.AddComponent<InventoryPanel>();
            Text title = Text("Class", panel, 42f, 24f, 700f, 64f, "", 48, art, heading: true);
            Text level = Text("Level", panel, 44f, 85f, 580f, 38f, "", 26, art);
            Button inventoryTab = MenuBuilder.GoldButton("Inventory Tab", panel, 800f, 55f, 270f, 66f, InventoryText.Inventory, 30, art,
                new UnityAction(inventory.ShowInventory), out _);
            Button abilitiesTab = MenuBuilder.GoldButton("Abilities Tab", panel, 1090f, 55f, 300f, 66f, InventoryText.Abilities, 30, art,
                new UnityAction(inventory.ShowAbilities), out _);

            RectTransform gearPage = MenuBuilder.Box("Inventory Page", panel, 26f, 152f, 1388f, 535f);
            RectTransform portraitBox = Frame("Portrait", gearPage, 0f, 0f, 330f, 535f, art);
            Image portrait = HudBuilder.Img(MenuBuilder.Box("Class Art", portraitBox, 17f, 13f, 296f, 455f).gameObject, art.Cards[0], Color.white);
            portrait.preserveAspect = true;
            Text weapon = Text("Weapon", portraitBox, 23f, 461f, 286f, 50f, "", 21, art);
            RectTransform stats = Frame("Statistics", gearPage, 344f, 0f, 376f, 535f, art);
            var statistics = new Text[10];
            for (int i = 0; i < statistics.Length; i++)
            {
                RectTransform row = MenuBuilder.Box("Row " + i, stats, 20f, 15f + i * 50f, 336f, 46f);
                HudBuilder.Img(row.gameObject, art.Button, new Color(0.58f, 0.4f, 0.63f, 0.55f)).type = Image.Type.Sliced;
                statistics[i] = Text("Value", row, 16f, 0f, 304f, 46f, "", 27, art);
            }
            RectTransform pack = Frame("Pack", gearPage, 734f, 0f, 654f, 535f, art);
            Text("Pack Heading", pack, 25f, 14f, 604f, 54f, InventoryText.Pack, 36, art, TextAnchor.MiddleCenter, true);
            var slots = new RectTransform[9];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Frame("Slot " + i, pack, 25f + (i % 3) * 202f, 82f + (i / 3) * 100f, 194f, 94f, art);
            Button torch = MenuBuilder.GoldButton("Torch", slots[0], 10f, 10f, 174f, 74f, InventoryText.TorchTake, 24, art,
                new UnityAction(inventory.ToggleTorch), out Text torchLabel);
            Button potion = MenuBuilder.GoldButton("Potion", slots[1], 10f, 10f, 174f, 74f, "", 24, art,
                new UnityAction(inventory.DrinkPotion), out Text potionLabel);
            Text relic = Text("Relic", slots[2], 12f, 7f, 170f, 80f, "", 23, art, TextAnchor.MiddleCenter);
            Text("Torch Hint", pack, 33f, 405f, 588f, 100f, InventoryText.TorchDescription, 25, art, TextAnchor.UpperLeft);

            RectTransform abilityPage = MenuBuilder.Box("Abilities Page", panel, 26f, 152f, 1388f, 535f);
            RectTransform skills = Frame("Class Abilities", abilityPage, 0f, 0f, 720f, 535f, art);
            Text("Skills Heading", skills, 25f, 12f, 670f, 54f, InventoryText.Abilities, 36, art, TextAnchor.MiddleCenter, true);
            var abilityLabels = new Text[2];
            var abilityDescriptions = new Text[2];
            var upgradeLabels = new Text[2];
            var upgradeButtons = new Button[2];
            for (int i = 0; i < 2; i++)
            {
                RectTransform row = Frame("Ability " + i, skills, 22f, 83f + i * 171f, 676f, 153f, art);
                abilityLabels[i] = Text("Name", row, 20f, 11f, 635f, 36f, "", 28, art, heading: true);
                abilityDescriptions[i] = Text("Description", row, 20f, 52f, 635f, 45f, "", 21, art, TextAnchor.UpperLeft);
                upgradeButtons[i] = MenuBuilder.GoldButton("Upgrade", row, 306f, 100f, 345f, 43f, "", 22, art, null, out upgradeLabels[i]);
                UnityEventTools.AddIntPersistentListener(upgradeButtons[i].onClick, inventory.UpgradeAbility, i);
            }
            Text("Free Actions", skills, 34f, 435f, 650f, 72f, InventoryText.BasicDescription, 24, art, TextAnchor.UpperLeft);
            RectTransform properties = Frame("Properties", abilityPage, 734f, 0f, 654f, 535f, art);
            Text("Properties Heading", properties, 20f, 15f, 614f, 56f, InventoryText.Properties, 36, art, TextAnchor.MiddleCenter, true);
            string[] statNames = { InventoryText.RaiseHealth, InventoryText.RaiseFocus, InventoryText.RaiseAttack,
                InventoryText.RaiseDefense, InventoryText.RaiseMagic, InventoryText.RaiseSpeed };
            var statButtons = new Button[statNames.Length];
            for (int i = 0; i < statNames.Length; i++)
            {
                statButtons[i] = MenuBuilder.GoldButton("Raise Stat " + i, properties, 30f + i % 2 * 302f, 90f + i / 2 * 91f,
                    290f, 73f, statNames[i], 29, art, null, out _);
                UnityEventTools.AddIntPersistentListener(statButtons[i].onClick, inventory.RaiseStat, i);
            }
            Text levelCost = Text("Level Cost", properties, 32f, 371f, 590f, 48f, "", 26, art, heading: true);
            Text("Guide", properties, 32f, 429f, 590f, 86f, InventoryText.AbilityGuide, 20, art, TextAnchor.UpperLeft);
            Text message = Text("Message", panel, 44f, 690f, 1352f, 37f, "", 23, art, TextAnchor.MiddleCenter);
            Button close = MenuBuilder.GoldButton("Close Inventory", panel, 45f, 734f, 1350f, 54f, InventoryText.Close, 33, art,
                new UnityAction(inventory.Close), out _);

            SceneWiring.Set(inventory, "group", group);
            SceneWiring.Set(inventory, "inventoryPage", gearPage.gameObject);
            SceneWiring.Set(inventory, "abilitiesPage", abilityPage.gameObject);
            SceneWiring.Set(inventory, "keeper", keeper);
            SceneWiring.Set(inventory, "title", title);
            SceneWiring.Set(inventory, "level", level);
            SceneWiring.Set(inventory, "portrait", portrait);
            SceneWiring.Set(inventory, "portraits", art.Cards);
            SceneWiring.Set(inventory, "statistics", statistics);
            SceneWiring.Set(inventory, "weapon", weapon);
            SceneWiring.Set(inventory, "relic", relic);
            SceneWiring.Set(inventory, "torchLabel", torchLabel);
            SceneWiring.Set(inventory, "potionLabel", potionLabel);
            SceneWiring.Set(inventory, "potionButton", potion);
            SceneWiring.Set(inventory, "abilityLabels", abilityLabels);
            SceneWiring.Set(inventory, "abilityDescriptions", abilityDescriptions);
            SceneWiring.Set(inventory, "upgradeLabels", upgradeLabels);
            SceneWiring.Set(inventory, "upgradeButtons", upgradeButtons);
            SceneWiring.Set(inventory, "statButtons", statButtons);
            SceneWiring.Set(inventory, "levelCost", levelCost);
            SceneWiring.Set(inventory, "message", message);
            SceneWiring.Set(inventory, "firstButton", close);
            SceneWiring.Set(inventory, "inventoryTab", inventoryTab);
            SceneWiring.Set(inventory, "abilitiesTab", abilitiesTab);
            return inventory;
        }

        static SecretStatsPanel BuildSecret(Transform canvas, MenuArt art, SoulKeeper keeper)
        {
            RectTransform panel = Overlay("Secret Stats", canvas, art, out CanvasGroup group);
            var secret = group.gameObject.AddComponent<SecretStatsPanel>();
            Text("Title", panel, 42f, 20f, 1340f, 65f, InventoryText.Secret, 48, art, heading: true);
            var fields = new InputField[InventoryText.DebugLabels.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                float x = 42f + i / 7 * 695f, y = 113f + i % 7 * 62f;
                Text("Stat Name " + i, panel, x, y, 340f, 48f, InventoryText.DebugLabels[i], 27, art);
                RectTransform field = MenuBuilder.Box("Stat Input " + i, panel, x + 344f, y, 280f, 48f);
                Image image = HudBuilder.Img(field.gameObject, art.Button, new Color(0.4f, 0.3f, 0.47f), true);
                image.type = Image.Type.Sliced;
                Text number = Text("Number", field, 20f, 1f, 240f, 46f, "0", 27, art);
                var input = field.gameObject.AddComponent<InputField>();
                input.targetGraphic = image;
                input.textComponent = number;
                input.contentType = InputField.ContentType.IntegerNumber;
                input.characterLimit = 7;
                fields[i] = input;
            }
            Text("Unlock Heading", panel, 42f, 562f, 1350f, 44f, InventoryText.SecretClasses, 27, art, heading: true);
            var unlocks = new Button[2];
            var unlockLabels = new Text[2];
            for (int i = 0; i < 2; i++)
            {
                float x = 42f + i * 695f;
                Text("Class " + i, panel, x, 614f, 330f, 48f, Catalog.Cards[i + 4].Name, 28, art);
                unlocks[i] = MenuBuilder.GoldButton("Unlock " + i, panel, x + 344f, 614f, 280f, 48f, InventoryText.Unlock, 26, art,
                    null, out unlockLabels[i]);
                UnityEventTools.AddIntPersistentListener(unlocks[i].onClick, secret.Unlock, i);
            }
            Text message = Text("Message", panel, 42f, 678f, 1350f, 40f, "", 24, art, TextAnchor.MiddleCenter);
            MenuBuilder.GoldButton("Apply Stats", panel, 330f, 738f, 360f, 52f, InventoryText.Apply, 30, art,
                new UnityAction(secret.Apply), out _);
            Button close = MenuBuilder.GoldButton("Close Secret", panel, 750f, 738f, 360f, 52f, InventoryText.Close, 30, art,
                new UnityAction(secret.Close), out _);
            SceneWiring.Set(secret, "group", group);
            SceneWiring.Set(secret, "keeper", keeper);
            SceneWiring.Set(secret, "fields", fields);
            SceneWiring.Set(secret, "unlockButtons", unlocks);
            SceneWiring.Set(secret, "unlockLabels", unlockLabels);
            SceneWiring.Set(secret, "message", message);
            SceneWiring.Set(secret, "firstButton", close);
            return secret;
        }
    }
}
