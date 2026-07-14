using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// A combat synergy: while the enemies on the board meet a condition (a number of them sharing a type,
    /// a rarity, or being the same enemy), it grants one or more outcomes to the enemies that take part —
    /// flat stats (+health / +shield / +speed), an immunity, a shield-skipping attack, or per-round boons
    /// (heal / +max health). Authoring data only; <see cref="SynergySystem"/> evaluates it each combat.
    ///
    /// A synergy stops applying the moment its condition is no longer met (e.g. a "3 of a kind" drops to 2
    /// when one dies): its granted bonuses are then reverted. Its on-screen icon never disappears mid-combat
    /// — it just swaps to its broken sprite.
    /// </summary>
    [CreateAssetMenu(fileName = "Synergy", menuName = "Undelivered/Night/Synergy")]
    public class SynergyData : ScriptableObject
    {
        /// <summary>What has to be shared for the synergy to trigger.</summary>
        public enum ConditionKind
        {
            SameType,   // N enemies of the same EnemyType
            SameRarity, // N enemies of the same EnemyRarity
            Identical   // N copies of the same EnemyData (e.g. 3 rats)
        }

        /// <summary>What an active synergy grants to the enemies taking part.</summary>
        public enum OutcomeKind
        {
            AddHealth,          // flat +max health (and heals by the same amount)
            AddShield,          // flat +shield
            AddSpeed,           // flat +speed (turn order)
            PoisonImmunity,     // immune to Veneno
            SkipShield,         // its attacks hit the player's health directly
            HealPerRound,       // heals this much at the start of each round
            MaxHealthPerRound   // +this much max health at the start of each round
        }

        [System.Serializable]
        public class Outcome
        {
            public OutcomeKind kind;
            [Tooltip("Amount for the numeric outcomes (health/shield/speed/per-round). Ignored by immunities/flags.")]
            public int amount = 1;
        }

        [SerializeField] private string synergyName;
        [SerializeField, TextArea] private string description;

        [Header("Icons (General tooltip on hover)")]
        [Tooltip("Icon shown while the synergy is active.")]
        [SerializeField] private Sprite icon;
        [Tooltip("Icon shown once the synergy stops applying (the same illustration, broken).")]
        [SerializeField] private Sprite brokenIcon;

        [Header("Condition")]
        [SerializeField] private ConditionKind condition = ConditionKind.SameType;
        [Tooltip("How many enemies must share the trait for the synergy to trigger (e.g. 2 or 3).")]
        [SerializeField, Min(2)] private int requiredCount = 3;

        [Tooltip("SameType / Identical: also require the shared trait to be this exact type (leave 'restrict' off for any).")]
        [SerializeField] private bool restrictType;
        [SerializeField] private EnemyType requiredType;

        [Tooltip("SameRarity: restrict to this rarity (leave off for any rarity).")]
        [SerializeField] private bool restrictRarity;
        [SerializeField] private EnemyRarity requiredRarity;

        [Tooltip("Identical: restrict to copies of this exact enemy (leave empty for any repeated enemy).")]
        [SerializeField] private EnemyData requiredEnemy;

        [Header("Outcomes")]
        [Tooltip("Everything this synergy grants while active (e.g. +5 health AND +10 shield = two outcomes).")]
        [SerializeField] private List<Outcome> outcomes = new List<Outcome>();

        public string SynergyName => synergyName;
        public string Description => description;
        public Sprite Icon => icon;
        public Sprite BrokenIcon => brokenIcon;

        public ConditionKind Condition => condition;
        public int RequiredCount => Mathf.Max(2, requiredCount);
        public bool RestrictType => restrictType;
        public EnemyType RequiredType => requiredType;
        public bool RestrictRarity => restrictRarity;
        public EnemyRarity RequiredRarity => requiredRarity;
        public EnemyData RequiredEnemy => requiredEnemy;
        public IReadOnlyList<Outcome> Outcomes => outcomes;
    }
}
