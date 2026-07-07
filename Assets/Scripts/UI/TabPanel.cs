using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Undelivered.UI
{
    /// <summary>
    /// A group of selectable tabs. Selecting a tab shows its content and hides the others. Used for
    /// the left panel: trucks list / upgrades shop / items obtained today.
    /// </summary>
    public class TabPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Tab
        {
            public Button button;
            public GameObject content;
        }

        [SerializeField] private List<Tab> tabs = new List<Tab>();

        [Tooltip("Index of the tab shown on start.")]
        [SerializeField] private int defaultTab = 0;

        private void Start()
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i; // capture for the closure
                if (tabs[i] != null && tabs[i].button != null)
                {
                    tabs[i].button.onClick.AddListener(() => Select(index));
                }
            }

            Select(defaultTab);
        }

        /// <summary>Shows the content of the tab at the given index and hides the rest.</summary>
        public void Select(int index)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                Tab tab = tabs[i];
                if (tab == null)
                {
                    continue;
                }

                bool selected = i == index;
                if (tab.content != null)
                {
                    tab.content.SetActive(selected);
                }
                if (tab.button != null)
                {
                    // The active tab's button is disabled so it reads as "selected" and can't be re-clicked.
                    tab.button.interactable = !selected;
                }
            }
        }
    }
}
