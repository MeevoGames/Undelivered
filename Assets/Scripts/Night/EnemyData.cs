using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// An enemy's base definition. All enemies have health + shield (kill them at 0), speed (turn
    /// order — faster than the player attacks first), a type (for synergies), an ability and a die they
    /// roll on their turn. Rarity is not stored here: the same enemy can be placed in a combat at
    /// different rarities, which scale its health, shield and ability via <see cref="EnemyRarities"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "Undelivered/Night/Enemy")]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] private string enemyName;
        [SerializeField] private Sprite sprite;
        [SerializeField, TextArea] private string descriptionForTooltip;

        [Header("Stats")]
        [SerializeField] private int health = 10;
        [SerializeField] private int shield;
        [Tooltip("Turn order: an enemy faster than the player attacks before them.")]
        [SerializeField] private int speed = 5;

        [SerializeField] private EnemyType type;
        [SerializeField] private EnemyAbilityData ability;

        [Tooltip("The die this enemy rolls on its turn.")]
        [SerializeField] private DiceData die;

        public string EnemyName => enemyName;
        public Sprite Sprite => sprite;
        public string DescriptionForTooltip => descriptionForTooltip;
        public int Health => health;
        public int Shield => shield;
        public int Speed => speed;
        public EnemyType Type => type;
        public EnemyAbilityData Ability => ability;
        public DiceData Die => die;

        /// <summary>Health at the given rarity.</summary>
        public int HealthAt(EnemyRarity rarity) => Mathf.RoundToInt(health * EnemyRarities.Multiplier(rarity));

        /// <summary>Shield at the given rarity.</summary>
        public int ShieldAt(EnemyRarity rarity) => Mathf.RoundToInt(shield * EnemyRarities.Multiplier(rarity));
    }
}
