namespace AshenRealm.Combat
{
    /// <summary>Anything the HUD's enemy bar can follow.</summary>
    public interface ICombatant
    {
        string DisplayName { get; }
        Health Health { get; }
        bool IsEngaged { get; }
    }
}
