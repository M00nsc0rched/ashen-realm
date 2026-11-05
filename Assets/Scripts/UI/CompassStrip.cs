using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// Horizontal compass at the top of the screen. Cardinal labels, ticks and an objective marker slide with
    /// the camera's heading (+Z = north) and fade toward the strip edges.
    /// </summary>
    public sealed class CompassStrip : MonoBehaviour
    {
        [SerializeField] Transform viewer;
        [SerializeField] RectTransform strip;
        [SerializeField] Graphic[] markers;
        [SerializeField] float[] headings;
        [SerializeField] Graphic objectiveMarker;
        [SerializeField] Vector3 objectivePosition;

        [Tooltip("Degrees visible on each side of the centre.")]
        [SerializeField] float halfRange = 75f;

        float[] baseAlpha;

        public void SetObjective(Vector3 worldPosition) => objectivePosition = worldPosition;

        void Awake()
        {
            baseAlpha = new float[markers.Length];
            for (int i = 0; i < markers.Length; i++) baseAlpha[i] = markers[i].color.a;
        }

        void LateUpdate()
        {
            if (viewer == null) return;
            float yaw = viewer.eulerAngles.y;
            float halfWidth = strip.rect.width * 0.5f;
            for (int i = 0; i < markers.Length; i++)
                Place(markers[i], Mathf.DeltaAngle(yaw, headings[i]), halfWidth, baseAlpha[i]);

            if (objectiveMarker != null)
            {
                Vector3 to = objectivePosition - viewer.position;
                float heading = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                Place(objectiveMarker, Mathf.DeltaAngle(yaw, heading), halfWidth, 1f);
            }
        }

        void Place(Graphic graphic, float delta, float halfWidth, float alpha)
        {
            var rect = graphic.rectTransform;
            rect.anchoredPosition = new Vector2(delta / halfRange * halfWidth, rect.anchoredPosition.y);
            float fade = Mathf.Clamp01((halfRange - Mathf.Abs(delta)) / 20f);
            Color c = graphic.color;
            c.a = alpha * fade;
            graphic.color = c;
        }
    }
}
