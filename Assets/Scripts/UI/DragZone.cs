using System.Collections.Generic;
using UnityEngine;

namespace Undelivered.UI
{
    /// <summary>
    /// Marks a RectTransform as a valid area for dragging (the table and each slot). Draggables
    /// query the shared registry to avoid being moved over dead screen space where there is
    /// neither table nor slots.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DragZone : MonoBehaviour
    {
        private static readonly List<DragZone> Zones = new List<DragZone>();

        /// <summary>True if at least one zone is currently active.</summary>
        public static bool HasAny => Zones.Count > 0;

        private RectTransform _rect;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            if (!Zones.Contains(this))
            {
                Zones.Add(this);
            }
        }

        private void OnDisable()
        {
            Zones.Remove(this);
        }

        /// <summary>True if any active zone contains the given screen point.</summary>
        public static bool AnyContains(Vector2 screenPoint, Camera eventCamera)
        {
            for (int i = 0; i < Zones.Count; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(Zones[i]._rect, screenPoint, eventCamera))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
