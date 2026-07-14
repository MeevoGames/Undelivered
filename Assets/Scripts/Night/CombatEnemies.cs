using System.Collections;
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

        [Header("Bounce-in (arriving for the next combat)")]
        [Tooltip("How far to the right (off-screen) the enemies start before sliding into their slots.")]
        [SerializeField] private float bounceFromX = 1600f;
        [Tooltip("How far past their slot they overshoot before settling.")]
        [SerializeField] private float bounceOvershootX = 40f;
        [SerializeField] private float bounceSpeed = 3500f;
        [SerializeField] private float bounceMinSegment = 0.08f;

        [Header("Heal room")]
        [Tooltip("The healing fountain shown in the heal room (a sprite), placed in a slot like an enemy.")]
        [SerializeField] private RectTransform fountainPrefab;
        [Tooltip("Which slot (0-based) the fountain appears in. 2 = slot 3.")]
        [SerializeField] private int fountainSlot = 2;

        private EnemySlot[] _slots;
        private float[] _homeX; // each slot's resting X (bounce offsets are relative to this)
        private RectTransform _fountain;

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
            _homeX = new float[count];

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
                _homeX[i] = slot.transform is RectTransform rt ? rt.anchoredPosition.x : 0f;
            }
        }

        /// <summary>Slides the enemies in from off-screen right to their slots with a bounce (like the shop/inventory).</summary>
        public IEnumerator BounceIn()
        {
            if (_slots == null || _homeX == null) yield break;

            SetBounceOffset(bounceFromX);                    // start off-screen to the right
            float current = bounceFromX;
            foreach (float target in new[] { -bounceOvershootX, 0f }) // slide in, overshoot past the slot, settle
            {
                float distance = Mathf.Abs(target - current);
                float duration = Mathf.Max(bounceMinSegment, distance / Mathf.Max(1f, bounceSpeed));
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / duration);
                    SetBounceOffset(Mathf.Lerp(current, target, k));
                    yield return null;
                }
                SetBounceOffset(target);
                current = target;
            }
        }

        /// <summary>The heal-room fountain, once shown (for the icon origin), or null.</summary>
        public RectTransform Fountain => _fountain;

        /// <summary>Places the heal-room fountain in its slot and bounces it in (like an enemy).</summary>
        public IEnumerator ShowFountain()
        {
            ClearFountain();
            if (fountainPrefab == null || slotContainers == null || fountainSlot < 0 || fountainSlot >= slotContainers.Length) yield break;
            Transform container = slotContainers[fountainSlot];
            if (container == null) yield break;

            _fountain = Instantiate(fountainPrefab, container, false);
            float homeX = _fountain.anchoredPosition.x;
            float homeY = _fountain.anchoredPosition.y;

            _fountain.anchoredPosition = new Vector2(homeX + bounceFromX, homeY);
            float current = bounceFromX;
            foreach (float target in new[] { -bounceOvershootX, 0f })
            {
                float distance = Mathf.Abs(target - current);
                float duration = Mathf.Max(bounceMinSegment, distance / Mathf.Max(1f, bounceSpeed));
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / duration);
                    _fountain.anchoredPosition = new Vector2(homeX + Mathf.Lerp(current, target, k), homeY);
                    yield return null;
                }
                _fountain.anchoredPosition = new Vector2(homeX + target, homeY);
                current = target;
            }
        }

        /// <summary>Removes the heal-room fountain.</summary>
        public void ClearFountain()
        {
            if (_fountain != null) { Destroy(_fountain.gameObject); _fountain = null; }
        }

        // Offsets every slot horizontally from its resting X (0 = home).
        private void SetBounceOffset(float dx)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null) continue;
                if (_slots[i].transform is RectTransform rt)
                    rt.anchoredPosition = new Vector2(_homeX[i] + dx, rt.anchoredPosition.y);
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

        /// <summary>The index of the closest living enemy (0 = first), or -1 if none are left.</summary>
        public int ClosestEnemyIndex()
        {
            if (_slots == null) return -1;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null && _slots[i].IsAlive) return i;
            return -1;
        }
    }
}
