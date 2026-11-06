using AshenRealm.Combat;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>
    /// Turns the one player object into the chosen class: stats, kit and which weapons sit under the hand pivots.
    /// All four weapon sets ship in the rig; only the chosen one stays enabled. A weapon bought in Hollowgate takes the
    /// place of the class's right-hand one, and what is carried adds to the class's numbers here.
    /// </summary>
    public sealed class PlayerClassRig : MonoBehaviour
    {
        /// <summary>Each class has a model for each hand, so weaponSets holds class * HandsPerClass + hand.</summary>
        public const int HandsPerClass = 2;
        const float HealthPerPoint = 1.6f;

        [SerializeField] Health health;
        [SerializeField] PlayerCombat combat;
        [SerializeField] PlayerAbilities abilities;
        [Tooltip("Right and left hand models per class, in CharacterClass order.")]
        [SerializeField] GameObject[] weaponSets;
        [Tooltip("The goods of the Cold Forge, in Catalog order.")]
        [SerializeField] GameObject[] boughtWeapons;

        void Start()
        {
            GameSession session = GameSession.Instance;
            if (session == null) return;
            session.ClassSelected += Apply;
            session.EquipmentChanged += ApplyGear;
            Apply(session.Class);
        }

        void OnDestroy()
        {
            if (GameSession.Instance == null) return;
            GameSession.Instance.ClassSelected -= Apply;
            GameSession.Instance.EquipmentChanged -= ApplyGear;
        }

        void Apply(ClassDefinition definition)
        {
            health.SetMax(definition.MaxHealth, true);
            combat.ApplyClass(definition);
            abilities.ApplyClass(definition);
            ApplyGear();
            health.SetCurrent(health.Max);
        }

        void ApplyGear()
        {
            GameSession session = GameSession.Instance;
            ItemDefinition weapon = session.Weapon, relic = session.Relic;
            int attack = (weapon?.Attack ?? 0) + (relic?.Attack ?? 0);
            int magic = (weapon?.Magic ?? 0) + (relic?.Magic ?? 0);
            int speed = (weapon?.Speed ?? 0) + (relic?.Speed ?? 0);
            combat.ApplyGear(attack, magic, speed, (weapon?.Lifesteal ?? 0f) + (relic?.Lifesteal ?? 0f));
            abilities.ApplyGear((weapon?.Focus ?? 0) + (relic?.Focus ?? 0));
            health.SetMax(session.Class.MaxHealth + ((weapon?.Health ?? 0) + (relic?.Health ?? 0)) * HealthPerPoint, false);

            int classIndex = (int)session.Class.Id;
            for (int i = 0; i < weaponSets.Length; i++)
                weaponSets[i].SetActive(i / HandsPerClass == classIndex && (weapon == null || i % HandsPerClass != 0));
            ItemDefinition[] goods = Catalog.ItemsOf(ItemSlot.Weapon);
            for (int i = 0; i < boughtWeapons.Length; i++) boughtWeapons[i].SetActive(goods[i] == weapon);
        }

        /// <summary>What one blow would do with this item in place of what its slot holds now, forge work included.</summary>
        public float DamageWith(ItemDefinition item)
        {
            GameSession session = GameSession.Instance;
            ItemDefinition weapon = item.Slot == ItemSlot.Weapon ? item : session.Weapon;
            ItemDefinition relic = item.Slot == ItemSlot.Relic ? item : session.Relic;
            return combat.DamageWith((weapon?.Attack ?? 0) + (relic?.Attack ?? 0), (weapon?.Magic ?? 0) + (relic?.Magic ?? 0)) * combat.OutgoingMultiplier;
        }

        /// <summary>The blow as it stands.</summary>
        public float Damage => combat.Damage * combat.OutgoingMultiplier;
    }
}
