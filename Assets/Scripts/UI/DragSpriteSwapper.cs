using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Undelivered.UI
{
    /// <summary>
    /// Swaps a draggable UI element's image to a "selected" sprite while the pointer is held down
    /// (press → drag), and restores the default the moment it is released.
    ///
    /// Uses pointer down/up (not drag begin/end) so the swap happens on the initial press and lasts
    /// the whole hold-and-drag, then reverts reliably even if released outside the element.
    /// Works on both <see cref="Image"/> (swaps the sprite) and <see cref="RawImage"/> (swaps the texture).
    /// </summary>
    [RequireComponent(typeof(UIDraggable))]
    [DisallowMultipleComponent]
    public class DragSpriteSwapper : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("Resting sprite. If left empty, whatever the graphic already shows at start is kept.")]
        [SerializeField] private Sprite defaultSprite;

        [Tooltip("Sprite shown while the element is pressed / being dragged.")]
        [SerializeField] private Sprite selectedSprite;

        private Image _image;
        private RawImage _rawImage;
        private Sprite _startSprite;
        private Texture _startTexture;
        private bool _selected;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rawImage = GetComponent<RawImage>();

            if (_image == null && _rawImage == null)
            {
                Debug.LogWarning($"{nameof(DragSpriteSwapper)} on '{name}' needs an Image or RawImage.", this);
                enabled = false;
                return;
            }

            if (_image != null)
            {
                _startSprite = _image.sprite;
            }
            if (_rawImage != null)
            {
                _startTexture = _rawImage.texture;
            }

            ApplyVisual(false); // show the resting sprite from the start
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            SetSelected(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetSelected(false);
        }

        // Never leave the "selected" sprite stuck if the object is disabled mid-press.
        private void OnDisable()
        {
            SetSelected(false);
        }

        private void SetSelected(bool selected)
        {
            if (_selected == selected)
            {
                return;
            }

            _selected = selected;
            ApplyVisual(selected);
        }

        private void ApplyVisual(bool selected)
        {
            Sprite sprite = selected
                ? selectedSprite
                : (defaultSprite != null ? defaultSprite : _startSprite);

            if (_image != null)
            {
                if (sprite != null)
                {
                    _image.sprite = sprite;
                }
            }
            else if (_rawImage != null)
            {
                Texture texture = sprite != null ? sprite.texture : _startTexture;
                if (texture != null)
                {
                    _rawImage.texture = texture;
                }
            }
        }
    }
}
