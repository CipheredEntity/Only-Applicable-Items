using System;
using System.Collections.Generic;
using System.Linq;
using OnlyApplicableItems.Integration;

// Test boundaries model native ID-based transfer and its partial/singleton hazards.
// They do not execute the game's trading, pricing expressions, or Unity rendering.
public abstract class SerializedItemProperty { }
public sealed class TestItemProperty : SerializedItemProperty { }
public sealed class AutoExpandSerializedItemProperty : SerializedItemProperty { }
public static class TestItemDefinitions { public static readonly Dictionary<string, ItemDef> Values = new(); }
public sealed partial class ItemDef { public string id = ""; }
public sealed partial class Item
{
    private readonly Dictionary<Type, SerializedItemProperty> properties = new();
    private int inventoryFillSize = -1;
    public void AddTestProperty() => properties[typeof(TestItemProperty)] = new TestItemProperty();
    public bool HasProperty<T>() where T : SerializedItemProperty => properties.ContainsKey(typeof(T));
    public void AddAutoExpandForCheck() => properties[typeof(AutoExpandSerializedItemProperty)] = new AutoExpandSerializedItemProperty();
    public int CachedFillForCheck { get => inventoryFillSize; set => inventoryFillSize = value; }
    public int InventoryFillSize
    {
        get { if (inventoryFillSize == -1) RecalculateFillForCheck(); return inventoryFillSize; }
    }
    public void RecalculateFillForCheck()
    {
        inventoryFillSize = Inventory.Count(item => !item.IsEmpty);
        foreach (var bag in Inventory.Where(item => item.IsBag)) bag.RecalculateFillForCheck();
    }
    public int InventoryCount => Inventory.Count;
    public Item(string id, int count = 1)
    {
        this.id = id; Count = count;
        Definition = TestItemDefinitions.Values.TryGetValue(id, out var definition) ? definition : new ItemDef { id = id };
    }
    public int GetTotalCountInInventory(string id) => Inventory.Where(i => i.id == id).Sum(i => i.Count) +
        Inventory.Where(i => i.IsBag).Sum(b => b.Inventory.Where(i => i.id == id).Sum(i => i.Count));
    public int CanAddItemCountToInventory(ItemDef definition, int count)
    {
        // Intentionally use the raw cache, including its native -1 initial value.
        // VendorDealInventoryWidget does not warm it when drawing the empty deal.
        int slots = (HasProperty<AutoExpandSerializedItemProperty>() ? 1000 : InventorySize) - inventoryFillSize;
        int capacity = slots * definition.stackCount + Inventory.Where(i => i.id == definition.id).Sum(i => definition.stackCount - i.Count);
        foreach (var bag in Inventory.Where(i => i.IsBag))
            capacity += (bag.InventorySize - bag.inventoryFillSize) * definition.stackCount +
                bag.Inventory.Where(i => i.id == definition.id).Sum(i => definition.stackCount - i.Count);
        return Math.Min(capacity, count);
    }
}
public sealed partial class Inventory
{
    public bool LastNativeAddSucceeded;
    public bool CanAddItemToInventory(Item item) => Data.CanAddItemCountToInventory(item.Definition, item.Count) == item.Count;
    public int NativeAdd(Item item)
    {
        LastNativeAddSucceeded = false;
        int fit = Data.CanAddItemCountToInventory(item.Definition, item.Count);
        if (fit <= 0) return 0;
        if (item.Definition.stackCount == 1) fit = 1; // Deliberately retain the native hazard.
        int left = fit;
        int actual = 0, reported = 0;
        foreach (var inventory in new[] { Data }.Concat(Data.Inventory.Where(i => i.IsBag)).ToArray())
        {
            foreach (var stack in inventory.Inventory.Where(i => i.id == item.id))
            { int n = Math.Min(left, stack.Definition.stackCount - stack.Count); stack.Count += n; left -= n; actual += n; reported += n; }
            if (item.Definition.stackCount == 1)
            {
                if (left > 0 && inventory.InventoryFillSize != inventory.InventorySize)
                { inventory.Inventory.Add(new Item(item.id, 1)); actual++; reported++; left--; inventory.RecalculateFillForCheck(); }
                continue;
            }
            // Match the native split-before-insert loop: a -1 cache can budget
            // seven stacks for six slots, report the seventh, and discard it.
            int planned = Math.Min(left, (inventory.InventorySize - inventory.CachedFillForCheck) * item.Definition.stackCount);
            while (planned > 0)
            {
                int n = Math.Min(left, item.Definition.stackCount); reported += n; left -= n;
                if (inventory.InventoryFillSize != inventory.InventorySize)
                { inventory.Inventory.Add(new Item(item.id, n)); actual += n; inventory.RecalculateFillForCheck(); }
                planned -= n;
            }
        }
        LastNativeAddSucceeded = reported > 0;
        Changed();
        return actual;
    }
    public int NativeRemove(string id, int count, Item selected)
    {
        var bag = Data.Inventory.FirstOrDefault(i => i.IsBag && i.Inventory.Any(s => s.UniqueId == selected.UniqueId));
        var inventories = new List<Item>(); if (bag != null) inventories.Add(bag);
        inventories.Add(Data); inventories.AddRange(Data.Inventory.Where(i => i.IsBag && !ReferenceEquals(i, bag)));
        int left = count;
        foreach (var inventory in inventories)
            foreach (var stack in inventory.Inventory.Where(i => i.id == id).Reverse().ToArray())
            {
                int take = Math.Min(left, stack.Count); left -= take;
                if (take == stack.Count) inventory.Inventory.Remove(stack); else stack.Count -= take;
                if (left == 0) { Data.RecalculateFillForCheck(); Removed(); return count; }
            }
        Data.RecalculateFillForCheck(); Removed(); return count - left;
    }
}
public sealed class PlayerData { public Inventory inventory = new("inventory", 40); }
public static class MainGame { public static PlayerData PlayerData = new(); }
public sealed class VendorTierData
{
    public readonly HashSet<string> Products = new();
    public readonly HashSet<string> notBuying = new();
    public bool HasProduct(string id) => Products.Contains(id);
    public bool IsBuyingProduct(string id) => !notBuying.Contains(id);
}
public sealed class VendorDef { public List<VendorTierData> tierDataList = new(); }
public sealed class Vendor
{
    public int CurTier = 1;
    public VendorDef Definition = new();
    public bool CanBuyItemFromPlayer(ItemDef definition) => Definition.tierDataList[CurTier - 1].HasProduct(definition.id) &&
        Definition.tierDataList[CurTier - 1].IsBuyingProduct(definition.id);
}
public sealed class UIVendorWindowData : LazyBearTechnology.LazyWidgetDataBase
{
    public Vendor Vendor = null!;
    public MultiInventoryWidgetData PlayerMultiInventoryWidgetData = new();
    public object VendorSide = new(), BuyDeal = new(), SellDeal = new();
    public Action? OnRedraw;
    public Action? OnWindowClosed;
}
public sealed class UIItemCountWindowData
{
    public delegate int PriceCalculateDelegate(int count);
    public Item Item = null!;
    public int Min, Max, SnapStep = 1;
    public bool IsForVendor;
    public Action<int>? OnConfirm;
    public PriceCalculateDelegate PriceCalculateDel = null!;
}
public sealed class UIItemCountWindow
{
    public bool IsShown;
    public UIItemCountWindowData? Data;
    public Action? OnShown;
    public Action<UIItemCountWindowData>? BeforeOpenForCheck;
    public void Open(UIItemCountWindowData data)
    { BeforeOpenForCheck?.Invoke(data); if (!ShopSellingPatches.BeforeCountOpen(this, data)) return; Data = data; IsShown = true; OnShown?.Invoke(); }
    public void Confirm(int count) { Data!.OnConfirm!(count); IsShown = false; }
}
public sealed class UIVendorWindow : LazyBearTechnology.LazyWidget<UIVendorWindowData>
{
    private readonly MultiInventoryWidget playerInventoryWidget = new();
    public bool IsShown;
    public int NativeRedraws;
    public bool IsShownAndTop = true;
    public bool FailRedraw;
    public string LeftPanelTitle { get; private set; } = "";
    public MultiInventoryWidget PlayerWidget => playerInventoryWidget;
    public readonly LazyBearTechnology.GamepadNavigationController Controller = new();
    public void Open(UIVendorWindowData value)
    {
        data = value; IsShown = true; playerInventoryWidget.Window = this; LeftPanelTitle = "Keeper";
        data.OnRedraw = RedrawLite; playerInventoryWidget.Draw(data.PlayerMultiInventoryWidgetData);
    }
    public void RedrawLite()
    {
        NativeRedraws++; ShopSellingPatches.BeforeRedraw(this);
        if (FailRedraw) throw new InvalidOperationException("native shop redraw");
        playerInventoryWidget.Redraw();
    }
    private void HideWindow() { data.OnWindowClosed?.Invoke(); IsShown = false; playerInventoryWidget.Hide(); ShopSellingPatches.AfterHidden(this); }
    public void CloseForCheck() => HideWindow();
    public T GetComponent<T>() where T : class => (Controller as T)!;
}
public sealed class Trading
{
    private UIVendorWindowData cachedWindowData = null!;
    private Inventory sellInventory = null!;
    public Inventory Sell => sellInventory;
    public int NativeMoves, NativeRemoved, NativeAdded, PriceCalls, CancelCalls;
    public bool ThrowBeforeMove, ThrowAfterAdd;
    public Action? BeforeNativeSingle;
    public Action? BeforeMoveForCheck;
    public Action<UIVendorWindowData>? BeforeFilled;
    public void FillVendorWindowData(UIVendorWindowData data, string vendorId, Action onClosed)
    {
        cachedWindowData = data; sellInventory = new Inventory("inventory", 6);
        data.PlayerMultiInventoryWidgetData.inventoriesData.AddRange(InventoryWidgetDataHelper.GetWidgetsDataForMultiInventory(
            new MultiInventory { inventoryList = { MainGame.PlayerData.inventory } }, null, null, OnPlayerItemPress1,
            OnPlayerItemPress2, null, PlayerItemsAvailableCondition));
        data.OnWindowClosed = () => { CancelForCheck(); onClosed(); };
        BeforeFilled?.Invoke(data);
        ShopSellingPatches.AfterFilled(this, data);
    }
    private bool PlayerItemsAvailableCondition(Item item) => item != null && !item.IsEmpty && cachedWindowData.Vendor.CanBuyItemFromPlayer(item.Definition);
    private void OnPlayerItemPress1(UIItemCell cell)
    {
        if (cell.DisplayingItem!.Count <= 1) OnPlayerItemPress2(cell);
        else OpenItemCountWindow(cell, MainGame.PlayerData.inventory, sellInventory, amount =>
        { int price = 0; for (int i = 0; i < amount; i++) price += GetSingleItemCostInPlayerInventory(cell.DisplayingItem.id, i + 1); return price; });
    }
    private void OnPlayerItemPress2(UIItemCell cell)
    { BeforeNativeSingle?.Invoke(); TryMoveItem(cell, 1, MainGame.PlayerData.inventory, sellInventory); }
    private int GetSingleItemCostInPlayerInventory(string id, int modifier) { PriceCalls++; return 100 - Sell.Data.GetTotalCountInInventory(id) - modifier; }
    private void OpenItemCountWindow(UIItemCell cell, Inventory from, Inventory to, UIItemCountWindowData.PriceCalculateDelegate price)
    {
        if (!to.CanAddItemToInventory(cell.DisplayingItem!)) return;
        var value = new UIItemCountWindowData { Item = new Item(cell.DisplayingItem!.id), Min = 1,
            Max = to.Data.CanAddItemCountToInventory(cell.DisplayingItem.Definition, from.Data.GetTotalCountInInventory(cell.DisplayingItem.id)),
            OnConfirm = count => TryMoveItem(cell, count, from, to), PriceCalculateDel = price, IsForVendor = true };
        LazyBearTechnology.LazyUI.GetWindow<UIItemCountWindow>().Open(value);
    }
    private bool TryMoveItem(UIItemCell cell, int count, Inventory from, Inventory to)
    {
        BeforeMoveForCheck?.Invoke();
        bool result = false;
        if (!ShopSellingPatches.BeforeMove(this, cell, count, from, to, ref result, out var state)) return result;
        Exception? failure = null;
        try
        {
            NativeMoves++;
            if (ThrowBeforeMove) throw new InvalidOperationException("native move before add");
            int added = to.NativeAdd(new Item(cell.DisplayingItem!.id, count)); NativeAdded += added;
            if (ThrowAfterAdd) throw new InvalidOperationException("native move after add");
            if (!to.LastNativeAddSucceeded) return false;
            NativeRemoved += from.NativeRemove(cell.DisplayingItem.id, count, cell.DisplayingItem);
            cachedWindowData.OnRedraw?.Invoke(); return true;
        }
        catch (Exception exception) { failure = exception; throw; }
        finally { ShopSellingPatches.AfterMove(state, failure); }
    }
    public bool OtherTransfer(UIItemCell cell, int count, Inventory from, Inventory to) => TryMoveItem(cell, count, from, to);
    public void CancelForCheck()
    {
        CancelCalls++;
        // Models the vanilla conditional-loss path: failed returns are discarded.
        foreach (var item in sellInventory.Data.Inventory.ToArray()) MainGame.PlayerData.inventory.NativeAdd(item);
        sellInventory.Data.Inventory.Clear(); sellInventory.Data.RecalculateFillForCheck(); cachedWindowData.OnRedraw?.Invoke();
    }
}
namespace LazyBearTechnology
{
    public static class LazyInput { public static bool IsGamepadActive = true; }
    public class LazyWidgetDataBase { }
    public class LazyWidget<T> where T : LazyWidgetDataBase { protected T data = default!; }
    public static class LazyUI
    {
        private static readonly Dictionary<Type, object> windows = new();
        public static T GetWindow<T>() where T : new()
        { if (!windows.TryGetValue(typeof(T), out var value)) windows[typeof(T)] = value = new T(); return (T)value; }
        public static void ResetForCheck() => windows.Clear();
    }
    public sealed class GamepadNavigationItem { public bool isActiveAndEnabled = true, Active = true; }
    public sealed class GamepadNavigationController
    {
        public GamepadNavigationItem? FocusedItem;
        public int Rebuilds;
        public void ReinitItems(bool focusOnFirstActive) => Rebuilds++;
        public void SetFocusedItem(GamepadNavigationItem item) => FocusedItem = item;
        public void FocusOnFirstActive() { }
    }
}
