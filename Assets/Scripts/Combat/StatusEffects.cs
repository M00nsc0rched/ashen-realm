using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>
    /// Timed conditions on a player or an enemy: Graveborne's poison, stun, weaken, fury, regen and ward, counted in
    /// seconds instead of turns. Also the damage filter for the ward, so it stacks with a raised shield.
    /// </summary>
    public sealed class StatusEffects : MonoBehaviour, IDamageFilter
    {
        const float WeakenedDamage = 0.65f;
        const float FuryDamage = 1.4f;
        const float WardedDamage = 0.5f;

        [SerializeField] Health health;

        float poisonDps, poisonUntil, nextPoisonTick;
        float regenPerSecond, regenUntil;
        float stunUntil, weakenUntil, furyUntil, wardUntil;

        public bool Stunned => Time.time < stunUntil;
        public bool Poisoned => Time.time < poisonUntil;
        public bool Weakened => Time.time < weakenUntil;
        public bool Furious => Time.time < furyUntil;
        public bool Warded => Time.time < wardUntil;
        public float DamageMultiplier => (Weakened ? WeakenedDamage : 1f) * (Furious ? FuryDamage : 1f);

        public void ApplyPoison(float damagePerSecond, float seconds)
        {
            if (GetComponent<AshenRealm.Player.PlayerProgression>() != null &&
                AshenRealm.Story.GameSession.Instance.Class.Id == AshenRealm.Story.CharacterClass.Alchemist) return;
            poisonDps = Mathf.Max(Poisoned ? poisonDps : 0f, damagePerSecond);
            poisonUntil = Mathf.Max(poisonUntil, Time.time + seconds);
            if (nextPoisonTick < Time.time) nextPoisonTick = Time.time + 1f;
        }

        public void ApplyStun(float seconds) => stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
        public void ApplyWeaken(float seconds) => weakenUntil = Mathf.Max(weakenUntil, Time.time + seconds);
        public void ApplyFury(float seconds) => furyUntil = Mathf.Max(furyUntil, Time.time + seconds);
        public void ApplyWard(float seconds) => wardUntil = Mathf.Max(wardUntil, Time.time + seconds);

        public void ApplyRegen(float healthPerSecond, float seconds)
        {
            regenPerSecond = healthPerSecond;
            regenUntil = Time.time + seconds;
        }

        public void Clear() => poisonUntil = regenUntil = stunUntil = weakenUntil = furyUntil = wardUntil = 0f;

        void Update()
        {
            if (health == null || !health.IsAlive) return;
            if (Time.time < regenUntil) health.Heal(regenPerSecond * Time.deltaTime);
            if (Poisoned && Time.time >= nextPoisonTick)
            {
                nextPoisonTick = Time.time + 1f;
                health.ApplyDamage(new DamageInfo(poisonDps, transform.position + Vector3.up, Vector3.zero, null));
            }
        }

        public float FilterDamage(in DamageInfo info, float amount) => Warded ? amount * WardedDamage : amount;
    }
}
