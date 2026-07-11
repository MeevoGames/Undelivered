using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// A consumable effect. Has a name, tooltip, price, rarity and an image. The rarity is shown as the
    /// colour of the item's border (a background image) wherever the effect is listed. What each effect
    /// does is resolved later. Implements <see cref="IItem"/> so the shop can list it.
    /// </summary>
    [CreateAssetMenu(fileName = "Effect", menuName = "Undelivered/Night/Effect")]
    public class EffectData : ScriptableObject, IItem
    {
        public enum Rarity { Comun, Rara, Epica }

        [SerializeField] private string effectName;
        [SerializeField, TextArea] private string descriptionForTooltip;
        [SerializeField] private Sprite icon;
        [Tooltip("Price in gems.")]
        [SerializeField] private int price = 10;
        [SerializeField] private Rarity rarity;

        public string EffectName => effectName;
        public Sprite Icon => icon;
        public int Price => price;
        public Rarity EffectRarity => rarity;
        public string DescriptionForTooltip => descriptionForTooltip;

        // IItem
        public string ItemName => effectName;
    }
}
