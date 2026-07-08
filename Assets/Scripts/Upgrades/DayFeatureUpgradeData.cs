using Undelivered.Player;
using Undelivered.Work;
using UnityEngine;

namespace Undelivered.Upgrades
{
    /// <summary>
    /// Upgrade that switches on a day-mode feature. Some are single-level (Balanza, Limpieza
    /// automática, Sello automático); others are leveled and read their value from
    /// <see cref="UpgradeData.ValueForLevel"/>: Etiqueta de Peso (weight-label chance), Sello de
    /// calidad (stamp reward multiplier) and Asegura la confianza (trust-loss protection).
    /// </summary>
    [CreateAssetMenu(fileName = "DayFeatureUpgrade", menuName = "Undelivered/Upgrades/Day Feature")]
    public class DayFeatureUpgradeData : UpgradeData
    {
        public enum Feature
        {
            Balanza, WeightLabel, QualityStamp, AutoCleanup, AutoStamp, TrustProtection, Repackager,
            SortingArm, Forklift, DistributionCenter, QualityControl, SortingSpeed
        }

        [SerializeField] private Feature feature;

        [Tooltip("Flat reward a stamped box gives when delivered (AutoStamp feature).")]
        [SerializeField] private int autoStampFlatBonus = 2;

        public override void Apply(int level)
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
                    Box.WeightLabelChance = ValueForLevel(level);
                    foreach (Box box in FindObjectsByType<Box>())
                    {
                        box.RollWeightLabel();
                        box.RefreshLabels();
                    }
                    break;

                case Feature.QualityStamp:
                    Box.StampRewardMultiplier = ValueForLevel(level);
                    Box.StampRewardFlat = 0;
                    if (DayFeatures.Instance != null)
                    {
                        DayFeatures.Instance.EnableStamp();
                    }
                    if (level >= 4) // level 4 also stamps every box automatically
                    {
                        Box.AutoStamp = true;
                        foreach (Box box in FindObjectsByType<Box>())
                        {
                            box.SetStamped(true);
                        }
                    }
                    break;

                case Feature.AutoCleanup:
                    Trash.AutoCleanupEnabled = true;
                    Trash.CleanupDuration = ValueForLevel(level); // seconds per level (7/4/2)
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

                case Feature.TrustProtection:
                    StatsManager.TrustLossProtection = ValueForLevel(level);
                    break;

                case Feature.Repackager:
                    if (DayFeatures.Instance != null)
                    {
                        DayFeatures.Instance.EnableRepackager();
                    }
                    break;

                case Feature.SortingArm: // "Brazo Clasificador": level = number of arms working (max 3)
                    RoboticArm.SortingArmCount = level;
                    break;

                case Feature.SortingSpeed: // "Velocidad de Clasificación": shared speed of every arm
                    RoboticArm.SortingSpeed = ValueForLevel(level);
                    break;

                case Feature.Forklift: // "Montacargas": carry several boxes with right-click; level = capacity
                    Work.Forklift.Enabled = true;
                    Work.Forklift.Capacity = Mathf.Max(1, Mathf.RoundToInt(ValueForLevel(level)));
                    break;

                case Feature.DistributionCenter: // "Centro de Distribución": every truck brings more boxes
                    float multiplier = ValueForLevel(level);
                    TruckManager.BoxCountMultiplier = multiplier > 0f ? multiplier : 1.5f;
                    break;

                case Feature.QualityControl: // "Control de Calidad": auto-repairs broken boxes; level = speed
                    RoboticArm.QualityEnabled = true;
                    RoboticArm.QualitySpeed = ValueForLevel(level);
                    // The arm carries broken boxes to the repackager, so make sure the station is present.
                    if (DayFeatures.Instance != null)
                    {
                        DayFeatures.Instance.EnableRepackager();
                    }
                    break;
            }
        }
    }
}
