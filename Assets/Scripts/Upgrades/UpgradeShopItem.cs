using TMPro;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Upgrades
{
    /// <summary>
    /// One row in the upgrades shop: icon, name, description, current level and the price of the next
    /// level, plus a buy button. The button is disabled at max level or when the player can't afford
    /// the next level.
    /// </summary>
    public class UpgradeShopItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Button buyButton;

        [Tooltip("Optional text showing the current/max level, e.g. \"1/3\".")]
        [SerializeField] private TextMeshProUGUI levelText;

        [Tooltip("Optional mark shown once the upgrade is at max level.")]
        [SerializeField] private GameObject maxedIndicator;

        [Tooltip("Tooltip trigger for the hover description (auto-found on this object if left empty).")]
        [SerializeField] private TooltipTrigger tooltip;

        public UpgradeData Upgrade { get; private set; }

        /// <summary>The buy button (the tutorial blinks it).</summary>
        public Button BuyButton => buyButton;

        private UpgradeShop _shop;

        public void Setup(UpgradeData upgrade, UpgradeShop shop)
        {
            Upgrade = upgrade;
            _shop = shop;

            if (icon != null)
            {
                icon.sprite = upgrade.Icon;
            }
            if (nameText != null)
            {
                nameText.text = upgrade.UpgradeName;
            }
            if (descriptionText != null)
            {
                descriptionText.text = upgrade.Description;
            }
            if (buyButton != null)
            {
                buyButton.onClick.RemoveListener(OnBuyClicked);
                buyButton.onClick.AddListener(OnBuyClicked);
            }

            if (tooltip == null)
            {
                tooltip = GetComponent<TooltipTrigger>();
            }
            if (tooltip != null)
            {
                tooltip.SetMessage(upgrade.DescriptionForTooltip);
            }
        }

        /// <summary>Updates the level text, next price and buy button from the current level and gold.</summary>
        public void Refresh(int level, int gold)
        {
            if (Upgrade == null)
            {
                return;
            }

            int maxLevel = Upgrade.MaxLevel;
            bool maxed = level >= maxLevel;
            int nextPrice = maxed ? 0 : Upgrade.GetPrice(level);

            if (levelText != null)
            {
                levelText.text = $"{level}/{maxLevel}";
            }
            if (priceText != null)
            {
                priceText.text = maxed ? "MAX" : $"${nextPrice}";
            }
            if (buyButton != null)
            {
                buyButton.interactable = !maxed && gold >= nextPrice;
            }
            if (maxedIndicator != null)
            {
                maxedIndicator.SetActive(maxed);
            }
        }

        private void OnBuyClicked()
        {
            if (_shop != null && Upgrade != null)
            {
                _shop.TryBuy(Upgrade);
            }
        }
    }
}
