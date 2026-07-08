using TMPro;
using UnityEngine;

namespace Undelivered.UI
{
    /// <summary>Which corner of the cursor the tooltip window is placed at.</summary>
    public enum TooltipDirection { TL, TR, BL, BR }

    /// <summary>
    /// Master for the tooltip system. Holds the (already-in-scene) window and its text, positions the
    /// window at the requested corner of the cursor (a minimum distance away) and follows the cursor
    /// while shown, then teleports the window off-screen when hidden. Triggers call it via the singleton.
    /// </summary>
    public class TooltipManager : MonoBehaviour
    {
        public static TooltipManager Instance { get; private set; }

        [Tooltip("The tooltip window (already in the scene).")]
        [SerializeField] private RectTransform window;

        [Tooltip("Text inside the window that shows the message.")]
        [SerializeField] private TextMeshProUGUI messageText;

        [Tooltip("Anchored position the window is parked at while hidden (off-screen).")]
        [SerializeField] private Vector2 hiddenPosition = new Vector2(100000f, 100000f);

        [Tooltip("Minimum gap (pixels) between the cursor and the window, in the chosen direction.")]
        [SerializeField] private float minDistance = 16f;

        private RectTransform _parent;
        private Canvas _canvas;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (window != null)
            {
                _parent = window.parent as RectTransform;
                _canvas = window.GetComponentInParent<Canvas>();
            }
            Hide(); // start parked off-screen
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Shows the window with the given message at the requested corner of the cursor.</summary>
        public void Show(string message, TooltipDirection direction, Vector2 screenPosition)
        {
            if (window == null)
            {
                return;
            }

            if (messageText != null)
            {
                messageText.text = message;
            }
            PositionWindow(direction, screenPosition);
        }

        /// <summary>Repositions the window at the cursor (used to follow the mouse while hovering).</summary>
        public void UpdatePosition(TooltipDirection direction, Vector2 screenPosition)
        {
            if (window != null)
            {
                PositionWindow(direction, screenPosition);
            }
        }

        /// <summary>Teleports the window off-screen.</summary>
        public void Hide()
        {
            if (window != null)
            {
                window.anchoredPosition = hiddenPosition;
            }
        }

        private void PositionWindow(TooltipDirection direction, Vector2 screenPosition)
        {
            window.pivot = PivotFor(direction);
            PositionAtScreen(screenPosition + OffsetFor(direction) * minDistance);
        }

        private void PositionAtScreen(Vector2 screenPosition)
        {
            if (_parent == null)
            {
                window.position = screenPosition;
                return;
            }

            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_parent, screenPosition, cam, out Vector3 world))
            {
                window.position = world;
            }
        }

        // The pivot is the window corner placed at the cursor, so the window extends in the direction.
        private static Vector2 PivotFor(TooltipDirection direction)
        {
            switch (direction)
            {
                case TooltipDirection.TL: return new Vector2(1f, 0f);
                case TooltipDirection.TR: return new Vector2(0f, 0f);
                case TooltipDirection.BL: return new Vector2(1f, 1f);
                case TooltipDirection.BR: return new Vector2(0f, 1f);
                default: return new Vector2(0f, 0f);
            }
        }

        // The direction the window is nudged away from the cursor (keeps the minimum gap).
        private static Vector2 OffsetFor(TooltipDirection direction)
        {
            switch (direction)
            {
                case TooltipDirection.TL: return new Vector2(-1f, 1f);
                case TooltipDirection.TR: return new Vector2(1f, 1f);
                case TooltipDirection.BL: return new Vector2(-1f, -1f);
                case TooltipDirection.BR: return new Vector2(1f, -1f);
                default: return Vector2.zero;
            }
        }
    }
}
