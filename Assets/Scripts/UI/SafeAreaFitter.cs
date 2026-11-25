using UnityEngine;

namespace AshenRealm.UI
{
    /// <summary>
    /// Fits this RectTransform to the safe area (notch / Dynamic Island / home indicator). Unity's web runtime reports the
    /// whole canvas as Screen.safeArea, so in a web build the insets come from the page instead (Plugins/WebGL/SafeArea.jslib).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rect;
        Rect appliedArea;
        Vector2Int appliedSize;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Safari can change the insets on rotation without a resize, so they are re-read now and then, not every frame.
        const float WebReadInterval = 0.25f;
        static readonly float[] insets = new float[4];
        float nextWebRead;

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void SafeAreaReadInsets(float[] topRightBottomLeft);

        static Rect CurrentArea()
        {
            SafeAreaReadInsets(insets);
            return FromInsets(Screen.width, Screen.height, insets[0], insets[1], insets[2], insets[3]);
        }
#else
        static Rect CurrentArea() => Screen.safeArea;
#endif

        /// <summary>The safe area of a screen with these insets (in CSS order, screen pixels), bottom-left origin, kept on the screen.</summary>
        public static Rect FromInsets(float width, float height, float top, float right, float bottom, float left)
        {
            float x = Mathf.Clamp(left, 0f, width), y = Mathf.Clamp(bottom, 0f, height);
            return new Rect(x, y, Mathf.Max(0f, width - x - Mathf.Clamp(right, 0f, width)), Mathf.Max(0f, height - y - Mathf.Clamp(top, 0f, height)));
        }

        void Awake() => rect = GetComponent<RectTransform>();

        void OnEnable() => Apply(CurrentArea());

        void Update()
        {
            bool resized = Screen.width != appliedSize.x || Screen.height != appliedSize.y;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!resized && Time.unscaledTime < nextWebRead) return;
            nextWebRead = Time.unscaledTime + WebReadInterval;
#endif
            Rect area = CurrentArea();
            if (resized || area != appliedArea) Apply(area);
        }

        void Apply(Rect area)
        {
            if (rect == null) rect = GetComponent<RectTransform>();
            appliedArea = area;
            appliedSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
