using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Undelivered.Work
{
    /// <summary>
    /// A package/box the player sorts. Has a principal <see cref="BoxType"/>, a dice flag and a
    /// weight. Its labels (direction, dice, weight) and quality stamp are hidden during normal play
    /// and revealed by upgrades / player actions. Clicking a box opens an enlarged view.
    /// </summary>
    public class Box : MonoBehaviour, IPointerClickHandler
    {
        // Upgrade-driven state (off in normal play; the upgrade system sets these).
        public static bool ShowDirectionLabel;  // "Etiquetadora"
        public static bool ShowDiceLabel;        // "Etiqueta de dados"
        public static float WeightLabelChance;   // "Etiqueta de Peso": 0.8 when owned, else 0
        public static bool AutoStamp;                    // "Sello automático": stamps every box
        public static float StampRewardMultiplier = 1f;  // manual "Sello de calidad" sets it to 1.5
        public static int StampRewardFlat;               // "Sello automático" sets it to +2

        [SerializeField] private BoxType type;

        [Tooltip("Dices toggle: when on, the box also carries dice.")]
        [SerializeField] private bool isDice;

        [Tooltip("Box weight. Heavier non-dice boxes tend to pay more when opened.")]
        [SerializeField] private int weight;

        [Header("Labels / stamp (revealed by upgrades or actions)")]
        [Tooltip("Direction label. Shown with the labeler upgrade.")]
        [SerializeField] private Image typeLabel;
        [Tooltip("Dice label. Shown with the dice-label upgrade (and if it's a dice box).")]
        [SerializeField] private Image diceLabel;
        [Tooltip("Weight label text. Shown when this box came with a weight label.")]
        [SerializeField] private TextMeshProUGUI weightLabelText;
        [Tooltip("Quality stamp mark. Shown when this box has been stamped.")]
        [SerializeField] private Image stampImage;

        private bool hasWeightLabel;
        private bool hasStamp;

        public BoxType Type
        {
            get => type;
            set => type = value;
        }

        public bool IsDice
        {
            get => isDice;
            set => isDice = value;
        }

        public int Weight => weight;
        public bool HasWeightLabel => hasWeightLabel;
        public bool HasStamp => hasStamp;

        private void Start()
        {
            RefreshLabels();
        }

        /// <summary>Sets the box's type, dice flag and weight, rolls its weight label, then refreshes.</summary>
        public void SetData(BoxType boxType, bool dice, int boxWeight)
        {
            type = boxType;
            isDice = dice;
            weight = boxWeight;
            RollWeightLabel();
            if (AutoStamp)
            {
                hasStamp = true;
            }
            RefreshLabels();
        }

        /// <summary>Rolls whether this box comes with a weight label (based on the upgrade chance).</summary>
        public void RollWeightLabel()
        {
            hasWeightLabel = Random.value < WeightLabelChance;
        }

        /// <summary>Marks the box as stamped (quality stamp) and refreshes its display.</summary>
        public void SetStamped(bool stamped)
        {
            hasStamp = stamped;
            RefreshLabels();
        }

        /// <summary>Shows/hides each label and the stamp based on the upgrade toggles and box state.</summary>
        public void RefreshLabels()
        {
            BoxManager manager = BoxManager.Instance;

            if (typeLabel != null)
            {
                if (ShowDirectionLabel && manager != null)
                {
                    typeLabel.sprite = manager.GetLabelSprite(type);
                }
                typeLabel.gameObject.SetActive(ShowDirectionLabel);
            }

            if (diceLabel != null)
            {
                bool show = isDice && ShowDiceLabel;
                if (show && manager != null)
                {
                    diceLabel.sprite = manager.GetLabelSprite(BoxType.Dice);
                }
                diceLabel.gameObject.SetActive(show);
            }

            if (weightLabelText != null)
            {
                if (hasWeightLabel)
                {
                    weightLabelText.text = weight.ToString();
                }
                weightLabelText.gameObject.SetActive(hasWeightLabel);
            }

            if (stampImage != null)
            {
                stampImage.gameObject.SetActive(hasStamp);
            }
        }

        // A single click (not a drag) opens the enlarged view for this box.
        public void OnPointerClick(PointerEventData eventData)
        {
            BoxInspector.Show(this);
        }
    }
}
