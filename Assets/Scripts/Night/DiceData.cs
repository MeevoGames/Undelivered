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

        /// <summary>The 6 face sprites, in order (for tooltips).</summary>
        public Sprite[] FaceSprites()
        {
            var sprites = new Sprite[FaceCount];
            if (faces != null)
                for (int i = 0; i < sprites.Length && i < faces.Length; i++)
                    sprites[i] = faces[i] != null ? faces[i].Sprite : null;
            return sprites;
        }

        /// <summary>The highest value among the faces.</summary>
        public int MaxFaceValue
        {
            get
            {
                int max = int.MinValue;
                if (faces != null)
                    foreach (DiceFace f in faces)
                        if (f != null && f.Value > max) max = f.Value;
                return max == int.MinValue ? 0 : max;
            }
        }

        /// <summary>Natural luck: the fraction of faces showing the highest value (e.g. 1-1-1-6-6-6 → 0.5).</summary>
        public float BaseLuckFraction
        {
            get
            {
                if (faces == null || faces.Length == 0) return 0f;
                int max = MaxFaceValue, maxCount = 0, total = 0;
                foreach (DiceFace f in faces)
                {
                    if (f == null) continue;
                    total++;
                    if (f.Value == max) maxCount++;
                }
                return total > 0 ? (float)maxCount / total : 0f;
            }
        }

        /// <summary>The natural luck (chance of the highest face) as a whole percentage.</summary>
        public int BaseLuckPercent => Mathf.RoundToInt(BaseLuckFraction * 100f);

        /// <summary>
        /// Rolls with luck: <paramref name="luckFraction"/> is the chance of landing on the highest face.
        /// At the natural luck it reproduces <see cref="Roll"/>; higher biases toward the max face.
        /// </summary>
        public DiceFace RollBiased(float luckFraction)
        {
            if (faces == null || faces.Length == 0) return null;

            int max = MaxFaceValue, maxCount = 0, nonMaxCount = 0;
            foreach (DiceFace f in faces)
            {
                if (f == null) continue;
                if (f.Value == max) maxCount++; else nonMaxCount++;
            }
            if (maxCount == 0) return Roll();

            bool pickMax = nonMaxCount == 0 || Random.value < luckFraction;
            int target = Random.Range(0, pickMax ? maxCount : nonMaxCount);
            int seen = 0;
            foreach (DiceFace f in faces)
            {
                if (f == null) continue;
                if ((f.Value == max) != pickMax) continue;
                if (seen == target) return f;
                seen++;
            }
            return Roll();
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
