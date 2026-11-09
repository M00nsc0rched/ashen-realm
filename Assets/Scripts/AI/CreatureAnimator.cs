using AshenRealm.Core;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.AI
{
    /// <summary>
    /// Procedural animation for the non-humanoid rigs: a trot for beasts, wingbeats and hover for flyers, squash and
    /// stretch for blobs. Pure presentation; timing comes from <see cref="EnemyAI"/>.
    /// </summary>
    public sealed class CreatureAnimator : MonoBehaviour, ICreatureAnimator, IEnemyAttackAnimator
    {
        [SerializeField] EnemyBody body;
        [SerializeField] Transform model;
        [SerializeField, Optional] Transform head;
        [SerializeField, Optional] Transform tail;
        [Tooltip("Beast legs (front left, front right, rear left, rear right) or flyer wings (left, right).")]
        [SerializeField] Transform[] limbs = new Transform[0];
        [SerializeField] float hoverHeight = 1.4f;
        [SerializeField] float stride = 2.4f;

        Quaternion[] limbRest;
        Quaternion headRest, tailRest;
        Vector3 modelRest, modelScale;
        MoveKind attackStyle;
        float burrow;
        float locomotion, phase;
        float attackTime = -1f, attackWindup = 0.5f, attackRecovery = 0.5f;
        float recoilTime = -1f, deathTime = -1f;
        Vector3 hitOffset, hitVelocity;

        void Awake()
        {
            limbRest = new Quaternion[limbs.Length];
            for (int i = 0; i < limbs.Length; i++) limbRest[i] = limbs[i].localRotation;
            if (head != null) headRest = head.localRotation;
            if (tail != null) tailRest = tail.localRotation;
            modelRest = model.localPosition;
            modelScale = model.localScale;
        }

        public void SetAttackStyle(MoveKind kind) => attackStyle = kind;
        public void SetBurrow(float amount) => burrow = Mathf.Clamp01(amount);

        public void SetLocomotion(float normalizedSpeed) =>
            locomotion = Mathf.MoveTowards(locomotion, Mathf.Clamp01(normalizedSpeed), Time.deltaTime * 4f);

        public void PlayAttack(float windup, float recovery)
        {
            attackWindup = windup;
            attackRecovery = recovery;
            attackTime = 0f;
            recoilTime = -1f;
        }

        public void PlayRecoil()
        {
            attackTime = -1f;
            recoilTime = 0f;
        }

        public void PlayHit(Vector3 worldDirection) => hitVelocity += transform.InverseTransformDirection(worldDirection) * 1.6f;

        public void PlayDeath()
        {
            deathTime = 0f;
            attackTime = -1f;
            recoilTime = -1f;
        }

        /// <summary>0 at rest, -1 fully drawn back, +1 fully lunged.</summary>
        float Lunge()
        {
            if (recoilTime >= 0f)
            {
                recoilTime += Time.deltaTime;
                if (recoilTime >= 0.6f) recoilTime = -1f;
                else return -Mathf.Sin(recoilTime / 0.6f * Mathf.PI);
            }
            if (attackTime < 0f) return 0f;

            attackTime += Time.deltaTime;
            if (attackTime < attackWindup) return -Ease(attackTime / attackWindup);
            float strike = attackTime - attackWindup;
            if (strike < 0.12f) return Mathf.Lerp(-1f, 1f, strike / 0.12f);
            float k = (strike - 0.12f) / Mathf.Max(0.01f, attackRecovery - 0.12f);
            if (k >= 1f) attackTime = -1f;
            return 1f - Ease(k);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (deathTime >= 0f)
            {
                deathTime += dt;
                UpdateDeath();
                return;
            }

            float moving = Mathf.Clamp01(locomotion * 1.5f);
            phase += dt * stride * Mathf.PI * 2f * Mathf.Lerp(0.6f, 1.4f, locomotion);
            float lunge = Lunge();

            hitVelocity += (-160f * hitOffset - 16f * hitVelocity) * dt;
            hitOffset += hitVelocity * dt;

            switch (body)
            {
                case EnemyBody.Beast: AnimateBeast(moving, lunge); break;
                case EnemyBody.Flyer: AnimateFlyer(moving, lunge); break;
                default: AnimateBlob(moving, lunge); break;
            }
            model.localPosition += Vector3.down * (burrow * 1.8f);
        }

        void AnimateBeast(float moving, float lunge)
        {
            float swing = Mathf.Sin(phase) * moving;
            for (int i = 0; i < limbs.Length; i++)
            {
                float sign = i == 0 || i == 3 ? 1f : -1f; // trot: diagonal pairs move together
                limbs[i].localRotation = limbRest[i] * Quaternion.Euler(swing * sign * 34f, 0f, 0f);
            }
            float leap = attackStyle == MoveKind.Pounce && attackTime >= attackWindup ? Mathf.Sin(Mathf.Clamp01((attackTime - attackWindup) / Mathf.Max(0.1f, attackRecovery)) * Mathf.PI) * 0.6f : 0f;
            model.localPosition = modelRest + hitOffset * 0.5f + new Vector3(0f, leap + Mathf.Abs(Mathf.Cos(phase)) * 0.05f * moving, lunge * (attackStyle == MoveKind.Charge ? 0.7f : 0.45f));
            model.localRotation = Quaternion.Euler(lunge * 12f + Mathf.Sin(Time.time * 2f) * 0.8f, 0f, 0f);
            if (head != null) head.localRotation = headRest * Quaternion.Euler(lunge * 18f + Mathf.Sin(Time.time * 1.7f) * 3f, 0f, 0f);
            if (tail != null) tail.localRotation = tailRest * Quaternion.Euler(0f, Mathf.Sin(Time.time * 6f) * 22f, 0f);
        }

        void AnimateFlyer(float moving, float lunge)
        {
            float beat = Mathf.Sin(Time.time * 14f);
            for (int i = 0; i < limbs.Length; i++)
                limbs[i].localRotation = limbRest[i] * Quaternion.Euler(0f, 0f, beat * (i == 0 ? -46f : 46f));
            float hover = hoverHeight + Mathf.Sin(Time.time * 2.4f + transform.position.x) * 0.16f;
            model.localPosition = modelRest + hitOffset * 0.5f + new Vector3(0f, hover - Mathf.Max(0f, lunge) * 0.3f, lunge * 0.7f);
            model.localRotation = Quaternion.Euler(moving * 14f + lunge * 24f, 0f, Mathf.Sin(Time.time * 1.3f) * 4f);
        }

        void AnimateBlob(float moving, float lunge)
        {
            float wobble = Mathf.Sin(phase * 0.6f) * (0.05f + moving * 0.09f);
            float stretch = lunge * 0.28f;
            model.localScale = Vector3.Scale(modelScale, new Vector3(1f + wobble - stretch * 0.5f, 1f - wobble + Mathf.Max(0f, -stretch) * 0.8f, 1f + wobble + Mathf.Max(0f, stretch)));
            model.localPosition = modelRest + hitOffset * 0.3f + new Vector3(0f, 0f, lunge * 0.35f);
        }

        void UpdateDeath()
        {
            float k = Mathf.Clamp01(deathTime / 0.8f);
            float fall = k * k;
            switch (body)
            {
                case EnemyBody.Beast:
                    model.localRotation = Quaternion.Euler(0f, 0f, 88f * fall);
                    model.localPosition = modelRest + Vector3.up * (0.12f * fall);
                    break;
                case EnemyBody.Flyer:
                    model.localRotation = Quaternion.Euler(60f * fall, 0f, 120f * fall);
                    model.localPosition = modelRest + Vector3.up * Mathf.Lerp(hoverHeight, 0.2f, fall);
                    break;
                default:
                    model.localScale = Vector3.Scale(modelScale, new Vector3(1f + fall * 0.5f, Mathf.Lerp(1f, 0.12f, fall), 1f + fall * 0.5f));
                    break;
            }
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
