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
        public static float DirectionLabelChance; // "Etiquetadora": 0.5/0.75/1 per level, else 0
        public static bool ShowDiceLabel;          // "Etiqueta de dados"
        public static float WeightLabelChance;     // "Etiqueta de Peso": 0.5/0.75/1 per level, else 0
        public static bool AutoStamp;                    // "Sello automático": stamps every box
        public static float StampRewardMultiplier = 1f;  // "Sello de calidad" sets it per level
        public static int StampRewardFlat;               // "Sello automático" sets it to +2

        [SerializeField] private BoxType type;

        [Tooltip("Dices toggle: when on, the box also carries dice.")]
        [SerializeField] private bool isDice;

        [Tooltip("Box weight. Heavier non-dice boxes tend to pay more when opened.")]
        [SerializeField] private int weight;

        [Header("Labels / stamp (revealed by upgrades or actions)")]
        [Tooltip("Direction label. Shown when this box rolled a direction label (labeler upgrade).")]
        [SerializeField] private Image typeLabel;
        [Tooltip("Dice label. Shown with the dice-label upgrade (and if it's a dice box).")]
        [SerializeField] private Image diceLabel;
        [Tooltip("Weight label text. Shown when this box rolled a weight label.")]
        [SerializeField] private TextMeshProUGUI weightLabelText;
        [Tooltip("Quality stamp mark. Shown when this box has been stamped.")]
        [SerializeField] private Image stampImage;
        [Tooltip("Optional mark shown when the box is broken (a broken prefab can also look broken on its own).")]
        [SerializeField] private GameObject brokenMark;

        [Tooltip("Whether this box is broken (fallado): pays half when classified, until repackaged.")]
        [SerializeField] private bool isBroken;

        private bool hasDirectionLabel;
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
        public bool HasDirectionLabel => hasDirectionLabel;
        public bool HasWeightLabel => hasWeightLabel;
        public bool HasStamp => hasStamp;
        public bool IsBroken => isBroken;

        private void Start()
        {
            RefreshLabels();
        }

        /// <summary>Sets the box's data, rolls its direction/weight labels and auto-stamp, then refreshes.</summary>
        public void SetData(BoxType boxType, bool dice, int boxWeight)
        {
            type = boxType;
            isDice = dice;
            weight = boxWeight;
            RollDirectionLabel();
            RollWeightLabel();
            if (AutoStamp)
            {
                hasStamp = true;
            }
            RefreshLabels();
        }

        /// <summary>Rolls whether this box comes with a direction label (based on the upgrade chance).</summary>
        public void RollDirectionLabel()
        {
            hasDirectionLabel = Random.value < DirectionLabelChance;
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

        /// <summary>Marks the box as broken (or repaired) and refreshes its display.</summary>
        public void SetBroken(bool broken)
        {
            isBroken = broken;
            RefreshLabels();
        }

        /// <summary>Copies another box's data (type, dice, weight, labels, stamp) — but not its broken state.</summary>
        public void CopyStateFrom(Box source)
        {
            if (source == null)
            {
                return;
            }
            type = source.type;
            isDice = source.isDice;
            weight = source.weight;
            hasDirectionLabel = source.hasDirectionLabel;
            hasWeightLabel = source.hasWeightLabel;
            hasStamp = source.hasStamp;
            RefreshLabels();
        }

        /// <summary>Shows/hides each label and the stamp based on this box's state.</summary>
        public void RefreshLabels()
        {
            BoxManager manager = BoxManager.Instance;

            if (typeLabel != null)
            {
                if (hasDirectionLabel && manager != null)
                {
                    typeLabel.sprite = manager.GetLabelSprite(type);
                }
                typeLabel.gameObject.SetActive(hasDirectionLabel);
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

            if (brokenMark != null)
            {
                brokenMark.SetActive(isBroken);
            }
        }

        // Right-click hands the box to the forklift (pick up / return); left-click opens the enlarged view.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (Forklift.Enabled && Forklift.Instance != null)
                {
                    Forklift.Instance.OnBoxClicked(this);
                }
                return;
            }

            BoxInspector.Show(this);
        }
    }
}
