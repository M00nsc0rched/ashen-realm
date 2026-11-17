using System.Collections.Generic;
using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Story;
using AshenRealm.World;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>Class passives driven by real combat events, with descent-scoped state owned by progression.</summary>
    public sealed class PlayerPassives : MonoBehaviour, IDamageFilter
    {
        const float CombatGrace = 3f;
        const float VigilDuration = 2f * AbilityBook.TurnSeconds;
        readonly HashSet<ICombatant> combatOpponents = new HashSet<ICombatant>();
        readonly HashSet<EnemyAI> creditedEnemies = new HashSet<EnemyAI>();
        readonly HashSet<CursedKnightAI> creditedKnights = new HashSet<CursedKnightAI>();
        Health health;
        StatusEffects status;
        PlayerCombat combat;
        PlayerProgression progression;
        GameSession boundSession;
        ZoneManager zones;
        PlayerLifecycle lifecycle;
        int descent = -1;
        CharacterClass playerClass;
        float lastCombatActivity = -999f;
        bool inCombat, knightOpeningUsed, savePending;

        GameSession Session => GameSession.Instance;
        bool Ready => Session != null && progression != null && health != null;
        bool RogueCritical => Ready && Session.Class.Id == CharacterClass.Gravethief && progression.PassiveState.RogueLuck == 2;
        public float CriticalMultiplier => RogueCritical ? 3f : AbilityBook.CriticalMultiplier;

        void Awake()
        {
            health = GetComponent<Health>();
            status = GetComponent<StatusEffects>();
            combat = GetComponent<PlayerCombat>();
            progression = GetComponent<PlayerProgression>();
            lifecycle = GetComponent<PlayerLifecycle>();
        }

        void OnEnable()
        {
            if (health != null) { health.Damaged += OnDamaged; health.Died += OnDied; }
            if (combat != null) combat.HitLanded += OnHitLanded;
            if (lifecycle != null) lifecycle.Respawned += ResetCombat;
            EnemyAI.Slain += OnEnemySlain;
            CursedKnightAI.SlainWithSource += OnKnightSlain;
            if (zones != null) zones.Arrived += OnZoneArrived;
            BindSession();
        }

        void Start()
        {
            BindSession();
            zones = FindFirstObjectByType<ZoneManager>();
            if (zones != null) zones.Arrived += OnZoneArrived;
        }

        void OnDisable()
        {
            if (health != null) { health.Damaged -= OnDamaged; health.Died -= OnDied; }
            if (combat != null) combat.HitLanded -= OnHitLanded;
            if (lifecycle != null) lifecycle.Respawned -= ResetCombat;
            EnemyAI.Slain -= OnEnemySlain;
            CursedKnightAI.SlainWithSource -= OnKnightSlain;
            if (boundSession != null) boundSession.ClassSelected -= OnClassSelected;
            boundSession = null;
            if (zones != null) zones.Arrived -= OnZoneArrived;
            FlushSave();
        }

        void BindSession()
        {
            if (boundSession != null || Session == null) return;
            boundSession = Session;
            playerClass = Session.Class.Id;
            boundSession.ClassSelected += OnClassSelected;
        }

        void OnClassSelected(ClassDefinition selected)
        {
            playerClass = selected.Id;
            descent = -1;
            creditedEnemies.Clear();
            creditedKnights.Clear();
            ResetCombat();
            // A class swap/load must not carry the old class's temporary shield, fury or bleeding.
            status?.Clear();
        }

        void OnZoneArrived(Zone _) => EndCombat();

        void ResetCombat()
        {
            inCombat = knightOpeningUsed = false;
            combatOpponents.Clear();
            lastCombatActivity = -999f;
        }

        void Update()
        {
            if (!Ready) return;
            BindSession();
            progression.SyncDescentState();
            SyncScope();
            if (!health.IsAlive) return;
            ICombatant focus = CombatFocus.Current;
            if (ValidOpponent(focus, true)) TouchCombat(focus);
            else
            {
                foreach (ICombatant opponent in combatOpponents)
                    if (ValidOpponent(opponent, true)) { lastCombatActivity = Time.time; break; }
                if (inCombat && Time.time - lastCombatActivity > CombatGrace) EndCombat();
            }

            PassiveProgressRecord passive = progression.PassiveState;
            if (Session.Class.Id != CharacterClass.Warden || !passive.VigilActive) return;
            // Remaining time is the saved value itself, so loading never grants another four seconds.
            passive.VigilRemaining = Mathf.Max(0f, passive.VigilRemaining - Time.deltaTime);
            if (passive.VigilRemaining > 0f) return;
            passive.VigilActive = false;
            savePending = true;
            health.ApplyDamage(new DamageInfo(health.Current + health.Max, transform.position + Vector3.up,
                Vector3.zero, null, bypassMitigation: true));
        }

        void LateUpdate() => FlushSave();
        void SyncScope()
        {
            if (descent == Session.Descents && playerClass == Session.Class.Id) return;
            descent = Session.Descents;
            playerClass = Session.Class.Id;
            creditedEnemies.Clear();
            creditedKnights.Clear();
            ResetCombat();
        }
        void FlushSave()
        {
            if (!savePending) return;
            savePending = false;
            SoulKeeper.Instance?.Save();
        }

        void EndCombat()
        {
            ResetCombat();
            if (Ready && Session.Class.Id == CharacterClass.Gravethief)
            {
                progression.PassiveState.RogueLuck = 0;
                savePending = true;
            }
        }

        static bool ValidOpponent(ICombatant opponent, bool requireEngaged)
        {
            var component = opponent as Component;
            if (component == null || !component.gameObject.activeInHierarchy || opponent.Health == null || !opponent.Health.IsAlive ||
                (requireEngaged && !opponent.IsEngaged)) return false;
            if (opponent is EnemyAI enemy)
            {
                var conditions = enemy.GetComponent<StatusEffects>();
                return conditions == null || !conditions.Charmed;
            }
            return opponent is CursedKnightAI;
        }

        void TouchCombat(ICombatant opponent)
        {
            if (!Ready || !ValidOpponent(opponent, false)) return;
            if (!inCombat)
            {
                inCombat = true;
                if (Session.Class.Id == CharacterClass.Gravethief && progression.PassiveState.RogueLuck == 0)
                {
                    progression.PassiveState.RogueLuck = Random.Range(1, 3);
                    savePending = true;
                }
            }
            lastCombatActivity = Time.time;
            combatOpponents.Add(opponent);
            bool undead = opponent is CursedKnightAI || opponent is EnemyAI foe && foe.Definition != null && foe.Definition.Undead;
            if (Session.Class.Id == CharacterClass.Knight && undead && !knightOpeningUsed)
            {
                knightOpeningUsed = true;
                status?.ApplyShield(progression.Defense * 2f, 3f * AbilityBook.TurnSeconds);
                status?.ApplyAttackBonus(5f, 3f * AbilityBook.TurnSeconds);
            }
        }

        static ICombatant Opponent(GameObject source)
        {
            if (source == null) return null;
            EnemyAI enemy = source.GetComponentInParent<EnemyAI>();
            if (enemy != null) return enemy;
            return source.GetComponentInParent<CursedKnightAI>();
        }

        void OnDamaged(DamageInfo info, float amount)
        {
            if (amount > 0f) TouchCombat(Opponent(info.Source));
        }

        void OnDied(Health _)
        {
            ResetCombat();
            if (!Ready) return;
            PassiveProgressRecord passive = progression.PassiveState;
            passive.RogueLuck = 0;
            passive.VigilActive = false;
            passive.VigilRemaining = 0f;
            savePending = true;
        }

        public float ModifyBasicDamage(float amount)
        {
            if (!Ready) return amount;
            ICombatant opponent = CombatFocus.Current;
            if (ValidOpponent(opponent, true)) TouchCombat(opponent);
            return RogueCritical && Random.value < 0.1f ? amount * CriticalMultiplier : amount;
        }

        void OnHitLanded(IDamageable victim, float amount)
        {
            if (!Ready || amount <= 0f || !(victim is Component component)) return;
            ICombatant opponent = Opponent(component.gameObject);
            TouchCombat(opponent);
            if (Session.Class.Id != CharacterClass.Gravethief || progression.PassiveState.RogueLuck != 1) return;
            StatusEffects victimStatus = component.GetComponentInParent<StatusEffects>();
            if (victim.IsAlive && victimStatus != null && (opponent == null || ValidOpponent(opponent, false)))
                victimStatus.ApplyBleed(3f / AbilityBook.TurnSeconds, 3f * AbilityBook.TurnSeconds, gameObject);
        }

        bool IsPlayerSource(GameObject source) => source != null && (source == gameObject || source.transform.IsChildOf(transform));

        void OnEnemySlain(EnemyAI enemy)
        {
            if (!Ready || !health.IsAlive || enemy == null || !IsPlayerSource(enemy.LastDamageSource)) return;
            progression.SyncDescentState();
            SyncScope();
            if (!creditedEnemies.Add(enemy)) return;
            RegisterKill(enemy.SaveKey);
            if (Session.Class.Id == CharacterClass.Witch) progression.GrantAccretion();
            if (Session.Class.Id == CharacterClass.Alchemist)
            {
                Zone zone = enemy.GetComponentInParent<Zone>();
                bool underground = zone != null ? zone.Underground : zones != null && zones.Current != null && zones.Current.Underground;
                if (underground) progression.AddHerbs(enemy.Definition != null && enemy.Definition.Rank >= EnemyRank.Elite ? 2 : 1);
            }
        }

        void OnKnightSlain(CursedKnightAI knight)
        {
            if (!Ready || !health.IsAlive || knight == null || !IsPlayerSource(knight.LastDamageSource)) return;
            progression.SyncDescentState();
            SyncScope();
            if (!creditedKnights.Add(knight)) return;
            RegisterKill(knight.SaveKey);
            if (Session.Class.Id == CharacterClass.Witch) progression.GrantAccretion();
        }

        void RegisterKill(string saveKey)
        {
            lastCombatActivity = Time.time;
            PassiveProgressRecord passive = progression.PassiveState;
            if (!passive.VigilActive || (!string.IsNullOrEmpty(passive.VigilTarget) && passive.VigilTarget != saveKey)) return;
            passive.VigilActive = false;
            passive.VigilRemaining = 0f;
            savePending = true;
        }

        public float FilterDamage(in DamageInfo info, float amount)
        {
            if (!Ready || Session.Class.Id != CharacterClass.Warden || !health.IsAlive || amount < health.Current || info.BypassMitigation) return amount;
            progression.SyncDescentState();
            PassiveProgressRecord passive = progression.PassiveState;
            if (passive.VigilUsed) return amount;
            passive.VigilUsed = passive.VigilActive = true;
            passive.VigilRemaining = VigilDuration;
            ICombatant attacker = Opponent(info.Source);
            passive.VigilTarget = attacker is EnemyAI enemy ? enemy.SaveKey : attacker is CursedKnightAI knight ? knight.SaveKey : string.Empty;
            savePending = true;
            TouchCombat(attacker);
            return Mathf.Max(0f, health.Current - 1f);
        }
    }
}
