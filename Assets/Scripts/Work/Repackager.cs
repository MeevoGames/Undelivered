using Undelivered.Player;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Undelivered.Work
{
    /// <summary>
    /// Repackaging station (reempaquetadora): drop a broken box on it to repair it for a small gold
    /// cost. The broken box is replaced by a fixed prefab that keeps its data (type, dice, weight,
    /// labels, stamp) but is no longer broken. Enabled by the Reempaquetado upgrade; also used by the
    /// Control de Calidad arm (which calls <see cref="Repair"/> directly).
    ///
    /// Requires a raycast-target Graphic so drops register.
    /// </summary>
    public class Repackager : MonoBehaviour, IDropHandler
    {
        [Tooltip("Fixed (not broken) box prefab a repaired box becomes. Must have a Box component.")]
        [SerializeField] private Box fixedBoxPrefab;

        [Tooltip("Gold spent to repackage one box.")]
        [SerializeField] private int cost = 2;

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerDrag == null)
            {
                return;
            }

            Box box = eventData.pointerDrag.GetComponent<Box>();
            Repair(box);
        }

        /// <summary>
        /// Repairs a broken box (spends gold, replaces it with the fixed prefab keeping its data at the
        /// same spot) and returns the new box, or null if it can't (not broken, no prefab, no gold).
        /// </summary>
        public Box Repair(Box box)
        {
            if (box == null || !box.IsBroken)
            {
                return null;
            }
            if (fixedBoxPrefab == null)
            {
                Debug.LogWarning($"{nameof(Repackager)} has no fixed box prefab assigned.", this);
                return null;
            }

            int gold = StatsManager.Instance != null ? StatsManager.Instance.Gold : 0;
            if (gold < cost)
            {
                Debug.LogWarning($"No alcanza para reempaquetar (cuesta {cost}, oro {gold}).");
                return null;
            }

            if (StatsManager.Instance != null)
            {
                StatsManager.Instance.AddGold(-cost);
            }

            RectTransform brokenRect = box.transform as RectTransform;
            Box fixedBox = Instantiate(fixedBoxPrefab, box.transform.parent, false);
            fixedBox.CopyStateFrom(box);

            if (brokenRect != null && fixedBox.transform is RectTransform fixedRect)
            {
                fixedRect.anchoredPosition = brokenRect.anchoredPosition;
            }

            Destroy(box.gameObject);
            return fixedBox;
        }
    }
}
