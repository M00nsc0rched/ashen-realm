using AshenRealm.Core;
using AshenRealm.Story;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// "Új lélek": the class cards in a row, with the sheet of the one in the middle beside it. The row is a single number,
    /// <c>position</c>; every card's place follows from its distance to that number, so arrows, a swipe and a half-finished
    /// drag are the same thing. Choosing a card turns the sheet to its story, the last stop before the descent.
    /// </summary>
    public sealed class ClassCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        const float SideOffset = 110f;    // units between the middle and a neighbour at rest
        const float SwingOut = 150f;      // extra sideways travel halfway, so the two cards pass beside each other
        const float SideScale = 0.8f;
        const float SideLight = 0.4f;
        const float GlowStrength = 0.2f;
        const float DragPerCard = 300f;
        const float Overdrag = 0.25f;
        const float FlickBias = 0.3f;     // a short swipe is enough to turn one card
        const float TurnTime = 0.11f;
        const float MiddleHalfWidth = 200f;

        static readonly Color DotOn = new Color(0.97f, 0.8f, 0.52f);
        static readonly Color DotOff = new Color(0.42f, 0.33f, 0.62f);
        static readonly Color HonorGood = new Color(0.68f, 0.8f, 0.52f);
        static readonly Color HonorPlain = new Color(0.9f, 0.7f, 0.4f);
        static readonly Color HonorBad = new Color(0.92f, 0.36f, 0.3f);
        static readonly Color LabelOn = new Color(0.93f, 0.88f, 0.77f);
        static readonly Color LabelOff = new Color(0.93f, 0.88f, 0.77f, 0.45f);

        [SerializeField] CanvasGroup group;
        [SerializeField] MainMenu menu;
        [SerializeField] Canvas canvas;

        [Header("Row")]
        [SerializeField] RectTransform[] cards;
        [SerializeField] Image[] cardArt;
        [SerializeField] Image[] cardGlow;
        [SerializeField] Image[] dots;
        [SerializeField] Selectable previousButton;
        [SerializeField] Selectable nextButton;
        [Tooltip("Arrows, dots and hint: they go away while the story is showing.")]
        [SerializeField] GameObject pager;
        [SerializeField] GameObject prologue;

        [Header("Sheet")]
        [Tooltip("Fades as the cards pass, so the text changes unseen.")]
        [SerializeField] CanvasGroup details;
        [SerializeField] GameObject sheetPage;
        [SerializeField] GameObject storyPage;
        [SerializeField] Text nameLabel;
        [SerializeField] Text roleLabel;
        [SerializeField] Text focusLabel;
        [Tooltip("Health, focus, attack, defense, magic, speed.")]
        [SerializeField] Text[] statValues;
        [SerializeField] Text honorValue;
        [SerializeField] Text passiveName;
        [SerializeField] Text passiveText;
        [SerializeField] Text storyText;

        [Header("Buttons")]
        [SerializeField] Button chooseButton;
        [SerializeField] Button backButton;
        [SerializeField] Image chooseFrame;
        [SerializeField] Text chooseLabel;
        [SerializeField] Sprite plainSprite;
        [SerializeField] Sprite litSprite;

        float position;
        float velocity;
        int target;
        int shown = -1;
        bool dragging;
        float grabbedAt;
        float grabbedPosition;

        public bool IsOpen { get; private set; }
        public bool ShowingStory { get; private set; }

        /// <summary>The card the row is turning to.</summary>
        public int Selected => target;

        /// <summary>The card the sheet describes: it changes halfway through a turn.</summary>
        public int ShownCard => shown;

        void Awake() => SetVisible(false);

        public void Open()
        {
            IsOpen = true;
            SetVisible(true);
            ShowSheet();
            Jump(target);
        }

        /// <summary>Puts a card in the middle at once.</summary>
        public void Jump(int index)
        {
            target = Mathf.Clamp(index, 0, cards.Length - 1);
            position = target;
            velocity = 0f;
            shown = -1;
            Place();
            UpdateArrows();
        }

        /// <summary>One card along the row.</summary>
        public void Step(int step)
        {
            if (ShowingStory) return;
            target = Mathf.Clamp(target + step, 0, cards.Length - 1);
            UpdateArrows();
        }

        public void Previous() => Step(-1);
        public void Next() => Step(1);

        /// <summary>"Kiválasztás", then "Indulás". A card that is not in the game yet stays where it is.</summary>
        public void Confirm()
        {
            ClassDefinition chosen = Catalog.Cards[target].Class;
            if (chosen == null) return;
            if (!ShowingStory)
            {
                ShowStory(chosen);
                return;
            }

            IsOpen = false;
            SetVisible(false);
            ShowingStory = false;
            EventSystem events = EventSystem.current;
            if (events != null) events.SetSelectedGameObject(null); // nothing stays selected behind the game
            menu.BeginSoul(chosen.Id);
        }

        /// <summary>"Vissza", Esc or the gamepad's B: one step at a time.</summary>
        public void Back()
        {
            if (ShowingStory)
            {
                ShowSheet();
                return;
            }

            IsOpen = false;
            SetVisible(false);
            menu.ChoiceClosed();
        }

        /// <summary>From the gamepad, keyboard: left and right turn the row, or between the two buttons on the story page.</summary>
        public void Move(MoveDirection direction)
        {
            if (direction != MoveDirection.Left && direction != MoveDirection.Right) return;
            if (ShowingStory) EventSystem.current.SetSelectedGameObject((direction == MoveDirection.Left ? (Selectable)backButton : chooseButton).gameObject);
            else Step(direction == MoveDirection.Left ? -1 : 1);
        }

        void ShowSheet()
        {
            ShowingStory = false;
            sheetPage.SetActive(true);
            storyPage.SetActive(false);
            pager.SetActive(true);
            prologue.SetActive(false);
            shown = -1;
            Select(chooseButton);
        }

        void ShowStory(ClassDefinition c)
        {
            position = target;
            velocity = 0f;
            Place();
            ShowingStory = true;
            storyText.text = $"{c.Backstory}\n\n{string.Format(MenuText.GoalFormat, c.Goal)}\n\n" +
                             $"{string.Format(MenuText.AbilityFormat, c.Ability1.Name, c.Ability1.Description)}\n" +
                             string.Format(MenuText.AbilityFormat, c.Ability2.Name, c.Ability2.Description);
            sheetPage.SetActive(false);
            storyPage.SetActive(true);
            pager.SetActive(false);
            prologue.SetActive(true);
            chooseLabel.text = MenuText.Begin;
            details.alpha = 1f;
            Select(chooseButton);
        }

        void Update()
        {
            if (!IsOpen) return;

            // Left and right turn the row from the first press, so one of the two buttons is always selected, even after
            // a pointer pressed something else. Kiválasztás is lit anyway.
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != chooseButton.gameObject && events.currentSelectedGameObject != backButton.gameObject)
                events.SetSelectedGameObject(chooseButton.gameObject);

            if (dragging) return;
            position = Mathf.SmoothDamp(position, target, ref velocity, TurnTime, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(position - target) < 0.001f) position = target;
            Place();
        }

        void Place()
        {
            int middle = Mathf.Clamp(Mathf.RoundToInt(position), 0, cards.Length - 1);
            for (int i = 0; i < cards.Length; i++)
            {
                float distance = i - position;
                float reach = Mathf.Abs(distance);
                bool near = reach < 2f;
                if (cards[i].gameObject.activeSelf != near) cards[i].gameObject.SetActive(near);
                if (!near) continue;

                float side = Mathf.Min(reach, 1f);
                cards[i].anchoredPosition = new Vector2(Mathf.Sign(distance) * (SideOffset * reach + SwingOut * Mathf.Sin(Mathf.PI * side)), 0f);
                cards[i].localScale = Vector3.one * Mathf.Lerp(1f, SideScale, side);
                float light = Mathf.Lerp(1f, SideLight, side);
                cardArt[i].color = new Color(light, light, light, Mathf.Clamp01(2f - reach));
                Color glow = cardGlow[i].color;
                glow.a = GlowStrength * (1f - side);
                cardGlow[i].color = glow;
            }

            if (middle != shown)
            {
                shown = middle;
                ShowCard(middle);

                // Painted from the outside in, so the middle card lies on top.
                for (int d = cards.Length - 1; d >= 0; d--)
                {
                    if (middle - d >= 0) cards[middle - d].SetAsLastSibling();
                    if (d > 0 && middle + d < cards.Length) cards[middle + d].SetAsLastSibling();
                }
            }

            // Out at the moment the text changes, halfway between two cards.
            float fade = Mathf.Abs(position - middle) * 2f;
            details.alpha = 1f - fade * fade;
        }

        void ShowCard(int index)
        {
            ClassCard card = Catalog.Cards[index];
            nameLabel.text = card.Name;
            roleLabel.text = card.Role;
            focusLabel.text = card.UsesIntellect ? MenuText.StatIntellect : MenuText.StatFocus;
            int[] stats = { card.Health, card.Focus, card.Attack, card.Defense, card.Magic, card.Speed };
            for (int i = 0; i < stats.Length; i++) statValues[i].text = stats[i].ToString();
            honorValue.text = string.Format(MenuText.HonorFormat, card.Honor, MenuText.TierOf(card.Honor));
            honorValue.color = card.Honor >= 20 ? HonorGood : card.Honor >= -19 ? HonorPlain : HonorBad;
            passiveName.text = card.PassiveName;
            passiveText.text = card.PassiveText;

            bool available = card.Class != null;
            chooseLabel.text = available ? MenuText.Choose : MenuText.Locked;
            chooseLabel.color = available ? LabelOn : LabelOff;
            chooseFrame.sprite = available ? litSprite : plainSprite;
            chooseFrame.overrideSprite = null; // the selected look would otherwise stay on
            chooseButton.transition = available ? Selectable.Transition.SpriteSwap : Selectable.Transition.None;

            for (int i = 0; i < dots.Length; i++) dots[i].color = i == index ? DotOn : DotOff;
        }

        void UpdateArrows()
        {
            previousButton.interactable = target > 0;
            nextButton.interactable = target < cards.Length - 1;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ShowingStory) return;
            dragging = true;
            grabbedAt = eventData.position.x;
            grabbedPosition = position;
            velocity = 0f;
        }

        // From where it was grabbed, not by per-frame deltas: on a slow phone a fast swipe must not lose ground.
        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            float moved = (eventData.position.x - grabbedAt) / canvas.scaleFactor / DragPerCard;
            position = Mathf.Clamp(grabbedPosition - moved, -Overdrag, cards.Length - 1 + Overdrag);
            Place();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            target = Mathf.Clamp(Mathf.RoundToInt(position + Mathf.Sign(position - grabbedPosition) * FlickBias), 0, cards.Length - 1);
            UpdateArrows();
        }

        /// <summary>A tap on a neighbour brings it to the middle.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (ShowingStory || eventData.dragging) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out Vector2 at);
            if (at.x < -MiddleHalfWidth) Step(-1);
            else if (at.x > MiddleHalfWidth) Step(1);
        }

        static void Select(Selectable selectable)
        {
            EventSystem events = EventSystem.current;
            if (events != null) events.SetSelectedGameObject(selectable.gameObject);
        }

        void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
