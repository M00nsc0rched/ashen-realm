using System;
using System.Collections.Generic;
using System.Linq;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Player;
using AshenRealm.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshenRealm.Story
{
    /// <summary>
    /// Keeps souls between sittings. A save happens on every arrival, after each guardian and on demand; loading always
    /// starts from a fresh scene, so what a soul has cleared only ever has to be taken away, never put back. The main menu
    /// is a fresh scene already; anywhere else a load reloads the scene and finishes on the other side.
    /// </summary>
    public sealed class SoulKeeper : MonoBehaviour
    {
        const int SoulsPerDiscovery = 4;

        // Graveborne's payouts per rank.
        static readonly int[] SoulsByRank = { 2, 6, 10, 15 };

        [SerializeField] GameSession session;
        [SerializeField] ZoneManager zones;
        [SerializeField] FirstPersonController player;
        [SerializeField] Health health;
        [SerializeField] PlayerAbilities abilities;

        /// <summary>The soul to continue once the scene has reloaded.</summary>
        static string pendingSoulId;

        string soulId;
        int lastZone;
        MetaRecord meta;

        /// <summary>A soul is in play (false on the main menu).</summary>
        public bool HasSoul => soulId != null;
        public static SoulKeeper Instance { get; private set; }
        public MetaRecord Meta => meta;

        /// <summary>Raised when a saved soul has been put back in the world.</summary>
        public event Action Loaded;

        void Awake()
        {
            Instance = this;
            meta = SaveStore.LoadMeta();
            if (meta.UnlockedClasses == null) meta.UnlockedClasses = new List<int>();
        }

        public bool IsClassUnlocked(CharacterClass id) => (int)id < 4 || meta.UnlockedClasses.Contains((int)id);

        public void UnlockClass(CharacterClass id)
        {
            if (meta.UnlockedClasses.Contains((int)id)) return;
            meta.UnlockedClasses.Add((int)id);
            SaveStore.SaveMeta(meta);
        }

        void Start() => zones.Arrived += OnArrived;

        void OnApplicationFocus(bool hasFocus)
        {
            // The last chance a browser tab or a phone gives before it may be closed.
            if (!hasFocus) Save();
        }

        /// <summary>Hands over the soul a reload was for, once.</summary>
        public static string TakePendingSoul()
        {
            string id = pendingSoulId;
            pendingSoulId = null;
            return id;
        }

        /// <summary>The freshly chosen class becomes a soul in the roster.</summary>
        public void Begin()
        {
            soulId = Guid.NewGuid().ToString("N");
            lastZone = zones.CurrentIndex;
            Discover(CodexBook.PlaceId(lastZone));
            Save();
        }

        /// <summary>Writes the soul in play. False when there is none.</summary>
        public bool Save()
        {
            if (soulId == null) return false;
            SaveStore.Put(Capture());
            return true;
        }

        /// <summary>The soul "Betöltés" would continue: the one in play, else the most recently played.</summary>
        public SoulRecord LatestSoul()
        {
            if (soulId != null) return SaveStore.Find(soulId);
            List<SoulRecord> souls = SaveStore.LoadRoster().Souls;
            return souls.Count > 0 ? souls[0] : null;
        }

        /// <summary>Continues a saved soul, from wherever the player is now.</summary>
        public void Resume(SoulRecord soul)
        {
            if (soulId == null)
            {
                Load(soul);
                return;
            }
            pendingSoulId = soul.Id;
            ReloadScene();
        }

        /// <summary>Saves and leaves for the main menu: the scene comes back fresh and opens on it.</summary>
        public void ReturnToMenu()
        {
            Save();
            pendingSoulId = null;
            ReloadScene();
        }

        /// <summary>Puts a saved soul into the fresh world.</summary>
        public void Load(SoulRecord soul)
        {
            soulId = soul.Id;
            lastZone = soul.Zone;
            session.Restore(soul);
            player.GetComponent<PlayerProgression>().Restore(soul.Progress);

            foreach (ISavedState state in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).OfType<ISavedState>())
                if (session.HasFlag(state.SaveKey)) state.ApplyCleared();

            zones.Place(soul.Zone, new Vector3(soul.X, soul.Y, soul.Z), soul.Yaw, soul.WorldVersion == SaveStore.WorldVersion);
            health.SetCurrent(soul.Health);
            abilities.SetFocus(soul.Focus);
            Loaded?.Invoke();
        }

        /// <summary>First time something is met: a Codex line and a few Souls, Graveborne's reward for curiosity.</summary>
        public void Discover(string codexId)
        {
            if (meta.Codex.Contains(codexId)) return;
            meta.Codex.Add(codexId);
            meta.Souls += SoulsPerDiscovery;
            SaveStore.SaveMeta(meta);
            CodexEntry entry = CodexBook.Find(codexId);
            if (entry != null) GameMessages.Notify(string.Format(MenuText.CodexFound, entry.Name));
        }

        /// <summary>What the Choir's ledger holds: the one currency that outlives a soul.</summary>
        public int Souls => meta.Souls;

        public void AddSouls(int amount)
        {
            meta.Souls = Mathf.Max(0, meta.Souls + amount);
            SaveStore.SaveMeta(meta);
        }

        public bool TrySpendSouls(int amount)
        {
            if (amount < 0 || meta.Souls < amount) return false;
            AddSouls(-amount);
            return true;
        }

        public void GainSouls(EnemyRank rank)
        {
            meta.Souls += SoulsByRank[(int)rank];
            SaveStore.SaveMeta(meta);
        }

        void OnArrived(Zone zone)
        {
            int index = zones.CurrentIndex;
            Discover(CodexBook.PlaceId(index));
            if (index == ZoneNumbers.FirstDepth && lastZone == ZoneNumbers.Tavern)
            {
                session.AddDescent();
                session.ClearFlag(StoryDirector.BlessedFlag);
                meta.Descents++;
            }
            if (index >= ZoneNumbers.FirstDepth && index < ZoneNumbers.FirstDepth + ZoneNumbers.DepthCount)
                meta.Deepest = Mathf.Max(meta.Deepest, index - ZoneNumbers.FirstDepth + 1);
            lastZone = index;
            SaveStore.SaveMeta(meta);
            Save();
        }

        SoulRecord Capture()
        {
            Vector3 position = player.transform.position;
            return new SoulRecord
            {
                Id = soulId,
                ClassId = (int)session.Class.Id,
                SavedAt = DateTime.UtcNow.Ticks,
                WorldVersion = SaveStore.WorldVersion,
                Honor = session.Honor,
                Gold = session.Gold,
                Potions = session.Potions,
                WeaponLevel = session.WeaponLevel,
                Descents = session.Descents,
                WeaponId = session.Weapon != null ? session.Weapon.Id : null,
                RelicId = session.Relic != null ? session.Relic.Id : null,
                Zone = zones.CurrentIndex,
                X = position.x,
                Y = position.y,
                Z = position.z,
                Yaw = player.transform.eulerAngles.y,
                Health = health.IsAlive ? health.Current : health.Max,
                Focus = abilities.Focus,
                Progress = player.GetComponent<PlayerProgression>().Capture(),
                Flags = session.Flags.ToList(),
            };
        }

        static void ReloadScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
