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
        }

        [Header("Slots")]
        [SerializeField] private RectTransform container;
        [SerializeField] private TurnSlot slotPrefab;
        [SerializeField] private float spacing = 20f;
        [SerializeField] private Color playerColor = new Color(0.25f, 0.75f, 0.68f);
        [SerializeField] private Color enemyColor = new Color(0.79f, 0.31f, 0.30f);

        [Header("Current-turn indicator")]
        [Tooltip("The gray bar that sits under the current slot (a sibling of the slots, not a child).")]
        [SerializeField] private RectTransform turnIndicator;
        [SerializeField] private float indicatorSpeed = 2500f;
        [Tooltip("Fixed vertical position of the indicator.")]
        [SerializeField] private float indicatorY = 430f;

        private readonly List<RectTransform> _slots = new List<RectTransform>();
        private float _indicatorTargetX;
        private bool _hasTarget;

        private void Awake()
        {
            ConfigureLayout();
            if (turnIndicator != null) turnIndicator.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_hasTarget || turnIndicator == null) return;
            Vector2 p = turnIndicator.anchoredPosition;
            p.x = Mathf.MoveTowards(p.x, _indicatorTargetX, indicatorSpeed * Time.unscaledDeltaTime);
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
            Build(participants);
        }

        /// <summary>Builds the bar from an explicit participant list.</summary>
        public void Build(List<Participant> participants)
        {
            if (container == null || slotPrefab == null) return;

            Clear();
            _slots.Clear();

            // Faster attacks first (left). Stable sort keeps input order on ties (player added first → wins ties).
            List<Participant> ordered = participants.OrderByDescending(p => p.speed).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                Participant p = ordered[i];
                TurnSlot slot = Instantiate(slotPrefab, container, false);
                slot.Setup(i + 1, p.sprite, p.isPlayer ? playerColor : enemyColor);
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

            _indicatorTargetX = LocalXUnder(_slots[index]);
            _hasTarget = true;
            if (snap)
            {
                Vector2 p = turnIndicator.anchoredPosition;
                p.x = _indicatorTargetX;
                p.y = indicatorY;
                turnIndicator.anchoredPosition = p;
            }
        }

        // The x of a slot's centre expressed in the indicator's own parent space.
        private float LocalXUnder(RectTransform slot)
        {
            RectTransform indicatorParent = turnIndicator.parent as RectTransform;
            if (indicatorParent == null) return turnIndicator.anchoredPosition.x;

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, slot.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(indicatorParent, screen, cam, out Vector2 local);
            return local.x;
        }

        private void ConfigureLayout()
        {
            if (container == null) return;
            HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = container.gameObject.AddComponent<HorizontalLayoutGroup>();
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
