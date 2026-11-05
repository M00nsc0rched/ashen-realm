using AshenRealm.AI;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.Combat
{
    /// <summary>
    /// The class kit beyond sword and shield: two abilities paid with focus (Graveborne's SP), each on a cooldown because
    /// real time has no turns to spend, plus the potion. Focus trickles back on its own and returns faster with kills.
    /// </summary>
    public sealed class PlayerAbilities : MonoBehaviour
    {
        const float BackstabDamage = 30f;
        const float BackstabBehindMultiplier = 3f;
        const float SmiteDamage = 32f;
        const float DrainDamage = 26f;
        const float MendAmount = 38f;
        const int FocusPerKill = 1;

        [SerializeField] PlayerInputHub inputSource;
        [SerializeField] PlayerCombat combat;
        [SerializeField] Health health;
        [SerializeField] StatusEffects status;
        [SerializeField] Transform aimOrigin;

        IPlayerInput input;
        ClassDefinition definition;
        float focus;
        readonly float[] readyAt = new float[2];

        public float Focus => focus;
        public float MaxFocus => definition != null ? definition.MaxFocus : 1f;
        public float FocusNormalized => focus / MaxFocus;

        void Awake() => input = inputSource;

        void OnEnable() => EnemyAI.Slain += OnEnemySlain;
        void OnDisable() => EnemyAI.Slain -= OnEnemySlain;

        void OnEnemySlain(EnemyAI _) => AddFocus(FocusPerKill);

        public void ApplyClass(ClassDefinition chosen)
        {
            definition = chosen;
            focus = chosen.MaxFocus;
            readyAt[0] = readyAt[1] = 0f;
        }

        public void AddFocus(float amount) => focus = Mathf.Min(MaxFocus, focus + amount);

        /// <summary>0 when the slot is usable, 1 right after use.</summary>
        public float CooldownFraction(int slot)
        {
            AbilityDefinition ability = Slot(slot);
            return ability == null ? 0f : Mathf.Clamp01((readyAt[slot] - Time.time) / ability.Cooldown);
        }

        public bool Affordable(int slot)
        {
            AbilityDefinition ability = Slot(slot);
            return ability != null && focus >= ability.Cost;
        }

        AbilityDefinition Slot(int slot) => definition == null ? null : slot == 0 ? definition.Ability1 : definition.Ability2;

        void Update()
        {
            if (definition == null || !health.IsAlive) return;
            focus = Mathf.Min(MaxFocus, focus + definition.FocusPerSecond * Time.deltaTime);

            for (int slot = 0; slot < 2; slot++)
                if (input.ConsumeAbility(slot)) TryUse(slot);
            if (input.ConsumePotion()) DrinkPotion();
        }

        /// <summary>Spends focus and fires the ability, if it is ready.</summary>
        public bool TryUse(int slot)
        {
            AbilityDefinition ability = Slot(slot);
            if (ability == null) return false;
            if (Time.time < readyAt[slot]) return false;
            if (focus < ability.Cost)
            {
                GameMessages.Notify(GameText.NoFocus);
                return false;
            }

            focus -= ability.Cost;
            readyAt[slot] = Time.time + ability.Cooldown;
            Fire(ability.Effect);
            return true;
        }

        void Fire(AbilityEffect effect)
        {
            switch (effect)
            {
                case AbilityEffect.ShieldBash:
                {
                    combat.PlayAbilitySwing();
                    GameObject foe = combat.Strike(14f, 3f, 0.55f);
                    if (foe != null && foe.TryGetComponent(out StatusEffects s)) s.ApplyStun(2.2f);
                    break;
                }
                case AbilityEffect.Rally:
                    status.ApplyFury(8f);
                    status.ApplyRegen(5f, 6f);
                    GameMessages.Notify(GameText.RallyMessage);
                    break;
                case AbilityEffect.Backstab:
                    combat.PlayAbilitySwing();
                    combat.Strike(BackstabDamage * (FoeFacingAway() ? BackstabBehindMultiplier : 1f), 2.8f, 0.45f);
                    break;
                case AbilityEffect.PoisonBlade:
                    combat.CoatBlade(10f);
                    GameMessages.Notify(GameText.PoisonMessage);
                    break;
                case AbilityEffect.LifeDrain:
                {
                    combat.PlayAbilitySwing();
                    float before = health.Current;
                    GameObject foe = combat.Strike(DrainDamage, 9f, 0.6f);
                    if (foe != null) health.Heal(DrainDamage);
                    if (foe == null && Mathf.Approximately(before, health.Current)) GameMessages.Notify(GameText.NoTarget);
                    break;
                }
                case AbilityEffect.Hex:
                {
                    combat.PlayAbilitySwing();
                    GameObject foe = combat.Strike(6f, 11f, 0.6f);
                    if (foe != null && foe.TryGetComponent(out StatusEffects s)) s.ApplyWeaken(9f);
                    else GameMessages.Notify(GameText.NoTarget);
                    break;
                }
                case AbilityEffect.Smite:
                {
                    combat.PlayAbilitySwing();
                    GameObject foe = FoeInReach(3.4f);
                    bool undead = foe != null && foe.TryGetComponent(out EnemyAI ai) && ai.Definition.Undead;
                    combat.Strike(undead ? SmiteDamage * 2f : SmiteDamage, 3.4f, 0.5f);
                    break;
                }
                case AbilityEffect.Mend:
                    health.Heal(MendAmount);
                    GameMessages.Notify(GameText.MendMessage);
                    break;
            }
        }

        public void DrinkPotion()
        {
            GameSession session = GameSession.Instance;
            if (session == null) return;
            if (session.Potions <= 0)
            {
                GameMessages.Notify(GameText.NoPotion);
                return;
            }
            if (health.Normalized >= 1f)
            {
                GameMessages.Notify(GameText.FullHealth);
                return;
            }
            session.AddPotions(-1);
            health.Heal(GameSession.PotionHeal);
        }

        GameObject FoeInReach(float reach)
        {
            if (!Physics.SphereCast(aimOrigin.position, 0.5f, aimOrigin.forward, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore)) return null;
            var enemy = hit.collider.GetComponentInParent<EnemyAI>();
            return enemy != null ? enemy.gameObject : null;
        }

        /// <summary>True when the foe under the crosshair has its back to the player, or has not noticed yet.</summary>
        bool FoeFacingAway()
        {
            GameObject foe = FoeInReach(3f);
            if (foe == null) return false;
            Vector3 toPlayer = transform.position - foe.transform.position;
            toPlayer.y = 0f;
            return Vector3.Angle(foe.transform.forward, toPlayer) > 110f;
        }
    }
}
