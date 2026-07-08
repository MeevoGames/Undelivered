using TMPro;
using Undelivered.UI;
using Undelivered.Work;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Shop
{
    /// <summary>
    /// One row in the truck shop: shows a truck's name, price and remaining stock, with a buy button.
    /// The button is disabled while the player can't afford the truck or it is out of stock.
    /// </summary>
    public class TruckShopItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Button buyButton;

        [Tooltip("Optional text showing the remaining stock.")]
        [SerializeField] private TextMeshProUGUI stockText;

        [Tooltip("Tooltip trigger for the hover description (auto-found on this object if left empty).")]
        [SerializeField] private TooltipTrigger tooltip;

        private TruckData _truck;
        private TruckShop _shop;

        public TruckData Truck => _truck;

        /// <summary>Binds this row to a truck and the shop that handles the purchase.</summary>
        public void Setup(TruckData truck, TruckShop shop)
        {
            _truck = truck;
            _shop = shop;

            if (nameText != null)
            {
                nameText.text = truck.name;
            }
            if (priceText != null)
            {
                priceText.text = "$" + truck.Price.ToString();
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
                tooltip.SetMessage(truck.DescriptionForTooltip);
            }
        }

        /// <summary>Updates the stock text and buy button from remaining stock and current gold.</summary>
        public void Refresh(int gold, int remaining)
        {
            if (stockText != null)
            {
                stockText.text = remaining.ToString();
            }
            if (buyButton != null)
            {
                buyButton.interactable = _truck != null && remaining > 0 && gold >= _truck.Price;
            }
        }

        private void OnBuyClicked()
        {
            if (_shop != null && _truck != null)
            {
                _shop.TryBuy(_truck);
            }
        }
    }
}
