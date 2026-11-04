using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>
    /// Tints a set of renderers briefly when the attached <see cref="Health"/> takes damage.
    /// Uses a MaterialPropertyBlock only while flashing so renderers stay SRP-batcher friendly otherwise.
    /// </summary>
    public sealed class HitFlash : MonoBehaviour
    {
        // AshenRealm/PixelLit multiplies albedo by _Tint.
        static readonly int BaseColorId = Shader.PropertyToID("_Tint");

        [SerializeField] Health health;
        [SerializeField] Renderer[] renderers;
        [SerializeField] Color flashColor = new Color(1f, 0.35f, 0.25f);
        [SerializeField] float duration = 0.18f;

        MaterialPropertyBlock block;
        float flashTime = -1f;

        void Awake() => block = new MaterialPropertyBlock();

        void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
            Clear();
        }

        void OnDamaged(DamageInfo info, float amount) => flashTime = 0f;

        void Update()
        {
            if (flashTime < 0f) return;
            flashTime += Time.deltaTime;
            float t = flashTime / duration;
            if (t >= 1f)
            {
                Clear();
                return;
            }

            block.SetColor(BaseColorId, Color.Lerp(flashColor, Color.white, t * t));
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].SetPropertyBlock(block);
        }

        void Clear()
        {
            flashTime = -1f;
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].SetPropertyBlock(null);
        }
    }
}
