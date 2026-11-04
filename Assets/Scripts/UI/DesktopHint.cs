using AshenRealm.Input;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// Shows the keyboard/mouse control reference only while touch controls are hidden.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class DesktopHint : MonoBehaviour
    {
        [SerializeField] PlayerInputHub input;

        Graphic graphic;

        void Awake() => graphic = GetComponent<Graphic>();

        void Update()
        {
            bool show = input != null && !input.TouchMode && !Application.isMobilePlatform;
            if (graphic.enabled != show) graphic.enabled = show;
        }
    }
}
