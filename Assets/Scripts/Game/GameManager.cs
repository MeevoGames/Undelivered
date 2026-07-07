using System.Collections;
using Undelivered.Player;
using Undelivered.Work;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Undelivered.Game
{
    /// <summary>
    /// Watches the player's stats for automatic closures: trust reaching 0 (or below) ends the whole
    /// run (game over window), gold reaching 0 (or below) ends the day (day summary). Also exposes
    /// <see cref="Restart"/> to reload everything from scratch.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Tooltip("Game over window shown when trust runs out. Should start disabled.")]
        [SerializeField] private GameObject finishGameWindow;

        private bool _subscribed;
        private bool _gameOver;
        private bool _dayEnded;
        private bool _endCheckPending;

        // Subscribe from both OnEnable and Start so it works regardless of initialization order.
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
        }

        private void OnTrustChanged(int trust)
        {
            if (_gameOver)
            {
                return;
            }
            if (trust <= 0)
            {
                GameOver();
                return;
            }
            // A box may have been consumed (e.g. opening it) while already broke; re-check.
            if (!_dayEnded && StatsManager.Instance != null && StatsManager.Instance.Gold <= 0)
            {
                ScheduleDayEndCheck();
            }
        }

        private void OnGoldChanged(int gold)
        {
            if (_gameOver || _dayEnded)
            {
                return;
            }
            if (gold <= 0)
            {
                ScheduleDayEndCheck();
            }
        }

        private void GameOver()
        {
            _gameOver = true;
            if (finishGameWindow != null)
            {
                finishGameWindow.SetActive(true);
                // Render above anything opened in the same frame (e.g. the day summary, when the
                // end-of-day trash penalty is what dropped trust to 0).
                finishGameWindow.transform.SetAsLastSibling();
            }
        }

        private void EndDay()
        {
            _dayEnded = true;
            if (DayManager.Instance != null)
            {
                DayManager.Instance.FinishDay();
            }
        }

        // The day only ends when gold is gone AND there are no boxes left to distribute (they would
        // still earn gold). The check runs one frame later so any box consumed this frame — whose
        // Destroy is deferred to end of frame — is already gone and not miscounted.
        private void ScheduleDayEndCheck()
        {
            if (_endCheckPending)
            {
                return;
            }
            _endCheckPending = true;
            StartCoroutine(DayEndCheckRoutine());
        }

        private IEnumerator DayEndCheckRoutine()
        {
            yield return null;
            _endCheckPending = false;

            if (_gameOver || _dayEnded)
            {
                yield break;
            }
            if (StatsManager.Instance != null && StatsManager.Instance.Gold <= 0 && !AnyBoxesLeft())
            {
                EndDay();
            }
        }

        private static bool AnyBoxesLeft()
        {
            return FindObjectsByType<Box>().Length > 0;
        }

        /// <summary>Restarts the whole run from scratch by reloading the active scene.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }
    }
}
