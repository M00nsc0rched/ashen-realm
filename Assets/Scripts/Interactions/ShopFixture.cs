using AshenRealm.Story;
using UnityEngine;

namespace AshenRealm.Interactions
{
    /// <summary>The goods themselves: the forge's weapon rack, the relics behind the chapel's rail. Opens the same counter as their keeper.</summary>
    public sealed class ShopFixture : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemSlot goods;
        [SerializeField] string prompt = "";
        [SerializeField] StoryDirector director;

        public ItemSlot Goods => goods;
        public string InteractionPrompt => prompt;
        public bool CanInteract(GameObject interactor) => true;
        public void Interact(GameObject interactor) => director.OpenShop(goods);
    }
}
