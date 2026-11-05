using AshenRealm.Core;
using UnityEngine;

namespace AshenRealm.Interactions
{
    /// <summary>
    /// Repeatable environmental-storytelling interaction that shows a short line of lore.
    /// </summary>
    public sealed class LoreInspectable : MonoBehaviour, IInteractable
    {
        [SerializeField] string prompt = GameText.Inspect;
        [SerializeField, TextArea] string loreText = "";
        [SerializeField] float cooldown = 1.5f;

        float lastUse = -999f;

        public string InteractionPrompt => prompt;

        public bool CanInteract(GameObject interactor) => Time.time - lastUse >= cooldown;

        public void Interact(GameObject interactor)
        {
            lastUse = Time.time;
            GameMessages.Notify(loreText);
        }
    }
}
