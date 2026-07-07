using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Work
{
    /// <summary>
    /// Defines a truck: how many boxes it brings and the percentage chance of each
    /// <see cref="BoxType"/>. Percentages are treated as weights, so they don't have to add up to
    /// exactly 100 — they are normalized when a box type is rolled.
    /// </summary>
    [CreateAssetMenu(fileName = "Truck", menuName = "Undelivered/Truck")]
    public class TruckData : ScriptableObject
    {
        [System.Serializable]
        public struct BoxChance
        {
            public BoxType type;
            [Range(0f, 100f)] public float percent;
        }

        /// <summary>Result of rolling a box: its principal type plus whether it also carries dice.</summary>
        public struct BoxRoll
        {
            public BoxType type;
            public bool isDice;
        }

        [Tooltip("Gold cost to buy this truck in the shop.")]
        [SerializeField] private int price = 100;

        [Tooltip("Total number of boxes this truck brings.")]
        [SerializeField] private int totalBoxes = 10;

        [Tooltip("Average weight of this truck's boxes; each box weighs this plus a random -10..+5.")]
        [SerializeField] private int averageWeight = 20;

        [Tooltip("Chance (percentage / weight) of each box type. Rolled independently for every box.")]
        [SerializeField] private List<BoxChance> boxChances = new List<BoxChance>();

        public int Price => Mathf.Max(0, price);
        public int TotalBoxes => Mathf.Max(0, totalBoxes);
        public int AverageWeight => averageWeight;
        public IReadOnlyList<BoxChance> BoxChances => boxChances;

        /// <summary>Rolls a random weight for a box of this truck: average plus a random -10..+5.</summary>
        public int RollBoxWeight()
        {
            return Mathf.Max(0, averageWeight + Random.Range(-10, 6)); // -10..+5 inclusive
        }

        /// <summary>
        /// Rolls a box: a principal type plus a dice flag. If the weighted roll lands on
        /// <see cref="BoxType.Dice"/>, the box is marked as dice and its principal type is rolled
        /// again from the non-dice chances (dice boxes always have another type as their main one).
        /// </summary>
        public BoxRoll RollBox()
        {
            BoxType rolled = RollWeighted(excludeDice: false);
            if (rolled == BoxType.Dice)
            {
                return new BoxRoll { type = RollWeighted(excludeDice: true), isDice = true };
            }
            return new BoxRoll { type = rolled, isDice = false };
        }

        // Weighted pick over the configured chances, optionally skipping the Dice entry.
        private BoxType RollWeighted(bool excludeDice)
        {
            float total = 0f;
            for (int i = 0; i < boxChances.Count; i++)
            {
                if (excludeDice && boxChances[i].type == BoxType.Dice)
                {
                    continue;
                }
                total += Mathf.Max(0f, boxChances[i].percent);
            }

            if (total <= 0f)
            {
                return BoxType.Local; // nothing usable configured; safe non-dice default
            }

            float roll = Random.value * total;
            float acc = 0f;
            BoxType last = BoxType.Local;
            for (int i = 0; i < boxChances.Count; i++)
            {
                if (excludeDice && boxChances[i].type == BoxType.Dice)
                {
                    continue;
                }
                acc += Mathf.Max(0f, boxChances[i].percent);
                last = boxChances[i].type;
                if (roll < acc)
                {
                    return boxChances[i].type;
                }
            }

            return last; // numerical safety
        }
    }
}
