using System;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The die as shown in the deck: just one face's image (nothing else). Holds the die it shows;
    /// <see cref="ShowFace"/> lets a roll swap the visible face later. In the deck it is tappable: a tap
    /// throws it (wired by <see cref="Deck"/> to the <see cref="DiceThrower"/>).
    /// </summary>
    public class DieView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image faceImage;

        [Tooltip("Which face (0-5) is shown at rest.")]
        [SerializeField] private int restingFace;
        [SerializeField, Range(0f, 1f)] private float spentAlpha = 0.2f;

        private CanvasGroup _canvasGroup;
        private Action _onClick;

        public DiceData Data { get; private set; }

        /// <summary>Already thrown this deck cycle: dimmed and untappable until the deck refreshes.</summary>
        public bool Spent { get; private set; }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void SetClick(Action onClick) => _onClick = onClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Spent) _onClick?.Invoke();
        }

        public void SetSpent(bool spent)
        {
            Spent = spent;
            if (_canvasGroup != null) _canvasGroup.alpha = spent ? spentAlpha : 1f;
        }

        public void Setup(DiceData die)
        {
            Data = die;
            if (die == null) return;

            ShowFace(FaceAt(restingFace));

            TooltipTrigger tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null) tooltip.SetMessage(die.DescriptionForTooltip);
        }

        /// <summary>Shows a specific face on the image (used at rest and while rolling).</summary>
        public void ShowFace(DiceFace face)
        {
            if (faceImage == null) return;
            faceImage.sprite = face != null ? face.Sprite : null;
            // Visible for any real (non-empty) face — even before its sprite is assigned it renders as
            // a plain box, so the slot shows; an empty face shows nothing.
            faceImage.enabled = face != null && !face.Empty;
        }

        private DiceFace FaceAt(int index)
        {
            if (Data == null || Data.Faces == null || Data.Faces.Length == 0) return null;
            index = Mathf.Clamp(index, 0, Data.Faces.Length - 1);
            return Data.Faces[index];
        }
    }
}
