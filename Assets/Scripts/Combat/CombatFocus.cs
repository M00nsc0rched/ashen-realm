namespace AshenRealm.Combat
{
    /// <summary>The foe the player is fighting right now: the last one to engage or be struck.</summary>
    public static class CombatFocus
    {
        public static ICombatant Current { get; private set; }

        /// <summary>Counts up on every focus event so the HUD can keep its bar lit.</summary>
        public static int Revision { get; private set; }

        public static void Set(ICombatant combatant)
        {
            Current = combatant;
            Revision++;
        }
    }
}
