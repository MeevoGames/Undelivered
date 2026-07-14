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

        /// <summary>Active → normal sprite; not applying → the broken sprite (falls back to the normal one).</summary>
        public void SetFulfilled(bool fulfilled)
        {
            if (image == null || _data == null) return;
            Sprite broken = _data.BrokenIcon != null ? _data.BrokenIcon : _data.Icon;
            image.sprite = fulfilled ? _data.Icon : broken;
        }
    }
}
