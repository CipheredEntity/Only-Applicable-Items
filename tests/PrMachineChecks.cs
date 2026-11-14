using System;
using System.Collections.Generic;
using System.Linq;
using LazyBearTechnology;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static void PrAccessorsInitializeBeforeGameBalanceLoads() => PrColdStartCheck.Run();

    // Deliberately reproduces the observed native UID helper's boolean contract,
    // not a Unity inventory simulation. A nonempty partial removal returns true.
    private static bool PrUnsafeRemove(List<int> stacks, int index, int requested)
    {
        int removed = Math.Min(stacks[index], requested);
        stacks[index] -= removed;
        return removed > 0;
    }

    private static void PrUidSuccessMustMeanCompleteConsumption()
    {
        var stacks = new List<int> { 5 };
        int before = stacks.Sum();
        bool success = PrUnsafeRemove(stacks, 0, 20);
        Assert(success && before - stacks.Sum() == 5, "Retain the reproduction of the native partial-success hazard.");
        var item = ShopItem("hypno", 5);
        var sources = SessionSources(item);
        bool rejected = false;
        try { PrStock(sources).Prepare(item.id, 20); } catch (InvalidOperationException) { rejected = true; }
        Assert(rejected && item.Count == 5 && sources.inventoryList[0].Data.Inventory.Contains(item),
            "The production planner rejects insufficient stock before any removal, rather than trusting a native boolean.");
    }

    private static void PrPrevalidationMustNotExposePartialConsumption()
    {
        var stacks = new List<int> { 10, 10 };
        int consumed = 0;
        bool effect = false;
        // The entire request was available at prevalidation. The first native
        // inventory notification can invalidate the second removal synchronously.
        if (stacks.Sum() >= 20)
        {
            PrUnsafeRemove(stacks, 0, 10); consumed += 10;
            stacks[1] = 0;
            if (PrUnsafeRemove(stacks, 1, 10)) { consumed += 10; effect = true; }
        }
        Assert(consumed == 10 && !effect, "Retain the reproduction of the naive notifying-loop hazard.");
        var a = ShopItem("hypno", 10); var b = ShopItem("hypno", 10);
        var source = SessionSources(a); var second = new Inventory("chest", 8); source.inventoryList.Add(second); second.Data.Inventory.Add(b);
        var plan = PrStock(source).Prepare(a.id, 20);
        bool effectsDone = false, completeAtNotification = false; int notifications = 0;
        source.inventoryList[0].OnItemsRemove += _ =>
        {
            notifications++;
            completeAtNotification = effectsDone && second.Data.Inventory.Count == 0;
            b.Count = 0; // This item has already been removed from storage.
            Assert(!plan.TryCommit(), "An observer cannot replay the committed plan.");
        };
        Assert(plan.TryCommit(), "Commit both sources without an interleaved event.");
        effectsDone = true; plan.Notify(_ => throw new Exception("Unexpected notification failure"));
        Assert(completeAtNotification && notifications == 1 && source.inventoryList[0].Data.Inventory.Count == 0 && second.Data.Inventory.Count == 0,
            "Consume exactly the full request before notifying, with no rollback or partial loss.");
    }

    private static PrMachineStock PrStock(MultiInventory source, Func<Item, bool>? eligible = null)
        => new(new PrItemFields(), source, eligible ?? (item => item.id.StartsWith("hypno", StringComparison.Ordinal)), new PlainItemValidator().Validate);

    private sealed class PrFixture : IDisposable
    {
        internal readonly UIPanicReductionMachineWindowData Owner;
        internal readonly PrMachineFeatures Feature;
        internal readonly SessionFixture Picker;
        internal readonly List<string> Warnings = new();
        internal readonly MultiInventory Sources;
        internal bool Enabled = true;
        internal Action? BeforeCommit;
        internal bool ThrowEffects;
        internal UIItemCountWindow Dialog => LazyUI.GetWindow<UIItemCountWindow>();
        internal PrFixture(MultiInventory sources, params string[] ids)
        {
            LazyUI.ResetForCheck(); Sources = sources;
            Owner = new UIPanicReductionMachineWindowData(sources, ids);
            LazyUI.GetWindow<UIPanicReductionMachineWindow>().Open(Owner);
            var contract = new PrMachineContract();
            Feature = new PrMachineFeatures(contract, new PrItemFields(), () => Enabled, new PlainItemValidator().Validate,
                (op, _) => Warnings.Add(op), () => { }, (owner, cell, carrier, plan) =>
                {
                    BeforeCommit?.Invoke();
                    if (!plan.TryCommit()) return;
                    try
                    {
                        owner.Effects++; owner.Days += carrier.Count * UIPanicReductionMachineWindowData.Definitions[carrier.id].dayIncreasePerItem;
                        owner.Expressions++; owner.Redraws++; owner.AfterEffect?.Invoke();
                        if (ThrowEffects) throw new InvalidOperationException("native effect failed");
                    }
                    finally { plan.Notify(_ => Warnings.Add("notification")); }
                });
            var original = new UIMultiInventoryWindowData(sources, Owner.Callback, Owner.Predicate);
            var policy = Feature.GetPolicy(original, Owner.Callback, Owner.Predicate)!;
            Picker = new SessionFixture(new[] { policy }, sources, Owner.Predicate, Owner.Callback);
            Feature.Bind(Picker.Session, Owner.Predicate);
            Picker.Plugin.PrMachine = Feature; Plugin.Instance = Picker.Plugin;
            Picker.Open();
        }
        internal UIItemCell Cell(string id) => Picker.Window.Widget.DrawnInventories[0].Cells.Single(c => c.DisplayingItem?.id == id);
        internal void Click(string id) { var cell = Cell(id); cell.OnItemCellPress!(cell); }
        internal void Tick() { UnityEngine.Time.unscaledTime += 1; Feature.Tick(); Picker.Session.Tick(); }
        public void Dispose() { Feature.Dispose(); Picker.Dispose(); Plugin.Instance = null; LazyUI.ResetForCheck(); }
    }

    private static void PrAggregatesBackpackBagsAndStorageAndLoadsAcrossStacks()
    {
        var a = ShopItem("hypno", 20); var b = ShopItem("hypno", 35); var c = ShopItem("hypno", 50);
        var bag = ShopBag(b); var news = ShopItem("newspaper", 7); var other = ShopItem("irrelevant", 4);
        var source = SessionSources(a, bag, news, other); var chest = new Inventory("chest", 8);
        chest.Data.Inventory.Add(c); source.inventoryList.Add(chest);
        using var f = new PrFixture(source, a.id, news.id);
        Assert(f.Cell(a.id).Value == 105 && f.Picker.View(0).Groups.Count == 2 && f.Picker.View(0).Policy.Title == "PR Resources",
            "Use one native-eligible shared view with true totals and the requested default title.");
        f.Click(a.id);
        Assert(f.Owner.NativeStackMaximum == 20 && f.Dialog.Data!.Max == 105 && !f.Dialog.Data.IsForVendor,
            "Extend the native non-vendor dialog beyond its real representative stack.");
        f.Dialog.Confirm(80);
        Assert(a.Count == 20 && !source.inventoryList[0].Data.Inventory.Contains(a) && bag.Inventory.Count == 0 && c.Count == 25 &&
            source.inventoryList[0].Data.Inventory.Contains(news) && other.Count == 4 && f.Owner.Days == 160 && f.Owner.Expressions == 1,
            "Consume 20+35+25 across real sources; preserve detached full-stack references, the partial UID, unrelated items, and one effect.");
        Assert(bag.InventoryFillSize == 0 && chest.Data.InventoryFillSize == 1, "Refresh the consumed containers' occupancy caches.");
    }

    private static void PrFullExhaustionAndPartialStackPreserveIdentity()
    {
        var a = ShopItem("hypno", 10); var b = ShopItem("hypno", 15); var source = SessionSources(a, b);
        var uid = a.UniqueId;
        var first = PrStock(source).Prepare(a.id, 3);
        Assert(first.TryCommit() && a.Count == 7 && a.UniqueId == uid && source.inventoryList[0].Data.Inventory.Count == 2,
            "A partial load changes only the real source count.");
        var rest = PrStock(source).Prepare(a.id, 22);
        Assert(rest.TryCommit() && source.inventoryList[0].Data.Inventory.Count == 0 && !rest.TryCommit(), "Exhaust several stacks once.");
    }

    private static void PrRejectsStaleOrInvalidPlansWithoutPartialWrites()
    {
        foreach (int change in new[] { 0, 1, 2, 3 })
        {
            var a = ShopItem("hypno", 10); var b = ShopItem("hypno", 10); var source = SessionSources(a, b);
            var plan = PrStock(source).Prepare(a.id, 20);
            if (change == 0) b.Count = 9;
            if (change == 1) source.inventoryList[0].Data.Inventory.Reverse();
            if (change == 2) source.inventoryList[0].Data.Inventory.Remove(b);
            if (change == 3) source.inventoryList.Add(new Inventory("new storage", 4));
            Assert(!plan.TryCommit() && a.Count == 10 && source.inventoryList[0].Data.Inventory.Contains(a),
                "Final validation rejects changed counts, positions, membership or roots before the first write.");
        }
    }

    private static void PrPendingDialogsFollowTheSavedIdNotReusedCells()
    {
        var a = ShopItem("hypno", 10); var b = ShopItem("hypno", 20); var news = ShopItem("newspaper", 5);
        var source = SessionSources(a, news); var chest = new Inventory("chest", 4); chest.Data.Inventory.Add(b); source.inventoryList.Add(chest);
        using var f = new PrFixture(source, a.id, news.id);
        f.Click(a.id); var oldCell = f.Cell(a.id);
        source.inventoryList[0].Data.Inventory.Remove(a); chest.Data.Inventory.Add(a); // Current roots, new location.
        f.Tick(); oldCell.Draw(news); // Model a pooled cell rebound to a different item.
        f.Dialog.Confirm(25);
        Assert(f.Owner.Effects == 1 && f.Owner.Days == 50 && news.Count == 5 &&
            source.inventoryList.Sum(i => i.Data.GetTotalCountInInventory(a.id)) == 5,
            "Use fresh real sources for the saved full ID, even when the former source cell now displays something else.");
    }

    private static void PrPendingDialogsRejectInsufficientStockAndRepeatedConfirmation()
    {
        var a = ShopItem("hypno", 20); var b = ShopItem("hypno", 20); var source = SessionSources(a, b);
        using var f = new PrFixture(source, a.id);
        f.Click(a.id); var callback = f.Dialog.Data!.OnConfirm!;
        b.Count = 5; f.Dialog.Confirm(30); callback(20);
        Assert(f.Owner.Effects == 0 && a.Count == 20 && b.Count == 5, "An invalidated request neither partially loads nor authorizes a retry.");
        f.Click(a.id); var repeat = f.Dialog.Data!.OnConfirm!; f.Dialog.Confirm(22); repeat(22);
        Assert(f.Owner.Effects == 1 && a.Count == 20 && !source.inventoryList[0].Data.Inventory.Contains(a) && b.Count == 3,
            "A successful confirmation consumes its request exactly once.");
    }

    private static void PrCancellationReplacementAndUnrelatedDialogsStaySafe()
    {
        using var f = new PrFixture(SessionSources(ShopItem("hypno", 20)), "hypno");
        f.Click("hypno"); var expired = f.Dialog.Data!.OnConfirm!; f.Dialog.IsShown = false; f.Tick(); expired(10);
        Assert(f.Owner.Effects == 0, "Cancelled dialogs cannot consume later.");
        f.Click("hypno"); expired = f.Dialog.Data!.OnConfirm!;
        int unrelated = 0;
        f.Dialog.Open(new UIItemCountWindowData { Item = new Item("other"), Min = 1, Max = 4, OnConfirm = n => unrelated += n });
        expired(10); f.Dialog.Confirm(3);
        Assert(f.Owner.Effects == 0 && unrelated == 3 && f.Dialog.Data!.Max == 4, "Unrelated dialogs retain their original bounds and callback.");
        f.Click("hypno"); expired = f.Dialog.Data!.OnConfirm!;
        f.Dialog.Data = new UIItemCountWindowData { Item = new Item("foreign dialog"), Min = 1, Max = 3, OnConfirm = n => unrelated += n };
        expired(10); f.Feature.Tick();
        Assert(f.Owner.Effects == 0 && f.Sources.inventoryList[0].Data.GetTotalCountInInventory("hypno") == 20,
            "Foreign quantity-dialog data invalidates a saved confirmation without touching its new owner.");
    }

    private static void PrNotificationsCannotInterruptOrReplayEffects()
    {
        var a = ShopItem("hypno", 10); var b = ShopItem("hypno", 10); var source = SessionSources(a);
        var chest = new Inventory("chest", 4); chest.Data.Inventory.Add(b); source.inventoryList.Add(chest);
        using var f = new PrFixture(source, a.id);
        f.Click(a.id); var replay = f.Dialog.Data!.OnConfirm!; int observed = 0; bool completeAtNotification = false;
        source.inventoryList[0].OnItemsRemove += _ =>
        {
            observed++;
            completeAtNotification = chest.Data.Inventory.Count == 0 && f.Owner.Expressions == 1;
            replay(20); f.Feature.Dispose(); throw new InvalidOperationException("observer failure");
        };
        chest.OnItemsRemove += _ => observed++;
        f.Dialog.Confirm(20);
        Assert(completeAtNotification && f.Owner.Effects == 1 && f.Owner.Days == 40 && observed == 2 && f.Warnings.Contains("notification"),
            "A failing observer cannot stop another root's notification, duplicate effects, or interrupt consumption.");
    }

    private static void PrFailedCommitAndNativeEffectFailureAreNeverRetried()
    {
        var a = ShopItem("hypno", 10); var source = SessionSources(a);
        using var f = new PrFixture(source, a.id);
        f.Click(a.id); f.BeforeCommit = () => a.Count = 9; f.Dialog.Confirm(10);
        Assert(a.Count == 9 && f.Owner.Effects == 0, "Changes at the final native entry reject the whole prepared load.");
        f.BeforeCommit = null; f.Click(a.id); var callback = f.Dialog.Data!.OnConfirm!; f.ThrowEffects = true;
        bool failed = false; try { f.Dialog.Confirm(5); } catch (InvalidOperationException) { failed = true; }
        callback(5);
        Assert(failed && a.Count == 4 && f.Owner.Effects == 1, "Native post-consumption exceptions propagate without rollback or replay.");
    }

    private static void PrToggleForeignOwnershipAndSourceChangesInvalidateRequests()
    {
        for (int scenario = 0; scenario < 5; scenario++)
        {
            var a = ShopItem("hypno", 10); var source = SessionSources(a);
            using var f = new PrFixture(source, a.id);
            f.Click(a.id);
            if (scenario == 0) { f.Enabled = false; f.Tick(); }
            if (scenario == 1) f.Owner.ChangeSources(SessionSources(a));
            if (scenario == 2) { f.Picker.Container.inventoriesData.Clear(); f.Picker.Container.inventoriesData.Add(new InventoryWidgetDataBase()); }
            if (scenario == 3) LazyUI.GetWindow<UIPanicReductionMachineWindow>().Open(new UIPanicReductionMachineWindowData(source, a.id));
            if (scenario == 4) MainGame.PlayerData = new PlayerData();
            f.Dialog.Confirm(10);
            Assert(a.Count == 10 && f.Owner.Effects == 0, "Disabled, foreign, reopened and replaced native contexts cannot authorize old requests.");
        }
    }

    private static void PrEligibilityVariantsSafetyAndConfigurationRemainIsolated()
    {
        var contract = new PrMachineContract(); var sources = SessionSources(ShopItem("hypno:1", 10), ShopItem("hypno:2", 8));
        var owner = new UIPanicReductionMachineWindowData(sources, "hypno:1", "hypno:2");
        Assert(contract.Matches(owner.Callback, owner.Predicate) && !contract.Matches(_ => { }, owner.Predicate), "Require exact native delegates.");
        var stock = PrStock(sources);
        Assert(stock.Total("hypno:1") == 10 && stock.Total("hypno:2") == 8, "Full definitions/qualities are never combined.");
        sources.inventoryList[0].Data.Inventory[1].AddTestProperty(); bool rejected = false;
        try { PrStock(sources); } catch (NotSupportedException) { rejected = true; }
        Assert(rejected, "Unexpected instance data rejects consolidation before consuming anything.");
        var config = new BepInEx.Configuration.ConfigFile(); var settings = new OnlyApplicableItems.Configuration.ActionSettings(config);
        var names = new OnlyApplicableItems.Configuration.InventoryNameSettings(config);
        settings.PrMachine.Value = false;
        NameEntry(config, PickerId.PrMachineResources).Value = "My PR supplies";
        Assert(settings.Seeds.Value && settings.ShopSelling.Value && !settings.PrMachine.Value && names.GetTitle(PickerId.PrMachineResources) == "My PR supplies",
            "The new default-on toggle and blank-default name stay independent of every existing action.");
    }

    private static void PrFinalValidationRejectsPropertiesAndOffThreadConsumption()
    {
        for (int scenario = 0; scenario < 4; scenario++)
        {
            var a = ShopItem("hypno", 10); var source = SessionSources(a);
            var plan = PrStock(source).Prepare(a.id, 10);
            bool committed;
            if (scenario == 0) a.AddTestProperty();
            if (scenario == 1) a.InventorySize = 1;
            if (scenario == 2) source.inventoryList[0].Data.AddTestProperty();
            committed = scenario == 3 ? System.Threading.Tasks.Task.Run(plan.TryCommit).GetAwaiter().GetResult() : plan.TryCommit();
            Assert(!committed && a.Count == 10 && source.inventoryList[0].Data.Inventory.Contains(a),
                "Properties, item capacity, container metadata, and thread changes fail before destructive writes.");
        }
    }

    private static void PrCannotReachNestedBagsOrAmbiguousSources()
    {
        var visible = ShopItem("hypno", 10); var hidden = ShopItem("hypno", 20);
        var outer = ShopBag(ShopBag(hidden)); var sources = SessionSources(visible, outer);
        var stock = PrStock(sources);
        Assert(stock.Total(visible.id) == 10 && stock.Prepare(visible.id, 10).TryCommit() && hidden.Count == 20,
            "The consumption plan cannot reach deeper bags excluded by the native picker.");
        var alias = ShopItem("hypno", 5); sources = SessionSources(alias, ShopBag(alias));
        bool rejected = false; try { PrStock(sources); } catch (NotSupportedException) { rejected = true; }
        Assert(rejected && alias.Count == 5, "Ambiguous ownership fails before modifying any source.");
    }

    private static void PrUnsupportedDialogsAndChangedEligibilityKeepStockIntact()
    {
        using var f = new PrFixture(SessionSources(ShopItem("hypno", 10)), "hypno");
        f.Dialog.BeforeOpenForCheck = data => data.IsForVendor = true;
        f.Click("hypno");
        Assert(!f.Dialog.IsShown && f.Owner.Effects == 0 && f.Warnings.Count == 1,
            "An unexpected native dialog contract requests vanilla fallback without opening an unsafe dialog.");
        f.Picker.Session.Tick();
        Assert(f.Picker.Container.inventoriesData[0] is not ApplicableInventoryWidgetData, "Restore the owned vanilla picker on fallback.");

        var a = ShopItem("hypno", 10);
        using var second = new PrFixture(SessionSources(a), a.id);
        second.Click(a.id); second.Owner.Allowed.Clear(); second.Dialog.Confirm(10);
        Assert(a.Count == 10 && second.Owner.Effects == 0, "The native predicate is reevaluated at confirmation.");
    }

    private static void PrAllQuantityBoundariesConserveSuppliesAndApplyOneEffect()
    {
        foreach (int count in new[] { 1, 19, 20, 21, 54, 55, 56, 80, 105 })
        {
            var a = ShopItem("hypno", 20); var bag = ShopBag(ShopItem("hypno", 35));
            var source = SessionSources(a, bag); var chest = new Inventory("chest", 4);
            chest.Data.Inventory.Add(ShopItem("hypno", 50)); source.inventoryList.Add(chest);
            using var f = new PrFixture(source, a.id);
            f.Click(a.id); f.Dialog.Confirm(count);
            Assert(source.inventoryList.Sum(i => i.Data.GetTotalCountInInventory(a.id)) == 105 - count &&
                f.Owner.Days == count * 2 && f.Owner.Effects == 1 && f.Owner.Expressions == 1,
                "Every boundary, including the whole 105-unit total, consumes exactly the requested supplies and applies effects once.");
        }
    }
}
