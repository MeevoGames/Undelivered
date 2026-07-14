using System.Collections.Generic;
using TMPro;
using Undelivered.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// One tournament in the selection UI: its info, an enemy preview (every distinct enemy of the run),
    /// entry requirements, and a "Comenzar" button that enters that tournament. Populated at runtime by
    /// <see cref="TournamentPanel"/>. Once finished (won or lost) the button locks and the label changes.
    /// </summary>
    public class TournamentEntry : MonoBehaviour
    {
        [SerializeField] private TournamentData tournament;

        [Header("Info (optional)")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI difficultyText;

        [Header("Reward / penalty (optional)")]
        [SerializeField] private TextMeshProUGUI rewardText;
        [Tooltip("Hidden when the tournament has no penalty.")]
        [SerializeField] private TextMeshProUGUI penaltyText;

        [Header("Enemy preview")]
        [Tooltip("Where an icon per distinct enemy is listed.")]
        [SerializeField] private Transform previewContainer;
        [SerializeField] private Image enemyIconPrefab;

        [Header("Enter")]
        [SerializeField] private Button enterButton;
        [Tooltip("The button's label; changes to the won/lost text when finished.")]
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private string wonText = "Has ganado";
        [SerializeField] private string lostText = "Has perdido";
        [Tooltip("Optional: shows why the tournament can't be entered yet.")]
        [SerializeField] private TextMeshProUGUI requirementText;
        [Tooltip("Elements shown only while there's an unmet requirement (hidden otherwise).")]
        [SerializeField] private List<GameObject> requirementElements = new List<GameObject>();

        public TournamentData Tournament => tournament;
        public bool Finished { get; private set; }

        private string _originalLabel;

        private void Awake()
        {
            if (enterButton != null) enterButton.onClick.AddListener(OnEnter);
            if (label != null) _originalLabel = label.text;
        }

        private void OnDestroy()
        {
            if (enterButton != null) enterButton.onClick.RemoveListener(OnEnter);
        }

        /// <summary>Fills the entry from a tournament: info, enemy preview and entry requirements.</summary>
        public void Setup(TournamentData data)
        {
            tournament = data;
            Finished = false;

            if (nameText != null) nameText.text = data != null ? data.TournamentName : "";
            if (difficultyText != null) difficultyText.text = data != null ? data.Difficulty : "";
            if (label != null) label.text = _originalLabel;

            if (rewardText != null) rewardText.text = RewardSummary();
            string penalty = PenaltySummary();
            if (penaltyText != null)
            {
                bool hasPenalty = !string.IsNullOrEmpty(penalty);
                penaltyText.gameObject.SetActive(hasPenalty);
                if (hasPenalty) penaltyText.text = penalty;
            }

            BuildPreview(data);
            Refresh();
        }

        // Lists an icon per distinct enemy of the tournament (future: unknown enemies show a black silhouette).
        private void BuildPreview(TournamentData data)
        {
            if (previewContainer == null || enemyIconPrefab == null) return;

            for (int i = previewContainer.childCount - 1; i >= 0; i--)
                Destroy(previewContainer.GetChild(i).gameObject);

            if (data == null) return;
            foreach (EnemyData enemy in data.UniqueEnemies())
            {
                if (enemy == null) continue;
                Image icon = Instantiate(enemyIconPrefab, previewContainer, false);
                icon.sprite = enemy.Sprite;
                icon.enabled = enemy.Sprite != null;
            }
        }

        /// <summary>Re-checks the entry requirements against the player's current state.</summary>
        public void Refresh()
        {
            if (Finished) return;
            string missing = MissingRequirement();
            bool met = string.IsNullOrEmpty(missing);
            if (enterButton != null) enterButton.interactable = met;
            if (requirementText != null)
            {
                requirementText.gameObject.SetActive(!met);
                if (!met) requirementText.text = missing;
            }
            ShowRequirementElements(!met);
        }

        private void ShowRequirementElements(bool show)
        {
            if (requirementElements == null) return;
            foreach (GameObject go in requirementElements)
                if (go != null) go.SetActive(show);
        }

        // The first unmet requirement (empty = all met). Base = having what the penalty would take; then extras.
        private string MissingRequirement()
        {
            if (tournament == null) return "Sin torneo";

            TournamentData.Penalty pen = tournament.TournamentPenalty;
            if (pen != null)
            {
                if (Gold() < pen.gold) return $"Necesitas {pen.gold} de oro";
                if (Gems() < pen.gems) return $"Necesitas {Amount(pen.gems, "gema", "gemas")}";
                if (DiceOwned() < pen.diceLost) return $"Necesitas {Amount(pen.diceLost, "dado", "dados")}";
                if (EffectsOwned() < pen.effectsLost) return $"Necesitas {Amount(pen.effectsLost, "efecto", "efectos")}";
            }

            if (Level() < tournament.MinLevel) return $"Necesitas nivel {tournament.MinLevel}";
            if (DeckDice() < tournament.MinDeckDice) return $"Necesitas {Amount(tournament.MinDeckDice, "dado", "dados")} en el deck";
            if (DeckEffects() < tournament.MinDeckEffects) return $"Necesitas {Amount(tournament.MinDeckEffects, "efecto", "efectos")} en el deck";
            return "";
        }

        private static int Gold() => StatsManager.Instance != null ? StatsManager.Instance.Gold : 0;
        private static int Gems() => NightWallet.Instance != null ? NightWallet.Instance.Gems : 0;
        private static int DiceOwned() => Inventory.Instance != null ? Inventory.Instance.Dice.Count : 0;
        private static int EffectsOwned() => Inventory.Instance != null ? Inventory.Instance.Effects.Count : 0;
        private static int Level() => PlayerLevel.Instance != null ? PlayerLevel.Instance.Level : 1;
        private static int DeckDice() => Deck.Instance != null ? Deck.Instance.Dice.Count : 0;
        private static int DeckEffects() => EffectDeck.Instance != null ? EffectDeck.Instance.Effects.Count : 0;

        // "1 dado" / "3 dados" — singular when the count is 1.
        private static string Amount(int n, string singular, string plural) => n + " " + (n == 1 ? singular : plural);

        private static int CountNonNull<T>(List<T> list) where T : Object
        {
            if (list == null) return 0;
            int n = 0;
            foreach (T x in list) if (x != null) n++;
            return n;
        }

        // "100 de oro, 2 gemas, 1 dado" — the non-empty parts of the tournament's reward.
        private string RewardSummary()
        {
            if (tournament == null) return "";
            TournamentData.Reward r = tournament.TournamentReward;
            if (r == null) return "";

            var parts = new List<string>();
            if (r.gold > 0) parts.Add($"{r.gold} de oro");
            if (r.gems > 0) parts.Add(Amount(r.gems, "gema", "gemas"));
            int dice = CountNonNull(r.dice); if (dice > 0) parts.Add(Amount(dice, "dado", "dados"));
            int effects = CountNonNull(r.effects); if (effects > 0) parts.Add(Amount(effects, "efecto", "efectos"));
            int boxes = CountNonNull(r.boxes); if (boxes > 0) parts.Add(Amount(boxes, "caja", "cajas"));
            return string.Join(", ", parts);
        }

        // The non-empty parts of the penalty (empty when there's no penalty → the text is hidden).
        private string PenaltySummary()
        {
            if (tournament == null) return "";
            TournamentData.Penalty p = tournament.TournamentPenalty;
            if (p == null) return "";

            var parts = new List<string>();
            if (p.gold > 0) parts.Add($"{p.gold} de oro");
            if (p.gems > 0) parts.Add(Amount(p.gems, "gema", "gemas"));
            if (p.diceLost > 0) parts.Add(Amount(p.diceLost, "dado", "dados"));
            if (p.effectsLost > 0) parts.Add(Amount(p.effectsLost, "efecto", "efectos"));
            return string.Join(", ", parts);
        }

        private void OnEnter()
        {
            if (Finished || !string.IsNullOrEmpty(MissingRequirement())) return;
            if (CombatController.Instance != null) CombatController.Instance.EnterTournament(this);
        }

        public void MarkWon() => Finish(wonText);
        public void MarkLost() => Finish(lostText);

        /// <summary>Back to available (a fresh night): re-enable the button and restore its label.</summary>
        public void ResetEntry()
        {
            Finished = false;
            if (label != null) label.text = _originalLabel;
            Refresh();
        }

        private void Finish(string text)
        {
            Finished = true;
            if (enterButton != null) enterButton.interactable = false;
            if (label != null) label.text = text;
            if (requirementText != null) requirementText.gameObject.SetActive(false);
            ShowRequirementElements(false);
        }
    }
}
