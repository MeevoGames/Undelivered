using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The deck: the dice currently in use. Holds between <see cref="MinSlots"/> (2, the start) and
    /// <see cref="MaxSlots"/> (6, via paid upgrades) dice, shown centred with a fixed gap between them.
    /// </summary>
    public class Deck : MonoBehaviour
    {
        public const int MinSlots = 2;
        public const int MaxSlots = 6;

        public static Deck Instance { get; private set; }

        [Tooltip("Dice in the deck at the start (for testing / a saved deck).")]
        [SerializeField] private List<DiceData> startingDice = new List<DiceData>();

        [Tooltip("Container the dice are listed under (a centred HorizontalLayoutGroup is applied).")]
        [SerializeField] private RectTransform container;
        [SerializeField] private DieView dieViewPrefab;

        [Tooltip("Pixels between dice.")]
        [SerializeField] private float spacing = 30f;

        [Tooltip("Height of each die in the deck (px).")]
        [SerializeField] private float dieHeight = 130f;

        [Tooltip("How many dice fit in the deck right now (2 at the start, up to 6).")]
        [SerializeField] private int slots = MinSlots;

        [Tooltip("Interim: auto-refresh the deck when it runs out. Turn this off once the combat engine drives turns via BeginTurn().")]
        [SerializeField] private bool autoRefreshWhenEmpty = true;
        [SerializeField] private float refreshDelay = 0.5f;

        private readonly List<DiceData> _dice = new List<DiceData>();
        private readonly List<DieView> _views = new List<DieView>();

        /// <summary>How many dice fit in the deck right now.</summary>
        public int Slots => slots;

        /// <summary>The dice currently in the deck.</summary>
        public IReadOnlyList<DiceData> Dice => _dice;

        /// <summary>When false, taps don't throw (e.g. during the opponent's turn). Set by the combat controller.</summary>
        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            slots = Mathf.Clamp(slots, MinSlots, MaxSlots);
            foreach (DiceData die in startingDice)
            {
                if (_dice.Count >= slots) break;
                if (die != null) _dice.Add(die);
            }

            ConfigureLayout();
            Rebuild();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Adds a die if there's a free slot. Returns false if the deck is full.</summary>
        public bool AddDie(DiceData die)
        {
            if (die == null || _dice.Count >= slots) return false;

            _dice.Add(die);
            Rebuild();
            return true;
        }

        /// <summary>Replaces the equipped dice, in order, clamped to <see cref="Slots"/>.</summary>
        public void SetDice(System.Collections.Generic.IEnumerable<DiceData> dice)
        {
            _dice.Clear();
            if (dice != null)
            {
                foreach (DiceData die in dice)
                {
                    if (die == null) continue;
                    if (_dice.Count >= slots) break;
                    _dice.Add(die);
                }
            }
            Rebuild();
        }

        /// <summary>Removes a die from the deck.</summary>
        public void RemoveDie(DiceData die)
        {
            if (die != null && _dice.Remove(die)) Rebuild();
        }

        /// <summary>Buys one more slot (paid upgrade). Returns false if already at <see cref="MaxSlots"/>.</summary>
        public bool UpgradeSlots()
        {
            if (slots >= MaxSlots) return false;

            slots++;
            return true;
        }

        /// <summary>True while at least one die hasn't been thrown this deck cycle.</summary>
        public bool HasAvailableDie
        {
            get
            {
                foreach (DieView view in _views)
                    if (view != null && !view.Spent) return true;
                return false;
            }
        }

        /// <summary>
        /// Combat engine: call at the start of the player's turn. If no die is left to throw, the deck
        /// refreshes automatically — every die becomes usable again.
        /// </summary>
        public void BeginTurn()
        {
            if (!HasAvailableDie) RefreshDeck();
        }

        /// <summary>Makes every die usable again (auto on an empty turn, or via a future refresh effect).</summary>
        public void RefreshDeck()
        {
            foreach (DieView view in _views)
                if (view != null) view.SetSpent(false);
        }

        // A tap throws the die (if it hasn't been used yet) and marks it spent for this deck cycle.
        private void TryThrow(DieView view, DiceData die)
        {
            if (view == null || view.Spent || !InputEnabled) return;

            view.SetSpent(true);
            if (DiceThrower.Instance != null) DiceThrower.Instance.Throw(die);

            // Until the combat engine drives turns (BeginTurn), refresh automatically when the deck empties.
            if (autoRefreshWhenEmpty && !HasAvailableDie) StartCoroutine(RefreshAfterDelay());
        }

        private IEnumerator RefreshAfterDelay()
        {
            yield return new WaitForSecondsRealtime(refreshDelay);
            RefreshDeck();
        }

        /// <summary>Rebuilds the deck's die views.</summary>
        public void Rebuild()
        {
            if (container == null || dieViewPrefab == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
            _views.Clear();

            foreach (DiceData die in _dice)
            {
                if (die == null) continue;
                DieView view = Instantiate(dieViewPrefab, container, false);
                view.Setup(die);
                view.SetClick(() => TryThrow(view, die));
                if (view.transform is RectTransform rect)
                {
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dieHeight);
                }
                _views.Add(view);
            }
        }

        // Centres the dice horizontally with a fixed gap between them.
        private void ConfigureLayout()
        {
            if (container == null) return;

            HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
    }
}
