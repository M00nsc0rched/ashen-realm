using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.AI
{
    /// <summary>
    /// Procedural animation for every humanoid rig: walk cycle, overhead chop, hit flinch,
    /// blocked recoil and death fall. Pure presentation — timing comes from <see cref="CursedKnightAI"/>.
    /// </summary>
    public sealed class KnightAnimator : MonoBehaviour, ICreatureAnimator
    {
        [Header("Rig")]
        [SerializeField] Transform model;
        [SerializeField] Transform torso;
        [SerializeField] Transform head;
        [SerializeField] Transform armLeft;
        [SerializeField] Transform armRight;
        [SerializeField] Transform legLeft;
        [SerializeField] Transform legRight;
        [SerializeField, Optional] Transform cape;
        [SerializeField, Optional] Renderer eyes;

        [Header("Walk")]
        [SerializeField] float strideFrequency = 1.5f;
        [SerializeField] float legSwing = 30f;
        [SerializeField] float armSwing = 16f;

        Quaternion torsoRest, headRest, armLeftRest, armRightRest, legLeftRest, legRightRest, capeRest;
        Vector3 torsoRestPos;
        float locomotion, walkPhase;
        float attackTime = -1f, attackWindup = 0.6f, attackRecovery = 0.5f;
        float recoilTime = -1f;
        float deathTime = -1f;
        Vector3 hitAngles, hitVelocity;

        void Awake()
        {
            torsoRest = torso.localRotation;
            torsoRestPos = torso.localPosition;
            headRest = head.localRotation;
            armLeftRest = armLeft.localRotation;
            armRightRest = armRight.localRotation;
            legLeftRest = legLeft.localRotation;
            legRightRest = legRight.localRotation;
            if (cape != null) capeRest = cape.localRotation;
        }

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

        public void PlayHit(Vector3 worldDirection)
        {
            Vector3 local = transform.InverseTransformDirection(worldDirection);
            hitVelocity += new Vector3(local.z * 260f, 0f, -local.x * 200f);
        }

        public void PlayDeath()
        {
            deathTime = 0f;
            attackTime = -1f;
            recoilTime = -1f;
            if (eyes != null) eyes.enabled = false;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            if (deathTime >= 0f)
            {
                UpdateDeath(dt);
                return;
            }

            // Walk cycle.
            float moving = Mathf.Clamp01(locomotion * 1.5f);
            walkPhase += dt * strideFrequency * Mathf.PI * 2f * Mathf.Lerp(0.7f, 1.5f, locomotion) * (moving > 0.01f ? 1f : 0f);
            float swing = Mathf.Sin(walkPhase) * moving;
            float breathe = Mathf.Sin(Time.time * 1.4f);

            legLeft.localRotation = legLeftRest * Quaternion.Euler(swing * legSwing, 0f, 0f);
            legRight.localRotation = legRightRest * Quaternion.Euler(-swing * legSwing, 0f, 0f);
            armLeft.localRotation = armLeftRest * Quaternion.Euler(-swing * armSwing, 0f, 0f);

            // Hit flinch spring.
            hitVelocity += (-180f * hitAngles - 2f * 0.5f * Mathf.Sqrt(180f) * hitVelocity) * dt;
            hitAngles += hitVelocity * dt;

            Vector3 torsoEuler = new Vector3(4f * moving + breathe * 1.5f, 0f, 0f) + hitAngles;
            Vector3 armRightEuler = new Vector3(swing * armSwing * 0.4f, 0f, 0f);
            Vector3 headEuler = new Vector3(breathe * 2f, 0f, 0f) - hitAngles * 0.5f;

            if (attackTime >= 0f) EvaluateAttack(dt, ref torsoEuler, ref armRightEuler);
            else if (recoilTime >= 0f) EvaluateRecoil(dt, ref torsoEuler, ref armRightEuler);

            torso.localRotation = torsoRest * Quaternion.Euler(torsoEuler);
            torso.localPosition = torsoRestPos + Vector3.up * (Mathf.Abs(Mathf.Cos(walkPhase)) * 0.05f * moving + breathe * 0.008f);
            armRight.localRotation = armRightRest * Quaternion.Euler(armRightEuler);
            head.localRotation = headRest * Quaternion.Euler(headEuler);
            if (cape != null)
                cape.localRotation = capeRest * Quaternion.Euler(10f * moving + Mathf.Sin(Time.time * 2.3f) * 3f, 0f, 0f);
        }

        void EvaluateAttack(float dt, ref Vector3 torsoEuler, ref Vector3 armEuler)
        {
            attackTime += dt;
            const float strikeTime = 0.14f;
            float raise, lean, twist;
            if (attackTime < attackWindup)
            {
                // Raise the blade overhead and lean back.
                float k = Ease(attackTime / attackWindup);
                raise = Mathf.Lerp(0f, -195f, k);
                lean = Mathf.Lerp(0f, -12f, k);
                twist = Mathf.Lerp(0f, -18f, k);
            }
            else if (attackTime < attackWindup + strikeTime)
            {
                // Chop down hard.
                float k = (attackTime - attackWindup) / strikeTime;
                k *= k;
                raise = Mathf.Lerp(-195f, -55f, k);
                lean = Mathf.Lerp(-12f, 16f, k);
                twist = Mathf.Lerp(-18f, 10f, k);
            }
            else
            {
                float k = Ease((attackTime - attackWindup - strikeTime) / Mathf.Max(0.01f, attackRecovery - strikeTime));
                raise = Mathf.Lerp(-55f, 0f, k);
                lean = Mathf.Lerp(16f, 0f, k);
                twist = Mathf.Lerp(10f, 0f, k);
                if (k >= 1f) attackTime = -1f;
            }
            armEuler = new Vector3(raise, 0f, 0f);
            torsoEuler += new Vector3(lean, twist, 0f);
        }

        void EvaluateRecoil(float dt, ref Vector3 torsoEuler, ref Vector3 armEuler)
        {
            recoilTime += dt;
            const float duration = 0.7f;
            float k = recoilTime / duration;
            if (k >= 1f)
            {
                recoilTime = -1f;
                return;
            }
            float w = Mathf.Sin(k * Mathf.PI);
            armEuler = new Vector3(-110f * w, 0f, -20f * w);
            torsoEuler += new Vector3(-14f * w, 12f * w, 0f);
        }

        void UpdateDeath(float dt)
        {
            deathTime += dt;
            const float fallDuration = 0.9f;
            float k = Mathf.Clamp01(deathTime / fallDuration);
            // Accelerating fall with a small settle bounce at the end.
            float fall = k * k;
            float bounce = deathTime > fallDuration ? Mathf.Exp(-(deathTime - fallDuration) * 9f) * Mathf.Sin((deathTime - fallDuration) * 22f) * 3f : 0f;
            model.localRotation = Quaternion.Euler(-88f * fall + bounce, 0f, 8f * fall);
            model.localPosition = new Vector3(0f, 0.15f * fall, 0f);

            float limp = Ease(k);
            armLeft.localRotation = armLeftRest * Quaternion.Euler(-60f * limp, 0f, -35f * limp);
            armRight.localRotation = armRightRest * Quaternion.Euler(-80f * limp, 0f, 40f * limp);
            legLeft.localRotation = legLeftRest * Quaternion.Euler(-10f * limp, 0f, -6f * limp);
            legRight.localRotation = legRightRest * Quaternion.Euler(8f * limp, 0f, 6f * limp);
            head.localRotation = headRest * Quaternion.Euler(-20f * limp, 25f * limp, 0f);
            torso.localRotation = torsoRest;
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
