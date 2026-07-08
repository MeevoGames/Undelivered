using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.Work
{
    /// <summary>
    /// Put this on the table (a raycast-target Graphic). Right-clicking the empty table while the
    /// forklift is carrying boxes drops them all back onto it. Clicks that land on a box hit the box
    /// instead (its own handler), so only clicks on bare table reach here.
    /// </summary>
    public class ForkliftDropArea : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right
                && Forklift.Instance != null && Forklift.Instance.HasBoxes)
            {
                Forklift.Instance.DropAll();
            }
        }
    }
}
