using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Work
{
    /// <summary>
    /// Enlarged view of a box shown when the player clicks it. Always reveals the type in text; also
    /// shows the direction label (with the labeler upgrade), the dice label (with the dice-label
    /// upgrade), the weight (if the box came weight-labeled) and the quality stamp (if stamped).
    /// Keeps a reference to the shown box so the stamp drag can apply to it.
    /// </summary>
    public class BoxInspector : MonoBehaviour
    {
        public static BoxInspector Instance { get; private set; }

        [Tooltip("The enlarged view panel. Should start disabled.")]
        [SerializeField] private GameObject view;

        [SerializeField] private TextMeshProUGUI typeText;

        [Header("Detail elements (shown per box state / upgrades)")]
        [SerializeField] private Image directionLabel;
        [SerializeField] private Image diceLabel;
        [SerializeField] private TextMeshProUGUI weightText;
        [SerializeField] private Image stampImage;

        private Box _currentBox;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Shows the enlarged view for the given box.</summary>
        public static void Show(Box box)
        {
            if (Instance != null)
            {
                Instance.ShowInternal(box);
            }
        }

        private void ShowInternal(Box box)
        {
            _currentBox = box;
            if (box == null)
            {
                return;
            }

            BoxManager manager = BoxManager.Instance;

            if (typeText != null)
            {
                typeText.text = box.Type.ToString();
            }

            if (directionLabel != null)
            {
                bool show = box.HasDirectionLabel;
                if (show && manager != null)
                {
                    directionLabel.sprite = manager.GetLabelSprite(box.Type);
                }
                directionLabel.gameObject.SetActive(show);
            }

            if (diceLabel != null)
            {
                bool show = box.IsDice && Box.ShowDiceLabel;
                if (show && manager != null)
                {
                    diceLabel.sprite = manager.GetLabelSprite(BoxType.Dice);
                }
                diceLabel.gameObject.SetActive(show);
            }

            if (weightText != null)
            {
                if (box.HasWeightLabel)
                {
                    weightText.text = box.Weight.ToString();
                }
                weightText.gameObject.SetActive(box.HasWeightLabel);
            }

            if (stampImage != null)
            {
                stampImage.gameObject.SetActive(box.HasStamp);
            }

            if (view != null)
            {
                view.SetActive(true);
            }
        }

        /// <summary>Stamps the box currently shown (used by the stamp drag mechanic).</summary>
        public void StampCurrentBox()
        {
            if (_currentBox == null)
            {
                return;
            }
            _currentBox.SetStamped(true);
            if (stampImage != null)
            {
                stampImage.gameObject.SetActive(true);
            }
        }

        /// <summary>Hides the enlarged view (wire this to a close button).</summary>
        public void Hide()
        {
            if (view != null)
            {
                view.SetActive(false);
            }
        }
    }
}
