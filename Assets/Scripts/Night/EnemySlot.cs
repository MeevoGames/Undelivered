using System;
using TMPro;
using Undelivered.UI;
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

        [SerializeField] private CombatantAnimator animator;

        [Header("Status marks (Quema / Veneno / Congelamiento)")]
        [Tooltip("Where the state markers appear (above the enemy). A HorizontalLayoutGroup is applied if missing.")]
        [SerializeField] private RectTransform statusContainer;
        [SerializeField] private StatusIcon statusIconPrefab;
        [Tooltip("Icon shown for Quema (burn) marks.")]
        [SerializeField] private Sprite burnIcon;
        [Tooltip("Icon shown for Veneno (poison) marks.")]
        [SerializeField] private Sprite poisonIcon;
        [Tooltip("Icon shown for Congelamiento (freeze) marks.")]
        [SerializeField] private Sprite freezeIcon;

        private StatusIcon[] _statusIcons; // one per StatusType (Burn, Poison, Freeze), created on demand

        private int _maxHealth = 1;

        public EnemyData Enemy { get; private set; }
        public EnemyRarity Rarity { get; private set; }
        public int CurrentHealth { get; private set; }
        public int CurrentShield { get; private set; }

        // --- Synergy bonuses (set/reverted by the SynergySystem while a synergy applies) ---
        /// <summary>While true, Veneno (poison) never sticks or ticks on this enemy.</summary>
        public bool PoisonImmune { get; set; }
        /// <summary>While true, this enemy's attacks hit the player's health directly (skip the shield).</summary>
        public bool SkipsShield { get; set; }
        /// <summary>Flat speed added to this enemy for turn order (the combat engine reads it).</summary>
        public int SynergySpeed { get; set; }

        /// <summary>Raised when this enemy takes damage (the amount dealt), so the player can gain XP.</summary>
        public event Action<int> Damaged;

        /// <summary>Raised once, the moment this enemy dies (for healing drops, etc.).</summary>
        public event Action Died;

        private bool _died;
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

            TooltipTrigger tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null) tooltip.SetGeneral(enemy.EnemyName, enemy.DescriptionForTooltip);

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

        /// <summary>Spawns a floating number (damage/heal/effect) around this enemy.</summary>
        public void ShowNumber(int amount, FloatingNumbers.Kind kind)
        {
            if (transform is RectTransform rect) FloatingNumbers.Spawn(rect, amount, kind);
        }

        /// <summary>Plays this enemy's attack animation (on the die result).</summary>
        public void PlayAttack()
        {
            if (animator != null) animator.PlayAttack();
        }

        /// <summary>Plays this enemy's throw wind-up (when it throws its die).</summary>
        public void PlayThrow()
        {
            if (animator != null) animator.PlayThrow();
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
            if (remaining > 0) SetHealth(CurrentHealth - remaining, _maxHealth);

            if (animator != null)
            {
                if (IsAlive) animator.PlayHurt();
                else animator.PlayDeath();
            }
            FireDamage(amount);
        }

        /// <summary>Damages health directly, ignoring the shield (skip-shield effect).</summary>
        public void ApplyHealthDamage(int amount)
        {
            if (amount <= 0) return;

            SetHealth(CurrentHealth - amount, _maxHealth);

            if (animator != null)
            {
                if (IsAlive) animator.PlayHurt();
                else animator.PlayDeath();
            }
            FireDamage(amount);
        }

        /// <summary>Destroys all shield (shield-break effect).</summary>
        public void BreakShield() => SetShield(0);

        /// <summary>Restores health, capped at the current max (per-round synergy heals, etc.).</summary>
        public void Heal(int amount)
        {
            if (amount > 0) SetHealth(CurrentHealth + amount, _maxHealth);
        }

        /// <summary>
        /// Adjusts the synergy max-health bonus. A gain raises the cap and heals by the same amount; a loss
        /// lowers the cap and clamps current health to it (a synergy dropping never kills, only clamps).
        /// </summary>
        public void AddMaxHealth(int delta)
        {
            if (delta == 0) return;
            int newMax = Mathf.Max(1, _maxHealth + delta);
            int newCurrent = delta > 0 ? CurrentHealth + delta : Mathf.Min(CurrentHealth, newMax);
            SetHealth(newCurrent, newMax);
        }

        /// <summary>Adjusts the synergy shield bonus (added on gain, removed on loss; never below 0).</summary>
        public void AddSynergyShield(int delta) => SetShield(CurrentShield + delta);

        // Reports damage (XP) and, if this was the killing blow, fires Died exactly once (drops).
        private void FireDamage(int amount)
        {
            Damaged?.Invoke(amount);
            if (!IsAlive && !_died) { _died = true; Died?.Invoke(); }
        }

        /// <summary>Updates the state markers above the enemy; a count of 0 hides that state's marker.</summary>
        public void SetStatuses(int burn, int poison, int freeze)
        {
            SetStatus(StatusType.Burn, burn);
            SetStatus(StatusType.Poison, poison);
            SetStatus(StatusType.Freeze, freeze);
        }

        private void SetStatus(StatusType type, int count)
        {
            EnsureStatusIcons();
            if (_statusIcons == null) return;

            StatusIcon marker = _statusIcons[(int)type];
            if (marker == null) return;

            if (count <= 0) { marker.gameObject.SetActive(false); return; }

            marker.gameObject.SetActive(true);
            marker.Set(IconFor(type), count);
        }

        // Creates the three markers (Burn, Poison, Freeze) once, in order, all hidden until they have marks.
        private void EnsureStatusIcons()
        {
            if (_statusIcons != null) return;
            _statusIcons = new StatusIcon[3];
            if (statusContainer == null || statusIconPrefab == null) return;

            if (statusContainer.GetComponent<HorizontalLayoutGroup>() == null)
            {
                HorizontalLayoutGroup layout = statusContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            for (int i = 0; i < _statusIcons.Length; i++)
            {
                StatusIcon marker = Instantiate(statusIconPrefab, statusContainer, false);
                marker.gameObject.SetActive(false);
                _statusIcons[i] = marker;
            }
        }

        private Sprite IconFor(StatusType type)
        {
            switch (type)
            {
                case StatusType.Burn: return burnIcon;
                case StatusType.Poison: return poisonIcon;
                case StatusType.Freeze: return freezeIcon;
                default: return null;
            }
        }
    }
}
