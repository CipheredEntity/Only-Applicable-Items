using System;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal sealed class BodyPartPickerContract
{
    private readonly MethodInfo insertSelected;
    private readonly MethodInfo insertEligible;
    private readonly MethodInfo changeSelected;
    private readonly MethodInfo changeEligible;
    private readonly FieldInfo selectedType;
    private readonly Func<bool> enabled;
    private readonly Action<Item> validateItem;

    public BodyPartPickerContract(Func<bool> enabled, Action<Item> validateItem)
    {
        this.enabled = enabled;
        this.validateItem = validateItem;
        insertSelected = RequireMethod("TryInsertMainOrgan", typeof(void), typeof(UIItemCell));
        insertEligible = RequireMethod("CanInsertOrgan", typeof(bool), typeof(Item));
        changeSelected = RequireMethod("TryChangeMainOrgan", typeof(void), typeof(UIItemCell));
        changeEligible = RequireMethod("CanChangeOrgan", typeof(bool), typeof(Item));
        selectedType = typeof(UIAutopsyWindowData).GetField("selectedItemType", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(UIAutopsyWindowData).FullName, "selectedItemType");
        if (selectedType.FieldType != typeof(ItemType))
            throw new NotSupportedException("The autopsy picker's selected category representation has changed.");
    }

    public PickerPolicy<Item>? GetPolicy(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
    {
        if (!(predicate?.Target is UIAutopsyWindowData) ||
            (!DelegateContext.Matches(callback, predicate, insertSelected, insertEligible) &&
             !DelegateContext.Matches(callback, predicate, changeSelected, changeEligible)))
            return null;

        if (!(selectedType.GetValue(predicate.Target) is ItemType category))
            throw new InvalidOperationException("The autopsy picker's selected category is unavailable.");
        var title = GetTitle(category);
        if (title == null)
            return null;

        // Snapshot the clicked category for visibility/title, but keep the live native
        // predicate for collar limits, surgical mistakes, and click-time validation.
        return new PickerPolicy<Item>(title, enabled,
            item => item != null && !item.IsEmpty && item.Definition.type == category,
            predicate, validateItem);
    }

    private static string? GetTitle(ItemType category)
    {
        switch (category)
        {
            case ItemType.Brain: return "Brains";
            case ItemType.Heart: return "Hearts";
            case ItemType.Guts: return "Guts";
            case ItemType.Skull: return "Skulls";
            case ItemType.Bones: return "Bones";
            case ItemType.Skin: return "Skins";
            default: return null;
        }
    }

    private static MethodInfo RequireMethod(string name, Type result, params Type[] arguments)
    {
        var method = typeof(UIAutopsyWindowData).GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
        if (method == null || method.ReturnType != result || method.IsStatic)
            throw new MissingMethodException(typeof(UIAutopsyWindowData).FullName, name);
        return method;
    }
}
