using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// Evaluates the combat synergies (<see cref="SynergyData"/>) against the enemies on the board and
    /// applies their outcomes. At combat start it lists an icon for every synergy that can possibly trigger
    /// in this line-up. As enemies fall it re-checks each one: a synergy that stops being met has its
    /// granted bonuses reverted and its icon swapped to the broken sprite (the icon stays until the combat
    /// ends). Flat outcomes (health/shield/speed/immunities) are reconciled every check so they can be
    /// cleanly removed; per-round outcomes (heal / +max health) are applied at each round start.
    ///
    /// The combat controller drives it: <see cref="StartCombat"/> once the board is built, <see cref="Evaluate"/>
    /// whenever an enemy dies, <see cref="RoundStart"/> at each new round, and <see cref="EndCombat"/> on exit.
    /// </summary>
    public class SynergySystem : MonoBehaviour
    {
        [Tooltip("Every synergy that may appear. Those met by a combat's opening line-up get an icon.")]
        [SerializeField] private List<SynergyData> allSynergies = new List<SynergyData>();

        [Tooltip("Where the synergy icons are listed on screen.")]
        [SerializeField] private RectTransform iconContainer;
        [SerializeField] private SynergyIcon iconPrefab;

        private static readonly List<int> Empty = new List<int>();

        private readonly List<SynergyData> _possible = new List<SynergyData>(); // synergies with an icon this combat
        private readonly List<SynergyIcon> _icons = new List<SynergyIcon>();     // parallel to _possible
        private int[] _roundsActive;   // parallel to _possible: rounds each synergy has been continuously active
        private int[] _appliedHealth;  // per slot index: max-health bonus currently applied (for clean revert)
        private int[] _appliedShield;  // per slot index: shield bonus currently applied
        private SynergyData _primary;  // first active synergy (shown in the enemy detail panel)

        /// <summary>The first currently-active synergy, or null — the enemy detail panel shows its name.</summary>
        public SynergyData PrimaryActive => _primary;

        /// <summary>Board just built: list the possible synergies as icons and apply their opening bonuses.</summary>
        public void StartCombat(EnemySlot[] slots)
        {
            EndCombat();
            if (slots == null) return;

            _appliedHealth = new int[slots.Length];
            _appliedShield = new int[slots.Length];

            if (allSynergies != null)
                foreach (SynergyData s in allSynergies)
                {
                    if (s == null || MatchedIndices(s, slots).Count == 0) continue; // can't ever trigger here
                    _possible.Add(s);

                    SynergyIcon icon = iconContainer != null && iconPrefab != null
                        ? Instantiate(iconPrefab, iconContainer, false)
                        : null;
                    if (icon != null) icon.Setup(s);
                    _icons.Add(icon);
                }

            _roundsActive = new int[_possible.Count];
            Evaluate(slots); // apply the initial bonuses and set the icons active
        }

        /// <summary>Re-checks every synergy (e.g. after an enemy dies): reconciles bonuses and updates icons.</summary>
        public void Evaluate(EnemySlot[] slots)
        {
            if (_possible.Count == 0 || slots == null) return;

            List<int>[] matched = Recompute(slots);
            for (int p = 0; p < _possible.Count; p++)
                if (matched[p].Count == 0) _roundsActive[p] = 0; // inactive resets its per-round accrual

            ApplyBonuses(slots, matched);
            UpdateIcons(matched);
        }

        /// <summary>New round: run the per-round outcomes (heal / +max health), then reconcile.</summary>
        public void RoundStart(EnemySlot[] slots)
        {
            if (_possible.Count == 0 || slots == null) return;

            List<int>[] matched = Recompute(slots);

            // Per-round heals happen now (transient — a later drop of the synergy doesn't take them back).
            for (int p = 0; p < _possible.Count; p++)
            {
                if (matched[p].Count == 0) continue;
                foreach (SynergyData.Outcome o in _possible[p].Outcomes)
                {
                    if (o == null || o.kind != SynergyData.OutcomeKind.HealPerRound || o.amount <= 0) continue;
                    foreach (int i in matched[p])
                    {
                        EnemySlot slot = i >= 0 && i < slots.Length ? slots[i] : null;
                        if (slot == null || !slot.IsAlive) continue;
                        slot.Heal(o.amount);
                        slot.ShowNumber(o.amount, FloatingNumbers.Kind.Heal);
                    }
                }
            }

            // Grow the per-round accruals for active synergies, then reconcile (bumps MaxHealthPerRound).
            for (int p = 0; p < _possible.Count; p++)
                _roundsActive[p] = matched[p].Count > 0 ? _roundsActive[p] + 1 : 0;

            ApplyBonuses(slots, matched);
            UpdateIcons(matched);
        }

        /// <summary>Combat over: remove every synergy icon and forget all state.</summary>
        public void EndCombat()
        {
            foreach (SynergyIcon icon in _icons)
                if (icon != null) Destroy(icon.gameObject);
            _icons.Clear();
            _possible.Clear();
            _roundsActive = null;
            _appliedHealth = null;
            _appliedShield = null;
            _primary = null;
        }

        // ----- evaluation -----

        // The alive-enemy slot indices that take part in this synergy (empty if its condition isn't met).
        private List<int> MatchedIndices(SynergyData s, EnemySlot[] slots)
        {
            if (s == null || slots == null) return Empty;

            // Group the alive enemies by the condition's key (respecting the optional restrict filter).
            var groups = new Dictionary<object, List<int>>();
            for (int i = 0; i < slots.Length; i++)
            {
                EnemySlot slot = slots[i];
                if (slot == null || !slot.IsAlive || slot.Enemy == null) continue;

                object key;
                switch (s.Condition)
                {
                    case SynergyData.ConditionKind.SameType:
                        if (s.RestrictType && slot.Enemy.Type != s.RequiredType) continue;
                        key = slot.Enemy.Type;
                        break;
                    case SynergyData.ConditionKind.SameRarity:
                        if (s.RestrictRarity && slot.Rarity != s.RequiredRarity) continue;
                        key = slot.Rarity;
                        break;
                    default: // Identical
                        if (s.RequiredEnemy != null && slot.Enemy != s.RequiredEnemy) continue;
                        key = slot.Enemy;
                        break;
                }

                if (!groups.TryGetValue(key, out List<int> members)) { members = new List<int>(); groups[key] = members; }
                members.Add(i);
            }

            // Matches the union of every group that reaches the required count.
            List<int> matched = null;
            foreach (KeyValuePair<object, List<int>> kv in groups)
                if (kv.Value.Count >= s.RequiredCount)
                    (matched ??= new List<int>()).AddRange(kv.Value);

            return matched ?? Empty;
        }

        private List<int>[] Recompute(EnemySlot[] slots)
        {
            var matched = new List<int>[_possible.Count];
            for (int p = 0; p < _possible.Count; p++)
                matched[p] = MatchedIndices(_possible[p], slots);
            return matched;
        }

        // Reconciles the standing (flat) bonuses to what the active synergies want, applying only the delta.
        private void ApplyBonuses(EnemySlot[] slots, List<int>[] matched)
        {
            if (slots == null) return;
            int n = slots.Length;
            if (_appliedHealth == null || _appliedHealth.Length != n) _appliedHealth = new int[n];
            if (_appliedShield == null || _appliedShield.Length != n) _appliedShield = new int[n];

            var desiredHealth = new int[n];
            var desiredShield = new int[n];
            var desiredSpeed = new int[n];
            var poison = new bool[n];
            var skip = new bool[n];

            for (int p = 0; p < _possible.Count; p++)
            {
                List<int> m = matched[p];
                if (m.Count == 0) continue;
                int rounds = _roundsActive[p];
                foreach (SynergyData.Outcome o in _possible[p].Outcomes)
                {
                    if (o == null) continue;
                    foreach (int i in m)
                    {
                        if (i < 0 || i >= n) continue;
                        switch (o.kind)
                        {
                            case SynergyData.OutcomeKind.AddHealth: desiredHealth[i] += o.amount; break;
                            case SynergyData.OutcomeKind.AddShield: desiredShield[i] += o.amount; break;
                            case SynergyData.OutcomeKind.AddSpeed: desiredSpeed[i] += o.amount; break;
                            case SynergyData.OutcomeKind.MaxHealthPerRound: desiredHealth[i] += o.amount * rounds; break;
                            case SynergyData.OutcomeKind.PoisonImmunity: poison[i] = true; break;
                            case SynergyData.OutcomeKind.SkipShield: skip[i] = true; break;
                            // HealPerRound is transient — handled in RoundStart.
                        }
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                EnemySlot slot = slots[i];
                if (slot == null) continue;

                int hDelta = desiredHealth[i] - _appliedHealth[i];
                if (hDelta != 0) slot.AddMaxHealth(hDelta);
                _appliedHealth[i] = desiredHealth[i];

                int sDelta = desiredShield[i] - _appliedShield[i];
                if (sDelta != 0) slot.AddSynergyShield(sDelta);
                _appliedShield[i] = desiredShield[i];

                slot.SynergySpeed = desiredSpeed[i];
                slot.PoisonImmune = poison[i];
                slot.SkipsShield = skip[i];
            }
        }

        private void UpdateIcons(List<int>[] matched)
        {
            _primary = null;
            for (int p = 0; p < _icons.Count && p < matched.Length; p++)
            {
                bool fulfilled = matched[p].Count > 0;
                if (fulfilled && _primary == null) _primary = _possible[p];
                if (_icons[p] != null) _icons[p].SetFulfilled(fulfilled);
            }
        }
    }
}
