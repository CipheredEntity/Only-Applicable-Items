using System;
using System.Collections.Generic;
using OnlyApplicableItems.Integration;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static void StudySelectedSlotUsesTheResolverAndPreservesEveryItemAndQuantity()
    {
        StudyOwner();
        using var display = new StudySelectedItemDisplay(() => true, (_, error) => throw error);
        var source = new Item("research:1", ItemType.None, 10);
        var uid = source.UniqueId;
        var cell = new UIItemCell();
        var resourceCell = new UIItemCell();
        resourceCell.Draw(new Item("faith", ItemType.None, 3));
        Item? nativeDisplay = null;
        var window = new UIResourceBasedCraftWindow(cell)
        {
            SelectedItem = source,
            MainIngredientCount = 100,
            AfterRedraw = (w, selectedCell) => { nativeDisplay = selectedCell.DisplayingItem; display.AfterRedraw(w, selectedCell); }
        };
        window.Redraw();
        Assert(!cell.CounterVisible && cell.Value == 100 && cell.RenderedCount == 100 &&
            ReferenceEquals(cell.DisplayingItem, nativeDisplay) && cell.DisplayingItem!.Count == 100 &&
            ReferenceEquals(window.SelectedItem, source) && source.Count == 10 && source.UniqueId == uid,
            "Only the native slot label is hidden: real selection, UID, source Count, native projection, and cached totals stay unchanged.");
        Assert(resourceCell.CounterVisible && resourceCell.Value == 3, "Supporting requirement counters must not be touched.");
        GameBalance.ResolvedSurveys[source.id].isScienceFuelCraft = true;
        window.Redraw();
        Assert(cell.CounterVisible && cell.Value == 100 && ReferenceEquals(cell.DisplayingItem, nativeDisplay),
            "Use the resolver on each draw, even if the same ID now resolves to decomposition instead of its previous category.");
    }

    private static void StudySelectedSlotSwitchesClearsAndReopensWithoutStaleCounters()
    {
        StudyOwner();
        bool enabled = true;
        using var display = new StudySelectedItemDisplay(() => enabled, (_, error) => throw error);
        var cell = new UIItemCell();
        var window = new UIResourceBasedCraftWindow(cell) { AfterRedraw = display.AfterRedraw };
        window.SelectedItem = new Item("research:1", ItemType.None, 5);
        window.MainIngredientCount = 20;
        window.Redraw();
        Assert(!cell.CounterVisible, "Study selection hides its counter.");
        window.SelectedItem = new Item("decompose", ItemType.None, 2);
        window.MainIngredientCount = 8;
        window.Redraw();
        Assert(cell.CounterVisible && cell.Value == 8, "Switching to decomposition must retain its full native quantity.");
        window.SelectedItem = new Item("research:2", ItemType.None);
        window.MainIngredientCount = 1;
        window.Redraw();
        Assert(!cell.CounterVisible && cell.Value == 1, "A different research definition follows the same resolver rule.");
        window.SelectedItem = null;
        window.Redraw();
        Assert(cell.DisplayingItem == null && !cell.CounterVisible, "Clearing the selection must show no stale quantity.");
        window.SelectedItem = new Item("research:1", ItemType.None, 0);
        window.Redraw();
        Assert(cell.DisplayingItem == null && !cell.CounterVisible, "A depleted selected source must keep the native empty-slot draw.");
        window.SelectedItem = new Item("unknown-survey", ItemType.None, 6);
        window.MainIngredientCount = 6;
        window.Redraw();
        Assert(cell.CounterVisible && cell.Value == 6, "Missing survey definitions must leave native rendering alone.");
        window.SelectedItem = new Item("research:1", ItemType.None, 5);
        window.MainIngredientCount = 20;
        window.Redraw();
        Assert(!cell.CounterVisible, "Release a hidden research slot before testing pooled reuse.");
        window.Hide();
        display.Forget(window);
        int redraws = window.NativeRedraws;
        enabled = false;
        display.Tick();
        Assert(window.NativeRedraws == redraws, "Closing must release tracking before a pooled cell can be reused elsewhere.");
        cell.Draw(new Item("vanilla-stack", ItemType.None, 12));
        Assert(cell.CounterVisible && cell.Value == 12, "A native draw of a pooled cell restores the quantity.");
        window.IsShown = true;
        window.SelectedItem = new Item("research:1", ItemType.None, 5);
        window.MainIngredientCount = 20;
        window.Redraw();
        Assert(cell.CounterVisible && cell.Value == 20, "Reopening while disabled must retain completely native rendering.");
        enabled = true;
        display.Tick();
        Assert(!cell.CounterVisible, "An open study window must respond to enabling without reselecting the item.");
    }

    private static void StudySelectedSlotLiveToggleAndShutdownRestoreFreshNativeTotals()
    {
        StudyOwner();
        bool enabled = false;
        using var display = new StudySelectedItemDisplay(() => enabled, (_, error) => throw error);
        var source = new Item("research:1", ItemType.None, 10);
        var cell = new UIItemCell();
        var window = new UIResourceBasedCraftWindow(cell)
        {
            SelectedItem = source, MainIngredientCount = 100, AfterRedraw = display.AfterRedraw
        };
        window.Redraw();
        Assert(cell.CounterVisible && cell.CountUpdates == 1, "Disabled draws must not perform another counter render or hide it.");
        enabled = true;
        display.Tick();
        Assert(!cell.CounterVisible && cell.Value == 100, "Enable only the research label correction.");
        int redraws = window.NativeRedraws;
        display.Tick();
        Assert(window.NativeRedraws == redraws, "Unchanged settings must not cause recurring redraws.");
        window.MainIngredientCount = 80;
        enabled = false;
        display.Tick();
        Assert(cell.CounterVisible && cell.Value == 80 && source.Count == 10 && ReferenceEquals(window.SelectedItem, source),
            "Disabling uses fresh native window totals without mutating or replacing the real selected stack.");
        enabled = true;
        display.Tick();
        display.Dispose();
        Assert(cell.CounterVisible && cell.Value == 80, "Plugin shutdown restores the native label before unpatching.");
    }

    private static void StudySelectedSlotDisplayFailureDoesNotDisableConsolidationOrRetryNativeFailure()
    {
        var owner = StudyOwner();
        var errors = new List<string>();
        using (var display = new StudySelectedItemDisplay(() => true, (operation, _) => errors.Add(operation)))
        {
            var cell = new UIItemCell();
            var window = new UIResourceBasedCraftWindow(cell)
            {
                SelectedItem = new Item("research:1", ItemType.None, 10), MainIngredientCount = 20, AfterRedraw = display.AfterRedraw
            };
            display.AfterRedraw(window, null!);
            Assert(cell.CounterVisible && cell.Value == 20 && errors.Count == 1 && window.SelectedItem.Count == 10,
                "A display failure must restore native rendering without changing selected items.");
            window.Redraw();
            display.Tick();
            Assert(cell.CounterVisible && errors.Count == 1 &&
                CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)![0].IsEnabled,
                "Failure disables only selected-slot correction, leaving coordinated inventory consolidation available.");
        }
        bool enabled = true;
        using var failed = new StudySelectedItemDisplay(() => enabled, (operation, _) => errors.Add(operation));
        var failedCell = new UIItemCell();
        var failedWindow = new UIResourceBasedCraftWindow(failedCell)
        {
            SelectedItem = new Item("research:1", ItemType.None, 10), MainIngredientCount = 20, AfterRedraw = failed.AfterRedraw
        };
        failedWindow.Redraw();
        int redraws = failedWindow.NativeRedraws;
        failedWindow.FailRedraw = true;
        enabled = false;
        failed.Tick();
        failed.Tick();
        Assert(failedWindow.NativeRedraws == redraws + 1 && errors.Count == 2 && failedWindow.SelectedItem.Count == 10,
            "Never retry a failing native redraw or modify source quantities during recovery.");
    }

    private static void StudySelectedSlotPostfixesUseOnlyTheDedicatedWindowAndCell()
    {
        StudyOwner();
        var previous = Plugin.Instance;
        using var display = new StudySelectedItemDisplay(() => true, (_, error) => throw error);
        var cell = new UIItemCell();
        var otherCell = new UIItemCell();
        otherCell.Draw(new Item("research:1", ItemType.None, 30));
        var window = new UIResourceBasedCraftWindow(cell)
        {
            SelectedItem = new Item("research:1", ItemType.None, 10), MainIngredientCount = 20,
            AfterRedraw = StudySelectedItemPatches.AfterRedraw
        };
        try
        {
            Plugin.Instance = new Plugin { StudySelectedSlot = display };
            window.Redraw();
            Assert(!cell.CounterVisible && otherCell.CounterVisible && otherCell.Value == 30,
                "The production postfix must operate only on the supplied selected slot, not other item displays.");
            window.Hide();
            StudySelectedItemPatches.AfterHidden(window);
            Plugin.Instance = null;
            StudySelectedItemPatches.AfterRedraw(window, cell);
            StudySelectedItemPatches.AfterHidden(window);
        }
        finally { Plugin.Instance = previous; }
    }
}
