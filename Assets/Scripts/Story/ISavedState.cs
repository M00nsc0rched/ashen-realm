namespace AshenRealm.Story
{
    /// <summary>
    /// Something in the world a soul can use up for good: a chest, a foe, an encounter. Its key becomes a session flag, so
    /// loading a soul finds everything it already cleared and puts the world back that way.
    /// </summary>
    public interface ISavedState
    {
        string SaveKey { get; }

        /// <summary>Called on load, also on objects in zones that have never been active, so it must not rely on Awake.</summary>
        void ApplyCleared();
    }
}
