using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// A combat synergy: when the enemies of a combat meet a type condition (all the same type, or
    /// certain types present together), an outcome triggers — a new effect, or multiplied damage /
    /// health / shield. Authoring data; the combat engine evaluates it against each combat's line-up.
    /// </summary>
    [CreateAssetMenu(fileName = "Synergy", menuName = "Undelivered/Night/Synergy")]
    public class SynergyData : ScriptableObject
    {
        public enum Condition { AllSameType, TypesPresent }
        public enum Outcome { NewEffect, MultiplyDamage, MultiplyHealth, MultiplyShield }

        [SerializeField] private string synergyName;
        [SerializeField, TextArea] private string description;

        [SerializeField] private Condition condition;
        [Tooltip("AllSameType: the shared type. TypesPresent: the types that must all appear.")]
        [SerializeField] private EnemyType[] types;

        [SerializeField] private Outcome outcome;
        [Tooltip("Multiplier for the MultiplyX outcomes.")]
        [SerializeField] private float magnitude = 2f;
        [Tooltip("Effect granted for the NewEffect outcome (optional).")]
        [SerializeField] private EffectData effect;

        public string SynergyName => synergyName;
        public string Description => description;
        public Condition SynergyCondition => condition;
        public EnemyType[] Types => types;
        public Outcome SynergyOutcome => outcome;
        public float Magnitude => magnitude;
        public EffectData Effect => effect;
    }
}
