using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// A combat: a row of enemies. The list order is the **visual order** (index 0 = closest = the
    /// first to take the player's damage unless an effect changes it). Each entry pairs an
    /// <see cref="EnemyData"/> with the rarity it appears at. Turn order is derived from speed and
    /// synergies are derived from the enemies' types, both at runtime by the combat engine.
    /// </summary>
    [CreateAssetMenu(fileName = "Encounter", menuName = "Undelivered/Night/Encounter")]
    public class EncounterData : ScriptableObject
    {
        [System.Serializable]
        public class Enemy
        {
            public EnemyData enemy;
            public EnemyRarity rarity;
        }

        [Tooltip("Enemies in visual order (index 0 = closest = first to take damage).")]
        [SerializeField] private List<Enemy> enemies = new List<Enemy>();

        public IReadOnlyList<Enemy> Enemies => enemies;
    }
}
