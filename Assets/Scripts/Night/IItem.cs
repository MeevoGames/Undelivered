using UnityEngine;

namespace Undelivered.Night
{
    /// <summary>
    /// Something that can be listed in the shop or the inventory — dice today, effects and boxes later:
    /// an icon, a gem price, a name and a tooltip description. Rendered by <see cref="ItemView"/>.
    /// </summary>
    public interface IItem
    {
        Sprite Icon { get; }
        int Price { get; }
        string ItemName { get; }
        string DescriptionForTooltip { get; }
    }
}
