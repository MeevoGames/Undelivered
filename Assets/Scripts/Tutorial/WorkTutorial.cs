using System.Collections;
using TMPro;
using Undelivered.Player;
using Undelivered.Shop;
using Undelivered.UI;
using Undelivered.Upgrades;
using Undelivered.Work;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Tutorial
{
    /// <summary>
    /// Drives the scripted first-day tutorial for the packaging mode: a truck arrives, the friend calls
    /// (subtitles), the first boxes drop, one blinks, inspecting it blinks its destination slot, and from
    /// there the flow guides the player through ordering boxes, buying a truck, buying the Etiquetadora
    /// upgrade and meeting the day's quota. Each step highlights the element to use (<see cref="TutorialHighlight"/>)
    /// and waits for the matching player action via events on the day-mode systems.
    /// </summary>
    public class WorkTutorial : MonoBehaviour
    {
        [Tooltip("Turn the tutorial off to play a normal day (boxes then spawn on Start as usual).")]
        [SerializeField] private bool runTutorial = true;

        [Header("Step 1 — truck arrives")]
        [Tooltip("Full-screen black overlay (needs a CanvasGroup) shown until the phone rings. Start it ACTIVE with alpha 1.")]
        [SerializeField] private GameObject blackScreen;
        [Tooltip("Seconds the black screen takes to fade out (its CanvasGroup alpha to 0) when the phone rings.")]
        [SerializeField] private float blackFadeSeconds = 0.8f;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private AudioClip truckArriveClip;
        [SerializeField] private float afterTruckSeconds = 1.5f;

        [Header("Phone call")]
        [SerializeField] private PhoneCall phone;
        [Tooltip("The friend's intro message (steps 2-5), one subtitle per line.")]
        [SerializeField, TextArea] private string[] introLines =
        {
            "Hey amigo, pude conseguirte el trabajo asi que no me falles.",
            "Es facil: revisa los paquetes y envialos a donde toca.",
            "Si no cumplis la cuota del dia, pedi mas camiones para llegar sobrado.",
            "Nos vemos a la noche para el juego!"
        };
        [Tooltip("The friend's later message about the labeler (step 12).")]
        [SerializeField, TextArea] private string[] labelLines =
        {
            "Hey bro, quizas quieras gastar un poco en etiquetar los paquetes para trabajar mas rapido.",
            "Hay mas opciones, pero con esa ya deberias ir flama."
        };

        [Header("Boxes (steps 6-9)")]
        [SerializeField] private TruckManager truckManager;
        [Tooltip("The destination the first blinking box is forced to (its slot is the one that blinks).")]
        [SerializeField] private BoxType tutorialBoxType = BoxType.National;
        [SerializeField] private float boxSettleSeconds = 0.7f;

        [Header("Delivery hint (step 8)")]
        [Tooltip("A sprite that repeatedly flies from the box to its destination slot until the player drags the box.")]
        [SerializeField] private RectTransform deliveryHintPrefab;
        [SerializeField] private float hintMoveSeconds = 0.8f;
        [SerializeField] private float hintHideSeconds = 0.15f;

        [Header("Truck shop (steps 9-11)")]
        [Tooltip("The left truck-shop panel; hidden until the first box is delivered.")]
        [SerializeField] private GameObject truckShopPanel;
        [SerializeField] private TruckShop truckShop;
        [Tooltip("The truck the player is guided to buy — its price is the gold the player must earn first.")]
        [SerializeField] private TruckData tutorialTruck;

        [Header("Upgrades (steps 12-14)")]
        [Tooltip("The 'Mejoras' tab button.")]
        [SerializeField] private Button mejorasButton;
        [Tooltip("The upgrades tab content — used to detect the tab was opened.")]
        [SerializeField] private GameObject upgradesContent;
        [SerializeField] private UpgradeShop upgradeShop;
        [Tooltip("The Etiquetadora upgrade the player is guided to buy.")]
        [SerializeField] private UpgradeData etiquetadora;

        [Header("Quota met (step 16)")]
        [SerializeField] private TMP_Text quotaText;
        [SerializeField] private Color quotaMetColor = new Color(0.34f, 0.75f, 0.30f); // #58BE4E
        [Tooltip("A small 'quota met' tooltip/popup, shown when the quota is reached.")]
        [SerializeField] private GameObject quotaMetTooltip;
        [SerializeField] private Button finishDayButton;

        // Event flags set by the day-mode systems.
        private bool _delivered;
        private bool _truckBought;
        private bool _upgradeBought;
        private bool _quotaReached;
        private Box _inspectedBox;
        private bool _subscribed;

        private Coroutine _hintLoop;
        private GameObject _hintInstance;

        private void Awake()
        {
            // Awake runs before every Start, so this reliably holds back TruckManager's Start-spawn.
            TruckManager.SuppressAutoSpawn = runTutorial;
        }

        private void Start()
        {
            if (!runTutorial)
            {
                if (blackScreen != null) blackScreen.SetActive(false); // no tutorial → never leave the scene black
                return;
            }
            Subscribe();
            StartCoroutine(RunTutorial());
        }

        private void OnDestroy() => Unsubscribe();

        // ----- the flow -----

        private IEnumerator RunTutorial()
        {
            ShowBlackScreen(); // the screen is black until the phone rings
            if (truckShopPanel != null) truckShopPanel.SetActive(false);
            if (quotaMetTooltip != null) quotaMetTooltip.SetActive(false);

            // 1. A truck arrives and you clock in (still on the black screen).
            PlaySfx(truckArriveClip);
            yield return new WaitForSeconds(afterTruckSeconds);

            // 2-5. The phone rings (the screen fades in), you answer, the friend talks over the typed
            // subtitles, then hangs up. The fade runs alongside the ring.
            StartCoroutine(FadeOutBlackScreen());
            if (phone != null) yield return phone.PlayCall(introLines);

            // 6. The first boxes fall.
            if (truckManager != null) truckManager.SpawnInitialBoxes();
            yield return null; // let them instantiate
            yield return new WaitForSeconds(boxSettleSeconds);

            // 7. One box blinks — bring it to the front so the blink is visible over the pile, and force
            // its destination so its slot is deterministic.
            Box box = FirstBox();
            if (box != null)
            {
                box.transform.SetAsLastSibling(); // render above the other boxes
                box.Type = tutorialBoxType;
                box.RefreshLabels();
                Blink(box.gameObject);
            }

            // 8. Touch it to see the address; then its destination slot blinks and a hint sprite flies
            // from the box to the slot on a loop until the player drags the box.
            if (box != null) yield return new WaitUntil(() => _inspectedBox == box || box == null);
            StopBlink(box != null ? box.gameObject : null);
            Slot slot = FindSlot(tutorialBoxType);
            if (slot != null) Blink(slot.gameObject);
            StartDeliveryHint(box, slot);

            // 9. Delivering it reveals the (unaffordable) truck shop on the left.
            _delivered = false;
            yield return new WaitUntil(() => _delivered || box == null);
            StopDeliveryHint();
            if (slot != null) StopBlink(slot.gameObject);
            if (truckShopPanel != null) truckShopPanel.SetActive(true);

            // -- the player orders the rest until they can afford the truck --
            int halfTruckTarget = -1; // deliveries (since the buy) after which the labeler call fires
            int deliveriesAtBuy = 0;
            if (tutorialTruck != null && truckShop != null)
            {
                // 10. Once there's enough gold, the truck's buy button lights up and blinks.
                int price = tutorialTruck.Price;
                yield return new WaitUntil(() => Gold() >= price);
                Button truckBuy = ButtonFor(truckShop.FindItem(tutorialTruck));
                if (truckBuy != null) Blink(truckBuy.gameObject);

                // 11. Buy it (it drops a new batch of boxes), then order those.
                _truckBought = false;
                yield return new WaitUntil(() => _truckBought);
                if (truckBuy != null) StopBlink(truckBuy.gameObject);

                deliveriesAtBuy = Deliveries();
                int truckBoxes = Mathf.Max(0, Mathf.RoundToInt(tutorialTruck.TotalBoxes * TruckManager.BoxCountMultiplier));
                halfTruckTarget = Mathf.Max(1, Mathf.CeilToInt(truckBoxes / 2f));
            }

            // 12. Once half the bought truck's boxes are delivered, the friend calls about labeling.
            if (halfTruckTarget > 0)
                yield return new WaitUntil(() => Deliveries() - deliveriesAtBuy >= halfTruckTarget);
            else
                yield return WaitForTableClear(); // fallback if no tutorial truck is configured
            if (phone != null) yield return phone.PlayCall(labelLines);

            // 13. The "Mejoras" button blinks.
            if (mejorasButton != null) Blink(mejorasButton.gameObject);

            // 14. Open the upgrades tab, then the Etiquetadora buy button blinks; buying it labels some boxes.
            if (etiquetadora != null && upgradeShop != null)
            {
                yield return new WaitUntil(() => upgradesContent != null && upgradesContent.activeInHierarchy);
                if (mejorasButton != null) StopBlink(mejorasButton.gameObject);

                Button upgradeBuy = ButtonFor(upgradeShop.FindItem(etiquetadora));
                if (upgradeBuy != null) Blink(upgradeBuy.gameObject);

                _upgradeBought = false;
                yield return new WaitUntil(() => _upgradeBought);
                if (upgradeBuy != null) StopBlink(upgradeBuy.gameObject);
            }

            // 15-16. Keep ordering until the quota is met: the number turns green, a tooltip shows and
            // the Finish-Day button lights up (DayManager already enabled it).
            yield return new WaitUntil(() => _quotaReached || QuotaMet());
            if (quotaText != null) quotaText.color = quotaMetColor;
            if (quotaMetTooltip != null) quotaMetTooltip.SetActive(true);
            if (finishDayButton != null) Blink(finishDayButton.gameObject);
        }

        // ----- events -----

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            if (DayManager.Instance != null)
            {
                DayManager.Instance.DeliveryRegistered += OnDelivery;
                DayManager.Instance.QuotaReached += OnQuota;
            }
            if (truckShop != null) truckShop.Bought += OnTruckBought;
            if (upgradeShop != null) upgradeShop.Bought += OnUpgradeBought;
            BoxInspector.Shown += OnBoxShown;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            if (DayManager.Instance != null)
            {
                DayManager.Instance.DeliveryRegistered -= OnDelivery;
                DayManager.Instance.QuotaReached -= OnQuota;
            }
            if (truckShop != null) truckShop.Bought -= OnTruckBought;
            if (upgradeShop != null) upgradeShop.Bought -= OnUpgradeBought;
            BoxInspector.Shown -= OnBoxShown;
        }

        private void OnDelivery(BoxType type) => _delivered = true;
        private void OnQuota() => _quotaReached = true;
        private void OnTruckBought(TruckData truck) { if (truck == tutorialTruck) _truckBought = true; }
        private void OnUpgradeBought(UpgradeData upgrade) { if (upgrade == etiquetadora) _upgradeBought = true; }
        private void OnBoxShown(Box box) => _inspectedBox = box;

        // ----- helpers -----

        private IEnumerator WaitForTableClear()
        {
            yield return new WaitUntil(() => BoxCount() == 0);
        }

        // Loops a hint sprite from the box to its destination slot until the player drags the box.
        private void StartDeliveryHint(Box box, Slot slot)
        {
            StopDeliveryHint();
            if (deliveryHintPrefab == null || box == null || slot == null) return;
            _hintLoop = StartCoroutine(DeliveryHintLoop(box, slot));
        }

        private void StopDeliveryHint()
        {
            if (_hintLoop != null) { StopCoroutine(_hintLoop); _hintLoop = null; }
            if (_hintInstance != null) { Destroy(_hintInstance); _hintInstance = null; }
        }

        private IEnumerator DeliveryHintLoop(Box box, Slot slot)
        {
            // Parent to the box's canvas and render on top so the hint is always visible.
            Canvas canvas = box.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : box.transform.parent;
            RectTransform hint = Instantiate(deliveryHintPrefab, parent, false);
            hint.SetAsLastSibling();
            _hintInstance = hint.gameObject;

            UIDraggable drag = box.GetComponent<UIDraggable>();
            float move = Mathf.Max(0.05f, hintMoveSeconds);

            while (box != null && (drag == null || !drag.IsDragging))
            {
                hint.gameObject.SetActive(true);
                Vector3 from = box.transform.position;
                Vector3 to = slot.transform.position;

                for (float t = 0f; t < move && box != null && (drag == null || !drag.IsDragging); t += Time.deltaTime)
                {
                    hint.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / move));
                    yield return null;
                }

                hint.gameObject.SetActive(false);
                yield return new WaitForSeconds(hintHideSeconds);
            }

            if (_hintInstance != null) { Destroy(_hintInstance); _hintInstance = null; }
            _hintLoop = null;
        }

        private void Blink(GameObject go)
        {
            if (go == null) return;
            TutorialHighlight h = go.GetComponent<TutorialHighlight>();
            if (h == null) h = go.AddComponent<TutorialHighlight>();
            h.Play();
        }

        private void StopBlink(GameObject go)
        {
            if (go == null) return;
            TutorialHighlight h = go.GetComponent<TutorialHighlight>();
            if (h != null) h.Stop();
        }

        private void PlaySfx(AudioClip clip)
        {
            if (sfx != null && clip != null) sfx.PlayOneShot(clip);
        }

        private void ShowBlackScreen()
        {
            if (blackScreen == null) return;
            blackScreen.SetActive(true);
            CanvasGroup cg = blackScreen.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }
        }

        // Fades the black screen's CanvasGroup alpha to 0, then deactivates it.
        private IEnumerator FadeOutBlackScreen()
        {
            if (blackScreen == null) yield break;

            CanvasGroup cg = blackScreen.GetComponent<CanvasGroup>();
            if (cg == null) { blackScreen.SetActive(false); yield break; } // no group → just hide

            cg.blocksRaycasts = false; // let the player interact while it fades
            float start = cg.alpha;
            for (float t = 0f; t < blackFadeSeconds; t += Time.deltaTime)
            {
                cg.alpha = Mathf.Lerp(start, 0f, t / blackFadeSeconds);
                yield return null;
            }
            cg.alpha = 0f;
            blackScreen.SetActive(false);
        }

        private static Box FirstBox()
        {
            Box[] boxes = FindObjectsByType<Box>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return boxes.Length > 0 ? boxes[0] : null;
        }

        private static Slot FindSlot(BoxType type)
        {
            foreach (Slot slot in FindObjectsByType<Slot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (slot != null && slot.SlotType == type) return slot;
            return null;
        }

        private static Button ButtonFor(TruckShopItem item) => item != null ? item.BuyButton : null;
        private static Button ButtonFor(UpgradeShopItem item) => item != null ? item.BuyButton : null;

        private static int BoxCount() => FindObjectsByType<Box>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        private static int Gold() => StatsManager.Instance != null ? StatsManager.Instance.Gold : 0;
        private static int Deliveries() => DayManager.Instance != null ? DayManager.Instance.CorrectDeliveries : 0;
        private static bool QuotaMet() => DayManager.Instance != null && DayManager.Instance.QuotaMet;
    }
}
