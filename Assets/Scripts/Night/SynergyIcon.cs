using Undelivered.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// One icon in the on-screen synergy list. It shows a synergy that is possible in the current combat;
    /// its <see cref="TooltipTrigger"/> (General) names and describes it on hover. While the synergy is
    /// active it shows the normal illustration; once it stops applying it swaps to the broken illustration
    /// (it does not disappear). Removed entirely only when the combat ends.
    /// </summary>
    public class SynergyIcon : MonoBehaviour
    {
        [SerializeField] private Image image;

        private SynergyData _data;

        public SynergyData Data => _data;

        public void Setup(SynergyData data)
        {
            _data = data;
            if (data == null) return;

            if (image != null) image.sprite = data.Icon;

            TooltipTrigger tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null) tooltip.SetGeneral(data.SynergyName, data.Description);
        }

        /// <summary>
        /// Active → the normal sprite. Not applying → the broken sprite if the synergy has one; if it
        /// doesn't, the icon is hidden instead, so a dropped synergy always reads on screen.
        /// </summary>
        public void SetFulfilled(bool fulfilled)
        {
            if (image == null || _data == null) return;

            if (fulfilled)
            {
                image.enabled = true;
                image.sprite = _data.Icon;
            }
            else if (_data.BrokenIcon != null)
            {
                image.enabled = true;
                image.sprite = _data.BrokenIcon;
            }
            else
            {
                image.enabled = false;
            }
        }
    }
}
