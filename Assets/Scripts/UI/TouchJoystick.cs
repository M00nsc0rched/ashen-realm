using AshenRealm.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AshenRealm.UI
{
    /// <summary>
    /// Floating virtual joystick. The whole zone receives touches; the base jumps to the first finger
    /// that lands in the zone and follows only that finger until it lifts.
    /// </summary>
    public sealed class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        const int NoPointer = int.MinValue;

        [SerializeField] PlayerInputHub input;
        [SerializeField] RectTransform zone;
        [SerializeField] RectTransform baseRect;
        [SerializeField] RectTransform knob;
        [Tooltip("Knob travel radius in canvas units.")]
        [SerializeField] float radius = 100f;
        [SerializeField, Range(0f, 0.5f)] float deadZone = 0.1f;
        [SerializeField] CanvasGroup visuals;
        [SerializeField] float idleAlpha = 0.45f;

        int pointerId = NoPointer;
        Vector2 restPosition;

        void Awake() => restPosition = baseRect.anchoredPosition;

        void OnDisable() => Release();

        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != NoPointer) return;
            pointerId = eventData.pointerId;

            // Re-centre the base under the finger, clamped so it stays inside the zone.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                Rect r = zone.rect;
                local.x = Mathf.Clamp(local.x, r.xMin + radius, r.xMax - radius);
                local.y = Mathf.Clamp(local.y, r.yMin + radius, r.yMax - radius);
                baseRect.anchoredPosition += local - LocalPointOf(baseRect);
            }
            if (visuals != null) visuals.alpha = 1f;
            UpdateKnob(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) UpdateKnob(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) Release();
        }

        Vector2 LocalPointOf(RectTransform child)
        {
            Vector3 world = child.TransformPoint(child.rect.center);
            return zone.InverseTransformPoint(world);
        }

        void UpdateKnob(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;
            local -= baseRect.rect.center;
            Vector2 clamped = Vector2.ClampMagnitude(local, radius);
            knob.anchoredPosition = clamped;

            Vector2 value = clamped / radius;
            float magnitude = value.magnitude;
            value = magnitude < deadZone ? Vector2.zero : value.normalized * Mathf.InverseLerp(deadZone, 1f, magnitude);
            if (input != null) input.SetTouchMove(value);
        }

        void Release()
        {
            pointerId = NoPointer;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (baseRect != null) baseRect.anchoredPosition = restPosition;
            if (visuals != null) visuals.alpha = idleAlpha;
            if (input != null) input.SetTouchMove(Vector2.zero);
        }
    }
}
