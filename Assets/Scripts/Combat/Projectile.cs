using System;
using AshenRealm.AI;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>
    /// A thrown bolt: arrows, curses, fire. Flies straight, can be sidestepped, and is stopped by walls and shields.
    /// Enemy bolts pass through other enemies, so a pack can fire past its front rank.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        const float Radius = 0.18f;

        [SerializeField] float speed = 14f;
        [SerializeField] float lifetime = 3f;

        readonly RaycastHit[] hits = new RaycastHit[8];
        Vector3 direction;
        float damage;
        GameObject owner;
        bool fromPlayer;
        EnemyMove move;
        Action<float> onHit;
        float expires;

        public void Launch(Vector3 aim, float amount, GameObject source, bool playerBolt, EnemyMove effects, Action<float> hitCallback)
        {
            direction = aim.normalized;
            damage = amount;
            owner = source;
            fromPlayer = playerBolt;
            move = effects;
            onHit = hitCallback;
            expires = Time.time + lifetime;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        void Update()
        {
            if (Time.time >= expires)
            {
                Destroy(gameObject);
                return;
            }

            float step = speed * Time.deltaTime;
            int count = Physics.SphereCastNonAlloc(transform.position, Radius, direction, hits, step, ~0, QueryTriggerInteraction.Ignore);
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (owner != null && c.transform.IsChildOf(owner.transform)) continue;
                if (!fromPlayer && c.GetComponentInParent<EnemyAI>() != null) continue;
                if (hits[i].distance < bestDistance)
                {
                    bestDistance = hits[i].distance;
                    best = i;
                }
            }

            if (best >= 0)
            {
                Strike(hits[best]);
                Destroy(gameObject);
                return;
            }
            transform.position += direction * step;
        }

        void Strike(in RaycastHit hit)
        {
            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive) return;

            Vector3 point = hit.distance <= 0f ? transform.position : hit.point;
            float applied = damageable.ApplyDamage(new DamageInfo(damage, point, direction, owner));
            onHit?.Invoke(applied);
            if (move != null && applied > 0f) EnemyMoveEffects.Apply(move, hit.collider.GetComponentInParent<StatusEffects>());
        }
    }
}
