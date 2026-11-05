using System;
using System.Collections;
using AshenRealm.Combat;
using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.Interactions
{
    /// <summary>
    /// One-shot lootable chest: swings its lid open, heals the opener and posts a reward notification.
    /// </summary>
    public sealed class TreasureChest : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform lid;
        [Tooltip("Lid rotation about its hinge (local X) when fully open.")]
        [SerializeField] float openAngle = 110f;
        [SerializeField] float openDuration = 0.7f;
        [SerializeField] string prompt = GameText.OpenChest;
        [SerializeField, TextArea] string rewardMessage = GameText.ChestReward;
        [SerializeField] float healAmount = 40f;
        [SerializeField, Optional] Light rewardGlow;

        bool opened;

        public event Action Opened;

        public bool IsOpen => opened;
        public string InteractionPrompt => opened ? string.Empty : prompt;

        void Awake()
        {
            if (rewardGlow != null) rewardGlow.enabled = false;
        }

        public bool CanInteract(GameObject interactor) => !opened;

        public void Interact(GameObject interactor)
        {
            if (opened) return;
            opened = true;

            var health = interactor != null ? interactor.GetComponentInParent<Health>() : null;
            if (health != null && healAmount > 0f) health.Heal(healAmount);

            GameMessages.Notify(rewardMessage);
            StartCoroutine(OpenRoutine());
            Opened?.Invoke();
        }

        IEnumerator OpenRoutine()
        {
            if (rewardGlow != null) rewardGlow.enabled = true;
            Quaternion closed = lid.localRotation;
            Quaternion open = closed * Quaternion.Euler(openAngle, 0f, 0f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / openDuration)
            {
                // Overshoot slightly, like a heavy lid thrown back on its hinges.
                float k = 1f - Mathf.Pow(1f - t, 3f);
                k += Mathf.Sin(t * Mathf.PI) * 0.08f;
                lid.localRotation = Quaternion.SlerpUnclamped(closed, open, k);
                if (rewardGlow != null) rewardGlow.intensity = Mathf.Lerp(0f, 1.6f, t);
                yield return null;
            }
            lid.localRotation = open;
        }
    }
}
