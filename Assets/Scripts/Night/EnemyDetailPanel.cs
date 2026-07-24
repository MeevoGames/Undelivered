using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The per-enemy detail window, opened by tapping an enemy in combat. Shows its current stats
    /// (health, speed, shield), type, ability, rarity, the combat's synergy and its die. The ability
    /// and synergy texts hide themselves when the enemy has no ability / the combat has no synergy.
    /// Opens and closes like the shop and inventory: drops in from the top with a bounce.
    /// </summary>
    public class EnemyDetailPanel : MonoBehaviour
    {
        public static EnemyDetailPanel Instance { get; private set; }

        [SerializeField] private RectTransform panel;
        [SerializeField] private Image enemyImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [Tooltip("Name shown when the window is opened on the player instead of an enemy.")]
        [SerializeField] private string playerName = "Jugador";

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private TextMeshProUGUI shieldText;
        [Tooltip("Hidden when the enemy has no ability.")]
        [SerializeField] private TextMeshProUGUI abilityText;
        [SerializeField] private TextMeshProUGUI rarityText;
        [Tooltip("Hidden when the combat has no synergy.")]
        [SerializeField] private TextMeshProUGUI synergyText;

        [Tooltip("The slot GameObject where the enemy's die view is created.")]
        [SerializeField] private Transform dieSlot;
        [SerializeField] private DieView dieViewPrefab;

        [Header("Bounce (from the top, like the shop/inventory)")]
        [SerializeField] private float hiddenY = 1200f;
        [SerializeField] private float centerY = 0f;
        [SerializeField] private float undershootY = -25f;
        [SerializeField] private float overshootY = 15f;
        [SerializeField] private float speed = 3500f;
        [SerializeField] private float minSegment = 0.06f;

        private Coroutine _anim;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (panel != null) SetY(hiddenY);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Fills the window from an enemy slot (its FULL stats) and the combat's synergy, then bounces in.</summary>
        public void Show(EnemySlot slot, IReadOnlyList<SynergyData> synergies)
        {
            if (slot == null || slot.Enemy == null) return;
            // Always the enemy's full stats at its rarity, not what's left of them.
            Fill(slot.Enemy, slot.Enemy.HealthAt(slot.Rarity), slot.Enemy.ShieldAt(slot.Rarity), slot.Rarity, synergies);
        }

        /// <summary>From enemy data (a combat-prep preview): shows full health/shield at the given rarity.</summary>
        public void Show(EnemyData enemy, EnemyRarity rarity, SynergyData synergy)
        {
            if (enemy == null) return;
            Fill(enemy, enemy.HealthAt(rarity), enemy.ShieldAt(rarity), rarity,
                synergy != null ? new[] { synergy } : null);
        }

        /// <summary>Fills the window with the player's own full stats (no rarity / ability / synergy / die).</summary>
        public void ShowPlayer(PlayerCombatant player)
        {
            if (player == null) return;

            if (enemyImage != null)
            {
                enemyImage.sprite = player.Sprite;
                enemyImage.enabled = player.Sprite != null;
            }

            SetText(nameText, playerName);
            SetText(healthText, player.MaxHealth.ToString());
            SetText(speedText, player.Speed.ToString());
            SetText(shieldText, player.BaseShield.ToString());

            // The player has no rarity or ability, isn't part of a synergy, and throws a deck, not one die.
            SetOptional(rarityText, null);
            SetOptional(abilityText, null);
            SetOptional(synergyText, (string)null);
            BuildDie(null);

            BounceIn();
        }

        private void Fill(EnemyData enemy, int health, int shield, EnemyRarity rarity, IReadOnlyList<SynergyData> synergies)
        {
            if (enemyImage != null)
            {
                enemyImage.sprite = enemy.Sprite;
                enemyImage.enabled = enemy.Sprite != null;
            }

            SetText(nameText, enemy.EnemyName);
            SetText(healthText, health.ToString());
            SetText(speedText, enemy.Speed.ToString());
            SetText(shieldText, shield.ToString());
            SetOptional(rarityText, rarity.ToString()); // optional, so the player's view can hide it

            SetOptional(abilityText, enemy.Ability != null
                ? Describe(enemy.Ability.AbilityName, enemy.Ability.Description)
                : null);
            SetOptional(synergyText, SynergyLines(synergies));

            BuildDie(enemy.Die);

            BounceIn();
        }

        // "Name: description." — the description keeps its own full stop rather than getting a second one.
        private static string Describe(string name, string description)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            string text = (description ?? string.Empty).Trim();
            if (text.Length == 0) return name;
            if (!text.EndsWith(".")) text += ".";
            return $"{name}: {text}";
        }

        // Every synergy affecting this enemy, one per line.
        private static string SynergyLines(IReadOnlyList<SynergyData> synergies)
        {
            if (synergies == null || synergies.Count == 0) return null;

            var lines = new StringBuilder();
            foreach (SynergyData synergy in synergies)
            {
                if (synergy == null) continue;
                string line = Describe(synergy.SynergyName, synergy.Description);
                if (string.IsNullOrEmpty(line)) continue;
                if (lines.Length > 0) lines.Append('\n');
                lines.Append(line);
            }
            return lines.Length > 0 ? lines.ToString() : null;
        }

        /// <summary>Closes the window (wire this to a close button).</summary>
        public void Close()
        {
            if (panel == null) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(MoveThroughY(overshootY, undershootY, hiddenY));
        }

        // Clears the slot and creates the enemy's die view inside it (nothing if the enemy has no die).
        private void BuildDie(DiceData die)
        {
            if (dieSlot == null) return;
            for (int i = dieSlot.childCount - 1; i >= 0; i--) Destroy(dieSlot.GetChild(i).gameObject);
            if (die == null || dieViewPrefab == null) return;

            DieView view = Instantiate(dieViewPrefab, dieSlot, false);
            view.Setup(die);
            view.HideLuck(); // luck % only shows in the deck
            view.HideDeckOnly();
        }

        private void BounceIn()
        {
            if (panel == null) return;
            if (_anim != null) StopCoroutine(_anim);
            SetY(hiddenY);
            _anim = StartCoroutine(MoveThroughY(undershootY, overshootY, centerY));
        }

        private IEnumerator MoveThroughY(params float[] targets)
        {
            foreach (float target in targets)
            {
                Vector2 start = panel.anchoredPosition;
                float distance = Mathf.Abs(target - start.y);
                float duration = Mathf.Max(minSegment, distance / Mathf.Max(1f, speed));
                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / duration);
                    panel.anchoredPosition = new Vector2(start.x, Mathf.Lerp(start.y, target, k));
                    yield return null;
                }
                panel.anchoredPosition = new Vector2(start.x, target);
            }
            _anim = null;
        }

        private void SetY(float y)
        {
            Vector2 p = panel.anchoredPosition;
            p.y = y;
            panel.anchoredPosition = p;
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null) text.text = value;
        }

        // Fills the text, or hides it entirely when there is nothing to show.
        private static void SetOptional(TextMeshProUGUI text, string value)
        {
            if (text == null) return;
            bool has = !string.IsNullOrEmpty(value);
            text.gameObject.SetActive(has);
            if (has) text.text = value;
        }
    }
}
