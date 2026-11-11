using System;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal sealed class SermonPickerContract
{
    private readonly MethodInfo eligible;
    private readonly Func<bool> enabled;
    private readonly Action<Item> validateItem;

    public SermonPickerContract(Func<bool> enabled, Action<Item> validateItem)
    {
        this.enabled = enabled;
        this.validateItem = validateItem;
        eligible = typeof(UIPrayWindowData).GetMethod("CanUsePray",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
            null, new[] { typeof(Item) }, null)
            ?? throw new MissingMethodException(typeof(UIPrayWindowData).FullName, "CanUsePray");
        if (eligible.ReturnType != typeof(bool))
            throw new NotSupportedException("The prayer stand's sermon predicate signature has changed.");
    }

    public PickerPolicy<Item>? GetPolicy(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
    {
        if (callback == null || !(predicate?.Target is UIPrayWindowData owner) ||
            !DelegateContext.MatchesPredicate(callback, predicate, eligible) ||
            !ReferenceEquals(callback.Target, owner) ||
            callback.Method.DeclaringType != typeof(UIPrayWindowData))
            return null;

        // The native callback is an inline lambda bound to the prayer data; do
        // not depend on its generated name. CanUsePray supplies the entire native
        // item filter. Attendance and success chance remain in the prayer UI.
        return new PickerPolicy<Item>(PickerId.Sermons, enabled, predicate, predicate, validateItem);
    }
}
