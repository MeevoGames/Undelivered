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

        /// <summary>Fills the entry. Pass <paramref name="showPrice"/> false where nothing is being bought (a gift).</summary>
        public void Setup(IItem item, bool showPrice = true)
        {
            Item = item;
            if (item == null) return;

            if (icon != null)
            {
                icon.sprite = item.Icon;
                icon.enabled = item.Icon != null;
            }
            if (priceText != null)
            {
                priceText.gameObject.SetActive(showPrice);
                if (showPrice) priceText.text = item.Price.ToString();
            }

            TooltipTrigger tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null)
            {
                if (item is DiceData die)
                    tooltip.SetDice(die.DiceName, die.DescriptionForTooltip, die.FaceSprites(), $"{die.BaseLuckPercent}% de Suerte.");
                else if (item is EffectData effect)
                    tooltip.SetEffect(effect.EffectName, effect.DescriptionForTooltip, effect.GoldenText);
                else
                    tooltip.SetGeneral(item.ItemName, item.DescriptionForTooltip);
            }
        }
    }
}
