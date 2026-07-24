using System.Collections;
using UnityEngine;

namespace Undelivered.UI
{
    /// <summary>
    /// A panel that drops in from the top with a bounce and leaves the same way, matching the night UI
    /// (shop / inventory / level-up). <see cref="Show"/> and <see cref="Hide"/> are coroutines to yield on.
    /// Used by the tutorial windows and the combat announcements.
    /// </summary>
    public class BouncePanel : MonoBehaviour
    {
        [Tooltip("The panel that moves (defaults to this object's RectTransform).")]
        [SerializeField] private RectTransform panel;

        [Header("Bounce (anchoredPosition.y)")]
        [SerializeField] private float hiddenY = 1200f;
        [SerializeField] private float centerY = 0f;
        [SerializeField] private float undershootY = -25f;
        [SerializeField] private float overshootY = 15f;
        [SerializeField] private float speed = 3500f;
        [SerializeField] private float minSegment = 0.06f;
        [Tooltip("Snap to the hidden position on Awake.")]
        [SerializeField] private bool startHidden = true;

        private Coroutine _anim;

        private void Awake()
        {
            if (panel == null) panel = transform as RectTransform;
            if (startHidden && panel != null) SetY(hiddenY);
        }

        /// <summary>Bounces the panel in from the top (hidden → undershoot → overshoot → centre).</summary>
        public IEnumerator Show()
        {
            if (panel == null) yield break;
            SetY(hiddenY);
            yield return Move(undershootY, overshootY, centerY);
        }

        /// <summary>Bounces the panel back out to the top (centre → overshoot → undershoot → hidden).</summary>
        public IEnumerator Hide()
        {
            if (panel == null) yield break;
            yield return Move(overshootY, undershootY, hiddenY);
        }

        /// <summary>Snaps the panel to its hidden position immediately (no animation).</summary>
        public void SnapHidden()
        {
            if (_anim != null) { StopCoroutine(_anim); _anim = null; }
            if (panel != null) SetY(hiddenY);
        }

        private IEnumerator Move(params float[] targets)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(MoveRoutine(targets));
            yield return _anim;
        }

        private IEnumerator MoveRoutine(float[] targets)
        {
            foreach (float target in targets)
            {
                Vector2 start = panel.anchoredPosition;
                float distance = Mathf.Abs(target - start.y);
                float duration = Mathf.Max(minSegment, distance / Mathf.Max(1f, speed));
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / duration);
                    panel.anchoredPosition = new Vector2(start.x, Mathf.Lerp(start.y, target, k));
                    yield return null;
                }
                panel.anchoredPosition = new Vector2(start.x, target);
            }
            _anim = null;
        }

        private void SetY(float y)
        {
            Vector2 p = panel.anchoredPosition;
            panel.anchoredPosition = new Vector2(p.x, y);
        }
    }
}
