using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.AI
{
    /// <summary>Small breathing and attentive turns on the visible figure, leaving its interaction collider still.</summary>
    public sealed class NpcIdleAnimator : MonoBehaviour
    {
        [SerializeField] Transform model;
        [SerializeField, Optional] Transform hammerArm;
        [SerializeField, Optional] ParticleSystem sparks;
        [SerializeField] float phase;

        Vector3 restPosition, restScale;
        Quaternion restRotation, armRest;
        float elapsed;
        int lastStrike = -1;

        public bool Working => hammerArm != null;

        void Awake()
        {
            restPosition = model.localPosition;
            restScale = model.localScale;
            restRotation = model.localRotation;
            if (hammerArm != null) armRest = hammerArm.localRotation;
        }

        void LateUpdate()
        {
            elapsed += Time.deltaTime;
            float t = elapsed + phase;
            float breathe = Mathf.Sin(t * 1.7f);
            model.localScale = Vector3.Scale(restScale, new Vector3(1f + breathe * 0.003f, 1f + breathe * 0.005f, 1f));
            model.localPosition = restPosition + Vector3.up * (breathe * 0.003f);
            model.localRotation = restRotation * Quaternion.Euler(Mathf.Sin(t * 0.8f) * 0.6f,
                Mathf.Sin(t * 0.47f) * (Working ? 0.8f : 2.5f), Mathf.Sin(t * 0.6f) * 0.45f);
            if (hammerArm == null) return;

            // A slow lift, a quick blow, then time for the tongs to settle.
            const float period = 1.8f;
            float cycle = Mathf.Repeat(elapsed, period);
            float angle = cycle < 0.95f ? Mathf.Lerp(18f, -60f, Mathf.SmoothStep(0f, 1f, cycle / 0.95f)) :
                cycle < 1.12f ? Mathf.Lerp(-60f, 18f, Mathf.Pow((cycle - 0.95f) / 0.17f, 2f)) : 18f;
            hammerArm.localRotation = armRest * Quaternion.Euler(angle, 0f, 0f);
            int strike = Mathf.FloorToInt((elapsed - 1.12f) / period);
            if (elapsed >= 1.12f && strike != lastStrike)
            {
                lastStrike = strike;
                if (sparks != null) sparks.Emit(14);
            }
        }
    }
}
