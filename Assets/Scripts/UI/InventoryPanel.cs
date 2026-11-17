using System;
using System.Collections.Generic;
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
        [SerializeField] Text title, level;
        [SerializeField] Image portrait;
        [SerializeField] Sprite[] portraits;
        [SerializeField] Text[] statistics;
        [SerializeField] Text weapon;
        [SerializeField] Image weaponImage;
        [SerializeField] Sprite[] classWeaponIcons, goodsWeaponIcons;
        [SerializeField] Text relic, torchLabel, potionLabel;
        [SerializeField] Button potionButton;
        [SerializeField] Text[] abilityLabels, abilityDescriptions, upgradeLabels;
        [SerializeField] Button[] upgradeButtons;
        [SerializeField] Image[] abilityIcons;
        [SerializeField] Sprite[] skillIcons;
        [SerializeField] AbilityEffect[] skillIconEffects;
        [SerializeField] Button[] equipQButtons, equipRButtons;
        [SerializeField] Text[] equipQLabels, equipRLabels;
        [SerializeField] Text levelCost;
        [SerializeField] Button[] statButtons;
        [SerializeField] Text message;
        [SerializeField] Selectable firstButton;
        [SerializeField] Button inventoryTab, abilitiesTab;
        [SerializeField] Button pagePrevious, pageNext, nativeFilterButton, foreignFilterButton;
        [SerializeField] Text pageLabel, passiveName, passiveDescription, necroPathLabel, herbStockLabel;
        [SerializeField] Button necroAttackButton, necroDefenseButton, herbBuyButton;

        readonly Dictionary<AbilityEffect, Sprite> icons = new Dictionary<AbilityEffect, Sprite>();
        readonly List<AbilityDefinition> visible = new List<AbilityDefinition>();
        string[] rowIds;
        int page;
        bool foreign;
        CharacterClass shownClass = (CharacterClass)(-1);
        Action closed;
        public bool IsOpen { get; private set; }
        public bool ShowingAbilities { get; private set; }
        public int CurrentPage => page;
        public bool ShowingForeignSkills => foreign;
        PlayerProgression Progression => PlayerProgression.Instance;
        public string VisibleSkillId(int row) => rowIds != null && row >= 0 && row < rowIds.Length ? rowIds[row] : string.Empty;

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
        void SelectTab(bool abilities)
        {
            ShowingAbilities = abilities;
            inventoryPage.SetActive(!abilities);
            abilitiesPage.SetActive(abilities);
            inventoryTab.interactable = abilities;
            abilitiesTab.interactable = !abilities;
            Refresh();
        }

        /// <summary>The same four reusable rows render preview and live catalogs.</summary>
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
            FillStats(new[] { card.Attack, card.Defense, card.Magic, card.Speed });
            statistics[6].text = string.Format(InventoryText.HonorFormat, definition.StartHonor);
            statistics[7].text = string.Format(InventoryText.FoodFormat, 100);
            statistics[8].text = string.Format(InventoryText.GoldFormat, 20);
            statistics[9].text = string.Format(InventoryText.SoulsFormat, 6);
            weapon.text = InventoryText.Weapon + "\n" + InventoryText.ClassWeapons[0];
            weaponImage.sprite = classWeaponIcons[0];
            relic.text = InventoryText.Relic + "\n" + InventoryText.Empty;
            torchLabel.text = InventoryText.TorchTake;
            potionLabel.text = string.Format(InventoryText.PotionFormat, 1);
            page = 0; foreign = false;
            RenderSkills(definition, null);
            RenderClassControls(definition, null);
            levelCost.text = string.Format(InventoryText.LevelCostFormat, 12);
            message.text = string.Empty;
        }
        void FillStats(int[] values)
        {
            string[] names = { MenuText.StatAttack, MenuText.StatDefense, MenuText.StatMagic, MenuText.StatSpeed };
            for (int i = 0; i < values.Length; i++) statistics[i + 2].text = string.Format(InventoryText.StatFormat, names[i], values[i]);
        }
        public void Refresh()
        {
            PlayerProgression p = Progression;
            GameSession session = GameSession.Instance;
            if (p == null || session == null) return;
            if (shownClass != session.Class.Id) { page = 0; foreign = false; shownClass = session.Class.Id; }
            Health health = p.GetComponent<Health>();
            PlayerAbilities skills = p.GetComponent<PlayerAbilities>();
            title.text = session.Class.Name;
            level.text = string.Format(InventoryText.LevelFormat, p.Level);
            portrait.sprite = portraits[(int)session.Class.Id];
            statistics[0].text = string.Format(InventoryText.HealthFormat, Mathf.CeilToInt(health.Current), Mathf.RoundToInt(health.Max));
            statistics[1].text = string.Format(InventoryText.FocusFormat, Mathf.CeilToInt(skills.Focus), Mathf.RoundToInt(skills.MaxFocus));
            FillStats(new[] { p.Attack, p.Defense, p.Magic, p.Speed });
            statistics[6].text = string.Format(InventoryText.HonorFormat, session.Honor);
            statistics[7].text = string.Format(InventoryText.FoodFormat, p.Food);
            statistics[8].text = string.Format(InventoryText.GoldFormat, session.Gold);
            statistics[9].text = string.Format(InventoryText.SoulsFormat, keeper.Souls);
            weapon.text = InventoryText.Weapon + "\n" + (session.Weapon?.Name ?? InventoryText.ClassWeapons[(int)session.Class.Id]);
            int goods = Array.IndexOf(Catalog.ItemsOf(ItemSlot.Weapon), session.Weapon);
            weaponImage.sprite = goods >= 0 ? goodsWeaponIcons[goods] : classWeaponIcons[(int)session.Class.Id];
            relic.text = InventoryText.Relic + "\n" + (session.Relic?.Name ?? InventoryText.Empty);
            torchLabel.text = p.TorchEquipped ? InventoryText.TorchPutAway : InventoryText.TorchTake;
            potionLabel.text = string.Format(InventoryText.PotionFormat, session.Potions);
            potionButton.interactable = session.Potions > 0 && health.Normalized < 1f;
            RenderSkills(session.Class, p);
            RenderClassControls(session.Class, p);
            levelCost.text = string.Format(InventoryText.LevelCostFormat, p.UpgradeCost);
            foreach (Button button in statButtons) button.interactable = keeper.Souls >= p.UpgradeCost;
        }
        Sprite Icon(AbilityEffect effect)
        {
            if (icons.Count == 0)
                for (int i = 0; i < Mathf.Min(skillIcons.Length, skillIconEffects.Length); i++) icons[skillIconEffects[i]] = skillIcons[i];
            return icons.TryGetValue(effect, out Sprite sprite) ? sprite : null;
        }
        void RenderSkills(ClassDefinition definition, PlayerProgression p)
        {
            if (definition.Id == CharacterClass.Alchemist) foreign = false;
            visible.Clear();
            foreach (AbilityDefinition skill in AbilityBook.All(definition))
                if (AbilityBook.IsNative(definition, skill) != foreign) visible.Add(skill);
            int rows = abilityLabels.Length;
            int pages = Mathf.Max(1, Mathf.CeilToInt(visible.Count / (float)rows));
            page = Mathf.Clamp(page, 0, pages - 1);
            rowIds = new string[rows];
            string[] starters = AbilityBook.StarterIds(definition.Id), loadout = AbilityBook.DefaultLoadoutIds(definition.Id);
            PlayerAbilities caster = p != null ? p.GetComponent<PlayerAbilities>() : null;
            for (int row = 0; row < rows; row++)
            {
                int index = page * rows + row;
                bool exists = index < visible.Count;
                abilityLabels[row].transform.parent.gameObject.SetActive(exists);
                if (!exists) continue;
                AbilityDefinition skill = visible[index];
                rowIds[row] = skill.Id;
                int tier = p != null ? p.SkillTier(skill.Id) : Array.IndexOf(starters, skill.Id) >= 0 ? 1 : 0;
                bool native = AbilityBook.IsNative(definition, skill);
                abilityLabels[row].text = skill.IsRecipe ? string.Format(SkillText.RecipeFormat, skill.Name, p?.RecipeStock(skill.Id) ?? 0) :
                    string.Format(tier == 0 ? SkillText.UnlearnedFormat : native ? SkillText.TierFormat : SkillText.ForeignTierFormat, skill.Name, caster != null ? Mathf.RoundToInt(caster.CastCost(skill)) : skill.Cost, tier);
                string summary = skill.Tier(Mathf.Max(1, tier)).Summary;
                abilityDescriptions[row].text = skill.Description + (string.IsNullOrEmpty(summary) ? string.Empty : " " + summary);
                abilityIcons[row].sprite = Icon(skill.Effect);
                bool allowed = p != null && p.CanEquipSkill(skill.Id);
                bool first = p != null ? p.EquippedAbilityId(0) == skill.Id : loadout[0] == skill.Id;
                bool second = p != null ? p.EquippedAbilityId(1) == skill.Id : loadout[1] == skill.Id;
                equipQLabels[row].text = first ? InventoryText.ActiveQ : InventoryText.EquipQ;
                equipRLabels[row].text = second ? InventoryText.ActiveR : InventoryText.EquipR;
                equipQButtons[row].interactable = allowed && !first;
                equipRButtons[row].interactable = allowed && !second;
                if (skill.IsRecipe)
                {
                    upgradeLabels[row].text = tier == 0 ? string.Format(SkillText.IntellectRequiredFormat, skill.IntellectRequired) : string.Format(SkillText.CraftFormat, skill.PlantCost);
                    upgradeButtons[row].interactable = allowed && p.Herbs >= skill.PlantCost;
                }
                else
                {
                    int cost = p != null ? p.SkillUpgradeCost(skill.Id) : tier == 0 ? PlayerProgression.LearnCost : 25;
                    upgradeLabels[row].text = SkillRequirement(definition, skill, p) ??
                        (cost == 0 ? native ? InventoryText.TierMaximum : SkillText.ForeignMaximum :
                            string.Format(tier == 0 ? SkillText.LearnFormat : InventoryText.UpgradeFormat, cost));
                    upgradeButtons[row].interactable = p != null && p.CanLearnSkill(skill.Id) && keeper.Souls >= cost;
                }
            }
            pageLabel.text = string.Format(SkillText.PageFormat, page + 1, pages);
            pagePrevious.interactable = page > 0;
            pageNext.interactable = page + 1 < pages;
            nativeFilterButton.interactable = foreign;
            foreignFilterButton.interactable = !foreign && definition.Id != CharacterClass.Alchemist;
        }
        static string SkillRequirement(ClassDefinition definition, AbilityDefinition skill, PlayerProgression p)
        {
            if (definition.Id != CharacterClass.Necromancer || skill.Path == NecromancerPath.None) return null;
            return p == null || p.ChosenPath == NecromancerPath.None ? SkillText.NoPath :
                p.ChosenPath != skill.Path ? SkillText.PathLocked : null;
        }
        void RenderClassControls(ClassDefinition definition, PlayerProgression p)
        {
            passiveName.text = AbilityBook.PassiveName(definition);
            passiveDescription.text = AbilityBook.PassiveDescription(definition);
            bool necro = definition.Id == CharacterClass.Necromancer, alchemist = definition.Id == CharacterClass.Alchemist;
            necroPathLabel.gameObject.SetActive(necro);
            necroAttackButton.gameObject.SetActive(necro);
            necroDefenseButton.gameObject.SetActive(necro);
            herbStockLabel.gameObject.SetActive(alchemist);
            herbBuyButton.gameObject.SetActive(alchemist);
            NecromancerPath path = p?.ChosenPath ?? NecromancerPath.None;
            necroPathLabel.text = path == NecromancerPath.None ? SkillText.NoPath :
                path == NecromancerPath.Attack ? SkillText.AttackPathChosen : SkillText.DefensePathChosen;
            necroAttackButton.interactable = necroDefenseButton.interactable = p != null && path == NecromancerPath.None;
            herbStockLabel.text = string.Format(SkillText.HerbsFormat, p?.Herbs ?? 6, p?.Intellect ?? 8);
            herbBuyButton.interactable = p != null && p.CanBuyHerbs;
            statButtons[1].GetComponentInChildren<Text>().text = alchemist ? SkillText.RaiseIntellect : InventoryText.RaiseFocus;
        }
        public void PreviousPage() { page = Mathf.Max(0, page - 1); Refresh(); }
        public void NextPage() { page++; Refresh(); }
        public void ShowNativeSkills() { page = 0; foreign = false; Refresh(); }
        public void ShowForeignSkills() { page = 0; foreign = true; Refresh(); }
        public void ChooseAttackPath() => ChoosePath(NecromancerPath.Attack);
        public void ChooseDefensePath() => ChoosePath(NecromancerPath.Defense);
        void ChoosePath(NecromancerPath path)
        {
            message.text = Progression.TryChoosePath(path) ? SkillText.PathChosen : SkillText.CannotLearn;
            SaveAndRefresh();
        }
        public void BuyHerbs()
        {
            message.text = Progression.TryBuyHerbs() ? SkillText.HerbsBought :
                !Progression.GetComponent<PlayerAbilities>().IsInTown ? SkillText.TownOnly : SkillText.NoGold;
            SaveAndRefresh();
        }
        public void ToggleTorch() { Progression.ToggleTorch(); SaveAndRefresh(); }
        public void DrinkPotion() { Progression.GetComponent<PlayerAbilities>().DrinkPotion(); SaveAndRefresh(); }
        public void RaiseStat(int index)
        {
            message.text = Progression.TryRaiseStat(index) ? string.Empty : InventoryText.CannotAfford;
            SaveAndRefresh();
        }
        public void UpgradeAbility(int slot)
        {
            message.text = Progression.TryUpgradeAbility(slot) ? SkillText.Learned : SkillText.CannotLearn;
            SaveAndRefresh();
        }
        public void UpgradeKnownAbility(int row)
        {
            string id = VisibleSkillId(row);
            AbilityDefinition skill = AbilityBook.Find(id);
            if (skill == null) return;
            message.text = skill.IsRecipe ? Progression.TryCraftRecipe(id) ? SkillText.Crafted : SkillText.CannotCraft :
                Progression.TryLearnOrUpgradeSkill(id) ? SkillText.Learned : Progression.CanLearnSkill(id) ? InventoryText.CannotAfford : SkillText.CannotLearn;
            SaveAndRefresh();
        }
        public void EquipQ(int row) => Equip(0, row);
        public void EquipR(int row) => Equip(1, row);
        void Equip(int slot, int row)
        {
            string id = VisibleSkillId(row);
            if (string.IsNullOrEmpty(id)) return;
            message.text = Progression.TryEquipAbility(slot, id) ? InventoryText.LoadoutSaved : SkillText.CannotLearn;
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
