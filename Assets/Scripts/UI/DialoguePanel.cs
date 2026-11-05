using System;
using AshenRealm.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// The one dialogue box: a name, a passage and up to four answers. Freezes time and gameplay input while open, so
    /// nothing bites the player mid-sentence. Answers can be clicked, tapped or picked with the number keys.
    /// </summary>
    public sealed class DialoguePanel : MonoBehaviour
    {
        [SerializeField] PlayerInputHub input;
        [SerializeField] CanvasGroup group;
        [SerializeField] Text speakerText;
        [SerializeField] Text bodyText;
        [SerializeField] Button[] buttons;
        [SerializeField] Text[] labels;

        Action<int> onChoice;
        int choiceCount;
        int openedFrame;

        public bool IsOpen { get; private set; }

        void Awake() => SetVisible(false);

        /// <summary>Shows a page. <paramref name="choice"/> receives the index of the chosen answer.</summary>
        public void Show(string speaker, string body, string[] choices, Action<int> choice)
        {
            speakerText.text = speaker;
            bodyText.text = body;
            onChoice = choice;
            choiceCount = Mathf.Min(choices.Length, buttons.Length);
            for (int i = 0; i < buttons.Length; i++)
            {
                bool used = i < choiceCount;
                buttons[i].gameObject.SetActive(used);
                if (used) labels[i].text = choices[i];
            }

            openedFrame = Time.frameCount;
            if (IsOpen) return;
            IsOpen = true;
            Time.timeScale = 0f;
            if (input != null) input.SetPaused(true);
            SetVisible(true);
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
            if (!IsOpen || Keyboard.current == null || Time.frameCount == openedFrame) return;
            var keys = Keyboard.current;
            if (keys.digit1Key.wasPressedThisFrame) Choose(0);
            else if (keys.digit2Key.wasPressedThisFrame) Choose(1);
            else if (keys.digit3Key.wasPressedThisFrame) Choose(2);
            else if (keys.digit4Key.wasPressedThisFrame) Choose(3);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
