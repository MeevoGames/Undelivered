using TMPro;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// A box entry in the shop / inventory: its image, a rarity-coloured border and (in the shop) the
    /// price. Name and description live in the hover tooltip. Leave <see cref="priceText"/> unassigned
    /// outside the shop.
    /// </summary>
    public class BoxView : MonoBehaviour
    {
        [SerializeField] private Image icon;

        [Tooltip("Background image whose colour shows the rarity (the border).")]
        [SerializeField] private Image border;

        [Tooltip("Price text — assign in the shop prefab, leave empty in the inventory.")]
        [SerializeField] private TextMeshProUGUI priceText;

        [Header("Rarity border colours")]
        [SerializeField] private Color comunColor = new Color(0.62f, 0.62f, 0.58f);
        [SerializeField] private Color raraColor = new Color(0.23f, 0.51f, 0.96f);
        [SerializeField] private Color epicaColor = new Color(0.65f, 0.33f, 0.94f);

        public BoxData Box { get; private set; }

        public void Setup(BoxData box)
        {
            Box = box;
            if (box == null) return;

            if (icon != null)
            {
                icon.sprite = box.Icon;
                icon.enabled = box.Icon != null;
            }
            if (border != null) border.color = ColorFor(box.BoxRarity);
            if (priceText != null) priceText.text = box.Price.ToString();

            TooltipTrigger tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null) tooltip.SetGeneral(box.BoxName, box.DescriptionForTooltip);
        }

        private Color ColorFor(BoxData.Rarity rarity)
        {
            switch (rarity)
            {
                case BoxData.Rarity.Rara: return raraColor;
                case BoxData.Rarity.Epica: return epicaColor;
                default: return comunColor;
            }
        }
    }
}
