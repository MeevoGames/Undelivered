using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// A die: a name, a tooltip and exactly six faces (its personality — the distribution of values).
    /// <see cref="Roll"/> returns a random face.
    /// </summary>
    [CreateAssetMenu(fileName = "Dice", menuName = "Undelivered/Night/Dice")]
    public class DiceData : ScriptableObject, IItem
    {
        public const int FaceCount = 6;

        [SerializeField] private string diceName;
        [SerializeField, TextArea] private string descriptionForTooltip;

        [Tooltip("The six faces. A die always has exactly 6.")]
        [SerializeField] private DiceFace[] faces = new DiceFace[FaceCount];

        [Tooltip("The die's own image for the shop and inventory lists (distinct from the face sprites).")]
        [SerializeField] private Sprite icon;

        [Tooltip("Base price in gems (shown in the shop).")]
        [SerializeField] private int price;

        [Tooltip("Die level / tier. Higher-level dice are rarer in low-rarity boxes, common in high ones.")]
        [SerializeField] private int level = 1;

        public string DiceName => diceName;
        public string DescriptionForTooltip => descriptionForTooltip;
        public DiceFace[] Faces => faces;
        public Sprite Icon => icon;
        public int Price => price;
        public int Level => level;

        // IItem
        public string ItemName => diceName;

        /// <summary>Rolls the die, returning a random face (or null if it somehow has none).</summary>
        public DiceFace Roll()
        {
            if (faces == null || faces.Length == 0)
            {
                return null;
            }
            return faces[Random.Range(0, faces.Length)];
        }

        private void OnValidate()
        {
            // A die always has exactly six faces.
            if (faces == null)
            {
                faces = new DiceFace[FaceCount];
            }
            else if (faces.Length != FaceCount)
            {
                System.Array.Resize(ref faces, FaceCount);
            }
        }
    }
}
