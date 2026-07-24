using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// One turn-order slot: a background whose sprite says whose turn it is (one for the player, another
    /// for enemies), the character portrait (a gray image masked to the slot in the prefab) and the
    /// attack-order number.
    /// </summary>
    public class TurnSlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image background;
        [Tooltip("Background sprite used for the player's slot.")]
        [SerializeField] private Sprite playerBackground;
        [Tooltip("Background sprite used for an enemy's slot.")]
        [SerializeField] private Sprite enemyBackground;
        [Tooltip("Character portrait, masked to the slot by the prefab. Stays gray until a sprite is set.")]
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI orderText;

        private Action _onClick;

        /// <summary>What tapping this slot does — opens its combatant's detail window.</summary>
        public void SetClick(Action onClick) => _onClick = onClick;

        public void OnPointerClick(PointerEventData eventData) => _onClick?.Invoke();

        public void Setup(int order, Sprite portraitSprite, bool isPlayer)
        {
            if (background != null)
            {
                Sprite sprite = isPlayer ? playerBackground : enemyBackground;
                if (sprite != null) background.sprite = sprite;
            }
            if (portrait != null && portraitSprite != null) portrait.sprite = portraitSprite;
            if (orderText != null) orderText.text = order.ToString();
        }
    }
}
