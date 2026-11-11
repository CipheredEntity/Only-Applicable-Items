using System;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static void VirtualInventoryViewUsesPolicySelectability()
    {
        var zombie = new ZombieWgoData { Armor = EquipmentItem("Armor") };
        var owner = new ZombieEquipmentInventoryWidgetData(zombie);
        var native = owner.ArmorPredicate;
        var policy = CreateZombieEquipmentContract().GetPolicy(owner.ReplaceCallback, native)!;
        var item = EquipmentItem("Armor");
        var snapshot = PickerSnapshot<Item>.Build(new[] { CollarSource(new object(), item) }, policy);
        var original = new InventoryWidgetDataBase
        {
            CustomItemsAvailableCondition = native,
            OnItemCellOver = _ => { },
            OnItemCellOut = _ => { },
            ExtraRedTooltipLocIdProvider = _ => "native-tooltip"
        };
        var session = new ApplicableItemsSession(policy);
        var view = new ApplicableInventoryWidgetData(session, new Inventory(), snapshot, original);
        var available = view.CustomItemsAvailableCondition ?? throw new InvalidOperationException("Missing UI availability predicate.");
        Assert(native(item) && ReferenceEquals(available, policy.CanSelect) &&
            !available(item), "The real virtual-view constructor must pass the full policy to the native gray-out/input mechanism.");
        Assert(ReferenceEquals(view.OnItemCellOver, original.OnItemCellOver) && ReferenceEquals(view.OnItemCellOut, original.OnItemCellOut) &&
            ReferenceEquals(view.ExtraRedTooltipLocIdProvider, original.ExtraRedTooltipLocIdProvider), "Retain original hover and tooltip callbacks.");
        zombie.Armor = null;
        Assert(available(item), "The UI must observe live equipped-slot changes through the same predicate.");
        owner.EquipmentRule = _ => false;
        Assert(!available(item), "Native rejection must still disable the UI.");
        view.OnItemCellPress!(new UIItemCell { DisplayingItem = item });
        Assert(ReferenceEquals(session.Selected, item), "Keep click dispatch routed through the session's selection handler.");
    }
}
