using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The deck: the dice currently in use. Always holds <see cref="SlotCount"/> (6) dice — the slots are
    /// there from the start and are never upgraded — dealt alternating between the two on-screen groups,
    /// so each side ends up with three.
    /// </summary>
    public class Deck : MonoBehaviour
    {
        /// <summary>How many dice the deck holds. Fixed: there is no slot upgrade.</summary>
        public const int SlotCount = 6;

        public static Deck Instance { get; private set; }

        [Tooltip("Dice in the deck at the start (for testing / a saved deck).")]
        [SerializeField] private List<DiceData> startingDice = new List<DiceData>();

        [Tooltip("Left group of slots — takes the 1st, 3rd and 5th die.")]
        [SerializeField] private RectTransform leftContainer;
        [Tooltip("Right group of slots — takes the 2nd, 4th and 6th die.")]
        [SerializeField] private RectTransform rightContainer;
        [SerializeField] private DieView dieViewPrefab;

        [Tooltip("Pixels between dice.")]
        [SerializeField] private float spacing = 30f;

        [Tooltip("Height of each die in the deck (px).")]
        [SerializeField] private float dieHeight = 130f;

        [Tooltip("The END TURN button — throws the selected die. Enabled only when it's the player's turn and a die is picked.")]
        [SerializeField] private Button endTurnButton;

        [Tooltip("Interim: auto-refresh the deck when it runs out. Turn this off once the combat engine drives turns via BeginTurn().")]
        [SerializeField] private bool autoRefreshWhenEmpty = true;
        [SerializeField] private float refreshDelay = 0.5f;

        private readonly List<DiceData> _dice = new List<DiceData>();
        private readonly List<DieView> _views = new List<DieView>();
        private DieView _selectedDie; // the radio-picked die, thrown on END TURN

        /// <summary>How many dice fit in the deck — always <see cref="SlotCount"/>.</summary>
        public int Slots => SlotCount;

        /// <summary>The dice currently in the deck.</summary>
        public IReadOnlyList<DiceData> Dice => _dice;

        /// <summary>True while the deck has at least one die (needed to start a combat).</summary>
        public bool HasDice => _dice.Count > 0;

        /// <summary>Raised whenever the deck's dice change (rebuild).</summary>
        public event Action Changed;

        private bool _inputEnabled;

        /// <summary>When false, dice can't be picked and END TURN is disabled (before "Comenzar" and during the opponent's turn).</summary>
        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                _inputEnabled = value;
                if (!value) ClearSelection(); // leaving the player's turn drops the radio pick
                UpdateEndTurnButton();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (endTurnButton != null) endTurnButton.onClick.AddListener(() => ThrowSelected());

            foreach (DiceData die in startingDice)
            {
                if (_dice.Count >= SlotCount) break;
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
            if (die == null || _dice.Count >= SlotCount) return false;

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
                    if (_dice.Count >= SlotCount) break;
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

        /// <summary>Makes every die usable again (auto on an empty turn, or a Renewal effect). Locked dice stay spent.</summary>
        public void RefreshDeck()
        {
            foreach (DieView view in _views)
                if (view != null && !view.Locked) view.SetSpent(false);
        }

        /// <summary>Renewal (type 7): un-spends one spent die of this kind (the die just used). Locked dice are skipped.</summary>
        public void RenewDie(DiceData die)
        {
            if (die == null) return;
            foreach (DieView view in _views)
                if (view != null && view.Spent && !view.Locked && view.Data == die) { view.SetSpent(false); return; }
        }

        /// <summary>Counterattack (ToAll): locks one spent die of this kind so no refresh/renew brings it back this combat.</summary>
        public void LockDie(DiceData die)
        {
            if (die == null) return;
            foreach (DieView view in _views)
                if (view != null && view.Spent && !view.Locked && view.Data == die) { view.SetLocked(true); return; }
        }

        /// <summary>Combat engine: at the start of a combat, unlock and un-spend every die (fresh deck).</summary>
        public void StartCombat()
        {
            foreach (DieView view in _views)
                if (view != null) { view.SetLocked(false); view.SetSpent(false); }
        }

        /// <summary>Updates the luck % shown on every die (type 13). <paramref name="boosted"/> tints the text when a luck effect is active.</summary>
        public void RefreshLuck(System.Func<DiceData, int> percentFor, bool boosted)
        {
            if (percentFor == null) return;
            foreach (DieView view in _views)
                if (view != null && view.Data != null) view.SetLuck(percentFor(view.Data), boosted);
        }

        /// <summary>New night: drops equipped dice no longer owned (tournament penalties) and un-spends the rest.</summary>
        public void ResetForNewNight()
        {
            if (Inventory.Instance != null)
                _dice.RemoveAll(d => d == null || !Inventory.Instance.Dice.Contains(d));
            Rebuild(); // fresh, un-spent views
        }

        // A tap picks the die to throw (radio: picking one deselects the rest). Nothing is thrown until
        // the END TURN button is pressed.
        private void SelectDie(DieView view)
        {
            if (view == null || view.Spent || !_inputEnabled) return;

            foreach (DieView v in _views)
                if (v != null) v.SetSelected(v == view);
            _selectedDie = view;
            UpdateEndTurnButton();
        }

        /// <summary>END TURN: throws the picked die (marking it spent). Returns false if nothing is picked.</summary>
        public bool ThrowSelected()
        {
            if (_selectedDie == null || _selectedDie.Spent || !_inputEnabled) return false;

            DiceData die = _selectedDie.Data;
            _selectedDie.SetSelected(false);
            _selectedDie.SetSpent(true);
            _selectedDie = null;

            _inputEnabled = false; // no more input until the next player turn re-enables it
            UpdateEndTurnButton();

            if (DiceThrower.Instance != null) DiceThrower.Instance.Throw(die);

            // Until the combat engine drives turns (BeginTurn), refresh automatically when the deck empties.
            if (autoRefreshWhenEmpty && !HasAvailableDie) StartCoroutine(RefreshAfterDelay());
            return true;
        }

        private void ClearSelection()
        {
            _selectedDie = null;
            foreach (DieView v in _views)
                if (v != null) v.SetSelected(false);
        }

        private void UpdateEndTurnButton()
        {
            if (endTurnButton != null) endTurnButton.interactable = _inputEnabled && _selectedDie != null;
        }

        private IEnumerator RefreshAfterDelay()
        {
            yield return new WaitForSecondsRealtime(refreshDelay);
            RefreshDeck();
        }

        /// <summary>Rebuilds the deck's die views.</summary>
        public void Rebuild()
        {
            if (dieViewPrefab == null) return;

            ClearContainer(leftContainer);
            if (rightContainer != leftContainer) ClearContainer(rightContainer);
            _views.Clear();
            _selectedDie = null; // the old views are gone

            // The dice are dealt alternating sides — left, right, left, right... — so both groups fill
            // evenly instead of one side filling up first.
            for (int i = 0; i < _dice.Count; i++)
            {
                DiceData die = _dice[i];
                if (die == null) continue;

                RectTransform parent = i % 2 == 0 || rightContainer == null ? leftContainer : rightContainer;
                if (parent == null) continue;

                DieView view = Instantiate(dieViewPrefab, parent, false);
                view.Setup(die);
                view.SetClick(() => SelectDie(view));
                if (view.transform is RectTransform rect)
                {
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dieHeight);
                }
                _views.Add(view);
            }

            UpdateEndTurnButton();
            Changed?.Invoke();
        }

        private static void ClearContainer(RectTransform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }

        // Centres the dice horizontally with a fixed gap between them, in each group.
        private void ConfigureLayout()
        {
            ConfigureGroup(leftContainer);
            if (rightContainer != leftContainer) ConfigureGroup(rightContainer);
        }

        private void ConfigureGroup(RectTransform parent)
        {
            if (parent == null) return;

            // Respect a layout group the scene already sets up (a grid, say); never add a second one —
            // Unity forbids it and AddComponent would return null.
            if (parent.GetComponent<LayoutGroup>() != null) return;

            HorizontalLayoutGroup layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
    }
}
