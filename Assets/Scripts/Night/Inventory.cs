using System;
using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// The player's permanent collection of dice and effects. Buying in the shop adds here. The deck
    /// (equipped) is a separate, capped subset chosen from this. Scene singleton.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory Instance { get; private set; }

        private readonly List<DiceData> _dice = new List<DiceData>();
        private readonly List<EffectData> _effects = new List<EffectData>();
        private readonly List<BoxData> _boxes = new List<BoxData>();

        public IReadOnlyList<DiceData> Dice => _dice;
        public IReadOnlyList<EffectData> Effects => _effects;

        /// <summary>Unopened boxes (won boxes that the player opens later from the inventory).</summary>
        public IReadOnlyList<BoxData> Boxes => _boxes;

        /// <summary>Raised whenever the collection changes.</summary>
        public event Action Changed;

        /// <summary>Raised only when an item is added (not on removal) — drives the inventory "NEW!" badge.</summary>
        public event Action ItemAdded;

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

        public void AddDie(DiceData die)
        {
            if (die == null) return;
            _dice.Add(die);
            if (Knowledge.Instance != null) Knowledge.Instance.LearnDie(die); // glossary memory
            if (Deck.Instance != null) Deck.Instance.AddDie(die); // equip straight away if there's a free slot
            Changed?.Invoke();
            ItemAdded?.Invoke();
        }

        public void AddEffect(EffectData effect)
        {
            if (effect == null) return;
            _effects.Add(effect);
            if (Knowledge.Instance != null) Knowledge.Instance.LearnEffect(effect); // glossary memory
            if (EffectDeck.Instance != null) EffectDeck.Instance.AddEffect(effect); // equip straight away if there's a free slot
            Changed?.Invoke();
            ItemAdded?.Invoke();
        }

        /// <summary>Adds an unopened box (won as a reward; opened later from the inventory).</summary>
        public void AddBox(BoxData box)
        {
            if (box == null) return;
            _boxes.Add(box);
            Changed?.Invoke();
            ItemAdded?.Invoke();
        }

        /// <summary>Removes a box (e.g. once it's opened).</summary>
        public void RemoveBox(BoxData box)
        {
            if (box != null && _boxes.Remove(box)) Changed?.Invoke();
        }

        /// <summary>Removes one effect (e.g. a spent Común/Rara effect destroyed after a combat/tournament).</summary>
        public void RemoveEffect(EffectData effect)
        {
            if (effect != null && _effects.Remove(effect)) Changed?.Invoke();
        }

        /// <summary>Removes one die (e.g. a tournament defeat penalty).</summary>
        public void RemoveDie(DiceData die)
        {
            if (die != null && _dice.Remove(die)) Changed?.Invoke();
        }

        /// <summary>Debug: empties the whole collection (dice, effects and boxes).</summary>
        public void Clear()
        {
            _dice.Clear();
            _effects.Clear();
            _boxes.Clear();
            Changed?.Invoke();
        }
    }
}
