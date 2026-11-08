using System;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Player;
using AshenRealm.Story;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    public sealed class SecretStatsPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] SoulKeeper keeper;
        [SerializeField] InputField[] fields;
        [SerializeField] Button[] unlockButtons;
        [SerializeField] Text[] unlockLabels;
        [SerializeField] Text message;
        [SerializeField] Selectable firstButton;
        Action closed;
        public bool IsOpen { get; private set; }

        void Awake() => SetVisible(false);

        public void Open(Action onClose)
        {
            closed = onClose;
            IsOpen = true;
            message.text = string.Empty;
            Refresh();
            SetVisible(true);
            MenuFocus.Enter(firstButton);
        }

        public void Refresh()
        {
            PlayerProgression p = PlayerProgression.Instance;
            if (p == null) return;
            Health health = p.GetComponent<Health>();
            PlayerAbilities abilities = p.GetComponent<PlayerAbilities>();
            GameSession session = GameSession.Instance;
            int[] values = { p.MaxHealth, Mathf.RoundToInt(health.Current), p.MaxFocus, Mathf.RoundToInt(abilities.Focus), p.Intellect,
                p.Attack, p.Defense, p.Magic, p.Speed, p.Food, session.Honor, p.Level, session.Gold, keeper.Souls };
            for (int i = 0; i < fields.Length; i++) fields[i].SetTextWithoutNotify(values[i].ToString());
            for (int i = 0; i < unlockButtons.Length; i++)
            {
                bool unlocked = keeper.IsClassUnlocked((CharacterClass)(i + 4));
                unlockButtons[i].interactable = !unlocked;
                unlockLabels[i].text = unlocked ? InventoryText.Unlocked : InventoryText.Unlock;
            }
        }

        public void Apply()
        {
            var values = new int[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                if (!int.TryParse(fields[i].text, out values[i])) { message.text = InventoryText.InvalidNumber; return; }
            PlayerProgression.Instance.ApplyDebug(new DebugStats
            {
                MaxHealth = values[0], Health = values[1], MaxFocus = values[2], Focus = values[3], Intellect = values[4],
                Attack = values[5], Defense = values[6], Magic = values[7], Speed = values[8], Food = values[9], Honor = values[10],
                Level = values[11], Gold = values[12], Souls = values[13]
            });
            keeper.Save();
            Refresh();
            message.text = InventoryText.DebugApplied;
        }

        public void Unlock(int index)
        {
            if (index < 0 || index > 1) return;
            keeper.UnlockClass((CharacterClass)(index + 4));
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SetVisible(false);
            Action callback = closed;
            closed = null;
            callback?.Invoke();
        }
        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
