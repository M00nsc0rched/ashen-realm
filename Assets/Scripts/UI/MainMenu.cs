using System.Collections;
using System.Collections.Generic;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Story;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// The screen the game opens on: the Souls ledger, the roster and the way into a new soul, the Codex and the settings.
    /// It lies over the paused world in the same scene, so nothing has to load between it and playing; the world camera
    /// sleeps while it is up.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        const float RuleWidthWithPager = 240f;
        const float RuleWidthAlone = 395f;

        [SerializeField] PlayerInputHub input;
        [SerializeField] CanvasGroup group;
        [Tooltip("The menu panel, without the painting: stands aside for the Codex and the settings.")]
        [SerializeField] CanvasGroup panelGroup;
        [SerializeField] Camera worldCamera;
        [SerializeField] SoulKeeper keeper;
        [SerializeField] StoryDirector director;
        [SerializeField] SettingsPanel settings;
        [SerializeField] CodexPanel codex;
        [SerializeField] ConfirmPanel confirm;
        [SerializeField] ClassCarousel carousel;

        [Header("Ledger")]
        [SerializeField] Text soulsLabel;
        [SerializeField] Text statsLabel;

        [Header("Roster")]
        [SerializeField] GameObject slot;
        [SerializeField] Text slotTitle;
        [SerializeField] Text slotHonorGold;
        [SerializeField] Text slotState;
        [SerializeField] GameObject emptyRoster;
        [SerializeField] GameObject pager;
        [SerializeField] Text pageLabel;
        [SerializeField] GameObject releaseButton;
        [SerializeField] RectTransform headingRule;

        [Header("Focus")]
        [SerializeField] Selectable slotButton;
        [SerializeField] Selectable newSoulButton;

        List<SoulRecord> souls = new List<SoulRecord>();
        int page;
        bool coveredByLayout;

        public bool IsOpen { get; private set; }
        public SoulRecord SelectedSoul => page < souls.Count ? souls[page] : null;

        void Awake() => keeper.Loaded += Hide;

        void OnDestroy() => keeper.Loaded -= Hide;

        IEnumerator Start()
        {
            string pending = SoulKeeper.TakePendingSoul();
            SoulRecord soul = pending != null ? SaveStore.Find(pending) : null;
            if (soul == null)
            {
                Show();
                yield break;
            }

            // A load from inside the game: the scene has just reloaded. Wait a frame so the class rig has started.
            SetVisible(false);
            yield return null;
            keeper.Load(soul);
        }

        public void Show()
        {
            IsOpen = true;
            Time.timeScale = 0f;
            input.SetPaused(true);
            worldCamera.enabled = false;
            SetVisible(true);
            Refresh();
            MenuFocus.Enter(souls.Count > 0 ? slotButton : newSoulButton);
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SetVisible(false);
            worldCamera.enabled = true;
            input.SetPaused(false);
            Time.timeScale = 1f;
        }

        /// <summary>The button layout editor needs to see the world and the HUD under it.</summary>
        public void SetCoveredByLayout(bool covered)
        {
            if (!IsOpen) return;
            coveredByLayout = covered;
            worldCamera.enabled = covered;
            SetVisible(!covered);
        }

        /// <summary>Esc or the gamepad's B: closes whatever lies on top, never the menu itself.</summary>
        public void Back()
        {
            if (confirm.IsOpen) confirm.Cancel();
            else if (carousel.IsOpen) carousel.Back();
            else if (codex.IsOpen) codex.Close();
            else settings.HandleBack();
        }

        public void Refresh()
        {
            MetaRecord meta = keeper.Meta;
            souls = SaveStore.LoadRoster().Souls;
            page = Mathf.Clamp(page, 0, Mathf.Max(0, souls.Count - 1));

            soulsLabel.text = string.Format(MenuText.SoulsFormat, meta.Souls);
            statsLabel.text = string.Format(MenuText.StatsFormat, meta.Descents, meta.Deepest, meta.Codex.Count, CodexBook.Entries.Count);

            SoulRecord soul = SelectedSoul;
            slot.SetActive(soul != null);
            releaseButton.SetActive(soul != null);
            emptyRoster.SetActive(soul == null);
            if (soul != null)
            {
                string name = MenuText.WithArticle(Catalog.ClassOf(soul.Class).Name);
                slotTitle.text = string.Format(MenuText.SlotTitleFormat, name, soul.Level);
                slotHonorGold.text = $"{MenuText.TierOf(soul.Honor)}   ·   {soul.Gold}";
                slotState.text = "·   " + (soul.Descents == 0 ? MenuText.Untouched : string.Format(MenuText.DescentsFormat, soul.Descents));
            }

            bool several = souls.Count > 1;
            pager.SetActive(several);
            pageLabel.text = string.Format(MenuText.PageFormat, page + 1, souls.Count);
            Vector2 rule = headingRule.sizeDelta;
            rule.x = several ? RuleWidthWithPager : RuleWidthAlone;
            headingRule.sizeDelta = rule;
        }

        public void NextSoul() => Turn(1);
        public void PreviousSoul() => Turn(-1);

        void Turn(int step)
        {
            if (souls.Count == 0) return;
            page = (page + step + souls.Count) % souls.Count;
            Refresh();
        }

        public void ContinueSoul()
        {
            if (SelectedSoul != null) keeper.Resume(SelectedSoul);
        }

        public void NewSoul()
        {
            SetPanelVisible(false);
            carousel.Open();
        }

        /// <summary>The row of cards closed without a choice.</summary>
        public void ChoiceClosed() => ReturnFocus();

        /// <summary>A card was taken: the menu goes and the soul starts.</summary>
        public void BeginSoul(CharacterClass id)
        {
            if (!keeper.IsClassUnlocked(id)) return;
            SetPanelVisible(true);
            Hide();
            director.StartSoul(id);
        }

        public void OpenCodex()
        {
            SetPanelVisible(false);
            codex.Open(ReturnFocus);
        }

        public void OpenSettings()
        {
            SetPanelVisible(false);
            settings.Open(ReturnFocus);
        }

        public void ReleaseSoul()
        {
            SoulRecord soul = SelectedSoul;
            if (soul == null) return;
            string name = MenuText.WithArticle(Catalog.ClassOf(soul.Class).Name);
            confirm.Ask(string.Format(MenuText.ReleaseQuestionFormat, name), () =>
            {
                SaveStore.Delete(soul.Id);
                Refresh();
                ReturnFocus();
            });
        }

        void ReturnFocus()
        {
            SetPanelVisible(true);
            Refresh();
            MenuFocus.Enter(souls.Count > 0 ? slotButton : newSoulButton);
        }

        void Update()
        {
            bool onTop = IsOpen && !coveredByLayout && !settings.IsOpen && !codex.IsOpen && !confirm.IsOpen && !carousel.IsOpen;
            if (onTop) MenuFocus.Keep(souls.Count > 0 ? slotButton : newSoulButton);
        }

        void SetPanelVisible(bool visible)
        {
            panelGroup.alpha = visible ? 1f : 0f;
            panelGroup.interactable = visible;
            panelGroup.blocksRaycasts = visible;
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
