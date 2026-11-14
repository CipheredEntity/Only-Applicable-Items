using System;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal sealed class PrMachineContract
{
    internal readonly MethodInfo Picked = Method("OnResourcePicked", typeof(void), false, typeof(UIItemCell), typeof(int));
    internal readonly MethodInfo Remove = Method("TryRemoveLoadedItem", typeof(bool), false, typeof(Item), typeof(int));
    private readonly MethodInfo open = Method("OpenItemCountWindow", typeof(void), false, typeof(UIItemCell));
    private readonly MethodInfo eligible = Method("IsItemAllowed", typeof(bool), false, typeof(Item));
    private readonly MethodInfo find = Method("FindLoadDef", typeof(PrMachineDef), true, typeof(string));
    private readonly FieldInfo source = typeof(UIPanicReductionMachineWindowData).GetField("loadSourceInventory", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException("PR loadSourceInventory");
    private readonly FieldInfo windowData = typeof(UIPanicReductionMachineWindow).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException("PR window data");
    private readonly FieldInfo countData = typeof(UIItemCountWindow).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException("PR quantity dialog data");
    internal PrMachineContract()
    {
        if (source.FieldType != typeof(MultiInventory) || windowData.FieldType != typeof(UIPanicReductionMachineWindowData) ||
            countData.FieldType != typeof(UIItemCountWindowData))
            throw new NotSupportedException("The PR window or inventory representation changed.");
    }
    internal bool Matches(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
        => DelegateContext.Matches(callback, predicate, open, eligible);
    internal MultiInventory Source(UIPanicReductionMachineWindowData owner)
        => (MultiInventory?)source.GetValue(owner) ?? throw new InvalidOperationException("Missing PR source inventories.");
    internal bool IsLive(UIPanicReductionMachineWindow window, UIPanicReductionMachineWindowData owner)
        => window.IsShown && ReferenceEquals(windowData.GetValue(window), owner);
    internal bool IsDialogCurrent(UIItemCountWindow window, UIItemCountWindowData? expected)
        => window.IsShown && expected != null && ReferenceEquals(countData.GetValue(window), expected);
    internal void ValidateEffect(UIPanicReductionMachineWindowData owner, string id, int count)
    {
        var definition = (PrMachineDef?)find.Invoke(null, new object[] { id });
        if (definition == null || definition.dayIncreasePerItem <= 0 ||
            (long)count * definition.dayIncreasePerItem + 1 + Math.Max(0, MainGame.PlayerData.GetResInt("pr_machine_left_active_days")) > int.MaxValue)
            throw new NotSupportedException("The PR effect definition or quantity cannot be safely aggregated.");
    }
    private static MethodInfo Method(string name, Type result, bool isStatic, params Type[] args)
    {
        var method = typeof(UIPanicReductionMachineWindowData).GetMethod(name,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, args, null);
        if (method == null || method.IsStatic != isStatic || method.ReturnType != result || method.IsGenericMethod)
            throw new MissingMethodException("Unsupported PR method: " + name);
        return method;
    }
}
