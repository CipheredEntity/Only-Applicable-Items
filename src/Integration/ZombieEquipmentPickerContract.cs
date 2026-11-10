using System;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

// Only collar upgrades are registered. Other equipment operations have different
// transfer/instance contracts and must remain unchanged until reviewed separately.
internal sealed class ZombieEquipmentPickerContract
{
    private readonly MethodInfo selected;
    private readonly MethodInfo eligible;
    private readonly Func<bool> enabled;
    private readonly Action<Item> validateItem;

    public ZombieEquipmentPickerContract(Func<bool> enabled, Action<Item> validateItem)
    {
        this.enabled = enabled;
        this.validateItem = validateItem;
        selected = RequireMethod(typeof(ZombieEquipmentInventoryWidgetData), "UpgradeCollar", typeof(void), typeof(UIItemCell));
        eligible = RequireMethod(typeof(ZombieWgoData), "CanUpgradeCollarTo", typeof(bool), typeof(Item));
        RequireMethod(typeof(ZombieEquipmentInventoryWidgetData), "get_ZombieWgoData", typeof(ZombieWgoData));
    }

    public PickerPolicy<Item>? GetPolicy(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
    {
        // Unlike the other direct action pairs, these delegates belong to different
        // objects. Verify the equipment owner references the predicate's exact zombie.
        if (!(callback?.Target is ZombieEquipmentInventoryWidgetData owner) ||
            !(predicate?.Target is ZombieWgoData zombie) ||
            callback.GetInvocationList().Length != 1 || predicate.GetInvocationList().Length != 1 ||
            callback.Method != selected || predicate.Method != eligible ||
            !ReferenceEquals(owner.ZombieWgoData, zombie))
            return null;

        return new PickerPolicy<Item>("Collars", enabled,
            item => item != null && !item.IsEmpty && item.Definition.type == ItemType.Collar,
            predicate, validateItem);
    }

    private static MethodInfo RequireMethod(Type type, string name, Type result, params Type[] arguments)
    {
        var method = type.GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
        if (method == null || method.ReturnType != result || method.IsStatic)
            throw new MissingMethodException(type.FullName, name);
        return method;
    }
}
