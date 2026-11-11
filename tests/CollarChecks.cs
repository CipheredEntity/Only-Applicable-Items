using System;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static ZombieEquipmentPickerContract CreateZombieEquipmentContract(Func<bool>? enabled = null)
        => new ZombieEquipmentPickerContract(enabled ?? (() => true), item =>
        {
            if (item.HasInstanceData || item.Definition.hasDurability)
                throw new NotSupportedException("Unsupported instance properties");
        });

    private static StackSource<Item> CollarSource(object container, Item item)
        => new StackSource<Item>(container, item, item.id, item.Count, 0);

    private static void CollarsRequireTheExactUpgradePairAndLinkedZombie()
    {
        var contract = CreateZombieEquipmentContract();
        var zombie = new ZombieWgoData();
        var other = new ZombieWgoData();
        var owner = new ZombieEquipmentInventoryWidgetData(zombie);
        var callback = owner.CollarCallback;
        var predicate = zombie.UpgradePredicate;
        var policy = contract.GetPolicy(callback, predicate);
        Assert(!ReferenceEquals(callback.Target, predicate.Target) && policy != null && policy.Title == "Collars" &&
            ReferenceEquals(policy.CanSelect, predicate), "Accept the exact two-owner native action and keep its live predicate.");
        Assert(contract.GetPolicy(callback, other.UpgradePredicate) == null &&
            contract.GetPolicy(new ZombieEquipmentInventoryWidgetData(null).CollarCallback, predicate) == null,
            "Reject callbacks whose owner has no zombie or references a different zombie.");
        Assert(contract.GetPolicy(callback, zombie.OtherPredicate) == null, "Reject other predicates on the same zombie.");
        Assert(contract.GetPolicy(callback + owner.InsertCallback, predicate) == null &&
            contract.GetPolicy(callback, predicate + zombie.OtherPredicate) == null, "Reject multicast contexts.");
        Assert(contract.GetPolicy(cell => callback(cell), predicate) == null &&
            contract.GetPolicy(callback, item => predicate(item)) == null, "Unknown delegate wrappers retain vanilla.");
        Assert(contract.GetPolicy(null, predicate) == null && contract.GetPolicy(callback, null) == null,
            "Incomplete contexts retain vanilla.");
    }

    private static void CollarsRejectUnregisteredEquipmentContextsAndOtherActions()
    {
        var contract = CreateZombieEquipmentContract();
        var zombie = new ZombieWgoData();
        var owner = new ZombieEquipmentInventoryWidgetData(zombie);
        Assert(contract.GetPolicy(owner.InsertCallback, owner.EquipmentPredicate) == null &&
            contract.GetPolicy(owner.ReplaceCallback, owner.EquipmentPredicate) == null &&
            contract.GetPolicy(owner.InsertCallback, zombie.UpgradePredicate) == null &&
            contract.GetPolicy(owner.ReplaceCallback, zombie.UpgradePredicate) == null,
            "Unregistered equipment predicates and crossed collar/equipment contexts must retain vanilla.");
        var embalming = new UIEmbalmWindowData();
        var grave = new UIGraveElementWidgetData(GraveElementType.Top);
        var autopsy = new UIAutopsyWindowData(ItemType.Skull);
        Assert(contract.GetPolicy(embalming.Callback, embalming.Predicate) == null &&
            contract.GetPolicy(grave.Callback, grave.Predicate) == null &&
            contract.GetPolicy(autopsy.InsertSelected, autopsy.InsertEligible) == null,
            "Existing actions must not activate the equipment contract.");
        Assert(CreateEmbalmingContract().GetPolicy(owner.CollarCallback, zombie.UpgradePredicate) == null &&
            CreateGraveDecorationContract().GetPolicy(owner.CollarCallback, zombie.UpgradePredicate) == null &&
            CreateBodyPartContract().GetPolicy(owner.CollarCallback, zombie.UpgradePredicate) == null,
            "Collar upgrades must not activate existing contracts.");
    }

    private static void CollarsConsolidateAllOwnedVariantsWithoutMutatingSources()
    {
        var zombie = new ZombieWgoData { UpgradeRule = item => item.id == "collar:2" };
        var owner = new ZombieEquipmentInventoryWidgetData(zombie);
        var policy = CreateZombieEquipmentContract().GetPolicy(owner.CollarCallback, zombie.UpgradePredicate)!;
        var a = new Item("collar:1", ItemType.Collar, 20);
        var b = new Item("collar:1", ItemType.Collar, 35);
        var upgrade = new Item("collar:2", ItemType.Collar, 50);
        var irrelevant = new Item("other_equipment", ItemType.None) { HasInstanceData = true };
        var bag = new object();
        var snapshot = PickerSnapshot<Item>.Build(new[]
        {
            CollarSource(new object(), a), CollarSource(bag, b), CollarSource(new object(), upgrade),
            CollarSource(new object(), a), CollarSource(new object(), irrelevant),
            CollarSource(bag, new Item("depleted", ItemType.Collar, 0))
        }, policy);
        Assert(snapshot.Groups.Count == 2 && snapshot.Groups[0].Total == 55 && snapshot.Groups[1].Total == 50,
            "Combine owned equivalents above stack limits, preserve full-ID variants, and deduplicate physical stacks.");
        Assert(!snapshot.TryResolveSelectable(a.id, out _) && snapshot.TryResolveSelectable(upgrade.id, out _),
            "Rejected collar variants stay visible while only native upgrades are selectable.");
        Assert(!policy.IncludeInDisplay(null!) && !policy.IncludeInDisplay(irrelevant) &&
            ReferenceEquals(snapshot.Groups[0].Sources[1].Container, bag), "Only collars appear, with real source ownership retained.");
        Assert(a.Count == 20 && b.Count == 35 && upgrade.Count == 50 && irrelevant.Count == 1,
            "Projection must not mutate stored quantities or validate unrelated instance-bearing equipment.");
    }

    private static void CollarsKeepRejectedUpgradesVisibleAndRevalidateAvailability()
    {
        bool allowed = false;
        var zombie = new ZombieWgoData { UpgradeRule = _ => allowed };
        var owner = new ZombieEquipmentInventoryWidgetData(zombie);
        var policy = CreateZombieEquipmentContract().GetPolicy(owner.CollarCallback, zombie.UpgradePredicate)!;
        var collar = new Item("collar", ItemType.Collar, 8);
        var sources = new[] { CollarSource(new object(), collar) };
        var disabled = PickerSnapshot<Item>.Build(sources, policy);
        Assert(disabled.Groups.Count == 1 && !disabled.TryResolveSelectable(collar.id, out _),
            "A collar blocked by native body/current-collar rules stays visible but disabled.");
        allowed = true;
        var enabled = PickerSnapshot<Item>.Build(sources, policy);
        Assert(!disabled.SameAs(enabled) && enabled.TryResolveSelectable(collar.id, out _),
            "Native availability changes must redraw without stock changes.");
        allowed = false;
        var fresh = PickerSnapshot<Item>.Build(sources, policy);
        Assert(!fresh.TryResolveSelectable(collar.id, out _) && collar.Count == 8,
            "Fresh click-time resolution must reject a stale upgrade without consuming anything.");
    }

    private static void CollarsRejectInstanceDataEvenForBlockedUpgrades()
    {
        foreach (bool allowed in new[] { false, true })
        {
            var zombie = new ZombieWgoData { UpgradeRule = _ => allowed };
            var owner = new ZombieEquipmentInventoryWidgetData(zombie);
            var policy = CreateZombieEquipmentContract().GetPolicy(owner.CollarCallback, zombie.UpgradePredicate)!;
            var plain = new Item("collar", ItemType.Collar, 5);
            var unusual = new Item("collar", ItemType.Collar) { HasInstanceData = true };
            bool rejected = false;
            try
            {
                PickerSnapshot<Item>.Build(new[] { CollarSource(new object(), plain), CollarSource(new object(), unusual) }, policy);
            }
            catch (NotSupportedException) { rejected = true; }
            Assert(rejected && plain.Count == 5 && unusual.Count == 1,
                "Included instance data must trigger fallback before grouping, including disabled same-ID collars.");
        }
    }

    private static void CollarsResolveRealSourcesAndReadTheZombieEquipmentToggle()
    {
        bool enabled = true;
        var zombie = new ZombieWgoData();
        var owner = new ZombieEquipmentInventoryWidgetData(zombie);
        var policy = CreateZombieEquipmentContract(() => enabled).GetPolicy(owner.CollarCallback, zombie.UpgradePredicate)!;
        var a = new Item("collar", ItemType.Collar, 2);
        var b = new Item("collar", ItemType.Collar, 8);
        var container = new object();
        var initial = PickerSnapshot<Item>.Build(new[] { CollarSource(container, a), CollarSource(container, b) }, policy);
        Assert(initial.Groups[0].Total == 10 && initial.TryResolveSelectable(a.id, out var first) && ReferenceEquals(first, a),
            "Resolve a real item rather than a synthetic aggregate passed to UID consumption.");
        a.Count = 0;
        var fresh = PickerSnapshot<Item>.Build(new[] { CollarSource(container, a), CollarSource(container, b) }, policy);
        Assert(fresh.TryResolveSelectable(b.id, out var selected) && ReferenceEquals(selected, b),
            "Resolve a surviving accessible stack after representative depletion.");
        owner.CollarCallback(new UIItemCell { DisplayingItem = selected });
        Assert(ReferenceEquals(owner.Selected, b) && b.Count == 8,
            "Preserve the selected object's identity and actual count for the original upgrade callback.");
        enabled = false;
        Assert(!policy.IsEnabled, "An existing policy must read the live shared Zombie Equipment toggle.");
        enabled = true;
        Assert(policy.IsEnabled, "Re-enabling permits the next collar picker to consolidate.");
    }
}
