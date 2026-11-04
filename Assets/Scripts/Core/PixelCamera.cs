using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.Core
{
    /// <summary>
    /// Renders the world camera into a low-resolution render texture (fixed pixel height, screen aspect) and shows
    /// it full screen through a RawImage with point filtering. This gives the chunky pixel-art look on every display
    /// size while the HUD stays at native resolution. A separate screen camera renders only the UI.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PixelCamera : MonoBehaviour
    {
        [SerializeField] RawImage output;
        [Tooltip("Camera that renders only the UI to the screen. Disabled in edit mode so the Game view shows the world camera.")]
        [SerializeField] Camera screenCamera;
        [Tooltip("Vertical resolution of the world render in pixels.")]
        [SerializeField, Range(144, 1080)] int targetHeight = 360;
        [SerializeField] bool pixelate = true;

        Camera worldCamera;
        RenderTexture target;
        Vector2Int screenSize;
        int appliedHeight;

        public int TargetHeight
        {
            get => targetHeight;
            set
            {
                targetHeight = Mathf.Clamp(value, 144, 1080);
                Rebuild();
            }
        }

        void OnEnable()
        {
            worldCamera = GetComponent<Camera>();
            Rebuild();
        }

        void OnDisable()
        {
            if (screenCamera != null) screenCamera.enabled = false;
            if (worldCamera != null) worldCamera.targetTexture = null;
            if (output != null)
            {
                output.texture = null;
                output.enabled = false;
            }
            Release();
        }

        void LateUpdate()
        {
            if (Screen.width != screenSize.x || Screen.height != screenSize.y || appliedHeight != targetHeight)
                Rebuild();
        }

        void Rebuild()
        {
            if (worldCamera == null || output == null) return;
            screenSize = new Vector2Int(Screen.width, Screen.height);
            appliedHeight = targetHeight;
            Release();

            if (!pixelate || screenSize.x <= 0 || screenSize.y <= 0)
            {
                worldCamera.targetTexture = null;
                output.enabled = false;
                if (screenCamera != null) screenCamera.enabled = false;
                return;
            }
            if (screenCamera != null) screenCamera.enabled = true;

            int height = Mathf.Min(targetHeight, screenSize.y);
            int width = Mathf.Max(1, Mathf.RoundToInt(height * (screenSize.x / (float)screenSize.y)));
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Pixel World Target",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false,
            };
            target.Create();
            worldCamera.targetTexture = target;
            output.texture = target;
            output.enabled = true;
        }

        void Release()
        {
            if (target == null) return;
            if (worldCamera != null && worldCamera.targetTexture == target) worldCamera.targetTexture = null;
            target.Release();
            Destroy(target);
            target = null;
        }
    }
}
