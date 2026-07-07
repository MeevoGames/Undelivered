using UnityEngine;

namespace Undelivered.Items
{
    /// <summary>
    /// A gift card won by opening a dice package: a reward for the (not yet built) night dice game.
    /// It always has a sprite, a name and a short description (max 50 chars). The kind and value are
    /// metadata so the future night system can act on it without needing that system to exist yet.
    /// </summary>
    [CreateAssetMenu(fileName = "GiftCard", menuName = "Undelivered/Gift Card")]
    public class GiftCardData : ScriptableObject
    {
        public enum Kind
        {
            Multiplier,          // value = 2..6
            ActivableMultiplier, // value = 4..6
            LuckMultiplier,      // value = 2..6
            DiceBox,
            MultiplierBox,
            LuckBox,
            SpecificDie          // pending: wired when the dice system exists
        }

        [SerializeField] private Sprite sprite;
        [SerializeField] private string cardName;
        [SerializeField, TextArea] private string description;

        [Header("Night-game metadata (used later)")]
        [SerializeField] private Kind kind;
        [Tooltip("Multiplier amount (2-6) for multiplier cards; ignored for boxes / specific die.")]
        [SerializeField] private int value;

        public Sprite Sprite => sprite;
        public string CardName => cardName;
        public string Description => description;
        public Kind CardKind => kind;
        public int Value => value;

        private const int MaxDescriptionLength = 50;

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(description) && description.Length > MaxDescriptionLength)
            {
                description = description.Substring(0, MaxDescriptionLength);
            }
        }
    }
}
