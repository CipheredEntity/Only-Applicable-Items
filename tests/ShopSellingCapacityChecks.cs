using System;
using System.Linq;
using OnlyApplicableItems.Integration;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static Item[] Carrots350() => Enumerable.Range(0, 7).Select(_ => ShopItem("carrot", 50)).ToArray();

    private static void ShopCapacityFixtureReproducesNativeMinusOneLossWhenDisabled()
    {
        using var f = new ShopFixture(Carrots350(), Merchant(new[] { "carrot" }), enabled: false);
        Assert(f.Trade.Sell.Data.CachedFillForCheck == -1 && f.Trade.Sell.Data.CanAddItemCountToInventory(f.Root.Data.Inventory[0].Definition, 350) == 350,
            "Fresh native capacity incorrectly budgets a seventh stack from the raw -1 cache.");
        var original = f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0];
        original.OnItemCellPress!(f.Window.PlayerWidget.DrawnInventories[0].Cells.First(cell => cell.DisplayingItem?.id == "carrot"));
        Assert(f.Dialog.IsShown && f.Dialog.Data!.Max == 350 && f.Trade.Sell.Data.CachedFillForCheck == -1,
            "Disabled Shop Selling must leave the native bad dialog maximum and cache untouched.");
        f.Dialog.Confirm(350);
        Assert(f.Trade.NativeAdded == 300 && f.Trade.NativeRemoved == 350 && f.Root.Data.GetTotalCountInInventory("carrot") == 0,
            "The fixture must preserve native split-before-insert loss rather than silently fixing its add routine.");
        f.Trade.CancelForCheck();
        Assert(f.Root.Data.GetTotalCountInInventory("carrot") == 300 && f.Trade.CancelCalls == 1,
            "Only 300 were staged and can be returned; the original 50-unit loss predates cancellation.");
    }

    private static void ShopCapacityWarmsFreshCacheAndCapsTheDialogAt300()
    {
        using var f = new ShopFixture(Carrots350(), Merchant(new[] { "carrot" }));
        Assert(f.Trade.Sell.Data.CachedFillForCheck == -1, "Opening the consolidated panel alone must not globally warm native inventories.");
        f.Click("carrot");
        Assert(f.Trade.Sell.Data.CachedFillForCheck == 0 && f.Dialog.IsShown && f.Dialog.Data!.Max == 300,
            "Warm the real destination before the native opener computes its limit.");
        f.Dialog.Confirm(350); f.Tick();
        Assert(f.Trade.NativeMoves == 0 && f.Root.Data.GetTotalCountInInventory("carrot") == 350 && f.Trade.Sell.Data.Inventory.Count == 0,
            "Out-of-range callbacks must be rejected without calling the native transaction.");
        f.Click("carrot"); f.Dialog.Confirm(300); f.Tick();
        Assert(f.Trade.NativeAdded == 300 && f.Trade.NativeRemoved == 300 && f.Root.Data.GetTotalCountInInventory("carrot") == 50 &&
            f.Trade.Sell.Data.Inventory.Count == 6 && f.Trade.Sell.Data.Inventory.All(i => i.Count == 50),
            "A fully fitting request stages six ordinary stacks and leaves 50 in real source inventories.");
        f.Trade.CancelForCheck(); f.Tick();
        Assert(f.Root.Data.GetTotalCountInInventory("carrot") == 350 && f.Trade.Sell.Data.Inventory.Count == 0 && f.Trade.Sell.Data.CachedFillForCheck == 0,
            "Native cancellation returns all staged goods with total ownership conserved.");
        f.Click("carrot"); Assert(f.Dialog.Data!.Max == 300, "The native cleared-deal cache remains valid for the next offer.");
    }

    private static void ShopCapacityCountsPartialMatchingStacksAndMixedSlots()
    {
        var carrot = ShopItem("carrot", 1);
        var destination = new Inventory("inventory", 6);
        destination.Data.Inventory.Add(ShopItem("carrot", 40));
        destination.Data.Inventory.Add(ShopItem("potato", 1));
        Assert(destination.Data.CachedFillForCheck == -1 && ShopSellingCapacity.Available(destination, carrot.Definition, 350) == 210,
            "Two occupied slots leave four new stacks plus 10 in the matching stack, not 49 in an unrelated stack.");
        Assert(destination.Data.CachedFillForCheck == 2 && ShopSellingCapacity.Available(destination, carrot.Definition, 7) == 7,
            "Honor the requested stock bound after initializing the actual occupied-slot count.");
        for (int i = 0; i < 4; i++) destination.NativeAdd(ShopItem("mixed" + i, 1, 1));
        Assert(ShopSellingCapacity.Available(destination, carrot.Definition, 350) == 10, "A full set of slots can still accept 10 into its existing matching stack.");
        destination.NativeAdd(ShopItem("carrot", 10));
        Assert(ShopSellingCapacity.Available(destination, carrot.Definition, 350) == 0 && destination.Data.Inventory.Count == 6,
            "Full slots and full matching stacks have zero remaining capacity.");
    }

    private static void ShopCapacityMixedDialogMatchesPhysicalRoomAndConservesGoods()
    {
        using var f = new ShopFixture(Carrots350(), Merchant(new[] { "carrot" }));
        f.Trade.Sell.NativeAdd(ShopItem("carrot", 40)); f.Trade.Sell.NativeAdd(ShopItem("potato", 1));
        f.Click("carrot"); Assert(f.Dialog.Data!.Max == 210, "The real dialog accounts for both mixed occupancy and partial same-ID space.");
        f.Dialog.Confirm(210); f.Tick();
        Assert(f.Trade.NativeAdded == 210 && f.Trade.NativeRemoved == 210 && f.Root.Data.GetTotalCountInInventory("carrot") == 140 &&
            f.Trade.Sell.Data.GetTotalCountInInventory("carrot") == 250 && f.Trade.Sell.Data.GetTotalCountInInventory("potato") == 1,
            "Fill only the available carrot space and preserve the unrelated item.");
    }

    private static void ShopCapacityClampsAChangedDestinationAtDialogBinding()
    {
        using var f = new ShopFixture(Carrots350(), Merchant(new[] { "carrot" }));
        int nativeMaximum = 0;
        f.Dialog.BeforeOpenForCheck = data =>
        {
            nativeMaximum = data.Max;
            f.Trade.Sell.NativeAdd(ShopItem("potato", 1));
        };
        f.Click("carrot");
        Assert(nativeMaximum == 300 && f.Dialog.Data!.Max == 250 && f.Trade.NativeMoves == 0,
            "Recompute capacity at the scoped Open prefix rather than trust a maximum calculated earlier.");
        f.Dialog.Confirm(251); f.Tick();
        Assert(f.Root.Data.GetTotalCountInInventory("carrot") == 350 && f.Trade.NativeMoves == 0, "Capture and enforce the corrected maximum.");
    }

    private static void ShopCapacityRevalidatesPendingChangesAndTheFinalNativeEntry()
    {
        using (var f = new ShopFixture(Carrots350(), Merchant(new[] { "carrot" })))
        {
            f.Click("carrot"); f.Trade.Sell.NativeAdd(ShopItem("potato", 1));
            f.Dialog.Confirm(300); f.Tick();
            Assert(f.Trade.NativeMoves == 0 && f.Root.Data.GetTotalCountInInventory("carrot") == 350,
                "Pending confirmation rejects a now-partially-fitting quantity without moving anything.");
            f.Click("carrot"); Assert(f.Dialog.Data!.Max == 250, "A reopened dialog reflects current capacity.");
            f.Dialog.Confirm(250); f.Tick();
            Assert(f.Trade.NativeAdded == 250 && f.Trade.NativeRemoved == 250 && f.Root.Data.GetTotalCountInInventory("carrot") == 100,
                "A newly valid quantity still delegates normally.");
        }
        using (var f = new ShopFixture(Carrots350(), Merchant(new[] { "carrot" })))
        {
            f.Click("carrot");
            f.Trade.BeforeMoveForCheck = () =>
            {
                f.Trade.BeforeMoveForCheck = null;
                f.Trade.Sell.NativeAdd(ShopItem("potato", 1));
            };
            f.Dialog.Confirm(300); f.Tick();
            Assert(f.Trade.NativeMoves == 0 && f.Trade.NativeRemoved == 0 && f.Root.Data.GetTotalCountInInventory("carrot") == 350,
                "Even changes after preflight are checked immediately before the native body.");
        }
    }

    private static void ShopCapacitySingleMovesAlsoInitializeAndVerifyTheCache()
    {
        using var f = new ShopFixture(Carrots350(), Merchant(new[] { "carrot" }));
        Assert(f.Trade.Sell.Data.CachedFillForCheck == -1, "Start with the real sentinel.");
        f.Click("carrot", true); f.Tick();
        Assert(f.Trade.NativeAdded == 1 && f.Trade.NativeRemoved == 1 && f.Trade.Sell.Data.CachedFillForCheck == 1,
            "One-unit quick transfers go through the same cache initialization and native add path.");
        f.Click("carrot"); Assert(f.Dialog.Data!.Max == 299, "The matching partial stack and five empty slots leave 299.");
    }

    private static void ShopCapacityStaleCacheAndUnexpectedLayoutsFallBackWithoutTransfers()
    {
        using (var f = new ShopFixture(Carrots350(), Merchant(new[] { "carrot" })))
        {
            f.Click("carrot");
            f.Trade.Sell.Data.Inventory.Add(ShopItem("potato", 1)); // Bypass native APIs: cache remains 0 while occupancy is 1.
            f.Dialog.Confirm(1); f.Tick();
            Assert(f.Trade.NativeMoves == 0 && f.Trade.CancelCalls == 0 && f.Trade.Sell.Data.GetTotalCountInInventory("potato") == 1 &&
                f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0] is not ShopSellingInventoryData && f.Warnings.Count == 1,
                "Do not silently repair a nonnegative stale cache, retry a transfer, or clear staged goods.");
        }
        var item = ShopItem("carrot");
        foreach (string unusual in new[] { "slots", "expand", "oversized", "alias", "bag" })
        {
            var inventory = new Inventory("inventory", 6);
            if (unusual == "slots") inventory.Data.InventorySize = 7;
            if (unusual == "expand") inventory.Data.AddAutoExpandForCheck();
            if (unusual == "oversized") inventory.Data.Inventory.Add(ShopItem("carrot", 51));
            if (unusual == "alias") { var stack = ShopItem("carrot", 1); inventory.Data.Inventory.Add(stack); inventory.Data.Inventory.Add(stack); }
            if (unusual == "bag") inventory.Data.Inventory.Add(ShopBag());
            bool rejected = false;
            try { ShopSellingCapacity.Available(inventory, item.Definition, 350); }
            catch (NotSupportedException) { rejected = true; }
            Assert(rejected && inventory.Data.CachedFillForCheck == -1, "Reject unsupported " + unusual + " before changing even the occupancy cache.");
        }
    }

    private static void ShopSellableSectionHeaderIsVisibleIconFreeAndRestoresNativeHeaders()
    {
        using var f = new ShopFixture(new[] { ShopItem("carrot", 20), ShopBag(ShopItem("carrot", 10)) }, Merchant(new[] { "carrot" }));
        void CheckHeader()
        {
            var header = f.View.InventoryHeaderWidgetData;
            Assert(header.DrawHeader && header.HeaderIconId == "" && header.CustomHeaderId == "Sellable" &&
                f.Window.LeftPanelTitle == "Keeper" && f.Data.PlayerMultiInventoryWidgetData.HeaderLocaleId == "ui_multiinventory",
                "Use the native icon-free section header without renaming either surrounding caption.");
        }
        CheckHeader(); f.Click("carrot", true); f.Tick(); CheckHeader();
        f.Enabled = false; f.Tick();
        var nativeHeader = ((InventoryWidgetData)f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0]).InventoryHeaderWidgetData;
        Assert(nativeHeader.DrawHeader && nativeHeader.HeaderIconId == "comm-header_2-type_icon-main_inventory" &&
            nativeHeader.CustomHeaderId != "Sellable" && f.Window.LeftPanelTitle == "Keeper" && f.Trade.CancelCalls == 0,
            "Disabling restores native header data and preserves both Keeper and the staged deal.");
    }
}
