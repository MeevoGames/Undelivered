using System;
using UnityEngine;

namespace Undelivered.Player
{
    /// <summary>
    /// Holds the player's stats (gold and trust for now; it will grow later). Fires an event with
    /// the new total whenever a stat changes so the HUD can react.
    /// </summary>
    public class StatsManager : MonoBehaviour
    {
        public static StatsManager Instance { get; private set; }

        /// <summary>Chance (0..1) to avoid a trust loss. Set by the "Asegura la confianza" upgrade.</summary>
        public static float TrustLossProtection;

        [SerializeField] private int gold;

        [Tooltip("Player trust. Starts at 10 on day one.")]
        [SerializeField] private int trust = 10;

        /// <summary>Current gold. Can be negative (misclassifying / trashing boxes costs gold).</summary>
        public int Gold => gold;

        /// <summary>Current trust.</summary>
        public int Trust => trust;

        /// <summary>Raised whenever gold changes, with the new total.</summary>
        public event Action<int> GoldChanged;

        /// <summary>Raised whenever trust changes, with the new total.</summary>
        public event Action<int> TrustChanged;

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

        /// <summary>Adds (or subtracts, if negative) gold and notifies listeners.</summary>
        public void AddGold(int delta)
        {
            gold += delta;
            GoldChanged?.Invoke(gold);
        }

        /// <summary>Sets gold to an exact value and notifies listeners.</summary>
        public void SetGold(int value)
        {
            gold = value;
            GoldChanged?.Invoke(gold);
        }

        /// <summary>Adds (or subtracts, if negative) trust and notifies listeners.</summary>
        public void AddTrust(int delta)
        {
            trust += delta;
            TrustChanged?.Invoke(trust);
        }

        /// <summary>Sets trust to an exact value and notifies listeners.</summary>
        public void SetTrust(int value)
        {
            trust = value;
            TrustChanged?.Invoke(trust);
        }
    }
}
