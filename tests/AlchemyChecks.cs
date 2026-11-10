using System;
using System.Collections.Generic;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static void AlchemyPolicyPreservesResearchAndSlotPredicates()
    {
        var laboratory = new FakeLaboratory();
        var researched = new AlchemyIngredient("researched", 50, 2);
        var unknown = new AlchemyIngredient("unknown", 50, 1);
        var excessiveRunes = new AlchemyIngredient("excessive", 50, 4);
        var excessiveInStorage = new AlchemyIngredient("excessive", 35, 4);
        laboratory.Researched.Add(researched.Id);
        laboratory.Researched.Add(excessiveRunes.Id);
        laboratory.OtherRunes = 2;
        Func<AlchemyIngredient, bool> nativePredicate = laboratory.IsValid;
        var visibility = CreateAlchemyVisibility(laboratory);
        var policy = new PickerPolicy<AlchemyIngredient>("Alchemical Ingredients", () => true,
            visibility.CanDisplay, nativePredicate, _ => { });
        var sources = new[] { AlchemySource(researched), AlchemySource(unknown), AlchemySource(excessiveRunes), AlchemySource(excessiveInStorage) };
        var initial = PickerSnapshot<AlchemyIngredient>.Build(sources, policy);
        Assert(initial.Groups.Count == 2 && initial.Groups[0].Key == excessiveRunes.Id && initial.Groups[0].Total == 85,
            "Researched rune-blocked stacks must stay visible and retain their combined total.");
        Assert(initial.TryResolveSelectable(researched.Id, out _) && !initial.TryResolveSelectable(excessiveRunes.Id, out _),
            "The supplied native predicate must continue to prevent selection of rune-blocked ingredients.");
        Assert(!initial.TryResolveSelectable(unknown.Id, out _), "Unresearched items must stay hidden and unselectable.");
        laboratory.Researched.Add(unknown.Id);
        laboratory.OtherRunes = 0; // Opening a different/replacement slot can change the native context.
        var changed = PickerSnapshot<AlchemyIngredient>.Build(sources, policy);
        Assert(changed.Groups.Count == 3 && !initial.SameAs(changed), "Context changes must not reuse stale eligibility.");
        Assert(changed.TryResolveSelectable(excessiveRunes.Id, out _), "An ingredient can become selectable after replacing another slot.");
        Assert(excessiveRunes.Count == 50 && excessiveInStorage.Count == 35, "Disabled entries must not change stored quantities.");
    }

    private static void AlchemyAllowsRepeatedIngredientSelections()
    {
        var a = new AlchemyIngredient("ingredient", 50, 1);
        var b = new AlchemyIngredient("ingredient", 50, 1);
        var policy = new PickerPolicy<AlchemyIngredient>("Alchemical Ingredients", () => true, _ => true, _ => true, _ => { });
        var snapshot = PickerSnapshot<AlchemyIngredient>.Build(new[] { AlchemySource(a), AlchemySource(b) }, policy);
        Assert(snapshot.Groups.Count == 1 && snapshot.Groups[0].Total == 100, "Combine real stacks without stack-size caps.");
        Assert(snapshot.TryResolveSelectable(a.Id, out var first) && ReferenceEquals(first, a), "Resolve the first slot to a real item.");
        Assert(snapshot.TryResolveSelectable(a.Id, out var second) && ReferenceEquals(second, a), "Allow the same ingredient in another slot.");
        Assert(a.Count == 50 && b.Count == 50, "Selecting slot entries must not reserve or consume real stock.");
    }

    private static void AlchemyContextIsIndependentOfGardenPredicates()
    {
        var laboratory = new FakeLaboratory();
        var destinationSlot = new object();
        Action<object> callback = value => laboratory.Select(value, destinationSlot);
        Func<AlchemyIngredient, bool> predicate = laboratory.IsValid;
        Assert(DelegateContext.MatchesPredicate(callback, predicate, predicate.Method), "Accept the exact alchemy predicate and captured slot.");
        var garden = new FakePicker();
        Assert(!DelegateContext.MatchesPredicate(callback, (Func<object, bool>)garden.IsSeedValid, predicate.Method), "Reject seed predicates.");
        Assert(!DelegateContext.MatchesPredicate(callback, (Func<object, bool>)garden.IsFertilizerValid, predicate.Method), "Reject fertilizer predicates.");
        Func<AlchemyIngredient, bool> wrapper = item => predicate(item);
        Assert(!DelegateContext.MatchesPredicate(callback, wrapper, predicate.Method), "Reject unknown wrapped predicates.");
        Assert(!DelegateContext.MatchesPredicate(callback + garden.SelectSeed, predicate, predicate.Method), "Reject multicast callbacks.");
    }

    private static void LaboratoryTotalsAreSharedAcrossTwoAndThreeSlots()
    {
        foreach (int occurrences in new[] { 2, 3 })
        {
            var cells = new List<LabCell>();
            for (int i = 0; i < occurrences; i++)
                cells.Add(new LabCell("ingredient", 50));
            int countReads = 0;
            var scope = QuantityDisplayScope<LabCell>.Apply(cells, cell => cell.Id,
                _ => { countReads++; return 100; }, cell => cell.Owned, (cell, count) => cell.Owned = count);
            int batch = occurrences == 2 ? 50 : 33;
            foreach (var cell in cells)
                cell.DrawRequirement(batch * occurrences); // The native method supplies this multiplier.
            Assert(countReads == 1, "Read accessible stock once per ID, not once per repeated slot.");
            Assert(cells[0].Text == (occurrences == 2 ? "100/100" : "100/99") && !cells[0].Red,
                "Every repeated row must use the shared total and native combined requirement.");
            cells[0].DrawRequirement(102);
            Assert(cells[0].Text == "100/102" && cells[0].Red, "Insufficient stock must retain native warning behavior.");
            scope.Dispose();
            foreach (var cell in cells)
                Assert(cell.Owned == 50, "Temporary drawing must not permanently replace native slot caches.");
            Assert(cells[0].Text == "100/102", "Restoring caches must leave the rendered label intact.");
            scope.Dispose(); // Cleanup is idempotent.
        }

        var alias = new LabCell("ingredient", 50);
        using (QuantityDisplayScope<LabCell>.Apply(new[] { alias, alias }, cell => cell.Id, _ => 100,
            cell => cell.Owned, (cell, count) => cell.Owned = count))
            Assert(alias.Owned == 100, "Repeated cell references should receive one override.");
        Assert(alias.Owned == 50, "Aliased cells must restore the genuine original cache.");
    }

    private static void LaboratoryScopeRestoresAfterNativeFailure()
    {
        var cell = new LabCell("ingredient", 50);
        var originalFailure = new InvalidOperationException("native rendering failed");
        Exception? observed = null;
        try
        {
            using (QuantityDisplayScope<LabCell>.Apply(new[] { cell }, value => value.Id, _ => 100,
                value => value.Owned, (value, count) => value.Owned = count))
                throw originalFailure;
        }
        catch (Exception exception) { observed = exception; }
        Assert(ReferenceEquals(observed, originalFailure) && cell.Owned == 50,
            "Cleanup must restore counters without swallowing or replacing the native exception.");
    }

    private static void LaboratoryCountingFailureLeavesAllCachesUntouched()
    {
        var a = new LabCell("a", 20);
        var b = new LabCell("b", 30);
        bool rejected = false;
        try
        {
            QuantityDisplayScope<LabCell>.Apply(new[] { a, b }, cell => cell.Id,
                id => id == "a" ? 100 : throw new InvalidOperationException("counting failed"),
                cell => cell.Owned, (cell, count) => cell.Owned = count);
        }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected && a.Owned == 20 && b.Owned == 30, "Prepare all totals before changing any cells.");
    }

    private static void LaboratorySetterFailureRollsBackAllAppliedValues()
    {
        var a = new LabCell("a", 20);
        var b = new LabCell("b", 30);
        bool fail = true;
        var expected = new InvalidOperationException("setter failed after writing");
        Exception? observed = null;
        try
        {
            QuantityDisplayScope<LabCell>.Apply(new[] { a, b }, cell => cell.Id, _ => 100,
                cell => cell.Owned, (cell, count) =>
                {
                    cell.Owned = count;
                    if (ReferenceEquals(cell, b) && fail)
                    {
                        fail = false;
                        throw expected;
                    }
                });
        }
        catch (Exception exception) { observed = exception; }
        Assert(ReferenceEquals(observed, expected) && a.Owned == 20 && b.Owned == 30,
            "A partially applied override must roll back every written cell.");
    }

    private static void LaboratoryRestoreAttemptsEveryCell()
    {
        var a = new LabCell("a", 20);
        var b = new LabCell("b", 30);
        var scope = QuantityDisplayScope<LabCell>.Apply(new[] { a, b }, cell => cell.Id, _ => 100,
            cell => cell.Owned, (cell, count) =>
            {
                if (ReferenceEquals(cell, b) && count == 30)
                    throw new InvalidOperationException("one cache cannot be restored");
                cell.Owned = count;
            });
        bool failed = false;
        try { scope.Dispose(); }
        catch (InvalidOperationException) { failed = true; }
        Assert(failed && a.Owned == 20, "One failed restoration must not prevent cleanup of other cells.");
    }

    private static void DisablingAlchemyRestoresNativeCounters()
    {
        var cell = new LabCell("ingredient", 50);
        bool enabled = true;
        var policy = new PickerPolicy<LabCell>("Alchemical Ingredients", () => enabled, _ => true, _ => true, _ => { });
        using (policy.IsEnabled ? QuantityDisplayScope<LabCell>.Apply(new[] { cell }, value => value.Id,
            _ => 100, value => value.Owned, (value, count) => value.Owned = count) : null)
            cell.DrawRequirement(100);
        Assert(cell.Text == "100/100" && cell.Owned == 50, "Enabled mode corrects only the rendered counter.");
        enabled = false;
        using (policy.IsEnabled ? QuantityDisplayScope<LabCell>.Apply(new[] { cell }, value => value.Id,
            _ => 100, value => value.Owned, (value, count) => value.Owned = count) : null)
            cell.DrawRequirement(100);
        Assert(cell.Text == "50/100" && cell.Red, "Disabling must render the untouched original cache again.");
    }

    private static StackSource<AlchemyIngredient> AlchemySource(AlchemyIngredient item)
        => new StackSource<AlchemyIngredient>(item, item, item.Id, item.Count, 0);

    private sealed class AlchemyIngredient
    {
        public string Id { get; }
        public int Count { get; }
        public int Runes { get; }
        public int Green { get; }
        public int Blue { get; }
        public bool CanBeUsedInAlchemy { get; }
        public bool HasSurvey { get; }
        public AlchemyIngredient(string id, int count, int runes, bool canBeUsedInAlchemy = true,
            bool hasSurvey = true, int green = 0, int blue = 0)
        {
            Id = id; Count = count; Runes = runes; Green = green; Blue = blue;
            CanBeUsedInAlchemy = canBeUsedInAlchemy; HasSurvey = hasSurvey;
        }
    }

    private sealed class FakeLaboratory
    {
        public HashSet<string> Researched { get; } = new HashSet<string>();
        public int OtherRunes { get; set; }
        public bool IsValid(AlchemyIngredient item) => Researched.Contains(item.Id)
            && OtherRunes + item.Runes >= 0 && OtherRunes + item.Runes <= 5
            && item.Green >= 0 && item.Green <= 5 && item.Blue >= 0 && item.Blue <= 5;
        public void Select(object item, object slot) { }
    }

    private sealed class LabCell
    {
        public string Id { get; }
        public int Owned { get; set; }
        public string Text { get; private set; } = "";
        public bool Red { get; private set; }
        public LabCell(string id, int owned) { Id = id; Owned = owned; }
        public void DrawRequirement(int required) { Text = Owned + "/" + required; Red = Owned < required; }
    }
}
