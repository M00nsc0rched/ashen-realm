using System;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Story;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// The counter of the Cold Forge and the Grey Chapel: a row of the painted goods, what the chosen one does and a button
    /// to take it. Like the dialogue box it freezes time and gameplay input while it is open.
    /// </summary>
    public sealed class ShopPanel : MonoBehaviour
    {
        const float TileSpacing = 286f;

        [SerializeField] PlayerInputHub input;
        [SerializeField] CanvasGroup group;
        [SerializeField] Text titleText;
        [SerializeField] Text balanceText;
        [SerializeField] Text nameText;
        [SerializeField] Text modsText;
        [SerializeField] Text descriptionText;
        [SerializeField] Text effectText;
        [SerializeField] Text noticeText;
        [SerializeField] ShopTile[] tiles;
        [SerializeField] Button buyButton;
        [SerializeField] Text buyLabel;
        [SerializeField] Sprite[] art;

        IShopCounter counter;
        ItemDefinition[] stock;
        int chosen;

        public bool IsOpen { get; private set; }
        public ItemDefinition Chosen => stock[chosen];
        public int StockCount => stock != null ? stock.Length : 0;
        public string Notice => noticeText.text;
        public bool CanBuy => buyButton.interactable;

        void Awake() => SetVisible(false);

        public void Open(string title, ItemDefinition[] goods, IShopCounter seller)
        {
            counter = seller;
            stock = goods;
            titleText.text = title;
            noticeText.text = string.Empty;
            float start = -(goods.Length - 1) * 0.5f;
            for (int i = 0; i < tiles.Length; i++)
            {
                bool used = i < goods.Length;
                tiles[i].gameObject.SetActive(used);
                if (used) tiles[i].Show(ArtOf(goods[i]), (start + i) * TileSpacing);
            }

            IsOpen = true;
            Time.timeScale = 0f;
            if (input != null) input.SetPaused(true);
            SetVisible(true);
            Select(0);
            MenuFocus.Enter(tiles[0].Button);
        }

        Sprite ArtOf(ItemDefinition item) => Array.Find(art, sprite => sprite.name == item.Id);

        /// <summary>Puts a tile's item in front of the player. A click and a controller's move both end here.</summary>
        public void Select(int index)
        {
            chosen = index;
            Refresh();
        }

        public void Buy()
        {
            noticeText.text = counter.Buy(Chosen);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Time.timeScale = 1f;
            if (input != null) input.SetPaused(false);
            SetVisible(false);
        }

        void Refresh()
        {
            balanceText.text = counter.Balance;
            for (int i = 0; i < stock.Length; i++)
            {
                tiles[i].SetPrice(counter.IsCarried(stock[i]) ? MenuText.Carried : counter.PriceOf(stock[i]));
                tiles[i].SetChosen(i == chosen);
            }

            ItemDefinition item = Chosen;
            nameText.text = item.Name;
            modsText.text = MenuText.ModsOf(item);
            descriptionText.text = item.Description;
            effectText.text = counter.EffectOf(item);
            bool carried = counter.IsCarried(item);
            buyButton.interactable = !carried;
            buyLabel.text = carried ? MenuText.Carried : MenuText.Buy;
        }

        void Update()
        {
            if (!IsOpen) return;
            MenuFocus.Keep(tiles[0].Button);
            Keyboard keys = Keyboard.current;
            if (keys == null) return;
            if (keys.digit1Key.wasPressedThisFrame) Select(0);
            else if (keys.digit2Key.wasPressedThisFrame) Select(1);
            else if (keys.digit3Key.wasPressedThisFrame) Select(2);
            else if (keys.digit4Key.wasPressedThisFrame && stock.Length > 3) Select(3);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
