using AshenRealm.Core;
using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.Interactions
{
    /// <summary>Someone who can be spoken to in Hollowgate, or the merchant on a stair landing.</summary>
    public sealed class NpcActor : MonoBehaviour, IInteractable
    {
        [SerializeField] string npcId;
        [SerializeField] StoryDirector director;

        public string NpcId => npcId;
        public string InteractionPrompt => GameText.Talk;
        public bool CanInteract(GameObject interactor) => true;
        public void Interact(GameObject interactor) => director.TalkTo(StoryText.Npc(npcId));
    }
}
