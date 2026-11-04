using UnityEngine;

namespace AshenRealm.Core
{
    /// <summary>
    /// Applies app-wide runtime settings (frame rate, orientation, screen sleep) when the scene starts.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Frame rate cap on mobile devices. 30 is the baseline; raise to 60 only after profiling.")]
        [SerializeField] int mobileTargetFrameRate = 30;
        [Tooltip("Frame rate cap in the Editor and on desktop. -1 = platform default.")]
        [SerializeField] int desktopTargetFrameRate = -1;
        [SerializeField] bool preventScreenSleep = true;

        void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Browsers pace frames with requestAnimationFrame; a fixed target would stutter.
            Application.targetFrameRate = -1;
#else
            if (Application.isMobilePlatform)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = mobileTargetFrameRate;
            }
            else
            {
                Application.targetFrameRate = desktopTargetFrameRate;
            }
#endif

            if (preventScreenSleep)
                Screen.sleepTimeout = SleepTimeout.NeverSleep;

#if UNITY_IOS || UNITY_ANDROID
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif
        }
    }
}
