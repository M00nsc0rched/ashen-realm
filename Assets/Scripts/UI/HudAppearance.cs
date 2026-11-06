using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.UI
{
    /// <summary>
    /// Applies the HUD settings to the status elements: bars, compass, emblem, crosshair, enemy bar. Prompts, messages and
    /// the death screen stay outside this group on purpose: they carry the game, and hiding them would hide its instructions.
    /// </summary>
    public sealed class HudAppearance : MonoBehaviour
    {
        [SerializeField] CanvasGroup elements;

        void OnEnable()
        {
            GameSettings.Changed += Apply;
            Apply();
        }

        void OnDisable() => GameSettings.Changed -= Apply;

        void Apply() => elements.alpha = GameSettings.HudVisible ? GameSettings.HudOpacity : 0f;
    }
}
