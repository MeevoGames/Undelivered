using UnityEngine;

namespace Undelivered.Upgrades
{
    /// <summary>
    /// Base data for a day-mode upgrade shown in the upgrades shop: icon, name, description and
    /// price. Concrete subclasses implement <see cref="Apply"/> with the actual effect.
    /// </summary>
    public abstract class UpgradeData : ScriptableObject
    {
        [SerializeField] private Sprite icon;
        [SerializeField] private string upgradeName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private int price = 50;

        public Sprite Icon => icon;
        public string UpgradeName => upgradeName;
        public string Description => description;
        public int Price => Mathf.Max(0, price);

        /// <summary>Applies this upgrade's effect. Called once, when the upgrade is bought.</summary>
        public abstract void Apply();
    }
}
