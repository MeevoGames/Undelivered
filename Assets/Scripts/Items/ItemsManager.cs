using System.Collections.Generic;
using Undelivered.Night;
using UnityEngine;

namespace Undelivered.Items
{
    /// <summary>
    /// The day's "items obtained" section. Opening a dice package grants a random night item — a die,
    /// effect or box (the same kinds sold in the night shop) — which is added to the night
    /// <see cref="Inventory"/> and listed here. The list is cleared when returning to the day mode.
    /// </summary>
    public class ItemsManager : MonoBehaviour
    {
        public static ItemsManager Instance { get; private set; }

        [Header("Pools (the current night items — dice / effects / boxes)")]
        [SerializeField] private List<DiceData> dicePool = new List<DiceData>();
        [SerializeField] private List<EffectData> effectPool = new List<EffectData>();
        [SerializeField] private List<BoxData> boxPool = new List<BoxData>();

        [Tooltip("Prefab of a single obtained-item entry (a night ItemView: icon, no price).")]
        [SerializeField] private ItemView itemPrefab;

        [Tooltip("Parent (the Items GameObject) the obtained items are listed under.")]
        [SerializeField] private Transform itemsParent;

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

        /// <summary>
        /// Grants a random night item (die / effect / box), adds it to the night <see cref="Inventory"/>
        /// and lists it in the Items panel. Returns the item, or null if every pool is empty.
        /// </summary>
        public IItem GrantRandomItem()
        {
            var categories = new List<int>();
            if (dicePool.Count > 0) categories.Add(0);
            if (effectPool.Count > 0) categories.Add(1);
            if (boxPool.Count > 0) categories.Add(2);
            if (categories.Count == 0)
            {
                Debug.LogWarning($"{nameof(ItemsManager)} has no dice / effects / boxes in its pools.", this);
                return null;
            }

            IItem item = null;
            switch (categories[Random.Range(0, categories.Count)])
            {
                case 0:
                    DiceData die = dicePool[Random.Range(0, dicePool.Count)];
                    if (Inventory.Instance != null) Inventory.Instance.AddDie(die);
                    item = die;
                    break;
                case 1:
                    EffectData effect = effectPool[Random.Range(0, effectPool.Count)];
                    if (Inventory.Instance != null) Inventory.Instance.AddEffect(effect);
                    item = effect;
                    break;
                default:
                    BoxData box = boxPool[Random.Range(0, boxPool.Count)];
                    if (Inventory.Instance != null) Inventory.Instance.AddBox(box);
                    item = box;
                    break;
            }

            if (item != null && itemPrefab != null && itemsParent != null)
            {
                Instantiate(itemPrefab, itemsParent, false).Setup(item);
            }
            return item;
        }

        /// <summary>Clears the obtained-items list (called when returning to the day mode). The items stay in the night Inventory.</summary>
        public void Clear()
        {
            if (itemsParent == null) return;
            for (int i = itemsParent.childCount - 1; i >= 0; i--)
            {
                Destroy(itemsParent.GetChild(i).gameObject);
            }
        }
    }
}
