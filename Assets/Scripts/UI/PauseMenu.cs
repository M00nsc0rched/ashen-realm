using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Story;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// Pause overlay: freezes time and gameplay input, and leads to the settings and back to the main menu.
    /// Opened by the pause button, Esc, a gamepad's Start, or the browser releasing pointer lock.
    /// Every "go back" request of the game arrives here, so this is also where it finds what to close.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] PlayerInputHub input;
        [SerializeField] CanvasGroup group;
        [SerializeField, Optional] DialoguePanel dialogue;
        [SerializeField, Optional] ShopPanel shop;
        [SerializeField, Optional] MainMenu mainMenu;
        [SerializeField, Optional] SettingsPanel settings;
        [SerializeField, Optional] SoulKeeper keeper;
        [SerializeField] Text fullscreenLabel;
        [SerializeField, Optional] Text objectiveLabel;
        [SerializeField, Optional] Selectable firstButton;

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
            if (mainMenu != null && mainMenu.IsOpen)
            {
                mainMenu.Back();
                return;
            }
            if (settings != null && settings.HandleBack()) return;
            if (shop != null && shop.IsOpen)
            {
                shop.Close();
                lastToggle = Time.unscaledTime;
                return;
            }

            // Esc and a pointer-lock release can arrive on consecutive frames; treat them as one request.
            if (Time.unscaledTime - lastToggle < 0.3f || (dialogue != null && dialogue.IsOpen)) return;
            Apply(!IsOpen);
        }

        public void Resume() => Apply(false);

        public void SetObjective(string text)
        {
            if (objectiveLabel != null) objectiveLabel.text = text;
        }

        public void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            UpdateLabels(!Screen.fullScreen);
        }

        public void OpenSettings()
        {
            SetVisible(false);
            settings.Open(() =>
            {
                SetVisible(true);
                MenuFocus.Enter(firstButton);
            });
        }

        public void ToMainMenu() => keeper.ReturnToMenu();

        void Apply(bool open)
        {
            IsOpen = open;
            lastToggle = Time.unscaledTime;
            Time.timeScale = open ? 0f : 1f;
            if (input != null) input.SetPaused(open);
            SetVisible(open);
            UpdateLabels(Screen.fullScreen);
            if (open) MenuFocus.Enter(firstButton);
        }

        void Update()
        {
            if (IsOpen && group.interactable) MenuFocus.Keep(firstButton);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        void UpdateLabels(bool fullscreen)
        {
            if (fullscreenLabel != null) fullscreenLabel.text = fullscreen ? MenuText.ExitFullscreen : MenuText.Fullscreen;
        }
    }
}
