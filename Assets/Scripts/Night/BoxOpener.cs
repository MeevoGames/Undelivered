using System;
using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// Draws a box's contents and opens it. The box rarity biases the draw: for dice, higher-rarity
    /// boxes favour higher-level dice (and vice versa); for effects, an effect can only appear if its
    /// rarity is ≤ the box rarity, and higher-rarity boxes favour the rarer eligible effects. Scene
    /// singleton holding the game's dice/effect catalogue.
    /// </summary>
    public class BoxOpener : MonoBehaviour
    {
        public static BoxOpener Instance { get; private set; }

        [Tooltip("Every die that can come out of a box.")]
        [SerializeField] private List<DiceData> allDice = new List<DiceData>();
        [Tooltip("Every effect that can come out of a box.")]
        [SerializeField] private List<EffectData> allEffects = new List<EffectData>();

        [Tooltip("Weight falloff per rarity/level step away from the box's tier (0-1). Lower = stricter bias.")]
        [SerializeField] private float falloff = 0.35f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Opens a box: draws its contents and shows the open-box UI (or grants directly if none).</summary>
        public void Open(BoxData box)
        {
            if (box == null) return;

            List<IItem> items = Draw(box);
            if (BoxOpenPanel.Instance != null)
            {
                BoxOpenPanel.Instance.Show(box, items);
            }
            else if (Inventory.Instance != null && items.Count > 0)
            {
                Grant(items[0]); // fallback if no panel is wired
            }
        }

        /// <summary>Draws the box's items (distinct), weighted by its rarity.</summary>
        public List<IItem> Draw(BoxData box)
        {
            var result = new List<IItem>();
            if (box == null) return result;

            int count = box.DrawCount;
            int tier = (int)box.BoxRarity;

            if (box.BoxContent == BoxData.Content.Dice)
            {
                var pool = new List<DiceData>();
                foreach (DiceData d in allDice) if (d != null) pool.Add(d);
                for (int i = 0; i < count && pool.Count > 0; i++)
                {
                    DiceData pick = WeightedPick(pool, d => Weight(d.Level, tier + 1));
                    if (pick == null) break;
                    result.Add(pick);
                    pool.Remove(pick);
                }
            }
            else
            {
                var pool = new List<EffectData>();
                foreach (EffectData e in allEffects) if (e != null && (int)e.EffectRarity <= tier) pool.Add(e);
                for (int i = 0; i < count && pool.Count > 0; i++)
                {
                    EffectData pick = WeightedPick(pool, e => Weight((int)e.EffectRarity, tier));
                    if (pick == null) break;
                    result.Add(pick);
                    pool.Remove(pick);
                }
            }
            return result;
        }

        // Peaks when the item's level/rarity matches the box target; falls off with distance.
        private float Weight(int value, int target) => Mathf.Pow(falloff, Mathf.Abs(value - target));

        private static void Grant(IItem item)
        {
            if (item is DiceData die) Inventory.Instance?.AddDie(die);
            else if (item is EffectData effect) Inventory.Instance?.AddEffect(effect);
        }

        private static T WeightedPick<T>(List<T> items, Func<T, float> weight)
        {
            float total = 0f;
            foreach (T item in items) total += Mathf.Max(0.0001f, weight(item));

            float r = UnityEngine.Random.value * total;
            foreach (T item in items)
            {
                r -= Mathf.Max(0.0001f, weight(item));
                if (r <= 0f) return item;
            }
            return items.Count > 0 ? items[items.Count - 1] : default;
        }
    }
}
