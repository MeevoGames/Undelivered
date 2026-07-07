using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Items
{
    /// <summary>
    /// Holds the gift cards the player has collected (for the future night dice game) and lists them
    /// in the Items panel. Opening a dice package grants a random card from the pool.
    /// </summary>
    public class ItemsManager : MonoBehaviour
    {
        public static ItemsManager Instance { get; private set; }

        [Tooltip("Possible gift cards granted when opening a dice package.")]
        [SerializeField] private List<GiftCardData> giftCardPool = new List<GiftCardData>();

        [Tooltip("Prefab of a single gift-card UI item (must have a GiftCardItem).")]
        [SerializeField] private GiftCardItem itemPrefab;

        [Tooltip("Parent (the Items GameObject) the collected cards are listed under.")]
        [SerializeField] private Transform itemsParent;

        private readonly List<GiftCardData> _collected = new List<GiftCardData>();

        /// <summary>The cards collected so far (the night system will consume these later).</summary>
        public IReadOnlyList<GiftCardData> Collected => _collected;

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

        /// <summary>Grants a random gift card from the pool and lists it. Returns the card, or null.</summary>
        public GiftCardData GrantRandomGiftCard()
        {
            if (giftCardPool == null || giftCardPool.Count == 0)
            {
                Debug.LogWarning($"{nameof(ItemsManager)} has no gift cards in the pool.", this);
                return null;
            }

            GiftCardData card = giftCardPool[Random.Range(0, giftCardPool.Count)];
            AddGiftCard(card);
            return card;
        }

        /// <summary>Adds a specific gift card to the collection and lists it in the Items panel.</summary>
        public void AddGiftCard(GiftCardData card)
        {
            if (card == null)
            {
                return;
            }

            _collected.Add(card);

            if (itemPrefab != null && itemsParent != null)
            {
                GiftCardItem item = Instantiate(itemPrefab, itemsParent, false);
                item.Setup(card);
            }
        }
    }
}
