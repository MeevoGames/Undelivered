using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.UI
{
    /// <summary>Which corner of the cursor the tooltip window is placed at.</summary>
    public enum TooltipDirection { TL, TR, BL, BR }

    /// <summary>The tooltip layout used, by the kind of element being hovered.</summary>
    public enum TooltipKind { Basic, General, Dice, Effect }

    /// <summary>
    /// Master for the tooltip system. Holds one window per <see cref="TooltipKind"/> (already in the scene):
    /// Basic (description), General (title + description), Dice (title + description + 6 face images) and
    /// Effect (title + description + a duration line). Shows the matching one at the cursor and follows it
    /// while hovering; parks all of them off-screen when hidden. Triggers call it via the singleton.
    /// </summary>
    public class TooltipManager : MonoBehaviour
    {
        public static TooltipManager Instance { get; private set; }

        [Header("Basic (description only)")]
        [SerializeField] private RectTransform window;
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("General (title + description)")]
        [SerializeField] private RectTransform generalWindow;
        [SerializeField] private TextMeshProUGUI generalTitle;
        [SerializeField] private TextMeshProUGUI generalDescription;

        [Header("Dice (title + description + 6 faces)")]
        [SerializeField] private RectTransform diceWindow;
        [SerializeField] private TextMeshProUGUI diceTitle;
        [SerializeField] private TextMeshProUGUI diceDescription;
        [Tooltip("The 6 face images, in face order.")]
        [SerializeField] private Image[] diceFaces = new Image[6];

        [Header("Effect (title + description + duration)")]
        [SerializeField] private RectTransform effectWindow;
        [SerializeField] private TextMeshProUGUI effectTitle;
        [SerializeField] private TextMeshProUGUI effectDescription;
        [SerializeField] private TextMeshProUGUI effectDuration;

        [Tooltip("Anchored position the windows are parked at while hidden (off-screen).")]
        [SerializeField] private Vector2 hiddenPosition = new Vector2(100000f, 100000f);
        [Tooltip("Minimum gap (pixels) between the cursor and the window, in the chosen direction.")]
        [SerializeField] private float minDistance = 16f;

        private RectTransform _active;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Hide(); // all parked off-screen
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Basic: just a description.</summary>
        public void ShowBasic(string description, TooltipDirection direction, Vector2 screenPosition)
        {
            HideAll();
            SetText(messageText, description);
            Activate(window, direction, screenPosition);
        }

        /// <summary>General: a title and a description (e.g. an enemy's name + description).</summary>
        public void ShowGeneral(string title, string description, TooltipDirection direction, Vector2 screenPosition)
        {
            if (generalWindow == null) { ShowBasic(Combine(title, description), direction, screenPosition); return; }
            HideAll();
            SetText(generalTitle, title);
            SetText(generalDescription, description);
            Activate(generalWindow, direction, screenPosition);
        }

        /// <summary>Dice: a title, a description and the die's 6 face sprites.</summary>
        public void ShowDice(string title, string description, Sprite[] faces, TooltipDirection direction, Vector2 screenPosition)
        {
            if (diceWindow == null) { ShowBasic(Combine(title, description), direction, screenPosition); return; }
            HideAll();
            SetText(diceTitle, title);
            SetText(diceDescription, description);
            if (diceFaces != null)
            {
                for (int i = 0; i < diceFaces.Length; i++)
                {
                    if (diceFaces[i] == null) continue;
                    Sprite s = faces != null && i < faces.Length ? faces[i] : null;
                    diceFaces[i].sprite = s;
                    diceFaces[i].enabled = s != null;
                }
            }
            Activate(diceWindow, direction, screenPosition);
        }

        /// <summary>Effect: a title, a description and a duration line (how long it lasts / when it's lost).</summary>
        public void ShowEffect(string title, string description, string duration, TooltipDirection direction, Vector2 screenPosition)
        {
            if (effectWindow == null) { ShowBasic(Combine(Combine(title, description), duration), direction, screenPosition); return; }
            HideAll();
            SetText(effectTitle, title);
            SetText(effectDescription, description);
            SetText(effectDuration, duration);
            Activate(effectWindow, direction, screenPosition);
        }

        /// <summary>Repositions the active window at the cursor (used to follow the mouse while hovering).</summary>
        public void UpdatePosition(TooltipDirection direction, Vector2 screenPosition)
        {
            if (_active != null) Position(direction, screenPosition);
        }

        /// <summary>Parks every window off-screen.</summary>
        public void Hide()
        {
            HideAll();
            _active = null;
        }

        private void HideAll()
        {
            Park(window);
            Park(generalWindow);
            Park(diceWindow);
            Park(effectWindow);
        }

        private void Park(RectTransform w)
        {
            if (w != null) w.anchoredPosition = hiddenPosition;
        }

        // Makes a window active, sizes it to its content THIS frame (so it isn't shown at the previous
        // tooltip's size while a VerticalLayoutGroup/ContentSizeFitter catch up), then places it.
        private void Activate(RectTransform w, TooltipDirection direction, Vector2 screenPosition)
        {
            _active = w;
            if (w != null) LayoutRebuilder.ForceRebuildLayoutImmediate(w);
            Position(direction, screenPosition);
        }

        private void Position(TooltipDirection direction, Vector2 screenPosition)
        {
            if (_active == null) return;
            _active.pivot = PivotFor(direction);
            PositionAtScreen(_active, screenPosition + OffsetFor(direction) * minDistance);
        }

        private void PositionAtScreen(RectTransform w, Vector2 screenPosition)
        {
            RectTransform parent = w.parent as RectTransform;
            if (parent == null) { w.position = screenPosition; return; }

            Canvas canvas = w.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPosition, cam, out Vector3 world))
                w.position = world;
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null) text.text = value;
        }

        // Joins two lines, skipping empties (used for the basic-window fallback).
        private static string Combine(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a)) return b;
            if (string.IsNullOrWhiteSpace(b)) return a;
            return a + "\n" + b;
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
