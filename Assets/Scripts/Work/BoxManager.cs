using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Work
{
    /// <summary>
    /// Central configuration for box types: label sprite, classification gold reward, the random
    /// gold range awarded when opened, and the trust it costs to open (with and without dice).
    /// </summary>
    public class BoxManager : MonoBehaviour
    {
        [System.Serializable]
        public class BoxTypeConfig
        {
            public BoxType type;
            public Sprite labelSprite;

            [Tooltip("Gold paid when this box is correctly classified.")]
            public int goldReward;

            [Header("On open")]
            [Tooltip("Minimum gold awarded when this box is opened (non-dice boxes).")]
            public int openGoldMin;
            [Tooltip("Maximum gold awarded when this box is opened (non-dice boxes).")]
            public int openGoldMax;

            [Tooltip("Trust lost when opened without a dice label.")]
            public int trustCostOnOpen;
            [Tooltip("Trust lost when opened with a dice label.")]
            public int trustCostOnOpenWithDice;
        }

        public static BoxManager Instance { get; private set; }

        [SerializeField] private List<BoxTypeConfig> boxTypes = new List<BoxTypeConfig>();

        [Header("Weight -> open gold bias")]
        [Tooltip("Box weight at/below which open gold is unbiased (uniform).")]
        [SerializeField] private float lightWeight = 0f;
        [Tooltip("Box weight at/above which open gold rolls near the maximum.")]
        [SerializeField] private float heavyWeight = 30f;

        private readonly Dictionary<BoxType, BoxTypeConfig> _lookup = new Dictionary<BoxType, BoxTypeConfig>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildLookup();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void BuildLookup()
        {
            _lookup.Clear();
            foreach (BoxTypeConfig config in boxTypes)
            {
                if (config == null)
                {
                    continue;
                }
                _lookup[config.type] = config; // on duplicate types, the last entry wins
            }
        }

        /// <summary>Label sprite configured for the given box type, or null if none is set.</summary>
        public Sprite GetLabelSprite(BoxType type)
        {
            return _lookup.TryGetValue(type, out BoxTypeConfig config) ? config.labelSprite : null;
        }

        /// <summary>Gold paid by correctly classifying the given box type, or 0 if none is configured.</summary>
        public int GetGoldReward(BoxType type)
        {
            return _lookup.TryGetValue(type, out BoxTypeConfig config) ? config.goldReward : 0;
        }

        /// <summary>Trust lost when opening a box of the given type, higher when it carries dice.</summary>
        public int GetTrustCost(BoxType type, bool isDice)
        {
            if (_lookup.TryGetValue(type, out BoxTypeConfig config))
            {
                return isDice ? config.trustCostOnOpenWithDice : config.trustCostOnOpen;
            }
            return 0;
        }

        /// <summary>
        /// Random gold awarded when opening a non-dice box. Heavier boxes bias the roll toward the
        /// maximum, so they are more likely to pay a lot.
        /// </summary>
        public int RollOpenGold(BoxType type, float weight)
        {
            if (_lookup.TryGetValue(type, out BoxTypeConfig config))
            {
                int min = Mathf.Min(config.openGoldMin, config.openGoldMax);
                int max = Mathf.Max(config.openGoldMin, config.openGoldMax);
                float bias = Mathf.Clamp01(Mathf.InverseLerp(lightWeight, heavyWeight, weight));
                float roll = Mathf.Lerp(Random.value, 1f, bias); // heavier -> closer to the max
                return Mathf.RoundToInt(Mathf.Lerp(min, max, roll));
            }
            return 0;
        }
    }
}
