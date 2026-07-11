using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// One turn-order slot: a coloured background (red for an enemy, teal for the player), the
    /// character portrait (a gray image masked to the slot in the prefab) and the attack-order number.
    /// </summary>
    public class TurnSlot : MonoBehaviour
    {
        [SerializeField] private Image background;
        [Tooltip("Character portrait, masked to the slot by the prefab. Stays gray until a sprite is set.")]
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI orderText;

        public void Setup(int order, Sprite portraitSprite, Color color)
        {
            if (background != null) background.color = color;
            if (portrait != null && portraitSprite != null) portrait.sprite = portraitSprite;
            if (orderText != null) orderText.text = order.ToString();
        }
    }
}
