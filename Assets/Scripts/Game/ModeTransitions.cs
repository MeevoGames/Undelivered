using Undelivered.Items;
using Undelivered.Night;
using Undelivered.Progression;
using Undelivered.Work;
using UnityEngine;

namespace Undelivered.Game
{
    /// <summary>
    /// Coordinates the resets when switching modes, listening to <see cref="ModeSwitcher"/>.
    ///   • Day → night: closes and advances the day (hides the summary, clears obtained items, applies
    ///     the next level's trucks / upgrades / quota) and resets the night mode for a fresh session.
    ///   • Night → day: delivers the day's initial boxes.
    /// </summary>
    public class ModeTransitions : MonoBehaviour
    {
        [SerializeField] private ModeSwitcher modeSwitcher;
        [SerializeField] private TruckManager truckManager;

        private void Awake()
        {
            if (modeSwitcher == null) modeSwitcher = GetComponent<ModeSwitcher>();
        }

        private void OnEnable()
        {
            if (modeSwitcher != null)
            {
                modeSwitcher.EnteredNight += OnEnteredNight;
                modeSwitcher.EnteredDay += OnEnteredDay;
            }
        }

        private void OnDisable()
        {
            if (modeSwitcher != null)
            {
                modeSwitcher.EnteredNight -= OnEnteredNight;
                modeSwitcher.EnteredDay -= OnEnteredDay;
            }
        }

        private void OnEnteredNight()
        {
            // Close the finished day and prepare the next one (summary hidden, items cleared, level applied).
            if (ProgressionManager.Instance != null) ProgressionManager.Instance.AdvanceDay();
            // Start the night fresh.
            if (CombatController.Instance != null) CombatController.Instance.ResetForNewNight();
        }

        private void OnEnteredDay()
        {
            // Empty the "items obtained" section and deliver the day's initial boxes.
            if (ItemsManager.Instance != null) ItemsManager.Instance.Clear();
            if (truckManager != null) truckManager.SpawnInitialBoxes();
        }
    }
}
