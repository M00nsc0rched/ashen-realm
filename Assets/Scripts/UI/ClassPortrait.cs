using AshenRealm.Story;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    public sealed class ClassPortrait : MonoBehaviour
    {
        [SerializeField] Image image;
        [SerializeField] Sprite[] portraits;
        void OnEnable() => Refresh();
        void Start()
        {
            if (GameSession.Instance != null) GameSession.Instance.ClassSelected += OnClassSelected;
            Refresh();
        }
        void OnDestroy()
        {
            if (GameSession.Instance != null) GameSession.Instance.ClassSelected -= OnClassSelected;
        }
        void OnClassSelected(ClassDefinition definition) => Refresh();
        void Refresh()
        {
            GameSession session = GameSession.Instance;
            if (session != null) image.sprite = portraits[Mathf.Clamp((int)session.Class.Id, 0, portraits.Length - 1)];
        }
    }
}
