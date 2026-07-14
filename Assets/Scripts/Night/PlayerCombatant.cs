using TMPro;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>The player stats a level-up can improve.</summary>
    public enum StatKind { Health, Shield, Speed, Luck }

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
        [Tooltip("Where floating numbers appear around the player. Defaults to this transform.")]
        [SerializeField] private RectTransform floatingAnchor;
        [SerializeField] private CombatantAnimator animator;

        [Header("Luck marker (type 13)")]
        [Tooltip("Where the luck marker appears (above the player).")]
        [SerializeField] private RectTransform luckMarkContainer;
        [SerializeField] private StatusIcon luckMarkPrefab;
        [SerializeField] private Sprite luckIcon;

        private StatusIcon _luckMark;
        private int luck;
        private int _baseSpeed;
        private int permanentLuckPercent; // level-up luck upgrades (flat % on every die, persists between combats)

        public int Speed => speed;
        public int Luck => luck;
        public Sprite Sprite => sprite;
        public int CurrentHealth { get; private set; }
        public int CurrentShield { get; private set; }
        public bool IsAlive => CurrentHealth > 0;

        // For the level-up UI: the current values of the upgradable stats.
        public int MaxHealth => maxHealth;
        public int BaseShield => shield;
        public int PermanentLuckPercent => permanentLuckPercent;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (fullBarWidth <= 0f && healthBar != null) fullBarWidth = healthBar.rect.width;
            _baseSpeed = speed;
            SetHealth(maxHealth);
            SetShield(shield);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Restores full health/shield and revives the visuals for a new combat (tournament start).</summary>
        public void ResetForCombat()
        {
            SetHealth(maxHealth);
            SetShield(shield);
            speed = _baseSpeed; // speed/luck boosts only last the combat
            luck = 0;
            if (animator != null) animator.ResetState();
        }

        /// <summary>
        /// Between a tournament's combats (with no reset effect): health and shield carry over — only the
        /// per-combat stat boosts drop (the speed boost). The temporary luck bonus is cleared by the engine.
        /// </summary>
        public void ClearCombatBoosts()
        {
            speed = _baseSpeed;
            if (animator != null) animator.ResetState();
        }

        /// <summary>Spawns a floating number (damage/heal/effect) around the player.</summary>
        public void ShowNumber(int amount, FloatingNumbers.Kind kind)
        {
            RectTransform anchor = floatingAnchor != null ? floatingAnchor : transform as RectTransform;
            if (anchor != null) FloatingNumbers.Spawn(anchor, amount, kind);
        }

        /// <summary>Plays the player's attack animation (on the die result).</summary>
        public void PlayAttack()
        {
            if (animator != null) animator.PlayAttack();
        }

        /// <summary>Plays the player's throw wind-up (when they throw a die).</summary>
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
            if (remaining > 0) SetHealth(CurrentHealth - remaining);

            if (animator != null)
            {
                if (IsAlive) animator.PlayHurt();
                else animator.PlayDeath();
            }
        }

        /// <summary>Damages health directly, ignoring the shield (an enemy's skip-shield synergy).</summary>
        public void ApplyHealthDamage(int amount)
        {
            if (amount <= 0) return;

            SetHealth(CurrentHealth - amount);

            if (animator != null)
            {
                if (IsAlive) animator.PlayHurt();
                else animator.PlayDeath();
            }
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

        /// <summary>Shows the current luck bonus above the player (0 hides the marker). Type 13.</summary>
        public void SetLuckMark(int amount)
        {
            if (luckMarkContainer == null || luckMarkPrefab == null) return;
            if (_luckMark == null) _luckMark = Instantiate(luckMarkPrefab, luckMarkContainer, false);

            if (amount <= 0) { _luckMark.gameObject.SetActive(false); return; }
            _luckMark.gameObject.SetActive(true);
            _luckMark.Set(luckIcon, amount);
        }

        /// <summary>Level-up: permanently raises a stat by <paramref name="amount"/> (survives ResetForCombat).</summary>
        public void UpgradeStat(StatKind stat, int amount)
        {
            if (amount <= 0) return;
            switch (stat)
            {
                case StatKind.Health: maxHealth += amount; SetHealth(CurrentHealth + amount); break; // raise the cap and heal it
                case StatKind.Shield: shield += amount; SetShield(CurrentShield + amount); break;
                case StatKind.Speed: _baseSpeed += amount; speed += amount; break;
                case StatKind.Luck: permanentLuckPercent += amount; break; // applied to every die by the combat controller
            }
        }

        // Roll-value conversions (effect type 2).
        public void Heal(int amount) => SetHealth(CurrentHealth + Mathf.Max(0, amount));
        public void AddShield(int amount) => SetShield(CurrentShield + Mathf.Max(0, amount));
        public void AddSpeed(int amount) => speed += Mathf.Max(0, amount);
        public void AddLuck(int amount) => luck += Mathf.Max(0, amount); // luck has no gameplay effect yet
    }
}
