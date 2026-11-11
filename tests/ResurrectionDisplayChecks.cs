using System;
using System.Collections.Generic;
using OnlyApplicableItems.Integration;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static void ResurrectionSlotHidesOnlyItsCounterAndRestoresEveryCachedValue()
    {
        using var display = new ResurrectionCollarDisplay(() => true, (_, error) => throw error);
        var source = new Item("collar:2", ItemType.Collar, 10);
        var uid = source.UniqueId;
        var cell = new UIItemCell { DisplayingItem = source, Value = 73 };
        var inventoryCell = new UIItemCell { DisplayingItem = source, Value = 10 };
        inventoryCell.UpdateCountLabel();
        var owner = new UIResurrectionWindowData();
        var callback = owner.Callback;
        cell.OnItemCellPress = callback;
        var window = new UIResurrectionWindow(cell) { AfterRedraw = display.AfterRedraw };
        display.AfterRedraw(window);
        Assert(!cell.CounterVisible && cell.RenderedCount == 1 && cell.Value == 73,
            "Render one without forcing a counter, then restore the exact native cached value, not a guessed count.");
        Assert(ReferenceEquals(cell.DisplayingItem, source) && source.UniqueId == uid && source.Count == 10 &&
            ReferenceEquals(cell.OnItemCellPress, callback), "Item identity, UID, Count, and native selection callback must remain untouched.");
        Assert(inventoryCell.CounterVisible && inventoryCell.RenderedCount == 10 && inventoryCell.Value == 10 && inventoryCell.CountUpdates == 1,
            "Other cells, including inventory cells referencing the same Item, must not be changed.");
        foreach (int count in new[] { 1, 2, 105 })
        {
            source.Count = count;
            window.RedrawForCheck();
            Assert(!cell.CounterVisible && cell.RenderedCount == 1 && cell.Value == count && source.Count == count,
                "Repeated native draws must hide only the slot counter for any real stack size.");
        }
        cell.OnItemCellPress!(cell);
        Assert(ReferenceEquals(owner.Collar, source), "The native callback must still receive the original real item.");
    }

    private static void ResurrectionSlotToggleRestoresNativeRenderingWithoutChangingSelection()
    {
        bool enabled = false;
        using var display = new ResurrectionCollarDisplay(() => enabled, (_, error) => throw error);
        var source = new Item("collar", ItemType.Collar, 10);
        var cell = new UIItemCell { DisplayingItem = source };
        var window = new UIResurrectionWindow(cell) { AfterRedraw = display.AfterRedraw };
        window.RedrawForCheck();
        Assert(cell.CounterVisible && cell.RenderedCount == 10 && cell.CountUpdates == 1,
            "Disabled from the outset must leave the original native draw completely alone.");
        enabled = true;
        display.Tick();
        Assert(!cell.CounterVisible && cell.Value == 10 && ReferenceEquals(cell.DisplayingItem, source),
            "Enabling while the window is open corrects only the selected slot.");
        int redraws = window.NativeRedraws;
        display.Tick();
        Assert(window.NativeRedraws == redraws, "A stable setting must not cause recurring native redraws.");
        source.Count = 9;
        enabled = false;
        display.Tick();
        Assert(cell.CounterVisible && cell.RenderedCount == 9 && cell.Value == 9 && source.Count == 9,
            "Disabling must use a fresh native draw, restoring current quantities rather than an old snapshot.");
        enabled = true;
        display.Tick();
        display.Dispose();
        Assert(cell.CounterVisible && cell.RenderedCount == 9 && ReferenceEquals(cell.DisplayingItem, source),
            "Plugin shutdown must restore native rendering before removing its patches.");
    }

    private static void ResurrectionSlotLeavesEmptyAndUnrelatedItemsUntouched()
    {
        using var display = new ResurrectionCollarDisplay(() => true, (_, error) => throw error);
        var cell = new UIItemCell();
        var window = new UIResurrectionWindow(cell) { AfterRedraw = display.AfterRedraw };
        foreach (var item in new Item?[] { null, new Item("empty", ItemType.Collar, 0), new Item("other", ItemType.None, 8) })
        {
            cell.DisplayingItem = item;
            int updates = cell.CountUpdates;
            window.RedrawForCheck();
            Assert(cell.CountUpdates == updates + 1 && cell.Value == (item?.Count ?? 0),
                "Null, depleted, and unrelated items must receive only their native draw.");
        }
    }

    private static void ResurrectionSlotFailureRestoresCacheAndLeavesPickerAvailable()
    {
        var errors = new List<string>();
        using var display = new ResurrectionCollarDisplay(() => true, (operation, _) => errors.Add(operation));
        var cell = new UIItemCell { DisplayingItem = new Item("collar", ItemType.Collar, 10), Value = 10 };
        var uid = cell.DisplayingItem.UniqueId;
        var window = new UIResurrectionWindow(cell) { AfterRedraw = display.AfterRedraw };
        cell.ThrowOnNextCountUpdate = true;
        display.AfterRedraw(window);
        Assert(cell.Value == 10 && cell.CounterVisible && cell.RenderedCount == 10 &&
            cell.DisplayingItem.Count == 10 && cell.DisplayingItem.UniqueId == uid && errors.Count == 1,
            "A renderer failure must restore UI caches and attempt one native restoration without touching the item.");
        window.RedrawForCheck();
        display.Tick();
        Assert(cell.CounterVisible && errors.Count == 1, "A failed correction stays inactive without repeated warnings or retries.");
        var policy = ResurrectionPolicy(CreateResurrectionContract(), new UIResurrectionWindowData());
        Assert(policy.IsEnabled, "Display failure must not disable independent resurrection picker consolidation.");
    }

    private static void ResurrectionSlotStopsTrackingHiddenWindowsAndDoesNotRetryNativeFailures()
    {
        bool enabled = true;
        var errors = new List<string>();
        using var display = new ResurrectionCollarDisplay(() => enabled, (operation, _) => errors.Add(operation));
        var cell = new UIItemCell { DisplayingItem = new Item("collar", ItemType.Collar, 10) };
        var window = new UIResurrectionWindow(cell) { AfterRedraw = display.AfterRedraw };
        window.RedrawForCheck();
        window.Hide();
        display.Forget(window);
        int redraws = window.NativeRedraws;
        enabled = false;
        display.Tick();
        Assert(window.NativeRedraws == redraws, "A hidden window must not be redrawn on setting changes.");
        window.IsShown = true;
        window.RedrawForCheck();
        Assert(cell.CounterVisible && cell.Value == 10, "Reopening with the setting off must produce the original native counter.");
        window.FailRedraw = true;
        enabled = true;
        redraws = window.NativeRedraws;
        display.Tick();
        display.Tick();
        Assert(window.NativeRedraws == redraws + 1 && errors.Count == 1 && cell.DisplayingItem.Count == 10,
            "Do not retry a native redraw after it throws, or change underlying stock during recovery.");
    }

    private static void ResurrectionSlotPostfixesUseOnlyTheDedicatedFeature()
    {
        var previous = Plugin.Instance;
        using var display = new ResurrectionCollarDisplay(() => true, (_, error) => throw error);
        var cell = new UIItemCell { DisplayingItem = new Item("collar", ItemType.Collar, 10), Value = 10 };
        var window = new UIResurrectionWindow(cell);
        try
        {
            Plugin.Instance = new Plugin { ResurrectionCollarSlot = display };
            ResurrectionCollarDisplayPatches.AfterRedraw(window);
            Assert(!cell.CounterVisible && cell.Value == 10, "The production postfix must route only to selected-slot correction.");
            window.Hide();
            ResurrectionCollarDisplayPatches.AfterHidden(window);
            Plugin.Instance = null;
            ResurrectionCollarDisplayPatches.AfterRedraw(window);
            ResurrectionCollarDisplayPatches.AfterHidden(window);
        }
        finally { Plugin.Instance = previous; }
    }
}
