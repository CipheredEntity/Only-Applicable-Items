using System;
using System.Collections.Generic;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal sealed class SeedPickerContract
{
    public ConstructorInfo PickerConstructor { get; }
    public MethodInfo InventoryRedrawNotification { get; }
    private readonly MethodInfo selected;
    private readonly MethodInfo eligible;
    private readonly FieldInfo properties;

    public SeedPickerContract()
    {
        selected = RequireMethod(typeof(UIGardenBedWindow), "OnSeedSelected", typeof(void), typeof(UIItemCell));
        eligible = RequireMethod(typeof(UIGardenBedWindow), "IsSeedValid", typeof(bool), typeof(Item));
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

    public bool IsSeedPicker(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
        => DelegateContext.Matches(callback, predicate, selected, eligible);

    public void ValidateSeed(Item item)
    {
        // Vanilla planting selects/removes by definition ID. Unknown instance data cannot
        // safely be represented as independently selectable variants by this action.
        var instanceProperties = (Dictionary<Type, SerializedItemProperty>?)properties.GetValue(item);
        if (item.InventoryCount != 0 || item.InventorySize != 0 || item.IsBag || item.Definition.hasDurability ||
            (instanceProperties != null && instanceProperties.Count != 0))
            throw new NotSupportedException("Seed '" + item.id + "' has instance data; preserving the original picker.");
    }

    private static MethodInfo RequireMethod(Type type, string name, Type result, params Type[] arguments)
    {
        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, arguments, null);
        if (method == null || method.ReturnType != result || method.IsStatic)
            throw new MissingMethodException(type.FullName, name);
        return method;
    }
}
