using System.Collections.Generic;

namespace Undelivered.Night
{
    /// <summary>The three states a mark can be: Quema (burn), Veneno (poison), Congelamiento (freeze).</summary>
    public enum StatusType { Burn, Poison, Freeze }

    /// <summary>
    /// The Quema/Veneno/Congelamiento marks on the enemies of a combat. Each mark remembers the round it was
    /// applied and expires 2 rounds later (a mark from round N is gone at the start of round N+2). A single
    /// (enemy, state) holds at most 4 marks, so an enemy caps at 12 marks (4 per type). Per-mark values:
    /// Burn = 2 damage on that enemy's turn, Poison = 1 damage on any turn, Freeze = -1 to that enemy's roll.
    /// </summary>
    public class StatusTracker
    {
        public const int MaxMarks = 4;        // per (enemy, state)
        public const int DurationRounds = 2;  // a mark lasts its round + the next, then expires
        public const int BurnPerMark = 2;
        public const int PoisonPerMark = 1;
        public const int FreezePerMark = 1;

        // enemyIndex -> [Burn, Poison, Freeze] -> the applied round of each live mark.
        private readonly Dictionary<int, List<int>[]> _marks = new Dictionary<int, List<int>[]>();

        private List<int> Marks(int enemyIndex, StatusType type, bool create)
        {
            if (!_marks.TryGetValue(enemyIndex, out List<int>[] byType))
            {
                if (!create) return null;
                byType = new List<int>[3];
                _marks[enemyIndex] = byType;
            }
            int t = (int)type;
            if (byType[t] == null)
            {
                if (!create) return null;
                byType[t] = new List<int>();
            }
            return byType[t];
        }

        /// <summary>How many marks of a state the enemy currently has.</summary>
        public int Count(int enemyIndex, StatusType type)
        {
            List<int> m = Marks(enemyIndex, type, false);
            return m != null ? m.Count : 0;
        }

        /// <summary>Adds up to <paramref name="count"/> marks (capped at 4 total for that state), tagged with the current round.</summary>
        public void AddMarks(int enemyIndex, StatusType type, int count, int round)
        {
            if (count <= 0) return;
            List<int> m = Marks(enemyIndex, type, true);
            for (int i = 0; i < count && m.Count < MaxMarks; i++) m.Add(round);
        }

        /// <summary>Burn damage this enemy takes on its own turn (2 per mark).</summary>
        public int BurnDamage(int enemyIndex) => Count(enemyIndex, StatusType.Burn) * BurnPerMark;

        /// <summary>Poison damage this enemy takes on any turn (1 per mark).</summary>
        public int PoisonDamage(int enemyIndex) => Count(enemyIndex, StatusType.Poison) * PoisonPerMark;

        /// <summary>How much this enemy's roll is reduced (1 per mark).</summary>
        public int FreezeReduction(int enemyIndex) => Count(enemyIndex, StatusType.Freeze) * FreezePerMark;

        /// <summary>Removes marks applied 2+ rounds ago. Call at each round start (after bumping the round).</summary>
        public void Expire(int currentRound)
        {
            foreach (List<int>[] byType in _marks.Values)
            {
                if (byType == null) continue;
                foreach (List<int> m in byType)
                    m?.RemoveAll(applied => currentRound - applied >= DurationRounds);
            }
        }

        /// <summary>Clears every mark (new combat).</summary>
        public void Clear() => _marks.Clear();
    }
}
