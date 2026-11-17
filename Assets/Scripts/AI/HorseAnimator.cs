using AshenRealm.Player;
using UnityEngine;

namespace AshenRealm.AI
{
    /// <summary>
    /// Procedural animation of the riding horse, purely visual: standing it breathes, flicks its ears and swishes its tail;
    /// ridden, its legs follow the rider's speed and step cycle, a paired canter blending into a gallop with a rocking body.
    /// The pivots are in the model's own frame (front -Z), where a positive turn about X swings a leg forward.
    /// </summary>
    public sealed class HorseAnimator : MonoBehaviour
    {
        // Leg order: front left, front right, hind left, hind right.
        static readonly float[] CanterOffsets = { 0f, 0.5f, 0.5f, 0f };
        static readonly float[] GallopOffsets = { 0.55f, 0.65f, 0f, 0.1f };
        const float StrideSteps = 1.3f;

        [SerializeField] RidingHorse horse;
        [Tooltip("The body: bobs and rocks, carrying the neck, tail and legs.")]
        [SerializeField] Transform frame;
        [SerializeField] Transform neck;
        [SerializeField] Transform ears;
        [SerializeField] Transform tail;
        [SerializeField] Transform[] legs = new Transform[4];

        Quaternion neckRest, earsRest, tailRest;
        Quaternion[] legRest;
        Vector3 frameRest;
        float phase, lastStep, moving, gallop;
        float nextFlick, flickTime = -1f, nextSwish, swishTime = -1f;

        void Awake()
        {
            neckRest = neck.localRotation;
            earsRest = ears.localRotation;
            tailRest = tail.localRotation;
            frameRest = frame.localPosition;
            legRest = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++) legRest[i] = legs[i].localRotation;
            nextFlick = Time.time + Random.Range(1f, 4f);
            nextSwish = Time.time + Random.Range(2f, 5f);
        }

        void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            FirstPersonController rider = horse.Rider;
            float speed = rider != null ? new Vector3(rider.Velocity.x, 0f, rider.Velocity.z).magnitude : 0f;
            float step = rider != null ? rider.StepCycle : lastStep;
            moving = Mathf.MoveTowards(moving, Mathf.Clamp01(speed / 3f), dt * 3f);
            gallop = Mathf.MoveTowards(gallop, Mathf.InverseLerp(8.5f, 11.5f, speed), dt * 2f);
            phase = Mathf.Repeat(phase + Mathf.Max(0f, step - lastStep) / (StrideSteps * Mathf.Lerp(1f, 1.6f, gallop)), 1f);
            lastStep = step;
            float cycle = phase * Mathf.PI * 2f;

            float swing = Mathf.Lerp(26f, 38f, gallop) * moving;
            for (int i = 0; i < legs.Length; i++)
            {
                float offset = Mathf.Lerp(CanterOffsets[i], GallopOffsets[i], gallop);
                legs[i].localRotation = legRest[i] * Quaternion.Euler(swing * Mathf.Sin(cycle + offset * Mathf.PI * 2f), 0f, 0f);
            }

            // The body only ever rises from its rest, so the hooves never sink; at a gallop it rocks once a stride.
            float breath = (Mathf.Sin(t * 1.7f) + 1f) * 0.005f * (1f - moving);
            float bounce = Mathf.Abs(Mathf.Sin(cycle * (gallop > 0.5f ? 1f : 2f))) * Mathf.Lerp(0.035f, 0.07f, gallop) * moving;
            frame.localPosition = frameRest + Vector3.up * (breath + bounce);
            frame.localRotation = Quaternion.Euler(Mathf.Sin(cycle) * 2.5f * gallop * moving, 0f, 0f);

            float nod = Mathf.Sin(cycle * (gallop > 0.5f ? 1f : 2f) + 0.6f) * Mathf.Lerp(3f, 7f, gallop) * moving;
            neck.localRotation = neckRest * Quaternion.Euler(nod + Mathf.Sin(t * 0.6f) * 1.5f * (1f - moving), 0f, 0f);

            ears.localRotation = earsRest * Quaternion.Euler(Flick(t, ref nextFlick, ref flickTime, 0.35f, 2.5f, 7f) * 28f, 0f, 0f);
            float swish = Flick(t, ref nextSwish, ref swishTime, 1.1f, 3f, 8f);
            tail.localRotation = tailRest * Quaternion.Euler(-moving * Mathf.Lerp(15f, 35f, gallop),
                Mathf.Sin(swish * Mathf.PI * 3f) * 22f * swish * (1f - moving * 0.6f), 0f);
        }

        /// <summary>A short 0-1-0 movement now and then, at random intervals.</summary>
        static float Flick(float t, ref float next, ref float started, float length, float minGap, float maxGap)
        {
            if (started < 0f && t >= next) started = t;
            if (started < 0f) return 0f;
            float k = (t - started) / length;
            if (k < 1f) return Mathf.Sin(k * Mathf.PI);
            started = -1f;
            next = t + Random.Range(minGap, maxGap);
            return 0f;
        }
    }
}
