using System.Collections;
using UnityEngine;

namespace Undelivered.UI
{
    /// <summary>
    /// Shakes the transform it is attached to (a decaying random offset). Uses anchoredPosition for a
    /// RectTransform (reliable for UI) or localPosition otherwise. Trigger it statically with
    /// <see cref="Trigger"/>. For Screen Space - Overlay UI attach it to a UI content root (an overlay
    /// canvas itself can't be moved); for camera/world-space canvases, the camera works.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Tooltip("How long a shake lasts, in seconds.")]
        [SerializeField] private float duration = 0.25f;

        [Tooltip("Max offset of the shake (UI units for a RectTransform, world units for a camera).")]
        [SerializeField] private float magnitude = 30f;

        private RectTransform _rect;
        private Vector2 _baseAnchored;
        private Vector3 _baseLocal;
        private Coroutine _routine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _rect = transform as RectTransform;
            if (_rect != null)
            {
                _baseAnchored = _rect.anchoredPosition;
            }
            else
            {
                _baseLocal = transform.localPosition;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Shakes the singleton, if there is one.</summary>
        public static void Trigger()
        {
            if (Instance != null)
            {
                Instance.Shake();
            }
        }

        public void Shake()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            _routine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            float elapsed = 0f;
            float total = Mathf.Max(0.01f, duration);
            while (elapsed < total)
            {
                elapsed += Time.deltaTime;
                float damper = 1f - Mathf.Clamp01(elapsed / total);
                ApplyOffset(Random.insideUnitCircle * (magnitude * damper));
                yield return null;
            }
            ApplyOffset(Vector2.zero);
        }

        private void ApplyOffset(Vector2 offset)
        {
            if (_rect != null)
            {
                _rect.anchoredPosition = _baseAnchored + offset;
            }
            else
            {
                transform.localPosition = _baseLocal + (Vector3)offset;
            }
        }
    }
}
