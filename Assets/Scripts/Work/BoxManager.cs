using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Work
{
    /// <summary>
    /// Central configuration for box types: label sprite and classification gold reward. Opening a
    /// box is derived from that reward by the box's weight, and costs a fixed amount of trust (more
    /// with dice). The trust loss only applies when the player's trust protection doesn't save it.
    /// </summary>
    public class BoxManager : MonoBehaviour
    {
        [System.Serializable]
        public class BoxTypeConfig
        {
            public BoxType type;
            public Sprite labelSprite;

            [Tooltip("Gold paid when this box is correctly classified (also the base for opening).")]
            public int goldReward;
        }

        public static BoxManager Instance { get; private set; }

        [SerializeField] private List<BoxTypeConfig> boxTypes = new List<BoxTypeConfig>();

        [Header("Trust cost on open")]
        [Tooltip("Trust lost when a box is opened (before trust protection).")]
        [SerializeField] private int trustCostOnOpen = 1;
        [Tooltip("Trust lost when a box with dice is opened (before trust protection).")]
        [SerializeField] private int trustCostOnOpenWithDice = 2;

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

        /// <summary>Gold paid by correctly classifying the given box type (also the base for opening).</summary>
        public int GetGoldReward(BoxType type)
        {
            return _lookup.TryGetValue(type, out BoxTypeConfig config) ? config.goldReward : 0;
        }

        /// <summary>Trust lost when opening a box (fixed; more if it carries dice).</summary>
        public int GetTrustCost(bool isDice)
        {
            return isDice ? trustCostOnOpenWithDice : trustCostOnOpen;
        }

        /// <summary>
        /// Gold from opening a box, based only on its weight relative to the type's base reward:
        /// under 10kg = half the base; 10-20kg = base -2..+4; over 20kg = base x2..x3.
        /// </summary>
        public int RollOpenGold(BoxType type, float weight)
        {
            int baseGold = GetGoldReward(type);

            if (weight < 10f)
            {
                return Mathf.RoundToInt(baseGold * 0.5f);
            }
            if (weight <= 20f)
            {
                return Random.Range(Mathf.Max(0, baseGold - 2), baseGold + 5); // inclusive [base-2, base+4]
            }
            return Random.Range(baseGold * 2, baseGold * 3 + 1); // inclusive [base*2, base*3]
        }
    }
}
