using System;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private const string CollarWarning = "ui_operation_failed_desc";

    private static UIEmbalmWindowData CollarBlockedEmbalmingOwner(string id)
    {
        GameBalance.ResolvedEmbalmingCrafts[id] = new CraftDef();
        return new UIEmbalmWindowData
        {
            IsZombie = true,
            Zombie = new ZombieWgoData
            {
                Collar = new Item("collar", ItemType.Collar),
                AddToBodyRule = _ => false
            },
            NativeRule = _ => false
        };
    }

    private static void EmbalmingWarningUsesNativeCollarValidationWithoutMutatingItems()
    {
        var fluid = new Item("warning-fluid", ItemType.Embalm, 12);
        var owner = CollarBlockedEmbalmingOwner(fluid.id);
        Item? checkedItem = null;
        owner.Zombie!.AddToBodyRule = item => { checkedItem = item; return false; };
        var native = owner.Predicate;
        var policy = CreateEmbalmingContract().GetPolicy(owner.Callback, native)!;
        var uid = fluid.UniqueId;
        Assert(policy.ExtraRedTooltipLocIdProvider!(fluid) == CollarWarning &&
            ReferenceEquals(checkedItem, fluid) && owner.Zombie.AddToBodyCalls == 1 &&
            ReferenceEquals(policy.CanSelect, native) && !policy.CanSelect(fluid),
            "Use the exact native warning key and real item in the native collar check, retaining native eligibility.");
        Assert(fluid.UniqueId == uid && fluid.Count == 12 && owner.Selected == null,
            "A tooltip query must not select, move, consume, or alter the fluid.");
        owner.Zombie.AddToBodyRule = _ => true;
        Assert(policy.ExtraRedTooltipLocIdProvider(fluid) == null && !policy.CanSelect(fluid),
            "Native rejection alone must never be mistaken for a collar failure.");
    }

    private static void EmbalmingWarningExcludesEarlierRestrictionsAndUncertainContexts()
    {
        Action<UIEmbalmWindowData, Item>[] cases =
        {
            (owner, _) => owner.Body!.OccupiedPocketCount = 6,
            (owner, _) => owner.Body!.OccupiedPocketCount = 7,
            (_, item) => GameBalance.ResolvedEmbalmingCrafts[item.id].ConsumesEmbalmItem = true,
            (owner, item) => { owner.IsZombie = false; GameBalance.ResolvedEmbalmingCrafts[item.id].ConsumesEmbalmItem = true; },
            (owner, _) => owner.Body = null,
            (owner, _) => owner.Body!.Count = 0,
            (owner, _) => owner.IsZombie = false,
            (owner, _) => owner.Zombie = null,
            (owner, _) => owner.Zombie!.Collar = null,
            (owner, _) => owner.Zombie!.Collar!.Count = 0,
            (owner, _) => owner.Zombie!.Collar = new Item("invalid-collar", ItemType.None),
            (_, item) => GameBalance.ResolvedEmbalmingCrafts.Remove(item.id),
            (_, item) => item.Count = 0,
            (_, item) => item.Definition.type = ItemType.Skin,
            (owner, _) => owner.NativeRule = _ => true
        };
        foreach (var change in cases)
        {
            var fluid = new Item("excluded-warning-fluid", ItemType.Embalm, 4);
            var owner = CollarBlockedEmbalmingOwner(fluid.id);
            var zombie = owner.Zombie!;
            var policy = CreateEmbalmingContract().GetPolicy(owner.Callback, owner.Predicate)!;
            change(owner, fluid);
            Assert(policy.ExtraRedTooltipLocIdProvider!(fluid) == null && zombie.AddToBodyCalls == 0,
                "Full pockets, special fluids, selectable items, and uncertain contexts must not show a collar warning.");
        }
        var lastOwner = CollarBlockedEmbalmingOwner("null-warning-fluid");
        Assert(CreateEmbalmingContract().GetPolicy(lastOwner.Callback, lastOwner.Predicate)!
            .ExtraRedTooltipLocIdProvider!(null!) == null, "Null cells have no supplemental warning.");
    }

    private static void EmbalmingWarningFailuresSuppressOnlyTheSupplementalMessage()
    {
        var fluid = new Item("failed-warning-fluid", ItemType.Embalm, 9);
        var owner = CollarBlockedEmbalmingOwner(fluid.id);
        owner.Zombie!.AddToBodyRule = _ => throw new InvalidOperationException("Stale collar context");
        var policy = CreateEmbalmingContract().GetPolicy(owner.Callback, owner.Predicate)!;
        var snapshot = PickerSnapshot<Item>.Build(new[] { BodyPartSource(new object(), fluid) }, policy);
        Assert(snapshot.Groups.Count == 1 && snapshot.Groups[0].Total == 9 &&
            !snapshot.TryResolveSelectable(fluid.id, out _) && policy.ExtraRedTooltipLocIdProvider!(fluid) == null,
            "Warning inspection failures must preserve consolidation, disabled state, and item data.");
        owner.NativeRule = _ => throw new InvalidOperationException("Unavailable eligibility context");
        Assert(policy.ExtraRedTooltipLocIdProvider!(fluid) == null && fluid.Count == 9,
            "A failed diagnostic query must not escape into tooltip rendering.");
    }

    private static void EmbalmingWarningReasonChangesRefreshEvenWhileItemsStayDisabled()
    {
        var a = new Item("changing-warning-fluid", ItemType.Embalm, 20);
        var b = new Item(a.id, ItemType.Embalm, 35);
        var owner = CollarBlockedEmbalmingOwner(a.id);
        var policy = CreateEmbalmingContract().GetPolicy(owner.Callback, owner.Predicate)!;
        using var fixture = new SessionFixture(new[] { policy }, SessionSources(a, b), owner.Predicate);
        fixture.Open();
        var cell = fixture.Window.Widget.DrawnInventories[0].Cells[0];
        Assert(cell.ExtraRedTooltipLocId == CollarWarning && cell.Value == 55,
            "The native provider boundary receives the warning while displaying the consolidated total.");
        var original = fixture.View(0).Snapshot;
        owner.Body!.OccupiedPocketCount = 6;
        UnityEngine.Time.unscaledTime += 1;
        fixture.Session.Tick();
        Assert(!original.SameAs(fixture.View(0).Snapshot) && cell.ExtraRedTooltipLocId == null &&
            !fixture.View(0).CustomItemsAvailableCondition!(a),
            "Moving from collar rejection to full pockets must clear the warning even when native selectability stays false.");
        owner.Body.OccupiedPocketCount = 0;
        UnityEngine.Time.unscaledTime += 1;
        fixture.Session.Tick();
        Assert(cell.ExtraRedTooltipLocId == CollarWarning, "Restoring pocket space must restore the collar warning.");
        owner.Zombie!.AddToBodyRule = _ => true;
        UnityEngine.Time.unscaledTime += 1;
        fixture.Session.Tick();
        Assert(cell.ExtraRedTooltipLocId == null && !fixture.View(0).CustomItemsAvailableCondition!(a),
            "A changed native collar result must remove the warning without authorizing a still-rejected fluid.");
        owner.NativeRule = _ => true;
        UnityEngine.Time.unscaledTime += 1;
        fixture.Session.Tick();
        Assert(fixture.View(0).CustomItemsAvailableCondition!(a) && cell.ExtraRedTooltipLocId == null &&
            a.Count == 20 && b.Count == 35 && cell.Value == 55,
            "Becoming selectable retains all stock, original item references, and the combined display.");
        a.Count = b.Count = 0;
        UnityEngine.Time.unscaledTime += 1;
        fixture.Session.Tick();
        Assert(fixture.View(0).Groups.Count == 0 && fixture.Window.Widget.DrawnInventories[0].Cells.Count == 0,
            "Depleted groups must disappear without leaving a warning-bearing empty cell.");
    }

    private static void EmbalmingWarningPreservesNativeProvidersAndLiveToggleRestoration()
    {
        bool enabled = true;
        bool nativeWarning = true;
        var fluid = new Item("native-warning-fluid", ItemType.Embalm, 3);
        var owner = CollarBlockedEmbalmingOwner(fluid.id);
        var policy = CreateEmbalmingContract(() => enabled).GetPolicy(owner.Callback, owner.Predicate)!;
        using var fixture = new SessionFixture(new[] { policy }, SessionSources(fluid), owner.Predicate);
        Func<Item, string> native = _ => nativeWarning ? "native-warning" : null!;
        fixture.Originals[0].ExtraRedTooltipLocIdProvider = native;
        fixture.Open();
        var widget = fixture.Window.Widget.DrawnInventories[0];
        var view = fixture.View(0);
        Assert(widget.Cells[0].ExtraRedTooltipLocId == "native-warning" &&
            ReferenceEquals(fixture.Originals[0].ExtraRedTooltipLocIdProvider, native),
            "An existing native warning wins and the original section is never modified.");
        nativeWarning = false;
        widget.Draw(view);
        Assert(widget.Cells[0].ExtraRedTooltipLocId == CollarWarning,
            "Use the supplemental warning only when the original provider has no warning.");
        enabled = false;
        Assert(view.ExtraRedTooltipLocIdProvider!(fluid) == null,
            "The supplemental provider observes the toggle immediately, before deferred restoration.");
        fixture.Session.Tick();
        Assert(fixture.Session.IsDisposed && !(fixture.Container.inventoriesData[0] is OnlyApplicableItems.UI.ApplicableInventoryWidgetData) &&
            ReferenceEquals(fixture.Container.inventoriesData[0].ExtraRedTooltipLocIdProvider, native) &&
            fixture.Window.Widget.DrawnInventories[0].Cells[0].ExtraRedTooltipLocId == null,
            "Disabling restores the native provider and clears the displayed supplemental warning.");
        enabled = true;
        using var reopened = new SessionFixture(new[] { policy }, SessionSources(fluid), owner.Predicate);
        reopened.Open();
        Assert(reopened.Window.Widget.DrawnInventories[0].Cells[0].ExtraRedTooltipLocId == CollarWarning,
            "A freshly reopened enabled picker must bind its own warning context.");
    }

    private static void EmbalmingWarningDoesNotLeakIntoPooledCellsOrOtherPickers()
    {
        var fluid = new Item("pooled-warning-fluid", ItemType.Embalm, 2);
        var owner = CollarBlockedEmbalmingOwner(fluid.id);
        var policy = CreateEmbalmingContract().GetPolicy(owner.Callback, owner.Predicate)!;
        UIItemCell reused;
        using (var fixture = new SessionFixture(new[] { policy }, SessionSources(fluid), owner.Predicate))
        {
            fixture.Open();
            reused = fixture.Window.Widget.DrawnInventories[0].Cells[0];
            Assert(reused.ExtraRedTooltipLocId == CollarWarning, "Begin with a real warning-decorated cell.");
        }
        foreach (PickerId id in Enum.GetValues(typeof(PickerId)))
        {
            Func<Item, bool> eligible = item => item != null && !item.IsEmpty;
            var other = new PickerPolicy<Item>(id, () => true, eligible, eligible, _ => { });
            Assert(other.ExtraRedTooltipLocIdProvider == null, "Other policies have no supplemental provider by default.");
            using var fixture = new SessionFixture(new[] { other }, SessionSources(new Item("other", ItemType.None, 4)), eligible);
            fixture.Open();
            var widget = fixture.Window.Widget.DrawnInventories[0];
            reused.ExtraRedTooltipLocId = CollarWarning;
            widget.Cells[0] = reused;
            widget.Draw(fixture.View(0));
            Assert(reused.ExtraRedTooltipLocId == null && reused.Value == 4,
                "Native redraw must clear a pooled warning in every unrelated picker without changing quantities.");
        }
        var inventory = new Inventory("vanilla", 1);
        inventory.Data.Inventory.Add(new Item("vanilla", ItemType.None, 6));
        var vanilla = new InventoryWidgetData(inventory, new InventoryHeaderWidgetData(inventory), null, null, null, null, null, null);
        var vanillaWidget = new InventoryWidget();
        vanillaWidget.Cells.Add(reused);
        reused.ExtraRedTooltipLocId = CollarWarning;
        vanillaWidget.Draw(vanilla);
        Assert(reused.ExtraRedTooltipLocId == null && reused.Value == 6,
            "Returning the same cell to a vanilla inventory must restore its original tooltip data.");
    }
}
