using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenRealm.Story
{
    /// <summary>
    /// The state of one descent: who you are, what your name is worth, and what you are carrying. Lives for the page
    /// session (Graveborne's saves are not ported yet).
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public const int MarkedAt = -40;
        public const int HallowedAt = 40;
        public const float PotionHeal = 40f;

        readonly HashSet<string> flags = new HashSet<string>();

        public static GameSession Instance { get; private set; }

        public ClassDefinition Class { get; private set; } = Catalog.Classes[0];
        public bool ClassChosen { get; private set; }
        public int Honor { get; private set; }
        public int Gold { get; private set; }
        public int Potions { get; private set; } = 1;
        public int WeaponLevel { get; private set; }

        public Alignment Alignment => Honor <= MarkedAt ? Alignment.Marked : Honor >= HallowedAt ? Alignment.Hallowed : Alignment.Mortal;
        /// <summary>The dishonoured see the warped version of every encounter.</summary>
        public bool SeesWarped => Honor < 0;

        /// <summary>Raised whenever honor, gold, potions or weapon level change.</summary>
        public event Action Changed;
        public event Action<ClassDefinition> ClassSelected;

        void Awake() => Instance = this;

        public void ChooseClass(CharacterClass id)
        {
            Class = Catalog.ClassOf(id);
            ClassChosen = true;
            Honor = Class.StartHonor;
            ClassSelected?.Invoke(Class);
            Changed?.Invoke();
        }

        public void AddHonor(int amount)
        {
            Honor = Mathf.Clamp(Honor + amount, -100, 100);
            Changed?.Invoke();
        }

        public void AddGold(int amount)
        {
            Gold = Mathf.Max(0, Gold + amount);
            Changed?.Invoke();
        }

        public bool TrySpend(int gold)
        {
            if (Gold < gold) return false;
            Gold -= gold;
            Changed?.Invoke();
            return true;
        }

        public void AddPotions(int amount)
        {
            Potions = Mathf.Max(0, Potions + amount);
            Changed?.Invoke();
        }

        public void UpgradeWeapon()
        {
            WeaponLevel++;
            Changed?.Invoke();
        }

        public void SetFlag(string flag) => flags.Add(flag);
        public bool HasFlag(string flag) => flags.Contains(flag);
    }
}
