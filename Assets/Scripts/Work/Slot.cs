using Undelivered.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.Work
{
    /// <summary>
    /// A destination slot (International / National / Local ...). Any box can be dropped on it.
    /// On drop it compares the box type with the slot type: a correct match pays the box's gold,
    /// a mismatch costs half of it (rounded up). Either way the box is consumed, the player's gold
    /// is updated (it may go negative), and the day stats are recorded. The floating gold text is
    /// spawned by StatsToHUD in reaction to the gold change.
    ///
    /// Requires a raycast-target Graphic (e.g. an Image with Raycast Target on) so drops register.
    /// </summary>
    public class Slot : MonoBehaviour, IDropHandler
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

            int baseReward = BoxManager.Instance != null ? BoxManager.Instance.GetGoldReward(box.Type) : 0;

            int delta;
            if (box.Type == slotType)
            {
                // A stamped box pays a bonus when correctly delivered (x1.5 manual, +2 flat auto).
                int reward = box.HasStamp
                    ? Mathf.RoundToInt(baseReward * Box.StampRewardMultiplier) + Box.StampRewardFlat
                    : baseReward;
                delta = reward;
                Debug.Log($"+{reward} oro — caja {box.Type} clasificada correctamente en slot {slotType}{(box.HasStamp ? " (sellada x1.5)" : "")}.");
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

            Destroy(box.gameObject); // the box is consumed whether correct or not
        }
    }
}
