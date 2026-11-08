using AshenRealm.World;
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

        Zone currentZone;
        Transform traveller;
        Material originalSky, eveningSky;
        float eveningProgress;
        bool eveningLocked;

        public float EveningProgress => eveningProgress;

        public Color AmbientColor => ambientColor;
        public Color FogColor => fogColor;
        public float FogStart => fogStart;
        public float FogEnd => fogEnd;

        void OnEnable() => Apply();

        void Update()
        {
            if (currentZone == null || currentZone.OutOfTheSun) return;
            ApplyOutside();
        }

        void OnDestroy()
        {
            if (eveningSky == null) return;
            if (RenderSettings.skybox == eveningSky) RenderSettings.skybox = originalSky;
            if (Application.isPlaying) Destroy(eveningSky);
            else DestroyImmediate(eveningSky);
        }

        /// <summary>The walk north carries sunset into evening. Visiting the city makes the change permanent for this soul.</summary>
        public void ApplyZone(Zone zone, Transform player, bool evening)
        {
            currentZone = zone;
            traveller = player;
            eveningLocked = evening;
            if (eveningLocked) eveningProgress = 1f;
            if (zone.OutOfTheSun)
                Apply(zone.Ambient, zone.Fog, zone.FogStart, zone.FogEnd);
            else
                ApplyOutside();
        }

        void ApplyOutside()
        {
            float travelled = traveller == null ? 0f : Mathf.InverseLerp(-86f, 92f, traveller.position.z);
            eveningProgress = eveningLocked ? 1f : Mathf.Max(eveningProgress, travelled);
            float t = Mathf.SmoothStep(0f, 1f, eveningProgress);
            Color duskAmbient = new Color(0.49f, 0.4f, 0.43f);
            Color nightAmbient = new Color(0.3f, 0.33f, 0.44f);
            Color duskFog = new Color(0.56f, 0.34f, 0.3f);
            Color nightFog = new Color(0.09f, 0.115f, 0.19f);
            Apply(Color.Lerp(duskAmbient, nightAmbient, t), Color.Lerp(duskFog, nightFog, t), currentZone.FogStart, currentZone.FogEnd);
            Light sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(13f, 2f, t), 110f, 0f);
                sun.color = Color.Lerp(new Color(1f, 0.65f, 0.36f), new Color(0.48f, 0.57f, 0.8f), t);
                sun.intensity = Mathf.Lerp(1.35f, 0.26f, t);
                sun.shadowStrength = Mathf.Lerp(0.85f, 0.55f, t);
            }
            if (eveningSky == null && RenderSettings.skybox != null)
            {
                originalSky = RenderSettings.skybox;
                eveningSky = new Material(originalSky);
                RenderSettings.skybox = eveningSky;
            }
            if (eveningSky != null)
            {
                if (eveningSky.HasProperty("_Tint")) eveningSky.SetColor("_Tint", Color.Lerp(new Color(0.65f, 0.44f, 0.4f), new Color(0.13f, 0.17f, 0.28f), t));
                if (eveningSky.HasProperty("_Exposure")) eveningSky.SetFloat("_Exposure", Mathf.Lerp(1f, 0.5f, t));
            }
        }

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
