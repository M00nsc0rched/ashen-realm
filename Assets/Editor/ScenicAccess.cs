using AshenRealm.Interactions;
using UnityEngine;

namespace AshenRealm.EditorTools
{
    internal static class ScenicAccess
    {
        /// <summary>Finds standing room and a clear probe, using the player's actual interaction reach.</summary>
        public static bool TryApproach(LoreInspectable site, out Vector3 feet, out Quaternion look)
        {
            var target = site.GetComponent<BoxCollider>();
            feet = default;
            look = Quaternion.identity;
            if (target == null) return false;
            int mask = ~(1 << PlayerInteractor.SeeThroughLayer);
            foreach (float radius in new[] { 1.4f, 1.9f, 2.4f })
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Vector3 candidate = target.bounds.center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                if (!Physics.Raycast(candidate + Vector3.up * 0.6f, Vector3.down, out RaycastHit ground, 4f, mask, QueryTriggerInteraction.Ignore) || ground.normal.y < 0.7f)
                    continue;
                candidate = ground.point + Vector3.up * 0.05f;
                var blockers = Physics.OverlapCapsule(candidate + Vector3.up * 0.4f, candidate + Vector3.up * 1.5f, 0.33f,
                    ~0, QueryTriggerInteraction.Ignore);
                bool clear = true;
                foreach (Collider blocker in blockers)
                    if (!blocker.transform.root.CompareTag("Player")) { clear = false; break; }
                if (!clear) continue;
                Vector3 eye = candidate + Vector3.up * 1.62f;
                Vector3 direction = target.bounds.center - eye;
                if (!Physics.SphereCast(eye, 0.2f, direction.normalized, out RaycastHit hit, 3f, mask, QueryTriggerInteraction.Collide) || hit.collider != target)
                    continue;
                feet = candidate;
                look = Quaternion.LookRotation(direction);
                return true;
            }
            return false;
        }
    }
}
