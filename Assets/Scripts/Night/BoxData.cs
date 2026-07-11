using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// A box that gives dice or effects. Two formats: draw several and pick one (<see cref="drawCount"/>
    /// &gt; 1), or a single random one you keep (<see cref="drawCount"/> == 1). Its <see cref="rarity"/>
    /// biases the draw toward better contents (see <see cref="BoxOpener"/>). Implements <see cref="IItem"/>
    /// so the shop can list it.
    /// </summary>
    [CreateAssetMenu(fileName = "Box", menuName = "Undelivered/Night/Box")]
    public class BoxData : ScriptableObject, IItem
    {
        public enum Content { Dice, Effects }
        public enum Rarity { Comun, Rara, Epica }

        [SerializeField] private string boxName;
        [SerializeField, TextArea] private string descriptionForTooltip;
        [SerializeField] private Sprite icon;
        [Tooltip("Price in gems.")]
        [SerializeField] private int price = 20;

        [SerializeField] private Content content;
        [SerializeField] private Rarity rarity;

        [Tooltip("3 = draw 3 and pick 1; 1 = a single random one you keep.")]
        [SerializeField] private int drawCount = 3;

        public string BoxName => boxName;
        public Sprite Icon => icon;
        public int Price => price;
        public string DescriptionForTooltip => descriptionForTooltip;
        public Content BoxContent => content;
        public Rarity BoxRarity => rarity;
        public int DrawCount => Mathf.Max(1, drawCount);

        // IItem
        public string ItemName => boxName;
    }
}
