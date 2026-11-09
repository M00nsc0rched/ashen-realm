using AshenRealm.AI;
using AshenRealm.Story;
using AshenRealm.Combat;
using AshenRealm.Core;
using AshenRealm.Input;
using AshenRealm.Interactions;
using AshenRealm.Player;
using UnityEngine;
using UnityEngine.UI;

namespace AshenRealm.UI
{
    /// <summary>
    /// Drives the gameplay HUD: health/stamina bars, crosshair, interaction prompt, notifications,
    /// damage vignette, death overlay and the enemy health bar.
    /// Bars are filled by scaling the fill rect's right anchor, so no sprites are required.
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] Health playerHealth;
        [SerializeField] Stamina playerStamina;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] PlayerLifecycle lifecycle;
        [SerializeField] PlayerInputHub input;

        [Header("Bars")]
        [SerializeField] RectTransform healthFill;
        [SerializeField] RectTransform healthChip;
        [SerializeField] RectTransform staminaFill;
        [SerializeField] Image staminaFillImage;
        [SerializeField] Color staminaColor = new Color(0.72f, 0.64f, 0.32f);
        [SerializeField] Color staminaExhaustedColor = new Color(0.55f, 0.22f, 0.15f);

        [Header("Class and story")]
        [SerializeField] PlayerAbilities abilities;
        [SerializeField] RectTransform focusFill;
        [SerializeField] Text statusText;
        [SerializeField] Text ability1Label;
        [SerializeField] Text ability2Label;
        [SerializeField] Text potionLabel;
        [SerializeField] Image ability1Cooldown;
        [SerializeField] Image ability2Cooldown;
        [SerializeField] Image ability1Icon;
        [SerializeField] Image ability2Icon;
        [SerializeField] Sprite[] skillIcons;
        [SerializeField] Image weaponEmblem;
        [SerializeField] Sprite[] classWeaponIcons;
        [SerializeField] Sprite[] goodsWeaponIcons;

        [Header("Center")]
        [SerializeField] Image crosshair;
        [SerializeField] Color crosshairIdle = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] Color crosshairInteract = new Color(1f, 0.82f, 0.45f, 0.95f);
        [SerializeField] CanvasGroup promptGroup;
        [SerializeField] Text promptText;
        [SerializeField, Optional] TouchButton interactButton;

        [Header("Messages")]
        [SerializeField] CanvasGroup notificationGroup;
        [SerializeField] Text notificationText;
        [SerializeField] float notificationDuration = 3.5f;

        [Header("Overlays")]
        [SerializeField] Image damageVignette;
        [SerializeField] CanvasGroup deathGroup;

        [Header("Enemy")]
        [SerializeField] Text enemyName;
        [SerializeField] CanvasGroup enemyBarGroup;
        [SerializeField] RectTransform enemyFill;

        float chipValue = 1f;
        float chipDelay;
        float notificationTimer;
        int notificationFrame = -1;
        float vignette;
        float enemyBarTimer;
        int focusRevision;
        float crosshairScale = 1f;
        int firstSkill = -1, secondSkill = -1;

        void OnEnable()
        {
            GameMessages.Notification += ShowNotification;
            if (playerHealth != null) playerHealth.Damaged += OnPlayerDamaged;

        }

        void OnDisable()
        {
            GameMessages.Notification -= ShowNotification;
            if (playerHealth != null) playerHealth.Damaged -= OnPlayerDamaged;

        }

        void RefreshSession()
        {
            GameSession session = GameSession.Instance;
            if (session == null) return;
            string alignment = session.Alignment == Alignment.Marked ? GameText.AlignmentMarked
                : session.Alignment == Alignment.Hallowed ? GameText.AlignmentHallowed : GameText.AlignmentMortal;
            statusText.text = $"{alignment}  {session.Honor}\n{GameText.GoldLabel}  {session.Gold}\n{GameText.PotionLabel}  {session.Potions}";
            RefreshSkills();
            int goodsIndex = System.Array.IndexOf(Catalog.ItemsOf(ItemSlot.Weapon), session.Weapon);
            weaponEmblem.sprite = goodsIndex >= 0 ? goodsWeaponIcons[goodsIndex] : classWeaponIcons[(int)session.Class.Id];
            potionLabel.text = $"{GameText.Potion} {session.Potions}";
        }

        void Start()
        {
            if (GameSession.Instance != null) GameSession.Instance.Changed += RefreshSession;
            RefreshSession();
            notificationGroup.alpha = 0f;
            promptGroup.alpha = 0f;
            deathGroup.alpha = 0f;
            enemyBarGroup.alpha = 0f;
            SetVignette(0f);
        }

        void OnDestroy()
        {
            if (GameSession.Instance != null) GameSession.Instance.Changed -= RefreshSession;
        }

        void OnPlayerDamaged(DamageInfo info, float amount)
        {
            vignette = Mathf.Clamp01(vignette + 0.25f + amount / 30f);
            chipDelay = 0.6f;
        }



        void ShowNotification(string message)
        {
            // Two things said on one arrival share the box, or the second would wipe the first.
            notificationText.text = Time.frameCount == notificationFrame ? $"{notificationText.text}\n{message}" : message;
            notificationFrame = Time.frameCount;
            notificationTimer = Mathf.Max(notificationDuration, Mathf.Min(12f, notificationText.text.Length / 22f));
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateBars(dt);
            UpdateCenter(dt);
            UpdateOverlays(dt);
            UpdateEnemyBar(dt);
        }

        void UpdateBars(float dt)
        {
            RefreshSkills();
            float health = playerHealth.Normalized;
            SetFill(healthFill, health);

            chipDelay -= dt;
            if (health > chipValue) chipValue = health;
            else if (chipDelay <= 0f) chipValue = Mathf.MoveTowards(chipValue, health, dt * 0.6f);
            SetFill(healthChip, chipValue);

            SetFill(focusFill, abilities.FocusNormalized);
            ability1Cooldown.fillAmount = abilities.Affordable(0) ? abilities.CooldownFraction(0) : 1f;
            ability2Cooldown.fillAmount = abilities.Affordable(1) ? abilities.CooldownFraction(1) : 1f;

            SetFill(staminaFill, playerStamina.Normalized);
            if (staminaFillImage != null)
            {
                Color target = playerStamina.IsExhausted
                    ? Color.Lerp(staminaExhaustedColor, staminaColor, Mathf.PingPong(Time.time * 3f, 1f) * 0.4f)
                    : staminaColor;
                staminaFillImage.color = target;
            }
        }

        void RefreshSkills()
        {
            AbilityDefinition first = abilities.ActiveAbility(0), second = abilities.ActiveAbility(1);
            if (first != null && firstSkill != (int)first.Effect)
            {
                firstSkill = (int)first.Effect;
                ability1Label.text = first.Name;
                ability1Icon.sprite = skillIcons[firstSkill];
            }
            if (second != null && secondSkill != (int)second.Effect)
            {
                secondSkill = (int)second.Effect;
                ability2Label.text = second.Name;
                ability2Icon.sprite = skillIcons[secondSkill];
            }
        }

        void UpdateCenter(float dt)
        {
            string prompt = interactor != null && interactor.isActiveAndEnabled ? interactor.CurrentPrompt : string.Empty;
            bool hasPrompt = !string.IsNullOrEmpty(prompt) && (input == null || !input.Paused); // a panel over the world has no use for it
            if (hasPrompt)
                promptText.text = input != null && input.TouchMode ? prompt : GameText.InteractKey + prompt;
            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, hasPrompt ? 1f : 0f, dt * 8f);

            crosshairScale = Mathf.Lerp(crosshairScale, hasPrompt ? 1.8f : 1f, 1f - Mathf.Exp(-15f * dt));
            crosshair.rectTransform.localScale = Vector3.one * crosshairScale;
            crosshair.color = hasPrompt ? crosshairInteract : crosshairIdle;

            if (interactButton != null) interactButton.SetAvailable(hasPrompt);
        }

        void UpdateOverlays(float dt)
        {
            notificationTimer -= dt;
            float notifyTarget = notificationTimer > 0f ? 1f : 0f;
            notificationGroup.alpha = Mathf.MoveTowards(notificationGroup.alpha, notifyTarget, dt * (notifyTarget > 0f ? 6f : 1.5f));

            vignette = Mathf.MoveTowards(vignette, 0f, dt * 0.9f);
            float lowHealth = playerHealth.IsAlive && playerHealth.Normalized < 0.3f
                ? (0.3f - playerHealth.Normalized) / 0.3f * (0.25f + Mathf.PingPong(Time.time * 0.8f, 0.15f))
                : 0f;
            SetVignette(Mathf.Max(vignette, lowHealth));

            bool dead = lifecycle != null && lifecycle.IsDead;
            deathGroup.alpha = Mathf.MoveTowards(deathGroup.alpha, dead ? 1f : 0f, dt * (dead ? 0.7f : 2f));
        }

        void UpdateEnemyBar(float dt)
        {
            ICombatant enemy = CombatFocus.Current;
            if (enemy == null || enemy.Health == null)
            {
                enemyBarGroup.alpha = 0f;
                return;
            }

            if (CombatFocus.Revision != focusRevision)
            {
                focusRevision = CombatFocus.Revision;
                enemyBarTimer = 6f;
                enemyName.text = enemy.DisplayName;
            }

            enemyBarTimer -= dt;
            bool alive = enemy.Health.IsAlive;
            bool show = alive && (enemy.IsEngaged || enemyBarTimer > 0f);
            if (!alive && enemyBarGroup.alpha > 0f && enemyBarTimer > 4.5f) show = true; // linger briefly on the killing blow
            enemyBarGroup.alpha = Mathf.MoveTowards(enemyBarGroup.alpha, show ? 1f : 0f, dt * (show ? 4f : 1.2f));
            SetFill(enemyFill, enemy.Health.Normalized);
        }

        void SetVignette(float alpha)
        {
            Color c = damageVignette.color;
            c.a = alpha * 0.85f;
            damageVignette.color = c;
        }

        static void SetFill(RectTransform fill, float normalized)
        {
            if (fill == null) return;
            Vector2 max = fill.anchorMax;
            max.x = Mathf.Clamp01(normalized);
            fill.anchorMax = max;
        }
    }
}
