using TMPro;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// The player's combatant for a night combat: health, shield and speed, with optional health/shield
    /// texts and a health bar (its width tracks health, like the enemies). Damage hits the shield first,
    /// then the health.
    /// </summary>
    public class PlayerCombatant : MonoBehaviour
    {
        public static PlayerCombatant Instance { get; private set; }

        [SerializeField] private int maxHealth = 30;
        [SerializeField] private int shield;
        [SerializeField] private int speed = 5;
        [SerializeField] private Sprite sprite;

        [Header("UI (optional)")]
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI shieldText;
        [SerializeField] private RectTransform healthBar;
        [SerializeField] private float fullBarWidth;

        public int Speed => speed;
        public Sprite Sprite => sprite;
        public int CurrentHealth { get; private set; }
        public int CurrentShield { get; private set; }
        public bool IsAlive => CurrentHealth > 0;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (fullBarWidth <= 0f && healthBar != null) fullBarWidth = healthBar.rect.width;
            SetHealth(maxHealth);
            SetShield(shield);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Applies damage: the shield absorbs first, then any remainder hits the health.</summary>
        public void ApplyDamage(int amount)
        {
            if (amount <= 0) return;

            int remaining = amount;
            if (CurrentShield > 0)
            {
                int absorbed = Mathf.Min(CurrentShield, remaining);
                SetShield(CurrentShield - absorbed);
                remaining -= absorbed;
            }
            if (remaining > 0) SetHealth(CurrentHealth - remaining);
        }

        public void SetHealth(int value)
        {
            int max = Mathf.Max(1, maxHealth);
            CurrentHealth = Mathf.Clamp(value, 0, max);
            if (healthText != null) healthText.text = CurrentHealth.ToString();
            if (healthBar != null)
                healthBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullBarWidth * ((float)CurrentHealth / max));
        }

        public void SetShield(int value)
        {
            CurrentShield = Mathf.Max(0, value);
            if (shieldText != null) shieldText.text = CurrentShield.ToString();
        }
    }
}
