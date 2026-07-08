using Undelivered.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.Work
{
    /// <summary>
    /// A destination slot (International / National / Local ...). A box dropped (or delivered by an
    /// automation) is classified: a correct match pays its gold (broken boxes pay half, stamped boxes
    /// a bonus), a mismatch costs half. Either way the box is consumed. Clicking the slot delivers all
    /// boxes the forklift is carrying. Requires a raycast-target Graphic so drops/clicks register.
    /// </summary>
    public class Slot : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [SerializeField] private BoxType slotType;

        public BoxType SlotType => slotType;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
            {
                return;
            }

            Box box = eventData.pointerDrag.GetComponent<Box>();
            if (box == null)
            {
                // Trash thrown at a classify slot (not the paper bin) costs gold.
                Trash trash = eventData.pointerDrag.GetComponent<Trash>();
                if (trash != null)
                {
                    trash.DiscardInWrongSlot();
                }
                return;
            }

            Deliver(box);
        }

        // Right-clicking a slot while the forklift is carrying boxes delivers them all here.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right
                && Forklift.Instance != null && Forklift.Instance.HasBoxes)
            {
                Forklift.Instance.DeliverAllTo(this);
            }
        }

        /// <summary>Classifies and consumes a box in this slot (used by drops, forklift and the sorting arm).</summary>
        public void Deliver(Box box)
        {
            if (box == null)
            {
                return;
            }

            int baseReward = BoxManager.Instance != null ? BoxManager.Instance.GetGoldReward(box.Type) : 0;
            if (box.IsBroken)
            {
                baseReward = Mathf.RoundToInt(baseReward * 0.5f); // a broken (fallado) box pays half
            }

            int delta;
            if (box.Type == slotType)
            {
                int reward = box.HasStamp
                    ? Mathf.RoundToInt(baseReward * Box.StampRewardMultiplier) + Box.StampRewardFlat
                    : baseReward;
                delta = reward;
                Debug.Log($"+{reward} oro — caja {box.Type} clasificada correctamente en slot {slotType}.");
                if (DayManager.Instance != null)
                {
                    DayManager.Instance.RegisterCorrectDelivery(box.Type, reward);
                }
            }
            else
            {
                int penalty = Mathf.CeilToInt(baseReward / 2f);
                delta = -penalty;
                Debug.LogWarning($"-{penalty} oro — caja {box.Type} mal clasificada en slot {slotType}.");
                if (DayManager.Instance != null)
                {
                    DayManager.Instance.RegisterIncorrectDelivery(penalty);
                }
            }

            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.AddGold(delta);
            }

            Destroy(box.gameObject);
        }
    }
}
