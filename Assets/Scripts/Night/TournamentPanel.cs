using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// Builds the tournament-selection list: one <see cref="TournamentEntry"/> per tournament scriptable,
    /// inside a container. Refresh it when the player's resources / level / deck change so each entry's
    /// requirements stay current.
    /// </summary>
    public class TournamentPanel : MonoBehaviour
    {
        public static TournamentPanel Instance { get; private set; }

        [Tooltip("Every tournament to list (one entry each).")]
        [SerializeField] private List<TournamentData> tournaments = new List<TournamentData>();
        [Tooltip("Where the entries are created.")]
        [SerializeField] private RectTransform container;
        [SerializeField] private TournamentEntry entryPrefab;

        private readonly List<TournamentEntry> _entries = new List<TournamentEntry>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            Build();
        }

        /// <summary>(Re)creates one entry per tournament scriptable.</summary>
        public void Build()
        {
            foreach (TournamentEntry e in _entries) if (e != null) Destroy(e.gameObject);
            _entries.Clear();
            if (container == null || entryPrefab == null) return;

            foreach (TournamentData t in tournaments)
            {
                if (t == null) continue;
                TournamentEntry entry = Instantiate(entryPrefab, container, false);
                entry.Setup(t);
                _entries.Add(entry);
            }
        }

        /// <summary>Re-checks every entry's requirements (call when resources / level / deck change).</summary>
        public void Refresh()
        {
            foreach (TournamentEntry e in _entries) if (e != null) e.Refresh();
        }
    }
}
