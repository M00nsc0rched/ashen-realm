using System;
using System.Collections.Generic;
using AshenRealm.Core;
using AshenRealm.Story;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// The Codex: everything met so far, in every sitting. The rows come from <see cref="CodexBook"/> when it first opens;
    /// what has not been met stays a row of question marks, so the player can see how much is left.
    /// </summary>
    public sealed class CodexPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] SoulKeeper keeper;
        [SerializeField] Text titleLabel;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] Button rowTemplate;
        [SerializeField] Text sectionTemplate;
        [SerializeField] Text detailName;
        [SerializeField] Text detailBody;
        [SerializeField] Selectable closeButton;

        readonly List<(CodexEntry entry, Button button, Text label)> rows = new List<(CodexEntry, Button, Text)>();
        Action onClosed;

        public bool IsOpen { get; private set; }

        void Awake() => SetVisible(false);

        public void Open(Action closed)
        {
            onClosed = closed;
            IsOpen = true;
            SetVisible(true); // first: buttons made inside a hidden panel start out in the grey disabled look
            if (rows.Count == 0) BuildRows();

            List<string> found = keeper.Meta.Codex;
            titleLabel.text = string.Format(MenuText.CodexTitleFormat, found.Count, CodexBook.Entries.Count);
            foreach (var row in rows)
                row.label.text = found.Contains(row.entry.Id) ? row.entry.Name : MenuText.Undiscovered;

            detailName.text = string.Empty;
            detailBody.text = MenuText.CodexPick;
            scroll.verticalNormalizedPosition = 1f;
            MenuFocus.Enter(rows.Count > 0 ? rows[0].button : closeButton);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SetVisible(false);
            Action closed = onClosed;
            onClosed = null;
            closed?.Invoke();
        }

        void BuildRows()
        {
            string section = null;
            foreach (CodexEntry entry in CodexBook.Entries)
            {
                if (entry.Section != section)
                {
                    section = entry.Section;
                    Text heading = Instantiate(sectionTemplate, content);
                    heading.text = section;
                    heading.gameObject.SetActive(true);
                }

                Button button = Instantiate(rowTemplate, content);
                button.gameObject.SetActive(true);
                button.onClick.AddListener(() => ShowEntry(entry));
                rows.Add((entry, button, button.GetComponentInChildren<Text>()));
            }
        }

        void ShowEntry(CodexEntry entry)
        {
            bool known = keeper.Meta.Codex.Contains(entry.Id);
            detailName.text = known ? entry.Name : MenuText.Undiscovered;
            detailBody.text = known ? entry.Text : MenuText.UndiscoveredHint;
        }

        void Update()
        {
            if (!IsOpen) return;
            MenuFocus.Keep(rows.Count > 0 ? rows[0].button : closeButton);
            FollowSelection();
        }

        /// <summary>A controller moves the selection, not the list: scroll so the selected row stays in view.</summary>
        void FollowSelection()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || !selected.transform.IsChildOf(content)) return;

            RectTransform viewport = scroll.viewport;
            Vector3[] corners = new Vector3[4];
            ((RectTransform)selected.transform).GetWorldCorners(corners);
            float top = viewport.InverseTransformPoint(corners[1]).y;
            float bottom = viewport.InverseTransformPoint(corners[0]).y;
            float shift = top > viewport.rect.yMax ? viewport.rect.yMax - top : bottom < viewport.rect.yMin ? viewport.rect.yMin - bottom : 0f;
            if (Mathf.Abs(shift) > 0.5f) content.anchoredPosition += new Vector2(0f, shift);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
