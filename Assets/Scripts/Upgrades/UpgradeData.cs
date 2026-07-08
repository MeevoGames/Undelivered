using UnityEngine;

namespace Undelivered.Upgrades
{
    /// <summary>
    /// Base data for a day-mode upgrade. Supports levels: <see cref="levelPrices"/> holds the price
    /// of each level (its length is the max level), and <see cref="valuePerLevel"/> holds an optional
    /// per-level value whose meaning depends on the concrete upgrade (a chance, a multiplier, ...).
    /// Single-purchase upgrades just use one level.
    /// </summary>
    public abstract class UpgradeData : ScriptableObject
    {
        [SerializeField] private Sprite icon;
        [SerializeField] private string upgradeName;
        [SerializeField, TextArea] private string description;

        [Tooltip("Shown as the hover tooltip.")]
        [SerializeField, TextArea] private string descriptionForTooltip;

        [Tooltip("Price of each level. The array length is the max level.")]
        [SerializeField] private int[] levelPrices = { 50 };

        [Tooltip("Per-level value (meaning depends on the upgrade: chance, multiplier, protection...).")]
        [SerializeField] private float[] valuePerLevel;

        [Tooltip("If set, this upgrade only appears in the shop once the prerequisite upgrade is owned.")]
        [SerializeField] private UpgradeData prerequisite;

        public Sprite Icon => icon;
        public string UpgradeName => upgradeName;
        public string Description => description;
        public string DescriptionForTooltip => descriptionForTooltip;
        public UpgradeData Prerequisite => prerequisite;
        public int MaxLevel => levelPrices != null ? levelPrices.Length : 0;

        /// <summary>Price to buy the next level from the given current (0-based) level.</summary>
        public int GetPrice(int currentLevel)
        {
            if (levelPrices == null || currentLevel < 0 || currentLevel >= levelPrices.Length)
            {
                return 0;
            }
            return Mathf.Max(0, levelPrices[currentLevel]);
        }

        /// <summary>The per-level value for the given (1-based) level, or 0 if none is configured.</summary>
        protected float ValueForLevel(int level)
        {
            if (valuePerLevel == null || level < 1 || level > valuePerLevel.Length)
            {
                return 0f;
            }
            return valuePerLevel[level - 1];
        }

        /// <summary>Applies this upgrade's effect for the given (1-based) level.</summary>
        public abstract void Apply(int level);
    }
}
