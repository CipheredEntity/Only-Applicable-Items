using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// In-memory Item/Inventory and a small PR-effect boundary. These are not Unity
// objects and do not execute proprietary game code. Metadata checks below verify
// that the production bridge's narrow IL seam also exists in the 1.012 assembly.
public abstract class SerializedItemProperty { }
public sealed class TakeOnlySerializedItemProperty : SerializedItemProperty { }
public sealed class AutoExpandSerializedItemProperty : SerializedItemProperty { }
public sealed class ItemDef { public int stackCount = 50; public bool isBag, hasDurability; }
public sealed class Item
{
    private int count, inventoryFillSize = -1;
    private List<Item> inventory = new List<Item>();
    private Dictionary<Type, SerializedItemProperty> properties = new Dictionary<Type, SerializedItemProperty>();
    public string id;
    public int Count { get => count; set => count = value; }
    public int InventoryCount => inventory.Count;
    private int inventorySize;
    public int InventorySize { get => inventorySize; set => inventorySize = value; }
    public List<Item> Inventory => inventory;
    public Guid UniqueId { get; } = Guid.NewGuid();
    public ItemDef Definition { get; } = new ItemDef();
    public bool IsBag => Definition.isBag;
    public bool IsEmpty => count <= 0;
    public Item() { id = "empty"; count = -1; }
    public Item(string id, int count = 1) { PrColdStartBalance.ReadDefinition(); this.id = id; this.count = count; }
    public bool HasProperty<T>() where T : SerializedItemProperty => properties.ContainsKey(typeof(T));
    public int Fill => inventoryFillSize;
    public Item Split(int count, bool transferInternalData = false) => throw new InvalidOperationException("No native split may run during atomic consumption.");
    public Item RemoveItemFromInventoryByUID(Guid uid, int count = -1) => throw new InvalidOperationException("No partial native removal may run during atomic consumption.");
    private void CalculateInventoryFillSize() { inventoryFillSize = inventory.Count; }
}
public sealed class Inventory
{
    private Item inventoryItem = new Item();
    public Item Data => inventoryItem;
    public event Action<List<Item>>? OnItemsRemove;
    public void NotifyItemsRemoved(List<Item> items) => OnItemsRemove?.Invoke(items);
    public bool RemoveItemFromInventoryByUID(Item item, int count = -1) => throw new InvalidOperationException("No notifying native removal may run during atomic consumption.");
}
public sealed class MultiInventory { public List<Inventory> inventoryList = new List<Inventory>(); }
public sealed class UIItemCell
{
    private Item item;
    public bool ThrowOnRead;
    public UIItemCell(Item item) { this.item = item; }
    public Item DisplayingItem
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get { if (ThrowOnRead) throw new InvalidOperationException("Pooled cell must not be read"); return item; }
    }
}
namespace UnityEngine
{
    public static class Mathf { public static int Min(int a, int b) => Math.Min(a, b); }
}
public sealed class UIPanicReductionMachineWindowData
{
    public Inventory Source = new Inventory();
    public int Effects, Expressions, Days, NativeRemovals;
    public Action? AfterEffect;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Load(UIItemCell cell, int count) => OnResourcePicked(cell, count);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void OnResourcePicked(UIItemCell cell, int count)
    {
        Item selected = cell.DisplayingItem;
        if (selected != null && count > 0)
        {
            count = UnityEngine.Mathf.Min(count, selected.Count);
            if (TryRemoveLoadedItem(selected, count))
            { Effects++; Days += count * 2; Expressions++; AfterEffect?.Invoke(); }
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryRemoveLoadedItem(Item item, int count)
    {
        NativeRemovals++;
        if (!Source.Data.Inventory.Contains(item)) return false;
        if (count >= item.Count) Source.Data.Inventory.Remove(item); else item.Count -= count;
        Source.NotifyItemsRemoved(new List<Item> { item });
        return true;
    }
}
