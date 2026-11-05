using System;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>Spawns bolts. Prefabs are indexed by <see cref="BoltKind"/>.</summary>
    public sealed class ProjectileLibrary : MonoBehaviour
    {
        [SerializeField] Projectile[] prefabs;

        public void Launch(BoltKind kind, Vector3 origin, Vector3 aim, float damage, GameObject owner, bool fromPlayer, EnemyMove effects = null,
            Action<float> onHit = null)
        {
            Projectile bolt = Instantiate(prefabs[(int)kind], origin, Quaternion.identity);
            bolt.Launch(aim, damage, owner, fromPlayer, effects, onHit);
        }
    }
}
