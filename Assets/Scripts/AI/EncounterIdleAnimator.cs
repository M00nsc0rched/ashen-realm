using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.AI
{
    public enum EncounterMotion { DrawingWater, Begging, Tending, Rowing, Stoking, Chanting, Wolf, Candle }

    /// <summary>Acts on the person and tools only; the encounter's interaction capsule stays anchored.</summary>
    public sealed class EncounterIdleAnimator : MonoBehaviour
    {
        [SerializeField] EncounterMotion motion;
        [SerializeField] Transform body;
        [SerializeField, Optional] Transform workPivot;
        [SerializeField, Optional] Transform secondaryPivot;
        [SerializeField] float phase;

        Vector3 bodyScale, bodyPosition, workPosition, workScale;
        Quaternion bodyRotation, workRotation, secondaryRotation;
        float elapsed;

        public EncounterMotion Motion => motion;
        public bool Working => workPivot != null;

        void Awake()
        {
            bodyScale = body.localScale;
            bodyPosition = body.localPosition;
            bodyRotation = body.localRotation;
            if (workPivot != null)
            {
                workRotation = workPivot.localRotation;
                workPosition = workPivot.localPosition;
                workScale = workPivot.localScale;
            }
            if (secondaryPivot != null) secondaryRotation = secondaryPivot.localRotation;
        }

        void LateUpdate()
        {
            elapsed += Time.deltaTime;
            float t = elapsed + phase;
            if (motion == EncounterMotion.Candle)
            {
                if (workPivot != null)
                    workPivot.localScale = Vector3.Scale(workScale, new Vector3(1f + Mathf.Sin(t * 11f) * 0.12f,
                        0.9f + Mathf.Sin(t * 7.3f) * 0.18f, 1f + Mathf.Sin(t * 8.7f) * 0.1f));
                return;
            }
            body.localScale = Vector3.Scale(bodyScale, new Vector3(1f + Mathf.Sin(t * 1.8f) * 0.003f,
                1f + Mathf.Sin(t * 1.8f) * 0.006f, 1f));
            body.localPosition = bodyPosition + Vector3.up * (Mathf.Sin(t * 1.8f) * 0.005f);
            body.localRotation = bodyRotation * Quaternion.Euler(0f, Mathf.Sin(t * 0.43f) * (motion == EncounterMotion.Wolf ? 1.5f : 3f), 0f);
            if (workPivot == null) return;
            float wave = Mathf.Sin(t * (motion == EncounterMotion.Rowing ? 1.25f : 1.7f));
            Vector3 action = motion switch
            {
                EncounterMotion.DrawingWater => new Vector3(-22f + wave * 19f, wave * 5f, -5f),
                EncounterMotion.Begging => new Vector3(-12f + wave * 9f, wave * 7f, -8f),
                EncounterMotion.Tending => new Vector3(-18f + wave * 16f, wave * 12f, -4f),
                EncounterMotion.Rowing => new Vector3(wave * 24f, wave * 11f, 8f),
                EncounterMotion.Stoking => new Vector3(-16f + wave * 21f, wave * 8f, 0f),
                EncounterMotion.Chanting => new Vector3(-15f + wave * 10f, 0f, wave * 12f),
                _ => new Vector3(Mathf.Sin(t * 0.9f) * 7f, Mathf.Sin(t * 0.57f) * 15f, -3f)
            };
            workPivot.localRotation = workRotation * Quaternion.Euler(action);
            if (motion == EncounterMotion.DrawingWater)
                workPivot.localPosition = workPosition + Vector3.up * ((wave + 1f) * 0.07f);
            if (secondaryPivot != null)
                secondaryPivot.localRotation = secondaryRotation * Quaternion.Euler(0f, Mathf.Sin(t * 2.3f) * 18f, Mathf.Sin(t * 1.6f) * 5f);
        }
    }
}
