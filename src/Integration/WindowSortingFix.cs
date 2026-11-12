using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using LazyBearTechnology;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal sealed class WindowSortingFix : IDisposable
{
    private readonly Harmony harmony;
    internal readonly WindowSortingTracker<LazyWidgetBase> Tracker;
    private static WindowSortingFix? active;
    private bool disposed;

    private sealed class Invocation
    {
        internal readonly WindowSortingTracker<LazyWidgetBase> Tracker;
        internal readonly WindowSortingTracker<LazyWidgetBase>.Operation? Operation;
        internal Invocation(WindowSortingTracker<LazyWidgetBase> tracker, LazyWidgetBase window, bool adding)
        { Tracker = tracker; Operation = tracker.Begin(window, adding); }
    }

    public WindowSortingFix(string owner, Func<bool> enabled, Action<string, Exception> warn)
    {
        if (active != null) throw new InvalidOperationException("A window sorting fix is already active.");
        if (Type.GetType("Mono.Runtime") == null)
            throw new NotSupportedException("The generic patch contract is verified for Unity Mono only.");
        Type controller = typeof(LazyWindowsStackController);
        FieldInfo stack = Field(controller, "openedWindowsStack", typeof(List<LazyWidgetBase>));
        FieldInfo counter = Field(controller, "activeWindowSorting", typeof(int));
        FieldInfo initial = Field(controller, "WINDOWS_INITIAL_SORTING_ORDER_VALUE", typeof(int));
        MethodInfo add = Target(controller, "AddToStack");
        MethodInfo remove = Target(controller, "RemoveFromStack");
        ValidateAllocation(add, remove, counter, initial);
        Tracker = new WindowSortingTracker<LazyWidgetBase>(
            () => (List<LazyWidgetBase>)stack.GetValue(null)!, () => (int)counter.GetValue(null)!,
            value => counter.SetValue(null, value), () => (int)initial.GetValue(null)!, enabled, warn);
        harmony = new Harmony(owner);
        active = this;
        try
        {
            // Every legal T is a reference type. On shipped Mono these constructions
            // share one method body. Patching each concrete T would replace that detour.
            harmony.Patch(add, prefix: Patch(nameof(BeforeAdd)), finalizer: Patch(nameof(AfterOperation)));
            harmony.Patch(remove, prefix: Patch(nameof(BeforeRemove)), finalizer: Patch(nameof(AfterOperation)));
        }
        catch (Exception installException)
        {
            try { Dispose(); }
            catch (Exception cleanupException)
            { throw new AggregateException("Sorting patch installation and cleanup failed.", installException, cleanupException); }
            throw;
        }
    }

    public void Tick() => Tracker.ObserveConfiguration();
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (ReferenceEquals(active, this)) active = null;
        Tracker.Dispose();
        harmony.UnpatchSelf();
    }

    private static void BeforeAdd(LazyWidgetBase __0, out Invocation? __state)
        => __state = active == null ? null : new Invocation(active.Tracker, __0, true);
    private static void BeforeRemove(LazyWidgetBase __0, out Invocation? __state)
        => __state = active == null ? null : new Invocation(active.Tracker, __0, false);
    private static Exception? AfterOperation(Invocation? __state, Exception? __exception)
    {
        // Finish on the original owner even if a callback unloaded/reinitialized it.
        __state?.Tracker.End(__state.Operation, __exception);
        return __exception;
    }

    private static HarmonyMethod Patch(string name) => new HarmonyMethod(typeof(WindowSortingFix), name);
    private static FieldInfo Field(Type owner, string name, Type type)
    {
        FieldInfo? field = owner.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null || field.FieldType != type || field.IsInitOnly || field.IsLiteral)
            throw new NotSupportedException("Unsupported window field: " + name);
        return field;
    }
    private static MethodInfo Target(Type owner, string name)
    {
        MethodInfo? method = owner.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        if (method == null || !method.IsGenericMethodDefinition || method.ReturnType != typeof(void) ||
            method.GetGenericArguments().Length != 1 || method.GetParameters().Length != 1)
            throw new NotSupportedException("Unsupported window method: " + name);
        Type argument = method.GetGenericArguments()[0];
        Type parameter = method.GetParameters()[0].ParameterType;
        if (!argument.GetGenericParameterConstraints().Contains(typeof(LazyWidgetDataBase)) ||
            !parameter.IsGenericType || parameter.GetGenericTypeDefinition() != typeof(LazyWindow<>) ||
            parameter.GetGenericArguments()[0] != argument)
            throw new NotSupportedException("Unsupported generic window constraint: " + name);
        return method.MakeGenericMethod(typeof(LazyWidgetDataBase));
    }
    private static void ValidateAllocation(MethodInfo add, MethodInfo remove, FieldInfo counter, FieldInfo initial)
    {
        var instructions = PatchProcessor.GetOriginalInstructions(add);
        int increments = 0;
        for (int i = 0; i + 3 < instructions.Count; i++)
            if (instructions[i].opcode == OpCodes.Ldsfld && Equals(instructions[i].operand, counter) &&
                instructions[i + 1].LoadsConstant(15) && instructions[i + 2].opcode == OpCodes.Add &&
                instructions[i + 3].opcode == OpCodes.Stsfld && Equals(instructions[i + 3].operand, counter)) increments++;
        if (increments != 1 || instructions.Count(i => i.opcode == OpCodes.Stsfld && Equals(i.operand, counter)) != 2 ||
            !instructions.Any(i => i.opcode == OpCodes.Ldsfld && Equals(i.operand, initial)) ||
            PatchProcessor.GetOriginalInstructions(remove).Any(i => i.opcode == OpCodes.Stsfld && Equals(i.operand, counter)))
            throw new NotSupportedException("The native window allocation IL has changed.");
    }
}
