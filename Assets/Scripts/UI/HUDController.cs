using AshenRealm.AI;
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
        [SerializeField, Optional] CursedKnightAI enemy;
        [SerializeField] CanvasGroup enemyBarGroup;
        [SerializeField] RectTransform enemyFill;

        float chipValue = 1f;
        float chipDelay;
        float notificationTimer;
        float vignette;
        float enemyBarTimer;
        float crosshairScale = 1f;

        void OnEnable()
        {
            GameMessages.Notification += ShowNotification;
            if (playerHealth != null) playerHealth.Damaged += OnPlayerDamaged;
            if (enemy != null && enemy.Health != null) enemy.Health.Damaged += OnEnemyDamaged;
        }

        void OnDisable()
        {
            GameMessages.Notification -= ShowNotification;
            if (playerHealth != null) playerHealth.Damaged -= OnPlayerDamaged;
            if (enemy != null && enemy.Health != null) enemy.Health.Damaged -= OnEnemyDamaged;
        }

        void Start()
        {
            notificationGroup.alpha = 0f;
            promptGroup.alpha = 0f;
            deathGroup.alpha = 0f;
            enemyBarGroup.alpha = 0f;
            SetVignette(0f);
        }

        void OnPlayerDamaged(DamageInfo info, float amount)
        {
            vignette = Mathf.Clamp01(vignette + 0.25f + amount / 30f);
            chipDelay = 0.6f;
        }

        void OnEnemyDamaged(DamageInfo info, float amount) => enemyBarTimer = 6f;

        void ShowNotification(string message)
        {
            notificationText.text = message;
            notificationTimer = notificationDuration;
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
            float health = playerHealth.Normalized;
            SetFill(healthFill, health);

            chipDelay -= dt;
            if (health > chipValue) chipValue = health;
            else if (chipDelay <= 0f) chipValue = Mathf.MoveTowards(chipValue, health, dt * 0.6f);
            SetFill(healthChip, chipValue);

            SetFill(staminaFill, playerStamina.Normalized);
            if (staminaFillImage != null)
            {
                Color target = playerStamina.IsExhausted
                    ? Color.Lerp(staminaExhaustedColor, staminaColor, Mathf.PingPong(Time.time * 3f, 1f) * 0.4f)
                    : staminaColor;
                staminaFillImage.color = target;
            }
        }

        void UpdateCenter(float dt)
        {
            string prompt = interactor != null && interactor.isActiveAndEnabled ? interactor.CurrentPrompt : string.Empty;
            bool hasPrompt = !string.IsNullOrEmpty(prompt);
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
            if (enemy == null)
            {
                enemyBarGroup.alpha = 0f;
                return;
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
