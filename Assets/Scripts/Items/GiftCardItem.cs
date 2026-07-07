using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Items
{
    /// <summary>UI item showing a collected gift card's icon, name and description.</summary>
    public class GiftCardItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        public GiftCardData Card { get; private set; }

        public void Setup(GiftCardData card)
        {
            Card = card;
            if (card == null)
            {
                return;
            }

            if (icon != null)
            {
                icon.sprite = card.Sprite;
            }
            if (nameText != null)
            {
                nameText.text = card.CardName;
            }
            if (descriptionText != null)
            {
                descriptionText.text = card.Description;
            }
        }
    }
}
