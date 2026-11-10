using System;
using System.Collections.Generic;
using System.Reflection;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal sealed class GardenPickerContract
{
    public ConstructorInfo PickerConstructor { get; }
    public MethodInfo InventoryRedrawNotification { get; }
    private readonly MethodInfo selected;
    private readonly MethodInfo eligible;
    private readonly MethodInfo? fertilizerEligible;
    private readonly FieldInfo properties;
    public bool SupportsFertilizers => fertilizerEligible != null;

    public GardenPickerContract()
    {
        selected = RequireMethod(typeof(UIGardenBedWindow), "OnSeedSelected", typeof(void), typeof(UIItemCell));
        eligible = RequireMethod(typeof(UIGardenBedWindow), "IsSeedValid", typeof(bool), typeof(Item));
        // A missing fertilizer method must not disable the already supported seed action.
        fertilizerEligible = FindMethod(typeof(UIGardenBedWindow), "IsItemValid", typeof(bool), typeof(Item));
        InventoryRedrawNotification = RequireMethod(typeof(InventoryWidget), "OnRedraw", typeof(void));
        PickerConstructor = typeof(UIMultiInventoryWindowData).GetConstructor(new[]
        {
            typeof(PlayerData), typeof(Action<UIItemCell>), typeof(Func<Item, bool>),
            typeof(bool), typeof(string), typeof(Func<Item, string>)
        }) ?? throw new MissingMethodException("The supported item-picker constructor was not found.");
        properties = typeof(Item).GetField("properties", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Item).FullName, "properties");
        if (properties.FieldType != typeof(Dictionary<Type, SerializedItemProperty>))
            throw new NotSupportedException("The game's instance-property representation has changed.");
    }

    public PickerPolicy<Item>? GetPolicy(Action<UIItemCell>? callback, Func<Item, bool>? predicate, ActionSettings settings)
    {
        if (predicate == null)
            return null;
        if (DelegateContext.Matches(callback, predicate, selected, eligible))
            return new PickerPolicy<Item>("Seeds", () => settings.Seeds.Value, predicate, predicate, ValidatePlainItem);
        if (fertilizerEligible != null && predicate.Target is UIGardenBedWindow &&
            DelegateContext.MatchesPredicate(callback, predicate, fertilizerEligible))
            return new PickerPolicy<Item>("Fertilizers", () => settings.Fertilizers.Value,
                item => item.IsFertilizer, predicate, ValidatePlainItem);
        return null;
    }

    internal void ValidatePlainItem(Item item)
    {
        // Grouping uses the full definition ID. Reject unknown instance data even for
        // UID-consuming autopsy actions, whose results recreate the part by definition.
        var instanceProperties = (Dictionary<Type, SerializedItemProperty>?)properties.GetValue(item);
        if (item.InventoryCount != 0 || item.InventorySize != 0 || item.IsBag || item.Definition.hasDurability ||
            (instanceProperties != null && instanceProperties.Count != 0))
            throw new NotSupportedException("Item '" + item.id + "' has instance data; preserving the original picker.");
    }

    private static MethodInfo RequireMethod(Type type, string name, Type result, params Type[] arguments)
    {
        return FindMethod(type, name, result, arguments) ?? throw new MissingMethodException(type.FullName, name);
    }

    private static MethodInfo? FindMethod(Type type, string name, Type result, params Type[] arguments)
    {
        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, arguments, null);
        return method != null && method.ReturnType == result && !method.IsStatic ? method : null;
    }
}
