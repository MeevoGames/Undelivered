using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// Tracks the player's temporary luck bonus (effect type 13). Luck belongs to the player, so a bonus
    /// applies to <b>every</b> die at once. A die's luck is the chance it lands on its highest face: the
    /// die's natural luck (<see cref="DiceData.BaseLuckFraction"/>) plus the active bonus. Each bonus is a
    /// number of percentage points that lasts a number of the player's turns, or the whole combat, and
    /// expires by player-turn number.
    /// </summary>
    public class LuckSystem
    {
        /// <summary>Passed as the duration for a bonus that lasts the whole combat.</summary>
        public const int WholeCombat = int.MaxValue;

        private struct Entry
        {
            public int amount;          // percentage points added to every die's luck
            public int expireAfterTurn; // gone once the player-turn counter passes this
        }

        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>Flat luck from level-up upgrades — applies to every die and isn't cleared between combats.</summary>
        public int Permanent { get; set; }

        /// <summary>Adds a luck bonus (in % points) to the player, lasting <paramref name="turns"/> player turns (or <see cref="WholeCombat"/>).</summary>
        public void AddLuck(int amount, int currentTurn, int turns)
        {
            if (amount <= 0) return;
            int expire = turns >= WholeCombat ? WholeCombat : currentTurn + turns;
            _entries.Add(new Entry { amount = amount, expireAfterTurn = expire });
        }

        /// <summary>The total active luck bonus (in % points), applied to every die and shown on the player's marker.</summary>
        public int TotalBonus()
        {
            int sum = 0;
            foreach (Entry e in _entries) sum += e.amount;
            return sum;
        }

        /// <summary>A die's luck as a 0..1 chance of its highest face (natural + permanent + temporary bonus, clamped).</summary>
        public float LuckFraction(DiceData die)
        {
            if (die == null) return 0f;
            return Mathf.Clamp01(die.BaseLuckFraction + (Permanent + TotalBonus()) / 100f);
        }

        /// <summary>A die's luck as a whole-percent for display.</summary>
        public int LuckPercent(DiceData die) => Mathf.RoundToInt(LuckFraction(die) * 100f);

        /// <summary>Drops bonuses whose duration has passed. Call at the start of each player turn (after bumping it).</summary>
        public void Expire(int currentTurn)
        {
            _entries.RemoveAll(e => currentTurn > e.expireAfterTurn);
        }

        /// <summary>Clears every bonus (new combat).</summary>
        public void Clear() => _entries.Clear();
    }
}
