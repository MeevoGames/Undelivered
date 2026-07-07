using TMPro;
using Undelivered.Work;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Shop
{
    /// <summary>
    /// One row in the truck shop: shows a truck's name and price, with a buy button. The button is
    /// disabled while the player can't afford the truck.
    /// </summary>
    public class TruckShopItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private Button buyButton;

        private TruckData _truck;
        private TruckShop _shop;

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
                priceText.text = truck.Price.ToString();
            }
            if (buyButton != null)
            {
                buyButton.onClick.RemoveListener(OnBuyClicked);
                buyButton.onClick.AddListener(OnBuyClicked);
            }
        }

        /// <summary>Enables the buy button only when the player has enough gold.</summary>
        public void UpdateAffordable(int gold)
        {
            if (buyButton != null)
            {
                buyButton.interactable = _truck != null && gold >= _truck.Price;
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
