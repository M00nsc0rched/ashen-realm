using System;
using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>
    /// Reusable hit-point pool. Damage passes through any <see cref="IDamageFilter"/> on the same GameObject.
    /// </summary>
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] float maxHealth = 100f;

        float current;
        bool initialized;
        IDamageFilter[] filters;

        /// <summary>Raised after damage is applied: (info, amount actually taken).</summary>
        public event Action<DamageInfo, float> Damaged;
        public event Action<Health> Died;
        /// <summary>Raised whenever the value changes: (current, max).</summary>
        public event Action<float, float> Changed;

        public float Max => maxHealth;
        public float Current { get { EnsureInitialized(); return current; } }
        public float Normalized => Current / maxHealth;
        public bool IsAlive => Current > 0f;

        void Awake() => EnsureInitialized();

        void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            current = maxHealth;
            filters = GetComponents<IDamageFilter>();
        }

        public void SetMax(float value, bool refill)
        {
            EnsureInitialized();
            maxHealth = Mathf.Max(1f, value);
            current = refill ? maxHealth : Mathf.Min(current, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }

        public float ApplyDamage(in DamageInfo info)
        {
            EnsureInitialized();
            if (current <= 0f || info.Amount <= 0f) return 0f;

            float amount = info.Amount;
            for (int i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                if (filter is Behaviour b && !b.enabled) continue;
                amount = filter.FilterDamage(info, amount);
            }

            amount = Mathf.Clamp(amount, 0f, current);
            if (amount <= 0f) return 0f;

            current -= amount;
            Damaged?.Invoke(info, amount);
            Changed?.Invoke(current, maxHealth);
            if (current <= 0f)
            {
                current = 0f;
                Died?.Invoke(this);
            }
            return amount;
        }

        public void Heal(float amount)
        {
            EnsureInitialized();
            if (current <= 0f || amount <= 0f) return;
            current = Mathf.Min(maxHealth, current + amount);
            Changed?.Invoke(current, maxHealth);
        }

        public void ResetHealth()
        {
            EnsureInitialized();
            current = maxHealth;
            Changed?.Invoke(current, maxHealth);
        }
    }
}
