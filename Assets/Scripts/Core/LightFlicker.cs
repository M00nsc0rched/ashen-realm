using UnityEngine;

namespace AshenRealm.Core
{
    /// <summary>
    /// Fire-like intensity and position flicker for a point light.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class LightFlicker : MonoBehaviour
    {
        [SerializeField] float baseIntensity = 2.2f;
        [SerializeField] float intensityVariation = 0.6f;
        [SerializeField] float speed = 7f;
        [SerializeField] float positionJitter = 0.05f;

        Light targetLight;
        Vector3 basePosition;
        float seed;

        void Awake()
        {
            targetLight = GetComponent<Light>();
            basePosition = transform.localPosition;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time * speed + seed;
            float n = Mathf.PerlinNoise(t, seed) - 0.5f;
            targetLight.intensity = baseIntensity + n * 2f * intensityVariation;
            transform.localPosition = basePosition + new Vector3(
                (Mathf.PerlinNoise(t * 0.7f, 3f) - 0.5f) * positionJitter,
                (Mathf.PerlinNoise(5f, t * 0.9f) - 0.5f) * positionJitter,
                (Mathf.PerlinNoise(t * 0.8f, 9f) - 0.5f) * positionJitter);
        }
    }
}
