using System;
using BepInEx.Configuration;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static void StudyHeaderHasIndependentNamesAndNextOpeningBehavior()
    {
        var config = new ConfigFile();
        var names = new InventoryNameSettings(config);
        var header = NameEntry(config, PickerId.StudyTableHeader);
        Assert(header.Definition.Key == "Study Table - Header" && header.DefaultValue == "" &&
            header.Description.Description == "Leave blank to use \"Study Table\".",
            "Use the existing blank-name convention with an independent header identity.");
        NameEntry(config, PickerId.StudyResearchable).Value = "My study items";
        NameEntry(config, PickerId.StudyDecomposable).Value = "My science items";
        var owner = StudyOwner();
        var policies = CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!;
        var item = new Item("research:1", ItemType.None, 2);
        foreach (string value in new[] { "", " \t ", "My study items", "Keeper's table & <b>literal</b>", "ui_multiinventory" })
        {
            header.Value = value;
            using (var fixture = new SessionFixture(names.Apply(policies), SessionSources(item), owner.Predicate,
                getTitle: names.GetTitle))
            {
                fixture.Open();
                string expected = PickerTitles.GetHeaderText(PickerId.StudyTableHeader, names.GetTitle(PickerId.StudyTableHeader));
                Assert(fixture.Container.HeaderLocaleId == expected && fixture.Window.Widget.RenderedHeaderToken == expected &&
                    fixture.View(0).Policy.Title == "My study items" && fixture.View(1).Policy.Title == "My science items",
                    "Blank, duplicate, special, and locale-like header names leave both section names independent.");
                header.Value = "Next opening";
                item.Count++;
                fixture.Data.MultiInventory.inventoryList[0].Changed();
                fixture.Session.Tick();
                Assert(fixture.Window.Widget.RenderedHeaderToken == expected && fixture.View(0).Groups[0].Total == item.Count &&
                    fixture.View(0).Policy.Title == "My study items" && fixture.View(1).Policy.Title == "My science items",
                    "Stock refresh retains the open window's captured title without changing section names or counts.");
            }
            using var reopened = new SessionFixture(names.Apply(policies), SessionSources(item), owner.Predicate, getTitle: names.GetTitle);
            reopened.Open();
            Assert(reopened.Window.Widget.RenderedHeaderToken == PickerTitles.GetHeaderText(PickerId.StudyTableHeader, "Next opening"),
                "The next opening uses the new preference without a restart.");
        }
    }

    private static void StudyHeaderRestoresOnDisableFallbackDisposeAndReuse()
    {
        foreach (string reason in new[] { "disable", "fallback", "dispose" })
        foreach (bool empty in new[] { false, true })
        {
            bool enabled = true;
            var owner = StudyOwner();
            var item = new Item("research:1", ItemType.None, 8);
            var sources = empty ? SessionSources() : SessionSources(item);
            using var fixture = new SessionFixture(CreateStudyContract(() => enabled).GetPolicies(owner.Callback, owner.Predicate)!,
                sources, owner.Predicate);
            fixture.Open();
            Assert(fixture.Window.Widget.RenderedHeaderToken == "Study Table", "Empty sections also receive the Study Table caption.");
            if (reason == "disable") { enabled = false; fixture.Session.Tick(); }
            else if (reason == "fallback")
            {
                fixture.Session.RequestFallback("test", new NotSupportedException("Unsupported item"));
                fixture.Session.Tick();
            }
            else fixture.Session.Dispose();
            Assert(fixture.Session.IsDisposed && fixture.Container.HeaderLocaleId == "ui_multiinventory" &&
                fixture.Container.inventoriesData.Count == fixture.Originals.Length && item.Count == 8 &&
                sources.inventoryList[0].EventSubscribers == 0,
                "Disabling, fallback, and disposal restore native data and clean up without modifying items.");
            if (reason != "dispose")
                Assert(fixture.Window.Widget.RenderedHeaderToken == "ui_multiinventory", "Live restoration redraws the native Inventory caption.");
            fixture.Window.Close();
            var native = new UIMultiInventoryWindowData(sources, _ => { }, owner.Predicate);
            fixture.Window.Widget.Draw(native.MultiInventoryWidgetData);
            Assert(fixture.Window.Widget.RenderedHeaderToken == "ui_multiinventory", "A reused widget cannot retain the custom title in vanilla UI.");
        }
    }

    private static void StudyHeaderDoesNotAffectOtherSessionLayoutsOrFailedOpening()
    {
        var owner = StudyOwner();
        var study = CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!;
        var sources = SessionSources();
        string UnexpectedLookup(PickerId id) => throw new InvalidOperationException("Unrelated name lookup: " + id);
        var seeds = new PickerPolicy<Item>(PickerId.Seeds, () => true, owner.Predicate, owner.Predicate, _ => { });
        foreach (var policies in new[] { new[] { seeds }, new[] { study[0] }, new[] { study[1], study[0] }, new[] { seeds, study[1] } })
        {
            using var fixture = new SessionFixture(policies, sources, owner.Predicate, getTitle: UnexpectedLookup);
            fixture.Open();
            Assert(fixture.Container.HeaderLocaleId == (policies.Length == 1 ? policies[0].Title : "ui_multiinventory"),
                "Only the exact ordered two-section Study session may request or replace its main header.");
        }
        var plugin = new Plugin();
        var data = new UIMultiInventoryWindowData(sources, _ => { }, owner.Predicate);
        var original = data.MultiInventoryWidgetData.inventoriesData[0];
        bool rejected = false;
        try { new OnlyApplicableItems.UI.ApplicableItemsSession(plugin, study, data, owner.Predicate, UnexpectedLookup); }
        catch (InvalidOperationException) { rejected = true; }
        Assert(rejected && data.MultiInventoryWidgetData.HeaderLocaleId == "ui_multiinventory" &&
            ReferenceEquals(data.MultiInventoryWidgetData.inventoriesData[0], original) && data.MultiInventoryWidgetData.onWidgetHide == null,
            "A title lookup failure before commit leaves all native UI and callbacks intact.");
    }

    private static void StudyHeaderRespectsForeignSectionOwnership()
    {
        foreach (bool restore in new[] { false, true })
        {
            var owner = StudyOwner();
            using var fixture = new SessionFixture(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!,
                SessionSources(), owner.Predicate);
            fixture.Open();
            var foreign = fixture.Originals[0];
            fixture.Container.inventoriesData.Clear();
            fixture.Container.inventoriesData.Add(foreign);
            fixture.Container.HeaderLocaleId = "Another mod's header";
            if (restore) fixture.Session.RestoreVanilla();
            else fixture.Session.Dispose();
            Assert(fixture.Session.IsDisposed && fixture.Container.HeaderLocaleId == "Another mod's header" &&
                fixture.Container.inventoriesData.Count == 1 && ReferenceEquals(fixture.Container.inventoriesData[0], foreign),
                "Detaching from a foreign-owned Study picker must preserve its sections and header.");
        }
    }
}
