using System.Collections;
using TMPro;
using Undelivered.Player;
using UnityEngine;

namespace Undelivered.UI
{
    /// <summary>
    /// Spawns temporary floating value texts (e.g. "+50", "-30") under the HUD space that matches a
    /// <see cref="StatType"/>. The text is colored by stat and sign, holds for a moment, then rises
    /// slightly while fading out, and destroys itself once invisible.
    ///
    /// Call it statically via <see cref="Create"/>; it forwards to the singleton instance.
    /// </summary>
    public class CreateTextPerState : MonoBehaviour
    {
        public static CreateTextPerState Instance { get; private set; }

        [Header("Text")]
        [Tooltip("TextMeshPro prefab instantiated for each floating text.")]
        [SerializeField] private TextMeshProUGUI textPrefab;

        [Header("Spaces (parents per stat)")]
        [SerializeField] private GameObject goldSpace;
        [SerializeField] private GameObject trustSpace;
        [SerializeField] private GameObject diceSpace;

        [Header("Colors")]
        [SerializeField] private Color goldPositive = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color goldNegative = new Color(0.9f, 0.25f, 0.2f);
        [SerializeField] private Color trustPositive = new Color(0.3f, 0.85f, 0.4f);
        [SerializeField] private Color trustNegative = new Color(0.9f, 0.25f, 0.2f);
        [SerializeField] private Color dice = new Color(0.4f, 0.7f, 1f);

        [Header("Animation")]
        [Tooltip("Seconds the text stays still before rising and fading.")]
        [SerializeField] private float holdSeconds = 0.6f;
        [Tooltip("How far up (pixels) the text rises while fading out.")]
        [SerializeField] private float riseDistance = 40f;
        [Tooltip("Seconds the rise + fade takes.")]
        [SerializeField] private float fadeDuration = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Static entry point: spawns a floating value text in the space for the given stat.</summary>
        public static void Create(StatType stat, int value)
        {
            if (Instance != null)
            {
                Instance.Spawn(stat, value);
            }
        }

        private void Spawn(StatType stat, int value)
        {
            GameObject space = GetSpace(stat);
            if (space == null || textPrefab == null)
            {
                Debug.LogWarning($"{nameof(CreateTextPerState)} is missing the text prefab or the {stat} space.", this);
                return;
            }

            TextMeshProUGUI text = Instantiate(textPrefab, space.transform, false);
            text.text = Format(value);
            text.color = GetColor(stat, value);
            text.raycastTarget = false; // never block drops/clicks

            StartCoroutine(Animate(text));
        }

        private GameObject GetSpace(StatType stat)
        {
            switch (stat)
            {
                case StatType.Gold: return goldSpace;
                case StatType.Trust: return trustSpace;
                case StatType.Dice: return diceSpace;
                default: return null;
            }
        }

        private Color GetColor(StatType stat, int value)
        {
            switch (stat)
            {
                case StatType.Gold: return value >= 0 ? goldPositive : goldNegative;
                case StatType.Trust: return value >= 0 ? trustPositive : trustNegative;
                case StatType.Dice: return dice;
                default: return Color.white;
            }
        }

        private static string Format(int value)
        {
            return value >= 0 ? $"+{value}" : value.ToString(); // negatives already carry the '-'
        }

        private IEnumerator Animate(TextMeshProUGUI text)
        {
            RectTransform rect = text.rectTransform;
            CanvasGroup group = text.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = text.gameObject.AddComponent<CanvasGroup>();
            }

            Vector2 startPosition = rect.anchoredPosition;
            group.alpha = 1f;

            yield return new WaitForSeconds(holdSeconds);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / fadeDuration);
                rect.anchoredPosition = startPosition + Vector2.up * (riseDistance * k);
                group.alpha = 1f - k;
                yield return null;
            }

            Destroy(text.gameObject);
        }
    }
}
