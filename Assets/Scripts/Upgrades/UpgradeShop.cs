using System.Collections.Generic;
using Undelivered.Player;
using UnityEngine;

namespace Undelivered.Upgrades
{
    /// <summary>
    /// Upgrades shop (second tab of the left panel). The ProgressionManager configures which upgrades
    /// are available each level; purchased levels persist across days. Tracks each upgrade's level,
    /// keeps buy buttons in sync with gold, and on purchase spends the next level's price and applies it.
    /// </summary>
    public class UpgradeShop : MonoBehaviour
    {
        [Tooltip("Upgrades for sale if no ProgressionManager configures the shop.")]
        [SerializeField] private List<UpgradeData> upgradesForSale = new List<UpgradeData>();

        [Tooltip("Prefab of a single shop row (must have an UpgradeShopItem).")]
        [SerializeField] private UpgradeShopItem itemPrefab;

        [Tooltip("Parent under which the rows are instantiated (the upgrades tab content).")]
        [SerializeField] private Transform itemsParent;

        private readonly List<UpgradeShopItem> _items = new List<UpgradeShopItem>();
        private readonly Dictionary<UpgradeData, int> _levels = new Dictionary<UpgradeData, int>();
        private bool _subscribed;
        private bool _configured;

        /// <summary>Raised after an upgrade level is successfully bought (the tutorial listens for this).</summary>
        public event System.Action<UpgradeData> Bought;

        /// <summary>The shop row for an upgrade, or null (used by the tutorial to blink its buy button).</summary>
        public UpgradeShopItem FindItem(UpgradeData upgrade)
        {
            foreach (UpgradeShopItem item in _items)
                if (item != null && item.Upgrade == upgrade) return item;
            return null;
        }

        private void Start()
        {
            TrySubscribe();
            if (!_configured)
            {
                Build(upgradesForSale);
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

        /// <summary>Rebuilds the shop to show only the given upgrades. Purchased levels are kept across days.</summary>
        public void Configure(List<UpgradeData> upgrades)
        {
            _configured = true;
            Build(upgrades);
        }

        private void Build(List<UpgradeData> upgrades)
        {
            if (itemPrefab == null || itemsParent == null)
            {
                Debug.LogWarning($"{nameof(UpgradeShop)} needs an item prefab and a parent assigned.", this);
                return;
            }

            ClearItems();
            foreach (UpgradeData upgrade in upgrades)
            {
                if (upgrade == null)
                {
                    continue;
                }
                UpgradeShopItem item = Instantiate(itemPrefab, itemsParent, false);
                item.Setup(upgrade, this);
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
                UpgradeShopItem item = _items[i];
                if (item == null)
                {
                    continue;
                }

                // Hide upgrades whose prerequisite isn't owned yet (e.g. Control de Calidad needs the
                // reempaquetadora); they appear the moment the prerequisite is purchased.
                bool available = IsAvailable(item.Upgrade);
                if (item.gameObject.activeSelf != available)
                {
                    item.gameObject.SetActive(available);
                }
                if (available)
                {
                    item.Refresh(GetLevel(item.Upgrade), gold);
                }
            }
        }

        private bool IsAvailable(UpgradeData upgrade)
        {
            if (upgrade == null)
            {
                return false;
            }
            UpgradeData prerequisite = upgrade.Prerequisite;
            return prerequisite == null || GetLevel(prerequisite) >= 1;
        }

        private int GetLevel(UpgradeData upgrade) => upgrade != null && _levels.TryGetValue(upgrade, out int level) ? level : 0;

        /// <summary>Debug: applies each upgrade up to its max level for free (records the level). Returns how many were touched.</summary>
        public int DebugMaxOut(IEnumerable<UpgradeData> upgrades)
        {
            if (upgrades == null) return 0;
            int count = 0;
            foreach (UpgradeData upgrade in upgrades)
            {
                if (upgrade == null) continue;
                int max = upgrade.MaxLevel;
                for (int level = GetLevel(upgrade) + 1; level <= max; level++) upgrade.Apply(level); // 1..max, so additive effects stack correctly
                _levels[upgrade] = max;
                count++;
            }
            RefreshItems();
            return count;
        }

        /// <summary>Buys the next level of an upgrade if affordable and not maxed.</summary>
        public void TryBuy(UpgradeData upgrade)
        {
            if (upgrade == null)
            {
                return;
            }

            int level = GetLevel(upgrade);
            if (level >= upgrade.MaxLevel)
            {
                return; // already maxed
            }

            int price = upgrade.GetPrice(level);
            int gold = StatsManager.Instance != null ? StatsManager.Instance.Gold : 0;
            if (gold < price)
            {
                Debug.LogWarning($"No alcanza para mejorar '{upgrade.UpgradeName}' a nivel {level + 1} (precio {price}, oro {gold}).");
                return;
            }

            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.AddGold(-price);
            }

            int newLevel = level + 1;
            _levels[upgrade] = newLevel;
            upgrade.Apply(newLevel);
            RefreshItems();
            Bought?.Invoke(upgrade);
        }
    }
}
