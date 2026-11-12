using System;
using System.Collections.Generic;
using System.Reflection;

namespace OnlyApplicableItems.Integration;

// Shared by action pickers and trading. Full-ID grouping is safe only without
// instance data; keeping separate rows cannot repair native definition-ID removal.
internal sealed class PlainItemValidator
{
    private readonly FieldInfo properties;
    public PlainItemValidator()
    {
        properties = typeof(Item).GetField("properties", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(Item).FullName, "properties");
        if (properties.FieldType != typeof(Dictionary<Type, SerializedItemProperty>))
            throw new NotSupportedException("The game's instance-property representation has changed.");
    }
    public void Validate(Item item)
    {
        var instanceProperties = (Dictionary<Type, SerializedItemProperty>?)properties.GetValue(item);
        if (item.InventoryCount != 0 || item.InventorySize != 0 || item.IsBag || item.Definition.hasDurability ||
            (instanceProperties != null && instanceProperties.Count != 0))
            throw new NotSupportedException("Item '" + item.id + "' has instance data; preserving the original inventory.");
    }
}
