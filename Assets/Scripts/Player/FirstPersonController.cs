using System;
using AshenRealm.Input;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>
    /// CharacterController-based first-person locomotion: walk, sprint (stamina), jump, gravity and camera look.
    /// Yaw rotates this transform; pitch rotates <see cref="cameraPivot"/>. In the saddle (<see cref="Mount"/>) the same loop
    /// runs with the horse's paces, body and eye, and sideways input turns instead of strafing.
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

        /// <summary>The horse's numbers, applied while <see cref="Mounted"/>.</summary>
        [Serializable]
        public struct Saddle
        {
            public float Canter, Gallop, GallopStaminaPerSecond, TurnDegreesPerSecond;
            public float EyeHeight, Height, Radius, StepOffset;
            [Tooltip("How far the muzzle and the rump reach beyond the rider: the horse stops before they touch anything.")]
            public float HeadReach, TailReach;
            [Tooltip("Step cycle per metre: a horse's strides are longer than a person's.")]
            public float StepScale;
        }

        CharacterController controller;
        IPlayerInput input;
        Vector3 planarVelocity;
        float verticalVelocity;
        float yaw, pitch;
        float lastGroundedTime = -999f;
        bool wasGrounded;
        float stepCycle;
        bool mounted;
        Saddle saddle;
        float footHeight, footRadius, footStep, footEye;
        Vector3 footCenter;
        readonly RaycastHit[] probeHits = new RaycastHit[16];

        public event Action Jumped;
        /// <summary>Raised on touchdown with the downward speed at impact.</summary>
        public event Action<float> Landed;

        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool InputEnabled { get; set; } = true;
        /// <summary>External speed scaling (blocking, attacking).</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        /// <summary>A stance that forbids running (blocking, swinging, stunned). Kept apart from the speed scale on purpose:
        /// a class or gear pace below 1 still runs, it only runs at its own pace.</summary>
        public bool SprintBlocked { get; set; }
        public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;
        /// <summary>Planar speed relative to walk speed (0 = still, 1 = walking, ~1.75 = sprinting).</summary>
        public float NormalizedSpeed => planarVelocity.magnitude / Mathf.Max(0.01f, mounted ? saddle.Canter : walkSpeed);
        /// <summary>Distance-driven step phase, used by camera bob and weapon sway.</summary>
        public float StepCycle => stepCycle;
        /// <summary>Look rotation applied this frame in degrees (yaw, pitch).</summary>
        public Vector2 LastLookDelta { get; private set; }
        public float Pitch => pitch;
        public IPlayerInput InputSource => input;
        public bool Mounted => mounted;

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
            if (mounted)
            {
                Turn(move.x * saddle.TurnDegreesPerSecond * dt);
                move.x = 0f;
            }
            bool hasMove = move.sqrMagnitude > 0.01f;

            bool wantsSprint = InputEnabled && input.SprintHeld && hasMove && move.y > -0.2f;
            IsSprinting = wantsSprint && stamina != null && !stamina.IsExhausted && !SprintBlocked;
            if (IsSprinting)
            {
                stamina.Drain((mounted ? saddle.GallopStaminaPerSecond : sprintStaminaPerSecond) * dt);
                if (stamina.IsExhausted)
                {
                    IsSprinting = false;
                    input.CancelSprint();
                }
            }

            // In the saddle the pace is the horse's: combat's stance and class pace (SpeedMultiplier) belong to the rider.
            float speed = mounted ? (IsSprinting ? saddle.Gallop : saddle.Canter) : (IsSprinting ? sprintSpeed : walkSpeed) * SpeedMultiplier;
            if (move.y < 0f) speed *= Mathf.Lerp(1f, backwardSpeedMultiplier, -move.y);
            Vector3 wish = (transform.right * move.x + transform.forward * move.y) * speed;

            float accel = IsGrounded ? groundAcceleration : airAcceleration;
            planarVelocity = Vector3.Lerp(planarVelocity, wish, 1f - Mathf.Exp(-accel * dt));
            if (mounted) Rein(dt);

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

            if (IsGrounded) stepCycle += planarVelocity.magnitude * dt * (mounted ? saddle.StepScale : 1f);
        }

        /// <summary>Takes the saddle: the horse's paces, a taller and wider body and the eye above the horse's head.</summary>
        public void Mount(Saddle numbers)
        {
            if (!mounted)
            {
                footHeight = controller.height;
                footRadius = controller.radius;
                footStep = controller.stepOffset;
                footCenter = controller.center;
                footEye = cameraPivot.localPosition.y;
            }
            mounted = true;
            saddle = numbers;
            controller.height = numbers.Height;
            controller.radius = numbers.Radius;
            controller.center = Vector3.up * (numbers.Height * 0.5f);
            controller.stepOffset = numbers.StepOffset;
            SetEye(numbers.EyeHeight);
            planarVelocity = Vector3.zero;
        }

        /// <summary>Back on foot with exactly the body and eye there were before <see cref="Mount"/>.</summary>
        public void Dismount()
        {
            if (!mounted) return;
            mounted = false;
            controller.stepOffset = footStep;
            controller.radius = footRadius;
            controller.height = footHeight;
            controller.center = footCenter;
            SetEye(footEye);
            planarVelocity = Vector3.zero;
        }

        void SetEye(float height)
        {
            Vector3 eye = cameraPivot.localPosition;
            eye.y = height;
            cameraPivot.localPosition = eye;
        }

        /// <summary>Turns the horse, unless the turn would swing its head further into something.</summary>
        void Turn(float degrees)
        {
            if (Mathf.Abs(degrees) < 0.0001f) return;
            Vector3 turned = Quaternion.Euler(0f, degrees, 0f) * transform.forward;
            if (Room(turned, saddle.HeadReach, 0.05f) < Mathf.Min(0f, Room(transform.forward, saddle.HeadReach, 0.05f))) return;
            yaw = Mathf.Repeat(yaw + degrees, 360f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>
        /// The capsule holds only the rider and the horse's middle, so the head and the rump look ahead along the way: the horse
        /// slows as its muzzle nears a wall or a tree and stops before it touches.
        /// </summary>
        void Rein(float dt)
        {
            float along = Vector3.Dot(planarVelocity, transform.forward);
            if (Mathf.Abs(along) < 0.01f) return;
            Vector3 direction = transform.forward * Mathf.Sign(along);
            float gap = Room(direction, along > 0f ? saddle.HeadReach : saddle.TailReach, Mathf.Abs(along) * 0.5f + 0.5f);
            float allowed = Mathf.Max(0f, Mathf.Min(gap * 4f, gap / dt));
            if (Mathf.Abs(along) > allowed) planarVelocity -= direction * (Mathf.Abs(along) - allowed);
        }

        /// <summary>
        /// Free distance before a body part reaching <paramref name="reach"/> ahead meets something; negative once it is closer
        /// than that. Walkable ground is not in the way, nor the rider and the horse themselves.
        /// </summary>
        float Room(Vector3 direction, float reach, float lookAhead)
        {
            const float Radius = 0.3f;
            Vector3 feet = transform.position;
            int count = Physics.CapsuleCastNonAlloc(feet + Vector3.up * 0.9f, feet + Vector3.up * 1.9f, Radius, direction, probeHits,
                reach - Radius + lookAhead, groundMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = probeHits[i];
                if (hit.distance <= 0f || hit.normal.y > 0.6f || hit.collider.transform.IsChildOf(transform)) continue;
                nearest = Mathf.Min(nearest, hit.distance - (reach - Radius));
            }
            return nearest;
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
