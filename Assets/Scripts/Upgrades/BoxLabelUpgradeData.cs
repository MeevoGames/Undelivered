using Undelivered.Work;
using UnityEngine;

namespace Undelivered.Upgrades
{
    /// <summary>
    /// Upgrade that reveals a box label. The direction label (Etiquetadora) is leveled: each level
    /// sets the per-box chance from <see cref="UpgradeData.ValueForLevel"/> (50/75/100%). The dice
    /// label (Etiqueta de dados) is a single-level toggle for every box.
    /// </summary>
    [CreateAssetMenu(fileName = "BoxLabelUpgrade", menuName = "Undelivered/Upgrades/Box Label")]
    public class BoxLabelUpgradeData : UpgradeData
    {
        public enum Label { Direction, Dice }

        [SerializeField] private Label label;

        public override void Apply(int level)
        {
            switch (label)
            {
                case Label.Direction:
                    Box.DirectionLabelChance = ValueForLevel(level);
                    foreach (Box box in FindObjectsByType<Box>())
                    {
                        box.RollDirectionLabel();
                        box.RefreshLabels();
                    }
                    break;

                case Label.Dice:
                    Box.ShowDiceLabel = true;
                    foreach (Box box in FindObjectsByType<Box>())
                    {
                        box.RefreshLabels();
                    }
                    break;
            }
        }
    }
}
