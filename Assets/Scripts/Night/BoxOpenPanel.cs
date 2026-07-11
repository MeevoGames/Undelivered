using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The open-a-box UI. It bounces up from off-screen (y = <see cref="offscreenY"/>) to the centre,
    /// the big box does two squash-and-stretch jumps (the second stronger), then opens: the drawn items
    /// appear in slots, each with a shake. In a pick box you tap one (the rest disable); in a single
    /// box you press CLAIM. Then the whole UI slides back down and is cleared, ready for the next box.
    /// Reusable scene singleton.
    /// </summary>
    public class BoxOpenPanel : MonoBehaviour
    {
        public static BoxOpenPanel Instance { get; private set; }

        [Header("Pieces")]
        [Tooltip("The whole UI (slides on Y).")]
        [SerializeField] private RectTransform panel;
        [Tooltip("The big box that jumps and then disappears when it opens.")]
        [SerializeField] private RectTransform boxImage;
        [Tooltip("Up to 3 slot containers (each with a Button for picking). Index 1 is the centred one.")]
        [SerializeField] private Transform[] slots = new Transform[3];
        [Tooltip("CLAIM button, shown only for single-item boxes.")]
        [SerializeField] private GameObject claimButton;

        [Header("Item prefabs (display only, no price)")]
        [SerializeField] private ItemView diceItemPrefab;
        [SerializeField] private EffectView effectItemPrefab;

        [Header("Panel motion")]
        [SerializeField] private float offscreenY = -2000f;
        [SerializeField] private float overshootY = 30f;
        [SerializeField] private float speed = 4000f;
        [SerializeField] private float minSegment = 0.05f;

        [Header("Box jumps (squash & stretch)")]
        [SerializeField] private float jumpHeight1 = 60f;
        [SerializeField] private float jumpSquash1 = 0.15f;
        [SerializeField] private float jumpHeight2 = 110f;
        [SerializeField] private float jumpSquash2 = 0.28f;
        [SerializeField] private float jumpDuration = 0.6f;

        [Header("Slot shake")]
        [SerializeField] private float shakeDuration = 0.5f;
        [SerializeField] private float shakeMagnitude = 12f;

        private readonly List<Button> _pickButtons = new List<Button>();
        private IItem[] _items;
        private bool _busy;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (panel != null) SetY(panel, offscreenY);
            ClearSlots();
            if (claimButton != null)
            {
                Button b = claimButton.GetComponent<Button>();
                if (b != null) b.onClick.AddListener(Claim);
                claimButton.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Opens the box with the given drawn items (ignored while already opening one).</summary>
        public void Show(BoxData box, List<IItem> items)
        {
            if (_busy || box == null || items == null) return;
            _items = items.ToArray();
            StartCoroutine(Sequence(box));
        }

        private IEnumerator Sequence(BoxData box)
        {
            _busy = true;

            ClearSlots();
            if (claimButton != null) claimButton.SetActive(false);
            if (boxImage != null)
            {
                boxImage.gameObject.SetActive(true);
                boxImage.localScale = Vector3.one;
            }
            if (panel != null) SetY(panel, offscreenY);

            // Bounce up to the centre.
            yield return MoveYThrough(panel, overshootY, 0f);

            // Two squash-and-stretch jumps, the second stronger.
            yield return Jump(boxImage, jumpHeight1, jumpSquash1);
            yield return Jump(boxImage, jumpHeight2, jumpSquash2);

            // Open: the box vanishes and the slots appear.
            if (boxImage != null) boxImage.gameObject.SetActive(false);
            RevealSlots(box);

            _busy = false; // picking / claiming is event-driven from here
        }

        private void RevealSlots(BoxData box)
        {
            bool single = box.DrawCount <= 1;
            if (single)
            {
                ActivateSlot(1, First(_items), pickable: false); // centre slot
                if (claimButton != null) claimButton.SetActive(true);
            }
            else
            {
                int count = Mathf.Min(_items.Length, slots.Length);
                for (int i = 0; i < count; i++) ActivateSlot(i, _items[i], pickable: true);
            }
        }

        private void ActivateSlot(int i, IItem item, bool pickable)
        {
            if (i < 0 || i >= slots.Length || slots[i] == null || item == null) return;

            Transform slot = slots[i];
            slot.gameObject.SetActive(true);

            GameObject go = null;
            if (item is DiceData die && diceItemPrefab != null)
            {
                ItemView v = Instantiate(diceItemPrefab, slot, false); v.Setup(die); go = v.gameObject;
            }
            else if (item is EffectData effect && effectItemPrefab != null)
            {
                EffectView v = Instantiate(effectItemPrefab, slot, false); v.Setup(effect); go = v.gameObject;
            }

            if (pickable)
            {
                // The click goes to the item's Button (it's on top); fall back to the slot's.
                Button btn = go != null ? go.GetComponent<Button>() : null;
                if (btn == null) btn = slot.GetComponent<Button>();
                if (btn != null)
                {
                    int index = i;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => Pick(index));
                    btn.interactable = true;
                    _pickButtons.Add(btn);
                }
                else
                {
                    Debug.LogWarning("BoxOpenPanel: el ítem/slot no tiene Button para seleccionar; " +
                                     "agregá un Button al prefab del ítem (o al slot).", this);
                }
            }

            if (slot is RectTransform rect) StartCoroutine(Shake(rect));
        }

        // Pick one of the drawn items; the rest disable so only one can be taken.
        private void Pick(int i)
        {
            if (_items != null && i >= 0 && i < _items.Length) Grant(_items[i]);
            DisableSlotButtons();
            StartCoroutine(Close());
        }

        /// <summary>Claim button for single-item boxes.</summary>
        public void Claim()
        {
            Grant(First(_items));
            StartCoroutine(Close());
        }

        private IEnumerator Close()
        {
            if (claimButton != null) claimButton.SetActive(false);
            yield return MoveYThrough(panel, offscreenY);
            ClearSlots();
        }

        private static void Grant(IItem item)
        {
            if (item is DiceData die) Inventory.Instance?.AddDie(die);
            else if (item is EffectData effect) Inventory.Instance?.AddEffect(effect);
        }

        private void DisableSlotButtons()
        {
            foreach (Button btn in _pickButtons) if (btn != null) btn.interactable = false;
        }

        private void ClearSlots()
        {
            _pickButtons.Clear();
            foreach (Transform slot in slots)
            {
                if (slot == null) continue;
                for (int c = slot.childCount - 1; c >= 0; c--) Destroy(slot.GetChild(c).gameObject);
                slot.gameObject.SetActive(false);
            }
        }

        private static IItem First(IItem[] items) => items != null && items.Length > 0 ? items[0] : null;

        // ----- motion helpers -----

        private IEnumerator MoveYThrough(RectTransform rect, params float[] ys)
        {
            if (rect == null) yield break;
            foreach (float target in ys)
            {
                Vector2 start = rect.anchoredPosition;
                float distance = Mathf.Abs(target - start.y);
                float duration = Mathf.Max(minSegment, distance / Mathf.Max(1f, speed));
                for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, e / duration);
                    rect.anchoredPosition = new Vector2(start.x, Mathf.Lerp(start.y, target, k));
                    yield return null;
                }
                rect.anchoredPosition = new Vector2(start.x, target);
            }
        }

        private IEnumerator Jump(RectTransform rect, float height, float squash)
        {
            if (rect == null) yield break;

            Vector2 home = rect.anchoredPosition;
            for (float e = 0f; e < jumpDuration; e += Time.unscaledDeltaTime)
            {
                float airborne = Mathf.Sin((e / jumpDuration) * Mathf.PI); // 0 on the ground, 1 at the apex
                rect.anchoredPosition = home + Vector2.up * (airborne * height);
                float sy = Mathf.Lerp(1f - squash, 1f + squash, airborne); // squash landing, stretch in air
                float sx = Mathf.Lerp(1f + squash, 1f - squash, airborne);
                rect.localScale = new Vector3(sx, sy, 1f);
                yield return null;
            }
            rect.anchoredPosition = home;
            rect.localScale = Vector3.one;
        }

        private IEnumerator Shake(RectTransform rect)
        {
            Vector3 home = rect.localPosition;
            for (float e = 0f; e < shakeDuration; e += Time.unscaledDeltaTime)
            {
                float damper = 1f - (e / shakeDuration);
                rect.localPosition = home + (Vector3)(Random.insideUnitCircle * (shakeMagnitude * damper));
                yield return null;
            }
            rect.localPosition = home;
        }

        private static void SetY(RectTransform rect, float y)
        {
            Vector2 p = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(p.x, y);
        }
    }
}
