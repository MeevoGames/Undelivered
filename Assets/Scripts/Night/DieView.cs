using System;
using TMPro;
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

        [Tooltip("Optional: shows this die's luck % (chance of its highest face).")]
        [SerializeField] private TextMeshProUGUI luckText;
        [Tooltip("Colour of the luck text while a luck effect is boosting it (#58BE4E).")]
        [SerializeField] private Color luckBoostedColor = new Color(0.345f, 0.745f, 0.306f);
        private Color _luckDefaultColor = Color.white;

        private CanvasGroup _canvasGroup;
        private Action _onClick;

        public DiceData Data { get; private set; }

        /// <summary>Already thrown this deck cycle: dimmed and untappable until the deck refreshes.</summary>
        public bool Spent { get; private set; }

        /// <summary>Locked spent for the whole combat: refresh/renew can't bring it back (a Counterattack cost).</summary>
        public bool Locked { get; private set; }

        public void SetLocked(bool locked) => Locked = locked;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (luckText != null) _luckDefaultColor = luckText.color; // remember the un-boosted colour
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

        /// <summary>Shows this die's luck as a percentage (deck only); <paramref name="boosted"/> tints it (#58BE4E) while a luck effect is active.</summary>
        public void SetLuck(int percent, bool boosted)
        {
            if (luckText == null) return;
            luckText.gameObject.SetActive(true);
            luckText.text = percent + "%";
            luckText.color = boosted ? luckBoostedColor : _luckDefaultColor;
        }

        /// <summary>Hides the luck number — for dice shown outside the deck (thrown, enemy detail, level-up).</summary>
        public void HideLuck()
        {
            if (luckText != null) luckText.gameObject.SetActive(false);
        }

        public void Setup(DiceData die)
        {
            Data = die;
            if (die == null) return;

            ShowFace(FaceAt(restingFace));

            TooltipTrigger tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null) tooltip.SetDice(die.DiceName, die.DescriptionForTooltip, die.FaceSprites());
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

        /// <summary>Sets the face image to a specific sprite (e.g. a NumberTransform swapping the result).</summary>
        public void SetFaceSprite(Sprite sprite)
        {
            if (faceImage == null) return;
            faceImage.sprite = sprite;
            faceImage.enabled = sprite != null;
        }

        private DiceFace FaceAt(int index)
        {
            if (Data == null || Data.Faces == null || Data.Faces.Length == 0) return null;
            index = Mathf.Clamp(index, 0, Data.Faces.Length - 1);
            return Data.Faces[index];
        }
    }
}
