using System.Collections;
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

        /// <summary>Fills the window from an enemy slot (current stats) and the combat's synergy, then bounces in.</summary>
        public void Show(EnemySlot slot, SynergyData synergy)
        {
            if (slot == null || slot.Enemy == null) return;
            EnemyData enemy = slot.Enemy;

            if (enemyImage != null)
            {
                enemyImage.sprite = enemy.Sprite;
                enemyImage.enabled = enemy.Sprite != null;
            }

            SetText(nameText, enemy.EnemyName);
            SetText(healthText, slot.CurrentHealth.ToString());
            SetText(speedText, enemy.Speed.ToString());
            SetText(shieldText, slot.CurrentShield.ToString());
            SetText(rarityText, slot.Rarity.ToString());

            SetOptional(abilityText, enemy.Ability != null ? enemy.Ability.AbilityName : null);
            SetOptional(synergyText, synergy != null ? synergy.SynergyName : null);

            BuildDie(enemy.Die);

            BounceIn();
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
