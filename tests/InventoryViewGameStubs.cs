using System;
using OnlyApplicableItems.Core;

// Test-only game/UI stand-ins for the production virtual inventory data constructor.
public sealed class Inventory { }

public sealed class InventoryHeaderWidgetData
{
    public bool DrawHeader { get; set; }
    public InventoryHeaderWidgetData(Inventory inventory, string customHeaderId) { }
}

public class InventoryWidgetDataBase
{
    public Action<UIItemCell>? OnItemCellOver;
    public Action<UIItemCell>? OnItemCellOut;
    public Action<UIItemCell>? OnItemCellPress;
    public Func<Item, bool>? CustomItemsAvailableCondition;
    public Func<Item, string>? ExtraRedTooltipLocIdProvider;
}

public class InventoryWidgetData : InventoryWidgetDataBase
{
    public InventoryWidgetData(Inventory inventory, InventoryHeaderWidgetData header,
        Action<UIItemCell>? over, Action<UIItemCell>? outCallback, Action<UIItemCell>? press,
        Action<UIItemCell>? press2, Action<UIItemCell>? down, Func<Item, bool>? available)
    {
        OnItemCellOver = over;
        OnItemCellOut = outCallback;
        OnItemCellPress = press;
        CustomItemsAvailableCondition = available;
    }
}

namespace OnlyApplicableItems.UI
{
    internal sealed class ApplicableItemsSession
    {
        public PickerPolicy<Item> Policy { get; }
        public Item? Selected { get; private set; }
        public ApplicableItemsSession(PickerPolicy<Item> policy) { Policy = policy; }
        public void Select(UIItemCell cell) { Selected = cell.DisplayingItem; }
    }
}
