using UnityEngine;
using UnityEngine.EventSystems;

namespace AshenRealm.UI
{
    /// <summary>
    /// Sits on the two buttons under the cards, which hold the selection: left and right belong to the row of cards, and
    /// the buttons' own navigation only answers up and down.
    /// </summary>
    public sealed class ClassCarouselKeys : MonoBehaviour, IMoveHandler
    {
        [SerializeField] ClassCarousel carousel;

        public void OnMove(AxisEventData eventData) => carousel.Move(eventData.moveDir);
    }
}
