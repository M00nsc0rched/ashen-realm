using AshenRealm.Core;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>The carried torch is a real inventory choice; travelling never silently takes it out of the hand.</summary>
    public sealed class PlayerTorch : MonoBehaviour
    {
        [SerializeField] Transform mount;
        [SerializeField] Renderer flame;
        [SerializeField] Light torchLight;
        [SerializeField] Texture2D warmStrip;
        [SerializeField] Texture2D coldStrip;
        [SerializeField] FirstPersonController controller;
        [SerializeField] PlayerClassRig classRig;

        static readonly int FrameId = Shader.PropertyToID("_Frame");
        static readonly int TextureId = Shader.PropertyToID("_MainTex");
        MaterialPropertyBlock properties;
        Vector3 restPosition;
        Quaternion restRotation;
        bool equipped;
        float elapsed;
        int frame = -1;

        public static PlayerTorch Instance { get; private set; }
        public bool Equipped => equipped;
        public int AnimationFrame => frame;
        public Light Light => torchLight;

        void Awake()
        {
            Instance = this;
            properties = new MaterialPropertyBlock();
            restPosition = mount.localPosition;
            restRotation = mount.localRotation;
            Refresh();
        }

        void Start()
        {
            if (GameSession.Instance != null) GameSession.Instance.ClassSelected += OnClassSelected;
            Refresh();
        }

        void OnDestroy()
        {
            if (GameSession.Instance != null) GameSession.Instance.ClassSelected -= OnClassSelected;
            if (Instance == this) Instance = null;
        }

        void OnClassSelected(ClassDefinition definition) => Refresh();

        public void SetEquipped(bool value)
        {
            equipped = value;
            Refresh();
            classRig.RefreshHeldModels();
        }

        /// <summary>Keep the inventory decision in houses as well as outdoors and underground.</summary>
        public void SetZone(Zone zone) => Refresh();

        void Refresh()
        {
            mount.gameObject.SetActive(equipped);
            torchLight.enabled = equipped;
            CharacterClass id = GameSession.Instance != null ? GameSession.Instance.Class.Id : CharacterClass.Knight;
            bool cold = id == CharacterClass.Witch || (int)id == 4;
            properties.SetTexture(TextureId, cold ? coldStrip : warmStrip);
            properties.SetFloat(FrameId, Mathf.Max(0, frame));
            flame.SetPropertyBlock(properties);
            torchLight.color = cold ? new Color(0.28f, 0.62f, 1f) : new Color(1f, 0.67f, 0.32f);
        }

        void LateUpdate()
        {
            if (!equipped) return;
            elapsed += Time.deltaTime;
            int next = Mathf.FloorToInt(elapsed * 10f) % 8;
            if (next != frame)
            {
                frame = next;
                properties.SetFloat(FrameId, frame);
                flame.SetPropertyBlock(properties);
            }

            float moving = Mathf.Clamp01(controller.NormalizedSpeed);
            float step = controller.StepCycle * 2.4f;
            Vector2 look = controller.LastLookDelta;
            mount.localPosition = restPosition + new Vector3(Mathf.Sin(step) * 0.012f * moving,
                Mathf.Sin(elapsed * 1.8f) * 0.006f + Mathf.Abs(Mathf.Cos(step)) * 0.018f * moving, 0f);
            mount.localRotation = restRotation * Quaternion.Euler(Mathf.Clamp(look.y * 0.3f, -3f, 3f),
                Mathf.Clamp(-look.x * 0.3f, -3f, 3f), Mathf.Sin(elapsed * 1.7f) * 1.2f);
        }
    }
}
