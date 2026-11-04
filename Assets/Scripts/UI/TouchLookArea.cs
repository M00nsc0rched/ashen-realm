using AshenRealm.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AshenRealm.UI
{
    /// <summary>
    /// Transparent drag surface for camera rotation. It sits behind the action buttons, so a press that
    /// starts on a button never reaches it. Tracks a single finger at a time.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        const int NoPointer = int.MinValue;

        [SerializeField] PlayerInputHub input;

        int pointerId = NoPointer;

        void OnDisable() => pointerId = NoPointer;

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId == NoPointer) pointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId && input != null)
                input.AddTouchLook(eventData.delta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) pointerId = NoPointer;
        }
    }
}
