using Undelivered.Items;
using Undelivered.Night;
using Undelivered.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.Work
{
    /// <summary>
    /// Slot that opens a box dropped on it: it costs trust (more if the box carries dice), gives a
    /// random amount of gold (or a die, logged for now, for dice boxes), spawns a <see cref="Trash"/>
    /// item (the empty box) on the table, and consumes the original box.
    ///
    /// Requires a raycast-target Graphic (e.g. an Image with Raycast Target on) so drops register.
    /// </summary>
    public class OpenPackageSlot : MonoBehaviour, IDropHandler
    {
        [Tooltip("Trash item spawned when a box is opened (the empty box). Must have a Trash component.")]
        [SerializeField] private Trash trashPrefab;

        [Tooltip("Parent (the table) the trash is spawned under.")]
        [SerializeField] private RectTransform trashParent;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
            {
                return;
            }

            Box box = eventData.pointerDrag.GetComponent<Box>();
            if (box == null)
            {
                // Trash thrown at the open slot (not the paper bin) costs gold.
                Trash trash = eventData.pointerDrag.GetComponent<Trash>();
                if (trash != null)
                {
                    trash.DiscardInWrongSlot();
                }
                return; // only boxes can be opened
            }

            BoxManager manager = BoxManager.Instance;

            // Opening costs trust (dice boxes cost more), unless the trust-protection upgrade saves it.
            int trustCost = manager != null ? manager.GetTrustCost(box.IsDice) : 0;
            bool trustProtected = Random.value < StatsManager.TrustLossProtection;
            if (!trustProtected)
            {
                if (StatsManager.Instance != null)
                {
                    StatsManager.Instance.AddTrust(-trustCost);
                }
                if (DayManager.Instance != null)
                {
                    DayManager.Instance.RegisterTrustLost(trustCost);
                }
            }

            // Reward: dice boxes give a random night item (die / effect / box); others give random gold.
            if (box.IsDice)
            {
                IItem item = ItemsManager.Instance != null ? ItemsManager.Instance.GrantRandomItem() : null;
                if (item != null && DayManager.Instance != null)
                {
                    DayManager.Instance.RegisterItemObtained();
                }
            }
            else
            {
                int gold = manager != null ? manager.RollOpenGold(box.Type, box.Weight) : 0;
                if (StatsManager.Instance != null)
                {
                    StatsManager.Instance.AddGold(gold);
                }
            }

            SpawnTrash();
            Destroy(box.gameObject);
        }

        private void SpawnTrash()
        {
            if (trashPrefab == null || trashParent == null)
            {
                Debug.LogWarning($"{nameof(OpenPackageSlot)} needs a trash prefab and parent assigned.", this);
                return;
            }

            Trash trash = Instantiate(trashPrefab, trashParent, false);
            if (trash.transform is RectTransform rect)
            {
                Vector2 parentSize = trashParent.rect.size;
                Vector2 trashSize = rect.rect.size;
                float maxX = Mathf.Max(0f, (parentSize.x - trashSize.x) * 0.5f);
                float maxY = Mathf.Max(0f, (parentSize.y - trashSize.y) * 0.5f);
                rect.anchoredPosition = new Vector2(Random.Range(-maxX, maxX), Random.Range(-maxY, maxY));
            }
        }
    }
}
