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
            Image weaponImage = HudBuilder.Img(MenuBuilder.Box("Weapon Icon", portraitBox, 20f, 465f, 48f, 48f).gameObject,
                EquipmentIconBuilder.ClassSprites()[0], Color.white);
            weaponImage.preserveAspect = true;
            Text weapon = Text("Weapon", portraitBox, 78f, 460f, 235f, 60f, "", 18, art);
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
            Button nativeFilter = MenuBuilder.GoldButton("Native Skills", skills, 22f, 12f, 204f, 38f, SkillText.NativeSkills, 20, art,
                new UnityAction(inventory.ShowNativeSkills), out _);
            Button foreignFilter = MenuBuilder.GoldButton("Foreign Skills", skills, 238f, 12f, 204f, 38f, SkillText.ForeignSkills, 20, art,
                new UnityAction(inventory.ShowForeignSkills), out _);
            Button previous = MenuBuilder.GoldButton("Previous Page", skills, 454f, 12f, 44f, 38f, SkillText.PreviousPage, 23, art,
                new UnityAction(inventory.PreviousPage), out _);
            Text pageLabel = Text("Page", skills, 503f, 12f, 122f, 38f, "", 21, art, TextAnchor.MiddleCenter);
            Button next = MenuBuilder.GoldButton("Next Page", skills, 638f, 12f, 44f, 38f, SkillText.NextPage, 23, art,
                new UnityAction(inventory.NextPage), out _);
            const int rows = 4;
            var abilityLabels = new Text[rows];
            var abilityDescriptions = new Text[rows];
            var upgradeLabels = new Text[rows];
            var upgradeButtons = new Button[rows];
            var abilityIcons = new Image[rows];
            var equipQ = new Button[rows];
            var equipR = new Button[rows];
            var equipQLabels = new Text[rows];
            var equipRLabels = new Text[rows];
            Sprite[] skillIcons = AbilityIconBuilder.Create();
            AbilityEffect[] effects = AbilityIconBuilder.Effects();
            for (int i = 0; i < rows; i++)
            {
                RectTransform row = Frame("Ability " + i, skills, 22f, 54f + i * 119f, 676f, 116f, art);
                AbilityDefinition sample = AbilityBook.At(Catalog.Classes[0], i);
                int iconIndex = sample != null ? System.Array.IndexOf(effects, sample.Effect) : 0;
                abilityIcons[i] = HudBuilder.Img(MenuBuilder.Box("Icon", row, 10f, 23f, 64f, 64f).gameObject,
                    skillIcons[Mathf.Max(0, iconIndex)], Color.white);
                abilityIcons[i].preserveAspect = true;
                abilityLabels[i] = Text("Name", row, 84f, 2f, 568f, 24f, "", 22, art, heading: true);
                abilityLabels[i].resizeTextForBestFit = true;
                abilityLabels[i].resizeTextMinSize = 17;
                abilityLabels[i].resizeTextMaxSize = 22;
                abilityLabels[i].verticalOverflow = VerticalWrapMode.Truncate;
                abilityDescriptions[i] = Text("Description", row, 84f, 27f, 568f, 42f, "", 16, art, TextAnchor.UpperLeft);
                abilityDescriptions[i].resizeTextForBestFit = true;
                abilityDescriptions[i].resizeTextMinSize = 13;
                abilityDescriptions[i].resizeTextMaxSize = 16;
                abilityDescriptions[i].verticalOverflow = VerticalWrapMode.Truncate;
                equipQ[i] = MenuBuilder.GoldButton("Equip Q", row, 84f, 72f, 106f, 40f, InventoryText.EquipQ, 18, art, null, out equipQLabels[i]);
                equipR[i] = MenuBuilder.GoldButton("Equip R", row, 206f, 72f, 106f, 40f, InventoryText.EquipR, 18, art, null, out equipRLabels[i]);
                upgradeButtons[i] = MenuBuilder.GoldButton("Upgrade or Brew", row, 330f, 72f, 322f, 40f, "", 18, art, null, out upgradeLabels[i]);
                upgradeLabels[i].resizeTextForBestFit = true;
                upgradeLabels[i].resizeTextMinSize = 14;
                upgradeLabels[i].resizeTextMaxSize = 18;
                upgradeLabels[i].verticalOverflow = VerticalWrapMode.Truncate;
                UnityEventTools.AddIntPersistentListener(upgradeButtons[i].onClick, inventory.UpgradeKnownAbility, i);
                UnityEventTools.AddIntPersistentListener(equipQ[i].onClick, inventory.EquipQ, i);
                UnityEventTools.AddIntPersistentListener(equipR[i].onClick, inventory.EquipR, i);
            }
            RectTransform properties = Frame("Properties", abilityPage, 734f, 0f, 654f, 535f, art);
            Text("Properties Heading", properties, 20f, 9f, 614f, 42f, InventoryText.Properties, 32, art, TextAnchor.MiddleCenter, true);
            Text passiveName = Text("Passive Name", properties, 30f, 54f, 594f, 30f, "", 23, art, heading: true);
            Text passiveDescription = Text("Passive Description", properties, 30f, 86f, 594f, 52f, "", 18, art, TextAnchor.UpperLeft);
            passiveDescription.resizeTextForBestFit = true;
            passiveDescription.resizeTextMinSize = 15;
            passiveDescription.resizeTextMaxSize = 18;
            passiveDescription.verticalOverflow = VerticalWrapMode.Truncate;
            string[] statNames = { InventoryText.RaiseHealth, InventoryText.RaiseFocus, InventoryText.RaiseAttack,
                InventoryText.RaiseDefense, InventoryText.RaiseMagic, InventoryText.RaiseSpeed };
            var statButtons = new Button[statNames.Length];
            for (int i = 0; i < statNames.Length; i++)
            {
                statButtons[i] = MenuBuilder.GoldButton("Raise Stat " + i, properties, 30f + i % 2 * 302f, 147f + i / 2 * 61f,
                    290f, 52f, statNames[i], 25, art, null, out _);
                UnityEventTools.AddIntPersistentListener(statButtons[i].onClick, inventory.RaiseStat, i);
            }
            Text levelCost = Text("Level Cost", properties, 30f, 329f, 594f, 32f, "", 22, art, heading: true);
            Text necroPathLabel = Text("Necromancer Path", properties, 30f, 369f, 594f, 30f, "", 19, art);
            Button necroAttack = MenuBuilder.GoldButton("Attack Path", properties, 30f, 405f, 284f, 42f, SkillText.AttackPath, 22, art,
                new UnityAction(inventory.ChooseAttackPath), out _);
            Button necroDefense = MenuBuilder.GoldButton("Defense Path", properties, 326f, 405f, 298f, 42f, SkillText.DefensePath, 22, art,
                new UnityAction(inventory.ChooseDefensePath), out _);
            Text herbStock = Text("Herb Stock", properties, 30f, 367f, 594f, 56f, "", 19, art, TextAnchor.UpperLeft);
            Button buyHerbs = MenuBuilder.GoldButton("Buy Herbs", properties, 30f, 429f, 594f, 42f, SkillText.BuyHerbs, 21, art,
                new UnityAction(inventory.BuyHerbs), out _);
            Text("Guide", properties, 30f, 481f, 594f, 40f, SkillText.AbilityGuide, 16, art, TextAnchor.UpperLeft);
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
            SceneWiring.Set(inventory, "weaponImage", weaponImage);
            SceneWiring.Set(inventory, "classWeaponIcons", EquipmentIconBuilder.ClassSprites());
            SceneWiring.Set(inventory, "goodsWeaponIcons", EquipmentIconBuilder.GoodsSprites());
            SceneWiring.Set(inventory, "relic", relic);
            SceneWiring.Set(inventory, "torchLabel", torchLabel);
            SceneWiring.Set(inventory, "potionLabel", potionLabel);
            SceneWiring.Set(inventory, "potionButton", potion);
            SceneWiring.Set(inventory, "abilityLabels", abilityLabels);
            SceneWiring.Set(inventory, "abilityDescriptions", abilityDescriptions);
            SceneWiring.Set(inventory, "upgradeLabels", upgradeLabels);
            SceneWiring.Set(inventory, "upgradeButtons", upgradeButtons);
            SceneWiring.Set(inventory, "abilityIcons", abilityIcons);
            SceneWiring.Set(inventory, "skillIcons", skillIcons);
            var inventorySettings = new UnityEditor.SerializedObject(inventory);
            var iconEffects = inventorySettings.FindProperty("skillIconEffects");
            iconEffects.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; i++) iconEffects.GetArrayElementAtIndex(i).intValue = (int)effects[i];
            inventorySettings.ApplyModifiedPropertiesWithoutUndo();
            SceneWiring.Set(inventory, "equipQButtons", equipQ);
            SceneWiring.Set(inventory, "equipRButtons", equipR);
            SceneWiring.Set(inventory, "equipQLabels", equipQLabels);
            SceneWiring.Set(inventory, "equipRLabels", equipRLabels);
            SceneWiring.Set(inventory, "pagePrevious", previous);
            SceneWiring.Set(inventory, "pageNext", next);
            SceneWiring.Set(inventory, "pageLabel", pageLabel);
            SceneWiring.Set(inventory, "nativeFilterButton", nativeFilter);
            SceneWiring.Set(inventory, "foreignFilterButton", foreignFilter);
            SceneWiring.Set(inventory, "passiveName", passiveName);
            SceneWiring.Set(inventory, "passiveDescription", passiveDescription);
            SceneWiring.Set(inventory, "necroPathLabel", necroPathLabel);
            SceneWiring.Set(inventory, "necroAttackButton", necroAttack);
            SceneWiring.Set(inventory, "necroDefenseButton", necroDefense);
            SceneWiring.Set(inventory, "herbStockLabel", herbStock);
            SceneWiring.Set(inventory, "herbBuyButton", buyHerbs);
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
