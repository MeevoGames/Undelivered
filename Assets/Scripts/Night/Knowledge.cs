using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// The player's memory of what they've encountered: dice/effects they've owned and enemies they've
    /// fought. Feeds the glossary (known items show normally, unknown ones as "???" + a black silhouette).
    /// In-memory for the session (no save file yet).
    /// </summary>
    public class Knowledge : MonoBehaviour
    {
        public static Knowledge Instance { get; private set; }

        private readonly HashSet<DiceData> _dice = new HashSet<DiceData>();
        private readonly HashSet<EffectData> _effects = new HashSet<EffectData>();
        private readonly HashSet<EnemyData> _enemies = new HashSet<EnemyData>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Learn what the player already owns / has equipped at the start.
            if (Inventory.Instance != null)
            {
                foreach (DiceData d in Inventory.Instance.Dice) LearnDie(d);
                foreach (EffectData e in Inventory.Instance.Effects) LearnEffect(e);
            }
            if (Deck.Instance != null) foreach (DiceData d in Deck.Instance.Dice) LearnDie(d);
            if (EffectDeck.Instance != null) foreach (EffectData e in EffectDeck.Instance.Effects) LearnEffect(e);
        }

        public void LearnDie(DiceData die) { if (die != null) _dice.Add(die); }
        public void LearnEffect(EffectData effect) { if (effect != null) _effects.Add(effect); }
        public void LearnEnemy(EnemyData enemy) { if (enemy != null) _enemies.Add(enemy); }

        public bool Knows(DiceData die) => die != null && _dice.Contains(die);
        public bool Knows(EffectData effect) => effect != null && _effects.Contains(effect);
        public bool Knows(EnemyData enemy) => enemy != null && _enemies.Contains(enemy);

        /// <summary>Debug: forgets everything — the glossary shows all entries as unknown again.</summary>
        public void Clear()
        {
            _dice.Clear();
            _effects.Clear();
            _enemies.Clear();
        }
    }
}
