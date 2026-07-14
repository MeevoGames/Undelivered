using System.Collections;
using TMPro;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// Floating value numbers over a character (the player or an enemy). Each spawns at a random point
    /// within a radius around the character, rises while fading in (alpha 0 → 1), then keeps rising while
    /// fading out (1 → 0), and destroys itself once invisible. Coloured by kind: damage, heal, or effect
    /// (poison, burn, and similar — used later).
    /// </summary>
    public class FloatingNumbers : MonoBehaviour
    {
        public enum Kind { Damage, Heal, Effect, Bonus, Burn, Poison, Freeze }

        public static FloatingNumbers Instance { get; private set; }

        [SerializeField] private TextMeshProUGUI textPrefab;
        [Tooltip("Where numbers are parented (an overlay above the combat). Defaults to this transform.")]
        [SerializeField] private RectTransform container;

        [Header("Colours")]
        [SerializeField] private Color damageColor = new Color(0.9f, 0.25f, 0.2f);
        [SerializeField] private Color healColor = new Color(0.35f, 0.85f, 0.4f);
        [SerializeField] private Color effectColor = new Color(0.7f, 0.4f, 0.95f);
        [Tooltip("Extra damage added by an effect (the '+X' text).")]
        [SerializeField] private Color bonusColor = new Color(1f, 0.75f, 0.2f);
        [Tooltip("Quema (burn) damage.")]
        [SerializeField] private Color burnColor = new Color(1f, 0.5f, 0.15f);
        [Tooltip("Veneno (poison) damage.")]
        [SerializeField] private Color poisonColor = new Color(0.55f, 0.85f, 0.25f);
        [Tooltip("Congelamiento (freeze) — used if freeze ever shows a number.")]
        [SerializeField] private Color freezeColor = new Color(0.4f, 0.8f, 1f);

        [Header("Placement")]
        [Tooltip("Max distance (px) from the character the number can appear.")]
        [SerializeField] private float spawnRadius = 60f;

        [Header("Animation")]
        [Tooltip("How far it rises (px) while fading in.")]
        [SerializeField] private float fadeInRise = 25f;
        [SerializeField] private float fadeInDuration = 0.25f;
        [Tooltip("How far it rises (px) while fading out.")]
        [SerializeField] private float fadeOutRise = 55f;
        [SerializeField] private float fadeOutDuration = 0.6f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (container == null) container = transform as RectTransform;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Spawns a floating number around a character (no-op if there is no spawner).</summary>
        public static void Spawn(RectTransform around, int amount, Kind kind)
        {
            if (Instance != null) Instance.SpawnAt(around, amount, kind);
        }

        private void SpawnAt(RectTransform around, int amount, Kind kind)
        {
            if (textPrefab == null || container == null || around == null) return;

            TextMeshProUGUI text = Instantiate(textPrefab, container, false);
            text.text = Format(amount, kind);
            text.color = ColorFor(kind);
            text.raycastTarget = false;

            RectTransform rt = text.rectTransform;
            // A random point in ANY direction around the character (below, above, left, right...). Offset
            // it down by the fade-in rise so the number is fully opaque exactly at that point, then rises.
            Vector2 point = LocalPointOf(around) + Random.insideUnitCircle * spawnRadius;
            rt.anchoredPosition = point - Vector2.up * fadeInRise;

            StartCoroutine(Animate(rt));
        }

        private IEnumerator Animate(RectTransform rt)
        {
            CanvasGroup group = rt.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();

            Vector2 start = rt.anchoredPosition;
            group.alpha = 0f;

            // Rise while fading in (0 → 1).
            for (float t = 0f; t < fadeInDuration; t += Time.unscaledDeltaTime)
            {
                float k = t / fadeInDuration;
                group.alpha = k;
                rt.anchoredPosition = start + Vector2.up * (fadeInRise * k);
                yield return null;
            }

            Vector2 mid = start + Vector2.up * fadeInRise;
            group.alpha = 1f;

            // Keep rising while fading out (1 → 0), then destroy.
            for (float t = 0f; t < fadeOutDuration; t += Time.unscaledDeltaTime)
            {
                float k = t / fadeOutDuration;
                group.alpha = 1f - k;
                rt.anchoredPosition = mid + Vector2.up * (fadeOutRise * k);
                yield return null;
            }

            Destroy(rt.gameObject);
        }

        private Color ColorFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Heal: return healColor;
                case Kind.Effect: return effectColor;
                case Kind.Bonus: return bonusColor;
                case Kind.Burn: return burnColor;
                case Kind.Poison: return poisonColor;
                case Kind.Freeze: return freezeColor;
                default: return damageColor;
            }
        }

        private static string Format(int amount, Kind kind)
        {
            int magnitude = Mathf.Abs(amount);
            switch (kind)
            {
                case Kind.Heal: return $"+{magnitude}";
                case Kind.Bonus: return $"+{magnitude}";
                case Kind.Effect: return magnitude.ToString();
                default: return magnitude.ToString(); // damage
            }
        }

        // The character's position expressed in the container's local space.
        private Vector2 LocalPointOf(RectTransform around)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, around.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screen, cam, out Vector2 local);
            return local;
        }
    }
}
