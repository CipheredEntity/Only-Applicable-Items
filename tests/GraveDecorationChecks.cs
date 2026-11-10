using System;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static GraveDecorationPickerContract CreateGraveDecorationContract(Func<bool>? enabled = null)
        => new GraveDecorationPickerContract(enabled ?? (() => true), item =>
        {
            if (item.HasInstanceData)
                throw new NotSupportedException("Unsupported instance properties");
        });

    private static Item Decoration(string id, string group, int count = 1)
    {
        var item = new Item(id, ItemType.None, count);
        item.Definition.itemGroupIds.Add(group);
        return item;
    }

    private static StackSource<Item> DecorationSource(object container, Item item)
        => new StackSource<Item>(container, item, item.id, item.Count, 0);

    private static void GraveDecorationsRecognizeBothTitlesAndSharedToggle()
    {
        bool enabled = true;
        var contract = CreateGraveDecorationContract(() => enabled);
        var policies = new System.Collections.Generic.List<PickerPolicy<Item>>();
        foreach (var entry in new[] { (GraveElementType.Top, "Tombstones"), (GraveElementType.Bot, "Fences") })
        {
            var owner = new UIGraveElementWidgetData(entry.Item1);
            var predicate = owner.Predicate;
            var policy = contract.GetPolicy(owner.Callback, predicate);
            Assert(policy != null && policy.Title == entry.Item2 && policy.IsEnabled && ReferenceEquals(policy.CanSelect, predicate),
                "Both category titles must preserve the exact native predicate.");
            Assert(!policy!.IncludeInDisplay(null!) && !policy.IncludeInDisplay(Decoration("empty", "gravetop", 0)),
                "Null and depleted entries must stay hidden.");
            policies.Add(policy);
        }
        foreach (bool value in new[] { false, true })
        {
            enabled = value;
            foreach (var policy in policies)
                Assert(policy.IsEnabled == value, "Both categories must observe the same live Grave Decorations toggle.");
        }
    }

    private static void GraveDecorationsRejectUnrelatedContextsAndCategories()
    {
        var contract = CreateGraveDecorationContract();
        var top = new UIGraveElementWidgetData(GraveElementType.Top);
        var bot = new UIGraveElementWidgetData(GraveElementType.Bot);
        Assert(contract.GetPolicy(top.Callback, bot.Predicate) == null && contract.GetPolicy(bot.Callback, top.Predicate) == null,
            "Even identical methods must belong to the same category owner.");
        Assert(contract.GetPolicy(top.Callback + bot.Callback, top.Predicate) == null &&
            contract.GetPolicy(top.Callback, top.Predicate + bot.Predicate) == null, "Reject multicast contexts.");
        Assert(contract.GetPolicy(cell => top.Callback(cell), top.Predicate) == null &&
            contract.GetPolicy(top.Callback, item => top.Predicate(item)) == null, "Unknown delegate wrappers retain vanilla.");
        Assert(contract.GetPolicy(null, top.Predicate) == null && contract.GetPolicy(top.Callback, null) == null,
            "Incomplete contexts retain vanilla.");
        var unknown = new UIGraveElementWidgetData((GraveElementType)999);
        Assert(contract.GetPolicy(unknown.Callback, unknown.Predicate) == null, "Unknown categories retain vanilla.");
        var embalming = new UIEmbalmWindowData();
        var autopsy = new UIAutopsyWindowData(ItemType.Skull);
        Assert(contract.GetPolicy(embalming.Callback, embalming.Predicate) == null &&
            contract.GetPolicy(autopsy.InsertSelected, autopsy.InsertEligible) == null &&
            CreateEmbalmingContract().GetPolicy(top.Callback, top.Predicate) == null &&
            CreateBodyPartContract().GetPolicy(top.Callback, top.Predicate) == null,
            "Decoration support must remain isolated from embalming and body parts.");
    }

    private static void GraveDecorationsAggregateOnlyTheirGroupAndKeepVariants()
    {
        foreach (var category in new[] { GraveElementType.Top, GraveElementType.Bot })
        {
            var group = category == GraveElementType.Top ? "gravetop" : "gravebot";
            var otherGroup = category == GraveElementType.Top ? "gravebot" : "gravetop";
            var owner = new UIGraveElementWidgetData(category);
            var policy = CreateGraveDecorationContract().GetPolicy(owner.Callback, owner.Predicate)!;
            var a = Decoration("decoration:1", group, 20);
            var b = Decoration("decoration:1", group, 35);
            var c = Decoration("decoration:1", group, 50);
            var variant = Decoration("decoration:2", group, 9);
            var irrelevant = Decoration("other", otherGroup);
            irrelevant.HasInstanceData = true;
            var bag = new object();
            var snapshot = PickerSnapshot<Item>.Build(new[]
            {
                DecorationSource(new object(), a), DecorationSource(bag, b), DecorationSource(new object(), c),
                DecorationSource(new object(), a), DecorationSource(new object(), variant), DecorationSource(bag, irrelevant),
                DecorationSource(new object(), Decoration("grave_name_without_group", "stone"))
            }, policy);
            Assert(snapshot.Groups.Count == 2 && snapshot.Groups[0].Total == 105 && snapshot.Groups[1].Total == 9,
                "Both categories must aggregate full-ID equivalents, preserve variants, deduplicate aliases, and exclude unrelated groups.");
            Assert(snapshot.TryResolveSelectable(a.id, out _) && snapshot.TryResolveSelectable(variant.id, out _),
                "Do not invent quality, research, or recipe restrictions absent from the native category predicate.");
            Assert(ReferenceEquals(snapshot.Groups[0].Sources[1].Container, bag) && a.Count == 20 && b.Count == 35 && c.Count == 50,
                "Preserve real ownership and all source quantities while showing totals above stack limits.");
        }
    }

    private static void GraveDecorationsPreserveNativeAvailabilityAndRefresh()
    {
        bool allowed = false;
        var owner = new UIGraveElementWidgetData(GraveElementType.Top) { NativeRule = _ => allowed };
        var policy = CreateGraveDecorationContract().GetPolicy(owner.Callback, owner.Predicate)!;
        var item = Decoration("headstone", "gravetop", 6);
        var sources = new[] { DecorationSource(new object(), item) };
        var disabled = PickerSnapshot<Item>.Build(sources, policy);
        Assert(disabled.Groups.Count == 1 && !disabled.TryResolveSelectable(item.id, out _),
            "A matching item rejected by the native predicate must stay visible but unselectable.");
        allowed = true;
        var enabled = PickerSnapshot<Item>.Build(sources, policy);
        Assert(!disabled.SameAs(enabled) && enabled.TryResolveSelectable(item.id, out _),
            "Availability-only changes must refresh through the shared snapshot mechanism.");
        allowed = false;
        var fresh = PickerSnapshot<Item>.Build(sources, policy);
        Assert(!fresh.TryResolveSelectable(item.id, out _) && item.Count == 6,
            "Fresh click-time resolution must reject a previously allowed selection without consuming anything.");
    }

    private static void GraveDecorationsRejectInstanceDataIncludingDisabledItems()
    {
        foreach (bool allowed in new[] { false, true })
        {
            var owner = new UIGraveElementWidgetData(GraveElementType.Bot) { NativeRule = _ => allowed };
            var policy = CreateGraveDecorationContract().GetPolicy(owner.Callback, owner.Predicate)!;
            var plain = Decoration("fence", "gravebot", 5);
            var unusual = Decoration("fence", "gravebot");
            unusual.HasInstanceData = true;
            bool rejected = false;
            try
            {
                PickerSnapshot<Item>.Build(new[] { DecorationSource(new object(), plain), DecorationSource(new object(), unusual) }, policy);
            }
            catch (NotSupportedException) { rejected = true; }
            Assert(rejected && plain.Count == 5 && unusual.Count == 1,
                "Any included instance data must abort grouping before mutation, even for disabled same-ID items.");
        }
    }

    private static void GraveDecorationsResolveRealSourcesWithoutChangingDefinitions()
    {
        var owner = new UIGraveElementWidgetData(GraveElementType.Bot);
        var policy = CreateGraveDecorationContract().GetPolicy(owner.Callback, owner.Predicate)!;
        var a = Decoration("fence:quality", "gravebot", 2);
        var b = Decoration("fence:quality", "gravebot", 8);
        var container = new object();
        var initial = PickerSnapshot<Item>.Build(new[] { DecorationSource(container, a), DecorationSource(container, b) }, policy);
        Assert(initial.Groups[0].Total == 10 && initial.TryResolveSelectable(a.id, out var first) && ReferenceEquals(first, a),
            "Resolve a real representative without overwriting its count or full definition ID.");
        a.Count = 0;
        var fresh = PickerSnapshot<Item>.Build(new[] { DecorationSource(container, a), DecorationSource(container, b) }, policy);
        Assert(fresh.TryResolveSelectable(b.id, out var selected) && ReferenceEquals(selected, b),
            "Resolve a surviving accessible representative after depletion.");
        owner.Callback(new UIItemCell { DisplayingItem = selected });
        Assert(owner.SelectedDefinition == b.id && b.Count == 8,
            "The callback receives the original definition for native recipe lookup; aggregation must not consume or reserve it.");
    }
}
