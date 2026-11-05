using UnityEngine;
using UnityEngine.Rendering;

namespace AshenRealm.Core
{
    /// <summary>
    /// Applies ambient light and fog at runtime so the look does not depend on baked lighting data.
    /// The scene builder writes the same values into the scene's RenderSettings for Editor preview.
    /// </summary>
    public sealed class AtmosphereController : MonoBehaviour
    {
        [SerializeField] Color ambientColor = new Color(0.36f, 0.34f, 0.38f);
        [SerializeField] bool fogEnabled = true;
        [SerializeField] Color fogColor = new Color(0.52f, 0.45f, 0.40f);
        [SerializeField] float fogStart = 20f;
        [SerializeField] float fogEnd = 330f;

        public Color AmbientColor => ambientColor;
        public Color FogColor => fogColor;
        public float FogStart => fogStart;
        public float FogEnd => fogEnd;

        void OnEnable() => Apply();

        /// <summary>Switches to a zone's light and fog.</summary>
        public void Apply(Color ambient, Color fog, float start, float end)
        {
            ambientColor = ambient;
            fogColor = fog;
            fogStart = start;
            fogEnd = end;
            Apply();
        }

        public void Apply()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambientColor);
            RenderSettings.ambientProbe = probe;

            RenderSettings.fog = fogEnabled;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }
    }
}
