using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.UI
{
    /// <summary>
    /// Add to any GameObject to show a tooltip on hover. Configure the message and which corner of
    /// the cursor the tooltip appears at. Needs to be raycast-hittable (a raycast-target Graphic for
    /// UI, or a collider + physics raycaster otherwise) so pointer enter/exit fire.
    /// </summary>
    [DisallowMultipleComponent]
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        [Tooltip("Which corner of the cursor the tooltip appears at.")]
        [SerializeField] private TooltipDirection direction = TooltipDirection.TR;

        [Tooltip("Message shown in the tooltip. If empty, no tooltip appears on hover.")]
        [SerializeField, TextArea] private string message;

        // No tooltip when there is no message.
        private bool HasMessage => !string.IsNullOrWhiteSpace(message);

        /// <summary>Sets the tooltip message at runtime (e.g. from item data). Empty = no tooltip.</summary>
        public void SetMessage(string value)
        {
            message = value;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (HasMessage && TooltipManager.Instance != null)
            {
                TooltipManager.Instance.Show(message, direction, eventData.position);
            }
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (HasMessage && TooltipManager.Instance != null)
            {
                TooltipManager.Instance.UpdatePosition(direction, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (HasMessage && TooltipManager.Instance != null)
            {
                TooltipManager.Instance.Hide();
            }
        }
    }
}
