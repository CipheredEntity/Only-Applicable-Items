using System;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

// Collar upgrades consume one unit; other equipment callbacks use the real source
// count, so only plain single-item equipment is safe to aggregate by definition ID.
internal sealed class ZombieEquipmentPickerContract
{
    private readonly MethodInfo selected;
    private readonly MethodInfo eligible;
    private readonly MethodInfo? insertSelected;
    private readonly MethodInfo? replaceSelected;
    private readonly MethodInfo? armorEligible;
    private readonly MethodInfo? weaponEligible;
    private readonly MethodInfo? toolEligible;
    private readonly bool equippedItemsAvailable;
    private readonly Func<bool> enabled;
    private readonly Action<Item> validateItem;
    public bool SupportsEquipment => insertSelected != null && replaceSelected != null &&
        armorEligible != null && weaponEligible != null && toolEligible != null && equippedItemsAvailable;

    public ZombieEquipmentPickerContract(Func<bool> enabled, Action<Item> validateItem)
    {
        this.enabled = enabled;
        this.validateItem = validateItem;
        selected = RequireMethod(typeof(ZombieEquipmentInventoryWidgetData), "UpgradeCollar", typeof(void), typeof(UIItemCell));
        eligible = RequireMethod(typeof(ZombieWgoData), "CanUpgradeCollarTo", typeof(bool), typeof(Item));
        RequireMethod(typeof(ZombieEquipmentInventoryWidgetData), "get_ZombieWgoData", typeof(ZombieWgoData));
        // A changed insertion/replacement contract must not disable working Collars.
        insertSelected = FindMethod(typeof(ZombieEquipmentInventoryWidgetData), "InsertItem", typeof(void), typeof(UIItemCell));
        replaceSelected = FindMethod(typeof(ZombieEquipmentInventoryWidgetData), "ReplaceItem", typeof(void), typeof(UIItemCell));
        armorEligible = FindMethod(typeof(ZombieEquipmentInventoryWidgetData), "IsArmor", typeof(bool), typeof(Item));
        weaponEligible = FindMethod(typeof(ZombieEquipmentInventoryWidgetData), "IsWeapon", typeof(bool), typeof(Item));
        toolEligible = FindMethod(typeof(ZombieEquipmentInventoryWidgetData), "IsTool", typeof(bool), typeof(Item));
        equippedItemsAvailable = FindMethod(typeof(ZombieWgoData), "get_Armor", typeof(Item)) != null &&
            FindMethod(typeof(ZombieWgoData), "get_Weapon", typeof(Item)) != null &&
            FindMethod(typeof(ZombieWgoData), "get_Hand", typeof(Item)) != null;
    }

    public PickerPolicy<Item>? GetPolicy(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
    {
        if (!(callback?.Target is ZombieEquipmentInventoryWidgetData owner) ||
            predicate == null || callback.GetInvocationList().Length != 1 || predicate.GetInvocationList().Length != 1)
            return null;

        // Collar delegates belong to different objects. Keep the existing linked-zombie
        // check and plain-item validator, without applying the new single-item rule.
        if (callback.Method == selected && predicate.Method == eligible &&
            predicate.Target is ZombieWgoData zombie && ReferenceEquals(owner.ZombieWgoData, zombie))
            return new PickerPolicy<Item>(PickerId.ZombieCollars, enabled,
                item => item != null && !item.IsEmpty && item.Definition.type == ItemType.Collar,
                predicate, validateItem);

        if (!SupportsEquipment || !ReferenceEquals(callback.Target, predicate.Target) ||
            (callback.Method != insertSelected && callback.Method != replaceSelected))
            return null;

        PickerId id;
        Func<Item, bool> category;
        Func<Item?> equipped;
        if (predicate.Method == armorEligible)
        {
            id = PickerId.ZombieArmor;
            category = item => item.Definition.type == ItemType.BodyArmor;
            equipped = () => owner.ZombieWgoData?.Armor;
        }
        else if (predicate.Method == weaponEligible)
        {
            id = PickerId.ZombieWeapons;
            category = item => item.Definition.isWeapon && !item.Definition.isTool && item.Definition.type != ItemType.Sword;
            equipped = () => owner.ZombieWgoData?.Weapon;
        }
        else if (predicate.Method == toolEligible)
        {
            id = PickerId.ZombieTools;
            category = item => item.Definition.isTool;
            equipped = () => owner.ZombieWgoData?.Hand;
        }
        else
        {
            return null;
        }

        // Keep identical full IDs visible, but prevent a redundant replacement.
        // Read the current slot each time so refreshes and click-time validation agree.
        bool CanSelect(Item item)
        {
            if (item == null || item.IsEmpty || !predicate(item))
                return false;
            var current = equipped();
            return current == null || current.IsEmpty || !string.Equals(item.id, current.id, StringComparison.Ordinal);
        }
        return new PickerPolicy<Item>(id, enabled,
            item => item != null && !item.IsEmpty && category(item), CanSelect, ValidateEquipment);
    }

    private void ValidateEquipment(Item item)
    {
        validateItem(item);
        if (item.Definition.stackCount != 1 || item.Count != 1)
            throw new NotSupportedException("Equipment '" + item.id + "' is not a single-item source; preserving the original picker.");
    }

    private static MethodInfo RequireMethod(Type type, string name, Type result, params Type[] arguments)
    {
        return FindMethod(type, name, result, arguments) ?? throw new MissingMethodException(type.FullName, name);
    }

    private static MethodInfo? FindMethod(Type type, string name, Type result, params Type[] arguments)
    {
        var method = type.GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
        return method != null && method.ReturnType == result && !method.IsStatic ? method : null;
    }
}
