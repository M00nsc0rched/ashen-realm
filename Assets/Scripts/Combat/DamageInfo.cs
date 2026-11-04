using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>
    /// Describes a single damage event. Direction points from the attacker toward the victim.
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly GameObject Source;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            Amount = amount;
            Point = point;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            Source = source;
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>Applies damage and returns the amount actually taken after mitigation.</summary>
        float ApplyDamage(in DamageInfo info);
    }

    /// <summary>
    /// Components on the same GameObject as a <see cref="Health"/> can mitigate incoming damage (e.g. shield block).
    /// </summary>
    public interface IDamageFilter
    {
        float FilterDamage(in DamageInfo info, float amount);
    }
}
