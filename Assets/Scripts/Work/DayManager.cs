using System.Collections.Generic;
using TMPro;
using Undelivered.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Work
{
    /// <summary>
    /// Tracks what happened during the current work day (gold spent / earned / lost, deliveries, and
    /// trust) and shows the totals in the end-of-day summary window. Trust gained from correct
    /// deliveries is only applied when the day ends (+1 per 10 correct boxes of the same type).
    /// </summary>
    public class DayManager : MonoBehaviour
    {
        public static DayManager Instance { get; private set; }

        private int goldSpent;
        private int goldGenerated;
        private int goldLost;
        private int correctDeliveries;
        private int incorrectDeliveries;
        private int trustLostToday;
        private int itemsObtained;
        private int quota;
        private readonly Dictionary<BoxType, int> _correctByType = new Dictionary<BoxType, int>();
        private bool finished;

        [Header("Penalties")]
        [Tooltip("Trust lost per trash item left on the table when the day ends.")]
        [SerializeField] private int trashLeftTrustPenalty = 2;

        [Header("Summary window")]
        [Tooltip("Window shown when the day ends. Should start disabled.")]
        [SerializeField] private GameObject summaryWindow;

        [Header("Summary texts")]
        [SerializeField] private TextMeshProUGUI goldSpentText;
        [SerializeField] private TextMeshProUGUI goldGeneratedText;
        [SerializeField] private TextMeshProUGUI goldLostText;
        [SerializeField] private TextMeshProUGUI correctDeliveriesText;
        [SerializeField] private TextMeshProUGUI incorrectDeliveriesText;
        [Tooltip("How much trust changed over the day (can be negative or positive).")]
        [SerializeField] private TextMeshProUGUI trustChangeText;
        [Tooltip("Number of gift-card items obtained this day.")]
        [SerializeField] private TextMeshProUGUI itemsObtainedText;

        [Header("Quota")]
        [Tooltip("Live quota progress text, e.g. \"12/30\".")]
        [SerializeField] private TextMeshProUGUI quotaText;
        [Tooltip("Finish-day button, enabled only once the quota is met.")]
        [SerializeField] private Button finishDayButton;

        public int GoldSpent => goldSpent;
        public int GoldGenerated => goldGenerated;
        public int GoldLost => goldLost;
        public int CorrectDeliveries => correctDeliveries;
        public int IncorrectDeliveries => incorrectDeliveries;
        public int ItemsObtained => itemsObtained;
        public bool QuotaMet => correctDeliveries >= quota;

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

        /// <summary>Records gold spent (e.g. buying a truck).</summary>
        public void RegisterGoldSpent(int amount)
        {
            if (finished)
            {
                return;
            }
            goldSpent += Mathf.Max(0, amount);
        }

        /// <summary>Records a correctly classified box (per type, for the trust bonus) and its gold.</summary>
        public void RegisterCorrectDelivery(BoxType type, int goldGained)
        {
            if (finished)
            {
                return;
            }
            correctDeliveries++;
            goldGenerated += Mathf.Max(0, goldGained);
            _correctByType.TryGetValue(type, out int current);
            _correctByType[type] = current + 1;
            UpdateQuotaUI();
        }

        /// <summary>Records a misclassified box and the gold it cost.</summary>
        public void RegisterIncorrectDelivery(int goldLostAmount)
        {
            if (finished)
            {
                return;
            }
            incorrectDeliveries++;
            goldLost += Mathf.Max(0, goldLostAmount);
        }

        /// <summary>Records trust lost during the day (e.g. from opening a package).</summary>
        public void RegisterTrustLost(int amount)
        {
            if (finished)
            {
                return;
            }
            trustLostToday += Mathf.Max(0, amount);
        }

        /// <summary>Records a gift-card item obtained this day (from opening a dice package).</summary>
        public void RegisterItemObtained()
        {
            if (finished)
            {
                return;
            }
            itemsObtained++;
        }

        /// <summary>Sets the day's quota (correct deliveries needed to finish) and refreshes the UI.</summary>
        public void SetQuota(int newQuota)
        {
            quota = Mathf.Max(0, newQuota);
            UpdateQuotaUI();
        }

        /// <summary>Resets all day counters for a new day (the new quota is set separately).</summary>
        public void ResetDay()
        {
            goldSpent = 0;
            goldGenerated = 0;
            goldLost = 0;
            correctDeliveries = 0;
            incorrectDeliveries = 0;
            trustLostToday = 0;
            itemsObtained = 0;
            _correctByType.Clear();
            finished = false;

            if (summaryWindow != null)
            {
                summaryWindow.SetActive(false);
            }
            UpdateQuotaUI();
        }

        private void UpdateQuotaUI()
        {
            SetText(quotaText, $"{correctDeliveries}/{quota}");
            if (finishDayButton != null)
            {
                finishDayButton.interactable = QuotaMet;
            }
        }

        /// <summary>
        /// Closes the day's counts (only if the quota is met), applies the trust gained (+1 per 10
        /// correct of the same type), fills the summary texts and opens the summary window.
        /// </summary>
        public void FinishDay()
        {
            if (finished || !QuotaMet)
            {
                return;
            }
            finished = true;

            // Trash left on the table costs trust (per leftover item).
            int trashLeft = FindObjectsByType<Trash>().Length;
            int trashPenalty = trashLeft * trashLeftTrustPenalty;
            trustLostToday += trashPenalty;

            int trustGained = 0;
            foreach (KeyValuePair<BoxType, int> pair in _correctByType)
            {
                trustGained += pair.Value / 10; // +1 trust per 10 correct boxes of the same type
            }

            // Opening losses were already applied during the day; apply the end-of-day delta now
            // (trust gained minus the leftover-trash penalty).
            int endOfDayTrustDelta = trustGained - trashPenalty;
            if (StatsManager.Instance != null && endOfDayTrustDelta != 0)
            {
                StatsManager.Instance.AddTrust(endOfDayTrustDelta);
            }

            int trustChange = trustGained - trustLostToday;

            SetText(goldSpentText, goldSpent.ToString());
            SetText(goldGeneratedText, goldGenerated.ToString());
            SetText(goldLostText, goldLost.ToString());
            SetText(correctDeliveriesText, correctDeliveries.ToString());
            SetText(incorrectDeliveriesText, incorrectDeliveries.ToString());
            SetText(trustChangeText, (trustChange >= 0 ? "+" : "") + trustChange);
            SetText(itemsObtainedText, itemsObtained.ToString());

            if (summaryWindow != null)
            {
                summaryWindow.SetActive(true);
            }
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }
    }
}
