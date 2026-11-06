using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>One of the goods on the shop's row. Moving onto it with a controller shows it, as a click does.</summary>
    public sealed class ShopTile : MonoBehaviour, ISelectHandler
    {
        [SerializeField] ShopPanel panel;
        [SerializeField] int index;
        [SerializeField] Button button;
        [SerializeField] Image picture;
        [SerializeField] Text priceText;
        [SerializeField] Image highlight;

        public Button Button => button;

        public void Show(Sprite art, float x)
        {
            picture.sprite = art;
            var rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
        }

        public void SetPrice(string price) => priceText.text = price;

        public void SetChosen(bool chosen) => highlight.enabled = chosen;

        public void OnSelect(BaseEventData eventData) => panel.Select(index);
    }
}
