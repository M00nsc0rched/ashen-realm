using System;
using UnityEngine;

namespace AshenRealm.Core
{
    /// <summary>
    /// Player preferences that outlive a soul: how see-through the on-screen controls and the HUD are, whether they show at
    /// all, and where the player wants the buttons. Lives in PlayerPrefs; <see cref="Flush"/> makes a change survive the page.
    /// </summary>
    public static class GameSettings
    {
        public const float MinOpacity = 0.15f;

        /// <summary>Touch buttons follow the device until the player chooses.</summary>
        public enum TouchButtons { Auto, Shown, Hidden }

        /// <summary>Everything the game stores starts with this. The playtest swaps it to stay out of a player's own data.</summary>
        public static string Prefix = "AshenRealm.";

        static string ButtonOpacityKey => Prefix + "ButtonOpacity";
        static string HudOpacityKey => Prefix + "HudOpacity";
        static string HudVisibleKey => Prefix + "HudVisible";
        static string TouchButtonsKey => Prefix + "TouchButtons";
        static string LayoutKey => Prefix + "TouchLayout";

        /// <summary>Raised after any change, so the HUD can apply it at once behind the open menu.</summary>
        public static event Action Changed;

        public static float ButtonOpacity
        {
            get => PlayerPrefs.GetFloat(ButtonOpacityKey, 1f);
            set => SetFloat(ButtonOpacityKey, Mathf.Clamp(value, MinOpacity, 1f));
        }

        public static float HudOpacity
        {
            get => PlayerPrefs.GetFloat(HudOpacityKey, 1f);
            set => SetFloat(HudOpacityKey, Mathf.Clamp(value, MinOpacity, 1f));
        }

        public static bool HudVisible
        {
            get => PlayerPrefs.GetInt(HudVisibleKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(HudVisibleKey, value ? 1 : 0);
                Changed?.Invoke();
            }
        }

        public static TouchButtons TouchButtonsMode
        {
            get => (TouchButtons)PlayerPrefs.GetInt(TouchButtonsKey, (int)TouchButtons.Auto);
            set
            {
                PlayerPrefs.SetInt(TouchButtonsKey, (int)value);
                Changed?.Invoke();
            }
        }

        /// <summary>JSON written by TouchLayout; empty means the built-in layout.</summary>
        public static string TouchLayoutJson
        {
            get => PlayerPrefs.GetString(LayoutKey, string.Empty);
            set => PlayerPrefs.SetString(LayoutKey, value);
        }

        /// <summary>Back to how the game ships. The look speed lives in the input hub, which resets its own.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(ButtonOpacityKey);
            PlayerPrefs.DeleteKey(HudOpacityKey);
            PlayerPrefs.DeleteKey(HudVisibleKey);
            PlayerPrefs.DeleteKey(TouchButtonsKey);
            Changed?.Invoke();
        }

        public static void Flush() => PlayerPrefs.Save();

        static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            Changed?.Invoke();
        }
    }
}
