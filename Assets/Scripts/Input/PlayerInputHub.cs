using System;
using AshenRealm.Core;
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
        static string SensitivityPrefKey => GameSettings.Prefix + "LookSensitivity";

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
        InputAction mouseAttackAction, mouseBlockAction, ability1Action, ability2Action, potionAction, pauseAction, backAction;
        InputAction[] actions;

        Vector2 lookAccumulator;
        Vector2 touchMove;
        bool touchAttackHeld, touchBlockHeld;
        bool sprintToggled;
        float moveIdleTime;
        float jumpPressTime = -1f, attackPressTime = -1f, interactPressTime = -1f, potionPressTime = -1f;
        readonly float[] abilityPressTime = { -1f, -1f };
        bool touchMode;
        float sensitivityMultiplier = 1f;
        bool paused;
        bool wasCursorLocked;

        public event Action<bool> TouchModeChanged;
        /// <summary>Raised when the player asks for the pause menu (Esc, or the browser released pointer lock).</summary>
        public event Action PauseRequested;

        public bool TouchMode => touchMode;
        public bool SprintToggled => sprintToggled;
        public bool Paused => paused;

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
                if (paused) return Vector2.zero;
                Vector2 move = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
                if (touchMove.sqrMagnitude > move.sqrMagnitude) move = touchMove;
                return Vector2.ClampMagnitude(move, 1f);
            }
        }

        public bool SprintHeld => !paused && (sprintToggled || (sprintAction != null && sprintAction.IsPressed()));
        public bool BlockHeld => !paused && (touchBlockHeld || Pressed(blockAction) || (MouseActionsAllowed && Pressed(mouseBlockAction)));
        public bool AttackHeld => !paused && (touchAttackHeld || Pressed(attackAction) || (MouseActionsAllowed && !mouseAttackSuppressed && Pressed(mouseAttackAction)));

        /// <summary>
        /// Mouse buttons fight only while the cursor is captured in desktop mode. In touch mode the mouse (and the
        /// mouse events browsers emulate from taps) only drives the on-screen controls, so dragging to look never
        /// attacks: there the STRIKE button is the only way to swing.
        /// </summary>
        bool MouseActionsAllowed => !touchMode && Cursor.lockState == CursorLockMode.Locked;

        static bool Pressed(InputAction action) => action != null && action.IsPressed();

        void Awake()
        {
            sensitivityMultiplier = PlayerPrefs.GetFloat(SensitivityPrefKey, 1f);
            CreateActions();
            ApplyTouchPreference();
        }

        void OnEnable()
        {
            foreach (var action in actions) action.Enable();
        }

        void OnDisable()
        {
            foreach (var action in actions) action.Disable();
            ClearTouchState();
            if (!Application.isMobilePlatform) Cursor.lockState = CursorLockMode.None;
        }

        void OnDestroy()
        {
            if (actions == null) return;
            foreach (var action in actions) action.Dispose();
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
            attackAction = new InputAction("Attack", InputActionType.Button, "<Gamepad>/rightTrigger");
            blockAction = new InputAction("Block", InputActionType.Button, "<Gamepad>/leftTrigger");
            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonWest");

            // Mouse buttons are separate actions: they are gated by cursor capture, the gamepad triggers are not.
            mouseAttackAction = new InputAction("MouseAttack", InputActionType.Button, "<Mouse>/leftButton");
            mouseBlockAction = new InputAction("MouseBlock", InputActionType.Button, "<Mouse>/rightButton");

            ability1Action = new InputAction("Ability1", InputActionType.Button, "<Keyboard>/q");
            ability1Action.AddBinding("<Gamepad>/rightShoulder");
            ability2Action = new InputAction("Ability2", InputActionType.Button, "<Keyboard>/r");
            ability2Action.AddBinding("<Gamepad>/leftShoulder");
            potionAction = new InputAction("Potion", InputActionType.Button, "<Keyboard>/f");
            potionAction.AddBinding("<Gamepad>/buttonNorth");

            pauseAction = new InputAction("Pause", InputActionType.Button, "<Gamepad>/start");
            backAction = new InputAction("Back", InputActionType.Button, "<Gamepad>/buttonEast");

            actions = new[]
            {
                moveAction, lookAction, gamepadLookAction, jumpAction, sprintAction,
                attackAction, blockAction, interactAction, mouseAttackAction, mouseBlockAction,
                ability1Action, ability2Action, potionAction, pauseAction, backAction,
            };
        }

        void Update()
        {
            UpdateDesktopCursor();
            if (paused)
            {
                lookAccumulator = Vector2.zero;
                return;
            }

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
            if (ability1Action.WasPressedThisFrame()) abilityPressTime[0] = Time.time;
            if (ability2Action.WasPressedThisFrame()) abilityPressTime[1] = Time.time;
            if (potionAction.WasPressedThisFrame()) potionPressTime = Time.time;
            if (attackAction.WasPressedThisFrame()) attackPressTime = Time.time;
            if (mouseAttackAction.WasPressedThisFrame() && MouseActionsAllowed && !cursorCapturedThisFrame) attackPressTime = Time.time;
            if (mouseAttackSuppressed && !mouseAttackAction.IsPressed()) mouseAttackSuppressed = false;

            // A touch sprint toggle ends when the joystick has been idle for a moment.
            if (sprintToggled)
            {
                moveIdleTime = Move.sqrMagnitude < 0.01f ? moveIdleTime + Time.deltaTime : 0f;
                if (moveIdleTime > sprintToggleIdleCancel) sprintToggled = false;
            }

            // Any real touch switches to touch mode automatically (e.g. Device Simulator, touch laptops).
            // Not after the player hid the buttons: a controller on a phone must not bring them back at every tap.
            if (!touchMode && GameSettings.TouchButtonsMode != GameSettings.TouchButtons.Hidden
                && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                SetTouchMode(true);

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                ChooseTouchButtons(!touchMode);
#endif
        }

        bool cursorCapturedThisFrame;
        bool mouseAttackSuppressed;

        void UpdateDesktopCursor()
        {
            cursorCapturedThisFrame = false;
            // B only steps back through open menus; in play it stays free for the game.
            bool escape = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                          || pauseAction.WasPressedThisFrame() || (paused && backAction.WasPressedThisFrame());
            if (Application.isMobilePlatform || touchMode)
            {
                wasCursorLocked = false;
                if (escape) PauseRequested?.Invoke();
                return;
            }

            var mouse = Mouse.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (escape)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                PauseRequested?.Invoke();
            }
            else if (wasCursorLocked && !locked && !paused)
            {
                // The browser released pointer lock (Esc in WebGL, alt-tab): treat it as a pause request.
                PauseRequested?.Invoke();
            }
            else if (!paused && mouse != null && !locked && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                cursorCapturedThisFrame = true;
                mouseAttackSuppressed = true;
            }
            wasCursorLocked = Cursor.lockState == CursorLockMode.Locked;
        }

        /// <summary>Freezes gameplay input (pause menu). Releases the cursor so menus can be clicked.</summary>
        public void SetPaused(bool value)
        {
            paused = value;
            if (!paused) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            wasCursorLocked = false;
            lookAccumulator = Vector2.zero;
            jumpPressTime = attackPressTime = interactPressTime = potionPressTime = -1f;
            abilityPressTime[0] = abilityPressTime[1] = -1f;
            touchMove = Vector2.zero;
            touchAttackHeld = touchBlockHeld = false;
        }

        /// <summary>The player chose (settings, F1): remembered, and it outranks what the device suggests.</summary>
        public void ChooseTouchButtons(bool shown)
        {
            GameSettings.TouchButtonsMode = shown ? GameSettings.TouchButtons.Shown : GameSettings.TouchButtons.Hidden;
            ApplyTouchPreference();
        }

        /// <summary>Takes the touch mode from the stored choice, or from the device when there is none.</summary>
        public void ApplyTouchPreference()
        {
            GameSettings.TouchButtons mode = GameSettings.TouchButtonsMode;
            bool device = Application.isMobilePlatform || touchControlsInEditor || Touchscreen.current != null;
            SetTouchMode(mode == GameSettings.TouchButtons.Shown || (mode == GameSettings.TouchButtons.Auto && device));
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
            Vector2 look = paused ? Vector2.zero : lookAccumulator;
            lookAccumulator = Vector2.zero;
            return look;
        }

        public bool ConsumeJump() => Consume(ref jumpPressTime, jumpBuffer) && !paused;
        public bool ConsumeAttack() => Consume(ref attackPressTime, attackBuffer) && !paused;
        public bool ConsumeInteract() => Consume(ref interactPressTime, interactBuffer) && !paused;
        public bool ConsumeAbility(int slot) => Consume(ref abilityPressTime[slot], attackBuffer) && !paused;
        public bool ConsumePotion() => Consume(ref potionPressTime, attackBuffer) && !paused;

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
                case PlayerButton.Ability1:
                    if (down) abilityPressTime[0] = Time.time;
                    break;
                case PlayerButton.Ability2:
                    if (down) abilityPressTime[1] = Time.time;
                    break;
                case PlayerButton.Potion:
                    if (down) potionPressTime = Time.time;
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
