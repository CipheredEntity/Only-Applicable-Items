using System;
using System.Collections.Generic;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static readonly string[] EquipmentTitles = { "Armor", "Weapons", "Tools" };

    private static Func<Item, bool> EquipmentPredicate(ZombieEquipmentInventoryWidgetData owner, string title)
        => title switch
        {
            "Armor" => owner.ArmorPredicate,
            "Weapons" => owner.WeaponPredicate,
            "Tools" => owner.ToolPredicate,
            _ => throw new ArgumentException(title)
        };

    private static Item EquipmentItem(string title, string id = "gear")
    {
        var item = new Item(id, title == "Armor" ? ItemType.BodyArmor : ItemType.None);
        item.Definition.isTool = title == "Tools";
        item.Definition.isWeapon = title == "Weapons";
        return item;
    }

    private static void EquipmentRecognizesBothActionsAndAllThreeTitles()
    {
        var contract = CreateZombieEquipmentContract();
        Assert(contract.SupportsEquipment, "The validated insertion/replacement contract must be available.");
        var owner = new ZombieEquipmentInventoryWidgetData(new ZombieWgoData());
        foreach (var title in EquipmentTitles)
        {
            foreach (var callback in new[] { owner.InsertCallback, owner.ReplaceCallback })
            {
                var predicate = EquipmentPredicate(owner, title);
                var policy = contract.GetPolicy(callback, predicate);
                Assert(policy != null && policy.Title == title && policy.IsEnabled && policy.CanSelect(EquipmentItem(title)),
                    "All three titles must allow native-valid equipment for empty slots in both action contexts.");
                Assert(!policy!.IncludeInDisplay(null!) && policy.IncludeInDisplay(EquipmentItem(title)),
                    "Visibility must handle empty slots and include the native category.");
                var depleted = EquipmentItem(title);
                depleted.Count = 0;
                Assert(!policy.IncludeInDisplay(depleted), "Depleted sources stay hidden rather than forcing fallback.");
            }
        }
    }

    private static void EquipmentRequiresExactSameOwnerDelegates()
    {
        var contract = CreateZombieEquipmentContract();
        var zombie = new ZombieWgoData();
        var owner = new ZombieEquipmentInventoryWidgetData(zombie);
        var other = new ZombieEquipmentInventoryWidgetData(zombie);
        foreach (var title in EquipmentTitles)
        {
            var predicate = EquipmentPredicate(owner, title);
            foreach (var callback in new[] { owner.InsertCallback, owner.ReplaceCallback })
            {
                Assert(contract.GetPolicy(callback, EquipmentPredicate(other, title)) == null,
                    "Different equipment owners must not match even when they reference the same zombie.");
                Assert(contract.GetPolicy(callback + owner.CollarCallback, predicate) == null &&
                    contract.GetPolicy(callback, predicate + owner.EquipmentPredicate) == null,
                    "Multicast contexts must retain vanilla.");
                Assert(contract.GetPolicy(cell => callback(cell), predicate) == null &&
                    contract.GetPolicy(callback, item => predicate(item)) == null &&
                    contract.GetPolicy(callback, owner.EquipmentPredicate) == null,
                    "Unknown wrappers and unregistered predicates must retain vanilla.");
                Assert(contract.GetPolicy(callback, zombie.UpgradePredicate) == null && contract.GetPolicy(callback, null) == null,
                    "Equipment callbacks must not match collar or missing predicates.");
            }
            Assert(contract.GetPolicy(owner.CollarCallback, predicate) == null && contract.GetPolicy(null, predicate) == null,
                "Collar and missing callbacks must not match equipment predicates.");
        }
    }

    private static void EquipmentPreservesNativeCategoryExclusions()
    {
        var contract = CreateZombieEquipmentContract();
        var owner = new ZombieEquipmentInventoryWidgetData(new ZombieWgoData());
        var weapons = contract.GetPolicy(owner.InsertCallback, owner.WeaponPredicate)!;
        var tools = contract.GetPolicy(owner.InsertCallback, owner.ToolPredicate)!;
        var armor = contract.GetPolicy(owner.InsertCallback, owner.ArmorPredicate)!;
        var sword = EquipmentItem("Weapons");
        sword.Definition.type = ItemType.Sword;
        var dual = EquipmentItem("Weapons");
        dual.Definition.isTool = true;
        Assert(!weapons.IncludeInDisplay(sword) && !weapons.CanSelect(sword), "Preserve the native sword exclusion.");
        Assert(!weapons.IncludeInDisplay(dual) && tools.IncludeInDisplay(dual) && tools.CanSelect(dual),
            "An item marked as a tool belongs in Tools even when it is also marked as a weapon.");
        var nonArmor = EquipmentItem("Weapons");
        Assert(!armor.IncludeInDisplay(nonArmor) && !armor.CanSelect(nonArmor), "Armor membership requires BodyArmor.");
        sword.Definition.isTool = true;
        Assert(tools.IncludeInDisplay(sword), "Do not invent a sword exclusion in Tools that its native filter lacks.");
    }

    private static void EquipmentAggregatesSingletonsWithoutChangingSources()
    {
        var owner = new ZombieEquipmentInventoryWidgetData(new ZombieWgoData());
        foreach (var title in EquipmentTitles)
        {
            var policy = CreateZombieEquipmentContract().GetPolicy(owner.InsertCallback, EquipmentPredicate(owner, title))!;
            var a = EquipmentItem(title, "gear:1");
            var b = EquipmentItem(title, "gear:1");
            var variant = EquipmentItem(title, "gear:2");
            var irrelevant = new Item("other", ItemType.Collar, 99) { HasInstanceData = true };
            var bag = new object();
            var snapshot = PickerSnapshot<Item>.Build(new[]
            {
                CollarSource(new object(), a), CollarSource(bag, b), CollarSource(new object(), a),
                CollarSource(new object(), variant), CollarSource(new object(), irrelevant)
            }, policy);
            Assert(snapshot.Groups.Count == 2 && snapshot.Groups[0].Total == 2 && snapshot.Groups[1].Total == 1,
                "Aggregate separate singleton pieces above normal stack limits while preserving full-ID variants and deduplicating aliases.");
            Assert(ReferenceEquals(snapshot.Groups[0].Sources[1].Container, bag) && a.Count == 1 && b.Count == 1 && variant.Count == 1,
                "Display construction must preserve ownership and every real source count.");
        }
    }

    private static void EquipmentRejectsUnexpectedStacksAndInstanceData()
    {
        foreach (var title in EquipmentTitles)
        {
            foreach (bool allowed in new[] { false, true })
            {
                var owner = new ZombieEquipmentInventoryWidgetData(new ZombieWgoData()) { EquipmentRule = _ => allowed };
                var policy = CreateZombieEquipmentContract().GetPolicy(owner.ReplaceCallback, EquipmentPredicate(owner, title))!;
                foreach (var mutate in new Action<Item>[]
                {
                    item => item.Count = 2,
                    item => item.Definition.stackCount = 2,
                    item => item.Definition.stackCount = 0,
                    item => item.HasInstanceData = true,
                    item => item.Definition.hasDurability = true
                })
                {
                    var plain = EquipmentItem(title);
                    var unusual = EquipmentItem(title);
                    mutate(unusual);
                    int originalCount = unusual.Count;
                    bool rejected = false;
                    try
                    {
                        PickerSnapshot<Item>.Build(new[] { CollarSource(new object(), plain), CollarSource(new object(), unusual) }, policy);
                    }
                    catch (NotSupportedException) { rejected = true; }
                    Assert(rejected && plain.Count == 1 && unusual.Count == originalCount,
                        "Any included anomalous stack or instance must trigger fallback before aggregation, even if disabled.");
                }
            }
        }
    }

    private static void EquipmentAvailabilityChangesRefreshAndRejectStaleSelections()
    {
        foreach (var title in EquipmentTitles)
        {
            bool allowed = true;
            var owner = new ZombieEquipmentInventoryWidgetData(new ZombieWgoData()) { EquipmentRule = _ => allowed };
            var policy = CreateZombieEquipmentContract().GetPolicy(owner.InsertCallback, EquipmentPredicate(owner, title))!;
            var item = EquipmentItem(title);
            var sources = new[] { CollarSource(new object(), item) };
            var initial = PickerSnapshot<Item>.Build(sources, policy);
            allowed = false;
            var blocked = PickerSnapshot<Item>.Build(sources, policy);
            Assert(!initial.SameAs(blocked) && blocked.Groups.Count == 1 && !blocked.TryResolveSelectable(item.id, out _),
                "Native rejection keeps a matching item visible but disabled and rejects a stale click.");
            allowed = true;
            var restored = PickerSnapshot<Item>.Build(sources, policy);
            Assert(!blocked.SameAs(restored) && restored.TryResolveSelectable(item.id, out _) && item.Count == 1,
                "Native availability changes refresh without stock mutation.");
        }
    }

    private static void EquipmentResolvesRealSingletonsForNativeCallbacks()
    {
        foreach (var title in EquipmentTitles)
        {
            foreach (bool replace in new[] { false, true })
            {
                var owner = new ZombieEquipmentInventoryWidgetData(new ZombieWgoData());
                var callback = replace ? owner.ReplaceCallback : owner.InsertCallback;
                var policy = CreateZombieEquipmentContract().GetPolicy(callback, EquipmentPredicate(owner, title))!;
                var a = EquipmentItem(title);
                var b = EquipmentItem(title);
                var container = new object();
                var initial = PickerSnapshot<Item>.Build(new[] { CollarSource(container, a), CollarSource(container, b) }, policy);
                Assert(initial.Groups[0].Total == 2 && initial.TryResolveSelectable(a.id, out var first) && ReferenceEquals(first, a),
                    "The initial selection must resolve to a real single-item source, not the displayed total.");
                a.Count = 0;
                var fresh = PickerSnapshot<Item>.Build(new[] { CollarSource(container, a), CollarSource(container, b) }, policy);
                Assert(fresh.TryResolveSelectable(b.id, out var selected) && ReferenceEquals(selected, b) && selected!.Count == 1,
                    "Fresh resolution must find a surviving singleton after depletion.");
                callback(new UIItemCell { DisplayingItem = selected });
                Assert(ReferenceEquals(owner.Selected, b) && owner.SelectedAction == (replace ? "Replace" : "Insert") && b.Count == 1,
                    "Pass the unchanged real item/count into the correct original callback; leave transfers to the game.");
            }
        }
    }

    private static void AllEquipmentCategoriesShareTheExistingToggle()
    {
        bool enabled = true;
        var contract = CreateZombieEquipmentContract(() => enabled);
        var zombie = new ZombieWgoData();
        var owner = new ZombieEquipmentInventoryWidgetData(zombie);
        var policies = new List<PickerPolicy<Item>> { contract.GetPolicy(owner.CollarCallback, zombie.UpgradePredicate)! };
        foreach (var title in EquipmentTitles)
        {
            policies.Add(contract.GetPolicy(owner.InsertCallback, EquipmentPredicate(owner, title))!);
            policies.Add(contract.GetPolicy(owner.ReplaceCallback, EquipmentPredicate(owner, title))!);
        }
        foreach (bool value in new[] { false, true })
        {
            enabled = value;
            foreach (var policy in policies)
                Assert(policy.IsEnabled == value, "Collars and all six insert/replace contexts must read the same live setting.");
        }
    }
}
