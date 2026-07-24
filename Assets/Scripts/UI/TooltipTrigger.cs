using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.UI
{
    /// <summary>
    /// Add to any GameObject to show a tooltip on hover. The <see cref="TooltipKind"/> picks the layout:
    /// Basic (description), General (title + description), Dice (title + description + 6 face sprites) or
    /// Effect (title + description + duration). Static text can be set in the inspector, or filled at
    /// runtime from data via the setters. Needs to be raycast-hittable so pointer enter/exit fire.
    /// </summary>
    [DisallowMultipleComponent]
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        [Tooltip("Which corner of the cursor the tooltip appears at.")]
        [SerializeField] private TooltipDirection direction = TooltipDirection.TR;

        [Tooltip("Which tooltip layout to use.")]
        [SerializeField] private TooltipKind kind = TooltipKind.Basic;

        [Tooltip("Title (General / Dice / Effect).")]
        [SerializeField] private string title;

        [Tooltip("Description / message. If empty (and no title), no tooltip appears.")]
        [SerializeField, TextArea] private string message;

        [Tooltip("Duration line (Effect only).")]
        [SerializeField] private string duration;

        private Sprite[] _faces; // Dice faces, set at runtime
        private string _diceLuck; // Dice luck line, set at runtime

        private bool HasContent => !string.IsNullOrWhiteSpace(message) || !string.IsNullOrWhiteSpace(title);

        /// <summary>Basic: just a description.</summary>
        public void SetMessage(string value)
        {
            message = value;
            kind = TooltipKind.Basic;
        }

        /// <summary>General: a title and a description.</summary>
        public void SetGeneral(string titleValue, string description)
        {
            title = titleValue;
            message = description;
            kind = TooltipKind.General;
        }

        /// <summary>Dice: a title, a description, the die's 6 face sprites and a luck line.</summary>
        public void SetDice(string titleValue, string description, Sprite[] faces, string luck = "")
        {
            title = titleValue;
            message = description;
            _faces = faces;
            _diceLuck = luck;
            kind = TooltipKind.Dice;
        }

        /// <summary>Updates just the luck line (the deck's live luck % changes with luck effects).</summary>
        public void SetDiceLuck(string luck) => _diceLuck = luck;

        /// <summary>Effect: a title, a description and a duration line.</summary>
        public void SetEffect(string titleValue, string description, string durationValue)
        {
            title = titleValue;
            message = description;
            duration = durationValue;
            kind = TooltipKind.Effect;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (HasContent) ShowTooltip(eventData.position);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (HasContent && TooltipManager.Instance != null)
                TooltipManager.Instance.UpdatePosition(direction, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TooltipManager.Instance != null) TooltipManager.Instance.Hide();
        }

        private void ShowTooltip(Vector2 position)
        {
            TooltipManager m = TooltipManager.Instance;
            if (m == null) return;
            switch (kind)
            {
                case TooltipKind.General: m.ShowGeneral(title, message, direction, position); break;
                case TooltipKind.Dice: m.ShowDice(title, message, _faces, _diceLuck, direction, position); break;
                case TooltipKind.Effect: m.ShowEffect(title, message, duration, direction, position); break;
                default: m.ShowBasic(message, direction, position); break;
            }
        }
    }
}
