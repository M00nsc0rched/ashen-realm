using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// Keyboard and gamepad reach the menus through the EventSystem's selection, which pointers should not leave behind:
    /// a tapped button would stay lit. Panels call <see cref="Keep"/> every frame while they are open.
    /// </summary>
    public static class MenuFocus
    {
        const float StickThreshold = 0.25f;

        /// <summary>A panel opened: a controller starts on its first control, pointers keep nothing selected.</summary>
        public static void Enter(Selectable first)
        {
            EventSystem events = EventSystem.current;
            if (events == null) return;
            events.SetSelectedGameObject(Gamepad.current != null && first != null ? first.gameObject : null);
        }

        public static void Keep(Selectable first)
        {
            EventSystem events = EventSystem.current;
            if (events == null) return;

            GameObject current = events.currentSelectedGameObject;
            if (PointerReleased())
            {
                if (current != null) events.SetSelectedGameObject(null);
                return;
            }

            bool lost = current == null || !current.activeInHierarchy;
            if (lost && first != null && first.isActiveAndEnabled && NavigationWanted())
                events.SetSelectedGameObject(first.gameObject);
        }

        static bool PointerReleased()
        {
            bool mouse = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
            bool touch = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
            return mouse || touch;
        }

        static bool NavigationWanted()
        {
            Keyboard keys = Keyboard.current;
            if (keys != null && (keys.upArrowKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame ||
                                 keys.leftArrowKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame))
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && (pad.dpad.ReadValue().sqrMagnitude > StickThreshold || pad.leftStick.ReadValue().sqrMagnitude > StickThreshold);
        }
    }
}
