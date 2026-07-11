using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// One enemy in the combat zone: its image, its health and shield numbers, and a health bar whose
    /// **width** tracks the remaining health — full at 100%, shrinking as it drops, without changing
    /// the bar's position or height. Health/shield use the enemy's stats scaled by its rarity.
    ///
    /// The health bar should be point-anchored (fixed width) with a left pivot so it drains to the left.
    /// </summary>
    public class EnemySlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image enemyImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI shieldText;

        [Tooltip("Health bar fill; only its width changes with the health fraction.")]
        [SerializeField] private RectTransform healthBar;
        [Tooltip("Bar width at full health. 0 = capture the bar's width at start.")]
        [SerializeField] private float fullBarWidth;

        private int _maxHealth = 1;

        public EnemyData Enemy { get; private set; }
        public EnemyRarity Rarity { get; private set; }
        public int CurrentHealth { get; private set; }
        public int CurrentShield { get; private set; }

        private Action _onClick;

        public void SetClick(Action onClick) => _onClick = onClick;
        public void OnPointerClick(PointerEventData eventData) => _onClick?.Invoke();

        private void Awake()
        {
            if (fullBarWidth <= 0f && healthBar != null) fullBarWidth = healthBar.rect.width;
        }

        /// <summary>Fills the slot from an enemy at a rarity (starts at full health).</summary>
        public void Setup(EnemyData enemy, EnemyRarity rarity)
        {
            Enemy = enemy;
            Rarity = rarity;
            if (enemy == null) return;

            if (enemyImage != null)
            {
                enemyImage.sprite = enemy.Sprite;
                enemyImage.enabled = enemy.Sprite != null;
            }
            if (nameText != null) nameText.text = enemy.EnemyName;

            int maxHp = enemy.HealthAt(rarity);
            SetHealth(maxHp, maxHp);
            SetShield(enemy.ShieldAt(rarity));
        }

        /// <summary>Updates the health number and the bar width (max keeps the 100% reference).</summary>
        public void SetHealth(int current, int max)
        {
            _maxHealth = Mathf.Max(1, max);
            current = Mathf.Clamp(current, 0, _maxHealth);
            CurrentHealth = current;

            if (healthText != null) healthText.text = current.ToString();
            if (healthBar != null)
            {
                float fraction = (float)current / _maxHealth;
                healthBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullBarWidth * fraction);
            }
        }

        public void SetShield(int shield)
        {
            CurrentShield = Mathf.Max(0, shield);
            if (shieldText != null) shieldText.text = CurrentShield.ToString();
        }

        public bool IsAlive => CurrentHealth > 0;

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
            if (remaining > 0) SetHealth(CurrentHealth - remaining, _maxHealth);
        }
    }
}
