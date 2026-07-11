using System;
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
        [SerializeField, Range(0f, 1f)] private float spentAlpha = 0.4f;

        [Header("Rarity border colours")]
        [SerializeField] private Color comunColor = new Color(0.62f, 0.62f, 0.58f);
        [SerializeField] private Color raraColor = new Color(0.23f, 0.51f, 0.96f);
        [SerializeField] private Color epicaColor = new Color(0.65f, 0.33f, 0.94f);

        private CanvasGroup _canvasGroup;
        private Action _onClick;

        public EffectData Effect { get; private set; }

        /// <summary>Activated for the next throw.</summary>
        public bool Selected { get; private set; }

        /// <summary>Consumed already this combat: dimmed and untappable.</summary>
        public bool Spent { get; private set; }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void SetClick(Action onClick) => _onClick = onClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Spent) _onClick?.Invoke();
        }

        public void SetSelected(bool selected)
        {
            Selected = selected;
            if (selectedMarker != null) selectedMarker.SetActive(selected);
        }

        public void SetSpent(bool spent)
        {
            Spent = spent;
            if (spent) SetSelected(false);
            if (_canvasGroup != null) _canvasGroup.alpha = spent ? spentAlpha : 1f;
        }

        public void Setup(EffectData effect)
        {
            Effect = effect;
            if (effect == null) return;

            if (icon != null)
            {
                icon.sprite = effect.Icon;
                icon.enabled = effect.Icon != null;
            }
            if (border != null) border.color = ColorFor(effect.EffectRarity);
            if (priceText != null) priceText.text = effect.Price.ToString();

            TooltipTrigger tooltip = GetComponent<TooltipTrigger>();
            if (tooltip != null)
            {
                tooltip.SetMessage(string.IsNullOrEmpty(effect.DescriptionForTooltip)
                    ? effect.EffectName
                    : $"{effect.EffectName}\n{effect.DescriptionForTooltip}");
            }

            SetSelected(false); // start deselected (the prefab marker may default to on)
        }

        private Color ColorFor(EffectData.Rarity rarity)
        {
            switch (rarity)
            {
                case EffectData.Rarity.Rara: return raraColor;
                case EffectData.Rarity.Epica: return epicaColor;
                default: return comunColor;
            }
        }
    }
}
