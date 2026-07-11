using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// The effects equipped for a match, listed in the <c>effectPlayer</c> object. It holds as many
    /// effects as the dice deck holds dice: the cap is <see cref="Deck.Slots"/> (2 at the start, up to
    /// 6 with upgrades). Regardless of how many effects the player owns, only this many can be equipped.
    /// </summary>
    public class EffectDeck : MonoBehaviour
    {
        public static EffectDeck Instance { get; private set; }

        [Tooltip("Effects equipped at the start (for testing).")]
        [SerializeField] private List<EffectData> startingEffects = new List<EffectData>();

        [Tooltip("Container the effects are listed under (the effectPlayer object).")]
        [SerializeField] private RectTransform container;
        [SerializeField] private EffectView effectViewPrefab;

        private readonly List<EffectData> _effects = new List<EffectData>();
        private readonly List<EffectView> _views = new List<EffectView>();
        private readonly List<EffectView> _selected = new List<EffectView>();
        private readonly List<EffectData> _removeAfterCombat = new List<EffectData>();     // Común
        private readonly List<EffectData> _removeAfterTournament = new List<EffectData>(); // Rara

        /// <summary>The equipped-effects cap — mirrors the dice deck's slots (2..6).</summary>
        public int MaxSlots => Deck.Instance != null ? Deck.Instance.Slots : Deck.MinSlots;

        /// <summary>The effects currently equipped.</summary>
        public IReadOnlyList<EffectData> Effects => _effects;

        /// <summary>When false, effects can't be activated (e.g. during the opponent's turn). Set by the combat controller.</summary>
        public bool InputEnabled { get; set; } = true;

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
                if (_effects.Count >= MaxSlots) break;
                if (effect != null) _effects.Add(effect);
            }

            Rebuild();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Equips an effect if there's a free slot. Returns false if full.</summary>
        public bool AddEffect(EffectData effect)
        {
            if (effect == null || _effects.Count >= MaxSlots) return false;

            _effects.Add(effect);
            Rebuild();
            return true;
        }

        /// <summary>Replaces the equipped effects, in order, clamped to <see cref="MaxSlots"/>.</summary>
        public void SetEffects(System.Collections.Generic.IEnumerable<EffectData> effects)
        {
            _effects.Clear();
            if (effects != null)
            {
                foreach (EffectData effect in effects)
                {
                    if (effect == null) continue;
                    if (_effects.Count >= MaxSlots) break;
                    _effects.Add(effect);
                }
            }
            Rebuild();
        }

        /// <summary>Unequips an effect.</summary>
        public void RemoveEffect(EffectData effect)
        {
            if (effect != null && _effects.Remove(effect)) Rebuild();
        }

        /// <summary>Rebuilds the effect views.</summary>
        public void Rebuild()
        {
            if (container == null || effectViewPrefab == null) return;

            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
            }
            _views.Clear();
            _selected.Clear();

            foreach (EffectData effect in _effects)
            {
                if (effect == null) continue;
                EffectView view = Instantiate(effectViewPrefab, container, false);
                view.Setup(effect);
                view.SetClick(() => ToggleSelect(view));
                _views.Add(view);
            }
        }

        // A tap in the deck toggles the effect activated for the next throw.
        private void ToggleSelect(EffectView view)
        {
            if (view == null || view.Spent || !InputEnabled) return;

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

        /// <summary>
        /// Spends the activated effects on a die throw. Each becomes spent for the rest of the combat and,
        /// by rarity, is queued for removal: Común after the combat, Rara after the tournament. Épica is
        /// never removed and comes back next combat (via <see cref="StartCombat"/>).
        /// </summary>
        public void ConsumeSelected()
        {
            foreach (EffectView view in _selected)
            {
                if (view == null || view.Effect == null) continue;

                view.SetSpent(true);
                switch (view.Effect.EffectRarity)
                {
                    case EffectData.Rarity.Comun: _removeAfterCombat.Add(view.Effect); break;
                    case EffectData.Rarity.Rara: _removeAfterTournament.Add(view.Effect); break;
                    // Épica: kept; refreshed at the start of the next combat.
                }
            }
            _selected.Clear();
        }

        /// <summary>Combat engine: at the start of a combat, Épica effects become available again.</summary>
        public void StartCombat()
        {
            foreach (EffectView view in _views)
            {
                if (view != null && view.Effect != null && view.Effect.EffectRarity == EffectData.Rarity.Epica)
                    view.SetSpent(false);
            }
        }

        /// <summary>Combat engine: after a combat, destroy the spent Común effects.</summary>
        public void EndCombat() => RemoveQueued(_removeAfterCombat);

        /// <summary>Combat engine: after a tournament, destroy the spent Rara effects.</summary>
        public void EndTournament() => RemoveQueued(_removeAfterTournament);

        private void RemoveQueued(List<EffectData> queued)
        {
            if (queued.Count == 0) return;

            foreach (EffectData effect in queued)
            {
                if (Inventory.Instance != null) Inventory.Instance.RemoveEffect(effect);
                _effects.Remove(effect);
            }
            queued.Clear();
            Rebuild();
        }
    }
}
