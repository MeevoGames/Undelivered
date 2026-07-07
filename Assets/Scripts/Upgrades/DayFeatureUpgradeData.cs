using Undelivered.Work;
using UnityEngine;

namespace Undelivered.Upgrades
{
    /// <summary>
    /// Upgrade that switches on a day-mode feature: the scale (Balanza), weight labels on most boxes
    /// (Etiqueta de Peso), the manual quality stamp (Sello de calidad), automatic trash cleanup
    /// (Limpieza automática) or the automatic stamp on every box (Sello automático).
    /// </summary>
    [CreateAssetMenu(fileName = "DayFeatureUpgrade", menuName = "Undelivered/Upgrades/Day Feature")]
    public class DayFeatureUpgradeData : UpgradeData
    {
        public enum Feature { Balanza, WeightLabel, QualityStamp, AutoCleanup, AutoStamp }

        [SerializeField] private Feature feature;

        [Tooltip("Chance a box comes with a weight label (WeightLabel feature).")]
        [Range(0f, 1f)]
        [SerializeField] private float weightLabelChance = 0.8f;

        [Tooltip("Reward multiplier a stamped box gives when delivered (QualityStamp feature).")]
        [SerializeField] private float stampMultiplier = 1.5f;

        [Tooltip("Flat reward a stamped box gives when delivered (AutoStamp feature).")]
        [SerializeField] private int autoStampFlatBonus = 2;

        public override void Apply()
        {
            switch (feature)
            {
                case Feature.Balanza:
                    if (DayFeatures.Instance != null)
                    {
                        DayFeatures.Instance.EnableScale();
                    }
                    break;

                case Feature.WeightLabel:
                    Box.WeightLabelChance = weightLabelChance;
                    foreach (Box box in FindObjectsByType<Box>())
                    {
                        box.RollWeightLabel();
                        box.RefreshLabels();
                    }
                    break;

                case Feature.QualityStamp:
                    Box.StampRewardMultiplier = stampMultiplier;
                    Box.StampRewardFlat = 0;
                    if (DayFeatures.Instance != null)
                    {
                        DayFeatures.Instance.EnableStamp();
                    }
                    break;

                case Feature.AutoCleanup:
                    Trash.AutoCleanupEnabled = true;
                    foreach (Trash trash in FindObjectsByType<Trash>())
                    {
                        trash.TryAutoCleanup();
                    }
                    break;

                case Feature.AutoStamp:
                    Box.AutoStamp = true;
                    Box.StampRewardMultiplier = 1f;
                    Box.StampRewardFlat = autoStampFlatBonus;
                    foreach (Box box in FindObjectsByType<Box>())
                    {
                        box.SetStamped(true);
                    }
                    break;
            }
        }
    }
}
