using TMPro;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// A shop/inventory item entry, for any <see cref="IItem"/> (dice today; effects and boxes later):
    /// the item's image plus, in the shop, its price. The name and description live in the hover
    /// tooltip, not as fields. Leave <see cref="priceText"/> unassigned in the inventory prefab.
    /// </summary>
    public class ItemView : MonoBehaviour
    {
        [SerializeField] private Image icon;

        [Tooltip("Price text — assign in the shop prefab, leave empty in the inventory prefab.")]
        [SerializeField] private TextMeshProUGUI priceText;

        public IItem Item { get; private set; }

        public void Setup(IItem item)
        {
            Item = item;
            if (item == null) return;

            if (icon != null)
            {
                icon.sprite = item.Icon;
                icon.enabled = item.Icon != null;
            }
            if (priceText != null) priceText.text = item.Price.ToString();

            TooltipTrigger tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null)
            {
                tooltip.SetMessage(string.IsNullOrEmpty(item.DescriptionForTooltip)
                    ? item.ItemName
                    : $"{item.ItemName}\n{item.DescriptionForTooltip}");
            }
        }
    }
}
