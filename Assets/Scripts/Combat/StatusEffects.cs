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
        float poisonInterval = 1f;
        int poisonTicks = -1;
        float regenPerSecond, regenUntil;
        float stunUntil, weakenUntil, furyUntil, wardUntil;
        float shield, shieldUntil, attackBonus, attackBonusUntil, attackReduction, attackReductionUntil;
        float bleedDps, bleedUntil, nextBleedTick, rotDps, rotUntil, nextRotTick;
        int bleedTicks, rotTicks;
        float charmUntil, drainFraction, drainUntil;
        GameObject poisonSource, bleedSource, rotSource, charmSource;

        public bool Stunned => Time.time < stunUntil;
        public bool Poisoned => poisonTicks > 0 || Time.time < poisonUntil;
        public bool Weakened => Time.time < weakenUntil || AttackReduction > 0f;
        public bool Furious => Time.time < furyUntil;
        public bool Warded => Time.time < wardUntil || ShieldPoints > 0f;
        public bool Bleeding => bleedTicks > 0;
        public bool Rotting => rotTicks > 0;
        public bool Charmed => Time.time < charmUntil;
        public GameObject CharmSource => Charmed ? charmSource : null;
        public float ShieldPoints => Time.time < shieldUntil ? shield : 0f;
        public float AttackBonus => Time.time < attackBonusUntil ? attackBonus : 0f;
        public float AttackReduction => Time.time < attackReductionUntil ? attackReduction : 0f;
        public float DamageMultiplier => (Time.time < weakenUntil ? WeakenedDamage : 1f) * (Furious ? FuryDamage : 1f);
        bool IronPalate => GetComponent<AshenRealm.Player.PlayerProgression>() != null &&
            AshenRealm.Story.GameSession.Instance != null &&
            AshenRealm.Story.GameSession.Instance.Class.Id == AshenRealm.Story.CharacterClass.Alchemist;

        void OnEnable()
        {
            if (poisonTicks > 0 && Time.time > poisonUntil) { poisonTicks = 0; poisonUntil = 0f; }
            if (Time.time > bleedUntil) bleedTicks = 0;
            if (Time.time > rotUntil) rotTicks = 0;
        }

        public void ApplyPoison(float damagePerSecond, float seconds, GameObject source = null)
        {
            if (IronPalate) return;
            bool wasPoisoned = Poisoned;
            if (!wasPoisoned || damagePerSecond >= poisonDps)
            {
                poisonDps = damagePerSecond;
                poisonInterval = 1f;
                poisonTicks = -1;
            }
            poisonUntil = Mathf.Max(poisonUntil, Time.time + seconds);
            poisonSource = source;
            if (!wasPoisoned) nextPoisonTick = Time.time + poisonInterval;
        }

        /// <summary>Workbook damage is paid once per whole turn, including the final tick.</summary>
        public void ApplyTurnPoison(float perTurn, int turns, GameObject source)
        {
            if (IronPalate || turns <= 0 || perTurn <= 0f) return;
            bool wasPoisoned = Poisoned;
            float perSecond = perTurn / AshenRealm.Story.AbilityBook.TurnSeconds;
            if (!wasPoisoned || perSecond >= poisonDps)
            {
                poisonDps = perSecond;
                poisonInterval = AshenRealm.Story.AbilityBook.TurnSeconds;
                poisonTicks = Mathf.Max(wasPoisoned ? poisonTicks : 0, turns);
            }
            poisonUntil = Mathf.Max(poisonUntil, Time.time + turns * AshenRealm.Story.AbilityBook.TurnSeconds);
            poisonSource = source;
            if (!wasPoisoned) nextPoisonTick = Time.time + poisonInterval;
        }

        public void ApplyStun(float seconds) => stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
        public void ApplyWeaken(float seconds) => weakenUntil = Mathf.Max(weakenUntil, Time.time + seconds);
        public void ApplyFury(float seconds) => furyUntil = Mathf.Max(furyUntil, Time.time + seconds);
        public void ApplyWard(float seconds) => wardUntil = Mathf.Max(wardUntil, Time.time + seconds);

        public void ApplyShield(float points, float seconds)
        {
            shield = Mathf.Max(ShieldPoints, Mathf.Max(0f, points));
            shieldUntil = Mathf.Max(shieldUntil, Time.time + seconds);
        }

        public void ApplyAttackBonus(float points, float seconds)
        {
            attackBonus = Mathf.Max(AttackBonus, Mathf.Max(0f, points));
            attackBonusUntil = Mathf.Max(attackBonusUntil, Time.time + seconds);
        }

        public void ApplyAttackReduction(float points, float seconds)
        {
            attackReduction = Mathf.Max(AttackReduction, Mathf.Max(0f, points));
            attackReductionUntil = Mathf.Max(attackReductionUntil, Time.time + seconds);
        }

        public void ApplyStatDrain(float fraction, float seconds)
        {
            drainFraction = Mathf.Max(Time.time < drainUntil ? drainFraction : 0f, Mathf.Clamp01(fraction));
            drainUntil = Mathf.Max(drainUntil, Time.time + seconds);
        }

        public void ApplyCharm(float seconds, GameObject source)
        {
            charmSource = source;
            charmUntil = Mathf.Max(charmUntil, Time.time + seconds);
        }

        public void ApplyBleed(float damagePerSecond, float seconds, GameObject source = null)
        {
            if (IronPalate) return;
            bool wasBleeding = Bleeding;
            bleedDps = Mathf.Max(Bleeding ? bleedDps : 0f, damagePerSecond);
            bleedTicks = Mathf.Max(bleedTicks, Mathf.CeilToInt(seconds));
            bleedUntil = Mathf.Max(bleedUntil, Time.time + seconds);
            bleedSource = source;
            if (!wasBleeding) nextBleedTick = Time.time + 1f;
        }

        public void ApplyRot(float damagePerSecond, float seconds, GameObject source = null)
        {
            if (IronPalate) return;
            bool wasRotting = Rotting;
            rotDps = Mathf.Max(Rotting ? rotDps : 0f, damagePerSecond);
            rotTicks = Mathf.Max(rotTicks, Mathf.CeilToInt(seconds));
            rotUntil = Mathf.Max(rotUntil, Time.time + seconds);
            rotSource = source;
            if (!wasRotting) nextRotTick = Time.time + 1f;
        }

        public void CleansePoison() { poisonUntil = bleedUntil = rotUntil = 0f; poisonTicks = bleedTicks = rotTicks = 0; }

        public float ModifyOutgoing(float amount, bool magic = false) =>
            Mathf.Max(0f, amount + (magic ? 0f : AttackBonus - AttackReduction)) * DamageMultiplier *
            (Time.time < drainUntil ? 1f - drainFraction : 1f);

        public void ApplyRegen(float healthPerSecond, float seconds)
        {
            regenPerSecond = healthPerSecond;
            regenUntil = Time.time + seconds;
        }

        public void Clear()
        {
            poisonUntil = regenUntil = stunUntil = weakenUntil = furyUntil = wardUntil = 0f;
            shieldUntil = attackBonusUntil = attackReductionUntil = bleedUntil = rotUntil = charmUntil = drainUntil = 0f;
            shield = 0f;
            poisonTicks = bleedTicks = rotTicks = 0;
        }

        void Update()
        {
            if (health == null || !health.IsAlive) return;
            if (Time.time < regenUntil) health.Heal(regenPerSecond * Time.deltaTime);
            if (Poisoned && Time.time >= nextPoisonTick)
            {
                nextPoisonTick += poisonInterval;
                health.ApplyDamage(new DamageInfo(poisonDps * poisonInterval, transform.position + Vector3.up, Vector3.zero, poisonSource));
                if (poisonTicks > 0 && --poisonTicks == 0) poisonUntil = Time.time;
            }
            Tick(ref bleedTicks, bleedDps, bleedSource, ref nextBleedTick, ref bleedUntil);
            Tick(ref rotTicks, rotDps, rotSource, ref nextRotTick, ref rotUntil);
        }

        void Tick(ref int remaining, float damage, GameObject source, ref float next, ref float until)
        {
            if (remaining <= 0 || Time.time < next || !health.IsAlive) return;
            next += 1f;
            health.ApplyDamage(new DamageInfo(damage, transform.position + Vector3.up, Vector3.zero, source));
            remaining = Mathf.Max(0, remaining - 1);
            if (remaining == 0) until = Time.time;
        }

        public float FilterDamage(in DamageInfo info, float amount)
        {
            if (Time.time < wardUntil) amount *= WardedDamage;
            float absorbed = Mathf.Min(ShieldPoints, amount);
            shield -= absorbed;
            return amount - absorbed;
        }
    }
}
