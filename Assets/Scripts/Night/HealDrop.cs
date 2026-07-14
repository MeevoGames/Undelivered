using System;
using System.Collections;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// A healing pickup / icon that animates in the combat overlay: it can fall to the ground, rest there,
    /// then fly to the player, invoking a callback (the heal) on arrival before destroying itself. Used by
    /// <see cref="HealVisuals"/> for both enemy heal drops and the heal-room fountain icons.
    /// </summary>
    public class HealDrop : MonoBehaviour
    {
        /// <summary>Falls from <paramref name="spawn"/> to <paramref name="ground"/>, waits, flies to <paramref name="target"/>, then calls <paramref name="onArrive"/>.</summary>
        public void Play(Vector2 spawn, Vector2 ground, float fallTime, float groundTime, Vector2 target, float flyTime, Action onArrive)
        {
            RectTransform rt = transform as RectTransform;
            if (rt == null) { onArrive?.Invoke(); Destroy(gameObject); return; }
            StartCoroutine(Routine(rt, spawn, ground, fallTime, groundTime, target, flyTime, onArrive));
        }

        private IEnumerator Routine(RectTransform rt, Vector2 spawn, Vector2 ground, float fallTime, float groundTime, Vector2 target, float flyTime, Action onArrive)
        {
            rt.anchoredPosition = spawn;
            yield return Move(rt, spawn, ground, fallTime, easeIn: true);    // drop, accelerating
            if (groundTime > 0f) yield return new WaitForSecondsRealtime(groundTime);
            yield return Move(rt, ground, target, flyTime, easeIn: false);   // fly to the player
            onArrive?.Invoke();
            Destroy(gameObject);
        }

        private IEnumerator Move(RectTransform rt, Vector2 a, Vector2 b, float duration, bool easeIn)
        {
            if (duration <= 0f) { rt.anchoredPosition = b; yield break; }
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                k = easeIn ? k * k : Mathf.SmoothStep(0f, 1f, k);
                rt.anchoredPosition = Vector2.Lerp(a, b, k);
                yield return null;
            }
            rt.anchoredPosition = b;
        }
    }
}
