using AshenRealm.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// On-screen action button. Forwards press/release of its own finger to the input hub and gives
    /// immediate visual feedback. The Sprint button behaves as a toggle and mirrors the hub's state.
    /// </summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;

        [SerializeField] PlayerInputHub input;
        [SerializeField] PlayerButton button;
        [SerializeField] Image background;
        [SerializeField] Color normalColor = new Color(0.08f, 0.07f, 0.06f, 0.55f);
        [SerializeField] Color pressedColor = new Color(0.55f, 0.42f, 0.22f, 0.85f);
        [SerializeField] Color activeColor = new Color(0.62f, 0.5f, 0.25f, 0.75f);
        [SerializeField] float pressedScale = 0.9f;

        int pointerId = NoPointer;
        float scale = 1f;
        bool available = true;
        CanvasGroup group;

        public PlayerButton Button => button;
        public bool IsPressed => pointerId != NoPointer;

        void Awake() => group = GetComponent<CanvasGroup>();

        void OnDisable()
        {
            if (pointerId != NoPointer && input != null && button != PlayerButton.Sprint)
                input.SetTouchButton(button, false);
            pointerId = NoPointer;
        }

        /// <summary>Dims the button when its action has nothing to act on (e.g. no interactable in view).</summary>
        public void SetAvailable(bool value)
        {
            available = value;
            if (group != null) group.alpha = value ? 1f : 0.4f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != NoPointer) return;
            pointerId = eventData.pointerId;
            if (input != null) input.SetTouchButton(button, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            pointerId = NoPointer;
            if (input != null) input.SetTouchButton(button, false);
        }

        void Update()
        {
            bool active = button == PlayerButton.Sprint && input != null && input.SprintToggled;
            Color target = IsPressed ? pressedColor : active ? activeColor : normalColor;
            if (!available) target.a *= 0.6f;
            if (background != null)
                background.color = Color.Lerp(background.color, target, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));

            scale = Mathf.Lerp(scale, IsPressed ? pressedScale : 1f, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
