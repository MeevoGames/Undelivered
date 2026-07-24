using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// Slides the night panels in and out of the centre with a code-driven bounce (so any panel can
    /// reuse it, and future ones too). Vertical axis, in anchoredPosition.y:
    ///   • centre (visible) = 0, hidden = +1200 (off the top edge), with overshoots at -25 and +15.
    ///
    /// Opening Shop / Inventory / Gems pushes whatever is at the centre (the Combat panel) up and out
    /// and brings the opened panel down:
    ///   • opened panel: 1200 → -25 → 15 → 0
    ///   • pushed panel: 0 → -25 → 1200
    /// Closing reverses it: the panel leaves (0 → 15 → -25 → 1200) and Combat returns (1200 → -25 → 15 → 0).
    /// </summary>
    public class NightScreens : MonoBehaviour
    {
        [Header("Panels")]
        [Tooltip("The combat tab — the base that sits at the centre; Open pushes it away, Close brings it back.")]
        [SerializeField] private RectTransform combat;
        [SerializeField] private RectTransform shop;
        [SerializeField] private RectTransform inventory;
        [SerializeField] private RectTransform gems;
        [Tooltip("Tournament panel (its own entrance is horizontal and handled elsewhere; referenced here for completeness).")]
        [SerializeField] private RectTransform tournament;

        [Header("Bounce (anchoredPosition.y)")]
        [SerializeField] private float centerY = 0f;
        [SerializeField] private float hiddenY = 1200f;
        [Tooltip("Overshoot below the centre.")]
        [SerializeField] private float undershootY = -25f;
        [Tooltip("Overshoot above the centre.")]
        [SerializeField] private float overshootY = 15f;
        [Tooltip("X the tournament panel slides to when entered (off-screen to the right).")]
        [SerializeField] private float tournamentExitX = 2000f;

        [Header("Combat start")]
        [Tooltip("The turn space; drops in from the top with the bounce when combat starts.")]
        [SerializeField] private RectTransform spaceTurn;
        [Tooltip("Shop / gems / inventory buttons shown before combat; they slide off right when it starts.")]
        [SerializeField] private RectTransform[] preCombatButtons;
        [SerializeField] private RectTransform effectPlayer;
        [SerializeField] private RectTransform effectWorld;
        [Tooltip("X the combat prep panel slides to when combat starts (off-screen left).")]
        [SerializeField] private float combatExitX = -2000f;
        [Tooltip("X the pre-combat buttons slide to (off-screen right).")]
        [SerializeField] private float buttonsExitX = 2000f;

        [Header("Enemy effects panel (slides in; size stays fixed. The player's effect deck never moves)")]
        [Tooltip("EffectWorld's final anchoredPosition (anchor it to the bottom-right corner).")]
        [SerializeField] private Vector2 effectWorldPosition = new Vector2(-30f, 30f);

        [Header("Speed")]
        [Tooltip("Slide speed in pixels per second.")]
        [SerializeField] private float speed = 3500f;
        [Tooltip("Minimum duration of any single segment (seconds).")]
        [SerializeField] private float minSegment = 0.06f;

        private RectTransform _current;
        private bool _hubCleared;
        private readonly Dictionary<RectTransform, Coroutine> _anims = new Dictionary<RectTransform, Coroutine>();
        private readonly Dictionary<RectTransform, (Vector2 pos, Vector2 size)> _initial =
            new Dictionary<RectTransform, (Vector2 pos, Vector2 size)>();

        private void Awake()
        {
            // Remember the authored layout of every panel so we can snap back to it later.
            CaptureInitial(combat);
            CaptureInitial(shop);
            CaptureInitial(inventory);
            CaptureInitial(gems);
            CaptureInitial(tournament);
            CaptureInitial(spaceTurn);
            CaptureInitial(effectPlayer);
            CaptureInitial(effectWorld);
            if (preCombatButtons != null)
            {
                foreach (RectTransform button in preCombatButtons) CaptureInitial(button);
            }

            // The tournament panel shows first; the combat tab and the overlays start hidden above.
            SetY(shop, hiddenY);
            SetY(inventory, hiddenY);
            SetY(gems, hiddenY);
            SetY(combat, hiddenY);
            SetY(spaceTurn, hiddenY);
            _current = tournament;
        }

        /// <summary>Snaps every panel back to its initial layout (used when re-entering the night mode).</summary>
        public void ResetLayout()
        {
            foreach (KeyValuePair<RectTransform, Coroutine> anim in _anims)
            {
                if (anim.Value != null) StopCoroutine(anim.Value);
            }
            _anims.Clear();

            foreach (KeyValuePair<RectTransform, (Vector2 pos, Vector2 size)> entry in _initial)
            {
                entry.Key.anchoredPosition = entry.Value.pos;
                entry.Key.sizeDelta = entry.Value.size;
            }

            SetY(shop, hiddenY);
            SetY(inventory, hiddenY);
            SetY(gems, hiddenY);
            SetY(combat, hiddenY);
            SetY(spaceTurn, hiddenY);
            _current = tournament;
            _hubCleared = false; // the hub is back, so it can be cleared again next combat
        }

        private void CaptureInitial(RectTransform rt)
        {
            if (rt != null) _initial[rt] = (rt.anchoredPosition, rt.sizeDelta);
        }

        // ----- public API -----
        public void OpenShop() => Open(shop);
        public void CloseShop() => Close(shop);
        public void OpenInventory() => Open(inventory);
        public void CloseInventory() => Close(inventory);
        public void OpenGems() => Open(gems);
        public void CloseGems() => Close(gems);

        /// <summary>Enters the tournament: the tournament panel slides off to the right and combat bounces to the centre.</summary>
        public void OpenTournament()
        {
            if (_current == combat) return; // already entered
            if (tournament != null) SlideX(tournament, tournamentExitX);
            EnterFromTop(combat);
            _current = combat;
        }

        /// <summary>Back to tournament selection: the tournament panel slides back to the centre, over the combat.</summary>
        public void ReturnToTournament()
        {
            if (tournament != null) SlideX(tournament, 0f);
            _current = tournament;
        }

        /// <summary>
        /// Empties the hub so nothing is left behind an announcement: the tournament list bounces out, the
        /// prep view and any open overlay (shop / inventory / gems) are put away, and the pre-combat
        /// buttons leave to the right. Safe to call twice — the second call does nothing.
        /// </summary>
        public void ClearHub()
        {
            if (_hubCleared) return;
            _hubCleared = true;

            if (tournament != null) ExitClose(tournament); // the list leaves with a bounce
            SetY(combat, hiddenY);                          // the prep view is skipped
            SetY(shop, hiddenY);
            SetY(inventory, hiddenY);
            SetY(gems, hiddenY);

            if (preCombatButtons != null)
            {
                foreach (RectTransform button in preCombatButtons) SlideX(button, buttonsExitX);
            }
            _current = null;
        }

        /// <summary>
        /// Tutorial: straight from the tournament menu into combat. The hub is cleared, the turn space
        /// drops in and the enemy effect panel slides over — the combat-prep view is skipped entirely.
        /// </summary>
        public void StartTournamentDirect()
        {
            ClearHub();                                     // no-op if an announcement already cleared it
            EnterFromTop(spaceTurn);                        // the turn space drops in

            // The effect deck (effectPlayer) never moves or resizes — it stays where the scene places it.
            MoveBounceTo(effectWorld, effectWorldPosition);

            _current = null;
        }

        /// <summary>
        /// "Comenzar": the combat-prep panel leaves to the left, the turn space drops in with a bounce,
        /// the pre-combat buttons leave to the right, and the effect panels bounce to size.
        /// </summary>
        public void StartCombat()
        {
            if (_current != combat) return; // only from the combat-prep view

            SlideX(combat, combatExitX);          // combat prep → off-screen left
            EnterFromTop(spaceTurn);              // turn space drops in from the top

            if (preCombatButtons != null)
            {
                foreach (RectTransform button in preCombatButtons) SlideX(button, buttonsExitX);
            }

            // The effect deck (effectPlayer) never moves or resizes — it stays where the scene places it.
            MoveBounceTo(effectWorld, effectWorldPosition);   // enemy effects slide to the bottom-right

            _current = null; // no longer on a vertical-overlay base
        }

        // ----- open / close -----
        private void Open(RectTransform target)
        {
            if (target == null || target == _current) return;

            EnterFromTop(target);            // 1200 → -25 → 15 → 0
            if (_current != null) ExitPushed(_current); // 0 → -25 → 1200
            _current = target;
        }

        private void Close(RectTransform target)
        {
            if (target == null) return;

            ExitClose(target);               // 0 → 15 → -25 → 1200
            if (combat != null && combat != target)
            {
                EnterFromTop(combat);        // 1200 → -25 → 15 → 0
            }
            _current = combat;
        }

        // ----- movement primitives -----
        private void EnterFromTop(RectTransform panel)
        {
            if (panel == null) return;
            StopAnim(panel);
            SetY(panel, hiddenY);
            Run(panel, undershootY, overshootY, centerY);
        }

        private void ExitPushed(RectTransform panel)
        {
            if (panel == null) return;
            StopAnim(panel);
            Run(panel, undershootY, hiddenY);
        }

        private void ExitClose(RectTransform panel)
        {
            if (panel == null) return;
            StopAnim(panel);
            Run(panel, overshootY, undershootY, hiddenY);
        }

        private void Run(RectTransform panel, params float[] keyframes)
        {
            _anims[panel] = StartCoroutine(Move(panel, keyframes));
        }

        private IEnumerator Move(RectTransform panel, float[] keyframes)
        {
            foreach (float targetY in keyframes)
            {
                Vector2 start = panel.anchoredPosition;
                float distance = Mathf.Abs(targetY - start.y);
                float duration = Mathf.Max(minSegment, distance / Mathf.Max(1f, speed));

                for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / duration);
                    panel.anchoredPosition = new Vector2(start.x, Mathf.Lerp(start.y, targetY, k));
                    yield return null;
                }
                panel.anchoredPosition = new Vector2(start.x, targetY);
            }
            _anims[panel] = null;
        }

        // Plain horizontal slide (no bounce) — used to send the tournament off to the right.
        private void SlideX(RectTransform panel, float targetX)
        {
            if (panel == null) return;
            StopAnim(panel);
            _anims[panel] = StartCoroutine(MoveX(panel, targetX));
        }

        private IEnumerator MoveX(RectTransform panel, float targetX)
        {
            Vector2 start = panel.anchoredPosition;
            float distance = Mathf.Abs(targetX - start.x);
            float duration = Mathf.Max(minSegment, distance / Mathf.Max(1f, speed));

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                panel.anchoredPosition = new Vector2(Mathf.Lerp(start.x, targetX, k), start.y);
                yield return null;
            }
            panel.anchoredPosition = new Vector2(targetX, start.y);
            _anims[panel] = null;
        }

        // Slides a panel's anchoredPosition to a target corner over a single smooth segment, WITHOUT
        // changing its size (the effect deck keeps its authored size).
        private void MoveBounceTo(RectTransform panel, Vector2 target)
        {
            if (panel == null) return;
            StopAnim(panel);
            _anims[panel] = StartCoroutine(MoveXY(panel, target));
        }

        private IEnumerator MoveXY(RectTransform panel, Vector2 target)
        {
            Vector2 start = panel.anchoredPosition;
            float distance = Vector2.Distance(target, start);
            float duration = Mathf.Max(minSegment, distance / Mathf.Max(1f, speed));

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                panel.anchoredPosition = Vector2.Lerp(start, target, k);
                yield return null;
            }
            panel.anchoredPosition = target;
            _anims[panel] = null;
        }

        private void StopAnim(RectTransform panel)
        {
            if (_anims.TryGetValue(panel, out Coroutine c) && c != null) StopCoroutine(c);
        }

        private static void SetY(RectTransform panel, float y)
        {
            if (panel == null) return;
            Vector2 p = panel.anchoredPosition;
            panel.anchoredPosition = new Vector2(p.x, y);
        }
    }
}
