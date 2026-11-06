using System;
using System.Collections.Generic;
using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.UI
{
    /// <summary>
    /// Where the player wants the on-screen controls, and the editor that moves them: it shows the controls over the frozen
    /// world, puts a ring on each, and writes the result to <see cref="GameSettings"/> when the player is done.
    /// </summary>
    public sealed class TouchLayout : MonoBehaviour
    {
        [Serializable]
        sealed class Saved
        {
            public List<Item> Items = new List<Item>();
        }

        [Serializable]
        sealed class Item
        {
            public string Name;
            public float X, Y;
        }

        [SerializeField] TouchControlsRoot controls;
        [SerializeField] TouchLayoutHandle[] handles;
        [SerializeField, Optional] CanvasGroup editorBar;

        readonly Dictionary<string, Vector2> defaults = new Dictionary<string, Vector2>();
        Action onClosed;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            foreach (TouchLayoutHandle handle in handles) defaults[handle.LayoutName] = handle.Position;
            ShowEditor(false);
        }

        void Start() => Load();

        public void Open(Action closed)
        {
            onClosed = closed;
            IsOpen = true;
            ShowEditor(true);
        }

        /// <summary>"Kész": keeps the positions and goes back to the settings.</summary>
        public void Finish()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Store();
            ShowEditor(false);
            Action closed = onClosed;
            onClosed = null;
            closed?.Invoke();
        }

        public void ResetLayout()
        {
            foreach (TouchLayoutHandle handle in handles)
            {
                handle.Position = defaults[handle.LayoutName];
                if (handle.isActiveAndEnabled) handle.Follow();
            }
            GameSettings.TouchLayoutJson = string.Empty;
            GameSettings.Flush();
        }

        void ShowEditor(bool editing)
        {
            controls.SetForced(editing);
            foreach (TouchLayoutHandle handle in handles) handle.gameObject.SetActive(editing);
            if (editorBar == null) return;
            editorBar.alpha = editing ? 1f : 0f;
            editorBar.interactable = editing;
            editorBar.blocksRaycasts = editing;
        }

        void Load()
        {
            string json = GameSettings.TouchLayoutJson;
            if (string.IsNullOrEmpty(json)) return;

            Saved saved = JsonUtility.FromJson<Saved>(json);
            foreach (TouchLayoutHandle handle in handles)
            {
                Item item = saved.Items.Find(i => i.Name == handle.LayoutName);
                if (item != null) handle.Position = new Vector2(item.X, item.Y);
            }
        }

        void Store()
        {
            var saved = new Saved();
            foreach (TouchLayoutHandle handle in handles)
                saved.Items.Add(new Item { Name = handle.LayoutName, X = handle.Position.x, Y = handle.Position.y });
            GameSettings.TouchLayoutJson = JsonUtility.ToJson(saved);
            GameSettings.Flush();
        }
    }
}
