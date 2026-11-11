using System;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static void SetEquippedItem(ZombieWgoData zombie, string title, Item? item)
    {
        switch (title)
        {
            case "Armor": zombie.Armor = item; break;
            case "Weapons": zombie.Weapon = item; break;
            case "Tools": zombie.Hand = item; break;
            default: throw new ArgumentException(title);
        }
    }

    private static void EquipmentMatchingEquippedIdsStayVisibleAndDisabled()
    {
        foreach (var title in EquipmentTitles)
        {
            foreach (bool replace in new[] { false, true })
            {
                var zombie = new ZombieWgoData();
                var owner = new ZombieEquipmentInventoryWidgetData(zombie);
                SetEquippedItem(zombie, title, EquipmentItem(title, "gear:1"));
                var predicate = EquipmentPredicate(owner, title);
                var policy = CreateZombieEquipmentContract().GetPolicy(replace ? owner.ReplaceCallback : owner.InsertCallback, predicate)!;
                var a = EquipmentItem(title, "gear:1");
                var b = EquipmentItem(title, "gear:1");
                var different = EquipmentItem(title, "gear:2");
                var snapshot = PickerSnapshot<Item>.Build(new[]
                {
                    CollarSource(new object(), a), CollarSource(new object(), b), CollarSource(new object(), different)
                }, policy);
                Assert(predicate(a) && policy.IncludeInDisplay(a) && !policy.CanSelect(a),
                    "Native-valid same-ID equipment must stay visible but become unselectable.");
                Assert(snapshot.Groups.Count == 2 && snapshot.Groups[0].Total == 2 && !snapshot.TryResolveSelectable(a.id, out _),
                    "The matching full-ID group keeps all owned quantities while blocking resolution.");
                Assert(snapshot.TryResolveSelectable(different.id, out var selected) && ReferenceEquals(selected, different) &&
                    a.Count == 1 && b.Count == 1 && different.Count == 1,
                    "Other full definitions retain native selection and sources stay unchanged.");
            }
        }
    }

    private static void EquipmentEquippedComparisonUsesFullIdAndCorrectSlot()
    {
        foreach (var title in EquipmentTitles)
        {
            var zombie = new ZombieWgoData
            {
                Armor = EquipmentItem("Armor", "gear:1"),
                Weapon = EquipmentItem("Weapons", "gear:1"),
                Hand = EquipmentItem("Tools", "gear:1")
            };
            var owner = new ZombieEquipmentInventoryWidgetData(zombie);
            var policy = CreateZombieEquipmentContract().GetPolicy(owner.ReplaceCallback, EquipmentPredicate(owner, title))!;
            var candidate = EquipmentItem(title, "gear:1");
            foreach (var otherId in new[] { "gear", "gear:10", "GEAR:1" })
            {
                SetEquippedItem(zombie, title, EquipmentItem(title, otherId));
                Assert(policy.CanSelect(candidate), "Compare complete, case-sensitive IDs only against the selected slot.");
            }
            SetEquippedItem(zombie, title, EquipmentItem(title, candidate.id));
            Assert(!policy.CanSelect(candidate), "Different instances with the same full ID must still be blocked.");
            var depleted = EquipmentItem(title, candidate.id);
            depleted.Count = 0;
            SetEquippedItem(zombie, title, depleted);
            Assert(policy.CanSelect(candidate), "An empty equipped item is not a same-ID restriction.");
            SetEquippedItem(zombie, title, null);
            Assert(policy.CanSelect(candidate), "An empty slot must allow native-valid insertion regardless of other equipped slots.");
        }
    }

    private static void EquipmentEquippedChangesRefreshAndRejectStaleSelections()
    {
        foreach (var title in EquipmentTitles)
        {
            var zombie = new ZombieWgoData();
            var owner = new ZombieEquipmentInventoryWidgetData(zombie);
            var policy = CreateZombieEquipmentContract().GetPolicy(owner.ReplaceCallback, EquipmentPredicate(owner, title))!;
            var candidate = EquipmentItem(title);
            var sources = new[] { CollarSource(new object(), candidate) };
            var allowed = PickerSnapshot<Item>.Build(sources, policy);
            Assert(allowed.TryResolveSelectable(candidate.id, out _), "An initially empty slot allows selection.");
            SetEquippedItem(zombie, title, EquipmentItem(title));
            var blocked = PickerSnapshot<Item>.Build(sources, policy);
            Assert(!allowed.SameAs(blocked) && blocked.Groups.Count == 1 && !blocked.TryResolveSelectable(candidate.id, out _),
                "An equipment-only state change must redraw disabled appearance and reject a stale click.");
            SetEquippedItem(zombie, title, EquipmentItem(title));
            Assert(blocked.SameAs(PickerSnapshot<Item>.Build(sources, policy)),
                "A new equipped instance with the same ID must not change availability or force another redraw.");
            SetEquippedItem(zombie, title, EquipmentItem(title, "other"));
            var restored = PickerSnapshot<Item>.Build(sources, policy);
            Assert(!blocked.SameAs(restored) && restored.TryResolveSelectable(candidate.id, out _) && candidate.Count == 1,
                "Changing to a different equipped definition must restore native selection without changing stock.");
        }
    }

    private static void EquipmentMatchingEquippedIdsPreserveSafetyAndNativeRules()
    {
        foreach (var title in EquipmentTitles)
        {
            var zombie = new ZombieWgoData();
            var owner = new ZombieEquipmentInventoryWidgetData(zombie);
            SetEquippedItem(zombie, title, EquipmentItem(title));
            var policy = CreateZombieEquipmentContract().GetPolicy(owner.ReplaceCallback, EquipmentPredicate(owner, title))!;
            var unusual = EquipmentItem(title);
            unusual.HasInstanceData = true;
            bool rejected = false;
            try { PickerSnapshot<Item>.Build(new[] { CollarSource(new object(), unusual) }, policy); }
            catch (NotSupportedException) { rejected = true; }
            Assert(rejected && unusual.Count == 1, "Same-ID disabled items must still receive instance-data validation.");
            owner.EquipmentRule = _ => false;
            Assert(!policy.CanSelect(EquipmentItem(title, "different")), "The new condition must never bypass native rejection.");
            var predicate = zombie.UpgradePredicate;
            var collars = CreateZombieEquipmentContract().GetPolicy(owner.CollarCallback, predicate)!;
            Assert(ReferenceEquals(collars.CanSelect, predicate) && collars.CanSelect(new Item("gear", ItemType.Collar)),
                "Collars must retain the exact original upgrade predicate, without the equipment same-ID rule.");
        }
    }
}
