using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.World
{
    /// <summary>A gate or stair to another zone, barred until its keeper has fallen.</summary>
    public sealed class ZonePortal : MonoBehaviour, IInteractable
    {
        [SerializeField] ZoneManager zones;
        [SerializeField] int targetZone;
        [SerializeField] bool arriveAtExit;
        [SerializeField] string prompt = "";
        [Tooltip("Session flag that unbars this portal. Empty means always open.")]
        [SerializeField] string requiredFlag = "";
        [SerializeField, TextArea] string lockedMessage = "";

        bool Unlocked => string.IsNullOrEmpty(requiredFlag) || (GameSession.Instance != null && GameSession.Instance.HasFlag(requiredFlag));

        public int TargetZone => targetZone;
        public string RequiredFlag => requiredFlag;
        public string InteractionPrompt => Unlocked ? prompt : GameText.Barred;

        public bool CanInteract(GameObject interactor) => !zones.Travelling;

        public void Interact(GameObject interactor)
        {
            if (Unlocked) zones.Travel(targetZone, arriveAtExit);
            else GameMessages.Notify(lockedMessage);
        }
    }
}
