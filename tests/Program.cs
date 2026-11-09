using System;
using System.Collections.Generic;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Checks;

internal static class Program
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
            RecognizesOnlyTheExactActionAndOwner
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
        public Stack(string id, int count) { Id = id; Count = count; }
    }

    private sealed class FakePicker
    {
        public void SelectSeed(object value) { }
        public void SelectFertilizer(object value) { }
        public bool IsSeedValid(object value) => true;
    }
}
