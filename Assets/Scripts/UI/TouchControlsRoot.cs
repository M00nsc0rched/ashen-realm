using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshenRealm.UI
{
    /// <summary>
    /// Shows the touch control layer while the input hub is in touch mode, as see-through as the player set it. The pause
    /// button is handled here too: with the buttons hidden it fades instead of leaving, because on a touch device it is the
    /// only way back to the settings that hid them. The fighting buttons leave while the weapons are at a door.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TouchControlsRoot : MonoBehaviour
    {
        const float HiddenPauseAlpha = 0.35f;
        const float EditorMinAlpha = 0.6f;

        [SerializeField] PlayerInputHub input;
        [SerializeField] CanvasGroup pauseButton;
        [SerializeField] PlayerCombat combat;
        [SerializeField] CanvasGroup[] fightingButtons;

        CanvasGroup group;
        bool forced;
        bool menuOpen;

        void Awake() => group = GetComponent<CanvasGroup>();

        void OnEnable()
        {
            if (input == null) return;
            input.TouchModeChanged += OnTouchMode;
            GameSettings.Changed += Apply;
            combat.ArmedChanged += Apply;
            Apply();
        }

        void OnDisable()
        {
            if (input != null) input.TouchModeChanged -= OnTouchMode;
            GameSettings.Changed -= Apply;
            if (combat != null) combat.ArmedChanged -= Apply;
        }

        /// <summary>The layout editor shows the controls whatever the mode, and never too faint to place.</summary>
        public void SetForced(bool value)
        {
            forced = value;
            Apply();
        }

        /// <summary>The menu button remains reachable after desktop pointer lock has been released.</summary>
        public void SetMenuOpen(bool value)
        {
            menuOpen = value;
            Apply();
        }

        void OnTouchMode(bool touchMode) => Apply();

        void Apply()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            float opacity = GameSettings.ButtonOpacity;
            bool show = forced || input.TouchMode;
            group.alpha = !show ? 0f : forced ? Mathf.Max(opacity, EditorMinAlpha) : opacity;
            group.interactable = show;
            group.blocksRaycasts = show;

            bool hiddenByChoice = GameSettings.TouchButtonsMode == GameSettings.TouchButtons.Hidden;
            bool touchDevice = Application.isMobilePlatform || Touchscreen.current != null;
            pauseButton.alpha = menuOpen ? Mathf.Max(opacity, 0.85f) : forced ? Mathf.Max(opacity, EditorMinAlpha) :
                !hiddenByChoice ? opacity : touchDevice ? HiddenPauseAlpha : 0f;
            pauseButton.blocksRaycasts = pauseButton.alpha > 0f;

            bool fighting = forced || combat.Armed;
            foreach (CanvasGroup button in fightingButtons)
            {
                button.alpha = fighting ? 1f : 0f;
                button.blocksRaycasts = fighting;
            }
        }
    }
}
