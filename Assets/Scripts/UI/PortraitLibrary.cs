using System;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.UI
{
    /// <summary>
    /// The painted faces of Hollowgate's people. Each has three, named person_mood: wary, warm and hostile, which are the
    /// three ways Graveborne says the city can look at a soul. The two cut from their rooms' paintings have one whatever
    /// the soul's honor, and someone without a painting simply talks without a face.
    /// </summary>
    public sealed class PortraitLibrary : MonoBehaviour
    {
        [SerializeField] Sprite[] faces;

        public Sprite Face(string npcId, Alignment alignment)
        {
            string name = $"{npcId}_{MoodOf(alignment)}", first = $"{npcId}_0";
            return Array.Find(faces, face => face.name == name) ?? Array.Find(faces, face => face.name == first);
        }

        static int MoodOf(Alignment alignment)
        {
            switch (alignment)
            {
                case Alignment.Hallowed: return 1;
                case Alignment.Marked: return 2;
                default: return 0;
            }
        }
    }
}
