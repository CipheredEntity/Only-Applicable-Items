using System;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal sealed class GraveDecorationPickerContract
{
    private readonly MethodInfo selected;
    private readonly MethodInfo eligible;
    private readonly Func<bool> enabled;
    private readonly Action<Item> validateItem;

    public GraveDecorationPickerContract(Func<bool> enabled, Action<Item> validateItem)
    {
        this.enabled = enabled;
        this.validateItem = validateItem;
        selected = RequireMethod("OnGraveItemSelected", typeof(void), typeof(UIItemCell));
        eligible = RequireMethod("IsGraveItem", typeof(bool), typeof(Item));
        RequireMethod("get_GraveElementType", typeof(GraveElementType));
    }

    public PickerPolicy<Item>? GetPolicy(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
    {
        if (!(predicate?.Target is UIGraveElementWidgetData owner) ||
            !DelegateContext.Matches(callback, predicate, selected, eligible))
            return null;

        string title;
        string group;
        switch (owner.GraveElementType)
        {
            case GraveElementType.Top:
                title = "Tombstones";
                group = GameConsts.Groups.TOMBSTONE_ITEM_GROUP;
                break;
            case GraveElementType.Bot:
                title = "Fences";
                group = GameConsts.Groups.FENCE_ITEM_GROUP;
                break;
            default:
                return null;
        }

        // Visibility follows the category; native selectability stays separate.
        // Do not add recipe/unlock/quality checks that the original picker lacks.
        return new PickerPolicy<Item>(title, enabled,
            item => item != null && !item.IsEmpty && item.Definition.itemGroupIds.Contains(group),
            predicate, validateItem);
    }

    private static MethodInfo RequireMethod(string name, Type result, params Type[] arguments)
    {
        var method = typeof(UIGraveElementWidgetData).GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
        if (method == null || method.ReturnType != result || method.IsStatic)
            throw new MissingMethodException(typeof(UIGraveElementWidgetData).FullName, name);
        return method;
    }
}
