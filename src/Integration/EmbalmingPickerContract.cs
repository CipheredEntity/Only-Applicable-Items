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
    private readonly FieldInfo? bodyItem;
    private readonly FieldInfo? zombieWgoData;
    private readonly FieldInfo? isZombie;

    public EmbalmingPickerContract(Func<bool> enabled, Action<Item> validateItem)
    {
        this.enabled = enabled;
        this.validateItem = validateItem;
        selected = RequireMethod("TryEmbalm", typeof(void), typeof(UIItemCell));
        eligible = RequireMethod("CanEmbalm", typeof(bool), typeof(Item));
        // Optional presentation context: a changed field layout suppresses the warning,
        // without taking away the existing native eligibility/consolidation support.
        bodyItem = FindField("bodyItem", typeof(Item));
        zombieWgoData = FindField("zombieWgoData", typeof(ZombieWgoData));
        isZombie = FindField("isZombie", typeof(bool));
    }

    public PickerPolicy<Item>? GetPolicy(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
    {
        if (!(predicate?.Target is UIEmbalmWindowData owner) ||
            !DelegateContext.Matches(callback, predicate, selected, eligible))
            return null;

        // All fluids are visible. Keep the native predicate for body capacity,
        // collar limits, and the different rules for consumed zombie-effect fluids.
        return new PickerPolicy<Item>(PickerId.EmbalmingFluids, enabled,
            item => item != null && !item.IsEmpty && item.Definition.type == ItemType.Embalm,
            predicate, validateItem, item => GetCollarWarning(owner, item, predicate));
    }

    private string? GetCollarWarning(UIEmbalmWindowData owner, Item item, Func<Item, bool> canSelect)
    {
        try
        {
            if (!enabled() || item == null || item.IsEmpty || item.Definition.type != ItemType.Embalm ||
                !(isZombie?.GetValue(owner) is bool zombie) || !zombie ||
                !(bodyItem?.GetValue(owner) is Item body) || body.IsEmpty ||
                !(zombieWgoData?.GetValue(owner) is ZombieWgoData data))
                return null;

            var craft = GameBalance.GetAutopsyCraftDef(AutopsyTypeCraft.Embalm, item.id);
            // Special fluids bypass capacity/collar rules. Unknown crafts are ambiguous.
            if (craft == null || craft.ConsumesEmbalmItem ||
                BodyPocketInventoryWidget.GetOccupiedSlotCount(body, data) >= 6)
                return null;

            var collar = data.Collar;
            if (collar == null || collar.IsEmpty || collar.Definition.type != ItemType.Collar || canSelect(item))
                return null;

            // Reuse native skull arithmetic and bounds; never infer a collar failure
            // from CanEmbalm returning false alone, or change the selection predicate.
            return data.CanAddItemToBody(item) ? null : "ui_operation_failed_desc";
        }
        catch (Exception)
        {
            // Incomplete/stale context must not produce a misleading warning or break the picker.
            return null;
        }
    }

    private static FieldInfo? FindField(string name, Type type)
    {
        var field = typeof(UIEmbalmWindowData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.FieldType == type ? field : null;
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
