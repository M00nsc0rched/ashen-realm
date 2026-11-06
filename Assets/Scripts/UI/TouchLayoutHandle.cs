using UnityEngine;
using UnityEngine.EventSystems;

namespace AshenRealm.UI
{
    /// <summary>
    /// The ring the layout editor puts over one on-screen control. It lies above the control, so a drag can never press it.
    /// For the joystick the visible ring and the thing that moves differ: the whole touch zone goes along with its stick.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TouchLayoutHandle : MonoBehaviour, IPointerDownHandler, IDragHandler, IInitializePotentialDragHandler
    {
        const float EdgeMargin = 40f;

        [Tooltip("Stable name the saved position is stored under.")]
        [SerializeField] string layoutName = "";
        [Tooltip("What moves.")]
        [SerializeField] RectTransform target;
        [Tooltip("What the player sees of it, and what stays on screen.")]
        [SerializeField] RectTransform marker;
        [SerializeField] RectTransform bounds;
        [SerializeField] Canvas canvas;

        Vector2 grabbedAt;
        Vector2 grabbedPosition;

        public string LayoutName => layoutName;
        public Vector2 Position
        {
            get => target.anchoredPosition;
            set
            {
                target.anchoredPosition = value;
                KeepOnScreen();
            }
        }

        void OnEnable() => Follow();

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            grabbedAt = eventData.position;
            grabbedPosition = target.anchoredPosition;
        }

        // From where it was grabbed, not by per-frame deltas: on a slow phone a fast drag must not lose ground.
        public void OnDrag(PointerEventData eventData)
        {
            target.anchoredPosition = grabbedPosition + (eventData.position - grabbedAt) / canvas.scaleFactor;
            KeepOnScreen();
            Follow();
        }

        /// <summary>A layout saved on a wider screen may not fit this one.</summary>
        public void KeepOnScreen()
        {
            Rect area = bounds.rect;
            Vector2 centre = bounds.InverseTransformPoint(marker.TransformPoint(marker.rect.center));
            Vector2 clamped = new Vector2(
                Mathf.Clamp(centre.x, area.xMin + EdgeMargin, area.xMax - EdgeMargin),
                Mathf.Clamp(centre.y, area.yMin + EdgeMargin, area.yMax - EdgeMargin));
            target.anchoredPosition += clamped - centre;
        }

        public void Follow() => transform.position = marker.TransformPoint(marker.rect.center);
    }
}
