using System.Collections;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// Drives the healing visuals: an enemy's drop that falls, rests and flies to the player (feature 1),
    /// and the heal-room fountain that sends one +1 icon per healed point to the player (feature 2). The
    /// actual healing happens when each drop/icon reaches the player, alongside a green "+N" floating text.
    /// </summary>
    public class HealVisuals : MonoBehaviour
    {
        public static HealVisuals Instance { get; private set; }

        [Tooltip("Overlay the drops/icons live in (above the combat).")]
        [SerializeField] private RectTransform overlay;
        [Tooltip("Where drops/icons fly to (the player).")]
        [SerializeField] private RectTransform playerTarget;

        [Header("Enemy heal drop (feature 1)")]
        [SerializeField] private HealDrop dropPrefab;
        [Tooltip("How far the drop falls from where the enemy died.")]
        [SerializeField] private float dropFallDistance = 120f;
        [SerializeField] private float dropFallTime = 0.35f;
        [Tooltip("How long the drop rests on the ground before flying to the player.")]
        [SerializeField] private float dropGroundTime = 0.6f;
        [SerializeField] private float dropFlyTime = 0.5f;

        [Header("Heal-room icons (feature 2)")]
        [SerializeField] private HealDrop healIconPrefab;
        [SerializeField] private float iconFlyTime = 0.5f;
        [SerializeField] private float iconStagger = 0.08f;
        [SerializeField] private float iconSpread = 30f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Feature 1: a drop falls where the enemy died, rests, then flies to the player and heals (capped).</summary>
        public void DropFromEnemy(RectTransform enemy, int amount, PlayerCombatant player)
        {
            if (enemy == null || player == null || overlay == null || playerTarget == null || dropPrefab == null || amount <= 0) return;

            Vector2 spawn = LocalOf(enemy);
            Vector2 ground = spawn + Vector2.down * dropFallDistance;
            Vector2 target = LocalOf(playerTarget);

            HealDrop drop = Instantiate(dropPrefab, overlay, false);
            drop.Play(spawn, ground, dropFallTime, dropGroundTime, target, dropFlyTime, () => HealBy(player, amount));
        }

        /// <summary>Feature 2: the fountain sends one +1 icon per point of healing (capped by missing HP) to the player.</summary>
        public IEnumerator FountainHeal(RectTransform fountain, int maxHeal, PlayerCombatant player)
        {
            if (fountain == null || player == null || overlay == null || playerTarget == null || healIconPrefab == null) yield break;

            int count = Mathf.Min(Mathf.Max(0, maxHeal), player.MaxHealth - player.CurrentHealth);
            if (count <= 0) yield break;

            Vector2 from = LocalOf(fountain);
            Vector2 to = LocalOf(playerTarget);
            for (int i = 0; i < count; i++)
            {
                HealDrop icon = Instantiate(healIconPrefab, overlay, false);
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * iconSpread;
                icon.Play(from + jitter, from + jitter, 0f, 0f, to, iconFlyTime, () => HealBy(player, 1));
                yield return new WaitForSecondsRealtime(iconStagger);
            }
            yield return new WaitForSecondsRealtime(iconFlyTime); // let the last icon land
        }

        // Heals up to the amount (never past max) and shows a green "+N" on the player.
        private void HealBy(PlayerCombatant player, int amount)
        {
            if (player == null) return;
            int heal = Mathf.Min(amount, player.MaxHealth - player.CurrentHealth);
            if (heal <= 0) return;
            player.Heal(heal);
            player.ShowNumber(heal, FloatingNumbers.Kind.Heal);
        }

        // A world-space RectTransform's position expressed in the overlay's local space.
        private Vector2 LocalOf(RectTransform rt)
        {
            Canvas canvas = overlay.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, rt.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, cam, out Vector2 local);
            return local;
        }
    }
}
