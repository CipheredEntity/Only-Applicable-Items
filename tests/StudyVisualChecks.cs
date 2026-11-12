using System;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private const string NativeInventoryIcon = "comm-header_2-type_icon-main_inventory";

    private static void StudyVisualsHideResearchLabelsWithoutChangingTotalsOrSelection()
    {
        var owner = StudyOwner();
        var a = new Item("research:1", ItemType.None, 40);
        var b = new Item("research:1", ItemType.None, 60);
        var decomposable = new Item("decompose", ItemType.None, 1);
        var uid = a.UniqueId;
        var unrelatedCell = new UIItemCell();
        unrelatedCell.Draw(a);
        var sources = SessionSources(a, b, decomposable);
        using var fixture = new SessionFixture(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!,
            sources, owner.Predicate, owner.Callback);
        fixture.Open();
        var researchCell = fixture.Window.Widget.DrawnInventories[0].Cells[0];
        var decompositionCell = fixture.Window.Widget.DrawnInventories[1].Cells[0];
        Assert(!researchCell.CounterVisible && researchCell.Value == 100 && researchCell.RenderedCount == 100 &&
            fixture.View(0).Groups[0].Total == 100 && ReferenceEquals(researchCell.DisplayingItem, a),
            "Hide only label visibility, retaining the exact aggregate and real displayed representative.");
        Assert(decompositionCell.CounterVisible && decompositionCell.Value == 1 &&
            unrelatedCell.CounterVisible && unrelatedCell.Value == 40,
            "Keep the Decompose single-item counter and all other cells unchanged.");
        b.Count = 70;
        decomposable.Count = 8;
        sources.inventoryList[0].Changed();
        fixture.Session.Tick();
        Assert(!researchCell.CounterVisible && researchCell.Value == 110 && decompositionCell.CounterVisible &&
            decompositionCell.Value == 8 && a.Count == 40 && b.Count == 70 && a.UniqueId == uid,
            "Refresh must preserve hidden research labels, visible decomposition totals, and every source quantity/UID.");
        researchCell.OnItemCellPress!(researchCell);
        Assert(owner.SelectedDefinition == a.id && a.Count == 40 && a.UniqueId == uid,
            "Native full-ID callback selection and source data must be unchanged by the visual correction.");
    }

    private static void StudyVisualsUseNativeIconFreeHeadersOnlyForStudySections()
    {
        var owner = StudyOwner();
        using var study = new SessionFixture(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!,
            SessionSources(), owner.Predicate);
        study.Open();
        Assert(study.View(0).InventoryHeaderWidgetData.HeaderIconId == string.Empty &&
            study.View(1).InventoryHeaderWidgetData.HeaderIconId == string.Empty &&
            study.View(0).InventoryHeaderWidgetData.DrawHeader && study.View(1).InventoryHeaderWidgetData.DrawHeader &&
            study.View(0).InventoryHeaderWidgetData.CustomHeaderId == "Study" &&
            study.View(1).InventoryHeaderWidgetData.CustomHeaderId == "Decompose",
            "Both empty study sections must keep titles and request the native icon-free header layout.");
        Assert(((InventoryWidgetData)study.Originals[0]).InventoryHeaderWidgetData.HeaderIconId == NativeInventoryIcon,
            "Never mutate the original header data or the native default icon.");
        foreach (PickerId id in Enum.GetValues(typeof(PickerId)))
        {
            if (id == PickerId.StudyResearchable || id == PickerId.StudyDecomposable) continue;
            Func<Item, bool> eligible = item => item != null && !item.IsEmpty;
            var policy = new PickerPolicy<Item>(id, () => true, eligible, eligible, _ => { });
            using var other = new SessionFixture(new[] { policy }, SessionSources(new Item("plain", ItemType.None, 2)), eligible);
            other.Open();
            Assert(other.View(0).InventoryHeaderWidgetData.HeaderIconId == NativeInventoryIcon &&
                !other.View(0).InventoryHeaderWidgetData.DrawHeader,
                "All existing single-section picker headers retain their previous icon data and visibility.");
        }
    }

    private static void StudyVisualsPooledCellsRecoverInVanillaAndEveryOtherPicker()
    {
        var owner = StudyOwner();
        UIItemCell reused;
        using (var study = new SessionFixture(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!,
            SessionSources(new Item("research:1", ItemType.None, 10)), owner.Predicate))
        {
            study.Open();
            reused = study.Window.Widget.DrawnInventories[0].Cells[0];
            Assert(!reused.CounterVisible, "Begin with an actually hidden cell from the production decorator.");
        }
        reused.Draw(new Item("vanilla", ItemType.None, 12));
        Assert(reused.CounterVisible && reused.Value == 12, "Native populated-cell redraw must re-enable a pooled counter.");
        reused.Draw(new Item("vanilla-singleton", ItemType.None));
        Assert(!reused.CounterVisible, "Native single-item rendering must retain its original visibility rule.");

        foreach (PickerId id in Enum.GetValues(typeof(PickerId)))
        {
            if (id == PickerId.StudyResearchable) continue;
            StudyTableVisuals.HideResearchQuantity(reused); // Model a cell released from Study.
            Func<Item, bool> eligible = item => item != null && !item.IsEmpty;
            var policy = new PickerPolicy<Item>(id, () => true, eligible, eligible, _ => { });
            var a = new Item("plain", ItemType.None, 7);
            var b = new Item("plain", ItemType.None, 8);
            using var other = new SessionFixture(new[] { policy }, SessionSources(a, b), eligible);
            other.Open();
            var widget = other.Window.Widget.DrawnInventories[0];
            widget.Cells[0] = reused;
            widget.Draw(other.View(0));
            Assert(reused.CounterVisible && reused.Value == 15 && ReferenceEquals(reused.DisplayingItem, a) && a.Count == 7 && b.Count == 8,
                "The same pooled cell must show consolidated totals in Decompose and every other picker identity.");
        }
    }

    private static void StudyVisualsRestoreVanillaCountersAndIconsOnToggleOrFallback()
    {
        foreach (bool failValidation in new[] { false, true })
        {
            var owner = StudyOwner();
            bool enabled = true;
            var item = new Item("research:1", ItemType.None, 10);
            var sources = SessionSources(item);
            using var fixture = new SessionFixture(CreateStudyContract(() => enabled).GetPolicies(owner.Callback, owner.Predicate)!,
                sources, owner.Predicate);
            fixture.Open();
            var pooled = fixture.Window.Widget.DrawnInventories[0].Cells[0];
            Assert(!pooled.CounterVisible, "Research starts with only its label hidden.");
            if (failValidation) item.HasInstanceData = true;
            else enabled = false;
            sources.inventoryList[0].Changed();
            fixture.Session.Tick();
            var native = (InventoryWidgetData)fixture.Container.inventoriesData[0];
            var nativeWidget = fixture.Window.Widget.DrawnInventories[0];
            nativeWidget.Cells[0] = pooled;
            nativeWidget.Draw(native);
            Assert(fixture.Session.IsDisposed && pooled.CounterVisible && pooled.Value == 10 &&
                native.InventoryHeaderWidgetData.HeaderIconId == NativeInventoryIcon && item.Count == 10,
                "Disabling or falling back must restore vanilla counters and icon data, even reusing the hidden cell.");
        }
    }
}
