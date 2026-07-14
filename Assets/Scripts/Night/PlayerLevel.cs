using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The player's level and experience. Damage dealt to enemies is XP; enough of it levels up (the
    /// threshold scales each level), which queues a stat upgrade the combat engine resolves. Also drives
    /// the XP bar and spawns the XP icons that fly from a hit enemy to the bar.
    /// </summary>
    public class PlayerLevel : MonoBehaviour
    {
        public static PlayerLevel Instance { get; private set; }

        [Header("Progression")]
        [SerializeField] private int startLevel = 1;
        [Tooltip("XP needed for level 2.")]
        [SerializeField] private int baseXpToLevel = 20;
        [Tooltip("The threshold grows by this factor each level.")]
        [SerializeField] private float growthPerLevel = 1.35f;

        [Header("UI")]
        [Tooltip("The XP bar (non-interactable). Its value fills toward the current XP.")]
        [SerializeField] private Slider xpBar;
        [SerializeField] private TextMeshProUGUI levelText;
        [Tooltip("How fast the bar visually catches up to the real XP (units/sec).")]
        [SerializeField] private float barFillSpeed = 40f;
        [Tooltip("How long the full bar is held (so the player sees it fill) before it empties on level-up.")]
        [SerializeField] private float fullHoldSeconds = 0.7f;

        [Header("XP icons")]
        [Tooltip("Overlay the flying XP icons are parented to.")]
        [SerializeField] private RectTransform iconContainer;
        [SerializeField] private XpIcon xpIconPrefab;
        [Tooltip("Where the XP icons fly to (bottom of the screen).")]
        [SerializeField] private RectTransform xpTarget;
        [Tooltip("Cap on icons spawned per hit (XP still counts the full amount).")]
        [SerializeField] private int maxIconsPerHit = 25;
        [SerializeField] private float iconFlyDuration = 0.6f;
        [SerializeField] private float iconSpread = 40f;

        private int _level;
        private int _xp;
        private int _xpToNext;
        private float _shownXp;

        public int Level => _level;

        /// <summary>True while the accumulated XP is enough for (another) level.</summary>
        public bool HasPendingLevelUp => _xp >= _xpToNext;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _level = Mathf.Max(1, startLevel);
            _xpToNext = XpForLevel(_level);
            _shownXp = 0f;
            UpdateBar(snap: true);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (xpBar == null) return;
            _shownXp = Mathf.MoveTowards(_shownXp, _xp, barFillSpeed * Time.unscaledDeltaTime);
            xpBar.value = _shownXp;
        }

        /// <summary>Adds XP (damage): the bar fills toward the threshold. The level-up itself is resolved by <see cref="PlayLevelUp"/>.</summary>
        public void GainXp(int amount)
        {
            if (amount <= 0) return;
            _xp += amount;
            UpdateBar();
        }

        /// <summary>
        /// Resolves one level-up visually: waits for the bar to reach full, holds it there so the player sees
        /// it, then empties it and rescales to the next level (carrying the overflow XP). The combat engine
        /// yields on this before opening the stat-upgrade UI.
        /// </summary>
        public IEnumerator PlayLevelUp()
        {
            if (_xp < _xpToNext) yield break;

            while (xpBar != null && _shownXp < _xpToNext - 0.5f) yield return null; // let it fill up
            yield return new WaitForSecondsRealtime(fullHoldSeconds);               // hold the full bar

            _xp -= _xpToNext;               // carry the overflow into the new level
            _level++;
            _xpToNext = XpForLevel(_level);
            _shownXp = 0f;                  // drop to empty, then it lerps toward the carry-over
            UpdateBar();
        }

        /// <summary>Spawns XP icons over <paramref name="from"/> that fly to the bar (cosmetic; XP already counted).</summary>
        public void SpawnXpIcons(RectTransform from, int count)
        {
            if (xpIconPrefab == null || iconContainer == null || xpTarget == null || from == null || count <= 0) return;

            Vector2 start = LocalPointOf(from);
            Vector2 end = LocalPointOf(xpTarget);
            int n = Mathf.Min(count, Mathf.Max(1, maxIconsPerHit));
            for (int i = 0; i < n; i++)
            {
                XpIcon icon = Instantiate(xpIconPrefab, iconContainer, false);
                Vector2 jitter = Random.insideUnitCircle * iconSpread;
                icon.Fly(start + jitter, end, iconFlyDuration + Random.Range(0f, 0.2f));
            }
        }

        private int XpForLevel(int lvl) => Mathf.Max(1, Mathf.RoundToInt(baseXpToLevel * Mathf.Pow(growthPerLevel, lvl - 1)));

        private void UpdateBar(bool snap = false)
        {
            if (xpBar != null)
            {
                xpBar.minValue = 0f;
                xpBar.maxValue = _xpToNext;
                if (snap) { _shownXp = _xp; xpBar.value = _shownXp; }
            }
            if (levelText != null) levelText.text = _level.ToString();
        }

        // A RectTransform's position expressed in the icon container's local space.
        private Vector2 LocalPointOf(RectTransform rt)
        {
            Canvas canvas = iconContainer.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(iconContainer, screen, cam, out Vector2 local);
            return local;
        }
    }
}
