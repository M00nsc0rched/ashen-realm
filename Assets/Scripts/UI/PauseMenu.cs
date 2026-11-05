using AshenRealm.Core;
using AshenRealm.Input;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// Pause overlay: freezes time and gameplay input, offers resume, fullscreen and look sensitivity.
    /// Opened by the pause button, Esc, or the browser releasing pointer lock.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] PlayerInputHub input;
        [SerializeField] CanvasGroup group;
        [SerializeField] Text sensitivityLabel;
        [SerializeField] Text fullscreenLabel;

        float lastToggle = -999f;

        public bool IsOpen { get; private set; }

        void OnEnable()
        {
            if (input != null) input.PauseRequested += Toggle;
            Apply(false);
        }

        void OnDisable()
        {
            if (input != null) input.PauseRequested -= Toggle;
            if (IsOpen) Time.timeScale = 1f;
        }

        public void Toggle()
        {
            // Esc and a pointer-lock release can arrive on consecutive frames; treat them as one request.
            if (Time.unscaledTime - lastToggle < 0.3f) return;
            Apply(!IsOpen);
        }

        public void Resume() => Apply(false);

        public void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            UpdateLabels(!Screen.fullScreen);
        }

        public void SensitivityUp() => ChangeSensitivity(1.15f);
        public void SensitivityDown() => ChangeSensitivity(1f / 1.15f);

        void ChangeSensitivity(float factor)
        {
            if (input == null) return;
            input.LookSensitivity *= factor;
            UpdateLabels(Screen.fullScreen);
        }

        void Apply(bool open)
        {
            IsOpen = open;
            lastToggle = Time.unscaledTime;
            Time.timeScale = open ? 0f : 1f;
            if (input != null) input.SetPaused(open);
            group.alpha = open ? 1f : 0f;
            group.interactable = open;
            group.blocksRaycasts = open;
            UpdateLabels(Screen.fullScreen);
        }

        void UpdateLabels(bool fullscreen)
        {
            if (sensitivityLabel != null && input != null) sensitivityLabel.text = $"{GameText.LookSpeed}  {GameText.Decimal(input.LookSensitivity)}";
            if (fullscreenLabel != null) fullscreenLabel.text = fullscreen ? GameText.ExitFullscreen : GameText.Fullscreen;
        }
    }
}
