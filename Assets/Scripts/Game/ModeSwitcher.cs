using Undelivered.Work;
using UnityEngine;

namespace Undelivered.Game
{
    /// <summary>
    /// Drives the day→night transition. Lives on a GameObject with an <see cref="Animator"/> that has
    /// the "ChangeMode" animation reachable through the <see cref="changeTrigger"/> trigger. When the
    /// work day finishes (<see cref="DayManager.Finished"/>) it fires that trigger once. Also exposes
    /// <see cref="ChangeMode"/> so a button (e.g. on the summary) can trigger it manually.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ModeSwitcher : MonoBehaviour
    {
        [Tooltip("Animator that holds the ChangeMode animation (defaults to the one on this object).")]
        [SerializeField] private Animator animator;

        [Tooltip("Trigger parameter that plays the ChangeMode animation.")]
        [SerializeField] private string changeTrigger = "Change";

        /// <summary>Raised right after switching into the night (dice) mode.</summary>
        public event System.Action EnteredNight;
        /// <summary>Raised right after switching back into the day (packaging) mode.</summary>
        public event System.Action EnteredDay;

        private bool _subscribed;
        private bool _changed;
        private bool _isNight;

        /// <summary>True while the night (dice) mode is the active one.</summary>
        public bool IsNight => _isNight;

        private void Reset() => animator = GetComponent<Animator>();

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        // Subscribe from both OnEnable and Start so it works regardless of initialization order.
        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void OnDisable()
        {
            if (_subscribed && DayManager.Instance != null)
            {
                DayManager.Instance.Finished -= OnDayFinished;
            }
            _subscribed = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed || DayManager.Instance == null)
            {
                return;
            }
            DayManager.Instance.Finished += OnDayFinished;
            _subscribed = true;
        }

        private void OnDayFinished() => ChangeMode();

        /// <summary>Plays the ChangeMode animation (once) to switch into the night mode.</summary>
        public void ChangeMode()
        {
            if (_changed)
            {
                return;
            }
            _changed = true;
            SwitchMode();
        }

        /// <summary>Fires the Change trigger unconditionally — e.g. the "Cerrar por hoy" button (night → day).</summary>
        public void SwitchMode()
        {
            if (animator != null && !string.IsNullOrEmpty(changeTrigger))
            {
                animator.SetTrigger(changeTrigger);
            }

            _isNight = !_isNight;
            if (_isNight)
            {
                EnteredNight?.Invoke();
            }
            else
            {
                _changed = false; // re-arm ChangeMode so the next finished day can switch again
                EnteredDay?.Invoke();
            }
        }
    }
}
