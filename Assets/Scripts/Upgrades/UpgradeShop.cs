using System.Collections.Generic;
using Undelivered.Player;
using UnityEngine;

namespace Undelivered.Upgrades
{
    /// <summary>
    /// Upgrades shop (second tab of the left panel). Lists the day-mode upgrades, keeps their buy
    /// buttons in sync with the player's gold, and on purchase spends the gold, applies the upgrade
    /// and marks it as owned so it can't be bought again.
    /// </summary>
    public class UpgradeShop : MonoBehaviour
    {
        [Tooltip("Upgrades available for sale.")]
        [SerializeField] private List<UpgradeData> upgradesForSale = new List<UpgradeData>();

        [Tooltip("Prefab of a single shop row (must have an UpgradeShopItem).")]
        [SerializeField] private UpgradeShopItem itemPrefab;

        [Tooltip("Parent under which the rows are instantiated (the upgrades tab content).")]
        [SerializeField] private Transform itemsParent;

        private readonly List<UpgradeShopItem> _items = new List<UpgradeShopItem>();
        private readonly HashSet<UpgradeData> _purchased = new HashSet<UpgradeData>();
        private bool _subscribed;

        private void Start()
        {
            BuildItems();
            TrySubscribe();
            RefreshItems();
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

        private void BuildItems()
        {
            if (itemPrefab == null || itemsParent == null)
            {
                Debug.LogWarning($"{nameof(UpgradeShop)} needs an item prefab and a parent assigned.", this);
                return;
            }

            foreach (UpgradeData upgrade in upgradesForSale)
            {
                if (upgrade == null)
                {
                    continue;
                }
                UpgradeShopItem item = Instantiate(itemPrefab, itemsParent, false);
                item.Setup(upgrade, this);
                _items.Add(item);
            }
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
                _items[i].Refresh(_purchased.Contains(_items[i].Upgrade), gold);
            }
        }

        /// <summary>Buys an upgrade if affordable and not owned: spends gold, applies it, marks owned.</summary>
        public void TryBuy(UpgradeData upgrade)
        {
            if (upgrade == null || _purchased.Contains(upgrade))
            {
                return;
            }

            int gold = StatsManager.Instance != null ? StatsManager.Instance.Gold : 0;
            if (gold < upgrade.Price)
            {
                Debug.LogWarning($"No alcanza para comprar la mejora '{upgrade.UpgradeName}' (precio {upgrade.Price}, oro {gold}).");
                return;
            }

            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.AddGold(-upgrade.Price);
            }
            upgrade.Apply();
            _purchased.Add(upgrade);
            RefreshItems();
        }
    }
}
