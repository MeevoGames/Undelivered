using UnityEngine;

namespace Undelivered.Work
{
    /// <summary>
    /// Holds the scene elements that day-mode upgrades switch on (the scale and the stamp) and
    /// exposes methods to enable them. Scene singleton so upgrade ScriptableObjects (which can't hold
    /// scene references) can reach it.
    /// </summary>
    public class DayFeatures : MonoBehaviour
    {
        public static DayFeatures Instance { get; private set; }

        [Tooltip("Scale (Balanza) element. Should start disabled.")]
        [SerializeField] private GameObject scaleElement;

        [Tooltip("Quality stamp element. Should start disabled.")]
        [SerializeField] private GameObject stampElement;

        [Tooltip("Repackager (reempaquetadora) element. Should start disabled.")]
        [SerializeField] private GameObject repackagerElement;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Start hidden; only the matching upgrade reveals them.
            if (scaleElement != null)
            {
                scaleElement.SetActive(false);
            }
            if (stampElement != null)
            {
                stampElement.SetActive(false);
            }
            if (repackagerElement != null)
            {
                repackagerElement.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Turns the scale element on (called by the Balanza upgrade).</summary>
        public void EnableScale()
        {
            if (scaleElement != null)
            {
                scaleElement.SetActive(true);
            }
        }

        /// <summary>Turns the stamp element on (called by the Quality Stamp upgrade).</summary>
        public void EnableStamp()
        {
            if (stampElement != null)
            {
                stampElement.SetActive(true);
            }
        }

        /// <summary>Turns the repackager element on (called by the Reempaquetado upgrade).</summary>
        public void EnableRepackager()
        {
            if (repackagerElement != null)
            {
                repackagerElement.SetActive(true);
            }
        }
    }
}
