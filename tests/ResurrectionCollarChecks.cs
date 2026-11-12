using System;
using BepInEx.Configuration;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static ResurrectionCollarPickerContract CreateResurrectionContract(Func<bool>? enabled = null)
        => new ResurrectionCollarPickerContract(enabled ?? (() => true), item =>
        {
            if (item.HasInstanceData || item.Definition.hasDurability)
                throw new NotSupportedException("Unsupported instance properties");
        });

    private static PickerPolicy<Item> ResurrectionPolicy(ResurrectionCollarPickerContract contract,
        UIResurrectionWindowData owner)
    {
        using (contract.BeginOpening(owner))
            return contract.GetPolicy(owner.Callback, owner.Predicate)
                ?? throw new InvalidOperationException("Missing resurrection policy.");
    }

    private static void PickerIdentitiesPreserveAllDefaultTitles()
    {
        var expected = new[]
        {
            (PickerId.Seeds, "Seeds"), (PickerId.Fertilizers, "Fertilizers"),
            (PickerId.AlchemicalIngredients, "Alchemical Ingredients"),
            (PickerId.Brains, "Brains"), (PickerId.Hearts, "Hearts"), (PickerId.Guts, "Guts"),
            (PickerId.Skulls, "Skulls"), (PickerId.Bones, "Bones"), (PickerId.Skins, "Skins"),
            (PickerId.EmbalmingFluids, "Embalming Fluids"), (PickerId.Tombstones, "Tombstones"),
            (PickerId.Fences, "Fences"), (PickerId.ZombieCollars, "Collars"),
            (PickerId.ZombieArmor, "Armor"), (PickerId.ZombieWeapons, "Weapons"),
            (PickerId.ZombieTools, "Tools"), (PickerId.ResurrectionCollars, "Collars"),
            (PickerId.Sermons, "Prayers"), (PickerId.StudyResearchable, "Study"),
            (PickerId.StudyDecomposable, "Decompose"), (PickerId.ShopSelling, "Sellable")
        };
        Assert(Enum.GetValues(typeof(PickerId)).Length == expected.Length, "Every picker has an explicit default name.");
        foreach (var entry in expected)
        {
            var policy = new PickerPolicy<Item>(entry.Item1, () => true, _ => true, _ => true, _ => { });
            Assert(policy.Id == entry.Item1 && policy.Title == entry.Item2,
                "Identity migration must retain every existing visible title.");
        }
        var zombie = new ZombieWgoData();
        var equipment = new ZombieEquipmentInventoryWidgetData(zombie);
        var upgrade = CreateZombieEquipmentContract().GetPolicy(equipment.CollarCallback, zombie.UpgradePredicate)!;
        var resurrection = ResurrectionPolicy(CreateResurrectionContract(), new UIResurrectionWindowData());
        Assert(upgrade.Id == PickerId.ZombieCollars && resurrection.Id == PickerId.ResurrectionCollars &&
            upgrade.Id != resurrection.Id && upgrade.Title == resurrection.Title && upgrade.Title == "Collars",
            "The two production collar policies share only their displayed default, never their identity.");
    }

    private static void ResurrectionRequiresOpeningScopeAndMatchingNativeOwners()
    {
        var contract = CreateResurrectionContract();
        var owner = new UIResurrectionWindowData();
        var other = new UIResurrectionWindowData();
        var callback = owner.Callback;
        var predicate = owner.Predicate;
        Assert(contract.OpenPicker.Name == "OnCollarPressed" && contract.GetPolicy(callback, predicate) == null,
            "A resurrection delegate outside the named opening method must not activate consolidation.");
        using (contract.BeginOpening(owner))
        {
            var policy = contract.GetPolicy(callback, predicate);
            Assert(policy != null && ReferenceEquals(policy.CanSelect, predicate), "Reuse the exact native predicate.");
            Assert(contract.GetPolicy(other.Callback, predicate) == null && contract.GetPolicy(callback, other.Predicate) == null &&
                contract.GetPolicy(other.Callback, other.Predicate) == null, "Reject every mismatched owner.");
            Assert(contract.GetPolicy(cell => callback(cell), predicate) == null &&
                contract.GetPolicy(callback, item => predicate(item)) == null, "Unknown wrappers must retain vanilla.");
            Assert(contract.GetPolicy(callback + callback, predicate) == null && contract.GetPolicy(callback, predicate + predicate) == null &&
                contract.GetPolicy(null, predicate) == null && contract.GetPolicy(callback, null) == null, "Reject multicast or missing delegates.");
            var zombie = new ZombieWgoData();
            var equipment = new ZombieEquipmentInventoryWidgetData(zombie);
            Assert(contract.GetPolicy(equipment.CollarCallback, zombie.UpgradePredicate) == null &&
                CreateZombieEquipmentContract().GetPolicy(callback, predicate) == null &&
                CreateEmbalmingContract().GetPolicy(callback, predicate) == null &&
                CreateBodyPartContract().GetPolicy(callback, predicate) == null &&
                CreateGraveDecorationContract().GetPolicy(callback, predicate) == null,
                "Resurrection must remain separate from all other production contracts.");
            Assert(CreateResurrectionContract().GetPolicy(callback, predicate) == null, "Scope belongs to its installed contract.");
        }
        Assert(contract.GetPolicy(callback, predicate) == null, "The context must not escape the opening call.");
    }

    private static void ResurrectionOpeningFinalizerRestoresContextAfterExceptionsAndNesting()
    {
        var previousPlugin = Plugin.Instance;
        var contract = CreateResurrectionContract();
        var outer = new UIResurrectionWindowData();
        var inner = new UIResurrectionWindowData();
        Plugin.Instance = new Plugin { ResurrectionCollars = contract };
        try
        {
            ResurrectionCollarPatches.BeforeOpening(outer, out var outerState);
            try
            {
                ResurrectionCollarPatches.BeforeOpening(inner, out var innerState);
                var failure = new InvalidOperationException("Native opening failure");
                try
                {
                    Assert(contract.GetPolicy(inner.Callback, inner.Predicate) != null &&
                        contract.GetPolicy(outer.Callback, outer.Predicate) == null, "Nested calls must use their own owner.");
                }
                finally
                {
                    Assert(ReferenceEquals(ResurrectionCollarPatches.AfterOpening(innerState, failure), failure),
                        "The real finalizer must preserve a native exception.");
                }
                Assert(contract.GetPolicy(outer.Callback, outer.Predicate) != null, "Finalizing the inner call restores its parent.");
                innerState!.Dispose();
                Assert(contract.GetPolicy(outer.Callback, outer.Predicate) != null, "Repeated disposal is harmless.");
            }
            finally { ResurrectionCollarPatches.AfterOpening(outerState, null); }
            Assert(contract.GetPolicy(outer.Callback, outer.Predicate) == null, "Finalizing success must also clear the scope.");
            Plugin.Instance = null;
            ResurrectionCollarPatches.BeforeOpening(outer, out var inactive);
            Assert(inactive == null && ResurrectionCollarPatches.AfterOpening(inactive, null) == null,
                "An unavailable plugin must leave the native call untouched.");
        }
        finally { Plugin.Instance = previousPlugin; }
    }

    private static void ResurrectionAggregatesCollarsIncludingUnsuitableVariants()
    {
        var owner = new UIResurrectionWindowData { Rule = item => item.id == "collar:2" };
        var policy = ResurrectionPolicy(CreateResurrectionContract(), owner);
        var a = new Item("collar:1", ItemType.Collar, 20);
        var b = new Item("collar:1", ItemType.Collar, 35);
        var c = new Item("collar:1", ItemType.Collar, 50);
        var suitable = new Item("collar:2", ItemType.Collar, 3);
        var unrelated = new Item("other", ItemType.None) { HasInstanceData = true };
        var bag = new object();
        var chest = new object();
        var snapshot = PickerSnapshot<Item>.Build(new[]
        {
            CollarSource(new object(), a), CollarSource(bag, b), CollarSource(chest, c),
            CollarSource(chest, suitable), CollarSource(chest, a), CollarSource(bag, unrelated),
            CollarSource(bag, new Item("depleted", ItemType.Collar, 0))
        }, policy);
        Assert(snapshot.Groups.Count == 2 && snapshot.Groups[0].Total == 105 && snapshot.Groups[1].Total == 3,
            "Aggregate accessible equivalents beyond stack limits while retaining full-ID variants and deduplicating aliases.");
        Assert(!snapshot.TryResolveSelectable(a.id, out _) && snapshot.TryResolveSelectable(suitable.id, out _),
            "Unsuitable collars remain visible and unselectable; unrelated/depleted items stay hidden.");
        Assert(ReferenceEquals(snapshot.Groups[0].Sources[1].Container, bag) &&
            ReferenceEquals(snapshot.Groups[0].Sources[2].Container, chest) && !policy.IncludeInDisplay(null!) &&
            a.Count == 20 && b.Count == 35 && c.Count == 50 && suitable.Count == 3,
            "Retain real sources and leave all stored quantities unchanged.");
    }

    private static void ResurrectionRevalidatesNativeEligibilityAndVirtualDisabledCells()
    {
        bool allowed = false;
        var owner = new UIResurrectionWindowData { Rule = _ => allowed };
        var policy = ResurrectionPolicy(CreateResurrectionContract(), owner);
        var collar = new Item("collar", ItemType.Collar, 8);
        var sources = new[] { CollarSource(new object(), collar) };
        var disabled = PickerSnapshot<Item>.Build(sources, policy);
        using var fixture = new SessionFixture(new[] { policy }, SessionSources(collar), policy.CanSelect);
        fixture.Open();
        var view = fixture.View(0);
        Assert(disabled.Groups.Count == 1 && !disabled.TryResolveSelectable(collar.id, out _) &&
            !view.CustomItemsAvailableCondition!(collar), "Native rejection must disable the displayed cell as well as resolution.");
        allowed = true;
        var enabled = PickerSnapshot<Item>.Build(sources, policy);
        Assert(!disabled.SameAs(enabled) && enabled.TryResolveSelectable(collar.id, out _) &&
            view.CustomItemsAvailableCondition!(collar), "The policy remains live after the opening scope ends.");
        allowed = false;
        Assert(!PickerSnapshot<Item>.Build(sources, policy).TryResolveSelectable(collar.id, out _) && collar.Count == 8,
            "Click-time revalidation must reject stale eligibility without consuming anything.");
    }

    private static void ResurrectionRejectsUnexpectedInstanceDataEvenWhenDisabled()
    {
        foreach (bool allowed in new[] { false, true })
        foreach (bool durability in new[] { false, true })
        {
            var owner = new UIResurrectionWindowData { Rule = _ => allowed };
            var policy = ResurrectionPolicy(CreateResurrectionContract(), owner);
            var collar = new Item("unusual", ItemType.Collar) { HasInstanceData = !durability };
            collar.Definition.hasDurability = durability;
            bool rejected = false;
            try { PickerSnapshot<Item>.Build(new[] { CollarSource(new object(), collar) }, policy); }
            catch (NotSupportedException) { rejected = true; }
            Assert(rejected && collar.Count == 1, "Even a disabled unusual collar must trigger the existing safe fallback.");
        }
    }

    private static void ResurrectionPassesARealSurvivingSourceForDelayedNativeConsumption()
    {
        var owner = new UIResurrectionWindowData();
        var policy = ResurrectionPolicy(CreateResurrectionContract(), owner);
        var first = new Item("collar", ItemType.Collar, 2);
        var surviving = new Item("collar", ItemType.Collar, 3);
        var sources = new[] { CollarSource(new object(), first), CollarSource(new object(), surviving) };
        var before = PickerSnapshot<Item>.Build(sources, policy);
        var fresh = PickerSnapshot<Item>.Build(new[] { sources[1] }, policy);
        Assert(!before.SameAs(fresh) && fresh.TryResolveSelectable("collar", out var selected) &&
            ReferenceEquals(selected, surviving), "A source removed before clicking must resolve to a currently accessible real stack.");
        owner.Callback(new UIItemCell { DisplayingItem = surviving });
        Assert(ReferenceEquals(owner.Collar, surviving) && first.Count == 2 && surviving.Count == 3,
            "The native callback retains the actual item for later UID consumption; selection itself consumes nothing.");
        PickerSnapshot<Item>.Build(Array.Empty<StackSource<Item>>(), policy);
        Assert(ReferenceEquals(owner.Collar, surviving), "A later display snapshot must not reserve, replace, or mutate the retained native selection.");
        // Actual delayed removal and stale-source behavior belong to the game and
        // are deliberately not reimplemented or claimed as tested here.
    }

    private static void ResurrectionConfigurationDefaultsAndLiveToggleRemainIndependent()
    {
        var config = new ConfigFile();
        config.Bind("Actions", "Zombie Equipment", false, "Existing saved preference");
        var settings = new ActionSettings(config);
        Assert(settings.ResurrectionCollars.Value && !settings.ZombieEquipment.Value && settings.Seeds.Value &&
            settings.Fertilizers.Value && settings.AlchemicalIngredients.Value && settings.BodyParts.Value &&
            settings.EmbalmingFluids.Value && settings.GraveDecorations.Value,
            "The real settings declarations default resurrection on without changing saved equipment settings or other defaults.");
        var owner = new UIResurrectionWindowData();
        var policy = ResurrectionPolicy(CreateResurrectionContract(() => settings.ResurrectionCollars.Value), owner);
        var zombie = new ZombieWgoData();
        var equipment = new ZombieEquipmentInventoryWidgetData(zombie);
        var upgrades = CreateZombieEquipmentContract(() => settings.ZombieEquipment.Value)
            .GetPolicy(equipment.CollarCallback, zombie.UpgradePredicate)!;
        Assert(policy.IsEnabled && !upgrades.IsEnabled, "The two Collars policies read separate live settings.");
        settings.ResurrectionCollars.Value = false;
        settings.ZombieEquipment.Value = true;
        Assert(!policy.IsEnabled && upgrades.IsEnabled && settings.Seeds.Value && settings.Fertilizers.Value &&
            settings.AlchemicalIngredients.Value, "Toggling either collar action must leave the other features independent.");
        Assert(ReferenceEquals(settings.ResurrectionCollars, config.Bind("Actions", "Resurrection Collars", false, "")),
            "The new setting must use the requested configuration key.");
    }
}
