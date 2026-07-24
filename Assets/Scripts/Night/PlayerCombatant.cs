using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.Night
{
    /// <summary>The player stats a level-up can improve.</summary>
    public enum StatKind { Health, Shield, Speed, Luck }

    /// <summary>
    /// The player's combatant for a night combat: health, shield and speed, with optional health/shield
    /// texts and a health bar (its width tracks health, like the enemies). Damage hits the shield first,
    /// then the health.
    /// </summary>
    public class PlayerCombatant : MonoBehaviour, IPointerClickHandler
    {
        public static PlayerCombatant Instance { get; private set; }

        [SerializeField] private int maxHealth = 30;
        [SerializeField] private int shield;
        [SerializeField] private int speed = 5;
        [SerializeField] private Sprite sprite;

        [Header("UI (optional)")]
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI shieldText;
        [Tooltip("The whole shield display (icon + number). Hidden while the shield is 0.")]
        [SerializeField] private GameObject shieldObject;
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

            EnsureBarWidth();
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

        // Tapping the player opens the same detail window the enemies use, with their own stats.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (EnemyDetailPanel.Instance != null) EnemyDetailPanel.Instance.ShowPlayer(this);
        }

        /// <summary>Spawns a floating number (damage/heal/effect) around the player.</summary>
        public void ShowNumber(int amount, FloatingNumbers.Kind kind)
        {
            RectTransform anchor = floatingAnchor != null ? floatingAnchor : transform as RectTransform;
            if (anchor != null) FloatingNumbers.Spawn(anchor, amount, kind);
        }

        /// <summary>Plays the player's attack animation, weighted by the raw face of the die they threw.</summary>
        public void PlayAttack(int dieValue = 0, RectTransform target = null)
        {
            if (animator != null) animator.PlayAttack(dieValue, target);
        }

        /// <summary>Plays the player's throw wind-up (when they throw a die).</summary>
        public void PlayThrow()
        {
            if (animator != null) animator.PlayThrow();
        }

        /// <summary>
        /// Applies damage: the shield absorbs first, then any remainder hits the health.
        /// <paramref name="dieValue"/> is the raw face of the die that dealt it, which picks the reaction.
        /// </summary>
        public void ApplyDamage(int amount, int dieValue = 0)
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
                if (IsAlive) animator.PlayHurt(dieValue);
                else animator.PlayDeath();
            }
        }

        /// <summary>Damages health directly, ignoring the shield (an enemy's skip-shield synergy).</summary>
        public void ApplyHealthDamage(int amount, int dieValue = 0)
        {
            if (amount <= 0) return;

            SetHealth(CurrentHealth - amount);

            if (animator != null)
            {
                if (IsAlive) animator.PlayHurt(dieValue);
                else animator.PlayDeath();
            }
        }

        public void SetHealth(int value)
        {
            int max = Mathf.Max(1, maxHealth);
            CurrentHealth = Mathf.Clamp(value, 0, max);
            if (healthText != null) healthText.text = CurrentHealth.ToString();

            EnsureBarWidth(); // the bar's authored size is the 100% reference
            if (healthBar != null && fullBarWidth > 0f)
                healthBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fullBarWidth * ((float)CurrentHealth / max));
        }

        /// <summary>
        /// Captures the health bar's authored width as the full-health reference. Done lazily because the
        /// layout may not be built yet in Awake — until a real width is known the bar is left untouched,
        /// so it can never be measured from an already-shrunk state.
        /// </summary>
        private void EnsureBarWidth()
        {
            if (fullBarWidth > 0f || healthBar == null) return;
            float width = healthBar.rect.width;
            if (width <= 0f) width = healthBar.sizeDelta.x;
            if (width > 0f) fullBarWidth = width;
        }

        public void SetShield(int value)
        {
            CurrentShield = Mathf.Max(0, value);
            if (shieldText != null) shieldText.text = CurrentShield.ToString();
            if (shieldObject != null) shieldObject.SetActive(CurrentShield > 0); // no shield → hide the whole display
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
