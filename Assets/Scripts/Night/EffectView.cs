using System;
using System.Collections;
using TMPro;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// An effect entry, used everywhere the effect is shown (effectPlayer / inventory: image only; shop:
    /// image + price). The rarity is shown by the colour of the <see cref="border"/> background image.
    /// Name and description live in the hover tooltip. Leave <see cref="priceText"/> unassigned outside
    /// the shop.
    ///
    /// In the effect deck it is also tappable: a tap toggles it activated (a marker shows) so it will be
    /// consumed on the next die throw. Once consumed it becomes <see cref="Spent"/> — dimmed and untappable.
    /// </summary>
    public class EffectView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image icon;

        [Tooltip("Background image whose colour shows the rarity (the border).")]
        [SerializeField] private Image border;

        [Tooltip("Price text — assign in the shop prefab, leave empty in effectPlayer / inventory.")]
        [SerializeField] private TextMeshProUGUI priceText;

        [Tooltip("Shown while the effect is activated for the next throw (leave empty outside the deck).")]
        [SerializeField] private GameObject selectedMarker;

        [Header("Border colours")]
        [SerializeField] private Color normalColor = new Color(0.62f, 0.62f, 0.58f);
        [Tooltip("Border of a golden effect (a bigger-scale version of another one).")]
        [SerializeField] private Color goldenColor = new Color(0.96f, 0.76f, 0.23f);

        [Header("Draw / discard animation")]
        [Tooltip("How long the card takes to pop in when drawn.")]
        [SerializeField] private float drawSeconds = 0.18f;
        [Tooltip("How far past full size it overshoots as it arrives.")]
        [SerializeField] private float drawOvershoot = 1.12f;
        [Tooltip("How long it takes to shrink away when spent.")]
        [SerializeField] private float discardSeconds = 0.16f;

        private CanvasGroup _canvasGroup;
        private Action _onClick;
        private Vector3 _baseScale = Vector3.one;

        public EffectData Effect { get; private set; }

        /// <summary>Activated for the next throw.</summary>
        public bool Selected { get; private set; }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _baseScale = transform.localScale;
        }

        public void SetClick(Action onClick) => _onClick = onClick;

        public void OnPointerClick(PointerEventData eventData) => _onClick?.Invoke();

        public void SetSelected(bool selected)
        {
            Selected = selected;
            if (selectedMarker != null) selectedMarker.SetActive(selected);
        }

        /// <summary>Pops the card in as it is dealt into the hand.</summary>
        public void PlayDraw()
        {
            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(DrawRoutine());
        }

        /// <summary>Shrinks the card away as it is spent, then destroys it (it goes to the discard pile).</summary>
        public void PlayDiscard()
        {
            SetSelected(false);
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false; // can't be tapped on its way out
            _onClick = null;
            StopAllCoroutines();
            StartCoroutine(DiscardRoutine());
        }

        private IEnumerator DrawRoutine()
        {
            // 0 → past full size → settle.
            yield return ScaleTo(Vector3.zero, _baseScale * drawOvershoot, drawSeconds * 0.6f, 1f, 1f);
            yield return ScaleTo(_baseScale * drawOvershoot, _baseScale, drawSeconds * 0.4f, 1f, 1f);
            transform.localScale = _baseScale;
        }

        private IEnumerator DiscardRoutine()
        {
            yield return ScaleTo(transform.localScale, Vector3.zero, discardSeconds, 1f, 0f);
            Destroy(gameObject);
        }

        private IEnumerator ScaleTo(Vector3 from, Vector3 to, float duration, float fromAlpha, float toAlpha)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, duration));
                transform.localScale = Vector3.Lerp(from, to, k);
                if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, k);
                yield return null;
            }
            transform.localScale = to;
            if (_canvasGroup != null) _canvasGroup.alpha = toAlpha;
        }

        /// <summary>Fills the entry. Pass <paramref name="showPrice"/> false where nothing is being bought (a gift).</summary>
        public void Setup(EffectData effect, bool showPrice = true)
        {
            Effect = effect;
            if (effect == null) return;

            if (icon != null)
            {
                icon.sprite = effect.Icon;
                icon.enabled = effect.Icon != null;
            }
            if (border != null) border.color = effect.IsGolden ? goldenColor : normalColor;
            if (priceText != null)
            {
                priceText.gameObject.SetActive(showPrice);
                if (showPrice) priceText.text = effect.Price.ToString();
            }

            TooltipTrigger tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null)
                tooltip.SetEffect(effect.EffectName, effect.DescriptionForTooltip, effect.GoldenText);

            SetSelected(false); // start deselected (the prefab marker may default to on)
        }
    }
}
