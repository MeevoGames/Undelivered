using Undelivered.Work;
using UnityEngine;

namespace Undelivered.Upgrades
{
    /// <summary>
    /// Upgrade that reveals one of the box labels: the direction label (Etiquetadora) or the dice
    /// label (Etiqueta de dados). Buying it turns the matching toggle on and refreshes the boxes
    /// already on the table.
    /// </summary>
    [CreateAssetMenu(fileName = "BoxLabelUpgrade", menuName = "Undelivered/Upgrades/Box Label")]
    public class BoxLabelUpgradeData : UpgradeData
    {
        public enum Label { Direction, Dice }

        [SerializeField] private Label label;

        public override void Apply()
        {
            switch (label)
            {
                case Label.Direction:
                    Box.ShowDirectionLabel = true;
                    break;
                case Label.Dice:
                    Box.ShowDiceLabel = true;
                    break;
            }

            // Reveal the label on boxes that are already on the table.
            foreach (Box box in FindObjectsByType<Box>())
            {
                box.RefreshLabels();
            }
        }
    }
}
