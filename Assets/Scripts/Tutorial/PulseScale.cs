using System.Collections;
using UnityEngine;

namespace Undelivered.Tutorial
{
    /// <summary>
    /// Continuously pulses an element's scale (shrinking and growing). Used by the tutorial's "select
    /// this" hint, which sits still beside a blinking button instead of travelling like the delivery hint.
    /// </summary>
    public class PulseScale : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] private float minScale = 0.85f;
        [SerializeField, Range(1f, 2f)] private float maxScale = 1.15f;
        [Tooltip("Seconds for a full shrink + grow cycle.")]
        [SerializeField] private float period = 0.8f;

        private Vector3 _baseScale = Vector3.one;

        private void Awake() => _baseScale = transform.localScale;

        private void OnEnable() => StartCoroutine(Pulse());

        private void OnDisable()
        {
            StopAllCoroutines();
            transform.localScale = _baseScale;
        }

        private IEnumerator Pulse()
        {
            float t = 0f;
            float p = Mathf.Max(0.05f, period);
            while (true)
            {
                t += Time.unscaledDeltaTime;
                float k = 0.5f * (1f + Mathf.Sin(t / p * Mathf.PI * 2f)); // 0..1..0
                transform.localScale = _baseScale * Mathf.Lerp(minScale, maxScale, k);
                yield return null;
            }
        }
    }
}
