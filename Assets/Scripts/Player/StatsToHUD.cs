using System.Collections;
using TMPro;
using Undelivered.UI;
using UnityEngine;

namespace Undelivered.Player
{
    /// <summary>
    /// Shows the player's stats in the HUD with juice: gold counts up progressively to its new value
    /// and its number flashes twice (gain = correct-order color, loss = incorrect-order color, reused
    /// from <see cref="CreateTextPerState"/>). Losing gold or trust triggers a camera shake. It also
    /// spawns the floating value texts.
    /// </summary>
    public class StatsToHUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI trustText;

        [Header("Gold juice")]
        [Tooltip("Seconds the gold number takes to count up to its new value.")]
        [SerializeField] private float countUpDuration = 0.4f;
        [Tooltip("Half-period of each flash blink, in seconds (two blinks per change).")]
        [SerializeField] private float flashHalfDuration = 0.08f;

        private bool _subscribed;
        private int _lastGold;
        private int _lastTrust;
        private int _displayedGold;
        private Color _normalGoldColor = Color.white;
        private Coroutine _countRoutine;
        private Coroutine _flashRoutine;

        private void Awake()
        {
            if (goldText != null)
            {
                _normalGoldColor = goldText.color;
            }
        }

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

            // Show current values immediately, without animation or floating texts.
            _lastGold = StatsManager.Instance.Gold;
            _lastTrust = StatsManager.Instance.Trust;
            _displayedGold = _lastGold;
            SetGoldText(_lastGold);
            SetTrustText(_lastTrust);
        }

        private void OnGoldChanged(int gold)
        {
            int delta = gold - _lastGold;
            _lastGold = gold;
            if (delta == 0)
            {
                return;
            }

            CreateTextPerState.Create(StatType.Gold, delta);
            StartCountUp(gold);
            Flash(delta >= 0);

            if (delta < 0)
            {
                CameraShake.Trigger();
            }
        }

        private void OnTrustChanged(int trust)
        {
            int delta = trust - _lastTrust;
            _lastTrust = trust;
            SetTrustText(trust);
            if (delta == 0)
            {
                return;
            }

            CreateTextPerState.Create(StatType.Trust, delta);

            if (delta < 0)
            {
                CameraShake.Trigger();
            }
        }

        private void StartCountUp(int target)
        {
            if (goldText == null)
            {
                return;
            }
            if (_countRoutine != null)
            {
                StopCoroutine(_countRoutine);
            }
            _countRoutine = StartCoroutine(CountUpRoutine(target));
        }

        private IEnumerator CountUpRoutine(int target)
        {
            int start = _displayedGold;
            float elapsed = 0f;
            float total = Mathf.Max(0.01f, countUpDuration);
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                _displayedGold = Mathf.RoundToInt(Mathf.Lerp(start, target, elapsed / total));
                SetGoldText(_displayedGold);
                yield return null;
            }
            _displayedGold = target;
            SetGoldText(target);
        }

        private void Flash(bool positive)
        {
            if (goldText == null)
            {
                return;
            }

            Color color = CreateTextPerState.Instance != null
                ? CreateTextPerState.Instance.GetColor(StatType.Gold, positive ? 1 : -1)
                : _normalGoldColor;

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
            }
            _flashRoutine = StartCoroutine(FlashRoutine(color));
        }

        private IEnumerator FlashRoutine(Color flashColor)
        {
            float half = Mathf.Max(0.01f, flashHalfDuration);
            for (int i = 0; i < 2; i++)
            {
                goldText.color = flashColor;
                yield return new WaitForSeconds(half);
                goldText.color = _normalGoldColor;
                yield return new WaitForSeconds(half);
            }
            goldText.color = _normalGoldColor;
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
