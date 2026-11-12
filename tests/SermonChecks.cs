using System;
using BepInEx.Configuration;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static SermonPickerContract CreateSermonContract(Func<bool>? enabled = null)
        => new SermonPickerContract(enabled ?? (() => true), item =>
        {
            if (item.HasInstanceData || item.Definition.hasDurability)
                throw new NotSupportedException("Unsupported instance properties");
        });

    private static void SermonsRequireExactNativePredicateAndMatchingCallbackOwner()
    {
        var contract = CreateSermonContract();
        var owner = new UIPrayWindowData();
        var other = new UIPrayWindowData();
        var predicate = owner.Predicate;
        var policy = contract.GetPolicy(owner.Callback, predicate);
        Assert(policy != null && policy.Id == PickerId.Sermons && policy.Title == "Prayers" && policy.IsEnabled &&
            ReferenceEquals(policy.IncludeInDisplay, predicate) && ReferenceEquals(policy.CanSelect, predicate),
            "Use the exact native predicate for both visibility and selection, with the stable Sermons identity.");
        Assert(contract.GetPolicy(owner.Callback, other.Predicate) == null && contract.GetPolicy(other.Callback, predicate) == null &&
            contract.GetPolicy(owner.Callback, owner.OtherPredicate) == null, "Reject crossed owners and other predicates on the same owner.");
    }

    private static void SermonsRejectOtherActionsAndUnknownDelegateWrappers()
    {
        var contract = CreateSermonContract();
        var owner = new UIPrayWindowData();
        var callback = owner.Callback;
        var predicate = owner.Predicate;
        Assert(contract.GetPolicy(cell => callback(cell), predicate) == null && contract.GetPolicy(callback, item => predicate(item)) == null &&
            contract.GetPolicy(callback + callback, predicate) == null && contract.GetPolicy(callback, predicate + predicate) == null &&
            contract.GetPolicy(null, predicate) == null && contract.GetPolicy(callback, null) == null,
            "Unknown wrappers, multicast delegates, or incomplete contexts must retain vanilla.");
        var zombie = new ZombieWgoData();
        var equipment = new ZombieEquipmentInventoryWidgetData(zombie);
        var resurrection = new UIResurrectionWindowData();
        var embalming = new UIEmbalmWindowData();
        Assert(contract.GetPolicy(equipment.CollarCallback, zombie.UpgradePredicate) == null &&
            contract.GetPolicy(resurrection.Callback, resurrection.Predicate) == null &&
            contract.GetPolicy(embalming.Callback, embalming.Predicate) == null,
            "Existing gameplay actions must never activate sermon consolidation.");
        Assert(CreateZombieEquipmentContract().GetPolicy(callback, predicate) == null &&
            CreateResurrectionContract().GetPolicy(callback, predicate) == null &&
            CreateBodyPartContract().GetPolicy(callback, predicate) == null &&
            CreateEmbalmingContract().GetPolicy(callback, predicate) == null &&
            CreateGraveDecorationContract().GetPolicy(callback, predicate) == null,
            "Sermon delegates must remain separate from all existing production contracts.");
    }

    private static void SermonsAggregateEligibleDefinitionsAndQualityVariantsWithoutMutation()
    {
        var owner = new UIPrayWindowData { Rule = item => item.id != "missing-sermon-definition" };
        var policy = CreateSermonContract().GetPolicy(owner.Callback, owner.Predicate)!;
        var a = new Item("sermon_faith:1", ItemType.Preach, 20);
        var b = new Item("sermon_faith:1", ItemType.Preach, 35);
        var c = new Item("sermon_faith:1", ItemType.Preach, 50);
        var quality = new Item("sermon_faith:2", ItemType.Preach, 3);
        var type = new Item("sermon_money:1", ItemType.Preach, 2);
        var bag = new object();
        var chest = new object();
        var uid = a.UniqueId;
        var snapshot = PickerSnapshot<Item>.Build(new[]
        {
            CollarSource(new object(), a), CollarSource(bag, b), CollarSource(chest, c), CollarSource(chest, a),
            CollarSource(bag, quality), CollarSource(chest, type),
            CollarSource(chest, new Item("missing-sermon-definition", ItemType.Preach) { HasInstanceData = true }),
            CollarSource(chest, new Item("unrelated", ItemType.None) { HasInstanceData = true }),
            CollarSource(chest, new Item("depleted", ItemType.Preach, 0))
        }, policy);
        Assert(snapshot.Groups.Count == 3 && snapshot.Groups[0].Key == a.id && snapshot.Groups[0].Total == 105 &&
            snapshot.Groups[1].Key == quality.id && snapshot.Groups[2].Key == type.id,
            "Combine eligible equivalents above stack limits, retain full-ID types/qualities, and hide native-rejected items.");
        Assert(ReferenceEquals(snapshot.Groups[0].Sources[1].Container, bag) &&
            ReferenceEquals(snapshot.Groups[0].Sources[2].Container, chest) && a.Count == 20 && b.Count == 35 && c.Count == 50 &&
            quality.Count == 3 && type.Count == 2 && a.UniqueId == uid && !policy.IncludeInDisplay(null!),
            "Retain real sources and UID/counts, deduplicate aliases, and exclude irrelevant data before validation.");
    }

    private static void SermonsRevalidateNativeEligibilityAndSourceChanges()
    {
        bool exists = true;
        var owner = new UIPrayWindowData { Rule = _ => exists };
        var policy = CreateSermonContract().GetPolicy(owner.Callback, owner.Predicate)!;
        var first = new Item("sermon", ItemType.Preach, 4);
        var second = new Item("sermon", ItemType.Preach, 7);
        var sources = new[] { CollarSource(new object(), first), CollarSource(new object(), second) };
        var before = PickerSnapshot<Item>.Build(sources, policy);
        var moved = PickerSnapshot<Item>.Build(new[] { sources[1] }, policy);
        Assert(!before.SameAs(moved) && moved.TryResolveSelectable(first.id, out var real) && ReferenceEquals(real, second),
            "A stale clicked group must resolve to a surviving real source before native callback dispatch.");
        exists = false;
        var rejected = PickerSnapshot<Item>.Build(sources, policy);
        Assert(!before.SameAs(rejected) && rejected.Groups.Count == 0 && !rejected.TryResolveSelectable(first.id, out _) &&
            first.Count == 4 && second.Count == 7, "Re-evaluate native eligibility and reject a stale selection without consuming stock.");
    }

    private static void SermonsRetainTheExistingItemSafetyValidator()
    {
        foreach (bool durability in new[] { false, true })
        {
            var owner = new UIPrayWindowData();
            var policy = CreateSermonContract().GetPolicy(owner.Callback, owner.Predicate)!;
            var sermon = new Item("unusual-sermon", ItemType.Preach) { HasInstanceData = !durability };
            sermon.Definition.hasDurability = durability;
            bool rejected = false;
            try { PickerSnapshot<Item>.Build(new[] { CollarSource(new object(), sermon) }, policy); }
            catch (NotSupportedException) { rejected = true; }
            Assert(rejected && sermon.Count == 1, "Unexpected visible item data must enter the unchanged safe fallback path.");
        }
    }

    private static void SermonsResolveARealSourceForTheOriginalDefinitionCallback()
    {
        var owner = new UIPrayWindowData();
        var policy = CreateSermonContract().GetPolicy(owner.Callback, owner.Predicate)!;
        var source = new Item("sermon:gold", ItemType.Preach, 8);
        var snapshot = PickerSnapshot<Item>.Build(new[] { CollarSource(new object(), source) }, policy);
        Assert(snapshot.TryResolveSelectable(source.id, out var selected) && ReferenceEquals(selected, source),
            "Selection forwards the actual source, never a synthetic combined stack.");
        owner.Callback(new UIItemCell { DisplayingItem = selected });
        PickerSnapshot<Item>.Build(Array.Empty<StackSource<Item>>(), policy);
        Assert(owner.SelectedDefinition == source.id && source.Count == 8,
            "Keep the native full-definition selection; stock updates must not reserve, consume, or clear it afterwards.");
    }

    private static void SermonsDefaultOnAndUseAnIndependentLiveSetting()
    {
        var config = new ConfigFile();
        config.Bind("Actions", "Seeds", false, "Saved preference");
        config.Bind("Actions", "Resurrection Collars", false, "Saved preference");
        var settings = new ActionSettings(config);
        var owner = new UIPrayWindowData();
        var policy = CreateSermonContract(() => settings.Sermons.Value).GetPolicy(owner.Callback, owner.Predicate)!;
        Assert(policy.IsEnabled && settings.Sermons.Value && !settings.Seeds.Value && !settings.ResurrectionCollars.Value &&
            settings.Fertilizers.Value && settings.AlchemicalIngredients.Value && settings.BodyParts.Value &&
            settings.EmbalmingFluids.Value && settings.GraveDecorations.Value && settings.ZombieEquipment.Value,
            "The production Prayers setting must default on without resetting existing preferences or other defaults.");
        settings.Sermons.Value = false;
        settings.ResurrectionCollars.Value = true;
        Assert(!policy.IsEnabled && settings.ResurrectionCollars.Value && settings.ZombieEquipment.Value && !settings.Seeds.Value,
            "The sermon policy must read its independent setting live without changing other features.");
        Assert(ReferenceEquals(settings.Sermons, config.Bind("Actions", "Prayers", true, "")) &&
            !config.ContainsKey(new ConfigDefinition("Actions", "Sermons")),
            "Expose only Actions/Prayers while retaining the stable internal identity.");
    }

    private static void PrayersUsesOnlyTheReleaseSettingWithoutMigration()
    {
        foreach (bool legacy in new[] { false, true })
        {
            foreach (bool? current in new bool?[] { null, false, true })
            {
                var config = new ConfigFile();
                config.LoadSavedValue("Actions", "Sermons", legacy);
                if (current.HasValue) config.LoadSavedValue("Actions", "Prayers", current.Value);
                config.LoadSavedValue("Actions", "Study Table", false);
                var settings = new ActionSettings(config);
                Assert(settings.Sermons.Value == (current ?? true) && !settings.StudyTable.Value &&
                    !config.ContainsKey(new ConfigDefinition("Actions", "Sermons")) &&
                    ReferenceEquals(settings.Sermons, config.Bind("Actions", "Prayers", true, "")),
                    "Use only Prayers, ignore obsolete development settings, and leave Study Table unchanged.");
                var rebound = new ActionSettings(config);
                Assert(rebound.Sermons.Value == settings.Sermons.Value,
                    "Repeated initialization must preserve the Prayers value.");
            }
        }
    }
}
