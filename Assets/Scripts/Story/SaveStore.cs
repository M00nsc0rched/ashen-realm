using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.Story
{
    /// <summary>
    /// Reads and writes the roster and the meta record through PlayerPrefs, which the web build keeps in the browser's
    /// IndexedDB. There PlayerPrefs.Save is what makes a write survive the page, so every write ends with it.
    /// </summary>
    public static class SaveStore
    {
        /// <summary>Bump when world geometry moves: older saves then arrive at their zone's entry, not inside a new wall.</summary>
        public const int WorldVersion = 3;

        static string RosterKey => GameSettings.Prefix + "Roster";
        static string MetaKey => GameSettings.Prefix + "Meta";

        public static SoulRoster LoadRoster() => Read(RosterKey, new SoulRoster());
        public static MetaRecord LoadMeta() => Read(MetaKey, new MetaRecord());

        public static void SaveMeta(MetaRecord meta) => Write(MetaKey, meta);

        /// <summary>Replaces the soul with the same id, or adds it, and keeps the most recently played first.</summary>
        public static void Put(SoulRecord soul)
        {
            SoulRoster roster = LoadRoster();
            roster.Souls.RemoveAll(s => s.Id == soul.Id);
            roster.Souls.Insert(0, soul);
            Write(RosterKey, roster);
        }

        public static SoulRecord Find(string id) => LoadRoster().Souls.Find(s => s.Id == id);

        public static void Delete(string id)
        {
            SoulRoster roster = LoadRoster();
            roster.Souls.RemoveAll(s => s.Id == id);
            Write(RosterKey, roster);
        }

        /// <summary>Removes everything under the current prefix (the playtest cleans up with this).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(RosterKey);
            PlayerPrefs.DeleteKey(MetaKey);
            PlayerPrefs.Save();
        }

        static T Read<T>(string key, T fallback) where T : class
        {
            string json = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(json)) return fallback;
            try
            {
                return JsonUtility.FromJson<T>(json) ?? fallback;
            }
            catch (System.ArgumentException e)
            {
                // A damaged save must not lock the player out of the menu.
                Debug.LogWarning($"[AshenRealm] Ignoring unreadable save {key}: {e.Message}");
                return fallback;
            }
        }

        static void Write<T>(string key, T value)
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(value));
            PlayerPrefs.Save();
        }
    }
}
