using System;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>
    /// Regenerating stamina pool. Hitting zero sets an exhausted state that blocks sprinting
    /// until stamina recovers past <see cref="exhaustionRecoveryThreshold"/>.
    /// </summary>
    public sealed class Stamina : MonoBehaviour
    {
        [SerializeField, Min(1f)] float maxStamina = 100f;
        [SerializeField] float regenPerSecond = 24f;
        [Tooltip("Seconds after spending stamina before regeneration starts.")]
        [SerializeField] float regenDelay = 0.8f;
        [SerializeField] float exhaustionRecoveryThreshold = 25f;

        float current;
        bool initialized;
        float lastSpendTime = -999f;
        bool exhausted;

        /// <summary>(current, max)</summary>
        public event Action<float, float> Changed;

        public float Max => maxStamina;
        public float Current { get { EnsureInitialized(); return current; } }
        public float Normalized => Current / maxStamina;
        public bool IsExhausted => exhausted;

        void Awake() => EnsureInitialized();

        void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            current = maxStamina;
        }

        /// <summary>Spends the full amount only if available.</summary>
        public bool TrySpend(float amount)
        {
            EnsureInitialized();
            if (amount <= 0f) return true;
            if (current < amount) return false;
            Spend(amount);
            return true;
        }

        /// <summary>Spends up to the requested amount (clamped at zero). Returns the amount actually spent.</summary>
        public float Drain(float amount)
        {
            EnsureInitialized();
            if (amount <= 0f) return 0f;
            float spent = Mathf.Min(current, amount);
            Spend(spent);
            return spent;
        }

        void Spend(float amount)
        {
            current -= amount;
            lastSpendTime = Time.time;
            if (current <= 0.001f)
            {
                current = 0f;
                exhausted = true;
            }
            Changed?.Invoke(current, maxStamina);
        }

        public void Refill()
        {
            EnsureInitialized();
            current = maxStamina;
            exhausted = false;
            Changed?.Invoke(current, maxStamina);
        }

        void Update()
        {
            if (current >= maxStamina || Time.time - lastSpendTime < regenDelay) return;
            current = Mathf.Min(maxStamina, current + regenPerSecond * Time.deltaTime);
            if (exhausted && current >= exhaustionRecoveryThreshold) exhausted = false;
            Changed?.Invoke(current, maxStamina);
        }
    }
}
