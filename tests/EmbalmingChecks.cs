using System;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static EmbalmingPickerContract CreateEmbalmingContract(Func<bool>? enabled = null)
        => new EmbalmingPickerContract(enabled ?? (() => true), item =>
        {
            if (item.HasInstanceData)
                throw new NotSupportedException("Unsupported instance properties");
        });

    private static void EmbalmingRecognizesOnlyItsExactActionAndOwner()
    {
        var contract = CreateEmbalmingContract();
        var owner = new UIEmbalmWindowData();
        var other = new UIEmbalmWindowData();
        var predicate = owner.Predicate;
        var policy = contract.GetPolicy(owner.Callback, predicate);
        Assert(policy != null && policy.Title == "Embalming Fluids" && policy.IsEnabled &&
            ReferenceEquals(policy.CanSelect, predicate), "Recognize the native action and retain its exact live predicate.");
        Assert(contract.GetPolicy(other.Callback, predicate) == null &&
            contract.GetPolicy(owner.Callback, other.Predicate) == null, "Reject delegates belonging to different bodies.");
        Assert(contract.GetPolicy(owner.Callback + other.Callback, predicate) == null &&
            contract.GetPolicy(owner.Callback, predicate + other.Predicate) == null, "Reject multicast actions/predicates.");
        Assert(contract.GetPolicy(cell => owner.Callback(cell), predicate) == null &&
            contract.GetPolicy(owner.Callback, item => predicate(item)) == null, "Unknown wrappers must retain vanilla.");
        Assert(contract.GetPolicy(null, predicate) == null && contract.GetPolicy(owner.Callback, null) == null,
            "Incomplete contexts must retain vanilla.");
        var autopsy = new UIAutopsyWindowData(ItemType.Brain);
        Assert(contract.GetPolicy(autopsy.InsertSelected, autopsy.InsertEligible) == null &&
            contract.GetPolicy(autopsy.InsertSelected, predicate) == null &&
            CreateBodyPartContract().GetPolicy(owner.Callback, predicate) == null,
            "Body-part actions and embalming must not activate each other's contracts.");
    }

    private static void EmbalmingConsolidatesFluidsIncludingBlockedVariants()
    {
        var owner = new UIEmbalmWindowData { NativeRule = item => item.id != "fluid:2" };
        var policy = CreateEmbalmingContract().GetPolicy(owner.Callback, owner.Predicate)!;
        var a = new Item("fluid:1", ItemType.Embalm, 20);
        var b = new Item("fluid:1", ItemType.Embalm, 35);
        var blocked = new Item("fluid:2", ItemType.Embalm, 50);
        var blockedInBag = new Item("fluid:2", ItemType.Embalm, 60);
        var unrelated = new Item("skin", ItemType.Skin) { HasInstanceData = true };
        var bag = new object();
        var snapshot = PickerSnapshot<Item>.Build(new[]
        {
            BodyPartSource(new object(), a), BodyPartSource(bag, b), BodyPartSource(new object(), a),
            BodyPartSource(new object(), blocked), BodyPartSource(bag, blockedInBag),
            BodyPartSource(new object(), unrelated), BodyPartSource(new object(), new Item("empty", ItemType.Embalm, 0))
        }, policy);
        Assert(snapshot.Groups.Count == 2 && snapshot.Groups[0].Total == 55 && snapshot.Groups[1].Total == 110,
            "Aggregate equivalent fluids without stack caps or aliases; keep definition variants distinct and visible when blocked.");
        Assert(snapshot.TryResolveSelectable(a.id, out var selected) && ReferenceEquals(selected, a) &&
            !snapshot.TryResolveSelectable(blocked.id, out _), "Only the native predicate can authorize selection.");
        Assert(!policy.IncludeInDisplay(null!) && !policy.IncludeInDisplay(unrelated) &&
            ReferenceEquals(snapshot.Groups[0].Sources[1].Container, bag), "Exclude non-fluids and retain real ownership.");
        Assert(a.Count == 20 && b.Count == 35 && blocked.Count == 50 && blockedInBag.Count == 60,
            "Projection must not mutate any source stack.");
    }

    private static void EmbalmingAvailabilityChangesRevalidateWithoutStockChanges()
    {
        bool canApply = true;
        var owner = new UIEmbalmWindowData { NativeRule = _ => canApply };
        var policy = CreateEmbalmingContract().GetPolicy(owner.Callback, owner.Predicate)!;
        var fluid = new Item("fluid", ItemType.Embalm, 7);
        var sources = new[] { BodyPartSource(new object(), fluid) };
        var allowed = PickerSnapshot<Item>.Build(sources, policy);
        Assert(allowed.TryResolveSelectable(fluid.id, out _), "The initial native result permits selection.");
        canApply = false;
        var blocked = PickerSnapshot<Item>.Build(sources, policy);
        Assert(!allowed.SameAs(blocked) && blocked.Groups.Count == 1 && !blocked.TryResolveSelectable(fluid.id, out _),
            "Body-state changes must refresh disabled appearance and reject stale selections while retaining visibility.");
        canApply = true;
        var restored = PickerSnapshot<Item>.Build(sources, policy);
        Assert(!blocked.SameAs(restored) && restored.TryResolveSelectable(fluid.id, out _) && fluid.Count == 7,
            "The same live predicate must observe restored availability without consuming or reserving stock.");
    }

    private static void EmbalmingRejectsInstanceDataIncludingDisabledFluids()
    {
        foreach (bool available in new[] { false, true })
        {
            var owner = new UIEmbalmWindowData { NativeRule = _ => available };
            var policy = CreateEmbalmingContract().GetPolicy(owner.Callback, owner.Predicate)!;
            var plain = new Item("fluid", ItemType.Embalm, 5);
            var unusual = new Item("fluid", ItemType.Embalm) { HasInstanceData = true };
            bool rejected = false;
            try
            {
                PickerSnapshot<Item>.Build(new[] { BodyPartSource(new object(), plain), BodyPartSource(new object(), unusual) }, policy);
            }
            catch (NotSupportedException) { rejected = true; }
            Assert(rejected && plain.Count == 5 && unusual.Count == 1,
                "Validate all included fluids before grouping, including disabled same-ID instances; preserve sources on failure.");
        }
    }

    private static void EmbalmingResolvesRealSurvivingSourceAndLiveToggle()
    {
        bool enabled = true;
        var owner = new UIEmbalmWindowData();
        var policy = CreateEmbalmingContract(() => enabled).GetPolicy(owner.Callback, owner.Predicate)!;
        var a = new Item("fluid", ItemType.Embalm, 2);
        var b = new Item("fluid", ItemType.Embalm, 8);
        var container = new object();
        var initial = PickerSnapshot<Item>.Build(new[] { BodyPartSource(container, a), BodyPartSource(container, b) }, policy);
        Assert(initial.Groups[0].Total == 10 && initial.TryResolveSelectable(a.id, out var first) && ReferenceEquals(first, a),
            "A virtual total must resolve to a real stack with its original identity/count.");
        a.Count = 0;
        var fresh = PickerSnapshot<Item>.Build(new[] { BodyPartSource(container, a), BodyPartSource(container, b) }, policy);
        Assert(fresh.TryResolveSelectable(b.id, out var selected) && ReferenceEquals(selected, b) && b.Count == 8,
            "Resolve a surviving accessible source when the original representative is depleted.");
        owner.Callback(new UIItemCell { DisplayingItem = selected });
        Assert(ReferenceEquals(owner.Selected, b), "Pass that real item to the native UID-consuming callback.");
        enabled = false;
        Assert(!policy.IsEnabled, "An open session must observe the live Embalming Fluids setting.");
        enabled = true;
        Assert(policy.IsEnabled, "The setting must permit consolidation on the next picker opening.");
    }
}
