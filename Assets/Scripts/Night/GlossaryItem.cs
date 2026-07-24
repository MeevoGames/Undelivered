using TMPro;
using Undelivered.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// One cell in the glossary grid. There are three prefab shapes (dice / effect / enemy) sharing this
    /// component; each is filled via its own Set method, which also sets the hover tooltip (Dice / Effect /
    /// General) and — for enemies — makes the cell open the <see cref="EnemyDetailPanel"/> on click. Unknown
    /// items show the correct sprite tinted #000000 with the name "???" and a "???" tooltip.
    /// </summary>
    public class GlossaryItem : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [Tooltip("Effect cells only: a background tinted by the effect's rarity.")]
        [SerializeField] private Image rarityBorder;

        [Header("Effect rarity colours")]
        [SerializeField] private Color normalColor = new Color(0.62f, 0.62f, 0.58f);
        [Tooltip("Border of a golden effect (a bigger-scale version of another one).")]
        [SerializeField] private Color goldenColor = new Color(0.96f, 0.76f, 0.23f);

        private EnemyData _enemy;   // set for enemy cells → click opens the detail
        private EnemyRarity _rarity;
        private bool _known;

        public void SetDie(DiceData die, bool known)
        {
            _enemy = null;
            _known = known;
            Fill(die != null ? die.Icon : null, die != null ? die.DiceName : "", known);
            if (rarityBorder != null) rarityBorder.enabled = false;

            TooltipTrigger t = GetComponent<TooltipTrigger>();
            if (t == null) return;
            if (known && die != null) t.SetDice(die.DiceName, die.DescriptionForTooltip, die.FaceSprites(), $"{die.BaseLuckPercent}% de Suerte.");
            else t.SetMessage("???");
        }

        public void SetEffect(EffectData effect, bool known)
        {
            _enemy = null;
            _known = known;
            Fill(effect != null ? effect.Icon : null, effect != null ? effect.EffectName : "", known);
            if (rarityBorder != null)
            {
                rarityBorder.enabled = effect != null;
                if (effect != null) rarityBorder.color = effect.IsGolden ? goldenColor : normalColor;
            }

            TooltipTrigger t = GetComponent<TooltipTrigger>();
            if (t == null) return;
            if (known && effect != null) t.SetEffect(effect.EffectName, effect.DescriptionForTooltip, effect.GoldenText);
            else t.SetMessage("???");
        }

        public void SetEnemy(EnemyData enemy, EnemyRarity rarity, bool known)
        {
            _enemy = enemy;
            _rarity = rarity;
            _known = known;
            Fill(enemy != null ? enemy.Sprite : null, enemy != null ? enemy.EnemyName : "", known);
            if (rarityBorder != null) rarityBorder.enabled = false;

            TooltipTrigger t = GetComponent<TooltipTrigger>();
            if (t == null) return;
            if (known && enemy != null) t.SetGeneral(enemy.EnemyName, enemy.DescriptionForTooltip);
            else t.SetMessage("???");
        }

        // Icon shows the sprite (black silhouette when unknown); the name is "???" when unknown.
        private void Fill(Sprite sprite, string itemName, bool known)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.color = known ? Color.white : Color.black;
            }
            if (nameText != null) nameText.text = known ? itemName : "???";
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_enemy != null && _known && EnemyDetailPanel.Instance != null)
                EnemyDetailPanel.Instance.Show(_enemy, _rarity, null); // synergy has no meaning outside combat
        }
    }
}
