using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.AI
{
    public enum NpcRoutine { Attentive, Trading, Reading, Praying, Seated, Watching }

    /// <summary>Visible work and attentive poses on the figure; feet and the interaction collider remain anchored.</summary>
    public sealed class NpcIdleAnimator : MonoBehaviour
    {
        [SerializeField] Transform model;
        [SerializeField, Optional] Transform hammerArm;
        [SerializeField, Optional] Transform gestureArm;
        [SerializeField, Optional] ParticleSystem sparks;
        [SerializeField] float phase;
        [SerializeField] NpcRoutine routine;

        Vector3 restPosition, restScale;
        Quaternion restRotation, armRest, gestureRest;
        float elapsed;
        int lastStrike = -1;

        public bool Working => hammerArm != null || gestureArm != null;
        public NpcRoutine Routine => routine;

        void Awake()
        {
            restPosition = model.localPosition;
            restScale = model.localScale;
            restRotation = model.localRotation;
            if (hammerArm != null) armRest = hammerArm.localRotation;
            if (gestureArm != null) gestureRest = gestureArm.localRotation;
        }

        void LateUpdate()
        {
            elapsed += Time.deltaTime;
            float t = elapsed + phase;
            float breathe = Mathf.Sin(t * 1.7f);
            model.localScale = Vector3.Scale(restScale, new Vector3(1f + breathe * 0.003f, 1f + breathe * 0.005f, 1f));
            float turn = routine == NpcRoutine.Watching ? 13f : routine == NpcRoutine.Attentive ? 8f : 4f;
            float lean = routine == NpcRoutine.Reading ? 5f : routine == NpcRoutine.Praying ? 4f : 1.6f;
            if (hammerArm != null) { turn = 0.8f; lean = 0.6f; }
            model.localPosition = restPosition + Vector3.up * (breathe * 0.007f);
            model.localRotation = restRotation * Quaternion.Euler(Mathf.Sin(t * 0.8f) * lean,
                Mathf.Sin(t * 0.47f) * turn, Mathf.Sin(t * 0.6f) * 0.65f);
            if (gestureArm != null)
            {
                // Pick a coin off the counter, hold it to the light, then count it onto the open ledger.
                float workCycle = Mathf.Repeat(t, 4.8f);
                float lift = workCycle < 1.5f ? Mathf.SmoothStep(0f, 1f, workCycle / 1.5f) : workCycle < 2.5f ? 1f :
                    workCycle < 4.1f ? 1f - Mathf.SmoothStep(0f, 1f, (workCycle - 2.5f) / 1.6f) : 0f;
                gestureArm.localRotation = gestureRest * Quaternion.Euler(-36f * lift, -18f * lift, -8f * lift);
            }
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
