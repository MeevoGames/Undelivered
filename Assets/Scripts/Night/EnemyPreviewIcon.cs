using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// A clickable enemy icon in a preview (the combat-prep view): shows the enemy's sprite and, on click,
    /// opens the <see cref="EnemyDetailPanel"/> for that enemy at its rarity. Needs a raycast-target Image.
    /// </summary>
    public class EnemyPreviewIcon : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image icon;

        private EnemyData _enemy;
        private EnemyRarity _rarity;
        private SynergyData _synergy;

        public void Setup(EnemyData enemy, EnemyRarity rarity, SynergyData synergy)
        {
            _enemy = enemy;
            _rarity = rarity;
            _synergy = synergy;
            if (icon != null)
            {
                icon.sprite = enemy != null ? enemy.Sprite : null;
                icon.enabled = enemy != null && enemy.Sprite != null;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_enemy != null && EnemyDetailPanel.Instance != null)
                EnemyDetailPanel.Instance.Show(_enemy, _rarity, _synergy);
        }
    }
}
