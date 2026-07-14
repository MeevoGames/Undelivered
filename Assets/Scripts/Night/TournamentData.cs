using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// A tournament: an ordered run of combats (usually 5, escalating in difficulty). It states what is
    /// won on victory and what is lost on defeat. Early tournaments have no penalty; later ones punish
    /// with gems / gold / dice / effects.
    /// </summary>
    [CreateAssetMenu(fileName = "Tournament", menuName = "Undelivered/Night/Tournament")]
    public class TournamentData : ScriptableObject
    {
        [System.Serializable]
        public class Reward
        {
            public int gold;
            public int gems;
            public List<DiceData> dice = new List<DiceData>();
            public List<EffectData> effects = new List<EffectData>();
            public List<BoxData> boxes = new List<BoxData>();
        }

        [System.Serializable]
        public class Penalty
        {
            public int gold;
            public int gems;
            [Tooltip("How many random dice / effects the player loses on defeat.")]
            public int diceLost;
            public int effectsLost;
        }

        [SerializeField] private string tournamentName;
        [SerializeField, TextArea] private string descriptionForTooltip;
        [SerializeField] private string difficulty;

        [Tooltip("The combats in order (usually 5, escalating).")]
        [SerializeField] private List<EncounterData> combats = new List<EncounterData>();

        [Tooltip("Optional heal rooms between combats. Entry i = max HP the player can heal in a room AFTER combat i (0 / missing = no room).")]
        [SerializeField] private List<int> healRoomAfterCombat = new List<int>();

        [SerializeField] private Reward reward = new Reward();
        [Tooltip("Lost on defeat. Leave everything at 0 for the early, no-penalty tournaments.")]
        [SerializeField] private Penalty penalty = new Penalty();

        [Header("Extra entry requirements (0 = none). The base requirement is always having what the penalty would take.")]
        [Tooltip("Minimum player level to enter.")]
        [SerializeField] private int minLevel;
        [Tooltip("Minimum dice equipped in the deck.")]
        [SerializeField] private int minDeckDice;
        [Tooltip("Minimum effects equipped in the deck.")]
        [SerializeField] private int minDeckEffects;

        public string TournamentName => tournamentName;
        public string DescriptionForTooltip => descriptionForTooltip;
        public string Difficulty => difficulty;
        public IReadOnlyList<EncounterData> Combats => combats;
        public Reward TournamentReward => reward;
        public Penalty TournamentPenalty => penalty;
        public int MinLevel => minLevel;
        public int MinDeckDice => minDeckDice;
        public int MinDeckEffects => minDeckEffects;

        /// <summary>Every distinct enemy across the tournament's combats, in first-seen order (for the preview).</summary>
        public List<EnemyData> UniqueEnemies()
        {
            var list = new List<EnemyData>();
            if (combats == null) return list;
            foreach (EncounterData combat in combats)
            {
                if (combat == null) continue;
                foreach (EncounterData.Enemy e in combat.Enemies)
                    if (e != null && e.enemy != null && !list.Contains(e.enemy)) list.Add(e.enemy);
            }
            return list;
        }

        /// <summary>Max HP the player can heal in a room after the combat at this index (0 = no room).</summary>
        public int HealRoomAfter(int combatIndex) =>
            healRoomAfterCombat != null && combatIndex >= 0 && combatIndex < healRoomAfterCombat.Count
                ? Mathf.Max(0, healRoomAfterCombat[combatIndex])
                : 0;
    }
}
