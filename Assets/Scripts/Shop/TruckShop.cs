using System.Collections.Generic;
using Undelivered.Player;
using Undelivered.Work;
using UnityEngine;

namespace Undelivered.Shop
{
    /// <summary>
    /// Left-side shop panel listing the trucks the player can buy. Builds one <see cref="TruckShopItem"/>
    /// per truck, keeps their affordability in sync with the player's gold, and handles purchases:
    /// it spends the gold (with a floating text), records it in the day stats, and tells the
    /// <see cref="TruckManager"/> to bring the boxes in.
    /// </summary>
    public class TruckShop : MonoBehaviour
    {
        [Tooltip("Trucks available for sale in this shop.")]
        [SerializeField] private List<TruckData> trucksForSale = new List<TruckData>();

        [Tooltip("Prefab of a single shop row (must have a TruckShopItem).")]
        [SerializeField] private TruckShopItem itemPrefab;

        [Tooltip("Parent under which the shop rows are instantiated (the left panel content).")]
        [SerializeField] private Transform itemsParent;

        [Tooltip("Manager that spawns the boxes when a truck is bought.")]
        [SerializeField] private TruckManager truckManager;

        private readonly List<TruckShopItem> _items = new List<TruckShopItem>();
        private bool _subscribed;

        private void Start()
        {
            BuildItems();
            TrySubscribe();
            RefreshAffordability();
        }

        private void OnEnable() => TrySubscribe();

        private void OnDisable()
        {
            if (_subscribed && StatsManager.Instance != null)
            {
                StatsManager.Instance.GoldChanged -= OnGoldChanged;
            }
            _subscribed = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed || StatsManager.Instance == null)
            {
                return;
            }
            StatsManager.Instance.GoldChanged += OnGoldChanged;
            _subscribed = true;
        }

        private void OnGoldChanged(int gold) => RefreshAffordability(gold);

        private void BuildItems()
        {
            if (itemPrefab == null || itemsParent == null)
            {
                Debug.LogWarning($"{nameof(TruckShop)} needs an item prefab and a parent assigned.", this);
                return;
            }

            foreach (TruckData truck in trucksForSale)
            {
                if (truck == null)
                {
                    continue;
                }
                TruckShopItem item = Instantiate(itemPrefab, itemsParent, false);
                item.Setup(truck, this);
                _items.Add(item);
            }
        }

        private void RefreshAffordability()
        {
            int gold = StatsManager.Instance != null ? StatsManager.Instance.Gold : 0;
            RefreshAffordability(gold);
        }

        private void RefreshAffordability(int gold)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].UpdateAffordable(gold);
            }
        }

        /// <summary>Buys a truck if the player can afford it: spends gold, logs it, and spawns the boxes.</summary>
        public void TryBuy(TruckData truck)
        {
            if (truck == null)
            {
                return;
            }

            int gold = StatsManager.Instance != null ? StatsManager.Instance.Gold : 0;
            if (gold < truck.Price)
            {
                Debug.LogWarning($"No alcanza para comprar '{truck.name}' (precio {truck.Price}, oro {gold}).");
                return;
            }

            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.AddGold(-truck.Price);
            }

            if (DayManager.Instance != null)
            {
                DayManager.Instance.RegisterGoldSpent(truck.Price);
            }

            if (truckManager != null)
            {
                truckManager.SpawnTruck(truck);
            }
        }
    }
}
