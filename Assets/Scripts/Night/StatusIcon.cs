using TMPro;
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

        /// <summary>Sets the icon sprite and the mark count.</summary>
        public void Set(Sprite sprite, int count)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            if (countText != null) countText.text = count.ToString();
        }
    }
}
