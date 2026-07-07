using TMPro;
using Undelivered.UI;
using UnityEngine;

namespace Undelivered.Player
{
    /// <summary>
    /// Shows the player's stats in the HUD and, whenever a stat changes, spawns a floating value
    /// text (via <see cref="CreateTextPerState"/>) with the delta in the matching space. The initial
    /// value is shown without a floating text.
    /// </summary>
    public class StatsToHUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI trustText;

        private bool _subscribed;
        private int _lastGold;
        private int _lastTrust;

        // Subscribe from both OnEnable and Start so it works regardless of whether the
        // StatsManager's Awake ran before or after this component was enabled.
        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void OnDisable()
        {
            if (_subscribed && StatsManager.Instance != null)
            {
                StatsManager.Instance.GoldChanged -= OnGoldChanged;
                StatsManager.Instance.TrustChanged -= OnTrustChanged;
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
            StatsManager.Instance.TrustChanged += OnTrustChanged;
            _subscribed = true;

            // Show current values immediately, without spawning floating texts.
            _lastGold = StatsManager.Instance.Gold;
            _lastTrust = StatsManager.Instance.Trust;
            SetGoldText(_lastGold);
            SetTrustText(_lastTrust);
        }

        private void OnGoldChanged(int gold)
        {
            int delta = gold - _lastGold;
            _lastGold = gold;
            SetGoldText(gold);
            if (delta != 0)
            {
                CreateTextPerState.Create(StatType.Gold, delta);
            }
        }

        private void OnTrustChanged(int trust)
        {
            int delta = trust - _lastTrust;
            _lastTrust = trust;
            SetTrustText(trust);
            if (delta != 0)
            {
                CreateTextPerState.Create(StatType.Trust, delta);
            }
        }

        private void SetGoldText(int gold)
        {
            if (goldText != null)
            {
                goldText.text = gold.ToString();
            }
        }

        private void SetTrustText(int trust)
        {
            if (trustText != null)
            {
                trustText.text = trust.ToString();
            }
        }
    }
}
