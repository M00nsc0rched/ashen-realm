using AshenRealm.AI;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.World;
using UnityEngine;

namespace AshenRealm.Story
{
    /// <summary>
    /// Lives outside the city zone so a watch contract can advance underground. All progress belongs to the current
    /// soul's existing saved flags; killing the same saved enemy or reloading cannot pay a contract twice.
    /// </summary>
    public sealed class CityActivityLedger : MonoBehaviour
    {
        public const int ContractCount = 3;
        const string Prefix = "city_n_";
        bool savePending;
        ZoneManager zones;

        public static CityActivityLedger Instance { get; private set; }
        public int CurrentContract
        {
            get
            {
                GameSession session = GameSession.Instance;
                if (session == null) return 0;
                for (int i = 0; i < ContractCount; i++)
                    if (!session.HasFlag(ContractFlag(i, "paid"))) return i;
                return ContractCount;
            }
        }

        public bool ContractAccepted => GameSession.Instance != null && CurrentContract < ContractCount &&
            GameSession.Instance.HasFlag(ContractFlag(CurrentContract, "accepted"));
        public int ContractKills => CountKills(CurrentContract);
        public static int Goal(int contract) => 3 + contract * 2;
        public static int GoldReward(int contract) => 35 + contract * 20;
        public static int SoulReward(int contract) => 4 + contract * 2;
        public static string ContractFlag(int contract, string suffix) => Prefix + "watch_" + contract + "_" + suffix;
        public static string ServiceFlag(CityActivityKind kind, int descent) => Prefix + "service_" + (int)kind + "_" + descent;

        void Awake()
        {
            Instance = this;
            zones = FindAnyObjectByType<ZoneManager>();
        }
        void OnEnable() => EnemyAI.Slain += OnEnemySlain;
        void OnDisable() => EnemyAI.Slain -= OnEnemySlain;
        void OnDestroy() { if (Instance == this) Instance = null; }

        // StoryDirector records the slain enemy and its ordinary loot in the same event. Save after all listeners finish.
        void LateUpdate()
        {
            if (!savePending) return;
            savePending = false;
            SoulKeeper.Instance?.Save();
        }

        public void Use(CityActivityKind kind, GameObject player)
        {
            GameSession session = GameSession.Instance;
            SoulKeeper keeper = SoulKeeper.Instance;
            if (session == null || !session.ClassChosen || keeper == null || !keeper.HasSoul || player == null) return;
            Health health = player.GetComponent<Health>();
            PlayerAbilities abilities = player.GetComponent<PlayerAbilities>();
            if (health == null || !health.IsAlive) return;
            if (kind == CityActivityKind.BountyBoard) { UseBoard(session, keeper); return; }

            string flag = ServiceFlag(kind, session.Descents);
            if (session.HasFlag(flag)) { GameMessages.Notify(CityText.AlreadyUsed); return; }
            bool healing = kind == CityActivityKind.Well || kind == CityActivityKind.LanternRest;
            if (healing && health.Current >= health.Max && (abilities == null || abilities.Focus >= abilities.MaxFocus))
            { GameMessages.Notify(CityText.NothingToRestore); return; }
            int cost = kind == CityActivityKind.Well ? 3 : kind == CityActivityKind.MarketSupplies ? 30 :
                kind == CityActivityKind.MemorialShrine ? 10 : 8;
            if (!session.TrySpend(cost)) { GameMessages.Notify(CityText.NoGold); return; }
            session.SetFlag(flag);
            switch (kind)
            {
                case CityActivityKind.Well:
                    health.Heal(25f); abilities?.AddFocus(3f); GameMessages.Notify(CityText.WellUsed); break;
                case CityActivityKind.MarketSupplies:
                    session.AddPotions(2); GameMessages.Notify(CityText.MarketUsed); break;
                case CityActivityKind.MemorialShrine:
                    session.AddHonor(8); GameMessages.Notify(CityText.ShrineUsed); break;
                case CityActivityKind.LanternRest:
                    health.Heal(15f); if (abilities != null) abilities.SetFocus(abilities.MaxFocus);
                    GameMessages.Notify(CityText.RestUsed); break;
            }
            keeper.Save();
        }

        void UseBoard(GameSession session, SoulKeeper keeper)
        {
            int contract = CurrentContract;
            if (contract >= ContractCount) { GameMessages.Notify(CityText.ContractsComplete); return; }
            int goal = Goal(contract), gold = GoldReward(contract), souls = SoulReward(contract);
            if (!session.HasFlag(ContractFlag(contract, "accepted")))
            {
                session.SetFlag(ContractFlag(contract, "accepted"));
                GameMessages.Notify(CityText.ContractAccepted(contract + 1, goal, gold, souls));
                keeper.Save();
                return;
            }
            int count = CountKills(contract);
            if (count < goal) { GameMessages.Notify(CityText.ContractProgress(count, goal)); return; }
            session.SetFlag(ContractFlag(contract, "paid"));
            session.AddGold(gold);
            keeper.AddSouls(souls);
            keeper.Save();
            GameMessages.Notify(CityText.ContractReward(gold, souls));
        }

        int CountKills(int contract)
        {
            if (contract >= ContractCount || GameSession.Instance == null) return 0;
            string prefix = ContractFlag(contract, "kill_");
            int count = 0;
            foreach (string flag in GameSession.Instance.Flags)
                if (flag.StartsWith(prefix, System.StringComparison.Ordinal)) count++;
            return count;
        }

        void OnEnemySlain(EnemyAI enemy)
        {
            GameSession session = GameSession.Instance;
            if (session == null || !session.ClassChosen || !ContractAccepted || enemy == null || string.IsNullOrEmpty(enemy.SaveKey) ||
                zones == null || zones.CurrentIndex < ZoneNumbers.FirstDepth ||
                zones.CurrentIndex >= ZoneNumbers.FirstDepth + ZoneNumbers.DepthCount) return;
            int contract = CurrentContract;
            if (CountKills(contract) >= Goal(contract)) return;
            string kill = ContractFlag(contract, "kill_" + enemy.SaveKey);
            if (session.HasFlag(kill)) return;
            session.SetFlag(kill);
            int count = CountKills(contract);
            GameMessages.Notify(count >= Goal(contract) ? CityText.ContractReady(GoldReward(contract), SoulReward(contract)) :
                CityText.ContractProgress(count, Goal(contract)));
            savePending = true;
        }
    }
}
