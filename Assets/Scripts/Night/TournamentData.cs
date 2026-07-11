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

        [SerializeField] private Reward reward = new Reward();
        [Tooltip("Lost on defeat. Leave everything at 0 for the early, no-penalty tournaments.")]
        [SerializeField] private Penalty penalty = new Penalty();

        public string TournamentName => tournamentName;
        public string DescriptionForTooltip => descriptionForTooltip;
        public string Difficulty => difficulty;
        public IReadOnlyList<EncounterData> Combats => combats;
        public Reward TournamentReward => reward;
        public Penalty TournamentPenalty => penalty;
    }
}
