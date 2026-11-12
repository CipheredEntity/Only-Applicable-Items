using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LazyBearTechnology;
using Mono.Cecil;
using Mono.Cecil.Cil;
using OnlyApplicableItems.Integration;

internal static class SortingRuntimeChecks
{
    private sealed class A : LazyWidgetDataBase { }
    private sealed class B : LazyWidgetDataBase { }
    private sealed class LateData : LazyWidgetDataBase { }
    private static readonly BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
    private static List<LazyWidgetBase> Stack => (List<LazyWidgetBase>)typeof(LazyWindowsStackController).GetField("openedWindowsStack", Flags)!.GetValue(null)!;
    private static int Counter => (int)typeof(LazyWindowsStackController).GetField("activeWindowSorting", Flags)!.GetValue(null)!;

    public static void Run(string managedPath)
    {
        CheckInstalledMetadata(managedPath);
        Action[] checks = { RepeatedAndLateGenericWindows, OutOfOrderAndOverrides, NativeEventsRemainIdentical,
            NativeExceptionsAndReentrancy, LiveToggleAndLateInitialization, UnexpectedCounterAndCleanup, DisposalDuringCallback };
        foreach (Action check in checks)
        {
            Reset();
            check();
            Console.WriteLine("PASS Mono/Harmony " + check.Method.Name);
        }
        Console.WriteLine("7 controller integration checks passed on shipped Mono/Harmony; installed controller metadata verified.");
    }

