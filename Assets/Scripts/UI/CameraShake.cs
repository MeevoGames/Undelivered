using System.Collections;
using UnityEngine;

namespace Undelivered.UI
{
    /// <summary>
    /// Shakes the transform it is attached to (a decaying random offset). Uses anchoredPosition for a
    /// RectTransform (reliable for UI) or localPosition otherwise. Trigger it statically with
    /// <see cref="Trigger"/>, or hold a direct reference and call <see cref="Shake"/>.
    ///
    /// IMPORTANT: a Screen Space - Overlay canvas ignores the camera, so shaking the Camera does nothing
    /// for overlay UI. Attach this to a UI content root (RectTransform) that holds the visuals instead.
    /// Several shakers can coexist (e.g. day HUD + night combat); set <see cref="registerAsGlobal"/> off
    /// on the secondary ones and reference them directly.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Tooltip("How long a shake lasts, in seconds.")]
        [SerializeField] private float duration = 0.25f;

        [Tooltip("Max offset of the shake (UI units for a RectTransform, world units for a camera).")]
        [SerializeField] private float magnitude = 30f;

        [Tooltip("Register as the global singleton for the static Trigger(). Turn off for a secondary shaker referenced directly.")]
        [SerializeField] private bool registerAsGlobal = true;

        private RectTransform _rect;
        private Vector2 _baseAnchored;
        private Vector3 _baseLocal;
        private Coroutine _routine;

        private void Awake()
        {
            if (registerAsGlobal && Instance == null) Instance = this;

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
                elapsed += Time.unscaledDeltaTime;
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
