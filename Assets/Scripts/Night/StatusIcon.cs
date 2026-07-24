using TMPro;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// One state marker shown above an enemy: the state's icon plus how many marks it has (e.g. a flame
    /// with "3" beside it). One of these is instantiated per active state by the <see cref="EnemySlot"/>.
    /// </summary>
    public class StatusIcon : MonoBehaviour
    {
        [Tooltip("The state's icon image.")]
        [SerializeField] private Image icon;
        [Tooltip("The mark count shown next to the icon.")]
        [SerializeField] private TextMeshProUGUI countText;
        [Tooltip("Optional hover tooltip (auto-found on this object). The state's name + description are set here.")]
        [SerializeField] private TooltipTrigger tooltip;

        private void Awake()
        {
            if (tooltip == null) tooltip = GetComponent<TooltipTrigger>();
        }

        /// <summary>
        /// Sets the icon sprite, the mark count and, on the shared prefab, the state's name and description
        /// (the prefab carries the tooltip but not the text — every state uses the same prefab).
        /// </summary>
        public void Set(Sprite sprite, int count, string stateName = null, string description = null)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            if (countText != null) countText.text = count.ToString();
            if (tooltip == null) tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null && (stateName != null || description != null))
                tooltip.SetGeneral(stateName, description);
        }
    }
}
