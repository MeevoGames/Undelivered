using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// The inventory screen. Lists what the player owns in a 5-column grid with four filters
    /// (ALL / BOXES / DICES / EFFECTS; ALL orders boxes → dice → effects). Tapping a box opens it;
    /// tapping a die or an effect enters deck-selection mode for that type: a Guardar / Cancelar bar
    /// and an "actual/total" counter appear, the list filters to that type, and tapping items toggles
    /// their inclusion in the deck (Guardar applies, Cancelar reverts). The grid container is resized
    /// in height to fit the items.
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        public static InventoryPanel Instance { get; private set; }

        private enum Filter { All, Boxes, Dice, Effects }

        [Header("Grid")]
        [SerializeField] private RectTransform container;
        [SerializeField] private InventoryItem itemPrefab;
        [SerializeField] private float itemWidth = 170f;
        [SerializeField] private float itemHeight = 190f;
        [SerializeField] private int columns = 5;
        [Tooltip("Preferred spacing between rows (also added to each row's height).")]
        [SerializeField] private float rowSpacing = 30f;
        [SerializeField] private float columnSpacing = 30f;

        [Header("Filter bar (normal mode)")]
        [SerializeField] private GameObject filterBar;
        [SerializeField] private Button allButton;
        [SerializeField] private Button boxesButton;
        [SerializeField] private Button dicesButton;
        [SerializeField] private Button effectsButton;

        [Header("Deck-selection bar")]
        [SerializeField] private GameObject selectionBar;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private Color underColor = Color.white;
        [SerializeField] private Color equalColor = Color.green;
        [SerializeField] private Color overColor = Color.red;

        private Filter _filter = Filter.All;
        private bool _selectMode;
        private InventoryItem.Kind _selectKind;
        private readonly List<int> _selected = new List<int>(); // indices into the collection being selected
        private bool _subscribed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (allButton != null) allButton.onClick.AddListener(() => SetFilter(Filter.All));
            if (boxesButton != null) boxesButton.onClick.AddListener(() => SetFilter(Filter.Boxes));
            if (dicesButton != null) dicesButton.onClick.AddListener(() => SetFilter(Filter.Dice));
            if (effectsButton != null) effectsButton.onClick.AddListener(() => SetFilter(Filter.Effects));
            if (saveButton != null) saveButton.onClick.AddListener(Save);
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);

            ConfigureGrid();
            ShowBars(selection: false);
        }

        private void OnEnable() => TrySubscribe();

        private void Start()
        {
            TrySubscribe();
            Rebuild();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Inventory.Instance != null) Inventory.Instance.Changed -= Rebuild;
        }

        private void TrySubscribe()
        {
            if (_subscribed || Inventory.Instance == null) return;
            Inventory.Instance.Changed += Rebuild;
            _subscribed = true;
        }

        private void SetFilter(Filter filter)
        {
            if (_selectMode) return; // filters are locked while selecting
            _filter = filter;
            Rebuild();
        }

        // ----- normal-mode actions -----

        private void OpenBox(BoxData box)
        {
            if (BoxOpener.Instance != null) BoxOpener.Instance.Open(box);
            if (Inventory.Instance != null) Inventory.Instance.RemoveBox(box);
        }

        private void EnterSelect(InventoryItem.Kind kind)
        {
            _selectMode = true;
            _selectKind = kind;

            // Pre-select whatever is already in the deck.
            _selected.Clear();
            if (kind == InventoryItem.Kind.Die)
            {
                var deck = Deck.Instance != null ? new List<DiceData>(Deck.Instance.Dice) : new List<DiceData>();
                var col = Inventory.Instance.Dice;
                for (int i = 0; i < col.Count; i++) if (deck.Remove(col[i])) _selected.Add(i);
            }
            else
            {
                var deck = EffectDeck.Instance != null ? new List<EffectData>(EffectDeck.Instance.Effects) : new List<EffectData>();
                var col = Inventory.Instance.Effects;
                for (int i = 0; i < col.Count; i++) if (deck.Remove(col[i])) _selected.Add(i);
            }

            ShowBars(selection: true);
            Rebuild();
        }

        // ----- deck-selection mode -----

        private void ToggleIndex(int i, InventoryItem item)
        {
            if (_selected.Contains(i)) _selected.Remove(i);
            else _selected.Add(i);

            if (item != null) item.SetSelected(_selected.Contains(i));
            UpdateCountUI();
        }

        private void Save()
        {
            if (Inventory.Instance == null || _selected.Count > Total()) return; // guarded by the disabled button

            if (_selectKind == InventoryItem.Kind.Die)
            {
                var chosen = new List<DiceData>();
                var col = Inventory.Instance.Dice;
                foreach (int i in _selected) if (i >= 0 && i < col.Count) chosen.Add(col[i]);
                Deck.Instance?.SetDice(chosen);
            }
            else
            {
                var chosen = new List<EffectData>();
                var col = Inventory.Instance.Effects;
                foreach (int i in _selected) if (i >= 0 && i < col.Count) chosen.Add(col[i]);
                EffectDeck.Instance?.SetEffects(chosen);
            }

            ExitSelect();
        }

        private void Cancel() => ExitSelect(); // deck was never touched, so previous selection stands

        private void ExitSelect()
        {
            _selectMode = false;
            _filter = Filter.All;
            _selected.Clear();
            ShowBars(selection: false);
            Rebuild();
        }

        private int Total() => _selectKind == InventoryItem.Kind.Die
            ? (Deck.Instance != null ? Deck.Instance.Slots : Deck.MinSlots)
            : (EffectDeck.Instance != null ? EffectDeck.Instance.MaxSlots : Deck.MinSlots);

        private void UpdateCountUI()
        {
            int actual = _selected.Count;
            int total = Total();
            if (countText != null)
            {
                countText.text = $"{actual}/{total}";
                countText.color = actual < total ? underColor : (actual == total ? equalColor : overColor);
            }
            if (saveButton != null) saveButton.interactable = actual <= total;
        }

        private void ShowBars(bool selection)
        {
            if (filterBar != null) filterBar.SetActive(!selection);
            if (selectionBar != null) selectionBar.SetActive(selection);
        }

        // ----- listing -----

        private void Rebuild()
        {
            if (container == null || itemPrefab == null || Inventory.Instance == null) return;

            Clear();
            int count = 0;

            if (_selectMode)
            {
                if (_selectKind == InventoryItem.Kind.Die)
                {
                    var col = Inventory.Instance.Dice;
                    for (int i = 0; i < col.Count; i++)
                    {
                        if (col[i] == null) continue;
                        int index = i;
                        InventoryItem it = Instantiate(itemPrefab, container, false);
                        it.SetupDie(col[i], () => ToggleIndex(index, it));
                        it.SetSelected(_selected.Contains(index));
                        count++;
                    }
                }
                else
                {
                    var col = Inventory.Instance.Effects;
                    for (int i = 0; i < col.Count; i++)
                    {
                        if (col[i] == null) continue;
                        int index = i;
                        InventoryItem it = Instantiate(itemPrefab, container, false);
                        it.SetupEffect(col[i], () => ToggleIndex(index, it));
                        it.SetSelected(_selected.Contains(index));
                        count++;
                    }
                }
                UpdateCountUI();
            }
            else
            {
                // Order for ALL: boxes, then dice, then effects.
                if (Shows(Filter.Boxes))
                {
                    foreach (BoxData box in Inventory.Instance.Boxes)
                    {
                        if (box == null) continue;
                        BoxData b = box;
                        Instantiate(itemPrefab, container, false).SetupBox(b, () => OpenBox(b));
                        count++;
                    }
                }
                if (Shows(Filter.Dice))
                {
                    foreach (DiceData die in Inventory.Instance.Dice)
                    {
                        if (die == null) continue;
                        Instantiate(itemPrefab, container, false).SetupDie(die, () => EnterSelect(InventoryItem.Kind.Die));
                        count++;
                    }
                }
                if (Shows(Filter.Effects))
                {
                    foreach (EffectData effect in Inventory.Instance.Effects)
                    {
                        if (effect == null) continue;
                        Instantiate(itemPrefab, container, false).SetupEffect(effect, () => EnterSelect(InventoryItem.Kind.Effect));
                        count++;
                    }
                }
            }

            ResizeContainer(count);
        }

        private bool Shows(Filter section) => _filter == Filter.All || _filter == section;

        private void ResizeContainer(int count)
        {
            if (container == null) return;
            int cols = Mathf.Max(1, columns);
            int rows = Mathf.CeilToInt(count / (float)cols);
            container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rows * (itemHeight + rowSpacing));
        }

        private void ConfigureGrid()
        {
            if (container == null) return;
            GridLayoutGroup grid = container.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = container.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(itemWidth, itemHeight);
            grid.spacing = new Vector2(columnSpacing, rowSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.childAlignment = TextAnchor.UpperCenter;
        }

        private void Clear()
        {
            for (int i = container.childCount - 1; i >= 0; i--) Destroy(container.GetChild(i).gameObject);
        }
    }
}
