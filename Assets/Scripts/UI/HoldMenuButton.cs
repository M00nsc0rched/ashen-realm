using UnityEngine;
using UnityEngine.EventSystems;

namespace AshenRealm.UI
{
    /// <summary>A normal click pauses; one continuous five-second hold opens the hidden editor.</summary>
    public sealed class HoldMenuButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
    {
        public const float HoldSeconds = 5f;
        [SerializeField] PauseMenu menu;
        bool holding;
        bool consumed;
        float started;

        public bool Holding => holding;
        public float HeldSeconds => holding ? Time.unscaledTime - started : 0f;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            consumed = false;
            holding = true;
            started = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData) => holding = false;
        public void OnPointerExit(PointerEventData eventData) => holding = false;
        public void OnCancel(BaseEventData eventData) => Cancel();
        void OnDisable() => Cancel();
        void OnApplicationFocus(bool focus) { if (!focus) Cancel(); }
        void Cancel() { holding = false; consumed = false; }

        void Update()
        {
            if (!holding || consumed || Time.unscaledTime - started < HoldSeconds) return;
            consumed = true;
            holding = false;
            menu.OpenSecret();
        }

        public void Click()
        {
            if (consumed) { consumed = false; return; }
            menu.Toggle();
        }
    }
}
