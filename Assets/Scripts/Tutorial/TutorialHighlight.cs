using System.Collections;
using UnityEngine;

namespace Undelivered.Tutorial
{
    /// <summary>
    /// Makes a UI element blink to draw the player's attention during the tutorial. Pulses a
    /// <see cref="CanvasGroup"/>'s alpha between <see cref="minAlpha"/> and 1 (adding the group if the
    /// object has none). The tutorial adds this to any target — a box, a slot, a button — and calls
    /// <see cref="Play"/> / <see cref="Stop"/>; <see cref="Stop"/> restores the original alpha.
    /// </summary>
    public class TutorialHighlight : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.3f;
        [Tooltip("Seconds for a full blink cycle.")]
        [SerializeField] private float period = 0.55f;
        [Tooltip("Start blinking as soon as the object is enabled.")]
        [SerializeField] private bool playOnEnable;

        private CanvasGroup _group;
        private Coroutine _loop;
        private float _baseAlpha = 1f;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _baseAlpha = _group.alpha;
        }

        private void OnEnable()
        {
            if (playOnEnable) Play();
        }

        private void OnDisable() => Stop();

        /// <summary>Starts the blink (no-op if already blinking).</summary>
        public void Play()
        {
            if (_group == null || _loop != null) return;
            _loop = StartCoroutine(Blink());
        }

        /// <summary>Stops the blink and restores the original alpha.</summary>
        public void Stop()
        {
            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
            if (_group != null) _group.alpha = _baseAlpha;
        }

        private IEnumerator Blink()
        {
            float t = 0f;
            float p = Mathf.Max(0.05f, period);
            while (true)
            {
                t += Time.unscaledDeltaTime;
                float k = 0.5f * (1f + Mathf.Sin(t / p * Mathf.PI * 2f)); // 0..1..0
                _group.alpha = Mathf.Lerp(minAlpha, 1f, k);
                yield return null;
            }
        }
    }
}
