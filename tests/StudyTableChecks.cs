using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static StudyTablePickerContract CreateStudyContract(Func<bool>? enabled = null)
        => new StudyTablePickerContract(enabled ?? (() => true), item =>
        {
            if (item.HasInstanceData || item.Definition.hasDurability || item.IsBag ||
                item.InventorySize != 0 || item.Inventory.Count != 0)
                throw new NotSupportedException("Unsupported instance properties");
        });

    private static UIResourceBasedCraftWindowData StudyOwner()
    {
        GameBalance.ResolvedSurveys.Clear();
        GameBalance.ResolvedSurveys.Add("research:1", new SurveyDef());
        GameBalance.ResolvedSurveys.Add("research:2", new SurveyDef());
        GameBalance.ResolvedSurveys.Add("decompose", new SurveyDef { isScienceFuelCraft = true });
        GameBalance.ResolvedSurveys.Add("completed", new SurveyDef());
        var owner = new UIResourceBasedCraftWindowData();
        owner.Allowed.UnionWith(new[] { "research:1", "research:2", "decompose", "missing-definition" });
        return owner;
    }

    private static void StudyTableRecognizesOnlyItsNativeContextAndOrderedIdentities()
    {
        var owner = StudyOwner();
        var other = StudyOwner();
        var contract = CreateStudyContract();
        var callback = owner.Callback;
        var predicate = owner.Predicate;
        var policies = contract.GetPolicies(callback, predicate)!;
        Assert(policies.Length == 2 && policies[0].Id == PickerId.StudyResearchable && policies[0].Title == "Study" &&
            policies[1].Id == PickerId.StudyDecomposable && policies[1].Title == "Decompose", "Use independent stable IDs in the requested fixed order.");
        Assert(contract.GetPolicies(other.Callback, predicate) == null && contract.GetPolicies(callback, other.Predicate) == null &&
            contract.GetPolicies(callback, owner.OtherPredicate) == null && contract.GetPolicies(cell => callback(cell), predicate) == null &&
            contract.GetPolicies(callback, item => predicate(item)) == null && contract.GetPolicies(callback + callback, predicate) == null &&
            contract.GetPolicies(callback, predicate + predicate) == null && contract.GetPolicies(null, predicate) == null,
            "Unknown wrappers, owners, predicates, or multicast contexts must remain native.");
        var sermon = new UIPrayWindowData();
        Assert(contract.GetPolicies(sermon.Callback, sermon.Predicate) == null &&
            CreateSermonContract().GetPolicy(callback, predicate) == null && CreateEmbalmingContract().GetPolicy(callback, predicate) == null &&
            CreateResurrectionContract().GetPolicy(callback, predicate) == null && CreateBodyPartContract().GetPolicy(callback, predicate) == null,
            "Study selection and existing gameplay contracts must remain isolated.");
    }

    private static void StudyTableUsesNativeEligibilityAndTheSingleResolvedSurvey()
    {
        var owner = StudyOwner();
        var policies = CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!;
        var sources = new[]
        {
            CollarSource(new object(), new Item("research:1", ItemType.None, 4)),
            CollarSource(new object(), new Item("research:2", ItemType.None, 3)),
            CollarSource(new object(), new Item("decompose", ItemType.None, 5)),
            CollarSource(new object(), new Item("completed", ItemType.None) { HasInstanceData = true }),
            CollarSource(new object(), new Item("missing-definition", ItemType.None)),
            CollarSource(new object(), new Item("irrelevant", ItemType.None) { HasInstanceData = true })
        };
        var snapshots = PickerSnapshot<Item>.BuildAll(sources, policies);
        Assert(snapshots[0].Groups.Count == 2 && snapshots[0].Groups[0].Key == "research:1" &&
            snapshots[0].Groups[1].Key == "research:2" && snapshots[1].Groups.Count == 1 && snapshots[1].Groups[0].Key == "decompose",
            "Keep full definition variants and classify only native-eligible items using the resolver's result.");
        owner.Allowed.Remove("research:1"); // Models the native allowed list excluding completed research.
        var completed = PickerSnapshot<Item>.BuildAll(sources, policies);
        Assert(completed[0].Groups.Count == 1 && completed[1].Groups.Count == 1,
            "A native-rejected research target must not move to decomposition or expose an alternative survey.");
        Assert(!policies[0].IncludeInDisplay(null!) && !policies[1].CanSelect(new Item("decompose", ItemType.None, 0)),
            "Empty or depleted items cannot become selectable through a cached native ID list.");
    }

    private static void CoordinatedSnapshotsEnumerateSourcesOnceAndRejectAmbiguity()
    {
        var owner = StudyOwner();
        var policies = CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!;
        int enumerations = 0;
        var item = new Item("research:1", ItemType.None, 7);
        IEnumerable<StackSource<Item>> Sources()
        {
            enumerations++;
            yield return CollarSource(new object(), item);
            yield return CollarSource(new object(), item);
        }
        var result = PickerSnapshot<Item>.BuildAll(Sources(), policies);
        Assert(enumerations == 1 && result[0].Groups[0].Total == 7 && result[1].Groups.Count == 0,
            "Enumerate accessible sources once, deduplicate aliases, and keep empty sections.");
        bool rejected = false;
        var overlapping = new PickerPolicy<Item>(PickerId.StudyDecomposable, () => true, _ => true, _ => true, _ => { });
        try { PickerSnapshot<Item>.BuildAll(Sources(), new[] { policies[0], overlapping }); }
        catch (NotSupportedException) { rejected = true; }
        Assert(rejected, "Ambiguous category membership must reject the whole plan rather than show duplicate choices.");
        rejected = false;
        try { PickerSnapshot<Item>.BuildAll(Sources(), new[] { policies[0], policies[0] }); }
        catch (NotSupportedException) { rejected = true; }
        Assert(rejected, "A coordinated picker cannot contain duplicate category identities.");
    }

    private static void StudySessionAggregatesPlayerBagsAndStorageWithVisibleHeaders()
    {
        var owner = StudyOwner();
        var a = new Item("research:1", ItemType.None, 20);
        var b = new Item("research:1", ItemType.None, 35);
        var c = new Item("research:1", ItemType.None, 50);
        var decomposable = new Item("decompose", ItemType.None, 4);
        var bag = new Item("bag", ItemType.None) { InventorySize = 4 };
        bag.Definition.isBag = true;
        bag.Inventory.AddRange(new[] { b, decomposable });
        var sources = SessionSources(a, bag);
        var chest = new Inventory("chest", 12);
        chest.Data.Inventory.Add(c);
        sources.inventoryList.Add(chest);
        var uid = a.UniqueId;
        using var fixture = new SessionFixture(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!, sources,
            owner.Predicate, owner.Callback);
        fixture.Open();
        Assert(fixture.Container.inventoriesData.Count == 2 && fixture.View(0).Groups[0].Total == 105 &&
            fixture.View(1).Groups[0].Total == 4 && fixture.View(0).InventoryHeaderWidgetData.CustomHeaderId == "Study" &&
            fixture.View(1).InventoryHeaderWidgetData.CustomHeaderId == "Decompose" &&
            fixture.View(0).InventoryHeaderWidgetData.DrawHeader && fixture.View(1).InventoryHeaderWidgetData.DrawHeader &&
            fixture.Container.HeaderLocaleId == "Study Table" && fixture.Window.Widget.RenderedHeaderToken == "Study Table",
            "The real session must create two named sections under the Study Table window caption.");
        Assert(fixture.Window.Widget.DrawnInventories[0].Cells[0].Value == 105 && a.Count == 20 && b.Count == 35 &&
            c.Count == 50 && decomposable.Count == 4 && a.UniqueId == uid && ReferenceEquals(fixture.Data.MultiInventory, sources),
            "Combined counters must not change source quantities, identities, or the real inventory list.");
        fixture.Window.Widget.DrawnInventories[1].Cells[0].OnItemCellPress!(fixture.Window.Widget.DrawnInventories[1].Cells[0]);
        Assert(owner.SelectedDefinition == "decompose" && decomposable.Count == 4,
            "Section-aware selection forwards the real item to the original native callback without consumption.");
    }

    private static void StudySessionKeepsEmptySectionsAndRejectsStaleCategoryClicks()
    {
        var owner = StudyOwner();
        var sources = SessionSources();
        using var fixture = new SessionFixture(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!, sources,
            owner.Predicate, owner.Callback);
        fixture.Open();
        var firstWidget = fixture.Window.Widget.DrawnInventories[0];
        var secondWidget = fixture.Window.Widget.DrawnInventories[1];
        Assert(firstWidget.Cells.Count == 0 && secondWidget.Cells.Count == 0 && fixture.View(0).InventoryHeaderWidgetData.DrawHeader,
            "Both empty sections must exist safely with no synthetic selectable items.");
        var decomposable = new Item("decompose", ItemType.None, 5);
        sources.inventoryList[0].Data.Inventory.Add(decomposable);
        sources.inventoryList[0].Changed();
        fixture.Session.Tick();
        Assert(firstWidget.Cells.Count == 0 && secondWidget.Cells.Count == 1 && fixture.Window.Widget.NavigationRefreshes == 2,
            "An empty first category must not prevent later sections or native navigation refresh notifications.");
        var research = new Item("research:1", ItemType.None, 3);
        sources.inventoryList[0].Data.Inventory.Add(research);
        sources.inventoryList[0].Changed();
        fixture.Session.Tick();
        var staleCell = firstWidget.Cells[0];
        GameBalance.ResolvedSurveys[research.id].isScienceFuelCraft = true;
        fixture.Session.Select(0, staleCell);
        Assert(owner.SelectedDefinition == null && firstWidget.Cells.Count == 0 && secondWidget.Cells.Count == 2 &&
            fixture.Container.inventoriesData.Count == 2 && ReferenceEquals(firstWidget, fixture.Window.Widget.DrawnInventories[0]) &&
            ReferenceEquals(secondWidget, fixture.Window.Widget.DrawnInventories[1]),
            "A stale category click must refresh both views without dispatch or widget recreation.");
        Assert(research.Count == 3 && decomposable.Count == 5, "Category refresh must leave all native stock untouched.");
    }

    private static void StudySessionBuildAndRefreshFailuresRestoreTheWholePicker()
    {
        var owner = StudyOwner();
        var research = new Item("research:1", ItemType.None, 4);
        var unusual = new Item("decompose", ItemType.None, 3) { HasInstanceData = true };
        using (var initial = new SessionFixture(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!,
            SessionSources(research, unusual), owner.Predicate))
        {
            bool failed = false;
            try { initial.Open(); }
            catch (NotSupportedException) { failed = true; }
            Assert(failed && initial.Container.inventoriesData.Count == initial.Originals.Length &&
                ReferenceEquals(initial.Container.inventoriesData[0], initial.Originals[0]) &&
                initial.Container.HeaderLocaleId == "ui_multiinventory", "Failure building the second category must leave the entire original picker intact.");
        }
        unusual.HasInstanceData = false;
        var sources = SessionSources(research, unusual);
        using var refresh = new SessionFixture(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!, sources, owner.Predicate);
        refresh.Open();
        unusual.Definition.hasDurability = true;
        sources.inventoryList[0].Changed();
        refresh.Session.Tick();
        Assert(refresh.Session.IsDisposed && refresh.Plugin.Sessions.Count == 0 && refresh.Container.inventoriesData.Count == 1 &&
            !(refresh.Container.inventoriesData[0] is OnlyApplicableItems.UI.ApplicableInventoryWidgetData) &&
            research.Count == 4 && unusual.Count == 3, "Any category's validation failure must restore all native sections and release subscriptions.");
    }

    private static void StudySessionNativeRedrawFailureAndLiveToggleRestoreBothSections()
    {
        var owner = StudyOwner();
        var item = new Item("research:1", ItemType.None, 5);
        bool enabled = true;
        var sources = SessionSources(item);
        using (var fixture = new SessionFixture(CreateStudyContract(() => enabled).GetPolicies(owner.Callback, owner.Predicate)!,
            sources, owner.Predicate, owner.Callback))
        {
            fixture.Open();
            enabled = false;
            fixture.Session.Select(0, fixture.Window.Widget.DrawnInventories[0].Cells[0]);
            Assert(fixture.Session.IsDisposed && owner.SelectedDefinition == null && fixture.Container.inventoriesData.Count == 1 &&
                fixture.Container.HeaderLocaleId == "ui_multiinventory", "Disabling either category's shared toggle restores the whole picker before a stale click.");
            enabled = true;
            fixture.Session.Tick();
            Assert(fixture.Container.inventoriesData.Count == 1, "Enabling must wait for the next picker opening.");
        }
        using var failed = new SessionFixture(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!, sources, owner.Predicate);
        failed.Open();
        failed.Window.Widget.FailNextRedraw = true;
        item.Count = 6;
        sources.inventoryList[0].Changed();
        failed.Session.Tick();
        Assert(failed.Session.IsDisposed && failed.Container.inventoriesData.Count == 1 && item.Count == 6,
            "A native redraw failure after publication must recover the entire original picker without modifying stock.");
    }

    private static void ExistingSingleSectionSessionsRetainTheirLayoutAndRealSourceBehavior()
    {
        foreach (PickerId id in Enum.GetValues(typeof(PickerId)))
        {
            if (id == PickerId.StudyResearchable || id == PickerId.StudyDecomposable || id == PickerId.StudyTableHeader) continue;
            bool enabled = true;
            Func<Item, bool> eligible = item => item != null && !item.IsEmpty;
            var policy = new PickerPolicy<Item>(id, () => enabled, eligible, eligible, _ => { });
            var a = new Item("plain", ItemType.None, 20);
            var b = new Item("plain", ItemType.None, 10);
            var sources = SessionSources(a, b);
            Item? selected = null;
            using var fixture = new SessionFixture(new[] { policy }, sources, eligible, cell => selected = cell.DisplayingItem);
            fixture.Open();
            var widget = fixture.Window.Widget.DrawnInventories[0];
            Assert(fixture.Container.inventoriesData.Count == 1 && fixture.Container.HeaderLocaleId == policy.Title &&
                !fixture.View(0).InventoryHeaderWidgetData.DrawHeader && widget.Cells[0].Value == 30,
                "Every existing picker identity must retain its one-section title, hidden section header, and combined counter.");
            a.Count = 4;
            sources.inventoryList[0].Changed();
            fixture.Session.Tick();
            widget.Cells[0].OnItemCellPress!(widget.Cells[0]);
            Assert(widget.Cells[0].DisplayingItem == a && ReferenceEquals(selected, a) && a.Count == 4 && b.Count == 10 &&
                fixture.View(0).Groups[0].Total == 14 && ReferenceEquals(widget, fixture.Window.Widget.DrawnInventories[0]),
                "Existing single-section refresh and real-source dispatch must retain the same widget and stored items.");
            enabled = false;
            fixture.Session.Tick();
            Assert(fixture.Session.IsDisposed && fixture.Container.HeaderLocaleId == "ui_multiinventory",
                "Existing single-section live disabling must still restore native sections and title.");
        }
    }

    private static void StudyTableSettingsDefaultOnAndControlBothIndependentIdentities()
    {
        var config = new ConfigFile();
        config.Bind("Actions", "Prayers", false, "Saved setting");
        var settings = new ActionSettings(config);
        var owner = StudyOwner();
        var policies = CreateStudyContract(() => settings.StudyTable.Value).GetPolicies(owner.Callback, owner.Predicate)!;
        Assert(settings.StudyTable.Value && policies[0].IsEnabled && policies[1].IsEnabled && !settings.Sermons.Value &&
            settings.Seeds.Value && settings.Fertilizers.Value && settings.AlchemicalIngredients.Value && settings.ResurrectionCollars.Value,
            "The real Study Table setting defaults on without resetting any previous setting.");
        settings.StudyTable.Value = false;
        settings.Sermons.Value = true;
        Assert(!policies[0].IsEnabled && !policies[1].IsEnabled && settings.Sermons.Value && settings.ZombieEquipment.Value &&
            ReferenceEquals(settings.StudyTable, config.Bind("Actions", "Study Table", true, "")),
            "One independent Actions/Study Table toggle controls both stable section identities.");
    }
}
