using System.Collections.Generic;
using Undelivered.Upgrades;
using Undelivered.Work;
using UnityEngine;

namespace Undelivered.Progression
{
    /// <summary>
    /// Config for one progression level (an internal, invisible day tier): the daily quota of correct
    /// deliveries, which trucks are available and their per-day stock, and which upgrades can be bought.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Undelivered/Level")]
    public class LevelData : ScriptableObject
    {
        [System.Serializable]
        public struct TruckStock
        {
            public TruckData truck;
            public int stock;
        }

        [Tooltip("Correct deliveries needed to be able to finish the day.")]
        public int quota = 30;

        [Tooltip("Trucks available this level and their stock (refilled each day).")]
        public List<TruckStock> truckStocks = new List<TruckStock>();

        [Tooltip("Upgrades that can be bought at this level.")]
        public List<UpgradeData> availableUpgrades = new List<UpgradeData>();
    }
}
