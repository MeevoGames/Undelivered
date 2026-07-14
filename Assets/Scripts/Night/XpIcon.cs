using System.Collections;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// A single XP icon: spawned over a hit enemy, it flies to the XP bar and destroys itself on arrival.
    /// Purely cosmetic — the XP is already counted when the icon is spawned.
    /// </summary>
    public class XpIcon : MonoBehaviour
    {
        /// <summary>Flies from <paramref name="startLocal"/> to <paramref name="endLocal"/> (icon-container space), then destroys itself.</summary>
        public void Fly(Vector2 startLocal, Vector2 endLocal, float duration)
        {
            RectTransform rt = transform as RectTransform;
            if (rt == null) { Destroy(gameObject); return; }
            rt.anchoredPosition = startLocal;
            StartCoroutine(FlyRoutine(rt, startLocal, endLocal, Mathf.Max(0.05f, duration)));
        }

        private IEnumerator FlyRoutine(RectTransform rt, Vector2 a, Vector2 b, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                rt.anchoredPosition = Vector2.Lerp(a, b, k);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
