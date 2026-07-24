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

        [Header("Fallback calls (only if the player gets into trouble)")]
        [Tooltip("The friend calls once after this many boxes are misclassified.")]
        [SerializeField] private int badSortingThreshold = 2;
        [SerializeField, TextArea] private string[] badSortingLines =
        {
            "Ey! El jefe me avisó que los paquetes no se están enviando bien.",
            "Revisá a donde tienen que ir antes de mandarlos así nomás.",
            "Me haces quedar mal."
        };
        [Tooltip("Played once if the day is stuck: quota unmet, no boxes left and no affordable truck in stock.")]
        [SerializeField, TextArea] private string[] stuckLines =
        {
            "Hey Bro te vas a tener que esforzar un poco más, por ahora te tiro una ayuda y te mando un camión gratis pero tenes que salir adelante solo máquina."
        };
        [Tooltip("The free truck sent as a rescue after that call (the first type, Voltra).")]
        [SerializeField] private TruckData rescueTruck;

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

        [Header("Select hint (sits beside a blinking element and pulses)")]
        [Tooltip("Sprite placed at the bottom-left of the blinking truck / Mejoras / Etiquetadora, pulsing until it is clicked.")]
        [SerializeField] private RectTransform selectHintPrefab;
        [Tooltip("Offset from the target's bottom-left corner.")]
        [SerializeField] private Vector2 selectHintOffset;

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
        private GameObject _selectHint;
        private Coroutine _selectHintFollow;
        private bool _phoneBusy; // only one call at a time — fallbacks wait their turn

        private void Awake()
        {
            // Awake runs before every Start, so this reliably holds back TruckManager's Start-spawn.
            TruckManager.SuppressAutoSpawn = runTutorial;
            // Upgrades stay unbuyable until the tutorial introduces them (step 13).
            UpgradeShop.PurchasesLocked = runTutorial;
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
            StartCoroutine(WatchMistakes()); // these two run alongside the scripted flow
            StartCoroutine(WatchStuck());
        }

        private void OnDestroy() => Unsubscribe();

        /// <summary>Debug ("jumptuto"): abort the day tutorial and leave a playable table.</summary>
        public void Skip()
        {
            StopAllCoroutines();
            StopDeliveryHint();
            HideSelectHint();
            TutorialHighlight.StopAll();
            if (phone != null) phone.Cancel();
            if (blackScreen != null) blackScreen.SetActive(false);          // reveal
            if (truckManager != null && BoxCount() == 0) truckManager.SpawnInitialBoxes(); // make sure there are boxes

            // The tutorial is over, so upgrades are buyable again.
            if (upgradeShop != null) upgradeShop.SetPurchasesLocked(false);
            else UpgradeShop.PurchasesLocked = false;
        }

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
            yield return Call(introLines);

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
                if (truckBuy != null) { Blink(truckBuy.gameObject); ShowSelectHint(truckBuy.gameObject); }

                // 11. Buy it (it drops a new batch of boxes), then order those.
                _truckBought = false;
                yield return new WaitUntil(() => _truckBought);
                if (truckBuy != null) StopBlink(truckBuy.gameObject);
                HideSelectHint();

                deliveriesAtBuy = Deliveries();
                int truckBoxes = Mathf.Max(0, Mathf.RoundToInt(tutorialTruck.TotalBoxes * TruckManager.BoxCountMultiplier));
                halfTruckTarget = Mathf.Max(1, Mathf.CeilToInt(truckBoxes / 2f));
            }

            // 12. Once half the bought truck's boxes are delivered, the friend calls about labeling.
            if (halfTruckTarget > 0)
                yield return new WaitUntil(() => Deliveries() - deliveriesAtBuy >= halfTruckTarget);
            else
                yield return WaitForTableClear(); // fallback if no tutorial truck is configured
            yield return Call(labelLines); // Call() blocks the boxes for as long as the panel is up

            // 13. Now that the friend has mentioned them, upgrades become buyable; "Mejoras" blinks.
            if (upgradeShop != null) upgradeShop.SetPurchasesLocked(false);
            if (mejorasButton != null) { Blink(mejorasButton.gameObject); ShowSelectHint(mejorasButton.gameObject); }

            // 14. Open the upgrades tab, then the Etiquetadora buy button blinks; buying it labels the boxes.
            if (etiquetadora != null && upgradeShop != null)
            {
                yield return new WaitUntil(() => upgradesContent != null && upgradesContent.activeInHierarchy);
                if (mejorasButton != null) StopBlink(mejorasButton.gameObject);
                HideSelectHint();

                // Mark the buy button; if the row prefab has none wired, fall back to the whole row.
                UpgradeShopItem row = upgradeShop.FindItem(etiquetadora);
                GameObject upgradeTarget = row == null ? null
                    : (row.BuyButton != null ? row.BuyButton.gameObject : row.gameObject);
                if (upgradeTarget != null) { Blink(upgradeTarget); ShowSelectHint(upgradeTarget); }

                _upgradeBought = false;
                yield return new WaitUntil(() => _upgradeBought);
                if (upgradeTarget != null) StopBlink(upgradeTarget);
                HideSelectHint();

                LabelAllBoxesButOne(); // every box on the table gets a label except one
            }

            // 15-16. Keep ordering until the quota is met: the number turns green, a tooltip shows and
            // the Finish-Day button lights up (DayManager already enabled it).
            yield return new WaitUntil(() => _quotaReached || QuotaMet());
            if (quotaText != null) quotaText.color = quotaMetColor;
            if (quotaMetTooltip != null) quotaMetTooltip.SetActive(true);
            if (finishDayButton != null) Blink(finishDayButton.gameObject);
        }

        // ----- fallback calls -----

        // Every call goes through here so a fallback never talks over the scripted flow.
        private IEnumerator Call(string[] lines)
        {
            if (phone == null || lines == null || lines.Length == 0) yield break;
            yield return new WaitUntil(() => !_phoneBusy);

            _phoneBusy = true;
            SetBoxesInteractive(false);  // no handling boxes while a message is being read
            yield return phone.PlayCall(lines);
            SetBoxesInteractive(true);   // back as soon as the phone panel is gone
            _phoneBusy = false;
        }

        // Once the player has misclassified enough boxes, the friend calls to tell them off. Once a day.
        private IEnumerator WatchMistakes()
        {
            yield return new WaitUntil(() => Incorrect() >= Mathf.Max(1, badSortingThreshold));
            yield return Call(badSortingLines);
        }

        // If the day becomes unwinnable — quota unmet, nothing left on the table and no truck they can
        // afford — the friend sends a free one so the day can still be finished. Once a day.
        private IEnumerator WatchStuck()
        {
            // Only once the day has actually started. On load the table is empty and there's no gold
            // either, so the condition would read as "stuck" before the player has done anything.
            yield return new WaitUntil(() => BoxCount() > 0);

            while (true)
            {
                yield return new WaitUntil(IsStuck);
                yield return null;          // boxes are destroyed at end of frame; re-check once settled
                if (!IsStuck()) continue;

                yield return Call(stuckLines);
                if (truckManager != null && rescueTruck != null) truckManager.SpawnTruck(rescueTruck);
                yield break;
            }
        }

        private bool IsStuck()
        {
            if (DayManager.Instance == null || DayManager.Instance.QuotaMet) return false;
            if (BoxCount() > 0) return false; // there's still work on the table

            TruckData cheapest = truckShop != null ? truckShop.CheapestAvailable() : null;
            return cheapest == null || Gold() < cheapest.Price; // nothing in stock, or nothing affordable
        }

        // Boxes can't be touched at all while a tutorial message is being read — blocking raycasts stops
        // dragging AND clicking, and unlike disabling UIDraggable it doesn't fight the entrance animation
        // (TruckManager turns that component off while a box flies in).
        private static void SetBoxesInteractive(bool interactive)
        {
            foreach (Box box in FindObjectsByType<Box>())
            {
                CanvasGroup group = box.GetComponent<CanvasGroup>();
                if (group == null) group = box.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = interactive;
            }
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
                Vector3 from = CenterOf(box.transform);
                Vector3 to = CenterOf(slot.transform); // the slot's centre, not its pivot/corner

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

        private static void Blink(GameObject go) => TutorialHighlight.Blink(go);
        private static void StopBlink(GameObject go) => TutorialHighlight.StopBlink(go);

        // Places the pulsing "select this" hint at the bottom-left of a blinking element.
        private void ShowSelectHint(GameObject target)
        {
            HideSelectHint();
            if (selectHintPrefab == null || target == null) return;

            Canvas canvas = target.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : target.transform.parent;
            RectTransform hint = Instantiate(selectHintPrefab, parent, false);
            hint.SetAsLastSibling();
            if (hint.GetComponent<PulseScale>() == null) hint.gameObject.AddComponent<PulseScale>();

            _selectHint = hint.gameObject;
            _selectHintFollow = StartCoroutine(FollowSelectHint(hint, target.transform));
        }

        // Keeps the hint pinned to the target's bottom-left corner every frame. The target's layout only
        // settles a frame or two after its panel opens (the upgrades tab), and it can scroll — placing it
        // once would leave the hint at a stale position.
        private IEnumerator FollowSelectHint(RectTransform hint, Transform target)
        {
            while (hint != null && target != null)
            {
                hint.position = target is RectTransform rect
                    ? rect.TransformPoint(new Vector3(rect.rect.xMin, rect.rect.yMin)) + (Vector3)selectHintOffset
                    : target.position;
                yield return null;
            }
        }

        private void HideSelectHint()
        {
            if (_selectHintFollow != null) { StopCoroutine(_selectHintFollow); _selectHintFollow = null; }
            if (_selectHint != null) { Destroy(_selectHint); _selectHint = null; }
        }

        // The labeler labels every box on the table but one, so the player sees the labels appear and
        // still learns that not every box comes labeled.
        private static void LabelAllBoxesButOne()
        {
            Box[] boxes = FindObjectsByType<Box>();
            if (boxes.Length == 0) return;

            int unlabeled = UnityEngine.Random.Range(0, boxes.Length);
            for (int i = 0; i < boxes.Length; i++)
                if (boxes[i] != null) boxes[i].SetDirectionLabel(i != unlabeled);
        }

        // The world-space centre of a UI element (its rect centre, independent of the pivot).
        private static Vector3 CenterOf(Transform t)
        {
            return t is RectTransform rect ? rect.TransformPoint(rect.rect.center) : t.position;
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
            Box[] boxes = FindObjectsByType<Box>();
            return boxes.Length > 0 ? boxes[0] : null;
        }

        private static Slot FindSlot(BoxType type)
        {
            foreach (Slot slot in FindObjectsByType<Slot>())
                if (slot != null && slot.SlotType == type) return slot;
            return null;
        }

        private static Button ButtonFor(TruckShopItem item) => item != null ? item.BuyButton : null;

        private static int BoxCount() => FindObjectsByType<Box>().Length;
        private static int Gold() => StatsManager.Instance != null ? StatsManager.Instance.Gold : 0;
        private static int Deliveries() => DayManager.Instance != null ? DayManager.Instance.CorrectDeliveries : 0;
        private static int Incorrect() => DayManager.Instance != null ? DayManager.Instance.IncorrectDeliveries : 0;
        private static bool QuotaMet() => DayManager.Instance != null && DayManager.Instance.QuotaMet;
    }
}
