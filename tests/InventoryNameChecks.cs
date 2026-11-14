using System;
using System.Linq;
using BepInEx.Configuration;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static ConfigEntry<string> NameEntry(ConfigFile config, PickerId id)
        => config.Bind("Inventory Names", InventoryNameSettings.GetKey(id), string.Empty, "");

    private static void ReleaseConfigurationHasExactlyThreeCategoriesAndOptionalMenuTags()
    {
        var config = new ConfigFile();
        new ActionSettings(config); new InventoryNameSettings(config); new FixSettings(config);
        Assert(config.Entries.Count == 35 && config.Entries.Keys.Count(k => k.StartsWith("Actions/")) == 12 &&
            config.Entries.Keys.Count(k => k.StartsWith("Inventory Names/")) == 22 &&
            config.Entries.Keys.Count(k => k.StartsWith("Fixes/")) == 1,
            "Expose only 12 action toggles, 22 inventory names, and one fix.");
        foreach (object entry in config.Entries.Values)
        {
            ConfigDescription description;
            string key;
            if (entry is ConfigEntry<bool> toggle)
            {
                Assert(toggle.DefaultValue && toggle.Value, "All existing toggles remain enabled by default.");
                description = toggle.Description; key = toggle.Definition.Key;
            }
            else { var name = (ConfigEntry<string>)entry; description = name.Description; key = name.Definition.Key; }
            var tag = description.Tags.Single();
            Assert(tag.GetType().Name == "ConfigurationManagerAttributes" &&
                (string)tag.GetType().GetField("DispName")!.GetValue(tag)! == key &&
                tag.GetType().GetField("Order")!.GetValue(tag) is int,
                "Expose optional reflection-readable menu labels/order and ordinary BepInEx descriptions.");
        }
    }

    private static void AllInventoryNamesHaveUniqueKeysAndIndependentDefaults()
    {
        var config = new ConfigFile();
        var names = new InventoryNameSettings(config);
        var ids = Enum.GetValues<PickerId>();
        Assert(ids.Length == 22 && ids.Select(InventoryNameSettings.GetKey).Distinct().Count() == 22 &&
            InventoryNameSettings.GetKey(PickerId.ZombieCollars) == "Zombie Equipment - Collars" &&
            InventoryNameSettings.GetKey(PickerId.ResurrectionCollars) == "Resurrection Collars",
            "Keep every stable inventory independently identifiable, including the two Collars names.");
        foreach (PickerId id in ids)
            Assert(names.GetTitle(id) == PickerTitles.GetDefault(id) && NameEntry(config, id).DefaultValue == "" &&
                NameEntry(config, id).Value == "" && NameEntry(config, id).Description.Description ==
                    "Leave blank to use \"" + PickerTitles.GetDefault(id) + "\".",
                "All 22 name entries default to blank with the exact concise hint, while original titles remain unchanged.");
        foreach (PickerId id in ids)
        {
            NameEntry(config, id).Value = "Custom " + id;
            foreach (PickerId other in ids)
                Assert(names.GetTitle(other) == (other == id ? "Custom " + id : PickerTitles.GetDefault(other)),
                    "Editing one name must not affect any other inventory, even if their defaults match.");
            NameEntry(config, id).Value = "";
        }
    }

    private static void ConfigurationDescriptionsAndOrderingMatchCompactLayout()
    {
        var config = new ConfigFile();
        new ActionSettings(config);
        var fixes = new FixSettings(config);
        (string Key, string Description, int Order)[] expected =
        {
            ("Seeds", "", 110), ("Fertilizers", "", 100),
            ("Alchemical Ingredients", "Also corrects ingredient quantity counters.", 90),
            ("Body Parts", "Controls Brains, Hearts, Guts, Skulls, Bones, and Skins.", 80),
            ("Embalming Fluids", "", 70),
            ("Grave Decorations", "Controls Tombstones and Fences.", 60),
            ("Zombie Equipment", "Controls Collars, Armor, Weapons, and Tools.", 50),
            ("Resurrection Collars", "", 40), ("Prayers", "", 30),
            ("Study Table", "Controls Study and Decompose.", 20), ("Shop Selling", "", 10), ("PR Machine", "", 0)
        };
        foreach (var item in expected)
        {
            var entry = config.Bind("Actions", item.Key, true, "");
            var tag = (ConfigurationManagerAttributes)entry.Description.Tags.Single();
            Assert(entry.Description.Description == item.Description && tag.DispName == item.Key &&
                tag.Order == item.Order && entry.DefaultValue && entry.Value,
                "Use exactly the requested action hint (including truly empty descriptions) without changing labels, order, or defaults.");
        }
        Assert(fixes.WindowSortingOrder.Description.Description ==
            "Prevents tooltips from appearing behind windows after repeatedly opening them." &&
            ((ConfigurationManagerAttributes)fixes.WindowSortingOrder.Description.Tags.Single()).Order == 10 &&
            fixes.WindowSortingOrder.Value, "Keep the sorting fix's concise description, order, and default enabled state.");
    }

    private static void InventoryNamesAllowDuplicatesWhitespaceAndOrdinarySpecialCharacters()
    {
        var config = new ConfigFile();
        var names = new InventoryNameSettings(config);
        foreach (PickerId id in Enum.GetValues<PickerId>()) NameEntry(config, id).Value = "Same name";
        Assert(Enum.GetValues<PickerId>().All(id => names.GetTitle(id) == "Same name"),
            "Display names are allowed to repeat and never replace stable identities.");
        foreach (string value in new[] { "", " \t\r\n ", null! })
        {
            NameEntry(config, PickerId.Seeds).Value = value;
            Assert(names.GetTitle(PickerId.Seeds) == "Seeds" && NameEntry(config, PickerId.Seeds).Value == value,
                "Blank names use the centralized default without rewriting the stored preference.");
        }
        string unusual = "Keeper's seeds = [gold]; #1 / 50% + café & <b>literal</b> </noparse>";
        NameEntry(config, PickerId.Seeds).Value = unusual;
        Assert(names.GetTitle(PickerId.Seeds) == unusual, "Keep spaces, punctuation, accents, and markup-like names as user text.");
        string header = PickerTitles.GetHeaderText(PickerId.Seeds, unusual);
        Assert(header.StartsWith("<noparse></noparse>") && !header.Contains("<b>") &&
            header.Contains("<noparse><</noparse>/noparse>") && header.Contains("&<noparse></noparse>"),
            "Protect native headers from locale-key lookup and rich-text interpretation of custom names.");
        Assert(PickerTitles.GetHeaderText(PickerId.Seeds, "Seeds") == "Seeds" &&
            PickerTitles.GetHeaderText(PickerId.Seeds, "ui_multiinventory").StartsWith("<noparse>"),
            "Default headers stay unchanged while custom names resembling locale tokens remain literal.");
    }

    private static void RenamedPoliciesPreserveEveryGameplayDelegateAndSavedDisabledNames()
    {
        var config = new ConfigFile();
        var actions = new ActionSettings(config);
        var names = new InventoryNameSettings(config);
        actions.EmbalmingFluids.Value = false;
        NameEntry(config, PickerId.EmbalmingFluids).Value = "My fluids";
        var owner = CollarBlockedEmbalmingOwner("renamed-fluid");
        var policy = CreateEmbalmingContract(() => actions.EmbalmingFluids.Value).GetPolicy(owner.Callback, owner.Predicate)!;
        var renamed = names.Apply(new[] { policy })[0];
        Assert(renamed.Id == policy.Id && renamed.Title == "My fluids" && !renamed.IsEnabled &&
            ReferenceEquals(renamed.CanSelect, policy.CanSelect) && ReferenceEquals(renamed.IncludeInDisplay, policy.IncludeInDisplay) &&
            ReferenceEquals(renamed.ValidateItem, policy.ValidateItem) &&
            ReferenceEquals(renamed.ExtraRedTooltipLocIdProvider, policy.ExtraRedTooltipLocIdProvider),
            "Applying a name preserves identity, native eligibility, filtering, safety validation, warnings, and the live toggle.");
        actions.EmbalmingFluids.Value = true;
        var item = new Item("renamed-fluid", ItemType.Embalm, 7);
        var sources = new[] { BodyPartSource(new object(), item) };
        Assert(renamed.IsEnabled && PickerSnapshot<Item>.Build(sources, policy).SameAs(PickerSnapshot<Item>.Build(sources, renamed)) &&
            names.GetTitle(PickerId.EmbalmingFluids) == "My fluids" && policy.Title == "Embalming Fluids",
            "Renaming does not change aggregation or stock, and disabled actions retain their name preferences.");
    }

    private static void SharedPickerNamesApplyOnNextOpenAndStayStableDuringRefresh()
    {
        var config = new ConfigFile();
        var names = new InventoryNameSettings(config);
        Func<Item, bool> eligible = item => item != null && !item.IsEmpty;
        foreach (PickerId id in Enum.GetValues<PickerId>())
        {
            NameEntry(config, id).Value = "First " + id;
            var policy = new PickerPolicy<Item>(id, () => true, eligible, eligible, _ => { });
            var item = new Item("plain", ItemType.None, 2);
            using (var fixture = new SessionFixture(names.Apply(new[] { policy }), SessionSources(item), eligible))
            {
                fixture.Open();
                string expected = PickerTitles.GetHeaderText(id, "First " + id);
                Assert(fixture.Container.HeaderLocaleId == expected &&
                    fixture.View(0).InventoryHeaderWidgetData.CustomHeaderId == expected && fixture.View(0).Policy.Id == id,
                    "Every shared inventory uses its own configured title at the presentation boundary.");
                NameEntry(config, id).Value = "Next " + id;
                item.Count = 3;
                UnityEngine.Time.unscaledTime += 1; fixture.Session.Tick();
                Assert(fixture.Container.HeaderLocaleId == expected && fixture.View(0).Policy.Title == "First " + id &&
                    fixture.View(0).Groups[0].Total == 3, "Refreshing stock keeps the open picker's captured title.");
            }
            using var reopened = new SessionFixture(names.Apply(new[] { policy }), SessionSources(item), eligible);
            reopened.Open();
            Assert(reopened.Container.HeaderLocaleId == PickerTitles.GetHeaderText(id, "Next " + id),
                "Reopening uses the edited name without restarting.");
        }
    }

    private static void StudyNamesStayIndependentWithDuplicateTitlesAndUnchangedVisuals()
    {
        var config = new ConfigFile();
        var names = new InventoryNameSettings(config);
        NameEntry(config, PickerId.StudyResearchable).Value = "Materials";
        NameEntry(config, PickerId.StudyDecomposable).Value = "Materials";
        var owner = StudyOwner();
        var policies = names.Apply(CreateStudyContract().GetPolicies(owner.Callback, owner.Predicate)!);
        using var fixture = new SessionFixture(policies, SessionSources(new Item("research:1", ItemType.None, 3),
            new Item("decompose", ItemType.None, 4)), owner.Predicate);
        fixture.Open();
        Assert(fixture.View(0).Policy.Id == PickerId.StudyResearchable && fixture.View(1).Policy.Id == PickerId.StudyDecomposable &&
            fixture.View(0).Policy.Title == "Materials" && fixture.View(1).Policy.Title == "Materials" &&
            !fixture.Window.Widget.DrawnInventories[0].Cells[0].CounterVisible && fixture.Window.Widget.DrawnInventories[1].Cells[0].CounterVisible &&
            fixture.View(0).InventoryHeaderWidgetData.HeaderIconId == "" && fixture.View(1).InventoryHeaderWidgetData.HeaderIconId == "",
            "Duplicate Study/Decompose titles retain section order, identities, filtering, and identity-based visual rules.");
    }

    private static void ShopSellingUsesItsConfiguredNameWithoutChangingAnActiveDeal()
    {
        var config = new ConfigFile();
        var names = new InventoryNameSettings(config);
        NameEntry(config, PickerId.ShopSelling).Value = "My goods";
        var item = ShopItem("named-carrot", 10);
        var vendor = Merchant(new[] { item.id });
        using (var fixture = new ShopFixture(new[] { item }, vendor, getTitle: names.GetTitle))
        {
            string title = PickerTitles.GetHeaderText(PickerId.ShopSelling, "My goods");
            Assert(fixture.View.InventoryHeaderWidgetData.CustomHeaderId == title &&
                fixture.Data.PlayerMultiInventoryWidgetData.HeaderLocaleId == "ui_multiinventory", "Rename only the Sellable section, preserving Keeper's panel caption.");
            NameEntry(config, PickerId.ShopSelling).Value = "Next goods";
            item.Count = 11; fixture.Tick();
            Assert(fixture.View.InventoryHeaderWidgetData.CustomHeaderId == title && fixture.Cell(item.id).Value == 11 && fixture.Warnings.Count == 0,
                "Refresh the active selling panel without renaming it, altering stock, or disturbing transaction state.");
        }
        using var reopened = new ShopFixture(new[] { item }, vendor, getTitle: names.GetTitle);
        Assert(reopened.View.InventoryHeaderWidgetData.CustomHeaderId == PickerTitles.GetHeaderText(PickerId.ShopSelling, "Next goods"),
            "The next merchant session uses the updated Shop Selling name.");
    }
}
