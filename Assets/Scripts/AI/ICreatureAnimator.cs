using UnityEngine;

namespace AshenRealm.AI
{
    /// <summary>Purely visual; timing comes from the AI. Implemented by each creature rig.</summary>
    public interface ICreatureAnimator
    {
        void SetLocomotion(float normalizedSpeed);
        void PlayAttack(float windup, float recovery);
        void PlayRecoil();
        void PlayHit(Vector3 worldDirection);
        void PlayDeath();
    }
}
