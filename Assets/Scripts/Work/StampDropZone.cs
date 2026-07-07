using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.Work
{
    /// <summary>
    /// Drop target on the enlarged box view: dropping the quality stamp here stamps the box the
    /// inspector is currently showing. Requires a raycast-target Graphic.
    /// </summary>
    public class StampDropZone : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
            {
                return;
            }

            if (eventData.pointerDrag.GetComponent<QualityStamp>() == null)
            {
                return; // only the stamp stamps
            }

            if (BoxInspector.Instance != null)
            {
                BoxInspector.Instance.StampCurrentBox();
            }
        }
    }
}
