using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>
    /// Code-driven first-person animation for the sword and shield pivots: idle breathing, movement bob,
    /// look sway, slash keyframes, block pose and impact kicks.
    /// Gameplay timing lives in PlayerCombat; this component is purely visual, so the pivots can later
    /// host animated models without touching combat code.
    /// </summary>
    public sealed class FirstPersonWeaponAnimator : MonoBehaviour
    {
        // Normalized times of the slash phases. PlayerCombat applies the hit inside the strike window.
        public const float WindupEnd = 0.3f;
        public const float StrikeEnd = 0.52f;

        struct SlashPose
        {
            public Vector3 Position;
            public Vector3 Euler;
            public SlashPose(Vector3 position, Vector3 euler) { Position = position; Euler = euler; }
        }

        // Offsets in camera space relative to the rest pose: [variant][windup, strike end].
        static readonly SlashPose[][] SlashKeys =
        {
            new[] // top-right to bottom-left
            {
                new SlashPose(new Vector3(0.12f, 0.12f, -0.06f), new Vector3(-35f, 15f, -40f)),
                new SlashPose(new Vector3(-0.30f, -0.12f, 0.14f), new Vector3(70f, -30f, 70f)),
            },
            new[] // left to right backhand
            {
                new SlashPose(new Vector3(-0.16f, 0.06f, -0.02f), new Vector3(-15f, -20f, 55f)),
                new SlashPose(new Vector3(0.20f, -0.10f, 0.16f), new Vector3(60f, 35f, -75f)),
            },
        };

        [SerializeField] FirstPersonController controller;
        [SerializeField] Transform swordPivot;
        [SerializeField] Transform shieldPivot;
        [SerializeField, Optional] TrailRenderer swordTrail;

        [Header("Sway")]
        [SerializeField] float lookSwayPerDegreePerSecond = 0.025f;
        [SerializeField] float maxLookSway = 7f;
        [SerializeField] float swaySmoothing = 9f;

        [Header("Bob")]
        [SerializeField] float bobHorizontal = 0.014f;
        [SerializeField] float bobVertical = 0.02f;
        [SerializeField] float strideLength = 2.6f;

        [Header("Block Pose (camera space)")]
        [SerializeField] Vector3 shieldBlockPosition = new Vector3(-0.1f, -0.2f, 0.42f);
        [SerializeField] Vector3 shieldBlockEuler = new Vector3(-5f, 30f, 0f);
        [SerializeField] Vector3 swordBlockOffset = new Vector3(0.06f, -0.12f, -0.06f);
        [SerializeField] Vector3 swordBlockEuler = new Vector3(25f, 0f, -20f);
        [SerializeField] float blockBlendSpeed = 12f;

        Vector3 swordRestPos, shieldRestPos;
        Quaternion swordRestRot, shieldRestRot;

        float slashTime = -1f, slashDuration = 0.5f;
        int slashVariant;
        float hitStopTimer;
        bool blocking;
        bool putAway;
        float blockWeight, sprintWeight, bobWeight, lowerWeight;
        Vector3 swayEuler;
        Vector3 swordKickPos, swordKickVel, shieldKickPos, shieldKickVel;

        public bool IsSlashing => slashTime >= 0f;

        void Awake()
        {
            swordRestPos = swordPivot.localPosition;
            swordRestRot = swordPivot.localRotation;
            shieldRestPos = shieldPivot.localPosition;
            shieldRestRot = shieldPivot.localRotation;
            if (swordTrail != null) swordTrail.emitting = false;
        }

        public void PlaySlash(int variant, float duration)
        {
            slashVariant = Mathf.Abs(variant) % SlashKeys.Length;
            slashDuration = Mathf.Max(0.1f, duration);
            slashTime = 0f;
            hitStopTimer = 0f;
        }

        public void SetBlocking(bool value) => blocking = value;

        /// <summary>Under a roof the weapons go down out of sight, and stay there until the player leaves.</summary>
        public void SetPutAway(bool value) => putAway = value;

        /// <summary>Sword connected with a body: brief hit-stop and recoil.</summary>
        public void PlayHitImpact()
        {
            hitStopTimer = 0.07f;
            swordKickVel += new Vector3(0.2f, 0.3f, -0.9f);
        }

        /// <summary>Sword struck solid geometry: stronger bounce back.</summary>
        public void PlayWallImpact()
        {
            hitStopTimer = 0.05f;
            swordKickVel += new Vector3(0.6f, 0.4f, -1.6f);
        }

        /// <summary>An enemy blow landed on the raised shield.</summary>
        public void PlayBlockImpact(float strength)
        {
            shieldKickVel += new Vector3(0.15f, 0.25f, -1.4f) * Mathf.Clamp(strength, 0.3f, 1.5f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            UpdateSlashTime(dt);

            // Look sway lags the weapons behind camera rotation.
            Vector2 lookRate = controller != null && dt > 0f ? controller.LastLookDelta / dt : Vector2.zero;
            Vector3 targetSway = new Vector3(
                Mathf.Clamp(lookRate.y * lookSwayPerDegreePerSecond, -maxLookSway, maxLookSway),
                Mathf.Clamp(-lookRate.x * lookSwayPerDegreePerSecond, -maxLookSway, maxLookSway),
                Mathf.Clamp(-lookRate.x * lookSwayPerDegreePerSecond * 0.6f, -maxLookSway, maxLookSway));
            swayEuler = Vector3.Lerp(swayEuler, targetSway, 1f - Mathf.Exp(-swaySmoothing * dt));

            // Movement bob, sprint carry pose, lowering when control is disabled (death).
            bool grounded = controller == null || controller.IsGrounded;
            float speed = controller != null ? controller.NormalizedSpeed : 0f;
            bobWeight = Mathf.MoveTowards(bobWeight, grounded ? Mathf.Clamp(speed, 0f, 1.6f) : 0f, dt * 5f);
            sprintWeight = Mathf.MoveTowards(sprintWeight, controller != null && controller.IsSprinting && !IsSlashing ? 1f : 0f, dt * 5f);
            lowerWeight = Mathf.MoveTowards(lowerWeight, putAway || (controller != null && !controller.InputEnabled) ? 1f : 0f, dt * 2.5f);
            bool gone = putAway && lowerWeight >= 1f;
            if (swordPivot.gameObject.activeSelf == gone)
            {
                swordPivot.gameObject.SetActive(!gone);
                shieldPivot.gameObject.SetActive(!gone);
            }
            blockWeight = Mathf.MoveTowards(blockWeight, blocking && !IsSlashing ? 1f : 0f, dt * blockBlendSpeed);
            float block = Smooth(blockWeight);

            float phase = controller != null ? controller.StepCycle / strideLength * Mathf.PI * 2f : 0f;
            float breathe = Mathf.Sin(Time.time * 1.7f) * 0.004f;
            Vector3 bob = new Vector3(Mathf.Sin(phase) * bobHorizontal, -Mathf.Abs(Mathf.Cos(phase)) * bobVertical + breathe, 0f) * Mathf.Max(bobWeight, 0.2f);
            float verticalLag = controller != null ? Mathf.Clamp(-controller.Velocity.y * 0.004f, -0.03f, 0.04f) : 0f;
            Vector3 common = bob + new Vector3(0f, verticalLag, 0f) + new Vector3(0f, -0.6f, -0.1f) * Smooth(lowerWeight);

            UpdateSpring(ref swordKickPos, ref swordKickVel, dt);
            UpdateSpring(ref shieldKickPos, ref shieldKickVel, dt);

            // Sword.
            EvaluateSlash(out Vector3 slashPos, out Vector3 slashEuler);
            Vector3 sprintPos = new Vector3(0.04f, -0.06f, -0.02f) * sprintWeight;
            Vector3 sprintEuler = new Vector3(18f, -12f, -10f) * sprintWeight;
            swordPivot.localPosition = swordRestPos + common + slashPos + swordBlockOffset * block + sprintPos + swordKickPos;
            swordPivot.localRotation = Quaternion.Euler(swayEuler + slashEuler + swordBlockEuler * block + sprintEuler
                                                        + new Vector3(swordKickPos.z * 120f, 0f, 0f)) * swordRestRot;

            // Shield.
            Vector3 shieldPos = Vector3.Lerp(shieldRestPos, shieldBlockPosition, block);
            Quaternion shieldRot = Quaternion.Slerp(shieldRestRot, Quaternion.Euler(shieldBlockEuler), block);
            shieldPivot.localPosition = shieldPos + common * 0.8f + shieldKickPos;
            shieldPivot.localRotation = Quaternion.Euler(swayEuler * 0.8f + new Vector3(shieldKickPos.z * 90f, 0f, 0f)) * shieldRot;
        }

        void UpdateSlashTime(float dt)
        {
            if (slashTime < 0f) return;
            if (hitStopTimer > 0f)
            {
                hitStopTimer -= dt;
                return;
            }
            slashTime += dt / slashDuration;
            if (slashTime >= 1f) slashTime = -1f;
            if (swordTrail != null)
                swordTrail.emitting = slashTime > WindupEnd - 0.04f && slashTime < StrikeEnd + 0.04f;
        }

        void EvaluateSlash(out Vector3 position, out Vector3 euler)
        {
            position = Vector3.zero;
            euler = Vector3.zero;
            if (slashTime < 0f) return;

            SlashPose windup = SlashKeys[slashVariant][0];
            SlashPose strike = SlashKeys[slashVariant][1];
            float t = slashTime;
            if (t < WindupEnd)
            {
                float k = EaseOut(t / WindupEnd);
                position = windup.Position * k;
                euler = windup.Euler * k;
            }
            else if (t < StrikeEnd)
            {
                float k = Smooth((t - WindupEnd) / (StrikeEnd - WindupEnd));
                position = Vector3.LerpUnclamped(windup.Position, strike.Position, k);
                euler = Vector3.LerpUnclamped(windup.Euler, strike.Euler, k);
            }
            else
            {
                float k = Smooth((t - StrikeEnd) / (1f - StrikeEnd));
                position = Vector3.Lerp(strike.Position, Vector3.zero, k);
                euler = Vector3.Lerp(strike.Euler, Vector3.zero, k);
            }
        }

        static void UpdateSpring(ref Vector3 position, ref Vector3 velocity, float dt)
        {
            const float frequency = 18f;
            velocity += (-frequency * frequency * position - 2f * 0.6f * frequency * velocity) * dt;
            position += velocity * dt;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }
    }
}
