using System;
using System.Collections.Generic;
using System.Linq;
using LazyBearTechnology;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Integration;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private sealed class ShopFixture : IDisposable
    {
        public readonly Trading Trade = new();
        public readonly UIVendorWindow Window = new();
        public readonly UIVendorWindowData Data;
        public readonly ShopSellingFeatures Features;
        public readonly List<string> Warnings = new();
        public bool Enabled = true;
        public Inventory Root => MainGame.PlayerData.inventory;
        public UIItemCountWindow Dialog => LazyUI.GetWindow<UIItemCountWindow>();
        public ShopSellingInventoryData View => (ShopSellingInventoryData)Data.PlayerMultiInventoryWidgetData.inventoriesData[0];
        public ShopFixture(Item[] owned, Vendor vendor, Action<UIVendorWindowData>? customize = null, bool enabled = true)
        {
            Enabled = enabled;
            LazyUI.ResetForCheck(); UnityEngine.Time.unscaledTime = 0;
            LazyInput.IsGamepadActive = true;
            MainGame.PlayerData = new PlayerData(); Root.Data.Inventory.AddRange(owned);
            Data = new UIVendorWindowData { Vendor = vendor };
            Features = new ShopSellingFeatures(new ShopSellingContract(), () => Enabled, new PlainItemValidator().Validate,
                (operation, _) => Warnings.Add(operation));
            OnlyApplicableItems.Plugin.Instance = new OnlyApplicableItems.Plugin { ShopSelling = Features };
            Trade.BeforeFilled = customize;
            Trade.FillVendorWindowData(Data, "vendor", () => { });
            Window.Open(Data);
        }
        public UIItemCell Cell(string id) => Window.PlayerWidget.DrawnInventories[0].Cells.Single(c => c.DisplayingItem?.id == id);
        public void Click(string id, bool secondary = false)
        {
            var cell = Cell(id);
            if (secondary) View.OnItemCellPress2!(cell); else View.OnItemCellPress!(cell);
        }
        public void Tick() { UnityEngine.Time.unscaledTime += 1; Features.Tick(); }
        public void Dispose()
        { Features.Dispose(); OnlyApplicableItems.Plugin.Instance = null; LazyUI.ResetForCheck(); }
    }

    private static Item ShopItem(string id, int count = 1, int stack = 50)
    { var item = new Item(id, ItemType.None, count); item.Definition.stackCount = stack; return item; }
    private static Item ShopBag(params Item[] items)
    { var bag = ShopItem("bag_" + Guid.NewGuid(), 1, 1); bag.Definition.isBag = true; bag.InventorySize = 10; bag.Inventory.AddRange(items); return bag; }
    private static Vendor Merchant(string[] current, string[]? future = null)
    {
        var vendor = new Vendor();
        foreach (var products in new[] { current, future ?? current })
        { var tier = new VendorTierData(); tier.Products.UnionWith(products); vendor.Definition.tierDataList.Add(tier); }
        return vendor;
    }

    private static void ShopSellingUsesNativeTierRulesSourcesAndFullIds()
    {
        var a = ShopItem("carrot:1", 20); var b = ShopItem("carrot:1", 35); var c = ShopItem("carrot:1", 50);
        var quality = ShopItem("carrot:2", 8); var later = ShopItem("wine:3", 5); var never = ShopItem("stone", 99);
        var rejected = ShopItem("buy-only", 2); var vendor = Merchant(new[] { "carrot:1", "carrot:2", "buy-only" }, new[] { "carrot:1", "carrot:2", "wine:3", "buy-only" });
        foreach (var tier in vendor.Definition.tierDataList) tier.notBuying.Add("buy-only");
        using var f = new ShopFixture(new[] { a, ShopBag(b), ShopBag(c, quality, later, never, rejected) }, vendor);
        Assert(f.Data.PlayerMultiInventoryWidgetData.inventoriesData.Count == 1 && f.Data.PlayerMultiInventoryWidgetData.HeaderLocaleId == "ui_multiinventory" &&
            f.View.InventoryHeaderWidgetData.DrawHeader && f.View.InventoryHeaderWidgetData.CustomHeaderId == "Sellable" &&
            f.View.InventoryHeaderWidgetData.HeaderIconId == string.Empty,
            "Use the visible native icon-free section header and preserve the surrounding native caption.");
        Assert(f.View.Snapshot.Groups.Count == 3 && f.Cell("carrot:1").Value == 105 && f.Cell("carrot:2").Value == 8,
            "Consolidate only owned relevant full IDs across root/direct bags.");
        Assert(f.View.CustomItemsAvailableCondition!(a) && !f.View.CustomItemsAvailableCondition!(later), "Future-tier items stay visible but disabled.");
        f.Click("wine:3"); Assert(f.Trade.NativeMoves == 0 && !f.Dialog.IsShown, "Disabled future products cannot sell.");
        Assert(ReferenceEquals(f.Cell("carrot:1").DisplayingItem, a) && a.Count == 20 && b.Count == 35 && c.Count == 50,
            "Display totals do not alter references, source counts, or ownership.");
        vendor.CurTier = 2; f.Tick(); Assert(f.View.CustomItemsAvailableCondition!(later), "Native tier changes become selectable.");
        Assert(f.Warnings.Count == 0, "Ordinary bags and quality variants are safe.");
    }

    private static void ShopSellingBulkUsesNativeDialogPricesAndDistributedRemoval()
    {
        var a = ShopItem("carrot", 20); var b = ShopItem("carrot", 35); var c = ShopItem("carrot", 50);
        using var f = new ShopFixture(new[] { a, ShopBag(b), ShopBag(c) }, Merchant(new[] { "carrot" }));
        var vendorPanel = f.Data.VendorSide; var buy = f.Data.BuyDeal; var sell = f.Data.SellDeal;
        f.Click("carrot");
        Assert(f.Dialog.IsShown && f.Dialog.Data!.Min == 1 && f.Dialog.Data.Max == 105 && f.Dialog.Data.SnapStep == 1,
            "Use the existing native quantity opener with total accessible stock and seller snap step.");
        Assert(f.Dialog.Data!.PriceCalculateDel(3) == 294 && f.Trade.PriceCalls == 3 && f.Trade.NativeMoves == 0,
            "The dialog uses the native per-unit helper and changes no inventory before confirmation.");
        f.Dialog.Confirm(80); f.Tick();
        Assert(f.Trade.NativeMoves == 1 && f.Trade.NativeAdded == 80 && f.Trade.NativeRemoved == 80 &&
            f.Root.Data.GetTotalCountInInventory("carrot") == 25 && f.Trade.Sell.Data.GetTotalCountInInventory("carrot") == 80,
            "One native transfer consumes distributed real sources with equal staged and removed totals.");
        Assert(f.Cell("carrot").Value == 25 && ReferenceEquals(vendorPanel, f.Data.VendorSide) && ReferenceEquals(buy, f.Data.BuyDeal) && ReferenceEquals(sell, f.Data.SellDeal),
            "Refresh only the selling view and preserve the other panels.");
        f.Trade.CancelForCheck(); f.Tick();
        Assert(f.Root.Data.GetTotalCountInInventory("carrot") == 105 && f.Trade.Sell.Data.Inventory.Count == 0 && f.Trade.CancelCalls == 1,
            "Explicit native cancellation remains in control of returning staged goods.");
    }

    private static void ShopSellingSingletonsStayOneUnitAndCannotBypassGuard()
    {
        var a = ShopItem("sword", 1, 1); var b = ShopItem("sword", 1, 1); var c = ShopItem("sword", 1, 1);
        using var f = new ShopFixture(new[] { a, b, ShopBag(c) }, Merchant(new[] { "sword" }));
        Assert(f.Cell("sword").Value == 3, "Nonstackable full IDs can share a display row.");
        Assert(!f.Trade.OtherTransfer(f.Cell("sword"), 3, f.Root, f.Trade.Sell) && f.Trade.NativeMoves == 0,
            "Unscoped virtual-cell calls cannot submit the dangerous bulk transfer.");
        f.Click("sword"); f.Tick();
        Assert(!f.Dialog.IsShown && f.Trade.NativeMoves == 1 && f.Trade.NativeAdded == 1 && f.Trade.NativeRemoved == 1 && f.Cell("sword").Value == 2,
            "Primary nonstackable selection delegates a single native transfer.");
        f.Click("sword", true); f.Tick();
        Assert(f.Trade.NativeAdded == 2 && f.Trade.NativeRemoved == 2 && f.Root.Data.GetTotalCountInInventory("sword") == 1,
            "Secondary nonstackable selection also transfers exactly one.");
    }

    private static void ShopSellingStackableSingletonSourcesStillOfferBulkAndEmptySafely()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 1), ShopBag(ShopItem("seed", 1), ShopItem("seed", 1)) }, Merchant(new[] { "seed" }));
        f.Click("seed"); Assert(f.Dialog.IsShown && f.Dialog.Data!.Max == 3, "Combined stackable quantity controls dialog opening even when the real representative count is one.");
        f.Dialog.Confirm(3); f.Tick();
        Assert(f.View.Snapshot.Groups.Count == 0 && f.Window.PlayerWidget.DrawnInventories[0].Cells.Count == 0 &&
            f.Trade.NativeAdded == 3 && f.Trade.NativeRemoved == 3 && f.Window.Controller.Rebuilds > 0,
            "An emptied category keeps the section and rebuilds native navigation.");
    }

    private static void ShopSellingPendingCapacityAndStockAreRevalidated()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 40), ShopBag(ShopItem("seed", 40)) }, Merchant(new[] { "seed" }));
        f.Click("seed");
        for (int i = 0; i < 5; i++) f.Trade.Sell.Data.Inventory.Add(ShopItem("filler" + i, 1, 1));
        f.Trade.Sell.Data.Inventory.Add(ShopItem("seed", 49));
        f.Trade.Sell.Data.RecalculateFillForCheck(); // A legitimate native add updates the cache.
        f.Dialog.Confirm(20); f.Tick();
        Assert(f.Trade.NativeMoves == 0 && f.Root.Data.GetTotalCountInInventory("seed") == 80 && f.Trade.Sell.Data.GetTotalCountInInventory("seed") == 49,
            "Reject complete-capacity failures instead of exposing native partial-add loss.");
        f.Click("seed", true); f.Tick();
        Assert(f.Trade.NativeAdded == 1 && f.Trade.NativeRemoved == 1 && f.Trade.Sell.Data.GetTotalCountInInventory("seed") == 50,
            "Partly filled stacks remain usable for a fully fitting one-unit sale.");
        f.Click("seed", true); Assert(f.Trade.NativeMoves == 1, "Completely full staging must reject before the native transfer.");
    }

    private static void ShopSellingPendingDialogsRejectDepletedReboundAndRemovedSources()
    {
        var selected = ShopItem("seed", 20); var bag = ShopBag(ShopItem("seed", 20));
        using var f = new ShopFixture(new[] { selected, bag, ShopItem("other", 5) }, Merchant(new[] { "seed", "other" }));
        f.Click("seed"); selected.Count = 1; bag.Inventory[0].Count = 1; f.Root.Changed();
        var cell = f.Cell("seed"); f.Tick();
        Assert(ReferenceEquals(cell, f.Cell("seed")) && cell.Value == 40, "Keep pooled view cells stable while the native quantity dialog is open.");
        f.Dialog.Confirm(10); f.Tick(); Assert(f.Trade.NativeMoves == 0 && f.Cell("seed").Value == 2, "Fresh total stock bounds every confirmation.");
        f.Click("seed"); f.Cell("seed").Draw(ShopItem("other", 5)); f.Dialog.Confirm(1); f.Tick();
        Assert(f.Trade.NativeMoves == 0, "Rebound cell identity must not change the item a pending callback sells.");
        f.Window.RedrawLite();
        f.Click("seed"); f.Root.Data.Inventory.Remove(selected); f.Dialog.Confirm(1); f.Tick();
        Assert(f.Trade.NativeMoves == 0 && f.Root.Data.GetTotalCountInInventory("seed") == 1, "A detached representative cannot authorize another source by accident.");
    }

    private static void ShopSellingPendingEligibilityAndDialogBoundsRemainNative()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 20) }, Merchant(new[] { "seed" }));
        f.Click("seed"); f.Dialog.Confirm(21); f.Tick();
        Assert(f.Trade.NativeMoves == 0, "The originally opened dialog's maximum cannot be exceeded even if callbacks are invoked directly.");
        f.Click("seed"); f.Data.Vendor.Definition.tierDataList[0].notBuying.Add("seed");
        f.Dialog.Confirm(5); f.Tick();
        Assert(f.Trade.NativeMoves == 0 && !f.View.CustomItemsAvailableCondition!(f.Root.Data.Inventory[0]), "Revalidate native merchant eligibility at confirmation.");
    }

    private static void ShopSellingSafetyFallbackPreservesStagedDealsAndNativeCallbacks()
    {
        var source = ShopItem("seed", 20); var bag = ShopBag(ShopItem("seed", 10));
        using var f = new ShopFixture(new[] { source, bag }, Merchant(new[] { "seed" }));
        f.Click("seed", true); f.Tick(); f.Click("seed"); source.AddTestProperty();
        f.Dialog.Confirm(5); f.Tick();
        Assert(f.Trade.NativeMoves == 1 && f.Trade.Sell.Data.GetTotalCountInInventory("seed") == 1 && f.Trade.CancelCalls == 0,
            "Unsafe instance data blocks transfer without clearing or returning already staged items.");
        Assert(f.Data.PlayerMultiInventoryWidgetData.inventoriesData.Count == 2 && f.Window.PlayerWidget.DrawnInventories.Count == 2 &&
            f.Data.PlayerMultiInventoryWidgetData.inventoriesData.All(s => s is not ShopSellingInventoryData) &&
            f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0].OnItemCellPress!.Target == f.Trade,
            "Restore the native child panel in place, including bag sections and original callbacks.");
        Assert(f.Warnings.Count == 1, "Explain safety fallback once through the supplied diagnostic boundary.");
    }

    private static void ShopSellingInitialUnsafeAndDisabledItemsUseTheExistingValidator()
    {
        var unsafeItem = ShopItem("future", 3); unsafeItem.AddTestProperty();
        using (var f = new ShopFixture(new[] { unsafeItem }, Merchant(Array.Empty<string>(), new[] { "future" })))
            Assert(f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0] is not ShopSellingInventoryData && f.Trade.NativeMoves == 0,
                "Validate disabled higher-tier instances before publishing a view.");
        var durability = ShopItem("tool", 1, 1); durability.Definition.hasDurability = true;
        using (var f = new ShopFixture(new[] { durability }, Merchant(new[] { "tool" })))
            Assert(f.Warnings.Count == 1 && f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0] is not ShopSellingInventoryData, "Durability remains unsafe.");
        var unexpected = ShopItem("single", 2, 1);
        using (var f = new ShopFixture(new[] { unexpected }, Merchant(new[] { "single" })))
            Assert(f.Warnings.Count == 1, "Unexpected nonstackable source counts restore vanilla.");
        var contents = ShopItem("container"); contents.Inventory.Add(ShopItem("inside"));
        using (var f = new ShopFixture(new[] { contents }, Merchant(new[] { "container" })))
            Assert(f.Warnings.Count == 1, "Meaningful contents still trigger the shared item safety guard.");
    }

    private static void ShopSellingToggleDefersPanelRestoreUntilDialogCloses()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 20), ShopBag(ShopItem("seed", 10)) }, Merchant(new[] { "seed" }));
        f.Click("seed", true); f.Tick(); f.Click("seed"); f.Enabled = false; f.Tick();
        Assert(f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0] is ShopSellingInventoryData && f.Dialog.IsShown, "Avoid releasing captured pooled cells beneath an open dialog.");
        f.Dialog.Confirm(5); f.Tick();
        Assert(f.Data.PlayerMultiInventoryWidgetData.inventoriesData.Count == 2 && f.Trade.NativeMoves == 1 && f.Trade.CancelCalls == 0 &&
            f.Trade.Sell.Data.GetTotalCountInInventory("seed") == 1, "Disabling makes pending confirmation inert and restores only the selling panel.");
        f.Enabled = true; f.Tick(); Assert(f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0] is not ShopSellingInventoryData, "Live enabling waits for next shop opening.");
    }

    private static void ShopSellingReentrancyCannotReplayOrRebindANativeTransfer()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" }));
        int callbacks = 0;
        f.Trade.Sell.OnItemsAdd += _ =>
        {
            callbacks++; f.Click("seed", true); f.Features.Tick(); f.Window.RedrawLite();
            Assert(!f.Trade.OtherTransfer(f.Cell("seed"), 2, f.Root, f.Trade.Sell), "Reject nested native calls within the transfer scope.");
        };
        f.Click("seed", true); f.Tick();
        Assert(callbacks == 1 && f.Trade.NativeMoves == 1 && f.Trade.NativeAdded == 1 && f.Trade.NativeRemoved == 1,
            "Inventory callbacks and redraws cannot replay the transaction.");
    }

    private static void ShopSellingNativeFailuresDoNotRetryOrResetTransactions()
    {
        foreach (bool afterAdd in new[] { false, true })
        {
            using var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" }));
            f.Trade.ThrowBeforeMove = !afterAdd; f.Trade.ThrowAfterAdd = afterAdd;
            try { f.Click("seed", true); throw new Exception("Expected native exception."); }
            catch (InvalidOperationException exception) { Assert(exception.Message.StartsWith("native move"), "Propagate native failure rather than hiding it."); }
            f.Tick();
            Assert(f.Trade.NativeMoves == 1 && f.Trade.CancelCalls == 0 && f.Root.Data.GetTotalCountInInventory("seed") == 10 &&
                f.Trade.Sell.Data.GetTotalCountInInventory("seed") == (afterAdd ? 1 : 0),
                "Leave native partial side effects intact; no retries, rollback, cancellation, or clearing.");
        }
    }

    private static void ShopSellingOtherTransfersAndExplicitCloseRemainNative()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" }));
        var stock = new Inventory("inventory", 10); stock.Data.Inventory.Add(ShopItem("seed", 5));
        var buy = new Inventory("inventory", 6);
        Assert(f.Trade.OtherTransfer(new UIItemCell { DisplayingItem = stock.Data.Inventory[0] }, 2, stock, buy) && buy.Data.GetTotalCountInInventory("seed") == 2,
            "Unmarked merchant purchases are not routed through the selling guard.");
        f.Click("seed", true); f.Tick(); f.Window.CloseForCheck();
        Assert(f.Trade.CancelCalls == 1 && f.Trade.Sell.Data.GetTotalCountInInventory("seed") == 0 && f.Root.Data.GetTotalCountInInventory("seed") == 10,
            "Explicit merchant close runs the native cancellation once; session cleanup adds none.");
    }

    private static void ShopSellingRejectsForeignPanelChangesAndUnrelatedCountDialogs()
    {
        using (var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" }),
            data => data.PlayerMultiInventoryWidgetData.inventoriesData[0].CustomItemsNotShowCondition = _ => false))
            Assert(f.Warnings.Count == 1 && f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0] is not ShopSellingInventoryData,
                "Unknown native panel customization gets unchanged vanilla behavior.");
        using (var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" })))
        {
            f.Click("seed"); var stale = f.Dialog.Data!.OnConfirm!; int unrelated = 0;
            f.Dialog.Open(new UIItemCountWindowData { Item = ShopItem("other"), OnConfirm = _ => unrelated++ });
            stale(2); f.Dialog.Confirm(1); f.Tick();
            Assert(unrelated == 1 && f.Trade.NativeMoves == 0, "Unrelated dialogs keep their callbacks and invalidate replaced selling confirmations.");
        }
    }

    private static void ShopSellingChecksAgainAtNativeEntryAfterPreflight()
    {
        foreach (string change in new[] { "stock", "capacity", "eligibility" })
        {
            using var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" }));
            f.Trade.BeforeNativeSingle = () =>
            {
                if (change == "stock") f.Root.Data.Inventory.Clear();
                else if (change == "capacity")
                {
                    for (int i = 0; i < 6; i++) f.Trade.Sell.Data.Inventory.Add(ShopItem("filled" + i, 1, 1));
                    f.Trade.Sell.Data.RecalculateFillForCheck();
                }
                else f.Data.Vendor.Definition.tierDataList[0].notBuying.Add("seed");
            };
            f.Click("seed", true);
            Assert(f.Trade.NativeMoves == 0 && f.Trade.NativeAdded == 0 && f.Trade.NativeRemoved == 0,
                "The immediate native-entry guard must recheck " + change + " changed after preflight.");
        }
    }

    private static void ShopSellingClosedDialogsAndDisposedFeaturesCannotAuthorizeSales()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" }));
        f.Click("seed"); var stale = f.Dialog.Data!.OnConfirm!; f.Dialog.IsShown = false; stale(2);
        Assert(f.Trade.NativeMoves == 0, "A closed quantity dialog cannot authorize a late callback before the next poll.");
        f.Tick(); f.Click("seed"); stale = f.Dialog.Data!.OnConfirm!; f.Features.Dispose(); stale(2);
        Assert(f.Trade.NativeMoves == 0 && f.Trade.CancelCalls == 0 && f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0] is not ShopSellingInventoryData,
            "Disposal restores only the player panel and leaves captured confirmations inert.");
    }

    private static void ShopSellingChangedMerchantContextCannotRedirectAPendingSale()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" }));
        f.Click("seed"); f.Data.Vendor = Merchant(new[] { "seed" }); f.Dialog.Confirm(2); f.Tick();
        Assert(f.Trade.NativeMoves == 0 && f.Trade.CancelCalls == 0 && f.Root.Data.GetTotalCountInInventory("seed") == 10 && f.Warnings.Count == 1,
            "A changed merchant identity invalidates pending sales even if the replacement accepts the same full ID.");
    }

    private static void ShopSellingUsesOnlyDeclaredNongenericHookTargets()
    {
        var contract = new ShopSellingContract();
        foreach (var method in new[] { contract.Fill, contract.Move, contract.Draw, contract.Redraw, contract.Hide, contract.CountOpen })
            Assert(!method.IsGenericMethod && !method.DeclaringType!.IsGenericType, "No typed hook may accidentally patch an inherited shared generic method.");
        Assert(contract.CountOpen.DeclaringType == typeof(UIItemCountWindow) && contract.Hide.DeclaringType == typeof(UIVendorWindow),
            "Count-window closing is observed through IsShown polling rather than a shared generic Hide detour.");
    }

    private static void ShopSellingReentrantDialogOpeningCannotInheritTheSellingScope()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" }));
        Action<int>? stale = null; int other = 0;
        f.Dialog.OnShown = () =>
        {
            f.Dialog.OnShown = null; stale = f.Dialog.Data!.OnConfirm;
            f.Dialog.Open(new UIItemCountWindowData { Item = ShopItem("unrelated"), OnConfirm = _ => other++ });
        };
        f.Click("seed"); stale!(2); f.Dialog.Confirm(1); f.Tick();
        Assert(other == 1 && f.Trade.NativeMoves == 0 && f.Warnings.Count == 0,
            "Consume the opening marker before native callbacks, leaving reentrant unrelated dialogs native and stale sales inert.");
    }

    private static void ShopSellingDisposalDuringCallbacksCompletesPanelRestoration()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" }));
        f.Trade.BeforeNativeSingle = f.Features.Dispose;
        f.Click("seed", true);
        Assert(f.Trade.NativeMoves == 0 && f.Trade.CancelCalls == 0 && f.Data.PlayerMultiInventoryWidgetData.inventoriesData[0] is not ShopSellingInventoryData,
            "Finish deferred shutdown restoration when its native callback scope unwinds, without retrying a sale.");
    }

    private static void ShopSellingNavigationDoesNotStealModalOrMouseFocus()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" }));
        f.Window.IsShownAndTop = false; f.Root.Data.Inventory.Add(ShopItem("seed", 5)); f.Root.Changed(); f.Tick();
        Assert(f.Cell("seed").Value == 15 && f.Window.Controller.Rebuilds == 0, "Stock may refresh beneath an unrelated modal without changing its controller focus.");
        f.Window.IsShownAndTop = true; f.Features.Tick();
        Assert(f.Window.Controller.Rebuilds == 1, "Deferred navigation rebuilding runs when the merchant is active again.");
        LazyInput.IsGamepadActive = false; f.Root.Data.Inventory.Add(ShopItem("seed", 1)); f.Root.Changed(); f.Tick();
        Assert(f.Cell("seed").Value == 16 && f.Window.Controller.Rebuilds == 1, "Mouse refreshes leave controller focus to the native input-mode transition.");
    }

    private static void ShopSellingDisabledAtOpeningLeavesNativeTradingUntouched()
    {
        using var f = new ShopFixture(new[] { ShopItem("seed", 10), ShopBag(ShopItem("seed", 5)) }, Merchant(new[] { "seed" }), enabled: false);
        var sections = f.Data.PlayerMultiInventoryWidgetData.inventoriesData;
        Assert(sections.Count == 2 && sections.All(s => s is not ShopSellingInventoryData), "Disabled startup leaves native root/bag sections intact.");
        sections[0].OnItemCellPress2!(f.Cell("seed"));
        Assert(f.Trade.NativeMoves == 1 && f.Trade.NativeAdded == 1 && f.Trade.NativeRemoved == 1 && f.Warnings.Count == 0,
            "Disabled trading callbacks and transfers remain completely native.");
    }

    private static void ShopSellingFallbackUsesCurrentOwnedBagsAndLeavesForeignPanelsAlone()
    {
        var oldBag = ShopBag(ShopItem("seed", 10)); var newBag = ShopBag(ShopItem("seed", 7));
        using (var f = new ShopFixture(new[] { ShopItem("seed", 10), oldBag }, Merchant(new[] { "seed" })))
        {
            f.Click("seed", true); f.Tick(); f.Root.Data.Inventory.Remove(oldBag); f.Root.Data.Inventory.Add(newBag);
            f.Enabled = false; f.Tick();
            var sections = f.Data.PlayerMultiInventoryWidgetData.inventoriesData;
            Assert(sections.Count == 2 && ReferenceEquals(sections[1].Inventory.Data, newBag) &&
                sections.All(s => !ReferenceEquals(s.Inventory.Data, oldBag)) && f.Trade.Sell.Data.GetTotalCountInInventory("seed") == 1 && f.Trade.CancelCalls == 0,
                "Native fallback must not expose a detached bag or omit newly owned bags; staged deals stay intact.");
        }
        using (var f = new ShopFixture(new[] { ShopItem("seed", 10) }, Merchant(new[] { "seed" })))
        {
            var panel = f.Data.PlayerMultiInventoryWidgetData;
            var foreign = new InventoryWidgetData(f.Root, new InventoryHeaderWidgetData(f.Root), null, null, null, null, null, null);
            panel.inventoriesData[0] = foreign; panel.HeaderLocaleId = "Other mod";
            f.Root.Changed(); f.Tick(); f.Tick();
            Assert(ReferenceEquals(panel.inventoriesData[0], foreign) && panel.HeaderLocaleId == "Other mod" && f.Trade.NativeMoves == 0,
                "A detected foreign section takeover must not be overwritten by fallback.");
        }
    }

    private static void ShopSellingConfigurationIdentityAndEmptyPanelsAreIndependent()
    {
        var config = new BepInEx.Configuration.ConfigFile(); var settings = new ActionSettings(config);
        Assert(settings.ShopSelling.Value && Core.PickerTitles.GetDefault(Core.PickerId.ShopSelling) == "Sellable", "Independent default-on setting and stable title.");
        settings.ShopSelling.Value = false;
        Assert(settings.Seeds.Value && settings.Fertilizers.Value && settings.AlchemicalIngredients.Value && settings.StudyTable.Value && new FixSettings(config).WindowSortingOrder.Value,
            "Shop Selling does not change any existing action or global fix setting.");
        using var f = new ShopFixture(new[] { ShopItem("never", 20), ShopBag() }, Merchant(new[] { "accepted" }));
        Assert(f.View.Snapshot.Groups.Count == 0 && f.Window.PlayerWidget.DrawnInventories[0].Cells.Count == 0, "Empty Sellable remains a safe category.");
        f.Enabled = false; f.Tick(); Assert(f.Window.PlayerWidget.DrawnInventories.Count == 2 && f.Trade.CancelCalls == 0, "Empty fallback rebuilds the native source layout only.");
    }
}
