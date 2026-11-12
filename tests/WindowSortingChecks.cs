using System;
using System.Collections.Generic;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private sealed class SortingFixture
    {
        public readonly List<object> Stack = new();
        public int Counter;
        public int Initial = 400;
        public bool Enabled = true;
        public bool FailRead;
        public bool FailWrite;
        public bool FailSetting;
        public int Writes;
        public int Warnings;
        public readonly WindowSortingTracker<object> Tracker;
        public SortingFixture()
        {
            Tracker = new WindowSortingTracker<object>(() => FailRead ? throw new InvalidOperationException("read") : Stack,
                () => Counter, n => { if (FailWrite) throw new InvalidOperationException("write"); Writes++; Counter = n; },
                () => Initial, () => FailSetting ? throw new InvalidOperationException("setting") : Enabled,
                (_, __) => Warnings++);
        }
        public void Add(object window, Action? callback = null)
        {
            var operation = Tracker.Begin(window, true);
            Exception? failure = null;
            try { Stack.Add(window); callback?.Invoke(); Counter = Stack.Count > 1 ? unchecked(Counter + 15) : Initial; }
            catch (Exception exception) { failure = exception; throw; }
            finally { Tracker.End(operation, failure); }
        }
        public void Remove(object window, Action? callback = null)
        {
            var operation = Tracker.Begin(window, false);
            Exception? failure = null;
            try { if (Stack.Remove(window)) callback?.Invoke(); }
            catch (Exception exception) { failure = exception; throw; }
            finally { Tracker.End(operation, failure); }
        }
        public void FreshRoot(object root)
        {
            foreach (object window in Stack.ToArray()) Remove(window);
            Add(root);
        }
    }

    private static void SortingReusesOnlySurvivingAssignments()
    {
        var f = new SortingFixture(); var root = new object(); var child = new object();
        f.Add(root);
        for (int i = 0; i < 1000; i++) { f.Add(child); Assert(f.Counter == 415, "Nested allocation remains 15 above root."); f.Remove(child); }
        Assert(f.Counter == 400 && f.Stack.Count == 1 && ReferenceEquals(f.Stack[0], root) && f.Warnings == 0,
            "Repeated pooled windows must not accumulate or change source stack membership.");
        int writes = f.Writes;
        f.Remove(child); f.Remove(new object());
        Assert(f.Counter == 400 && f.Writes == writes, "Removing unopened windows remains a native no-op.");
        f.Remove(root);
        Assert(f.Counter == 400 && f.Writes == writes, "Closing the last root must not write an idle counter.");
    }

    private static void SortingPreservesGapsAndHandlesRootRemoval()
    {
        var f = new SortingFixture(); var root = new object(); var middle = new object(); var top = new object(); var last = new object();
        f.Add(root); f.Add(middle); f.Add(top); f.Remove(middle); f.Add(last);
        Assert(f.Counter == 445, "Depth-based renumbering would collide with the surviving top at 430.");
        f.Remove(root); f.Remove(last);
        Assert(f.Counter == 430 && f.Stack.Count == 1 && ReferenceEquals(f.Stack[0], top), "Out-of-order root closure leaves surviving allocation intact.");
        f.Add(root); Assert(f.Counter == 445, "Reopened object receives a new assignment, not its old root allocation.");
        f.Remove(root); f.Remove(top); f.Add(root);
        Assert(f.Counter == 400 && f.Warnings == 0, "A fresh root releases prior gaps.");
    }

    private static void SortingConfigIsIndependentAndWaitsForFreshRoot()
    {
        var config = new BepInEx.Configuration.ConfigFile();
        var actions = new ActionSettings(config); var fixes = new FixSettings(config);
        Assert(fixes.WindowSortingOrder.Value && config.Bind("Fixes", "Window Sorting Order", false, "").Value,
            "Exact independent default-on Fixes key.");
        fixes.WindowSortingOrder.Value = false;
        Assert(actions.Seeds.Value && actions.Fertilizers.Value && actions.StudyTable.Value && actions.AlchemicalIngredients.Value,
            "Global fix must not depend on or change action settings.");
        var f = new SortingFixture(); var root = new object(); var child = new object(); f.Add(root);
        f.Enabled = false; f.Tracker.ObserveConfiguration(); f.Add(child); f.Remove(child);
        Assert(f.Counter == 415 && f.Writes == 0, "Disabled uses native accumulation without writes.");
        f.Enabled = true; f.Tracker.ObserveConfiguration(); f.Add(child); f.Remove(child);
        Assert(f.Counter == 430 && f.Writes == 0, "Live enabling must not adopt unknown historical allocations.");
        f.FreshRoot(root); f.Add(child); f.Remove(child);
        Assert(f.Counter == 400, "Fresh root starts corrected allocation.");
    }

    private static void SortingLateInitializationDoesNotInventHistory()
    {
        var f = new SortingFixture(); var root = new object(); var child = new object();
        f.Stack.Add(root); f.Counter = 685; f.Add(child); f.Remove(child);
        Assert(f.Counter == 700 && f.Writes == 0, "Existing inflated windows are left untouched.");
        f.FreshRoot(root); f.Add(child); f.Remove(child);
        Assert(f.Counter == 400, "Recovery requires a fresh root, not canvas-based inference.");
    }

    private static void SortingReentrantAddsAndRemovesSuspendWithoutReplay()
    {
        var f = new SortingFixture(); var root = new object(); var child = new object(); var nested = new object();
        f.Add(root); int callbacks = 0;
        f.Add(child, () => { callbacks++; f.Add(nested); });
        Assert(f.Counter == 430 && f.Warnings == 1 && callbacks == 1 && f.Writes == 0,
            "Reentrant addition must retain native behavior and suspend without replaying callbacks.");
        f.Remove(child, () => { callbacks++; f.Remove(nested); });
        Assert(f.Counter == 430 && callbacks == 2, "Reentrant removals cannot silently restart correction.");
        f.FreshRoot(root); f.Add(child); f.Remove(child);
        Assert(f.Counter == 400, "Depth bookkeeping recovers at a fresh root.");
    }

    private static void SortingNativeFailurePreservesIdentityAndPartialState()
    {
        var f = new SortingFixture(); var root = new object(); var child = new object(); f.Add(root);
        var failure = new InvalidOperationException("original");
        try { f.Add(child, () => throw failure); throw new Exception("Expected failure."); }
        catch (InvalidOperationException exception) { Assert(ReferenceEquals(exception, failure), "Native exception identity remains intact."); }
        Assert(f.Stack.Count == 2 && f.Counter == 400 && f.Writes == 0, "Partial native state is not rolled back or retried.");
        f.FreshRoot(root); f.Add(child); f.Remove(child);
        Assert(f.Counter == 400, "Failed transaction does not strand the depth guard.");
        try { f.Remove(root, () => throw failure); throw new Exception("Expected failure."); }
        catch (InvalidOperationException exception) { Assert(ReferenceEquals(exception, failure), "Native removal failure is also preserved."); }
        Assert(f.Stack.Count == 0, "Native source removal remains intact after its callback failure.");
    }

    private static void SortingUnexpectedCounterChangesAreNotOverwritten()
    {
        var f = new SortingFixture(); var root = new object(); var child = new object(); f.Add(root); f.Counter = 600;
        f.Add(child); f.Remove(child);
        Assert(f.Counter == 615 && f.Writes == 0 && f.Warnings == 1, "Another mod's counter change suspends correction.");
        f.FreshRoot(root); f.Add(child, () => f.Counter = 650); f.Remove(child);
        Assert(f.Counter == 665 && f.Warnings == 2, "Unexpected allocation during callbacks is also preserved.");
    }

    private static void SortingUnexpectedMembershipAndSkippedOriginalSuspend()
    {
        var f = new SortingFixture(); var root = new object(); var child = new object(); f.Add(root);
        f.Stack.Add(new object()); f.Add(child); f.Remove(child);
        Assert(f.Counter == 415 && f.Writes == 0 && f.Warnings == 1, "Untracked source membership is not repaired by guessing.");
        f.FreshRoot(root);
        var operation = f.Tracker.Begin(child, true); f.Tracker.End(operation, null); // Another Harmony prefix skipped Add.
        f.Add(child); f.Remove(child);
        Assert(f.Counter == 415 && f.Warnings == 2, "Skipped native original invalidates this session.");
        f.FreshRoot(root); f.Add(child);
        operation = f.Tracker.Begin(child, false); f.Tracker.End(operation, null); // Skipped Remove.
        Assert(f.Counter == 415 && f.Stack.Count == 2 && f.Warnings == 3, "Skipped removal cannot release an allocation still in use.");
    }

    private static void SortingDuplicateStateAndReadFailuresFailConservatively()
    {
        var f = new SortingFixture(); var root = new object(); var child = new object(); f.Add(root); f.Stack.Add(root);
        f.Add(child); Assert(f.Counter == 415 && f.Writes == 0 && f.Warnings == 1, "Duplicate native state does not get rewritten.");
        f.Stack.Clear(); f.FreshRoot(root); f.FailRead = true; f.Add(child);
        Assert(f.Counter == 415 && f.Writes == 0 && f.Warnings == 2, "Read failure must not stop native execution.");
        f.FailRead = false; f.FreshRoot(root); f.Add(child); f.Remove(child);
        Assert(f.Counter == 400, "Preparation failure still balances its finalizer.");
    }

    private static void SortingWriteAndSettingFailuresDoNotEscape()
    {
        var f = new SortingFixture(); var root = new object(); var child = new object(); f.Add(root); f.Add(child);
        f.FailWrite = true; f.Remove(child);
        Assert(f.Counter == 415 && f.Warnings == 1 && f.Stack.Count == 1, "Setter failure preserves native removal and remaining counter.");
        f.FailWrite = false; f.FreshRoot(root); f.FailSetting = true; f.Add(child); f.Remove(child);
        Assert(f.Counter == 415, "Uncertain configuration cannot enable counter writes.");
        f.FailSetting = false; f.FreshRoot(root); f.Add(child); f.Remove(child);
        Assert(f.Counter == 400, "Configuration failure recovery waits for a new root.");
    }

    private static void SortingLiveChangesDuringCallbacksAndDisposeStayNative()
    {
        var f = new SortingFixture(); var root = new object(); var child = new object(); f.Add(root);
        f.Add(child, () => f.Enabled = false); f.Remove(child);
        Assert(f.Counter == 415 && f.Writes == 0, "Toggle during native callbacks cancels finalizer correction.");
        f.Enabled = true; f.Tracker.ObserveConfiguration(); f.FreshRoot(root); f.Add(child);
        f.Tracker.Dispose(); f.Remove(child); f.Add(child); f.Remove(child);
        Assert(f.Counter == 430 && f.Writes == 0, "Disposal changes no canvas or counter and later callbacks remain native.");
    }

    private static void SortingHonorsNativeBaseAndGuardsOverflow()
    {
        var f = new SortingFixture { Initial = 500 }; var root = new object(); var child = new object();
        f.Add(root); f.Add(child); f.Remove(child);
        Assert(f.Counter == 500, "Use the public native initial value rather than hardcoding 400.");
        f.Initial = 550; f.Add(child); f.Remove(child);
        Assert(f.Counter == 515 && f.Warnings == 1, "Changing the initial contract mid-session suspends correction.");
        f.FreshRoot(root); f.Add(child); f.Remove(child); Assert(f.Counter == 550, "New base is adopted at fresh root.");
        f.Remove(root); f.Initial = int.MaxValue - 5; f.Add(root); int writes = f.Writes; f.Add(child);
        Assert(f.Writes == writes && f.Counter == unchecked(int.MaxValue - 5 + 15) && f.Warnings == 2,
            "Unsafe arithmetic suspends correction without replacing native allocation behavior.");
    }

    private static void SortingFinalizersAreIdempotentAndDiagnosticsCannotBreakUi()
    {
        var stack = new List<object>(); int counter = 0;
        var tracker = new WindowSortingTracker<object>(() => stack, () => counter, n => counter = n,
            () => 400, () => true, (_, __) => throw new Exception("logger"));
        var window = new object(); var operation = tracker.Begin(window, true);
        stack.Add(window); counter = 400; tracker.End(operation, null); tracker.End(operation, null);
        var child = new object(); operation = tracker.Begin(child, true); stack.Add(child); counter = 415; tracker.End(operation, null);
        operation = tracker.Begin(child, false); stack.Remove(child); tracker.End(operation, null);
        Assert(counter == 400, "Repeated finalization cannot unbalance subsequent operation depth.");
        counter = 650; operation = tracker.Begin(child, true); stack.Add(child); counter += 15; tracker.End(operation, null);
        Assert(counter == 665, "Even a throwing diagnostic cannot escape into native UI.");
    }
}
