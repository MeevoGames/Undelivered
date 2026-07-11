using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// The combat zone (top-right): up to 5 enemy positions (the max enemies per combat). For each
    /// enemy in the current combat, in visual order (index 0 = first = closest), it instantiates an
    /// <see cref="EnemySlot"/> from a prefab into the matching container and fills its data. Empty
    /// positions are left clear. The combat engine updates a slot's health/shield through <see cref="Slot"/>.
    /// </summary>
    public class CombatEnemies : MonoBehaviour
    {
        [Tooltip("The 5 fixed positions (plain GameObjects), in visual order. A slot is instantiated inside each.")]
        [SerializeField] private Transform[] slotContainers = new Transform[5];
        [SerializeField] private EnemySlot enemySlotPrefab;

        [Tooltip("The synergy active in this combat, if any (shown in an enemy's detail panel). Set by the combat engine.")]
        [SerializeField] private SynergyData currentSynergy;

        private EnemySlot[] _slots;

        /// <summary>The combat engine sets the active synergy so enemy detail panels can show it.</summary>
        public void SetSynergy(SynergyData synergy) => currentSynergy = synergy;

        /// <summary>
        /// Instantiates a slot per enemy, filling the **last** positions: N enemies occupy the last N
        /// containers in order (e.g. 2 enemies → positions 4 and 5; 3 enemies → 3, 4 and 5). Clears the rest.
        /// </summary>
        public void Build(EncounterData encounter)
        {
            if (slotContainers == null) return;

            // Clear every position from a previous combat.
            foreach (Transform container in slotContainers)
            {
                if (container == null) continue;
                for (int c = container.childCount - 1; c >= 0; c--) Destroy(container.GetChild(c).gameObject);
            }

            int count = encounter != null ? Mathf.Min(encounter.Enemies.Count, slotContainers.Length) : 0;
            _slots = new EnemySlot[count];

            int start = slotContainers.Length - count; // first enemy goes here, the rest follow to the last
            for (int i = 0; i < count; i++)
            {
                EncounterData.Enemy entry = encounter.Enemies[i];
                Transform container = slotContainers[start + i];
                if (entry == null || entry.enemy == null || container == null || enemySlotPrefab == null) continue;

                EnemySlot slot = Instantiate(enemySlotPrefab, container, false);
                slot.Setup(entry.enemy, entry.rarity);
                slot.SetClick(() => EnemyDetailPanel.Instance?.Show(slot, currentSynergy));
                _slots[i] = slot;
            }
        }

        /// <summary>The slot for the enemy at this visual index (0 = first), or null.</summary>
        public EnemySlot Slot(int index) => _slots != null && index >= 0 && index < _slots.Length ? _slots[index] : null;

        /// <summary>The closest living enemy — the one in the lowest-numbered occupied slot (index 0 first).</summary>
        public EnemySlot ClosestEnemy()
        {
            if (_slots == null) return null;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].IsAlive) return _slots[i];
            return null;
        }
    }
}
