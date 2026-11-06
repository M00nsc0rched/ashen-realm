using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenRealm.Story
{
    /// <summary>
    /// The state of one soul: who you are, what your name is worth, and what you are carrying. SoulKeeper writes it to the
    /// roster and reads it back.
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
        public int Descents { get; private set; }
        public ItemDefinition Weapon { get; private set; }
        public ItemDefinition Relic { get; private set; }
        public IEnumerable<string> Flags => flags;

        public Alignment Alignment => Honor <= MarkedAt ? Alignment.Marked : Honor >= HallowedAt ? Alignment.Hallowed : Alignment.Mortal;
        /// <summary>The dishonoured see the warped version of every encounter.</summary>
        public bool SeesWarped => Honor < 0;

        /// <summary>Raised whenever honor, gold, potions or weapon level change.</summary>
        public event Action Changed;
        public event Action<ClassDefinition> ClassSelected;
        /// <summary>Raised when something bought goes into the hand or round the neck.</summary>
        public event Action EquipmentChanged;

        void Awake() => Instance = this;

        public void ChooseClass(CharacterClass id)
        {
            Class = Catalog.ClassOf(id);
            ClassChosen = true;
            Honor = Class.StartHonor;
            Weapon = null;
            Relic = null;
            ClassSelected?.Invoke(Class);
            Changed?.Invoke();
        }

        /// <summary>Takes on a saved soul. The class event dresses the player; the keeper puts back the rest.</summary>
        public void Restore(SoulRecord soul)
        {
            Class = Catalog.ClassOf(soul.Class);
            ClassChosen = true;
            Honor = soul.Honor;
            Gold = soul.Gold;
            Potions = soul.Potions;
            WeaponLevel = soul.WeaponLevel;
            Descents = soul.Descents;
            Weapon = Catalog.Item(soul.WeaponId);
            Relic = Catalog.Item(soul.RelicId);
            flags.Clear();
            flags.UnionWith(soul.Flags);
            ClassSelected?.Invoke(Class);
            Changed?.Invoke();
        }

        public void AddDescent() => Descents++;

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

        /// <summary>Takes the item up in place of what its slot held, and returns that.</summary>
        public ItemDefinition Equip(ItemDefinition item)
        {
            ItemDefinition replaced;
            if (item.Slot == ItemSlot.Weapon)
            {
                replaced = Weapon;
                Weapon = item;
            }
            else
            {
                replaced = Relic;
                Relic = item;
            }
            EquipmentChanged?.Invoke();
            Changed?.Invoke();
            return replaced;
        }

        public bool IsEquipped(ItemDefinition item) => Weapon == item || Relic == item;

        public void SetFlag(string flag) => flags.Add(flag);
        public void ClearFlag(string flag) => flags.Remove(flag);
        public bool HasFlag(string flag) => flags.Contains(flag);
    }
}
