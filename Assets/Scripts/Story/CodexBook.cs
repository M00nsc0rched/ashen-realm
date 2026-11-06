using System.Collections.Generic;
using System.Linq;
using AshenRealm.Core;

namespace AshenRealm.Story
{
    /// <summary>One line of the Codex.</summary>
    public sealed class CodexEntry
    {
        public string Id;
        public string Section;
        public string Name;
        public string Text;
    }

    /// <summary>
    /// The Codex is built from the game's own tables, so a new foe or encounter appears here without further work and the
    /// total on the main menu is always the true one. Discoveries live in the meta record and outlive every soul.
    /// </summary>
    public static class CodexBook
    {
        public const string KnightId = "foe_knight";

        static List<CodexEntry> entries;

        public static string FoeId(string enemyId) => "foe_" + enemyId;
        public static string EncounterId(string id, bool warped) => "enc_" + id + (warped ? "_warped" : "");
        public static string PersonId(string id) => "npc_" + id;
        public static string PlaceId(int zone) => "zone_" + zone;

        public static IReadOnlyList<CodexEntry> Entries => entries ?? (entries = Build());

        public static CodexEntry Find(string id) => Entries.FirstOrDefault(e => e.Id == id);

        static List<CodexEntry> Build()
        {
            var list = new List<CodexEntry>();
            void Add(string id, string section, string name, string text) =>
                list.Add(new CodexEntry { Id = id, Section = section, Name = name, Text = text });

            Add(KnightId, MenuText.CodexFoes, Capitalized(GameText.KnightName), MenuText.KnightEntry);
            foreach (EnemyDefinition foe in Catalog.Enemies)
            {
                string moves = string.Join(", ", foe.Moves.Select(m => m.Name));
                Add(FoeId(foe.Id), MenuText.CodexFoes, foe.Name, string.Format(MenuText.FoeFormat, MenuText.RankOf(foe.Rank), foe.Health, moves));
            }
            foreach (EncounterDefinition encounter in StoryText.Encounters)
            {
                Add(EncounterId(encounter.Id, false), MenuText.CodexEncounters, encounter.Name, encounter.Clear.Text);
                if (encounter.Warped != null)
                    Add(EncounterId(encounter.Id, true), MenuText.CodexEncounters, encounter.Name + MenuText.WarpedSuffix, encounter.Warped.Text);
            }
            foreach (NpcDefinition person in StoryText.Npcs)
                Add(PersonId(person.Id), MenuText.CodexPeople, person.Name, person.Intro);
            for (int zone = 0; zone < StoryText.ZoneTitles.Length; zone++)
                Add(PlaceId(zone), MenuText.CodexPlaces, Capitalized(StoryText.ZoneTitles[zone]), StoryText.ZoneSubtitles[zone]);
            return list;
        }

        /// <summary>HUD names are written in capitals; the Codex reads like a book.</summary>
        static string Capitalized(string upper)
        {
            string[] words = upper.Split(' ');
            for (int i = 0; i < words.Length; i++)
                words[i] = words[i].Substring(0, 1) + Lower(words[i].Substring(1));
            return string.Join(" ", words);
        }

        // Written out because WebGL has no culture data to rely on for the accented capitals.
        static string Lower(string text)
        {
            const string upper = "ÁÉÍÓÖŐÚÜŰ", lower = "áéíóöőúüű";
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                int accented = upper.IndexOf(chars[i]);
                chars[i] = accented >= 0 ? lower[accented] : char.ToLowerInvariant(chars[i]);
            }
            return new string(chars);
        }
    }
}
