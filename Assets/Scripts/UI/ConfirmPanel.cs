using System;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>The one yes-or-no box, for the things that cannot be undone: releasing a soul, loading over unsaved play.</summary>
    public sealed class ConfirmPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Text questionText;
        [SerializeField] Selectable cancelButton;

        Action onYes;

        public bool IsOpen { get; private set; }

        void Awake() => SetVisible(false);

        public void Ask(string question, Action yes)
        {
            questionText.text = question;
            onYes = yes;
            IsOpen = true;
            SetVisible(true);
            MenuFocus.Enter(cancelButton);
        }

        public void Yes()
        {
            Action action = onYes;
            Cancel();
            action?.Invoke();
        }

        public void Cancel()
        {
            IsOpen = false;
            onYes = null;
            SetVisible(false);
        }

        void Update()
        {
            if (IsOpen) MenuFocus.Keep(cancelButton);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
