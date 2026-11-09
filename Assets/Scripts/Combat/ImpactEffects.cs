using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>
    /// Single shared particle system that emits short spark/dust bursts at impact points.
    /// One system for all impacts keeps draw calls and allocations flat.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ImpactEffects : MonoBehaviour
    {
        public enum Kind { Flesh, Metal, Stone }

        [SerializeField] Color fleshColor = new Color(0.55f, 0.08f, 0.06f);
        [SerializeField] Color metalColor = new Color(1f, 0.82f, 0.45f);
        [SerializeField] Color stoneColor = new Color(0.62f, 0.58f, 0.52f);

        ParticleSystem system;

        void Awake() => system = GetComponent<ParticleSystem>();

        public void Emit(Vector3 position, Vector3 normal, Kind kind)
        {
            if (system == null) return;
            Color color = kind == Kind.Flesh ? fleshColor : kind == Kind.Metal ? metalColor : stoneColor;
            int count = kind == Kind.Metal ? 14 : 10;
            if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
            normal.Normalize();

            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false, startColor = color };
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = (normal + Random.insideUnitSphere * 0.9f).normalized;
                emit.position = position;
                emit.velocity = dir * Random.Range(1.5f, kind == Kind.Metal ? 6f : 3.5f);
                emit.startSize = Random.Range(0.03f, kind == Kind.Stone ? 0.09f : 0.06f);
                emit.startLifetime = Random.Range(0.2f, 0.45f);
                system.Emit(emit, 1);
            }
        }

        /// <summary>Short coloured spell traces share the existing impact renderer and particle budget.</summary>
        public void EmitAbility(Vector3 centre, Color color, float radius)
        {
            if (system == null) return;
            int count = radius > 2f ? 28 : 14;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false, startColor = color };
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                emit.position = centre + outward * radius;
                emit.velocity = outward * 0.25f + Vector3.up * 0.35f;
                emit.startSize = 0.075f;
                emit.startLifetime = 0.8f;
                system.Emit(emit, 1);
            }
        }

        public void EmitAbilityRay(Vector3 from, Vector3 to, Color color)
        {
            if (system == null) return;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false, startColor = color };
            int count = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(from, to) * 3f), 8, 36);
            for (int i = 0; i < count; i++)
            {
                emit.position = Vector3.Lerp(from, to, i / (float)(count - 1));
                emit.velocity = Vector3.up * 0.08f;
                emit.startSize = 0.055f;
                emit.startLifetime = 0.3f;
                system.Emit(emit, 1);
            }
        }
    }
}
