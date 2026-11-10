using System;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static readonly (ItemType Category, string Title)[] BodyPartTitles =
    {
        (ItemType.Brain, "Brains"), (ItemType.Heart, "Hearts"), (ItemType.Guts, "Guts"),
        (ItemType.Skull, "Skulls"), (ItemType.Bones, "Bones"), (ItemType.Skin, "Skins")
    };

    private static BodyPartPickerContract CreateBodyPartContract(Func<bool>? enabled = null)
        => new BodyPartPickerContract(enabled ?? (() => true), item =>
        {
            if (item.HasInstanceData)
                throw new NotSupportedException("Unsupported instance properties");
        });

    private static StackSource<Item> BodyPartSource(object container, Item item)
        => new StackSource<Item>(container, item, item.id, item.Count, 0);

    private static void BodyPartsRecognizeBothActionsAndAllSixTitles()
    {
        var contract = CreateBodyPartContract();
        foreach (var entry in BodyPartTitles)
        {
            var owner = new UIAutopsyWindowData(entry.Category);
            foreach (bool replacement in new[] { false, true })
            {
                var predicate = replacement ? owner.ChangeEligible : owner.InsertEligible;
                var policy = contract.GetPolicy(replacement ? owner.ChangeSelected : owner.InsertSelected, predicate);
                Assert(policy != null && policy.Title == entry.Title && policy.IsEnabled,
                    "Both insertion and replacement must map all six categories to the requested titles.");
                Assert(ReferenceEquals(policy!.CanSelect, predicate), "Keep the original live predicate delegate.");
                Assert(policy.IncludeInDisplay(new Item("part", entry.Category)), "Include the requested category.");
                Assert(!policy.IncludeInDisplay(null!) && !policy.IncludeInDisplay(new Item("empty", entry.Category, 0)),
                    "Null and depleted items must stay hidden.");
            }
        }
    }

    private static void BodyPartsRejectUnrelatedAndMismatchedDelegates()
    {
        var contract = CreateBodyPartContract();
        var owner = new UIAutopsyWindowData(ItemType.Brain);
        var other = new UIAutopsyWindowData(ItemType.Brain);
        Assert(contract.GetPolicy(owner.InsertSelected, owner.ChangeEligible) == null &&
            contract.GetPolicy(owner.ChangeSelected, owner.InsertEligible) == null, "Reject crossed action pairs.");
        Assert(contract.GetPolicy(other.InsertSelected, owner.InsertEligible) == null,
            "Callback and predicate must belong to the same autopsy data instance.");
        Assert(contract.GetPolicy(owner.InsertSelected + other.InsertSelected, owner.InsertEligible) == null &&
            contract.GetPolicy(owner.InsertSelected, owner.InsertEligible + other.InsertEligible) == null,
            "Multicast delegates must retain the original picker.");
        Assert(contract.GetPolicy(cell => owner.InsertSelected(cell), owner.InsertEligible) == null &&
            contract.GetPolicy(owner.InsertSelected, item => owner.InsertEligible(item)) == null,
            "Unknown wrappers and predicates must not activate body-part support.");
        Assert(contract.GetPolicy(null, owner.InsertEligible) == null &&
            contract.GetPolicy(owner.InsertSelected, null) == null, "Incomplete contexts must use vanilla.");
    }

    private static void BodyPartsRejectUnsupportedCategories()
    {
        var contract = CreateBodyPartContract();
        foreach (var category in new[] { ItemType.None, ItemType.Flesh, (ItemType)999 })
        {
            var owner = new UIAutopsyWindowData(category);
            Assert(contract.GetPolicy(owner.InsertSelected, owner.InsertEligible) == null &&
                contract.GetPolicy(owner.ChangeSelected, owner.ChangeEligible) == null,
                "Only the six explicitly supported categories may activate consolidation.");
        }
    }

    private static void BodyPartsAggregateOnlyTheirCategoryAndRetainVariants()
    {
        var owner = new UIAutopsyWindowData(ItemType.Brain);
        var policy = CreateBodyPartContract().GetPolicy(owner.InsertSelected, owner.InsertEligible)!;
        var a = new Item("brain:1", ItemType.Brain, 20);
        var b = new Item("brain:1", ItemType.Brain, 35);
        var variant = new Item("brain:2", ItemType.Brain, 50);
        var unrelated = new Item("heart", ItemType.Heart, 9) { HasInstanceData = true };
        var bag = new object();
        var sources = new[]
        {
            BodyPartSource(new object(), a), BodyPartSource(bag, b), BodyPartSource(new object(), variant),
            BodyPartSource(new object(), unrelated), BodyPartSource(new object(), a)
        };
        var snapshot = PickerSnapshot<Item>.Build(sources, policy);
        Assert(snapshot.Groups.Count == 2 && snapshot.Groups[0].Total == 55 && snapshot.Groups[1].Total == 50,
            "Combine full-ID equivalents without collapsing variants or counting aliases twice.");
        Assert(ReferenceEquals(snapshot.Groups[0].Sources[1].Container, bag) &&
            a.Count == 20 && b.Count == 35 && variant.Count == 50 && unrelated.Count == 9,
            "Preserve real ownership and all source quantities, including unrelated property-bearing items.");
    }

    private static void BodyPartsKeepBlockedItemsVisibleAndRevalidateSelection()
    {
        var owner = new UIAutopsyWindowData(ItemType.Heart) { PermitReplacement = false };
        var policy = CreateBodyPartContract().GetPolicy(owner.ChangeSelected, owner.ChangeEligible)!;
        var heart = new Item("heart", ItemType.Heart, 12);
        var mistake = new Item("mistake", ItemType.Heart);
        mistake.Definition.isOrganMistake = true;
        var sources = new[] { BodyPartSource(new object(), heart), BodyPartSource(new object(), mistake) };
        var blocked = PickerSnapshot<Item>.Build(sources, policy);
        Assert(blocked.Groups.Count == 2 && !blocked.TryResolveSelectable(heart.id, out _) &&
            !blocked.TryResolveSelectable(mistake.id, out _), "Matching blocked and mistake entries stay visible and disabled.");
        owner.PermitReplacement = true;
        var allowed = PickerSnapshot<Item>.Build(sources, policy);
        Assert(!blocked.SameAs(allowed) && allowed.TryResolveSelectable(heart.id, out _) &&
            !allowed.TryResolveSelectable(mistake.id, out _), "Availability changes refresh even with identical stock.");
        owner.PermitReplacement = false;
        var fresh = PickerSnapshot<Item>.Build(sources, policy);
        Assert(!fresh.TryResolveSelectable(heart.id, out _) && heart.Count == 12,
            "Fresh selection validation must reject a previously enabled part without consuming anything.");
    }

    private static void BodyPartsRejectInstanceDataIncludingDisabledParts()
    {
        foreach (bool available in new[] { false, true })
        {
            var owner = new UIAutopsyWindowData(ItemType.Skin) { PermitInsertion = available };
            var policy = CreateBodyPartContract().GetPolicy(owner.InsertSelected, owner.InsertEligible)!;
            var plain = new Item("skin", ItemType.Skin, 3);
            var unusual = new Item("skin", ItemType.Skin) { HasInstanceData = true };
            bool rejected = false;
            try
            {
                PickerSnapshot<Item>.Build(new[] { BodyPartSource(new object(), plain), BodyPartSource(new object(), unusual) }, policy);
            }
            catch (NotSupportedException) { rejected = true; }
            Assert(rejected && plain.Count == 3 && unusual.Count == 1,
                "Any included instance data must abort aggregation before mutation, even for disabled same-ID parts.");
        }
    }

    private static void BodyPartsResolveCurrentRealSourcesForUidConsumption()
    {
        var owner = new UIAutopsyWindowData(ItemType.Skull);
        var policy = CreateBodyPartContract().GetPolicy(owner.ChangeSelected, owner.ChangeEligible)!;
        var a = new Item("skull", ItemType.Skull, 2);
        var b = new Item("skull", ItemType.Skull, 5);
        var container = new object();
        var initial = PickerSnapshot<Item>.Build(new[] { BodyPartSource(container, a), BodyPartSource(container, b) }, policy);
        Assert(initial.TryResolveSelectable(a.id, out var selected) && ReferenceEquals(selected, a) && selected.Count == 2,
            "The native callback must receive a real source rather than a synthetic item with the total.");
        owner.ChangeSelected(new UIItemCell { DisplayingItem = selected });
        Assert(ReferenceEquals(owner.Selected, a), "Retain the exact selected object for the game's delayed UID removal.");
        a.Count = 0;
        var fresh = PickerSnapshot<Item>.Build(new[] { BodyPartSource(container, a), BodyPartSource(container, b) }, policy);
        Assert(fresh.Groups[0].Total == 5 && fresh.TryResolveSelectable(b.id, out selected) && ReferenceEquals(selected, b),
            "Fresh resolution must select a surviving accessible stack after the original representative is depleted.");
    }

    private static void BodyPartsShareOneIndependentLiveToggle()
    {
        bool enabled = true;
        var contract = CreateBodyPartContract(() => enabled);
        var policies = new System.Collections.Generic.List<PickerPolicy<Item>>();
        foreach (var entry in BodyPartTitles)
        {
            var owner = new UIAutopsyWindowData(entry.Category);
            policies.Add(contract.GetPolicy(owner.InsertSelected, owner.InsertEligible)!);
            policies.Add(contract.GetPolicy(owner.ChangeSelected, owner.ChangeEligible)!);
        }
        foreach (bool value in new[] { false, true })
        {
            enabled = value;
            foreach (var policy in policies)
                Assert(policy.IsEnabled == value, "All six titles and both actions must read the same live Body Parts toggle.");
        }
    }
}
