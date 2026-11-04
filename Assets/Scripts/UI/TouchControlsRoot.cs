using AshenRealm.Input;
using UnityEngine;

namespace AshenRealm.UI
{
    /// <summary>
    /// Shows the touch control layer only while the input hub is in touch mode.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TouchControlsRoot : MonoBehaviour
    {
        [SerializeField] PlayerInputHub input;

        CanvasGroup group;

        void Awake() => group = GetComponent<CanvasGroup>();

        void OnEnable()
        {
            if (input == null) return;
            input.TouchModeChanged += Apply;
            Apply(input.TouchMode);
        }

        void OnDisable()
        {
            if (input != null) input.TouchModeChanged -= Apply;
        }

        void Apply(bool touchMode)
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            group.alpha = touchMode ? 1f : 0f;
            group.interactable = touchMode;
            group.blocksRaycasts = touchMode;
        }
    }
}
