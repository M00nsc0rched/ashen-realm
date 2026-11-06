using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>One answer of a two-way setting: the chosen one glows and carries the diamond, as in the settings sketch.</summary>
    public sealed class ChoiceButton : MonoBehaviour
    {
        [SerializeField] Image frame;
        [SerializeField] Image marker;
        [SerializeField] Sprite normalSprite;
        [SerializeField] Sprite chosenSprite;

        public bool Chosen { get; private set; }

        public void SetChosen(bool chosen)
        {
            Chosen = chosen;
            frame.sprite = chosen ? chosenSprite : normalSprite;
            marker.enabled = chosen;
        }
    }
}
