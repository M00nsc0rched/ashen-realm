using AshenRealm.Core;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.Interactions
{
    public enum CityActivityKind { Well, BountyBoard, MarketSupplies, MemorialShrine, LanternRest }

    /// <summary>A visible, repeatable city service. Its trigger never obstructs the street.</summary>
    public sealed class CityActivity : MonoBehaviour, IInteractable
    {
        [SerializeField] string activityId;
        [SerializeField] CityActivityKind kind;

        public string ActivityId => activityId;
        public CityActivityKind Kind => kind;
        public string InteractionPrompt => kind switch
        {
            CityActivityKind.Well => CityText.WellPrompt,
            CityActivityKind.BountyBoard => CityText.BoardPrompt,
            CityActivityKind.MarketSupplies => CityText.MarketPrompt,
            CityActivityKind.MemorialShrine => CityText.ShrinePrompt,
            _ => CityText.RestPrompt
        };

        public bool CanInteract(GameObject interactor) => GameSession.Instance != null &&
            GameSession.Instance.ClassChosen && CityActivityLedger.Instance != null;

        public void Interact(GameObject interactor)
        {
            if (CanInteract(interactor)) CityActivityLedger.Instance.Use(kind, interactor);
        }
    }
}
