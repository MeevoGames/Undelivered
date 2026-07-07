using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Upgrades
{
    /// <summary>
    /// One row in the upgrades shop: icon, name, description, price and buy button. The button is
    /// disabled when the upgrade is already owned or the player can't afford it.
    /// </summary>
    public class UpgradeShopItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Button buyButton;

        [Tooltip("Optional mark shown once the upgrade has been purchased.")]
        [SerializeField] private GameObject purchasedIndicator;

        public UpgradeData Upgrade { get; private set; }

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
            if (priceText != null)
            {
                priceText.text = upgrade.Price.ToString();
            }
            if (buyButton != null)
            {
                buyButton.onClick.RemoveListener(OnBuyClicked);
                buyButton.onClick.AddListener(OnBuyClicked);
            }
        }

        /// <summary>Updates the buy button and the purchased mark from state and current gold.</summary>
        public void Refresh(bool purchased, int gold)
        {
            if (buyButton != null)
            {
                buyButton.interactable = !purchased && Upgrade != null && gold >= Upgrade.Price;
            }
            if (purchasedIndicator != null)
            {
                purchasedIndicator.SetActive(purchased);
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
