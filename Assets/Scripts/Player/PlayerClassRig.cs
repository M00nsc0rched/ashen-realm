using AshenRealm.Combat;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.Player
{
    /// <summary>
    /// Turns the one player object into the chosen class: stats, kit and which weapons sit under the hand pivots.
    /// All four weapon sets ship in the rig; only the chosen one stays enabled.
    /// </summary>
    public sealed class PlayerClassRig : MonoBehaviour
    {
        /// <summary>Each class has a model for each hand, so weaponSets holds class * HandsPerClass + hand.</summary>
        public const int HandsPerClass = 2;

        [SerializeField] Health health;
        [SerializeField] PlayerCombat combat;
        [SerializeField] PlayerAbilities abilities;
        [Tooltip("Right and left hand models per class, in CharacterClass order.")]
        [SerializeField] GameObject[] weaponSets;

        void Start()
        {
            GameSession session = GameSession.Instance;
            if (session == null) return;
            session.ClassSelected += Apply;
            Apply(session.Class);
        }

        void OnDestroy()
        {
            if (GameSession.Instance != null) GameSession.Instance.ClassSelected -= Apply;
        }

        void Apply(ClassDefinition definition)
        {
            health.SetMax(definition.MaxHealth, true);
            combat.ApplyClass(definition);
            abilities.ApplyClass(definition);
            for (int i = 0; i < weaponSets.Length; i++) weaponSets[i].SetActive(i / HandsPerClass == (int)definition.Id);
        }
    }
}
