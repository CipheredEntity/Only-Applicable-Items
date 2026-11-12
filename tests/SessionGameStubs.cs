using System;
using System.Collections.Generic;
using OnlyApplicableItems.UI;

// In-memory boundaries for executing the production session, not Unity rendering,
// actual controller navigation, inventory transfers, or Harmony interception.
public sealed class MultiInventory { public List<Inventory> inventoryList = new(); }
public enum MultiInventoryWidgetMode { Default, BagMode }
public enum ItemRelatedWidgetState { Default, Disabled }

public sealed class MultiInventoryWidgetData
{
    public List<InventoryWidgetDataBase> inventoriesData = new();
    public string HeaderLocaleId = "ui_multiinventory";
    public MultiInventoryWidgetMode WidgetSelectionMode;
    public Action? onWidgetHide;
    public Action? OnItemsAvailabilityChanged;
    private InventoryWidgetDataBase? selected;
    public InventoryWidgetDataBase? SelectedWidgetData
    {
        get => selected ?? (inventoriesData.Count > 0 ? inventoriesData[0] : null);
        set => selected = value;
    }
}

public static class InventoryWidgetDataHelper
{
    public static List<InventoryWidgetDataBase> GetWidgetsDataForMultiInventory(MultiInventory inventories,
        Action<UIItemCell>? over, Action<UIItemCell>? outCallback, Action<UIItemCell>? press,
        Action<UIItemCell>? press2, Action<UIItemCell>? down, Func<Item, bool> available)
    {
        var result = new List<InventoryWidgetDataBase>();
        foreach (var inventory in inventories.inventoryList)
        {
            Add(inventory);
            foreach (var item in inventory.Data.Inventory)
                if (item.IsBag) Add(new Inventory(item));
        }
        return result;
        void Add(Inventory inventory) => result.Add(new InventoryWidgetData(inventory,
            new InventoryHeaderWidgetData(inventory, customHeaderId: "native"), over, outCallback, press, press2, down, available));
    }
}

public sealed class UIMultiInventoryWindowData
{
    public MultiInventory MultiInventory { get; }
    public MultiInventoryWidgetData MultiInventoryWidgetData { get; } = new();
    public UIMultiInventoryWindowData(MultiInventory inventories, Action<UIItemCell> callback, Func<Item, bool> predicate)
    {
        MultiInventory = inventories;
        MultiInventoryWidgetData.inventoriesData.AddRange(InventoryWidgetDataHelper.GetWidgetsDataForMultiInventory(
            inventories, null, null, callback, null, null, predicate));
    }
}

public sealed class TestGameObject
{
    public bool activeInHierarchy = true;
    public bool activeSelf { get; private set; } = true;
    public void SetActive(bool value) { activeSelf = value; activeInHierarchy = value; }
}

public sealed class InventoryWidget
{
    public InventoryWidgetData Data = null!;
    public List<UIItemCell> Cells { get; } = new();
    public MultiInventoryWidget? Parent;
    public T? GetComponentInParent<T>() where T : class =>
        typeof(T) == typeof(MultiInventoryWidget) ? Parent as T : Parent?.Window as T;
    public void Draw(InventoryWidgetDataBase data)
    {
        Data = (InventoryWidgetData)data;
        while (Cells.Count > Data.Inventory.Data.InventorySize) Cells.RemoveAt(Cells.Count - 1);
        while (Cells.Count < Data.Inventory.Data.InventorySize) Cells.Add(new UIItemCell());
        for (int i = 0; i < Cells.Count; i++)
        {
            Cells[i].Draw(i < Data.Inventory.Data.Inventory.Count ? Data.Inventory.Data.Inventory[i] : null);
            Cells[i].OnItemCellPress = Data.OnItemCellPress;
        }
        if (Data is ApplicableInventoryWidgetData view) view.Session.Decorate(this, view);
    }
}

public sealed class MultiInventoryWidget
{
    public MultiInventoryWidgetData Data = null!;
    public TestGameObject gameObject = new();
    public UIMultiInventoryWindow? Window;
    public List<InventoryWidget> DrawnInventories { get; } = new();
    public int NavigationRefreshes;
    public bool FailNextRedraw;
    public void Draw(MultiInventoryWidgetData data)
    {
        Data = data;
        gameObject.activeInHierarchy = true;
        data.OnItemsAvailabilityChanged = Redraw;
        foreach (var section in data.inventoriesData)
        {
            var widget = new InventoryWidget { Parent = this };
            if (section is InventoryWidgetData inventory) inventory.OnWidgetPressed = _ => { };
            DrawnInventories.Add(widget);
        }
        Redraw();
    }
    public void Redraw()
    {
        if (FailNextRedraw) { FailNextRedraw = false; throw new InvalidOperationException("Native redraw failed"); }
        if (Data.inventoriesData.Count != DrawnInventories.Count) throw new InvalidOperationException("Section count changed without rebuilding widgets");
        for (int i = 0; i < DrawnInventories.Count; i++) DrawnInventories[i].Draw(Data.inventoriesData[i]);
        NavigationRefreshes++; // Models the notification used by native controller rebuilding.
    }
    public void Hide()
    {
        Data.OnItemsAvailabilityChanged = null;
        DrawnInventories.Clear();
        gameObject.activeInHierarchy = false;
        Data.onWidgetHide?.Invoke();
    }
    public T? GetComponentInParent<T>() where T : class => Window as T;
}

public sealed class UIMultiInventoryWindow
{
    public bool IsShown = true;
    public MultiInventoryWidget Widget = new();
    public UIMultiInventoryWindow() { Widget.Window = this; }
    public void Close() { IsShown = false; Widget.Hide(); }
}

namespace UnityEngine
{
    public static class Time { public static float unscaledTime; }
}
