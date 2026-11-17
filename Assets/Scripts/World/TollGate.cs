using AshenRealm.Core;
using AshenRealm.Interactions;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.World
{
    /// <summary>
    /// The Old Toll Gate, the only pass from the lower valley to the north: its doors are shut and solid until its flag
    /// (the Cursed Knight's death) is set, then they swing open for good. A loaded soul with the flag finds them open,
    /// because the check runs whenever the valley is active.
    /// </summary>
    public sealed class TollGate : MonoBehaviour, IInteractable
    {
        [SerializeField] string requiredFlag = "";
        [SerializeField, TextArea] string lockedMessage = "";
        [SerializeField] Transform leftDoor;
        [SerializeField] Transform rightDoor;
        [Tooltip("The solid collider of the shut doors, which is also what the crosshair finds.")]
        [SerializeField] BoxCollider barrier;
        [SerializeField] float openAngle = 100f;

        public bool IsOpen { get; private set; }
        public string RequiredFlag => requiredFlag;

        public string InteractionPrompt => IsOpen ? string.Empty : GameText.Barred;
        public bool CanInteract(GameObject interactor) => !IsOpen;
        public void Interact(GameObject interactor) { if (!IsOpen) GameMessages.Notify(lockedMessage); }

        void OnEnable() => Refresh();
        void Update() => Refresh();

        void Refresh()
        {
            if (IsOpen || GameSession.Instance == null || !GameSession.Instance.HasFlag(requiredFlag)) return;
            IsOpen = true;
            barrier.enabled = false;
            // The leaves swing north, away from the valley.
            leftDoor.localRotation = Quaternion.Euler(0f, -openAngle, 0f);
            rightDoor.localRotation = Quaternion.Euler(0f, openAngle, 0f);
        }
    }
}
