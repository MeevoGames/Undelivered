using System;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// The dice-game currency: gems. Everything in Dicegeon is paid with gems (bought later with the
    /// day's gold). Scene singleton. Seed <see cref="gems"/> in the inspector for testing.
    /// </summary>
    public class NightWallet : MonoBehaviour
    {
        public static NightWallet Instance { get; private set; }

        [SerializeField] private int gems;

        public int Gems => gems;

        /// <summary>Raised whenever gems change, with the new total.</summary>
        public event Action<int> GemsChanged;

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

        public bool CanAfford(int cost) => gems >= Mathf.Max(0, cost);

        /// <summary>Adds (or subtracts, if negative) gems and notifies listeners.</summary>
        public void AddGems(int delta)
        {
            gems = Mathf.Max(0, gems + delta);
            GemsChanged?.Invoke(gems);
        }

        /// <summary>Spends the cost if affordable and returns true; otherwise leaves gems untouched.</summary>
        public bool TrySpend(int cost)
        {
            cost = Mathf.Max(0, cost);
            if (gems < cost) return false;

            gems -= cost;
            GemsChanged?.Invoke(gems);
            return true;
        }
    }
}
