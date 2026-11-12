using System;
using System.Collections.Generic;
using OnlyApplicableItems.Core;

// Test-only game/UI stand-ins for the production virtual inventory data constructor.
public sealed partial class Inventory
{
    public Item Data { get; }
    public Inventory() : this("inventory", 0) { }
    public Inventory(string id, int size) { Data = new Item(id, ItemType.None) { InventorySize = size }; }
    public Inventory(Item data) { Data = data; }
    public event Action<List<Item>>? OnItemsAdd;
    public event Action<List<Item>>? OnItemsRemove;
    public event Action<Item>? OnBagAdded;
    public event Action<Item>? OnBagRemoved;
    public void Changed() => OnItemsAdd?.Invoke(new List<Item>());
    public void Removed() => OnItemsRemove?.Invoke(new List<Item>());
    public void BagAdded(Item item) => OnBagAdded?.Invoke(item);
    public void BagRemoved(Item item) => OnBagRemoved?.Invoke(item);
}

public sealed class InventoryHeaderWidgetData
{
    public bool DrawHeader { get; set; } = true;
    public string CustomHeaderId { get; }
    public string HeaderIconId { get; set; }
    public InventoryHeaderWidgetData(Inventory inventory, string headerIconId = "comm-header_2-type_icon-main_inventory", string customHeaderId = "")
    {
        CustomHeaderId = customHeaderId;
        HeaderIconId = headerIconId;
    }
}

public class InventoryWidgetDataBase
{
    public Inventory Inventory = new();
    public Action<UIItemCell>? OnItemCellOver;
    public Action<UIItemCell>? OnItemCellOut;
    public Action<UIItemCell>? OnItemCellPress;
    public Action<UIItemCell>? OnItemCellPress2;
    public Action<UIItemCell>? OnItemCellDown;
    public Func<Item, bool>? CustomItemsAvailableCondition;
    public Func<Item, bool>? CustomItemsNotShowCondition;
    public Func<Item, bool>? CustomItemSelectedCondition;
    public Func<Item, string>? ExtraRedTooltipLocIdProvider;
    public ItemRelatedWidgetState ItemRelatedWidgetState;
    public bool DrawEmptyCellsAsDisabledWhenUnavailable;
}

public class InventoryWidgetData : InventoryWidgetDataBase
{
    public InventoryHeaderWidgetData InventoryHeaderWidgetData { get; }
    public Action<InventoryWidget>? OnWidgetPressed { get; set; }
    public InventoryWidgetData(Inventory inventory, InventoryHeaderWidgetData header,
        Action<UIItemCell>? over, Action<UIItemCell>? outCallback, Action<UIItemCell>? press,
        Action<UIItemCell>? press2, Action<UIItemCell>? down, Func<Item, bool>? available)
    {
        Inventory = inventory;
        InventoryHeaderWidgetData = header;
        OnItemCellOver = over;
        OnItemCellOut = outCallback;
        OnItemCellPress = press;
        OnItemCellPress2 = press2;
        OnItemCellDown = down;
        CustomItemsAvailableCondition = available;
    }
}
