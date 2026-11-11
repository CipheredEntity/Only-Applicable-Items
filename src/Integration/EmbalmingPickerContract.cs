using System;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal sealed class EmbalmingPickerContract
{
    private readonly MethodInfo selected;
    private readonly MethodInfo eligible;
    private readonly Func<bool> enabled;
    private readonly Action<Item> validateItem;

    public EmbalmingPickerContract(Func<bool> enabled, Action<Item> validateItem)
    {
        this.enabled = enabled;
        this.validateItem = validateItem;
        selected = RequireMethod("TryEmbalm", typeof(void), typeof(UIItemCell));
        eligible = RequireMethod("CanEmbalm", typeof(bool), typeof(Item));
    }

    public PickerPolicy<Item>? GetPolicy(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
    {
        if (!(predicate?.Target is UIEmbalmWindowData) ||
            !DelegateContext.Matches(callback, predicate, selected, eligible))
            return null;

        // All fluids are visible. Keep the native predicate for body capacity,
        // collar limits, and the different rules for consumed zombie-effect fluids.
        return new PickerPolicy<Item>(PickerId.EmbalmingFluids, enabled,
            item => item != null && !item.IsEmpty && item.Definition.type == ItemType.Embalm,
            predicate, validateItem);
    }

    private static MethodInfo RequireMethod(string name, Type result, params Type[] arguments)
    {
        var method = typeof(UIEmbalmWindowData).GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
        if (method == null || method.ReturnType != result || method.IsStatic)
            throw new MissingMethodException(typeof(UIEmbalmWindowData).FullName, name);
        return method;
    }
}
