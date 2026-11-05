using UnityEngine;

namespace AshenRealm.World
{
    /// <summary>
    /// One place in the world: the valley, the city or a depth of the Deep Dark. Zones sit far apart in the scene and only
    /// the current one is active, so a much larger world costs no more per frame than the old valley did.
    /// </summary>
    public sealed class Zone : MonoBehaviour
    {
        [SerializeField] string title;
        [SerializeField] string subtitle;
        [Tooltip("Spawn point when arriving from above; also where the player respawns.")]
        [SerializeField] Transform entry;
        [Tooltip("Spawn point when climbing back up from the zone below.")]
        [SerializeField] Transform exit;
        [Tooltip("Where the compass points.")]
        [SerializeField] Transform objective;

        [Header("Atmosphere")]
        [SerializeField] bool underground;
        [SerializeField] Color ambient = new Color(0.6f, 0.58f, 0.64f);
        [SerializeField] Color fog = new Color(0.5f, 0.45f, 0.4f);
        [SerializeField] float fogStart = 30f;
        [SerializeField] float fogEnd = 320f;

        public string Title => title;
        public string Subtitle => subtitle;
        public Transform Entry => entry;
        public Transform Exit => exit;
        public Transform Objective => objective;
        public bool Underground => underground;
        public Color Ambient => ambient;
        public Color Fog => fog;
        public float FogStart => fogStart;
        public float FogEnd => fogEnd;
    }
}
