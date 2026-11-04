using System;
using AshenRealm.Input;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>
    /// CharacterController-based first-person locomotion: walk, sprint (stamina), jump, gravity and camera look.
    /// Yaw rotates this transform; pitch rotates <see cref="cameraPivot"/>.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerInputHub inputSource;
        [SerializeField] Transform cameraPivot;
        [SerializeField] Stamina stamina;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 4.2f;
        [SerializeField] float sprintSpeed = 7.4f;
        [SerializeField, Range(0.3f, 1f)] float backwardSpeedMultiplier = 0.7f;
        [SerializeField] float groundAcceleration = 12f;
        [SerializeField] float airAcceleration = 3f;
        [SerializeField] float jumpHeight = 1.1f;
        [SerializeField] float gravity = -22f;
        [SerializeField] float coyoteTime = 0.12f;

        [Header("Stamina")]
        [SerializeField] float sprintStaminaPerSecond = 18f;
        [SerializeField] float jumpStaminaCost = 10f;

        [Header("Look")]
        [SerializeField] float minPitch = -80f;
        [SerializeField] float maxPitch = 80f;

        [Header("Ground Check")]
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] float groundProbeDistance = 0.2f;

        CharacterController controller;
        IPlayerInput input;
        Vector3 planarVelocity;
        float verticalVelocity;
        float yaw, pitch;
        float lastGroundedTime = -999f;
        bool wasGrounded;
        float stepCycle;

        public event Action Jumped;
        /// <summary>Raised on touchdown with the downward speed at impact.</summary>
        public event Action<float> Landed;

        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool InputEnabled { get; set; } = true;
        /// <summary>External speed scaling (blocking, attacking).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;
        /// <summary>Planar speed relative to walk speed (0 = still, 1 = walking, ~1.75 = sprinting).</summary>
        public float NormalizedSpeed => planarVelocity.magnitude / Mathf.Max(0.01f, walkSpeed);
        /// <summary>Distance-driven step phase, used by camera bob and weapon sway.</summary>
        public float StepCycle => stepCycle;
        /// <summary>Look rotation applied this frame in degrees (yaw, pitch).</summary>
        public Vector2 LastLookDelta { get; private set; }
        public float Pitch => pitch;
        public IPlayerInput InputSource => input;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = inputSource;
            yaw = transform.eulerAngles.y;
            if (cameraPivot != null) pitch = NormalizePitch(cameraPivot.localEulerAngles.x);
        }

        static float NormalizePitch(float angle) => angle > 180f ? angle - 360f : angle;

        void Update()
        {
            if (input == null) return;
            float dt = Time.deltaTime;

            if (InputEnabled) UpdateLook();
            else LastLookDelta = Vector2.zero;

            UpdateGrounded();
            UpdateMovement(dt);
        }

        void UpdateLook()
        {
            Vector2 look = input.ConsumeLook();
            LastLookDelta = look;
            yaw = Mathf.Repeat(yaw + look.x, 360f);
            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void UpdateGrounded()
        {
            bool grounded = controller.isGrounded;
            if (!grounded && verticalVelocity <= 0f)
            {
                // CharacterController.isGrounded flickers on uneven terrain; confirm with a short sphere probe.
                float radius = controller.radius * 0.9f;
                Vector3 origin = transform.position + controller.center - Vector3.up * (controller.height * 0.5f - controller.radius);
                grounded = Physics.SphereCast(origin + Vector3.up * 0.05f, radius, Vector3.down, out _,
                    groundProbeDistance + 0.05f, groundMask, QueryTriggerInteraction.Ignore);
            }

            IsGrounded = grounded;
            if (grounded) lastGroundedTime = Time.time;

            if (grounded && !wasGrounded && verticalVelocity < -2f)
                Landed?.Invoke(-verticalVelocity);
            wasGrounded = grounded;
        }

        void UpdateMovement(float dt)
        {
            Vector2 move = InputEnabled ? input.Move : Vector2.zero;
            bool hasMove = move.sqrMagnitude > 0.01f;

            bool wantsSprint = InputEnabled && input.SprintHeld && hasMove && move.y > -0.2f;
            IsSprinting = wantsSprint && stamina != null && !stamina.IsExhausted && SpeedMultiplier >= 0.99f;
            if (IsSprinting)
            {
                stamina.Drain(sprintStaminaPerSecond * dt);
                if (stamina.IsExhausted)
                {
                    IsSprinting = false;
                    input.CancelSprint();
                }
            }

            float speed = (IsSprinting ? sprintSpeed : walkSpeed) * SpeedMultiplier;
            if (move.y < 0f) speed *= Mathf.Lerp(1f, backwardSpeedMultiplier, -move.y);
            Vector3 wish = (transform.right * move.x + transform.forward * move.y) * speed;

            float accel = IsGrounded ? groundAcceleration : airAcceleration;
            planarVelocity = Vector3.Lerp(planarVelocity, wish, 1f - Mathf.Exp(-accel * dt));

            if (IsGrounded && verticalVelocity < 0f) verticalVelocity = -2f;

            bool canJump = InputEnabled && Time.time - lastGroundedTime <= coyoteTime && verticalVelocity <= 0f;
            if (canJump && input.ConsumeJump())
            {
                if (stamina == null || stamina.TrySpend(jumpStaminaCost))
                {
                    verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
                    lastGroundedTime = -999f;
                    Jumped?.Invoke();
                }
            }

            verticalVelocity += gravity * dt;

            CollisionFlags flags = controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            if (IsGrounded) stepCycle += planarVelocity.magnitude * dt;
        }

        /// <summary>Moves the player instantly (respawn, kill plane, scripted tests). Positive pitch looks down.</summary>
        public void Teleport(Vector3 position, float newYaw, float newPitch = 0f)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }
    }
}
