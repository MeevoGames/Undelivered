using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// An enemy ability — the most specific thing about an enemy (though it can repeat). Like a player
    /// effect, but working for the enemy: it helps them beat you or complicate the match. Its magnitude
    /// scales with the enemy's rarity. What it actually does is resolved by the combat engine later.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyAbility", menuName = "Undelivered/Night/Enemy Ability")]
    public class EnemyAbilityData : ScriptableObject
    {
        /// <summary>When the ability fires (resolved by the combat engine).</summary>
        public enum Trigger { Passive, HealthThreshold, Reactive, OnDeath }

        [SerializeField] private string abilityName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private Trigger trigger;
        [Tooltip("Base magnitude of the effect (scaled by the enemy's rarity).")]
        [SerializeField] private int magnitude = 1;

        public string AbilityName => abilityName;
        public string Description => description;
        public Trigger AbilityTrigger => trigger;
        public int Magnitude => magnitude;
    }
}
