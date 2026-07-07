using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.Work
{
    /// <summary>
    /// The scale (Balanza): drop a box on it to display its weight. The box is snapped so its
    /// bottom-right corner sits at the center of the scale, and it is not consumed (the player can
    /// drag it off afterward). Requires a raycast-target Graphic and a DragZone so boxes can be
    /// dropped here.
    /// </summary>
    public class Scale : MonoBehaviour, IDropHandler
    {
        [Tooltip("Text that shows the weight of the box dropped on the scale.")]
        [SerializeField] private TextMeshProUGUI weightText;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
            {
                return;
            }

            Box box = eventData.pointerDrag.GetComponent<Box>();
            if (box == null)
            {
                return;
            }

            if (weightText != null)
            {
                weightText.text = box.Weight.ToString();
            }

            SnapBottomRightToCenter(box.transform as RectTransform);
        }

        // Moves the box so its bottom-right corner lands exactly on the center of the scale.
        private void SnapBottomRightToCenter(RectTransform boxRect)
        {
            if (boxRect == null)
            {
                return;
            }

            RectTransform scaleRect = (RectTransform)transform;
            Vector3 centerWorld = scaleRect.TransformPoint(scaleRect.rect.center);
            Vector3 boxBottomRightWorld = boxRect.TransformPoint(new Vector3(boxRect.rect.xMax, boxRect.rect.yMin, 0f));

            boxRect.position += centerWorld - boxBottomRightWorld;
        }
    }
}
