using System.Collections;
using System.Collections.Generic;
using Undelivered.UI;
using UnityEngine;

namespace Undelivered.Work
{
    /// <summary>
    /// Spawns the boxes of a truck onto the table. Each box appears above the table and slides down
    /// into place, as if it just arrived. The shop calls <see cref="SpawnTruck"/> on purchase;
    /// trucks already owned at the start of the day are spawned on Start.
    /// </summary>
    public class TruckManager : MonoBehaviour
    {
        // "Centro de Distribución" upgrade: multiplies the boxes every truck brings.
        public static float BoxCountMultiplier = 1f;

        // The tutorial sets this in Awake so the initial boxes drop on cue (after the intro call) instead
        // of on Start. Awake runs before every Start, so the flag is set in time.
        public static bool SuppressAutoSpawn;

        [Tooltip("Trucks the player already owns at the start of the day. The shop spawns extra trucks at runtime.")]
        [SerializeField] private List<TruckData> purchasedTrucks = new List<TruckData>();

        [Tooltip("Box prefab variants (different shapes/sizes). One is picked at random per box so " +
                 "they don't all look the same. The choice is purely visual and independent of the " +
                 "box type. Each must have a Box component.")]
        [SerializeField] private Box[] boxPrefabs;

        [Tooltip("Prefab used for broken boxes (a broken duplicate of a box). Must have a Box component.")]
        [SerializeField] private Box brokenBoxPrefab;

        [Tooltip("Table (a RectTransform under the Canvas) where boxes are placed as children.")]
        [SerializeField] private RectTransform table;

        [Header("Entrance animation")]
        [Tooltip("Seconds each box takes to slide from above onto the table.")]
        [SerializeField] private float entranceDuration = 0.4f;

        [Tooltip("Delay between consecutive boxes so they arrive in a cascade.")]
        [SerializeField] private float entranceStagger = 0.05f;

        [Tooltip("Boxes delivered at the start of every day.")]
        [SerializeField] private int initialBoxCount = 5;

        private void Start()
        {
            if (!SuppressAutoSpawn) SpawnInitialBoxes();
        }

        /// <summary>Delivers the day's initial boxes (called on day start and each time the day mode is entered).</summary>
        public void SpawnInitialBoxes()
        {
            if (purchasedTrucks != null && purchasedTrucks.Count > 0 && purchasedTrucks[0] != null)
            {
                SpawnTruck(purchasedTrucks[0], initialBoxCount);
            }
        }

        /// <summary>Spawns all the boxes of a truck; each slides in from above onto the table.</summary>
        public void SpawnTruck(TruckData truck, int limit = -1)
        {
            if (truck == null) return;

            if (boxPrefabs == null || boxPrefabs.Length == 0 || table == null)
            {
                Debug.LogWarning($"{nameof(TruckManager)} needs at least one box prefab and a table assigned.", this);
                return;
            }

            bool bringsBroken = Random.value < truck.BrokenChance;

            int count = Mathf.Max(0, Mathf.RoundToInt(truck.TotalBoxes * BoxCountMultiplier));
            for (int i = 0; i < count; i++)
            {
                if (limit > 0 && i >= limit) { break; }

                bool broken = bringsBroken && brokenBoxPrefab != null && Random.value < truck.BrokenPortion;

                Box prefab = broken ? brokenBoxPrefab : boxPrefabs[Random.Range(0, boxPrefabs.Length)];
                if (prefab == null)
                {
                    continue; // empty slot in the array; skip
                }

                TruckData.BoxRoll roll = truck.RollBox();
                Box box = Instantiate(prefab, table, false);
                box.SetData(roll.type, roll.isDice, truck.RollBoxWeight());

                RectTransform boxRect = box.transform as RectTransform;
                Vector2 target = RandomTablePosition(boxRect);
                StartCoroutine(EnterFromAbove(boxRect, target, entranceStagger * i));
            }
        }

        // Random target fully inside the table.
        private Vector2 RandomTablePosition(RectTransform boxRect)
        {
            if (boxRect == null)
            {
                return Vector2.zero;
            }

            Vector2 tableSize = table.rect.size;
            Vector2 boxSize = boxRect.rect.size;
            float maxX = Mathf.Max(0f, (tableSize.x - boxSize.x) * 0.5f);
            float maxY = Mathf.Max(0f, (tableSize.y - boxSize.y) * 0.5f);
            return new Vector2(Random.Range(-maxX, maxX), Random.Range(-maxY, maxY));
        }

        // Slides the box from just above the table's top edge down to its target position.
        private IEnumerator EnterFromAbove(RectTransform boxRect, Vector2 target, float delay)
        {
            if (boxRect == null)
            {
                yield break;
            }

            // Disable dragging while it flies in so the player can't fight the animation.
            UIDraggable drag = boxRect.GetComponent<UIDraggable>();
            if (drag != null)
            {
                drag.enabled = false;
            }

            float topEdge = table.rect.height * 0.5f;
            Vector2 start = new Vector2(target.x, topEdge + boxRect.rect.height);
            boxRect.anchoredPosition = start;

            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            float elapsed = 0f;
            while (elapsed < entranceDuration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / entranceDuration));
                boxRect.anchoredPosition = Vector2.Lerp(start, target, k);
                yield return null;
            }
            boxRect.anchoredPosition = target;

            if (drag != null)
            {
                drag.enabled = true;
            }
        }
    }
}
