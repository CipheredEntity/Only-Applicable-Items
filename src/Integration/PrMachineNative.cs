using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using HarmonyLib;

namespace OnlyApplicableItems.Integration;

// Retain the native effect body. Only its selected-item read is redirected to a
// detached count carrier during an authorized operation. Its fresh UID can never
// remove a real item if the removal interception is unexpectedly absent.
internal sealed class PrMachineNative : IDisposable
{
    private readonly Harmony harmony;
    private readonly MethodInfo picked, remove;
    private readonly MethodInfo[] storageMethods;
    private static Invocation? invocation;
    private bool disposed;
    private sealed class Invocation
    {
        internal readonly UIPanicReductionMachineWindowData Owner;
        internal readonly UIItemCell Cell;
        internal readonly Item Carrier;
        internal readonly PrMachineStock.Consumption Plan;
        internal bool Entered, Removed;
        internal Invocation(UIPanicReductionMachineWindowData owner, UIItemCell cell, Item carrier, PrMachineStock.Consumption plan)
        { Owner = owner; Cell = cell; Carrier = carrier; Plan = plan; }
    }

    internal PrMachineNative(string owner, MethodInfo picked, MethodInfo remove)
    {
        this.picked = picked; this.remove = remove; harmony = new Harmony(owner);
        storageMethods = new[]
        {
            typeof(Item).GetProperty(nameof(Item.Count))?.GetGetMethod(),
            typeof(Item).GetProperty(nameof(Item.Count))?.GetSetMethod(),
            typeof(Item).GetProperty(nameof(Item.Inventory))?.GetGetMethod(),
            typeof(Inventory).GetProperty(nameof(Inventory.Data))?.GetGetMethod(),
            typeof(Item).GetMethod("Split", new[] { typeof(int), typeof(bool) }),
            typeof(Item).GetMethod("RemoveItemFromInventoryByUID", new[] { typeof(Guid), typeof(int) }),
            typeof(Inventory).GetMethod("RemoveItemFromInventoryByUID", new[] { typeof(Item), typeof(int) }),
            typeof(Item).GetMethod("CalculateInventoryFillSize", BindingFlags.Instance | BindingFlags.NonPublic)
        }.Select(method => method ?? throw new NotSupportedException("The native PR storage contract changed.")).ToArray();
        if (invocation != null) throw new NotSupportedException("A PR load is already executing.");
        EnsureUnmodified(picked); EnsureUnmodified(remove);
        foreach (var method in storageMethods) EnsureUnmodified(method);
        ValidateBody(PatchProcessor.GetOriginalInstructions(picked));
        try
        {
            harmony.Patch(picked, prefix: Patch(nameof(BeforePicked)), transpiler: Patch(nameof(RewriteSelection)));
            harmony.Patch(remove, prefix: Patch(nameof(BeforeRemove)));
        }
        catch { harmony.UnpatchSelf(); throw; }
    }

    private static HarmonyMethod Patch(string method) => new HarmonyMethod(typeof(PrMachineNative), method);
    private void EnsureUnmodified(MethodInfo method)
    {
        var patches = Harmony.GetPatchInfo(method);
        if (patches != null && patches.Owners.Any(owner => owner != harmony.Id))
            throw new NotSupportedException("Another mod changes PR loading; preserving vanilla selection.");
    }
    internal void Verify()
    {
        if (disposed) throw new ObjectDisposedException(nameof(PrMachineNative));
        EnsureUnmodified(picked); EnsureUnmodified(remove);
        foreach (var method in storageMethods) EnsureUnmodified(method);
        var p = Harmony.GetPatchInfo(picked); var r = Harmony.GetPatchInfo(remove);
        if (p == null || r == null ||
            !p.Prefixes.Any(x => x.owner == harmony.Id && x.PatchMethod.Name == nameof(BeforePicked)) ||
            !p.Transpilers.Any(x => x.owner == harmony.Id && x.PatchMethod.Name == nameof(RewriteSelection)) ||
            !r.Prefixes.Any(x => x.owner == harmony.Id && x.PatchMethod.Name == nameof(BeforeRemove)))
            throw new NotSupportedException("PR loading hooks are no longer intact.");
    }

    private static void ValidateBody(IReadOnlyList<CodeInstruction> body)
    {
        var getter = typeof(UIItemCell).GetProperty(nameof(UIItemCell.DisplayingItem))!.GetGetMethod();
        if (body.Count(i => Equals(i.operand, getter)) != 1 ||
            body.Count(i => i.operand is MethodInfo m && m.Name == "TryRemoveLoadedItem" && m.DeclaringType == typeof(UIPanicReductionMachineWindowData)) != 1 ||
            body.Count(i => i.operand is MethodInfo m && m.DeclaringType == typeof(UnityEngine.Mathf) && m.Name == "Min") != 1)
            throw new NotSupportedException("The native PR loading body changed.");
    }
    private static IEnumerable<CodeInstruction> RewriteSelection(IEnumerable<CodeInstruction> instructions)
    {
        var body = instructions.ToList(); ValidateBody(body);
        var getter = typeof(UIItemCell).GetProperty(nameof(UIItemCell.DisplayingItem))!.GetGetMethod();
        var replacement = typeof(PrMachineNative).GetMethod(nameof(ReadSelection), BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var instruction in body)
        {
            if (Equals(instruction.operand, getter)) { instruction.opcode = OpCodes.Call; instruction.operand = replacement; }
            yield return instruction;
        }
    }
    private static Item ReadSelection(UIItemCell cell)
        => invocation != null && invocation.Entered ? invocation.Carrier : cell.DisplayingItem;
    private static bool BeforePicked(UIPanicReductionMachineWindowData __instance, UIItemCell __0)
    {
        var current = invocation;
        if (current == null) return true;
        if (current.Entered || !ReferenceEquals(current.Owner, __instance) || !ReferenceEquals(current.Cell, __0)) return false;
        current.Entered = true;
        return true;
    }
    private static bool BeforeRemove(UIPanicReductionMachineWindowData __instance, Item __0, int __1, ref bool __result)
    {
        var current = invocation;
        if (current == null) return true;
        __result = false;
        if (!current.Entered || current.Removed || !ReferenceEquals(current.Owner, __instance) ||
            !ReferenceEquals(current.Carrier, __0) || __1 != current.Plan.Count || __0.id != current.Plan.Id) return false;
        current.Removed = true;
        __result = current.Plan.TryCommit();
        return false;
    }

    internal void Run(UIPanicReductionMachineWindowData owner, UIItemCell cell, Item carrier,
        PrMachineStock.Consumption plan, Action<Exception> warn)
    {
        Verify();
        if (invocation != null) throw new InvalidOperationException("Reentrant PR load.");
        if (carrier.id != plan.Id) throw new InvalidOperationException("PR carrier does not match the prepared source definition.");
        invocation = new Invocation(owner, cell, carrier, plan);
        try { picked.Invoke(owner, new object[] { cell, plan.Count }); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        finally
        {
            try { plan.Notify(warn); }
            finally
            {
                invocation = null;
                if (disposed)
                    try { harmony.UnpatchSelf(); }
                    catch (Exception exception) { try { warn(exception); } catch { } }
            }
        }
    }
    public void Dispose()
    {
        disposed = true;
        if (invocation == null) harmony.UnpatchSelf();
    }
}
