using Undelivered.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.Work
{
    /// <summary>
    /// Slot that discards any item dropped on it. Trash is thrown away for free; a valuable box
    /// costs the gold it would have paid if classified. Either way the item is destroyed.
    ///
    /// Requires a raycast-target Graphic (e.g. an Image with Raycast Target on) so drops register.
    /// </summary>
    public class PaperBinSlot : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
            {
                return;
            }

            GameObject dropped = eventData.pointerDrag;

            // A box has value: trashing it loses the gold it would have paid. Trash loses nothing.
            Box box = dropped.GetComponent<Box>();
            if (box != null)
            {
                int value = BoxManager.Instance != null ? BoxManager.Instance.GetGoldReward(box.Type) : 0;
                if (value != 0 && StatsManager.Instance != null)
                {
                    StatsManager.Instance.AddGold(-value);
                }
            }

            Destroy(dropped);
        }
    }
}
