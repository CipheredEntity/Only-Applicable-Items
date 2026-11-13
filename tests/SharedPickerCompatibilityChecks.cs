using System;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static SessionFixture CompatibilitySession(Func<bool>? enabled = null, Action<UIItemCell>? selected = null)
    {
        Func<Item, bool> eligible = item => item != null && !item.IsEmpty;
        var policy = new PickerPolicy<Item>(PickerId.Seeds, enabled ?? (() => true), eligible, eligible,
            item => { if (item.HasInstanceData) throw new NotSupportedException("Unusual item"); });
        return new SessionFixture(new[] { policy }, SessionSources(new Item("seed", ItemType.None, 20),
            new Item("seed", ItemType.None, 35)), eligible, selected);
    }

    private static InventoryWidgetData ForeignSection()
    {
        var inventory = new Inventory("other-mod", 1);
        inventory.Data.Inventory.Add(new Item("other-item", ItemType.None, 2));
        return new InventoryWidgetData(inventory, new InventoryHeaderWidgetData(inventory, customHeaderId: "Other mod"),
            null, null, _ => { }, null, null, _ => true);
    }

    private static void CheckForeignSectionsSurvive(SessionFixture fixture, Action terminate)
    {
        fixture.Open();
        var foreign = ForeignSection();
        fixture.Container.inventoriesData.Clear();
        fixture.Container.inventoriesData.Add(foreign);
        fixture.Container.HeaderLocaleId = "Other mod title";
        fixture.Container.SelectedWidgetData = foreign;
        fixture.Window.Widget.Redraw(); // The other mod binds its own section to the existing widget.
        int redraws = fixture.Window.Widget.NavigationRefreshes;
        Action refresh = () => { };
        int hiddenCalls = 0;
        Action hidden = () => hiddenCalls++;
        fixture.Container.OnItemsAvailabilityChanged = refresh;
        fixture.Container.onWidgetHide += hidden;
        var root = fixture.Data.MultiInventory.inventoryList[0];
        Action<System.Collections.Generic.List<Item>> otherSubscription = _ => { };
        root.OnItemsAdd += otherSubscription;
        try
        {
            terminate();
            Assert(fixture.Container.inventoriesData.Count == 1 && ReferenceEquals(fixture.Container.inventoriesData[0], foreign) &&
                fixture.Container.HeaderLocaleId == "Other mod title" && ReferenceEquals(fixture.Container.SelectedWidgetData, foreign) &&
                ReferenceEquals(fixture.Container.OnItemsAvailabilityChanged, refresh) &&
                fixture.Window.Widget.NavigationRefreshes == redraws && fixture.Window.IsShown && hiddenCalls == 0,
                "Foreign sections, header, selection, refresh delegate, and visible widgets must remain untouched.");
            Assert(fixture.Session.IsDisposed && fixture.Plugin.Sessions.Count == 0 && root.EventSubscribers == 1 &&
                fixture.Container.onWidgetHide == hidden && root.Data.Inventory[0].Count == 20 && root.Data.Inventory[1].Count == 35,
                "Detach our session and subscriptions while retaining the other mod's handlers and real stock.");
            root.Changed(); fixture.Session.Tick(); fixture.Session.Dispose();
            Assert(ReferenceEquals(fixture.Container.inventoriesData[0], foreign) && root.EventSubscribers == 1,
                "Later source notifications and repeated disposal cannot reclaim a foreign interface.");
        }
        finally
        {
            root.OnItemsAdd -= otherSubscription;
            fixture.Container.onWidgetHide -= hidden;
        }
    }

    private static void SharedPickerForeignSectionsSurviveRestore()
    {
        using var fixture = CompatibilitySession();
        CheckForeignSectionsSurvive(fixture, fixture.Session.RestoreVanilla);
    }

    private static void SharedPickerForeignSectionsSurviveDispose()
    {
        using var fixture = CompatibilitySession();
        CheckForeignSectionsSurvive(fixture, fixture.Session.Dispose);
    }

    private static void SharedPickerPassiveTakeoverDetachesWithoutRefresh()
    {
        using var fixture = CompatibilitySession();
        CheckForeignSectionsSurvive(fixture, fixture.Session.Tick);
    }

    private static void SharedPickerUnboundCommitDisposalRespectsSectionOwnership()
    {
        using (var owned = CompatibilitySession())
        {
            owned.Session.Commit(); // No widget binding/subscriptions yet.
            owned.Session.Dispose();
            Assert(ReferenceEquals(owned.Container.inventoriesData[0], owned.Originals[0]) &&
                owned.Container.HeaderLocaleId == "ui_multiinventory" && owned.Container.onWidgetHide == null,
                "Disposal must still undo an owned commit before the first native draw.");
        }
        using var foreign = CompatibilitySession();
        foreign.Session.Commit();
        var section = ForeignSection();
        foreign.Container.inventoriesData[0] = section;
        foreign.Container.HeaderLocaleId = "Unbound foreign owner";
        foreign.Session.Dispose();
        Assert(ReferenceEquals(foreign.Container.inventoriesData[0], section) &&
            foreign.Container.HeaderLocaleId == "Unbound foreign owner" && foreign.Container.onWidgetHide == null &&
            foreign.Plugin.Sessions.Count == 0 && foreign.Data.MultiInventory.inventoryList[0].EventSubscribers == 0,
            "A takeover before binding must retain foreign data while removing our hide handler.");
    }

    private static void SharedPickerForeignSectionsSurviveDisablingAndFallback()
    {
        bool enabled = true;
        using (var fixture = CompatibilitySession(() => enabled))
            CheckForeignSectionsSurvive(fixture, () => { enabled = false; fixture.Session.Tick(); });
        using var fallback = CompatibilitySession();
        CheckForeignSectionsSurvive(fallback, () =>
        {
            fallback.Session.RequestFallback("test", new NotSupportedException("Cannot continue"));
            fallback.Session.Tick();
        });
    }

    private static void SharedPickerPartialTakeoverPreservesBothSections()
    {
        var owner = StudyOwner();
        using var fixture = new SessionFixture(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!,
            SessionSources(new Item("research:1", ItemType.None, 5), new Item("decompose", ItemType.None, 4)), owner.Predicate);
        fixture.Open();
        var first = fixture.Container.inventoriesData[0];
        var foreign = ForeignSection();
        fixture.Container.inventoriesData[1] = foreign;
        fixture.Container.HeaderLocaleId = "Mixed owner";
        fixture.Session.RestoreVanilla();
        Assert(fixture.Session.IsDisposed && fixture.Container.inventoriesData.Count == 2 &&
            ReferenceEquals(fixture.Container.inventoriesData[0], first) && ReferenceEquals(fixture.Container.inventoriesData[1], foreign) &&
            fixture.Container.HeaderLocaleId == "Mixed owner" && fixture.Data.MultiInventory.inventoryList[0].EventSubscribers == 0,
            "Losing either Study section must detach without rewriting any of the mixed-ownership interface.");
    }

    private static void SharedPickerForeignTakeoverBlocksStaleSelection()
    {
        int calls = 0;
        using var fixture = CompatibilitySession(selected: _ => calls++);
        var stale = new UIItemCell { DisplayingItem = fixture.Data.MultiInventory.inventoryList[0].Data.Inventory[0] };
        CheckForeignSectionsSurvive(fixture, () => fixture.Session.Select(stale));
        Assert(calls == 0, "An old virtual callback cannot dispatch gameplay after a foreign section takeover.");
    }

    private static void SharedPickerTakeoverDuringValidationBlocksDispatch()
    {
        var item = new Item("seed", ItemType.None, 20);
        var foreign = ForeignSection();
        bool takeover = false;
        SessionFixture? target = null;
        Func<Item, bool> eligible = candidate =>
        {
            if (takeover)
            {
                takeover = false;
                target!.Container.inventoriesData[0] = foreign;
                target.Container.HeaderLocaleId = "Taken during validation";
            }
            return candidate != null && !candidate.IsEmpty;
        };
        var policy = new PickerPolicy<Item>(PickerId.Seeds, () => true, eligible, eligible, _ => { });
        int actions = 0;
        using var fixture = new SessionFixture(new[] { policy }, SessionSources(item), eligible, _ =>
        {
            actions++;
            item.Count--; // Models the native action's consumption if incorrectly dispatched.
        });
        target = fixture;
        fixture.Open();
        takeover = true;
        fixture.Session.Select(fixture.Window.Widget.DrawnInventories[0].Cells[0]);
        Assert(fixture.Session.IsDisposed && ReferenceEquals(fixture.Container.inventoriesData[0], foreign) &&
            fixture.Container.HeaderLocaleId == "Taken during validation" && actions == 0 && item.Count == 20 &&
            fixture.Data.MultiInventory.inventoryList[0].EventSubscribers == 0,
            "A native eligibility callback that changes UI ownership must prevent dispatch and leave the foreign interface alone.");
    }

    private static void CheckUnsupportedCallbacks(bool secondary)
    {
        var sources = SessionSources(new Item("seed", ItemType.None, 20));
        sources.inventoryList.Add(new Inventory("chest", 2));
        Func<Item, bool> eligible = item => item != null && !item.IsEmpty;
        var policy = new PickerPolicy<Item>(PickerId.Seeds, () => true, eligible, eligible, _ => { });
        var data = new UIMultiInventoryWindowData(sources, _ => { }, eligible);
        int calls = 0;
        Action<UIItemCell> custom = _ => calls++;
        var supplied = new InventoryWidgetDataBase[data.MultiInventoryWidgetData.inventoriesData.Count];
        for (int i = 0; i < supplied.Length; i++)
        {
            var original = (InventoryWidgetData)data.MultiInventoryWidgetData.inventoriesData[i];
            supplied[i] = new InventoryWidgetData(original.Inventory, original.InventoryHeaderWidgetData,
                original.OnItemCellOver, original.OnItemCellOut, original.OnItemCellPress,
                secondary ? custom : null, secondary ? null : custom, eligible);
            data.MultiInventoryWidgetData.inventoriesData[i] = supplied[i];
        }
        var plugin = new Plugin();
        ApplicableItemsSession? session = null;
        bool rejected = false;
        bool discarded = false;
        try
        {
            session = new ApplicableItemsSession(plugin, policy, data, eligible);
            session.Commit();
            var view = data.MultiInventoryWidgetData.inventoriesData[0];
            discarded = secondary ? view.OnItemCellPress2 == null : view.OnItemCellDown == null;
        }
        catch (NotSupportedException) { rejected = true; }
        finally { session?.Dispose(); }
        Assert(rejected, "Unsupported " + (secondary ? "secondary" : "down") +
            " callback was accepted; the virtual section discarded it: " + discarded + ".");
        for (int i = 0; i < supplied.Length; i++)
        {
            Assert(ReferenceEquals(data.MultiInventoryWidgetData.inventoriesData[i], supplied[i]),
                "Reject before changing the supplied native sections.");
            (secondary ? supplied[i].OnItemCellPress2 : supplied[i].OnItemCellDown)!(new UIItemCell());
        }
        Assert(calls == supplied.Length && plugin.Sessions.Count == 0 && sources.inventoryList[0].EventSubscribers == 0,
            "Leave all custom handlers callable and avoid subscribing an unsupported session.");
    }

    private static void SharedPickerRejectsUniformSecondaryCallbacks() => CheckUnsupportedCallbacks(true);
    private static void SharedPickerRejectsUniformDownCallbacks() => CheckUnsupportedCallbacks(false);

    private static void SharedPickerTakeoverDuringHideIsNotOverwritten()
    {
        using var fixture = CompatibilitySession();
        fixture.Open();
        var foreign = ForeignSection();
        Action takeover = () =>
        {
            fixture.Container.inventoriesData.Clear();
            fixture.Container.inventoriesData.Add(foreign);
            fixture.Container.HeaderLocaleId = "Taken during hide";
        };
        fixture.Container.onWidgetHide += takeover;
        try
        {
            fixture.Session.RestoreVanilla();
            Assert(fixture.Session.IsDisposed && ReferenceEquals(fixture.Container.inventoriesData[0], foreign) &&
                fixture.Container.HeaderLocaleId == "Taken during hide" && fixture.Window.Widget.DrawnInventories.Count == 0 &&
                fixture.Data.MultiInventory.inventoryList[0].EventSubscribers == 0,
                "If a native hide callback takes over the sections, recovery must not publish or draw over them afterward.");
        }
        finally { fixture.Container.onWidgetHide -= takeover; }
    }

    private static void SharedPickerOwnedSessionsRestoreAndCleanUpRepeatedly()
    {
        var sources = SessionSources(new Item("seed", ItemType.None, 20), new Item("seed", ItemType.None, 35));
        Func<Item, bool> eligible = item => item != null && !item.IsEmpty;
        var root = sources.inventoryList[0];
        var plugin = new Plugin();
        var window = new UIMultiInventoryWindow();
        for (int cycle = 0; cycle < 24; cycle++)
        {
            bool enabled = true;
            var policy = new PickerPolicy<Item>(PickerId.Seeds, () => enabled, eligible, eligible,
                item => { if (item.HasInstanceData) throw new NotSupportedException("Unusual item"); });
            int selections = 0;
            var data = new UIMultiInventoryWindowData(sources, cell =>
            {
                Assert(ReferenceEquals(cell.DisplayingItem, root.Data.Inventory[0]), "Forward a real source item.");
                selections++;
            }, eligible);
            var container = data.MultiInventoryWidgetData;
            using var session = new ApplicableItemsSession(plugin, policy, data, eligible);
            session.Commit();
            window.IsShown = true;
            window.Widget.Draw(container);
            Assert(((ApplicableInventoryWidgetData)container.inventoriesData[0]).Groups[0].Total == 55 &&
                root.EventSubscribers == 4 && plugin.Sessions.Count == 1,
                "Each opening must consolidate and subscribe exactly once.");
            session.Select(window.Widget.DrawnInventories[0].Cells[0]);
            Assert(selections == 1 && root.Data.Inventory[0].Count == 20 && root.Data.Inventory[1].Count == 35,
                "Normal selection and aggregation must leave stored quantities unchanged.");
            switch (cycle % 4)
            {
                case 0: window.Close(); break;
                case 1: enabled = false; session.Tick(); break;
                case 2: session.RequestFallback("test", new NotSupportedException()); session.Tick(); break;
                case 3: session.RestoreVanilla(); break;
            }
            Assert(session.IsDisposed && plugin.Sessions.Count == 0 && root.EventSubscribers == 0 &&
                container.inventoriesData.Count == 1 && !(container.inventoriesData[0] is ApplicableInventoryWidgetData) &&
                container.HeaderLocaleId == "ui_multiinventory" && container.onWidgetHide == null,
                "Closing, disabling, and fallback must restore owned data and release every session handler.");
            if (window.IsShown) window.Close();
            session.Dispose();
            Assert(root.EventSubscribers == 0 && window.Widget.DrawnInventories.Count == 0,
                "Repeated disposal and reopening the same window must leave no old widgets or subscriptions.");
        }
    }
}
