using AshenRealm.Core;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.Interactions
{
    /// <summary>
    /// One of Graveborne's honor-shaped encounters, standing in a room. The dishonoured see a dark shape where the
    /// honorable see a person: both figures are in the prefab and the session's honor picks one.
    /// </summary>
    public sealed class EncounterSite : MonoBehaviour, IInteractable
    {
        [SerializeField] string encounterId;
        [SerializeField] StoryDirector director;
        [SerializeField] GameObject clearFigure;
        [SerializeField, Optional] GameObject warpedFigure;

        bool resolved;

        public string EncounterId => encounterId;
        public string InteractionPrompt => resolved ? string.Empty : GameText.Approach;
        public bool CanInteract(GameObject interactor) => !resolved;

        public void Interact(GameObject interactor)
        {
            if (resolved) return;
            director.StartEncounter(StoryText.Encounter(encounterId), this);
        }

        /// <summary>The choice is made; whoever was here is gone.</summary>
        public void Resolve()
        {
            resolved = true;
            clearFigure.SetActive(false);
            if (warpedFigure != null) warpedFigure.SetActive(false);
        }

        void Update()
        {
            if (resolved || warpedFigure == null || GameSession.Instance == null) return;
            bool warped = GameSession.Instance.SeesWarped;
            if (clearFigure.activeSelf == warped) clearFigure.SetActive(!warped);
            if (warpedFigure.activeSelf != warped) warpedFigure.SetActive(warped);
        }
    }
}
