using System;
using System.Collections.Generic;
using LazyBearTechnology;

// Only PR delegate, data and dialog boundaries. Actual Harmony interception and
// the retained native-body shape are checked separately on the shipped Mono.
public sealed class PrMachineDef { public int dayIncreasePerItem = 2; }
public sealed class UIPanicReductionMachineWindowData
{
    private MultiInventory loadSourceInventory;
    public readonly HashSet<string> Allowed = new(StringComparer.Ordinal);
    public static readonly Dictionary<string, PrMachineDef> Definitions = new();
    public int Effects, Days, Expressions, Redraws, NativeStackMaximum;
    public Action? AfterEffect;
    public UIPanicReductionMachineWindowData(MultiInventory source, params string[] ids)
    {
        loadSourceInventory = source;
        foreach (string id in ids) { Allowed.Add(id); Definitions[id] = new PrMachineDef(); }
    }
    public Action<UIItemCell> Callback => OpenItemCountWindow;
    public Func<Item, bool> Predicate => IsItemAllowed;
    public void ChangeSources(MultiInventory value) => loadSourceInventory = value;
    private bool IsItemAllowed(Item item) => item != null && Allowed.Contains(item.id);
    private static PrMachineDef? FindLoadDef(string id) => Definitions.TryGetValue(id, out var definition) ? definition : null;
    private void OpenItemCountWindow(UIItemCell cell)
    {
        NativeStackMaximum = cell.DisplayingItem!.Count;
        LazyUI.GetWindow<UIItemCountWindow>().Open(new UIItemCountWindowData
        { Item = new Item(cell.DisplayingItem.id), Min = 1, Max = cell.DisplayingItem.Count,
            OnConfirm = count => OnResourcePicked(cell, count), IsForVendor = false });
    }
    private void OnResourcePicked(UIItemCell cell, int count) => throw new InvalidOperationException("The guarded test adapter must replace the unsafe captured-cell callback.");
    private bool TryRemoveLoadedItem(Item item, int count) => throw new InvalidOperationException("The unsafe native helper must not be used for aggregated removal.");
}
public class PrTestWindowBase<T>
{
    protected T? data;
}
public sealed class UIPanicReductionMachineWindow : PrTestWindowBase<UIPanicReductionMachineWindowData>
{
    public bool IsShown;
    public void Open(UIPanicReductionMachineWindowData value) { data = value; IsShown = true; }
}
