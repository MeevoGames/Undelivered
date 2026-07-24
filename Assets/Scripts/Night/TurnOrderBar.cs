using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// Draws the turn order at the top: one slot per participant (the player + every enemy in the
    /// combat), laid out left→right by attack order (fastest first) and numbered 1..N. The player's
    /// slot is teal, enemies' are red. A gray indicator bar slides left/right to sit under the slot
    /// whose turn it currently is (<see cref="SetCurrentTurn"/>).
    /// </summary>
    public class TurnOrderBar : MonoBehaviour
    {
        public class Participant
        {
            public bool isPlayer;
            public int speed;
            public Sprite sprite;
            [Tooltip("Opens this combatant's detail window when its slot is tapped (optional).")]
            public System.Action onSelected;
        }

        [Header("Slots")]
        [SerializeField] private RectTransform container;
        [SerializeField] private TurnSlot slotPrefab;
        [SerializeField] private float spacing = 20f;

        [Header("Current-turn indicator")]
        [Tooltip("The gray bar that sits under the current slot (a sibling of the slots, not a child).")]
        [SerializeField] private RectTransform turnIndicator;
        [SerializeField] private float indicatorSpeed = 2500f;
        [Tooltip("Fixed vertical position of the indicator.")]
        [SerializeField] private float indicatorY = 430f;
        [Tooltip("How much faster the indicator rushes during the extra-turn loop.")]
        [SerializeField] private float extraTurnSpeedMultiplier = 3f;

        private readonly List<RectTransform> _slots = new List<RectTransform>();
        private int _currentIndex = -1; // which slot the indicator tracks (its X is recomputed live)
        private bool _hasTarget;
        private Coroutine _loop;

        private void Awake()
        {
            ConfigureLayout();
            if (turnIndicator != null) turnIndicator.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (turnIndicator == null || _loop != null) return; // the extra-turn loop drives the indicator itself
            if (!_hasTarget || _currentIndex < 0 || _currentIndex >= _slots.Count) return;

            // Recompute the target every frame so the indicator glides to the slot even while a
            // ContentSizeFitter / GridLayoutGroup is still settling the slot positions.
            Vector2 p = turnIndicator.anchoredPosition;
            p.x = Mathf.MoveTowards(p.x, TargetXFor(_slots[_currentIndex]), indicatorSpeed * Time.unscaledDeltaTime);
            p.y = indicatorY;
            turnIndicator.anchoredPosition = p;
        }

        /// <summary>Builds the bar from a combat plus the player's speed and portrait.</summary>
        public void Build(EncounterData encounter, int playerSpeed, Sprite playerSprite)
        {
            var participants = new List<Participant>
            {
                new Participant { isPlayer = true, speed = playerSpeed, sprite = playerSprite }
            };
            if (encounter != null)
            {
                foreach (EncounterData.Enemy e in encounter.Enemies)
                {
                    if (e == null || e.enemy == null) continue;
                    participants.Add(new Participant { isPlayer = false, speed = e.enemy.Speed, sprite = e.enemy.Sprite });
                }
            }
            Build(participants.OrderByDescending(p => p.speed).ToList()); // faster attacks first (left)
        }

        /// <summary>Builds the bar from a participant list, shown in the exact order given (no re-sort).</summary>
        public void Build(List<Participant> participants)
        {
            if (container == null || slotPrefab == null) return;

            Clear();
            _slots.Clear();
            _currentIndex = -1;
            _hasTarget = false;

            for (int i = 0; i < participants.Count; i++)
            {
                Participant p = participants[i];
                TurnSlot slot = Instantiate(slotPrefab, container, false);
                slot.Setup(i + 1, p.sprite, p.isPlayer); // the slot picks its own background sprite
                slot.SetClick(p.onSelected);             // tapping it opens that combatant's detail
                _slots.Add((RectTransform)slot.transform);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(container); // so slot positions are ready
            if (turnIndicator != null) turnIndicator.gameObject.SetActive(_slots.Count > 0);
            SetCurrentTurn(0, snap: true);
        }

        /// <summary>Moves the indicator under the slot at this attack-order index (0-based).</summary>
        public void SetCurrentTurn(int index, bool snap = false)
        {
            if (index < 0 || index >= _slots.Count || turnIndicator == null) return;

            _currentIndex = index;
            _hasTarget = true;
            if (snap)
            {
                Vector2 p = turnIndicator.anchoredPosition;
                p.x = TargetXFor(_slots[index]);
                p.y = indicatorY;
                turnIndicator.anchoredPosition = p;
            }
        }

        /// <summary>
        /// Extra-turn feedback: the indicator rushes to the last slot and back to the player, as if a whole
        /// round passed with nobody acting and it's your turn again.
        /// </summary>
        public void PlayExtraTurnLoop(int playerIndex)
        {
            if (turnIndicator == null || _slots.Count == 0) return;
            if (_loop != null) StopCoroutine(_loop);
            _loop = StartCoroutine(ExtraTurnLoop(playerIndex));
        }

        private IEnumerator ExtraTurnLoop(int playerIndex)
        {
            _hasTarget = false; // take over from the Update-driven movement
            float speed = indicatorSpeed * extraTurnSpeedMultiplier;
            int last = _slots.Count - 1;

            yield return MoveIndicatorTo(TargetXFor(_slots[last]), speed);                              // rush to the last slot
            yield return MoveIndicatorTo(TargetXFor(_slots[Mathf.Clamp(playerIndex, 0, last)]), speed);  // back to you

            SetCurrentTurn(playerIndex, snap: true); // resume normal targeting on the player
            _loop = null;
        }

        private IEnumerator MoveIndicatorTo(float x, float speed)
        {
            while (Mathf.Abs(turnIndicator.anchoredPosition.x - x) > 0.5f)
            {
                Vector2 p = turnIndicator.anchoredPosition;
                p.x = Mathf.MoveTowards(p.x, x, speed * Time.unscaledDeltaTime);
                p.y = indicatorY;
                turnIndicator.anchoredPosition = p;
                yield return null;
            }
        }

        // The anchoredPosition.x that puts the indicator under a slot's centre — robust to the
        // indicator's anchors/pivot and to the slot living under a different parent (e.g. the grid).
        private float TargetXFor(RectTransform slot)
        {
            RectTransform parent = turnIndicator.parent as RectTransform;
            if (parent == null || slot == null) return turnIndicator.anchoredPosition.x;

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, slot.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out Vector2 local))
                return turnIndicator.anchoredPosition.x;

            // 'local' is relative to the parent's pivot; convert it to an anchoredPosition for the
            // indicator's anchor (so it's correct whatever the indicator is anchored to).
            Rect pr = parent.rect;
            float anchorMidX = (turnIndicator.anchorMin.x + turnIndicator.anchorMax.x) * 0.5f;
            float anchorRefX = pr.x + anchorMidX * pr.width;
            return local.x - anchorRefX;
        }

        private void ConfigureLayout()
        {
            if (container == null) return;

            // The slots are laid out by whatever LayoutGroup the container already has (a GridLayoutGroup
            // in the scene). Never add a second one — Unity forbids it and AddComponent would return null,
            // which then NREs. Only add a HorizontalLayoutGroup as a fallback when there is no group at all.
            if (container.GetComponent<LayoutGroup>() != null) return;

            HorizontalLayoutGroup layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private void Clear()
        {
            for (int i = container.childCount - 1; i >= 0; i--) Destroy(container.GetChild(i).gameObject);
        }
    }
}
