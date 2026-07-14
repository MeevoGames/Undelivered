using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The pre-combat shop: three items (a die, an effect, and a third of either), paid in gems. The
    /// stock is randomised once per tournament and kept while in the same one; a new tournament (or the
    /// reroll button) regenerates it. Buying an item adds it to the inventory and a new one takes its
    /// place. The upgrade button raises the deck's dice/effect slots for a per-level cost.
    ///
    /// Anything the player can't afford is shown at 50% opacity (dice, effects, reroll, upgrade).
    /// </summary>
    public class Shop : MonoBehaviour
    {
        public static Shop Instance { get; private set; }

        [Header("Pools to draw from")]
        [SerializeField] private List<DiceData> dicePool = new List<DiceData>();
        [SerializeField] private List<EffectData> effectPool = new List<EffectData>();

        [Header("The 3 slots (fixed positions)")]
        [SerializeField] private Transform[] slots = new Transform[3];
        [SerializeField] private ItemView diceItemPrefab;
        [SerializeField] private EffectView effectItemPrefab;

        [Header("Boxes (second section — 3 slots)")]
        [SerializeField] private Transform[] boxSlots = new Transform[3];
        [SerializeField] private BoxView boxItemPrefab;
        [SerializeField] private List<BoxData> boxPool = new List<BoxData>();

        [Header("Buttons (wire their onClick to Reroll / UpgradeBuild)")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private Button upgradeButton;
        [Tooltip("Text that shows the upgrade cost (cleared at max).")]
        [SerializeField] private TextMeshProUGUI upgradePriceText;
        [Tooltip("Separate text that shows \"MAX.\" when the deck is maxed.")]
        [SerializeField] private TextMeshProUGUI upgradeButtonLabel;
        [SerializeField] private string maxLabel = "MAX.";

        [Header("Reroll cost")]
        [Tooltip("First reroll costs this; each further reroll adds it again (8, 16, 24...). Resets per tournament.")]
        [SerializeField] private int rerollBaseCost = 8;

        [Header("Upgrade costs per level (deck slots 3, 4, 5, 6)")]
        [SerializeField] private int[] upgradeCosts = { 30, 60, 120, 240 };

        [Header("Reroll shake")]
        [SerializeField] private float shakeDuration = 0.25f;
        [SerializeField] private float shakeMagnitude = 12f;

        private readonly IItem[] _items = new IItem[3];
        private readonly GameObject[] _views = new GameObject[3];
        private readonly BoxData[] _boxes = new BoxData[3];
        private readonly GameObject[] _boxViews = new GameObject[3];
        private int _rerolls;
        private bool _generated;

        /// <summary>The box pool (used by the debug console's "give random box").</summary>
        public IReadOnlyList<BoxData> BoxPool => boxPool;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (NightWallet.Instance != null) NightWallet.Instance.GemsChanged -= OnGemsChanged;
        }

        private void Start()
        {
            if (NightWallet.Instance != null) NightWallet.Instance.GemsChanged += OnGemsChanged;
            EnsureStock();
        }

        private void OnGemsChanged(int gems) => RefreshAffordability();

        /// <summary>Generates the stock the first time (kept afterwards until a new tournament / reroll).</summary>
        public void Open() => EnsureStock();

        /// <summary>Call when a new tournament starts: the stock and the reroll price are randomised/reset.</summary>
        public void ResetForNewTournament()
        {
            _generated = false;
            _rerolls = 0;
            EnsureStock();
        }

        private void EnsureStock()
        {
            if (_generated) return;
            for (int i = 0; i < slots.Length; i++) FillSlot(i);
            for (int i = 0; i < boxSlots.Length; i++) FillBoxSlot(i);
            _generated = true;
            RefreshAffordability();
        }

        private int RerollCost() => rerollBaseCost * (_rerolls + 1);

        /// <summary>Reroll button: spend the (rising) gem cost, replace all three items, and shake them in.</summary>
        public void Reroll()
        {
            if (NightWallet.Instance == null || !NightWallet.Instance.TrySpend(RerollCost()))
            {
                Debug.LogWarning("No alcanzan las gemas para relanzar la tienda.");
                return;
            }

            _rerolls++;
            for (int i = 0; i < slots.Length; i++)
            {
                FillSlot(i);
                if (_views[i] != null && _views[i].transform is RectTransform rect) StartCoroutine(Shake(rect));
            }
            RefreshAffordability();
        }

        /// <summary>Upgrade button: +1 to the deck's dice/effect slots for this level's gem cost.</summary>
        public void UpgradeBuild()
        {
            if (Deck.Instance == null || Deck.Instance.Slots >= Deck.MaxSlots)
            {
                Debug.LogWarning("El deck ya está al máximo.");
                return;
            }

            int cost = UpgradeCost();
            if (NightWallet.Instance == null || !NightWallet.Instance.TrySpend(cost))
            {
                Debug.LogWarning("No alcanzan las gemas para mejorar el deck.");
                return;
            }

            Deck.Instance.UpgradeSlots(); // +1 dice slot; the effect deck's cap mirrors it
            RefreshAffordability();
        }

        private int UpgradeCost()
        {
            int bought = Deck.Instance != null ? Deck.Instance.Slots - Deck.MinSlots : 0;
            if (upgradeCosts == null || bought < 0 || bought >= upgradeCosts.Length) return int.MaxValue;
            return upgradeCosts[bought];
        }

        private void Buy(int slot)
        {
            IItem item = _items[slot];
            if (item == null) return;

            if (NightWallet.Instance == null || !NightWallet.Instance.TrySpend(item.Price))
            {
                Debug.LogWarning("No alcanzan las gemas para comprar.");
                return;
            }

            if (item is DiceData die) Inventory.Instance?.AddDie(die);
            else if (item is EffectData effect) Inventory.Instance?.AddEffect(effect);

            FillSlot(slot); // a new item takes its place
            RefreshAffordability();
        }

        private void FillSlot(int i)
        {
            if (slots[i] == null) return;

            for (int c = slots[i].childCount - 1; c >= 0; c--) Destroy(slots[i].GetChild(c).gameObject);
            _items[i] = null;
            _views[i] = null;

            IItem item = Generate(i);
            _items[i] = item;
            if (item == null) return;

            GameObject go = null;
            if (item is DiceData die && diceItemPrefab != null)
            {
                ItemView view = Instantiate(diceItemPrefab, slots[i], false);
                view.Setup(die);
                go = view.gameObject;
            }
            else if (item is EffectData effect && effectItemPrefab != null)
            {
                EffectView view = Instantiate(effectItemPrefab, slots[i], false);
                view.Setup(effect);
                go = view.gameObject;
            }
            if (go == null) return;

            _views[i] = go;
            Button buy = go.GetComponent<Button>();
            if (buy != null)
            {
                int slot = i;
                buy.onClick.AddListener(() => Buy(slot));
            }
        }

        // Boxes bought here open right away; a new box takes the slot.
        private void FillBoxSlot(int i)
        {
            if (boxSlots[i] == null) return;

            for (int c = boxSlots[i].childCount - 1; c >= 0; c--) Destroy(boxSlots[i].GetChild(c).gameObject);
            _boxes[i] = null;
            _boxViews[i] = null;

            BoxData box = boxPool.Count > 0 ? boxPool[Random.Range(0, boxPool.Count)] : null;
            _boxes[i] = box;
            if (box == null || boxItemPrefab == null) return;

            BoxView view = Instantiate(boxItemPrefab, boxSlots[i], false);
            view.Setup(box);
            _boxViews[i] = view.gameObject;

            Button buy = view.GetComponent<Button>();
            if (buy != null)
            {
                int slot = i;
                buy.onClick.AddListener(() => BuyBox(slot));
            }
        }

        private void BuyBox(int slot)
        {
            BoxData box = _boxes[slot];
            if (box == null) return;

            if (NightWallet.Instance == null || !NightWallet.Instance.TrySpend(box.Price))
            {
                Debug.LogWarning("No alcanzan las gemas para comprar la caja.");
                return;
            }

            if (BoxOpener.Instance != null) BoxOpener.Instance.Open(box); // opens automatically on purchase
            FillBoxSlot(slot); // a new box takes its place
            RefreshAffordability();
        }

        // Slot 0 is a die, slot 1 an effect, slot 2 either (falls back to the other pool if one is empty).
        private IItem Generate(int slot)
        {
            switch (slot)
            {
                case 0: return (IItem)RandomDie() ?? RandomEffect();
                case 1: return (IItem)RandomEffect() ?? RandomDie();
                default:
                    return Random.value < 0.5f
                        ? ((IItem)RandomDie() ?? RandomEffect())
                        : ((IItem)RandomEffect() ?? RandomDie());
            }
        }

        private DiceData RandomDie() => dicePool.Count > 0 ? dicePool[Random.Range(0, dicePool.Count)] : null;
        private EffectData RandomEffect() => effectPool.Count > 0 ? effectPool[Random.Range(0, effectPool.Count)] : null;

        // Grey out (50% opacity) anything the player can't afford; handle the maxed-out upgrade button.
        private void RefreshAffordability()
        {
            int gems = NightWallet.Instance != null ? NightWallet.Instance.Gems : 0;

            for (int i = 0; i < _views.Length; i++)
            {
                if (_views[i] == null) continue;
                bool affordable = _items[i] != null && gems >= _items[i].Price;
                SetOpacity(_views[i], affordable ? 1f : 0.5f);
            }

            for (int i = 0; i < _boxViews.Length; i++)
            {
                if (_boxViews[i] == null) continue;
                bool affordable = _boxes[i] != null && gems >= _boxes[i].Price;
                SetOpacity(_boxViews[i], affordable ? 1f : 0.5f);
            }

            if (rerollButton != null)
            {
                SetOpacity(rerollButton.gameObject, gems >= RerollCost() ? 1f : 0.5f);
            }

            if (upgradeButton != null)
            {
                bool maxed = Deck.Instance != null && Deck.Instance.Slots >= Deck.MaxSlots;
                if (maxed)
                {
                    upgradeButton.interactable = false;
                    SetOpacity(upgradeButton.gameObject, 1f);
                    if (upgradeButtonLabel != null) upgradeButtonLabel.text = maxLabel;
                    if (upgradePriceText != null) upgradePriceText.text = string.Empty;
                }
                else
                {
                    upgradeButton.interactable = true;
                    int cost = UpgradeCost();
                    SetOpacity(upgradeButton.gameObject, gems >= cost ? 1f : 0.5f);
                    if (upgradePriceText != null) upgradePriceText.text = cost.ToString();
                }
            }
        }

        // 50% / 100% opacity over all coloured layers via a CanvasGroup.
        private static void SetOpacity(GameObject go, float alpha)
        {
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            group.alpha = alpha;
        }

        private IEnumerator Shake(RectTransform rect)
        {
            Vector3 home = rect.localPosition;
            for (float e = 0f; e < shakeDuration; e += Time.unscaledDeltaTime)
            {
                float damper = 1f - (e / shakeDuration);
                rect.localPosition = home + (Vector3)(Random.insideUnitCircle * (shakeMagnitude * damper));
                yield return null;
            }
            rect.localPosition = home;
        }
    }
}
