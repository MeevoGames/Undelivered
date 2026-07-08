using System.Collections.Generic;
using Undelivered.UI;
using UnityEngine;

namespace Undelivered.Work
{
    /// <summary>
    /// "Montacargas" upgrade. With the right mouse button you carry several boxes at once:
    ///   • right-click a box on the table → pick it up (up to <see cref="Capacity"/>);
    ///   • right-click a destination slot → deliver every carried box there (classified);
    ///   • right-click the empty table → drop every carried box back onto it;
    ///   • right-click a stacked box → return that box and every box above it to the table.
    /// Left-click (enlarged view) and hold-drag (the old drag system) are untouched.
    ///
    /// Carried boxes are re-parented to <see cref="stackArea"/> (off the table, to the left) and
    /// stacked upward. Scene singleton so the box/slot/table click handlers can reach it.
    /// </summary>
    public class Forklift : MonoBehaviour
    {
        public static Forklift Instance { get; private set; }

        public static bool Enabled;      // set by the Montacargas upgrade
        public static int Capacity = 3;  // boxes carried at once (per level)

        [Tooltip("Table (RectTransform) boxes are dropped back onto.")]
        [SerializeField] private RectTransform table;

        [Tooltip("Stack area off the table (to the left) where carried boxes are shown, stacked upward.")]
        [SerializeField] private RectTransform stackArea;

        [Tooltip("Vertical spacing (pixels) between stacked boxes.")]
        [SerializeField] private float stackSpacing = 40f;

        private readonly List<Box> _hand = new List<Box>();

        public bool HasBoxes => _hand.Count > 0;

        /// <summary>True while the forklift is carrying this box, so robotic arms leave it alone.</summary>
        public static bool IsCarried(Box box)
        {
            return Instance != null && box != null && Instance._hand.Contains(box);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Right-click on a box: pick it up, or (if already carried) return it and those above it.</summary>
        public void OnBoxClicked(Box box)
        {
            if (!Enabled || box == null) return;

            int index = _hand.IndexOf(box);
            if (index >= 0)
            {
                ReturnFrom(index);
            }
            else
            {
                PickUp(box);
            }
        }

        private void PickUp(Box box)
        {
            if (_hand.Count >= Capacity) return; // hands full

            _hand.Add(box);
            SetDraggable(box, false); // can't drag a stacked box; right-click returns it
            Restack();
        }

        // Returns the box at index and everything stacked above it (higher indices) to the table.
        private void ReturnFrom(int index)
        {
            for (int i = _hand.Count - 1; i >= index; i--)
            {
                ReturnToTable(_hand[i]);
                _hand.RemoveAt(i);
            }
            Restack();
        }

        /// <summary>Right-click on a slot: deliver (classify) every carried box there.</summary>
        public void DeliverAllTo(Slot slot)
        {
            if (slot == null) return;

            List<Box> boxes = new List<Box>(_hand); // Deliver destroys boxes, so iterate a copy
            _hand.Clear();
            foreach (Box b in boxes)
            {
                if (b != null) slot.Deliver(b);
            }
        }

        /// <summary>Right-click on the empty table: drop every carried box back onto it.</summary>
        public void DropAll()
        {
            List<Box> boxes = new List<Box>(_hand);
            _hand.Clear();
            foreach (Box b in boxes)
            {
                ReturnToTable(b);
            }
        }

        private void ReturnToTable(Box box)
        {
            if (box == null) return;

            if (box.transform is RectTransform rect && table != null)
            {
                rect.SetParent(table, false);
                rect.anchoredPosition = RandomTablePosition(rect);
            }
            SetDraggable(box, true);
        }

        // Lays out the carried boxes in the stack area, bottom to top.
        private void Restack()
        {
            for (int i = 0; i < _hand.Count; i++)
            {
                Box box = _hand[i];
                if (box == null || !(box.transform is RectTransform rect)) continue;

                if (stackArea != null) rect.SetParent(stackArea, false);
                rect.anchoredPosition = new Vector2(0f, i * stackSpacing);
            }
        }

        private static void SetDraggable(Box box, bool value)
        {
            if (box == null) return;
            UIDraggable drag = box.GetComponent<UIDraggable>();
            if (drag != null) drag.enabled = value;
        }

        private Vector2 RandomTablePosition(RectTransform boxRect)
        {
            if (table == null || boxRect == null) return Vector2.zero;

            Vector2 tableSize = table.rect.size;
            Vector2 boxSize = boxRect.rect.size;
            float maxX = Mathf.Max(0f, (tableSize.x - boxSize.x) * 0.5f);
            float maxY = Mathf.Max(0f, (tableSize.y - boxSize.y) * 0.5f);
            return new Vector2(Random.Range(-maxX, maxX), Random.Range(-maxY, maxY));
        }
    }
}