    private static void CheckInstalledMetadata(string managedPath)
    {
        using (var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(managedPath, "LazyBearTechnology.dll")))
        {
            var controller = assembly.MainModule.Types.Single(t => t.FullName == "LazyBearTechnology.LazyWindowsStackController");
            var add = controller.Methods.Single(m => m.Name == "AddToStack");
            var remove = controller.Methods.Single(m => m.Name == "RemoveFromStack");
            Assert(add.GenericParameters.Count == 1 && remove.GenericParameters.Count == 1 &&
                add.GenericParameters[0].Constraints.Single().FullName == "LazyBearTechnology.LazyWidgetDataBase" &&
                remove.GenericParameters[0].Constraints.Single().FullName == "LazyBearTechnology.LazyWidgetDataBase",
                "Installed methods must have the same reference-type constraints as the runtime fixture.");
            Assert(add.Parameters[0].ParameterType.FullName.StartsWith("LazyBearTechnology.LazyWindow`1<", StringComparison.Ordinal), "Installed argument shape.");
            Assert(add.Body.Instructions.Count(i => i.OpCode == OpCodes.Stsfld && (i.Operand as FieldReference)?.Name == "activeWindowSorting") == 2,
                "Installed Add must contain the two native counter assignments.");
            Assert(!remove.Body.Instructions.Any(i => i.OpCode == OpCodes.Stsfld && (i.Operand as FieldReference)?.Name == "activeWindowSorting"),
                "Installed Remove must leave the counter alone.");
            var code = add.Body.Instructions;
            Assert(Enumerable.Range(0, code.Count - 3).Count(i =>
                code[i].OpCode == OpCodes.Ldsfld && (code[i].Operand as FieldReference)?.Name == "activeWindowSorting" &&
                code[i + 1].OpCode == OpCodes.Ldc_I4_S && Convert.ToInt32(code[i + 1].Operand) == 15 &&
                code[i + 2].OpCode == OpCodes.Add && code[i + 3].OpCode == OpCodes.Stsfld) == 1,
                "Installed increment must match the production IL guard and unchanged controller fixture.");
        }
        Console.WriteLine("PASS installed LazyBearTechnology.dll generic signatures and allocation IL (metadata only).");
    }

    private static void RepeatedAndLateGenericWindows()
    {
        using (var fix = NewFix(() => true))
        {
            var root = new LazyWindow<A>();
            LazyWindowsStackController.AddToStack(root);
            for (int i = 0; i < 100; i++)
            {
                var child = new LazyWindow<B>();
                LazyWindowsStackController.AddToStack(child);
                Assert(root.Canvas.sortingOrder == 400 && child.Canvas.sortingOrder == 415, "Fresh generic calls use surviving assignments.");
                LazyWindowsStackController.RemoveFromStack(child);
                Assert(Counter == 400 && child.Canvas.Writes == 1, "Closing corrects only the counter.");
            }
            var late = new LazyWindow<LateData>();
            LazyWindowsStackController.AddToStack(late);
            Assert(late.Canvas.sortingOrder == 415 && root.Canvas.Writes == 1, "Previously unused generic instantiation shares the patch.");
        }
        var native = new LazyWindow<B>();
        LazyWindowsStackController.AddToStack(native);
        LazyWindowsStackController.RemoveFromStack(native);
        Assert(Counter == 430, "Unpatching restores native accumulation.");
    }

    private static void OutOfOrderAndOverrides()
    {
        using (var fix = NewFix(() => true))
        {
            var root = new LazyWindow<A>(); var middle = new LazyWindow<B>(); var top = new LazyWindow<A>();
            LazyWindowsStackController.AddToStack(root); LazyWindowsStackController.AddToStack(middle); LazyWindowsStackController.AddToStack(top);
            root.Canvas.sortingOrder = 30000; // Credits-style post-open override.
            LazyWindowsStackController.RemoveFromStack(middle);
            Assert(Counter == 430 && root.Canvas.sortingOrder == 30000 && top.Canvas.Writes == 1, "Preserve live canvas overrides and gaps.");
            var fourth = new LazyWindow<B>(); LazyWindowsStackController.AddToStack(fourth);
            Assert(fourth.Canvas.sortingOrder == 445, "Out-of-order removal must not collide with surviving top.");
            LazyWindowsStackController.RemoveFromStack(fourth); LazyWindowsStackController.RemoveFromStack(top);
            Assert(Counter == 400 && root.Canvas.sortingOrder == 30000, "Recorded assignment is separate from rendered order.");
            LazyWindowsStackController.RemoveFromStack(middle);
            Assert(Counter == 400, "Duplicate removal remains harmless.");
        }
    }

    private static void NativeEventsRemainIdentical()
    {
        string RunTrace(bool enabled)
        {
            Reset();
            var events = new List<string>();
            Action<LazyWidgetBase> opened = _ => events.Add("open");
            Action<LazyWidgetBase> closed = _ => events.Add("close");
            Action<LazyWidgetBase> visible = _ => events.Add("visible");
            Action<LazyWidgetBase> hidden = _ => events.Add("hidden");
            Action all = () => events.Add("all");
            LazyWindowsStackController.OnWindowOpened += opened; LazyWindowsStackController.OnWindowClosed += closed;
            LazyWindowsStackController.OnWindowBecameVisibleInStack += visible; LazyWindowsStackController.OnWindowBecameHiddenInStack += hidden;
            LazyWindowsStackController.OnAllWindowsClosed += all;
            try
            {
                using (var fix = NewFix(() => enabled))
                {
                    var root = new LazyWindow<A> { IsModalWindow = false }; var child = new LazyWindow<B>();
                    LazyWindowsStackController.AddToStack(root); Assert(!LazyWindowsStackController.HasAnyModalWindowOpened, "Nonmodal root.");
                    LazyWindowsStackController.AddToStack(child); Assert(LazyWindowsStackController.ActiveWindow == child && LazyWindowsStackController.HasAnyModalWindowOpened, "Native focus and modality.");
                    LazyWindowsStackController.RemoveFromStack(child); Assert(LazyWindowsStackController.ActiveWindow == root && !LazyWindowsStackController.HasAnyModalWindowOpened, "Native focus restored.");
                    LazyWindowsStackController.RemoveFromStack(root); Assert(LazyWindowsStackController.ActiveWindow == null, "Native empty stack.");
                }
            }
            finally
            {
                LazyWindowsStackController.OnWindowOpened -= opened; LazyWindowsStackController.OnWindowClosed -= closed;
                LazyWindowsStackController.OnWindowBecameVisibleInStack -= visible; LazyWindowsStackController.OnWindowBecameHiddenInStack -= hidden;
                LazyWindowsStackController.OnAllWindowsClosed -= all;
            }
            return string.Join(",", events);
        }
        Assert(RunTrace(true) == RunTrace(false), "Patch must preserve event ordering and counts.");
    }

    private static void NativeExceptionsAndReentrancy()
    {
        int warnings = 0;
        using (var fix = NewFix(() => true, (_, __) => warnings++))
        {
            var root = new LazyWindow<A>(); var child = new LazyWindow<B>(); var nested = new LazyWindow<A>();
            LazyWindowsStackController.AddToStack(root); LazyWindowsStackController.AddToStack(child);
            Action<LazyWidgetBase> reenter = w => { if (ReferenceEquals(w, child)) LazyWindowsStackController.AddToStack(nested); };
            LazyWindowsStackController.OnWindowClosed += reenter;
            try { LazyWindowsStackController.RemoveFromStack(child); }
            finally { LazyWindowsStackController.OnWindowClosed -= reenter; }
            Assert(Counter == 430 && nested.Canvas.sortingOrder == 430 && warnings == 1, "Reentrant operation completes natively without a counter rewrite.");
            LazyWindowsStackController.RemoveFromStack(nested); LazyWindowsStackController.RemoveFromStack(root);
            LazyWindowsStackController.AddToStack(root); LazyWindowsStackController.AddToStack(child); LazyWindowsStackController.RemoveFromStack(child);
            Assert(Counter == 400, "Fresh root recovers after reentrancy.");
            var failure = new InvalidOperationException("native test failure");
            Action<LazyWidgetBase> throwing = _ => throw failure;
            LazyWindowsStackController.OnWindowOpened += throwing;
            try
            {
                try { LazyWindowsStackController.AddToStack(child); throw new Exception("Native exception was suppressed."); }
                catch (InvalidOperationException exception) { Assert(ReferenceEquals(exception, failure), "Preserve original exception identity."); }
            }
            finally { LazyWindowsStackController.OnWindowOpened -= throwing; }
            Assert(child.Canvas.Writes == 2 && warnings == 2, "Failed native add must not retry its canvas assignment.");
        }
    }

    private static void LiveToggleAndLateInitialization()
    {
        var root = new LazyWindow<A>(); var child = new LazyWindow<B>();
        LazyWindowsStackController.AddToStack(root);
        bool enabled = true;
        using (var fix = NewFix(() => enabled))
        {
            LazyWindowsStackController.AddToStack(child); LazyWindowsStackController.RemoveFromStack(child);
            Assert(Counter == 415, "Do not adopt an already-open stack.");
            LazyWindowsStackController.RemoveFromStack(root); LazyWindowsStackController.AddToStack(root);
            LazyWindowsStackController.AddToStack(child); LazyWindowsStackController.RemoveFromStack(child);
            Assert(Counter == 400, "Fresh root starts tracking.");
            enabled = false; fix.Tick();
            LazyWindowsStackController.AddToStack(child); LazyWindowsStackController.RemoveFromStack(child);
            Assert(Counter == 415, "Disabled behavior is native.");
            enabled = true; fix.Tick();
            LazyWindowsStackController.AddToStack(child); LazyWindowsStackController.RemoveFromStack(child);
            Assert(Counter == 430, "Enabling mid-stack waits rather than reconstructs history.");
            LazyWindowsStackController.RemoveFromStack(root); LazyWindowsStackController.AddToStack(root);
            LazyWindowsStackController.AddToStack(child); LazyWindowsStackController.RemoveFromStack(child);
            Assert(Counter == 400, "Re-enable on fresh root.");
        }
    }

    private static void UnexpectedCounterAndCleanup()
    {
        int warnings = 0;
        using (var fix = NewFix(() => true, (_, __) => warnings++))
        {
            var root = new LazyWindow<A>(); var child = new LazyWindow<B>();
            LazyWindowsStackController.AddToStack(root);
            typeof(LazyWindowsStackController).GetField("activeWindowSorting", Flags)!.SetValue(null, 600);
            LazyWindowsStackController.AddToStack(child); LazyWindowsStackController.RemoveFromStack(child);
            Assert(Counter == 615 && warnings == 1 && root.Canvas.Writes == 1, "Foreign counter change suspends instead of overwriting another mod.");
        }
        using (var fix = NewFix(() => true)) { } // Owner and static hooks are released.
    }

    private static void DisposalDuringCallback()
    {
        var first = NewFix(() => true);
        WindowSortingFix? second = null;
        var root = new LazyWindow<A>(); var child = new LazyWindow<B>();
        LazyWindowsStackController.AddToStack(root);
        Action<LazyWidgetBase> reload = _ => { first.Dispose(); second = NewFix(() => true); };
        LazyWindowsStackController.OnWindowOpened += reload;
        try { LazyWindowsStackController.AddToStack(child); }
        finally { LazyWindowsStackController.OnWindowOpened -= reload; first.Dispose(); }
        try
        {
            LazyWindowsStackController.RemoveFromStack(child); LazyWindowsStackController.RemoveFromStack(root);
            LazyWindowsStackController.AddToStack(root); LazyWindowsStackController.AddToStack(child); LazyWindowsStackController.RemoveFromStack(child);
            Assert(Counter == 400, "A pending finalizer must finish on its old owner instead of corrupting a replacement tracker.");
        }
        finally { second?.Dispose(); }
    }

    private static WindowSortingFix NewFix(Func<bool> enabled, Action<string, Exception>? warn = null)
        => new WindowSortingFix("onlyapplicableitems.sorting-runtime", enabled, warn ?? ((_, __) => { }));
    private static void Reset()
    {
        Stack.Clear();
        ((Dictionary<LazyWidgetBase, bool>)typeof(LazyWindowsStackController).GetField("windowsWithModalStatus", Flags)!.GetValue(null)!).Clear();
        typeof(LazyWindowsStackController).GetField("activeWindow", Flags)!.SetValue(null, null);
        typeof(LazyWindowsStackController).GetField("activeWindowSorting", Flags)!.SetValue(null, 0);
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
