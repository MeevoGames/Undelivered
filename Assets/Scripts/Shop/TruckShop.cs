using System.Collections.Generic;
using Undelivered.Player;
using Undelivered.Work;
using UnityEngine;

namespace Undelivered.Shop
{
    /// <summary>
    /// Left-side shop panel listing the trucks the player can buy. The ProgressionManager configures it
    /// each day with the available trucks and their per-day stock (refilled every day). Tracks how many
    /// of each have been bought, keeps rows in sync with the player's gold, and handles purchases.
    /// </summary>
    public class TruckShop : MonoBehaviour
    {
        [Tooltip("Trucks for sale if no ProgressionManager configures the shop (uses each truck's own Stock).")]
        [SerializeField] private List<TruckData> trucksForSale = new List<TruckData>();

        [Tooltip("Prefab of a single shop row (must have a TruckShopItem).")]
        [SerializeField] private TruckShopItem itemPrefab;

        [Tooltip("Parent under which the shop rows are instantiated (the left panel content).")]
        [SerializeField] private Transform itemsParent;

        [Tooltip("Manager that spawns the boxes when a truck is bought.")]
        [SerializeField] private TruckManager truckManager;

        private readonly List<TruckShopItem> _items = new List<TruckShopItem>();
        private readonly Dictionary<TruckData, int> _bought = new Dictionary<TruckData, int>();
        private readonly Dictionary<TruckData, int> _stock = new Dictionary<TruckData, int>();
        private bool _subscribed;
        private bool _configured;

        private void Start()
        {
            TrySubscribe();
            if (!_configured)
            {
                BuildFromFallback();
            }
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

        private void OnGoldChanged(int gold) => RefreshItems(gold);

        /// <summary>Rebuilds the shop for a new day with the given trucks and their per-day stock (refilled).</summary>
        public void Configure(List<KeyValuePair<TruckData, int>> trucksWithStock)
        {
            _configured = true;
            Build(trucksWithStock);
        }

        private void BuildFromFallback()
        {
            List<KeyValuePair<TruckData, int>> list = new List<KeyValuePair<TruckData, int>>();
            foreach (TruckData truck in trucksForSale)
            {
                if (truck != null)
                {
                    list.Add(new KeyValuePair<TruckData, int>(truck, truck.Stock));
                }
            }
            Build(list);
        }

        private void Build(List<KeyValuePair<TruckData, int>> trucksWithStock)
        {
            if (itemPrefab == null || itemsParent == null)
            {
                Debug.LogWarning($"{nameof(TruckShop)} needs an item prefab and a parent assigned.", this);
                return;
            }

            ClearItems();
            _bought.Clear(); // stock refills each day
            _stock.Clear();

            foreach (KeyValuePair<TruckData, int> entry in trucksWithStock)
            {
                if (entry.Key == null)
                {
                    continue;
                }
                _stock[entry.Key] = entry.Value;
                TruckShopItem item = Instantiate(itemPrefab, itemsParent, false);
                item.Setup(entry.Key, this);
                _items.Add(item);
            }

            RefreshItems();
        }

        private void ClearItems()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null)
                {
                    Destroy(_items[i].gameObject);
                }
            }
            _items.Clear();
        }

        private void RefreshItems()
        {
            int gold = StatsManager.Instance != null ? StatsManager.Instance.Gold : 0;
            RefreshItems(gold);
        }

        private void RefreshItems(int gold)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].Refresh(gold, Remaining(_items[i].Truck));
            }
        }

        private int GetBought(TruckData truck) => truck != null && _bought.TryGetValue(truck, out int n) ? n : 0;
        private int GetStock(TruckData truck) => truck != null && _stock.TryGetValue(truck, out int s) ? s : 0;
        private int Remaining(TruckData truck) => truck != null ? Mathf.Max(0, GetStock(truck) - GetBought(truck)) : 0;

        /// <summary>Buys a truck if the player can afford it and it is in stock.</summary>
        public void TryBuy(TruckData truck)
        {
            if (truck == null)
            {
                return;
            }

            if (Remaining(truck) <= 0)
            {
                Debug.LogWarning($"'{truck.name}' está agotado.");
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

            _bought[truck] = GetBought(truck) + 1;

            if (truckManager != null)
            {
                truckManager.SpawnTruck(truck);
            }

            RefreshItems();
        }
    }
}
