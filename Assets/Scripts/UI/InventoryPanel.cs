using System;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Player;
using AshenRealm.Story;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    public sealed class InventoryPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] GameObject inventoryPage;
        [SerializeField] GameObject abilitiesPage;
        [SerializeField] SoulKeeper keeper;
        [SerializeField] Text title;
        [SerializeField] Text level;
        [SerializeField] Image portrait;
        [SerializeField] Sprite[] portraits;
        [SerializeField] Text[] statistics;
        [SerializeField] Text weapon;
        [SerializeField] Text relic;
        [SerializeField] Text torchLabel;
        [SerializeField] Text potionLabel;
        [SerializeField] Button potionButton;
        [SerializeField] Text[] abilityLabels;
        [SerializeField] Text[] abilityDescriptions;
        [SerializeField] Text[] upgradeLabels;
        [SerializeField] Button[] upgradeButtons;
        [SerializeField] Text levelCost;
        [SerializeField] Button[] statButtons;
        [SerializeField] Text message;
        [SerializeField] Selectable firstButton;
        [SerializeField] Button inventoryTab;
        [SerializeField] Button abilitiesTab;

        Action closed;
        public bool IsOpen { get; private set; }
        public bool ShowingAbilities { get; private set; }
        PlayerProgression Progression => PlayerProgression.Instance;

        void Awake() => SetVisible(false);

        public void Open(Action onClose)
        {
            closed = onClose;
            IsOpen = true;
            message.text = string.Empty;
            ShowInventory();
            SetVisible(true);
            MenuFocus.Enter(firstButton);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SetVisible(false);
            keeper.Save();
            Action callback = closed;
            closed = null;
            callback?.Invoke();
        }

        public void ShowInventory() => SelectTab(false);
        public void ShowAbilities() => SelectTab(true);

        /// <summary>Sample content for the existing edit-mode screenshot workflow; gameplay always reads live state.</summary>
        public void Preview(bool abilities)
        {
            ClassDefinition definition = Catalog.Classes[0];
            ClassCard card = Catalog.Cards[0];
            ShowingAbilities = abilities;
            inventoryPage.SetActive(!abilities);
            abilitiesPage.SetActive(abilities);
            title.text = definition.Name;
            level.text = string.Format(InventoryText.LevelFormat, 1);
            portrait.sprite = portraits[0];
            statistics[0].text = string.Format(InventoryText.HealthFormat, Mathf.RoundToInt(definition.MaxHealth), Mathf.RoundToInt(definition.MaxHealth));
            statistics[1].text = string.Format(InventoryText.FocusFormat, definition.MaxFocus, definition.MaxFocus);
            int[] values = { card.Attack, card.Defense, card.Magic, card.Speed };
            string[] names = { MenuText.StatAttack, MenuText.StatDefense, MenuText.StatMagic, MenuText.StatSpeed };
            for (int i = 0; i < values.Length; i++) statistics[i + 2].text = string.Format(InventoryText.StatFormat, names[i], values[i]);
            statistics[6].text = string.Format(InventoryText.HonorFormat, definition.StartHonor);
            statistics[7].text = string.Format(InventoryText.FoodFormat, 100);
            statistics[8].text = string.Format(InventoryText.GoldFormat, 20);
            statistics[9].text = string.Format(InventoryText.SoulsFormat, 6);
            weapon.text = InventoryText.Weapon + "\n" + InventoryText.ClassWeapons[0];
            relic.text = InventoryText.Relic + "\n" + InventoryText.Empty;
            torchLabel.text = InventoryText.TorchTake;
            potionLabel.text = string.Format(InventoryText.PotionFormat, 1);
            AbilityDefinition[] definitions = { definition.Ability1, definition.Ability2 };
            for (int i = 0; i < definitions.Length; i++)
            {
                abilityLabels[i].text = string.Format(InventoryText.AbilityFormat, definitions[i].Name, definitions[i].Cost, 1);
                abilityDescriptions[i].text = definitions[i].Description;
                upgradeLabels[i].text = string.Format(InventoryText.UpgradeFormat, 25);
            }
            levelCost.text = string.Format(InventoryText.LevelCostFormat, 12);
            message.text = string.Empty;
        }
        void SelectTab(bool abilities)
        {
            ShowingAbilities = abilities;
            inventoryPage.SetActive(!abilities);
            abilitiesPage.SetActive(abilities);
            inventoryTab.interactable = abilities;
            abilitiesTab.interactable = !abilities;
            Refresh();
        }

        public void Refresh()
        {
            PlayerProgression p = Progression;
            GameSession session = GameSession.Instance;
            if (p == null || session == null) return;
            Health health = p.GetComponent<Health>();
            PlayerAbilities skills = p.GetComponent<PlayerAbilities>();
            title.text = session.Class.Name;
            level.text = string.Format(InventoryText.LevelFormat, p.Level);
            portrait.sprite = portraits[Mathf.Clamp((int)session.Class.Id, 0, portraits.Length - 1)];
            statistics[0].text = string.Format(InventoryText.HealthFormat, Mathf.CeilToInt(health.Current), Mathf.RoundToInt(health.Max));
            statistics[1].text = string.Format(InventoryText.FocusFormat, Mathf.CeilToInt(skills.Focus), Mathf.RoundToInt(skills.MaxFocus));
            int[] values = { p.Attack, p.Defense, p.Magic, p.Speed };
            string[] names = { MenuText.StatAttack, MenuText.StatDefense, MenuText.StatMagic, MenuText.StatSpeed };
            for (int i = 0; i < values.Length; i++) statistics[i + 2].text = string.Format(InventoryText.StatFormat, names[i], values[i]);
            statistics[6].text = string.Format(InventoryText.HonorFormat, session.Honor);
            statistics[7].text = string.Format(InventoryText.FoodFormat, p.Food);
            statistics[8].text = string.Format(InventoryText.GoldFormat, session.Gold);
            statistics[9].text = string.Format(InventoryText.SoulsFormat, keeper.Souls);
            weapon.text = InventoryText.Weapon + "\n" + (session.Weapon?.Name ?? InventoryText.ClassWeapons[(int)session.Class.Id]);
            relic.text = InventoryText.Relic + "\n" + (session.Relic?.Name ?? InventoryText.Empty);
            torchLabel.text = p.TorchEquipped ? InventoryText.TorchPutAway : InventoryText.TorchTake;
            potionLabel.text = string.Format(InventoryText.PotionFormat, session.Potions);
            potionButton.interactable = session.Potions > 0 && health.Normalized < 1f;
            AbilityDefinition[] abilities = { session.Class.Ability1, session.Class.Ability2 };
            for (int i = 0; i < abilities.Length; i++)
            {
                int tier = p.AbilityTier(i);
                abilityLabels[i].text = string.Format(InventoryText.AbilityFormat, abilities[i].Name, abilities[i].Cost, tier);
                abilityDescriptions[i].text = abilities[i].Description;
                upgradeLabels[i].text = tier >= 3 ? InventoryText.TierMaximum : string.Format(InventoryText.UpgradeFormat, p.AbilityUpgradeCost(i));
                upgradeButtons[i].interactable = tier < 3 && keeper.Souls >= p.AbilityUpgradeCost(i);
            }
            levelCost.text = string.Format(InventoryText.LevelCostFormat, p.UpgradeCost);
            foreach (Button button in statButtons) button.interactable = keeper.Souls >= p.UpgradeCost;
        }

        public void ToggleTorch() { Progression.ToggleTorch(); SaveAndRefresh(); }
        public void DrinkPotion() { Progression.GetComponent<PlayerAbilities>().DrinkPotion(); SaveAndRefresh(); }
        public void RaiseStat(int index)
        {
            if (!Progression.TryRaiseStat(index)) message.text = InventoryText.CannotAfford;
            else message.text = string.Empty;
            SaveAndRefresh();
        }
        public void UpgradeAbility(int slot)
        {
            if (!Progression.TryUpgradeAbility(slot)) message.text = InventoryText.CannotAfford;
            else message.text = string.Empty;
            SaveAndRefresh();
        }
        void SaveAndRefresh() { keeper.Save(); Refresh(); }
        void Update() { if (IsOpen) MenuFocus.Keep(firstButton); }
        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
