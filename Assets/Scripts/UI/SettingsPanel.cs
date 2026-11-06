using System;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Story;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// The settings screen, the same one from the main menu and from pause. Every control applies at once, so the player
    /// sees the HUD change behind the panel; closing the panel makes the changes survive the page.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        // The look speed slider is logarithmic: 0.25 to 4, with the default 1 in the middle.
        const float MinLook = 0.25f;
        const float LookRange = 16f;

        [SerializeField] PlayerInputHub input;
        [SerializeField] CanvasGroup group;
        [SerializeField] SoulKeeper keeper;
        [SerializeField] ConfirmPanel confirm;
        [SerializeField] TouchLayout layout;
        [SerializeField] MainMenu mainMenu;
        [SerializeField] Selectable firstControl;

        [Header("Sliders")]
        [SerializeField] Slider sensitivity;
        [SerializeField] Text sensitivityValue;
        [SerializeField] Slider buttonOpacity;
        [SerializeField] Text buttonOpacityValue;
        [SerializeField] Slider hudOpacity;
        [SerializeField] Text hudOpacityValue;

        [Header("Choices")]
        [SerializeField] ChoiceButton buttonsShown;
        [SerializeField] ChoiceButton buttonsHidden;
        [SerializeField] ChoiceButton hudShown;
        [SerializeField] ChoiceButton hudHidden;

        [Header("Saving")]
        [SerializeField] Text saveStatus;

        Action onClosed;

        public bool IsOpen { get; private set; }

        void Awake() => SetVisible(false);

        public void Open(Action closed)
        {
            onClosed = closed;
            IsOpen = true;
            saveStatus.text = string.Empty;
            Refresh();
            SetVisible(true);
            MenuFocus.Enter(firstControl);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SetVisible(false);
            GameSettings.Flush();
            Action closed = onClosed;
            onClosed = null;
            closed?.Invoke();
        }

        /// <summary>Esc or the gamepad's B. Returns false when there was nothing here to step back from.</summary>
        public bool HandleBack()
        {
            if (confirm.IsOpen) confirm.Cancel();
            else if (layout.IsOpen) layout.Finish();
            else if (IsOpen) Close();
            else return false;
            return true;
        }

        void Refresh()
        {
            float t = Mathf.Log(input.LookSensitivity / MinLook, LookRange);
            sensitivity.SetValueWithoutNotify(t);
            buttonOpacity.SetValueWithoutNotify(GameSettings.ButtonOpacity);
            hudOpacity.SetValueWithoutNotify(GameSettings.HudOpacity);
            RefreshValues();

            bool shown = input.TouchMode;
            buttonsShown.SetChosen(shown);
            buttonsHidden.SetChosen(!shown);
            hudShown.SetChosen(GameSettings.HudVisible);
            hudHidden.SetChosen(!GameSettings.HudVisible);
        }

        void RefreshValues()
        {
            sensitivityValue.text = GameText.Decimal(input.LookSensitivity);
            buttonOpacityValue.text = Percent(GameSettings.ButtonOpacity);
            hudOpacityValue.text = Percent(GameSettings.HudOpacity);
        }

        static string Percent(float value) => string.Format(MenuText.PercentFormat, Mathf.RoundToInt(value * 100f));

        public void OnSensitivityChanged(float t)
        {
            input.LookSensitivity = MinLook * Mathf.Pow(LookRange, t);
            RefreshValues();
        }

        public void OnButtonOpacityChanged(float value)
        {
            GameSettings.ButtonOpacity = value;
            RefreshValues();
        }

        public void OnHudOpacityChanged(float value)
        {
            GameSettings.HudOpacity = value;
            RefreshValues();
        }

        public void ShowButtons() => ChooseButtons(true);
        public void HideButtons() => ChooseButtons(false);
        public void ShowHud() => ChooseHud(true);
        public void HideHud() => ChooseHud(false);

        void ChooseButtons(bool shown)
        {
            input.ChooseTouchButtons(shown);
            Refresh();
        }

        void ChooseHud(bool visible)
        {
            GameSettings.HudVisible = visible;
            Refresh();
        }

        public void OpenLayoutEditor()
        {
            SetVisible(false);
            mainMenu.SetCoveredByLayout(true);
            layout.Open(() =>
            {
                mainMenu.SetCoveredByLayout(false);
                SetVisible(true);
                MenuFocus.Enter(firstControl);
            });
        }

        public void SaveSoul() => saveStatus.text = keeper.Save() ? MenuText.Saved : MenuText.NothingToSave;

        public void LoadSoul()
        {
            SoulRecord soul = keeper.LatestSoul();
            if (soul == null)
            {
                saveStatus.text = MenuText.NothingToLoad;
                return;
            }
            string name = MenuText.WithArticle(Catalog.ClassOf(soul.Class).Name);
            confirm.Ask(string.Format(MenuText.LoadQuestionFormat, name), () =>
            {
                Close();
                keeper.Resume(soul);
            });
        }

        public void RestoreDefaults()
        {
            GameSettings.ResetAll();
            input.LookSensitivity = 1f;
            input.ApplyTouchPreference();
            layout.ResetLayout();
            Refresh();
        }

        void Update()
        {
            if (IsOpen && !layout.IsOpen && !confirm.IsOpen) MenuFocus.Keep(firstControl);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
