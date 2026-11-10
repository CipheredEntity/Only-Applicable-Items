using System;
using System.Collections.Generic;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static int Main()
    {
        var checks = new Action[]
        {
            CombinesContainersWithoutChangingSources,
            KeepsQualityDefinitionsSeparate,
            DoesNotCountAliasedStacksTwice,
            RejectsQuantityOverflow,
            DetectsSourceChangesWithTheSameTotal,
            SupportsEmptyAndDepletedInventories,
            ProducesStableOrdering,
            RecognizesOnlyTheExactActionAndOwner,
            FertilizersIncludeAppliedItemsButExcludeOtherItems,
            SeedsStillExcludeUnavailableItems,
            AvailabilityChangesInvalidateSnapshotsWithoutStockChanges,
            BlockedFertilizerCannotResolveAfterBedChanges,
            ActionSettingsRemainIndependent,
            FertilizerContextAcceptsSlotClosuresOnlyWithItsExactPredicate,
            ValidatesUnavailableFertilizerInstances,
            AlchemyPolicyPreservesResearchAndSlotPredicates,
            AlchemyAllowsRepeatedIngredientSelections,
            AlchemyContextIsIndependentOfGardenPredicates,
            AlchemyVisibilityPreservesSurveyExceptions,
            AlchemyVisibilityHidesNonAlchemyEmptyAndNullItems,
            AlchemyRuneBoundsRemainNativeSelectionRestrictions,
            AlchemyRuneAvailabilityChangesRefreshWithoutChangingStock,
            LaboratoryTotalsAreSharedAcrossTwoAndThreeSlots,
            LaboratoryScopeRestoresAfterNativeFailure,
            LaboratoryCountingFailureLeavesAllCachesUntouched,
            LaboratorySetterFailureRollsBackAllAppliedValues,
            LaboratoryRestoreAttemptsEveryCell,
            DisablingAlchemyRestoresNativeCounters,
            BodyPartsRecognizeBothActionsAndAllSixTitles,
            BodyPartsRejectUnrelatedAndMismatchedDelegates,
            BodyPartsRejectUnsupportedCategories,
            BodyPartsAggregateOnlyTheirCategoryAndRetainVariants,
            BodyPartsKeepBlockedItemsVisibleAndRevalidateSelection,
            BodyPartsRejectInstanceDataIncludingDisabledParts,
            BodyPartsResolveCurrentRealSourcesForUidConsumption,
            BodyPartsShareOneIndependentLiveToggle,
            EmbalmingRecognizesOnlyItsExactActionAndOwner,
            EmbalmingConsolidatesFluidsIncludingBlockedVariants,
            EmbalmingAvailabilityChangesRevalidateWithoutStockChanges,
            EmbalmingRejectsInstanceDataIncludingDisabledFluids,
            EmbalmingResolvesRealSurvivingSourceAndLiveToggle,
            GraveDecorationsRecognizeBothTitlesAndSharedToggle,
            GraveDecorationsRejectUnrelatedContextsAndCategories,
            GraveDecorationsAggregateOnlyTheirGroupAndKeepVariants,
            GraveDecorationsPreserveNativeAvailabilityAndRefresh,
            GraveDecorationsRejectInstanceDataIncludingDisabledItems,
            GraveDecorationsResolveRealSourcesWithoutChangingDefinitions
        };
        foreach (var check in checks)
        {
            check();
            Console.WriteLine("PASS " + check.Method.Name);
        }
        Console.WriteLine(checks.Length + " checks passed (pure logic; no game assemblies executed).");
        return 0;
    }

    private static void CombinesContainersWithoutChangingSources()
    {
        var backpack = new object();
        var bag = new object();
        var chest = new object();
        var a = new Stack("carrot-seed", 20);
        var b = new Stack("carrot-seed", 35);
        var c = new Stack("carrot-seed", 50);
        var result = StackAggregation.Build(new[] { Source(backpack, a), Source(bag, b), Source(chest, c) });
        Assert(result.Count == 1 && result[0].Total == 105, "Totals must exceed individual stack limits.");
        Assert(result[0].Sources.Count == 3 && ReferenceEquals(result[0].Representative, a), "Retain real source references.");
        Assert(a.Count == 20 && b.Count == 35 && c.Count == 50, "Aggregation must not mutate stored quantities.");
        Assert(ReferenceEquals(result[0].Sources[1].Container, bag), "Preserve bag ownership.");
    }

    private static void KeepsQualityDefinitionsSeparate()
    {
        var owner = new object();
        var result = StackAggregation.Build(new[]
        {
            Source(owner, new Stack("seed_bronze", 10)),
            Source(owner, new Stack("seed_gold", 20))
        });
        Assert(result.Count == 2 && result[0].Total == 10 && result[1].Total == 20,
            "Different full definition IDs must remain separate.");
    }

    private static void DoesNotCountAliasedStacksTwice()
    {
        var seed = new Stack("seed", 20);
        var result = StackAggregation.Build(new[] { Source(new object(), seed), Source(new object(), seed) });
        Assert(result[0].Total == 20 && result[0].Sources.Count == 1, "Multiple wrappers must not multiply a physical stack.");
    }

    private static void RejectsQuantityOverflow()
    {
        bool rejected = false;
        try
        {
            StackAggregation.Build(new[]
            {
                Source(new object(), new Stack("seed", int.MaxValue)),
                Source(new object(), new Stack("seed", 1))
            });
        }
        catch (OverflowException) { rejected = true; }
        Assert(rejected, "An unsupported total must trigger fallback rather than wrap into a negative count.");
    }

    private static void DetectsSourceChangesWithTheSameTotal()
    {
        var owner = new object();
        var a = new Stack("seed", 20);
        var b = new Stack("seed", 30);
        var before = StackAggregation.Build(new[] { Source(owner, a), Source(owner, b) });
        a.Count = 25;
        b.Count = 25;
        var redistributed = StackAggregation.Build(new[] { Source(owner, a), Source(owner, b) });
        Assert(!StackAggregation.SameSnapshot(before, redistributed), "Changed source quantities require a fresh snapshot.");
        var unchanged = StackAggregation.Build(new[] { Source(owner, a), Source(owner, b) });
        Assert(StackAggregation.SameSnapshot(redistributed, unchanged), "Unchanged data must avoid needless UI refreshes.");
        var moved = StackAggregation.Build(new[] { Source(new object(), a), Source(owner, b) });
        Assert(!StackAggregation.SameSnapshot(unchanged, moved), "A move with the same quantity changes ownership.");
        var replaced = StackAggregation.Build(new[] { Source(owner, new Stack("seed", 25)), Source(owner, b) });
        Assert(!StackAggregation.SameSnapshot(unchanged, replaced), "A replacement stack must not reuse a stale item reference.");
    }

    private static void SupportsEmptyAndDepletedInventories()
    {
        Assert(StackAggregation.Build(Array.Empty<StackSource<Stack>>()).Count == 0, "Empty results must be supported.");
        var result = StackAggregation.Build(new[]
        {
            Source(new object(), new Stack("seed", 0)), Source(new object(), new Stack("seed", -1))
        });
        Assert(result.Count == 0, "Depleted stacks must not create entries.");
    }

    private static void ProducesStableOrdering()
    {
        var owner = new object();
        var result = StackAggregation.Build(new[]
        {
            Source(owner, new Stack("z", 1), 2), Source(owner, new Stack("b", 1), 1),
            Source(owner, new Stack("a", 1), 1)
        });
        Assert(result[0].Key == "a" && result[1].Key == "b" && result[2].Key == "z",
            "Use definition sort order followed by the full ID.");
    }

    private static void RecognizesOnlyTheExactActionAndOwner()
    {
        var garden = new FakePicker();
        var otherGarden = new FakePicker();
        Action<object> seed = garden.SelectSeed;
        Func<object, bool> validSeed = garden.IsSeedValid;
        Assert(DelegateContext.Matches(seed, validSeed, seed.Method, validSeed.Method), "Accept the registered seed action.");
        Assert(!DelegateContext.Matches((Action<object>)garden.SelectFertilizer, validSeed, seed.Method, validSeed.Method),
            "Reject other actions on the same garden window.");
        Assert(!DelegateContext.Matches(seed, (Func<object, bool>)otherGarden.IsSeedValid, seed.Method, validSeed.Method),
            "Reject delegates bound to different window instances.");
        Action<object> combined = seed + garden.SelectFertilizer;
        Assert(!DelegateContext.Matches(combined, validSeed, seed.Method, validSeed.Method), "Reject multicast callbacks.");
        Action<object> wrapper = value => seed(value);
        Assert(!DelegateContext.Matches(wrapper, validSeed, seed.Method, validSeed.Method), "Unknown wrappers must use vanilla.");
        Assert(!DelegateContext.Matches(null, validSeed, seed.Method, validSeed.Method), "Missing callbacks must use vanilla.");
    }

    private static void FertilizersIncludeAppliedItemsButExcludeOtherItems()
    {
        var applied = new Stack("fertilizer_applied", 20, fertilizer: true);
        var appliedInBag = new Stack("fertilizer_applied", 35, fertilizer: true);
        var usable = new Stack("fertilizer_usable", 50, fertilizer: true);
        var irrelevant = new Stack("stone", 99);
        var policy = new PickerPolicy<Stack>("Fertilizers", () => true,
            item => item.IsFertilizer, item => item.IsFertilizer && item.Id != applied.Id, _ => { });
        var snapshot = PickerSnapshot<Stack>.Build(new[]
        {
            Source(new object(), applied), Source(new object(), appliedInBag),
            Source(new object(), usable), Source(new object(), irrelevant)
        }, policy);
        Assert(snapshot.Groups.Count == 2, "Only fertilizers should appear, including applied fertilizers.");
        Assert(snapshot.Groups[0].Key == applied.Id && snapshot.Groups[0].Total == 55,
            "Unavailable fertilizer stacks must be combined and retain their total.");
        Assert(!snapshot.TryResolveSelectable(applied.Id, out _), "Applied fertilizer must remain unselectable.");
        Assert(snapshot.TryResolveSelectable(usable.Id, out var selected) && ReferenceEquals(selected, usable),
            "Usable fertilizer must resolve to a real owned item.");
        Assert(applied.Count == 20 && appliedInBag.Count == 35 && usable.Count == 50 && irrelevant.Count == 99,
            "Neither enabled nor disabled display entries may change source quantities.");
    }

    private static void SeedsStillExcludeUnavailableItems()
    {
        var owner = new object();
        var valid = new Stack("seed_valid", 10);
        var invalid = new Stack("seed_invalid", 20);
        Func<Stack, bool> nativeSeedPredicate = item => item.Id == valid.Id;
        var policy = new PickerPolicy<Stack>("Seeds", () => true,
            nativeSeedPredicate, nativeSeedPredicate, _ => { });
        var snapshot = PickerSnapshot<Stack>.Build(new[] { Source(owner, valid), Source(owner, invalid) }, policy);
        Assert(snapshot.Groups.Count == 1 && snapshot.Groups[0].Key == valid.Id,
            "The existing seed mode must continue to hide items rejected by its original predicate.");
        Assert(snapshot.TryResolveSelectable(valid.Id, out var item) && ReferenceEquals(item, valid),
            "Seed selection must retain its original real representative.");
    }

    private static void AvailabilityChangesInvalidateSnapshotsWithoutStockChanges()
    {
        var fertilizer = new Stack("fertilizer", 10, fertilizer: true);
        var sources = new[] { Source(new object(), fertilizer) };
        bool applied = true;
        var policy = new PickerPolicy<Stack>("Fertilizers", () => true,
            item => item.IsFertilizer, _ => !applied, _ => { });
        var disabled = PickerSnapshot<Stack>.Build(sources, policy);
        var unchanged = PickerSnapshot<Stack>.Build(sources, policy);
        Assert(disabled.SameAs(unchanged), "Unchanged availability must not force a redraw.");
        applied = false;
        var enabled = PickerSnapshot<Stack>.Build(sources, policy);
        Assert(StackAggregation.SameSnapshot(disabled.Groups, enabled.Groups), "The stock remains unchanged.");
        Assert(!disabled.SameAs(enabled), "Re-enabling must trigger a full redraw, including native interaction flags.");
        Assert(enabled.TryResolveSelectable(fertilizer.Id, out _), "A newly available fertilizer must resolve.");
        applied = true;
        var disabledAgain = PickerSnapshot<Stack>.Build(sources, policy);
        Assert(!enabled.SameAs(disabledAgain) && !disabledAgain.TryResolveSelectable(fertilizer.Id, out _),
            "Disabling must also trigger a redraw and block resolution.");
    }

    private static void BlockedFertilizerCannotResolveAfterBedChanges()
    {
        var fertilizer = new Stack("fertilizer", 10, fertilizer: true);
        var sources = new[] { Source(new object(), fertilizer) };
        bool canApply = true;
        var policy = new PickerPolicy<Stack>("Fertilizers", () => true,
            item => item.IsFertilizer, _ => canApply, _ => { });
        var displayed = PickerSnapshot<Stack>.Build(sources, policy);
        Assert(displayed.TryResolveSelectable(fertilizer.Id, out _), "The displayed snapshot starts selectable.");
        canApply = false;
        var fresh = PickerSnapshot<Stack>.Build(sources, policy);
        Assert(!fresh.TryResolveSelectable(fertilizer.Id, out var blocked) && blocked == null,
            "The click-time snapshot must reject a previously enabled fertilizer after the bed changes.");
        Assert(fresh.Groups.Count == 1 && fertilizer.Count == 10, "Rejected selection must preserve visibility and stock.");
    }

    private static void ActionSettingsRemainIndependent()
    {
        var settings = new bool[6];
        var policies = new[]
        {
            new PickerPolicy<Stack>("Seeds", () => settings[0], _ => true, _ => true, _ => { }),
            new PickerPolicy<Stack>("Fertilizers", () => settings[1], _ => true, _ => true, _ => { }),
            new PickerPolicy<Stack>("Alchemical Ingredients", () => settings[2], _ => true, _ => true, _ => { }),
            new PickerPolicy<Stack>("Brains", () => settings[3], _ => true, _ => true, _ => { }),
            new PickerPolicy<Stack>("Embalming Fluids", () => settings[4], _ => true, _ => true, _ => { }),
            new PickerPolicy<Stack>("Tombstones", () => settings[5], _ => true, _ => true, _ => { })
        };
        for (int combination = 0; combination < (1 << settings.Length); combination++)
        {
            for (int i = 0; i < settings.Length; i++)
                settings[i] = (combination & (1 << i)) != 0;
            for (int i = 0; i < policies.Length; i++)
            {
                Assert(policies[i].IsEnabled == ((combination & (1 << i)) != 0),
                    "Each action must read its own live setting in all sixty-four combinations.");
            }
        }
    }

    private static void FertilizerContextAcceptsSlotClosuresOnlyWithItsExactPredicate()
    {
        var garden = new FakePicker();
        var slot = new object();
        Action<object> callback = value => garden.ApplyFertilizer(value, slot);
        Func<object, bool> predicate = garden.IsFertilizerValid;
        Assert(!ReferenceEquals(callback.Target, predicate.Target), "The native callback captures a separate slot closure.");
        Assert(DelegateContext.MatchesPredicate(callback, predicate, predicate.Method), "Recognize the exact fertilizer predicate.");
        Assert(!DelegateContext.Matches(callback, predicate, ((Action<object>)garden.SelectSeed).Method,
            ((Func<object, bool>)garden.IsSeedValid).Method), "The seed matcher must not broaden to fertilizer closures.");
        Assert(!DelegateContext.MatchesPredicate(callback, (Func<object, bool>)garden.IsSeedValid, predicate.Method),
            "Seed and other predicates must not enter fertilizer mode.");
        Assert(!DelegateContext.MatchesPredicate(callback + garden.SelectSeed, predicate, predicate.Method),
            "Reject composite fertilizer callbacks.");
        Assert(!DelegateContext.MatchesPredicate(callback, predicate + garden.IsSeedValid, predicate.Method),
            "Reject composite predicates.");
        Assert(!DelegateContext.MatchesPredicate(callback, (Func<object, bool>)(value => predicate(value)), predicate.Method),
            "Unknown predicate wrappers must preserve vanilla.");
        Assert(!DelegateContext.MatchesPredicate(null, predicate, predicate.Method), "Reject missing callbacks.");
        Assert(!DelegateContext.MatchesPredicate((Action<object>)FakePicker.StaticSelect, predicate, predicate.Method),
            "Reject unexpected static callbacks.");
    }

    private static void ValidatesUnavailableFertilizerInstances()
    {
        var invalidFertilizer = new Stack("fertilizer", 10, fertilizer: true);
        var policy = new PickerPolicy<Stack>("Fertilizers", () => true,
            item => item.IsFertilizer, _ => false,
            _ => throw new NotSupportedException("Unsupported instance properties"));
        bool rejected = false;
        try
        {
            PickerSnapshot<Stack>.Build(new[] { Source(new object(), invalidFertilizer) }, policy);
        }
        catch (NotSupportedException) { rejected = true; }
        Assert(rejected, "Unavailable fertilizers must receive the same instance-data safety checks as selectable items.");
        var nonFertilizer = new Stack("stone", 10);
        var irrelevant = PickerSnapshot<Stack>.Build(new[] { Source(new object(), nonFertilizer) }, policy);
        Assert(irrelevant.Groups.Count == 0, "Unrelated items must not be validated as fertilizer or cause fallback.");
    }

    private static StackSource<Stack> Source(object owner, Stack stack, int order = 0)
        => new StackSource<Stack>(owner, stack, stack.Id, stack.Count, order);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Stack
    {
        public string Id { get; }
        public int Count { get; set; }
        public bool IsFertilizer { get; }
        public Stack(string id, int count, bool fertilizer = false) { Id = id; Count = count; IsFertilizer = fertilizer; }
    }

    private sealed class FakePicker
    {
        public void SelectSeed(object value) { }
        public void SelectFertilizer(object value) { }
        public bool IsSeedValid(object value) => true;
        public bool IsFertilizerValid(object value) => true;
        public void ApplyFertilizer(object value, object slot) { }
        public static void StaticSelect(object value) { }
    }
}
