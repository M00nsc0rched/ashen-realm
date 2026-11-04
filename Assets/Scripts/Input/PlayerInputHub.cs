using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshenRealm.Input
{
    /// <summary>
    /// Merges keyboard/mouse, gamepad and on-screen touch controls into one <see cref="IPlayerInput"/>.
    /// Touch UI pushes its state in through the Touch* methods; gameplay only ever reads the interface.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputHub : MonoBehaviour, IPlayerInput
    {
        const string SensitivityPrefKey = "AshenRealm.LookSensitivity";

        [Header("Look")]
        [Tooltip("Degrees of rotation per pixel of mouse movement.")]
        [SerializeField] float mouseDegreesPerPixel = 0.1f;
        [Tooltip("Degrees of rotation for a swipe across the full screen height.")]
        [SerializeField] float touchDegreesPerScreenHeight = 160f;
        [SerializeField] float gamepadDegreesPerSecond = 140f;
        [SerializeField] bool invertY;

        [Header("Buffers (seconds a press stays valid)")]
        [SerializeField] float jumpBuffer = 0.15f;
        [SerializeField] float attackBuffer = 0.3f;
        [SerializeField] float interactBuffer = 0.2f;

        [Header("Touch")]
        [Tooltip("Show touch controls on non-mobile platforms (toggle at runtime with F1).")]
        [SerializeField] bool touchControlsInEditor;
        [Tooltip("Seconds without joystick input before a touch sprint toggle switches off.")]
        [SerializeField] float sprintToggleIdleCancel = 0.35f;

        InputAction moveAction, lookAction, gamepadLookAction, jumpAction, sprintAction, attackAction, blockAction, interactAction;

        Vector2 lookAccumulator;
        Vector2 touchMove;
        bool touchAttackHeld, touchBlockHeld;
        bool sprintToggled;
        float moveIdleTime;
        float jumpPressTime = -1f, attackPressTime = -1f, interactPressTime = -1f;
        bool touchMode;
        float sensitivityMultiplier = 1f;

        public event Action<bool> TouchModeChanged;

        public bool TouchMode => touchMode;
        public bool SprintToggled => sprintToggled;

        /// <summary>Global look sensitivity multiplier (persisted in PlayerPrefs).</summary>
        public float LookSensitivity
        {
            get => sensitivityMultiplier;
            set
            {
                sensitivityMultiplier = Mathf.Clamp(value, 0.1f, 4f);
                PlayerPrefs.SetFloat(SensitivityPrefKey, sensitivityMultiplier);
            }
        }

        public Vector2 Move
        {
            get
            {
                Vector2 move = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
                if (touchMove.sqrMagnitude > move.sqrMagnitude) move = touchMove;
                return Vector2.ClampMagnitude(move, 1f);
            }
        }

        public bool SprintHeld => sprintToggled || (sprintAction != null && sprintAction.IsPressed());
        public bool BlockHeld => touchBlockHeld || (blockAction != null && blockAction.IsPressed() && MouseActionsAllowed);
        public bool AttackHeld => touchAttackHeld || (attackAction != null && attackAction.IsPressed() && MouseActionsAllowed && !mouseAttackSuppressed);

        /// <summary>Mouse buttons drive gameplay only while the cursor is captured (prevents UI clicks from attacking).</summary>
        bool MouseActionsAllowed => touchMode || Cursor.lockState == CursorLockMode.Locked || Application.isMobilePlatform;

        void Awake()
        {
            sensitivityMultiplier = PlayerPrefs.GetFloat(SensitivityPrefKey, 1f);
            CreateActions();
            bool startInTouch = Application.isMobilePlatform || touchControlsInEditor || Touchscreen.current != null;
            SetTouchMode(startInTouch);
        }

        void OnEnable()
        {
            moveAction.Enable(); lookAction.Enable(); gamepadLookAction.Enable(); jumpAction.Enable();
            sprintAction.Enable(); attackAction.Enable(); blockAction.Enable(); interactAction.Enable();
        }

        void OnDisable()
        {
            moveAction.Disable(); lookAction.Disable(); gamepadLookAction.Disable(); jumpAction.Disable();
            sprintAction.Disable(); attackAction.Disable(); blockAction.Disable(); interactAction.Disable();
            ClearTouchState();
            if (!Application.isMobilePlatform) Cursor.lockState = CursorLockMode.None;
        }

        void OnDestroy()
        {
            moveAction?.Dispose(); lookAction?.Dispose(); gamepadLookAction?.Dispose(); jumpAction?.Dispose();
            sprintAction?.Dispose(); attackAction?.Dispose(); blockAction?.Dispose(); interactAction?.Dispose();
        }

        void CreateActions()
        {
            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
            gamepadLookAction = new InputAction("GamepadLook", InputActionType.Value, "<Gamepad>/rightStick", expectedControlType: "Vector2");

            jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");
            sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            sprintAction.AddBinding("<Gamepad>/leftStickPress");
            attackAction = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            attackAction.AddBinding("<Gamepad>/rightTrigger");
            blockAction = new InputAction("Block", InputActionType.Button, "<Mouse>/rightButton");
            blockAction.AddBinding("<Gamepad>/leftTrigger");
            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonWest");
        }

        void Update()
        {
            UpdateDesktopCursor();

            if (Cursor.lockState == CursorLockMode.Locked && !touchMode)
            {
                Vector2 mouse = lookAction.ReadValue<Vector2>() * mouseDegreesPerPixel;
                AddLookDegrees(mouse);
            }

            Vector2 stick = gamepadLookAction.ReadValue<Vector2>();
            if (stick.sqrMagnitude > 0.0225f)
                AddLookDegrees(stick * (gamepadDegreesPerSecond * Time.deltaTime));

            if (jumpAction.WasPressedThisFrame()) jumpPressTime = Time.time;
            if (interactAction.WasPressedThisFrame()) interactPressTime = Time.time;
            if (attackAction.WasPressedThisFrame() && MouseActionsAllowed && !cursorCapturedThisFrame) attackPressTime = Time.time;
            if (mouseAttackSuppressed && !attackAction.IsPressed()) mouseAttackSuppressed = false;

            // A touch sprint toggle ends when the joystick has been idle for a moment.
            if (sprintToggled)
            {
                moveIdleTime = Move.sqrMagnitude < 0.01f ? moveIdleTime + Time.deltaTime : 0f;
                if (moveIdleTime > sprintToggleIdleCancel) sprintToggled = false;
            }

            // Any real touch switches to touch mode automatically (e.g. Device Simulator, touch laptops).
            if (!touchMode && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                SetTouchMode(true);

#if UNITY_EDITOR || UNITY_STANDALONE
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                SetTouchMode(!touchMode);
#endif
        }

        bool cursorCapturedThisFrame;
        bool mouseAttackSuppressed;

        void UpdateDesktopCursor()
        {
            cursorCapturedThisFrame = false;
            if (Application.isMobilePlatform || touchMode) return;

            var mouse = Mouse.current;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (mouse != null && Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                cursorCapturedThisFrame = true;
                mouseAttackSuppressed = true;
            }
        }

        public void SetTouchMode(bool enabled)
        {
            touchMode = enabled;
            if (touchMode)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                ClearTouchState();
            }
            TouchModeChanged?.Invoke(touchMode);
        }

        void AddLookDegrees(Vector2 degrees)
        {
            degrees *= sensitivityMultiplier;
            if (invertY) degrees.y = -degrees.y;
            lookAccumulator += degrees;
        }

        // ---- IPlayerInput -------------------------------------------------------------------------

        public Vector2 ConsumeLook()
        {
            Vector2 look = lookAccumulator;
            lookAccumulator = Vector2.zero;
            return look;
        }

        public bool ConsumeJump() => Consume(ref jumpPressTime, jumpBuffer);
        public bool ConsumeAttack() => Consume(ref attackPressTime, attackBuffer);
        public bool ConsumeInteract() => Consume(ref interactPressTime, interactBuffer);

        public void CancelSprint() => sprintToggled = false;

        static bool Consume(ref float pressTime, float buffer)
        {
            if (pressTime < 0f) return false;
            bool valid = Time.time - pressTime <= buffer;
            pressTime = -1f;
            return valid;
        }

        // ---- Touch UI entry points ----------------------------------------------------------------

        public void SetTouchMove(Vector2 value) => touchMove = Vector2.ClampMagnitude(value, 1f);

        /// <summary>Adds a touch drag delta in screen pixels.</summary>
        public void AddTouchLook(Vector2 pixelDelta)
        {
            float degreesPerPixel = touchDegreesPerScreenHeight / Mathf.Max(1, Screen.height);
            AddLookDegrees(pixelDelta * degreesPerPixel);
        }

        public void SetTouchButton(PlayerButton button, bool down)
        {
            switch (button)
            {
                case PlayerButton.Attack:
                    if (down && !touchAttackHeld) attackPressTime = Time.time;
                    touchAttackHeld = down;
                    break;
                case PlayerButton.Block:
                    touchBlockHeld = down;
                    break;
                case PlayerButton.Jump:
                    if (down) jumpPressTime = Time.time;
                    break;
                case PlayerButton.Interact:
                    if (down) interactPressTime = Time.time;
                    break;
                case PlayerButton.Sprint:
                    if (down)
                    {
                        sprintToggled = !sprintToggled;
                        moveIdleTime = 0f;
                    }
                    break;
            }
        }

        void ClearTouchState()
        {
            touchMove = Vector2.zero;
            touchAttackHeld = false;
            touchBlockHeld = false;
            sprintToggled = false;
        }
    }
}
