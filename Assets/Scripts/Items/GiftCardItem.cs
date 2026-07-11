using TMPro;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Items
{
    /// <summary>
    /// UI item for a gift card: icon, name and a quantity count (identical cards are shown as a single
    /// accumulated entry). No description text — the hover tooltip comes from the TooltipTrigger on
    /// this same GameObject.
    /// </summary>
    public class GiftCardItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;

        [Tooltip("Quantity of this card (for accumulated duplicates).")]
        [SerializeField] private TextMeshProUGUI countText;

        public GiftCardData Card { get; private set; }

        public void Setup(GiftCardData card) => Setup(card, 1);

        public void Setup(GiftCardData card, int count)
        {
            Card = card;
            if (card == null)
            {
                return;
            }

            if (icon != null) icon.sprite = card.Sprite;
            if (nameText != null) nameText.text = card.CardName;
            if (countText != null) countText.text = count.ToString();

            TooltipTrigger tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null) tooltip.SetMessage(card.DescriptionForTooltip);
        }
    }
}
