using AshenRealm.Combat;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>
    /// Procedural camera motion layered on the eye pivot: head bob, trauma-based shake, landing dip and death fall.
    /// Lives on the camera transform, whose parent is the pitch pivot.
    /// </summary>
    public sealed class CameraEffects : MonoBehaviour
    {
        [SerializeField] FirstPersonController controller;
        [SerializeField] Health health;

        [Header("Head Bob")]
        [SerializeField] float bobVertical = 0.04f;
        [SerializeField] float bobHorizontal = 0.025f;
        [Tooltip("Meters travelled per full step cycle (two footsteps).")]
        [SerializeField] float strideLength = 2.6f;

        [Header("Shake")]
        [SerializeField] float maxShakeOffset = 0.08f;
        [SerializeField] float maxShakeAngle = 3.5f;
        [SerializeField] float traumaDecay = 1.8f;

        [Header("Landing")]
        [Tooltip("Downward camera velocity (m/s) added per m/s of landing speed.")]
        [SerializeField] float landingDipPerSpeed = 0.25f;
        [Tooltip("Spring frequency of the landing recovery.")]
        [SerializeField] float landingRecovery = 11f;

        float trauma;
        float bobWeight;
        float landingOffset, landingVelocity;
        float deathBlend;

        void OnEnable()
        {
            if (controller != null) controller.Landed += OnLanded;
            if (health != null) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (controller != null) controller.Landed -= OnLanded;
            if (health != null) health.Damaged -= OnDamaged;
        }

        void OnLanded(float speed) => landingVelocity -= speed * landingDipPerSpeed;

        void OnDamaged(DamageInfo info, float amount) => AddTrauma(Mathf.Clamp01(amount / 25f) * 0.6f + 0.15f);

        public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            // Head bob follows distance travelled so it stays in sync with movement speed.
            float targetBob = controller != null && controller.IsGrounded ? Mathf.Clamp01(controller.NormalizedSpeed) : 0f;
            bobWeight = Mathf.MoveTowards(bobWeight, targetBob, dt * 4f);
            float phase = controller != null ? controller.StepCycle / strideLength * Mathf.PI * 2f : 0f;
            Vector3 bob = new Vector3(Mathf.Sin(phase) * bobHorizontal, -Mathf.Abs(Mathf.Cos(phase)) * bobVertical, 0f) * bobWeight;

            // Critically damped spring for the landing dip.
            float stiffness = landingRecovery * landingRecovery;
            landingVelocity += (-stiffness * landingOffset - 2f * landingRecovery * landingVelocity) * dt;
            landingOffset = Mathf.Clamp(landingOffset + landingVelocity * dt, -0.35f, 0.1f);

            // Shake.
            trauma = Mathf.Max(0f, trauma - traumaDecay * dt);
            float shake = trauma * trauma;
            float t = Time.time * 25f;
            Vector3 shakeOffset = new Vector3(Mathf.PerlinNoise(t, 1.3f) - 0.5f, Mathf.PerlinNoise(2.7f, t) - 0.5f, 0f) * (2f * maxShakeOffset * shake);
            Vector3 shakeEuler = new Vector3(Mathf.PerlinNoise(t, 4.1f) - 0.5f, Mathf.PerlinNoise(5.9f, t) - 0.5f, Mathf.PerlinNoise(t, 7.7f) - 0.5f) * (2f * maxShakeAngle * shake);

            // Death: sink toward the ground and roll.
            bool dead = health != null && !health.IsAlive;
            deathBlend = Mathf.MoveTowards(deathBlend, dead ? 1f : 0f, dt * (dead ? 1.2f : 4f));
            float d = deathBlend * deathBlend * (3f - 2f * deathBlend);
            Vector3 deathOffset = new Vector3(0f, -1.25f * d, 0f);
            Vector3 deathEuler = new Vector3(-10f * d, 0f, 38f * d);

            transform.localPosition = bob + Vector3.up * landingOffset + shakeOffset + deathOffset;
            transform.localRotation = Quaternion.Euler(shakeEuler + deathEuler);
        }
    }
}
