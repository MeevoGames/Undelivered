using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The Game Over window, shown when the player dies. Drops in from the top with a bounce (like the
    /// shop / inventory). Its "Continuar" button raises <see cref="Continued"/> and closes the window;
    /// the combat controller then applies the tournament penalty and returns to tournament selection.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform panel;
        [SerializeField] private Button continueButton;

        [Header("Bounce (from the top, like the shop/inventory)")]
        [SerializeField] private float hiddenY = 1200f;
        [SerializeField] private float centerY = 0f;
        [SerializeField] private float undershootY = -25f;
        [SerializeField] private float overshootY = 15f;
        [SerializeField] private float speed = 3500f;
        [SerializeField] private float minSegment = 0.06f;

        /// <summary>Raised when the player presses "Continuar".</summary>
        public event Action Continued;

        private Coroutine _anim;

        private void Awake()
        {
            if (panel != null) SetY(hiddenY);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
        }

        private void OnDestroy()
        {
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinue);
        }

        /// <summary>Bounces the window in from the top.</summary>
        public void Show()
        {
            if (panel == null) return;
            if (_anim != null) StopCoroutine(_anim);
            SetY(hiddenY);
            _anim = StartCoroutine(MoveThroughY(undershootY, overshootY, centerY));
        }

        /// <summary>Bounces the window back out through the top.</summary>
        public void Hide()
        {
            if (panel == null) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(MoveThroughY(overshootY, undershootY, hiddenY));
        }

        private void OnContinue()
        {
            Hide();
            Continued?.Invoke();
        }

        private IEnumerator MoveThroughY(params float[] targets)
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
            p.y = y;
            panel.anchoredPosition = p;
        }
    }
}
