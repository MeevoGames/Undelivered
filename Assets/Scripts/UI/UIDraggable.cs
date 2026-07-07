using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.UI
{
    /// <summary>
    /// Makes any UI element draggable with the pointer (mouse or touch).
    /// Attach it to a <see cref="RectTransform"/> that lives under a Canvas and it just works.
    ///
    /// Movement is driven through the EventSystem drag callbacks, so it is independent of the
    /// active input backend (new Input System or legacy). The element keeps the exact point that
    /// was grabbed under the cursor, and canvas scaling is handled automatically.
    ///
    /// Assumes a point-anchored element (anchorMin == anchorMax), which is the normal case for
    /// draggable cards, boxes and tokens. Stretched anchors are not supported.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Behaviour")]
        [Tooltip("Render this element on top of its siblings while it is being dragged.")]
        [SerializeField] private bool bringToFrontOnDrag = true;

        [Tooltip("Let pointer raycasts pass through this element while dragging, so drop targets " +
                 "underneath can be detected. Requires (and auto-adds) a CanvasGroup.")]
        [SerializeField] private bool passThroughRaycastsWhileDragging = true;

        [Tooltip("Snap back to the position it had when the drag started once the drag ends. " +
                 "Useful for slot-based systems where the drop target decides the final placement.")]
        [SerializeField] private bool returnToStartOnDrop = false;

        [Tooltip("Only follow the pointer while it is over a registered DragZone (table or slots), " +
                 "so the element can't be dragged into dead screen space. Ignored while no " +
                 "DragZones exist, so unrestricted draggables still work.")]
        [SerializeField] private bool constrainToDragZones = false;

        /// <summary>Raised when a drag starts, after the grab offset has been captured.</summary>
        public event Action<UIDraggable> DragBegan;

        /// <summary>Raised when a drag ends, before any return-to-start snapping is applied.</summary>
        public event Action<UIDraggable> DragEnded;

        private RectTransform _rectTransform;
        private RectTransform _parentRect;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;

        private Vector2 _startAnchoredPosition;
        private int _startSiblingIndex;
        private Vector2 _grabOffset;

        /// <summary>True while the element is currently being dragged.</summary>
        public bool IsDragging { get; private set; }

        /// <summary>Anchored position the element had when the current (or last) drag began.</summary>
        public Vector2 StartAnchoredPosition => _startAnchoredPosition;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();

            if (passThroughRaycastsWhileDragging)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _parentRect = _rectTransform.parent as RectTransform;
            if (_parentRect == null)
            {
                Debug.LogWarning($"{nameof(UIDraggable)} on '{name}' needs a RectTransform parent to drag against.", this);
                return;
            }

            _startAnchoredPosition = _rectTransform.anchoredPosition;
            _startSiblingIndex = _rectTransform.GetSiblingIndex();

            // Capture the offset between the element's anchored position and the pointer, both in
            // parent-local space. Adding it back every frame keeps the grabbed point under the cursor.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect, eventData.position, GetEventCamera(), out Vector2 pointerLocal))
            {
                _grabOffset = _startAnchoredPosition - pointerLocal;
            }
            else
            {
                _grabOffset = Vector2.zero;
            }

            if (bringToFrontOnDrag)
            {
                _rectTransform.SetAsLastSibling();
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
            }

            IsDragging = true;
            DragBegan?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging || _parentRect == null)
            {
                return;
            }

            // Freeze at the last valid position while the pointer is over dead screen space.
            if (constrainToDragZones && DragZone.HasAny &&
                !DragZone.AnyContains(eventData.position, GetEventCamera()))
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect, eventData.position, GetEventCamera(), out Vector2 pointerLocal))
            {
                _rectTransform.anchoredPosition = pointerLocal + _grabOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging)
            {
                return;
            }

            IsDragging = false;
            DragEnded?.Invoke(this);

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
            }

            if (returnToStartOnDrop)
            {
                _rectTransform.anchoredPosition = _startAnchoredPosition;
                _rectTransform.SetSiblingIndex(_startSiblingIndex);
            }
        }

        /// <summary>Snaps the element back to where the last drag started (e.g. on an invalid drop).</summary>
        public void ResetToStart()
        {
            _rectTransform.anchoredPosition = _startAnchoredPosition;
            _rectTransform.SetSiblingIndex(_startSiblingIndex);
        }

        /// <summary>
        /// The camera to use when converting screen points to local UI space:
        /// null for Screen Space - Overlay canvases, the canvas camera otherwise.
        /// </summary>
        private Camera GetEventCamera()
        {
            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return _canvas != null ? _canvas.worldCamera : null;
        }
    }
}
