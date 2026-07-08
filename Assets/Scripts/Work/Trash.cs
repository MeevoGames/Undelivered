using System.Collections;
using Undelivered.Player;
using Undelivered.UI;
using UnityEngine;

namespace Undelivered.Work
{
    /// <summary>
    /// Marker for an empty-box "trash" item left after opening a package. It has no classification
    /// value, so the paper bin discards it for free; discarding it on any other slot costs gold, and
    /// leaving it on the table at day end costs trust. With the auto-cleanup upgrade it drifts to the
    /// paper bin on its own (a bit slowly) and removes itself.
    /// </summary>
    public class Trash : MonoBehaviour
    {
        // "Limpieza automática" upgrade.
        public static bool AutoCleanupEnabled;

        // Seconds the auto-cleanup takes to reach the bin; set per level by the upgrade (7/4/2).
        public static float CleanupDuration = 3f;

        [Tooltip("Gold lost when this trash is dropped on a slot that isn't the paper bin.")]
        [SerializeField] private int wrongSlotGoldPenalty = 2;

        private bool _cleaningUp;

        private void Start()
        {
            TryAutoCleanup();
        }

        /// <summary>Called when dropped on a non-bin slot: charges the gold penalty and removes it.</summary>
        public void DiscardInWrongSlot()
        {
            if (wrongSlotGoldPenalty != 0 && StatsManager.Instance != null)
            {
                StatsManager.Instance.AddGold(-wrongSlotGoldPenalty);
            }
            Debug.LogWarning($"-{wrongSlotGoldPenalty} oro — tiraste basura en un slot que no es la papelera.");
            Destroy(gameObject);
        }

        /// <summary>If auto-cleanup is on, starts carrying this trash to the paper bin on its own.</summary>
        public void TryAutoCleanup()
        {
            if (!AutoCleanupEnabled || _cleaningUp)
            {
                return;
            }
            _cleaningUp = true;
            StartCoroutine(AutoCleanupRoutine());
        }

        private IEnumerator AutoCleanupRoutine()
        {
            // Stop the player from dragging it while it drifts to the bin.
            UIDraggable drag = GetComponent<UIDraggable>();
            if (drag != null)
            {
                drag.enabled = false;
            }

            RectTransform rect = (RectTransform)transform;
            Vector3 start = rect.position;

            PaperBinSlot bin = FindAnyObjectByType<PaperBinSlot>();
            RectTransform binRect = bin != null ? bin.transform as RectTransform : null;
            Vector3 target = binRect != null ? binRect.TransformPoint(binRect.rect.center) : start;

            float duration = Mathf.Max(0.01f, CleanupDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                rect.position = Vector3.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
