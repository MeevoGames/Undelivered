using System;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// A uniform inventory grid cell for any owned thing (box / die / effect): its icon, a rarity
    /// border (boxes and effects; hidden for dice), a selected marker (deck-selection mode) and a tap.
    /// Name and description go in the hover tooltip.
    /// </summary>
    public class InventoryItem : MonoBehaviour
    {
        public enum Kind { Box, Die, Effect }

        [SerializeField] private Image icon;
        [Tooltip("Border tinted by rarity (boxes / effects); hidden for dice.")]
        [SerializeField] private Image border;
        [Tooltip("Shown while this item is selected in deck-selection mode.")]
        [SerializeField] private GameObject selectedMarker;
        [SerializeField] private Button button;

        [Header("Box rarity border colours")]
        [SerializeField] private Color comunColor = new Color(0.62f, 0.62f, 0.58f);
        [SerializeField] private Color raraColor = new Color(0.23f, 0.51f, 0.96f);
        [SerializeField] private Color epicaColor = new Color(0.65f, 0.33f, 0.94f);

        [Tooltip("Border of a golden effect (a bigger-scale version of another one).")]
        [SerializeField] private Color goldenColor = new Color(0.96f, 0.76f, 0.23f);

        public Kind ItemKind { get; private set; }
        public BoxData Box { get; private set; }
        public DiceData Die { get; private set; }
        public EffectData Effect { get; private set; }

        public void SetupBox(BoxData box, Action onClick)
        {
            Begin(Kind.Box, onClick);
            Box = box;
            SetIcon(box.Icon);
            SetBorder(true, (int)box.BoxRarity);
            Tooltip?.SetGeneral(box.BoxName, box.DescriptionForTooltip);
        }

        public void SetupDie(DiceData die, Action onClick)
        {
            Begin(Kind.Die, onClick);
            Die = die;
            SetIcon(die.Icon);
            SetBorder(false, 0);
            Tooltip?.SetDice(die.DiceName, die.DescriptionForTooltip, die.FaceSprites(), $"{die.BaseLuckPercent}% de Suerte.");
        }

        public void SetupEffect(EffectData effect, Action onClick)
        {
            Begin(Kind.Effect, onClick);
            Effect = effect;
            SetIcon(effect.Icon);
            SetEffectBorder(effect.IsGolden);
            Tooltip?.SetEffect(effect.EffectName, effect.DescriptionForTooltip, effect.GoldenText);
        }

        public void SetSelected(bool selected)
        {
            if (selectedMarker != null) selectedMarker.SetActive(selected);
        }

        private void Begin(Kind kind, Action onClick)
        {
            ItemKind = kind;
            Box = null; Die = null; Effect = null;
            SetSelected(false);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }

        private void SetIcon(Sprite sprite)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        // Boxes still have rarities, so their border keeps the three tiers.
        private void SetBorder(bool show, int tier)
        {
            if (border == null) return;
            border.enabled = show;
            if (show) border.color = tier == 2 ? epicaColor : tier == 1 ? raraColor : comunColor;
        }

        // Effects have no rarity — only whether they're golden.
        private void SetEffectBorder(bool golden)
        {
            if (border == null) return;
            border.enabled = true;
            border.color = golden ? goldenColor : comunColor;
        }

        private TooltipTrigger Tooltip => GetComponent<TooltipTrigger>();
    }
}
