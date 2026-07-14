using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.Night
{
    /// <summary>
    /// A "NEW!" tag on the inventory button. It appears whenever an item is added to the collection
    /// (day packages, opening a box, tournament rewards…) and hides once the inventory is opened.
    /// Put this on the inventory button; assign the "NEW!" GameObject as <see cref="newTag"/>.
    /// </summary>
    public class InventoryNewBadge : MonoBehaviour
    {
        [Tooltip("The 'NEW!' GameObject shown while there are unseen items.")]
        [SerializeField] private GameObject newTag;
        [Tooltip("Button that opens the inventory; tapping it hides the badge. Defaults to this object's Button.")]
        [SerializeField] private Button openInventoryButton;

        private bool _subscribed;

        private void Awake()
        {
            if (openInventoryButton == null) openInventoryButton = GetComponent<Button>();
            if (openInventoryButton != null) openInventoryButton.onClick.AddListener(Hide);
            if (newTag != null) newTag.SetActive(false);
        }

        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void OnDestroy()
        {
            if (openInventoryButton != null) openInventoryButton.onClick.RemoveListener(Hide);
            if (Inventory.Instance != null) Inventory.Instance.ItemAdded -= Show;
        }

        private void TrySubscribe()
        {
            if (_subscribed || Inventory.Instance == null) return;
            Inventory.Instance.ItemAdded += Show;
            _subscribed = true;
        }

        private void Show()
        {
            if (newTag != null) newTag.SetActive(true);
        }

        private void Hide()
        {
            if (newTag != null) newTag.SetActive(false);
        }
    }
}
