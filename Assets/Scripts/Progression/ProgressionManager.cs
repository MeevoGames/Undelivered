using System.Collections.Generic;
using Undelivered.Shop;
using Undelivered.Upgrades;
using Undelivered.Work;
using UnityEngine;

namespace Undelivered.Progression
{
    /// <summary>
    /// Drives the multi-day progression by internal (invisible) levels. Each day applies its level's
    /// config: the shop's available trucks and their per-day stock, the buyable upgrades, and the daily
    /// quota. Finishing a day (quota met) advances to the next level; days past the last defined level
    /// repeat it. Achievements and the second-job proposal are not built yet.
    /// </summary>
    public class ProgressionManager : MonoBehaviour
    {
        public static ProgressionManager Instance { get; private set; }

        [Tooltip("Level configs for days 1..N. Days beyond N repeat the last one.")]
        [SerializeField] private List<LevelData> levels = new List<LevelData>();

        [SerializeField] private TruckShop truckShop;
        [SerializeField] private UpgradeShop upgradeShop;

        private int _levelIndex; // 0-based: day 1 = index 0

        public int CurrentDay => _levelIndex + 1;
        public LevelData CurrentLevel => GetLevel(_levelIndex);
        public int CurrentQuota => CurrentLevel != null ? CurrentLevel.quota : 0;

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

        private void Start()
        {
            ApplyLevel();
        }

        /// <summary>Advances to the next day: clears the table, resets the day stats and applies the new level.</summary>
        public void AdvanceDay()
        {
            _levelIndex++;
            ClearTable();
            if (DayManager.Instance != null)
            {
                DayManager.Instance.ResetDay();
            }
            ApplyLevel();
        }

        private void ApplyLevel()
        {
            LevelData level = CurrentLevel;
            if (level == null)
            {
                Debug.LogWarning($"{nameof(ProgressionManager)} has no levels configured.", this);
                return;
            }

            if (truckShop != null)
            {
                List<KeyValuePair<TruckData, int>> stocks = new List<KeyValuePair<TruckData, int>>();
                foreach (LevelData.TruckStock ts in level.truckStocks)
                {
                    if (ts.truck != null)
                    {
                        stocks.Add(new KeyValuePair<TruckData, int>(ts.truck, ts.stock));
                    }
                }
                truckShop.Configure(stocks);
            }

            if (upgradeShop != null)
            {
                upgradeShop.Configure(level.availableUpgrades);
            }

            if (DayManager.Instance != null)
            {
                DayManager.Instance.SetQuota(level.quota);
            }
        }

        private LevelData GetLevel(int index)
        {
            if (levels == null || levels.Count == 0)
            {
                return null;
            }
            return levels[Mathf.Clamp(index, 0, levels.Count - 1)];
        }

        private static void ClearTable()
        {
            foreach (Box box in FindObjectsByType<Box>())
            {
                Destroy(box.gameObject);
            }
            foreach (Trash trash in FindObjectsByType<Trash>())
            {
                Destroy(trash.gameObject);
            }
        }
    }
}
