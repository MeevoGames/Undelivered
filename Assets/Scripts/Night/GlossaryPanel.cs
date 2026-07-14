using System.Collections;
using System.Collections.Generic;
using Undelivered.DebugTools;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The knowledge book / glossary: opened with TAB, it lists every die / effect / enemy in the game in
    /// three tabs. Items the player knows (owned a die/effect, fought an enemy — see <see cref="Knowledge"/>)
    /// show normally; unknown ones show as "???" with a black-silhouette icon. Drops in from the top with a
    /// bounce, like the shop / inventory.
    /// </summary>
    public class GlossaryPanel : MonoBehaviour
    {
        public static GlossaryPanel Instance { get; private set; }

        private enum Category { Dice, Effects, Enemies }

        [SerializeField] private RectTransform panel;

        [Header("Content (a container + a prefab per category)")]
        [Tooltip("Simple square die cells.")]
        [SerializeField] private Transform diceContent;
        [SerializeField] private GlossaryItem diceItemPrefab;
        [Tooltip("Effect cells (square + a rarity-colour image).")]
        [SerializeField] private Transform effectContent;
        [SerializeField] private GlossaryItem effectItemPrefab;
        [Tooltip("Enemy cells (elongated, clickable).")]
        [SerializeField] private Transform enemyContent;
        [SerializeField] private GlossaryItem enemyItemPrefab;

        [Header("Tabs")]
        [SerializeField] private Button diceTab;
        [SerializeField] private Button effectsTab;
        [SerializeField] private Button enemiesTab;

        [Header("All content (assign every scriptable that exists)")]
        [SerializeField] private List<DiceData> allDice = new List<DiceData>();
        [SerializeField] private List<EffectData> allEffects = new List<EffectData>();
        [SerializeField] private List<EnemyData> allEnemies = new List<EnemyData>();

        /// <summary>Every scriptable in the game (used as the debug console's "give random" pools + allunlock).</summary>
        public IReadOnlyList<DiceData> AllDice => allDice;
        public IReadOnlyList<EffectData> AllEffects => allEffects;
        public IReadOnlyList<EnemyData> AllEnemies => allEnemies;

        [Header("Bounce (from the top, like the shop/inventory)")]
        [SerializeField] private float hiddenY = 1200f;
        [SerializeField] private float centerY = 0f;
        [SerializeField] private float undershootY = -25f;
        [SerializeField] private float overshootY = 15f;
        [SerializeField] private float speed = 3500f;
        [SerializeField] private float minSegment = 0.06f;

        private bool _open;
        private Category _current = Category.Dice;
        private Coroutine _anim;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            WireTab(diceTab, Category.Dice);
            WireTab(effectsTab, Category.Effects);
            WireTab(enemiesTab, Category.Enemies);
            if (panel != null) SetY(hiddenY);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || !k.tabKey.wasPressedThisFrame) return;
            if (!DebugConsole.IsOpen) Toggle(); // TAB toggles the book (not while the debug console is open)
        }

        /// <summary>Redraws the known/unknown state if the glossary is currently open (after allunlock).</summary>
        public void RefreshIfOpen()
        {
            if (_open) RebuildAll();
        }

        /// <summary>Opens the glossary if closed, closes it if open.</summary>
        public void Toggle()
        {
            if (_open) Close(); else Open();
        }

        public void Open()
        {
            _open = true;
            RebuildAll();        // fresh known/unknown state in each container
            SelectTab(_current); // show the active one
            BounceIn();
        }

        public void Close()
        {
            _open = false;
            if (panel == null) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(MoveThroughY(overshootY, undershootY, hiddenY));
        }

        private void WireTab(Button button, Category category)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectTab(category));
        }

        // Shows the active category's container and hides the others.
        private void SelectTab(Category category)
        {
            _current = category;
            if (diceContent != null) diceContent.gameObject.SetActive(category == Category.Dice);
            if (effectContent != null) effectContent.gameObject.SetActive(category == Category.Effects);
            if (enemyContent != null) enemyContent.gameObject.SetActive(category == Category.Enemies);
        }

        // Fills each category's own container (fresh known/unknown state).
        private void RebuildAll()
        {
            if (diceContent != null && diceItemPrefab != null)
            {
                Clear(diceContent);
                foreach (DiceData d in allDice)
                    if (d != null) Instantiate(diceItemPrefab, diceContent, false).SetDie(d, Knows(d));
            }
            if (effectContent != null && effectItemPrefab != null)
            {
                Clear(effectContent);
                foreach (EffectData eff in allEffects)
                    if (eff != null) Instantiate(effectItemPrefab, effectContent, false).SetEffect(eff, Knows(eff));
            }
            if (enemyContent != null && enemyItemPrefab != null)
            {
                Clear(enemyContent);
                foreach (EnemyData enm in allEnemies)
                    if (enm != null) Instantiate(enemyItemPrefab, enemyContent, false).SetEnemy(enm, default(EnemyRarity), Knows(enm));
            }
        }

        private static void Clear(Transform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--) Destroy(container.GetChild(i).gameObject);
        }

        private static bool Knows(DiceData d) => Knowledge.Instance != null && Knowledge.Instance.Knows(d);
        private static bool Knows(EffectData e) => Knowledge.Instance != null && Knowledge.Instance.Knows(e);
        private static bool Knows(EnemyData e) => Knowledge.Instance != null && Knowledge.Instance.Knows(e);

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
    }
}
