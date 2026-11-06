using System;
using AshenRealm.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// The one dialogue box: a name, a passage and up to five answers, with the speaker's face beside the text when they
    /// have one. Freezes time and gameplay input while open, so nothing bites the player mid-sentence. Answers can be
    /// clicked, tapped or picked with the number keys.
    /// </summary>
    public sealed class DialoguePanel : MonoBehaviour
    {
        const float WideBodyWidth = 1120f;
        const float NarrowBodyWidth = 760f;
        const float NarrowBodyShift = 180f;

        [SerializeField] PlayerInputHub input;
        [SerializeField] CanvasGroup group;
        [SerializeField] Text speakerText;
        [SerializeField] Text bodyText;
        [SerializeField] GameObject portraitFrame;
        [SerializeField] Image portrait;
        [SerializeField] Button[] buttons;
        [SerializeField] Text[] labels;

        Action<int> onChoice;
        int choiceCount;
        int openedFrame;

        public bool IsOpen { get; private set; }

        /// <summary>The face on the page, if any.</summary>
        public Sprite Face => portraitFrame.activeSelf ? portrait.sprite : null;

        void Awake() => SetVisible(false);

        /// <summary>Shows a page. <paramref name="choice"/> receives the index of the chosen answer.</summary>
        public void Show(string speaker, string body, string[] choices, Action<int> choice, Sprite face = null)
        {
            speakerText.text = speaker;
            bodyText.text = body;
            ShowFace(face);
            onChoice = choice;
            choiceCount = Mathf.Min(choices.Length, buttons.Length);
            for (int i = 0; i < buttons.Length; i++)
            {
                bool used = i < choiceCount;
                buttons[i].gameObject.SetActive(used);
                if (used) labels[i].text = choices[i];
            }

            openedFrame = Time.frameCount;
            MenuFocus.Enter(buttons[0]);
            if (IsOpen) return;
            IsOpen = true;
            Time.timeScale = 0f;
            if (input != null) input.SetPaused(true);
            SetVisible(true);
        }

        /// <summary>A face takes the left of the page, and the text steps aside for it.</summary>
        void ShowFace(Sprite face)
        {
            bool shown = face != null;
            portraitFrame.SetActive(shown);
            portrait.sprite = face;
            RectTransform body = bodyText.rectTransform;
            body.sizeDelta = new Vector2(shown ? NarrowBodyWidth : WideBodyWidth, body.sizeDelta.y);
            body.anchoredPosition = new Vector2(shown ? NarrowBodyShift : 0f, body.anchoredPosition.y);
        }

        public void Choose(int index)
        {
            if (!IsOpen || index >= choiceCount) return;
            Action<int> callback = onChoice;

            // Close first: the callback may open the next page in the same frame.
            IsOpen = false;
            Time.timeScale = 1f;
            if (input != null) input.SetPaused(false);
            SetVisible(false);
            callback?.Invoke(index);
        }

        void Update()
        {
            // The frame guard keeps the key that opened this page from answering it.
            if (!IsOpen) return;
            MenuFocus.Keep(buttons[0]);
            if (Keyboard.current == null || Time.frameCount == openedFrame) return;
            var keys = Keyboard.current;
            if (keys.digit1Key.wasPressedThisFrame) Choose(0);
            else if (keys.digit2Key.wasPressedThisFrame) Choose(1);
            else if (keys.digit3Key.wasPressedThisFrame) Choose(2);
            else if (keys.digit4Key.wasPressedThisFrame) Choose(3);
            else if (keys.digit5Key.wasPressedThisFrame) Choose(4);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
