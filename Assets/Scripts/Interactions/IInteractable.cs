using UnityEngine;

namespace AshenRealm.Interactions
{
    /// <summary>
    /// Anything the player can aim at and activate. Implementations live on (or above) a collider.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Short verb phrase shown on the HUD, e.g. "Open Chest". Empty hides the prompt.</summary>
        string InteractionPrompt { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
