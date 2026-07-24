using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// The effects the player brings to a match, played as a card deck.
    ///
    /// From the inventory they pick a <b>library</b> of <see cref="LibrarySlots"/> effects. Each one is
    /// <see cref="CopiesPerEffect"/>×duplicated into the draw pile, so a match is played with 24 cards out
    /// of 12 distinct effects. A combat opens by dealing <see cref="OpeningDraw"/>, and every round deals
    /// <see cref="RoundDraw"/> more, never going past <see cref="HandLimit"/> in hand — at the cap nothing
    /// is drawn until cards are spent.
    ///
    /// Using an effect removes it from the hand and sends it to the discard pile. When the draw pile runs
    /// out the discard is shuffled back in, and a Renewal effect can force that recycle early.
    /// </summary>
    public class EffectDeck : MonoBehaviour
    {
        /// <summary>Distinct effects the player equips from the inventory.</summary>
        public const int LibrarySlots = 12;
        /// <summary>Copies of each equipped effect in the draw pile (12 × 2 = 24 cards).</summary>
        public const int CopiesPerEffect = 2;
        /// <summary>Most effects that can be held at once.</summary>
        public const int HandLimit = 12;
        /// <summary>Dealt when a combat starts.</summary>
        public const int OpeningDraw = 6;
        /// <summary>Dealt at the start of every round.</summary>
        public const int RoundDraw = 3;

        public static EffectDeck Instance { get; private set; }

        [Tooltip("Effects equipped at the start (for testing).")]
        [SerializeField] private List<EffectData> startingEffects = new List<EffectData>();

        [Tooltip("Container the effects in hand are listed under (the effectPlayer object).")]
        [SerializeField] private RectTransform container;
        [SerializeField] private EffectView effectViewPrefab;
        [Tooltip("Shows how many cards are left in the draw pile (updates when the discard is recycled in).")]
        [SerializeField] private TMP_Text drawCountText;

        private readonly List<EffectData> _library = new List<EffectData>();  // the equipped 12
        private readonly List<EffectData> _drawPile = new List<EffectData>(); // shuffled, 24 at full
        private readonly List<EffectData> _discard = new List<EffectData>();  // spent cards
        private readonly List<EffectView> _hand = new List<EffectView>();     // what's on screen
        private readonly List<EffectView> _selected = new List<EffectView>();

        /// <summary>The equipped-effects cap.</summary>
        public int MaxSlots => LibrarySlots;

        /// <summary>The effects currently equipped (the library the draw pile is built from).</summary>
        public IReadOnlyList<EffectData> Effects => _library;

        public int DrawPileCount => _drawPile.Count;
        public int DiscardCount => _discard.Count;
        public int HandCount => _hand.Count;

        /// <summary>When false, effects can't be activated (before "Comenzar" and during the opponent's turn).</summary>
        public bool InputEnabled { get; set; } = false;

        /// <summary>The effects activated for the next throw.</summary>
        public IEnumerable<EffectData> SelectedEffects
        {
            get
            {
                foreach (EffectView view in _selected)
                    if (view != null && view.Effect != null) yield return view.Effect;
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

            foreach (EffectData effect in startingEffects)
            {
                if (_library.Count >= LibrarySlots) break;
                if (effect != null) _library.Add(effect);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ----- the equipped library -----

        /// <summary>Equips an effect if there's a free slot. Returns false if full.</summary>
        public bool AddEffect(EffectData effect)
        {
            if (effect == null || _library.Count >= LibrarySlots) return false;
            _library.Add(effect);
            return true;
        }

        /// <summary>Replaces the equipped effects, in order, clamped to <see cref="LibrarySlots"/>.</summary>
        public void SetEffects(IEnumerable<EffectData> effects)
        {
            _library.Clear();
            if (effects != null)
            {
                foreach (EffectData effect in effects)
                {
                    if (effect == null) continue;
                    if (_library.Count >= LibrarySlots) break;
                    _library.Add(effect);
                }
            }
        }

        /// <summary>Unequips an effect.</summary>
        public void RemoveEffect(EffectData effect)
        {
            if (effect != null) _library.Remove(effect);
        }

        // ----- the match -----

        /// <summary>
        /// Combat engine: shuffles a fresh draw pile from the library (each effect duplicated), empties the
        /// discard and the hand, and deals the opening hand.
        /// </summary>
        public void StartCombat()
        {
            _drawPile.Clear();
            _discard.Clear();
            ClearHand();

            foreach (EffectData effect in _library)
            {
                if (effect == null) continue;
                for (int copy = 0; copy < CopiesPerEffect; copy++) _drawPile.Add(effect);
            }
            Shuffle(_drawPile);
            UpdateDrawCount();

            Draw(OpeningDraw);
        }

        /// <summary>Combat engine: deals this round's effects (nothing if the hand is already full).</summary>
        public void DrawForRound() => Draw(RoundDraw);

        /// <summary>
        /// Draws up to <paramref name="count"/> effects into the hand, stopping at <see cref="HandLimit"/>.
        /// If the draw pile empties mid-deal the discard is shuffled back in and the deal continues.
        /// </summary>
        public void Draw(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_hand.Count >= HandLimit) return;      // at the cap nothing more is dealt
                if (_drawPile.Count == 0) RecycleDiscard();
                if (_drawPile.Count == 0) return;          // library empty: nothing left anywhere

                EffectData effect = _drawPile[_drawPile.Count - 1];
                _drawPile.RemoveAt(_drawPile.Count - 1);
                UpdateDrawCount(); // reflects every card drawn, whatever exits the loop
                AddToHand(effect);
            }
            AutoRecycle(); // never leave the counter sitting at 0 while the discard still has cards
        }

        // Reloads the draw pile from the discard the instant it empties, so the counter shows the
        // recycled number rather than 0.
        private void AutoRecycle()
        {
            if (_drawPile.Count == 0 && _discard.Count > 0) RecycleDiscard();
        }

        /// <summary>
        /// Spends the activated effects: each leaves the hand and goes to the discard pile.
        /// </summary>
        public void ConsumeSelected()
        {
            foreach (EffectView view in _selected.ToList())
            {
                if (view == null) continue;
                if (view.Effect != null) _discard.Add(view.Effect);

                _hand.Remove(view);
                view.PlayDiscard(); // shrinks away, then destroys itself (the data already moved to discard)
            }
            _selected.Clear();
            AutoRecycle(); // if the draw pile was empty, the just-spent cards reload it now (counter jumps off 0)
        }

        /// <summary>
        /// Renewal (type 7): shuffles the discard pile back into the draw pile. Also used automatically
        /// whenever the draw pile runs dry.
        /// </summary>
        public void RecycleDiscard()
        {
            if (_discard.Count == 0) return;
            _drawPile.AddRange(_discard);
            _discard.Clear();
            Shuffle(_drawPile);
            UpdateDrawCount();
        }

        /// <summary>New night: drops equipped effects no longer owned (tournament penalties) and resets the match.</summary>
        public void ResetForNewNight()
        {
            if (Inventory.Instance != null)
                _library.RemoveAll(e => e == null || !Inventory.Instance.Effects.Contains(e));

            _drawPile.Clear();
            _discard.Clear();
            ClearHand();
            UpdateDrawCount();
        }

        // ----- hand -----

        private void AddToHand(EffectData effect)
        {
            if (container == null || effectViewPrefab == null || effect == null) return;

            EffectView view = Instantiate(effectViewPrefab, container, false);
            view.Setup(effect);
            view.SetClick(() => ToggleSelect(view));
            view.PlayDraw(); // pops in
            _hand.Add(view);
        }

        private void ClearHand()
        {
            foreach (EffectView view in _hand)
                if (view != null) Destroy(view.gameObject);
            _hand.Clear();
            _selected.Clear();

            // Anything left over from an interrupted animation.
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--) Destroy(container.GetChild(i).gameObject);
        }

        // A tap in the hand toggles the effect activated for the next throw.
        private void ToggleSelect(EffectView view)
        {
            if (view == null || !InputEnabled) return;

            if (view.Selected)
            {
                view.SetSelected(false);
                _selected.Remove(view);
            }
            else
            {
                view.SetSelected(true);
                _selected.Add(view);
            }
        }

        private void UpdateDrawCount()
        {
            if (drawCountText != null) drawCountText.text = _drawPile.Count.ToString();
        }

        private static void Shuffle(List<EffectData> pile)
        {
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }
        }
    }
}
