using AshenRealm.Story;

namespace AshenRealm.UI
{
    /// <summary>What the shop panel asks of whoever is selling: the panel shows, the counter decides.</summary>
    public interface IShopCounter
    {
        /// <summary>What the player has to spend, as a line of text.</summary>
        string Balance { get; }
        string PriceOf(ItemDefinition item);
        bool IsCarried(ItemDefinition item);
        /// <summary>A line on what it would change, or empty.</summary>
        string EffectOf(ItemDefinition item);
        /// <summary>Tries to buy the item and returns what happened, to be shown.</summary>
        string Buy(ItemDefinition item);
    }
}
