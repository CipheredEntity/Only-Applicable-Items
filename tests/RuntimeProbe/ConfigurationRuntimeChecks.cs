using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using OnlyApplicableItems.Configuration;
using OnlyApplicableItems.Core;

internal static class ConfigurationRuntimeChecks
{
    public static void Run(string gamePath)
    {
        // The real loader normally initializes Paths before ConfigFile's static
        // core-config constructor. Bootstrap only a unique directory next to this
        // executable, so even BepInEx.cfg is isolated from the installation.
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config-check-" + Guid.NewGuid().ToString("N"));
        typeof(BepInEx.Paths).GetMethod("SetExecutablePath", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object?[] { Path.Combine(root, "Standalone.exe"), root, null, null });
        try { CheckConfiguration(gamePath, Path.Combine(root, "OnlyApplicableItems.cfg")); }
        finally
        {
            // Explicit files and nonrecursive empty-directory cleanup only.
            string core = Path.Combine(root, "config", "BepInEx.cfg");
            string settings = Path.Combine(root, "OnlyApplicableItems.cfg");
            if (File.Exists(settings)) File.Delete(settings);
            if (File.Exists(core)) File.Delete(core);
            if (Directory.Exists(Path.GetDirectoryName(core))) Directory.Delete(Path.GetDirectoryName(core));
            if (Directory.Exists(root)) Directory.Delete(root);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CheckConfiguration(string gamePath, string path)
    {
        try
        {
            var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
            var actions = new ActionSettings(config);
            var names = new InventoryNameSettings(config);
            new FixSettings(config);
            Require(config.Count == 36, "Settings count differs from 12 + 23 + 1.");
            foreach (PickerId id in Enum.GetValues(typeof(PickerId)))
                Require(Entry(config, id).Value == "" && (string)Entry(config, id).DefaultValue == "" &&
                    names.GetTitle(id) == PickerTitles.GetDefault(id), "A fresh name must be blank and resolve to its original title: " + id);
            config.Save();
            var blankReload = new ConfigFile(path, false) { SaveOnConfigSet = false };
            var blankNames = new InventoryNameSettings(blankReload);
            foreach (PickerId id in Enum.GetValues(typeof(PickerId)))
                Require(Entry(blankReload, id).Value == "" && blankNames.GetTitle(id) == PickerTitles.GetDefault(id),
                    "Blank-name fallback did not survive real BepInEx save/reload: " + id);
            Console.WriteLine("PASS all 23 blank configuration defaults retain their original titles after real BepInEx save/reload.");
            var expected = new Dictionary<PickerId, string>();
            foreach (PickerId id in Enum.GetValues(typeof(PickerId)))
            {
                string value = id + " - Keeper's [gold] = 50% / café & <b>literal</b> #1";
                if (id == PickerId.Seeds) value = "";
                if (id == PickerId.Fertilizers) value = " \t ";
                if (id == PickerId.ZombieCollars || id == PickerId.ResurrectionCollars) value = "Same name";
                Entry(config, id).Value = value;
                expected.Add(id, names.GetTitle(id));
            }
            actions.Seeds.Value = false;
            actions.ShopSelling.Value = false;
            config.Save();

            var reloaded = new ConfigFile(path, false) { SaveOnConfigSet = false };
            var reloadedActions = new ActionSettings(reloaded);
            var reloadedNames = new InventoryNameSettings(reloaded);
            new FixSettings(reloaded);
            Require(reloaded.Count == 36 && !reloadedActions.Seeds.Value && !reloadedActions.ShopSelling.Value &&
                reloadedActions.StudyTable.Value, "Action values did not survive real BepInEx save/reload.");
            foreach (var pair in expected)
                Require(reloadedNames.GetTitle(pair.Key) == pair.Value &&
                    (string)Entry(reloaded, pair.Key).DefaultValue == "",
                    "Inventory title did not survive real BepInEx save/reload: " + pair.Key);
            Console.WriteLine("PASS real BepInEx configuration: 36 entries, all 23 names, duplicate/blank/special values, and disabled-action preferences survive save/reload.");

            var reloadErrors = new List<Exception>();
            var refresh = new ConfigurationReload(reloaded, reloadErrors.Add);
            string edited = File.ReadAllText(path).Replace("Zombie Equipment - Collars = Same name", "Zombie Equipment - Collars = From file")
                // Match the serialized value: BepInEx escapes punctuation such as apostrophes.
                .Replace("Study Table - Header = " + TomlTypeConverter.ConvertToString(expected[PickerId.StudyTableHeader], typeof(string)),
                    "Study Table - Header = My renamed table")
                .Replace("Seeds = false", "Seeds = true");
            File.WriteAllText(path, edited);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
            refresh.Refresh();
            Require(reloadedNames.GetTitle(PickerId.ZombieCollars) == "From file" &&
                reloadedNames.GetTitle(PickerId.ResurrectionCollars) == "Same name" && reloadedActions.Seeds.Value &&
                reloadedNames.GetTitle(PickerId.StudyTableHeader) == "My renamed table" &&
                reloadedNames.GetTitle(PickerId.StudyResearchable) == expected[PickerId.StudyResearchable] &&
                reloadedNames.GetTitle(PickerId.StudyDecomposable) == expected[PickerId.StudyDecomposable] &&
                !reloadedActions.ShopSelling.Value && reloadErrors.Count == 0,
                "Manual file edits did not apply at the next-opening refresh boundary.");
            refresh.Refresh();
            Require(reloadedNames.GetTitle(PickerId.ZombieCollars) == "From file" && reloadErrors.Count == 0,
                "An unchanged config file should keep existing settings.");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(4));
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                refresh.Refresh();
                Require(reloadErrors.Count == 1 && reloadedNames.GetTitle(PickerId.ZombieCollars) == "From file",
                    "A locked config file must report the failure without breaking the opening or changing loaded names.");
            }
            refresh.Refresh();
            Require(reloadErrors.Count == 1 && reloadedNames.GetTitle(PickerId.ZombieCollars) == "From file",
                "A temporarily locked config should be retried at the next opening.");
            Console.WriteLine("PASS manual configuration edits reload at the next opening without restarting or changing an active panel.");

            string menuPath = Path.Combine(gamePath, "BepInEx", "plugins", "GK2ModMenu", "GK2ModMenu.dll");
            if (!File.Exists(menuPath))
            {
                Console.WriteLine("SKIP optional GK2 Mod Menu model check: not installed; standard BepInEx checks passed.");
                return;
            }
            // Execute only its pure configuration model. Do not instantiate its
            // plugin/window, invoke Unity, or run game assembly methods.
            var menu = Assembly.LoadFrom(menuPath);
            var model = menu.GetType("GK2ModMenu.ConfigModel", true)!;
            var build = model.GetMethod("BuildItems", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
            var items = ((IEnumerable)build.Invoke(null, new object[] { reloaded })!).Cast<object>().ToArray();
            Require(items.Length == 36, "GK2 Mod Menu did not expose every setting.");
            var previousOrder = new Dictionary<string, int>();
            foreach (var item in items)
            {
                object Field(string name) => item.GetType().GetField(name)!.GetValue(item);
                var entry = (ConfigEntryBase)Field("Entry");
                string category = (string)Field("Category");
                int order = (int)Field("Order");
                Require((string)Field("Label") == entry.Definition.Key && category == entry.Definition.Section &&
                    (string)Field("Info") == entry.Description.Description && !(bool)Field("ReadOnly"),
                    "GK2 Mod Menu did not honor optional metadata for " + entry.Definition);
                Require(!previousOrder.ContainsKey(category) || previousOrder[category] > order,
                    "GK2 Mod Menu settings are not in the declared descending order.");
                previousOrder[category] = order;
            }
            var nameKeys = items.Select(item => (ConfigEntryBase)item.GetType().GetField("Entry")!.GetValue(item))
                .Where(entry => entry.Definition.Section == "Inventory Names").Select(entry => entry.Definition.Key).ToArray();
            int headerIndex = Array.IndexOf(nameKeys, "Study Table - Header");
            Require(headerIndex > 0 && headerIndex + 3 < nameKeys.Length && nameKeys[headerIndex - 1] == "Prayers" &&
                nameKeys[headerIndex + 1] == "Study Table - Study" && nameKeys[headerIndex + 2] == "Study Table - Decompose" &&
                nameKeys[headerIndex + 3] == "Shop Selling",
                "GK2 Mod Menu must place the Study Table header immediately before Study and Decompose, preserving surrounding settings.");
            reloaded.SaveOnConfigSet = true;
            Entry(reloaded, PickerId.ZombieCollars).BoxedValue = "Edited through menu entry";
            var savedAgain = new ConfigFile(path, false) { SaveOnConfigSet = false };
            var savedNames = new InventoryNameSettings(savedAgain);
            Require(savedNames.GetTitle(PickerId.ZombieCollars) == "Edited through menu entry" &&
                savedNames.GetTitle(PickerId.ResurrectionCollars) == "Same name", "Menu-style editing did not persist independently.");
            Require(!typeof(InventoryNameSettings).Assembly.GetReferencedAssemblies().Any(a => a.Name == "GK2ModMenu"),
                "Optional GK2 Mod Menu acquired a compile-time dependency.");
            VerifyEmptyDescriptionRendering(menuPath);
            Console.WriteLine("PASS installed GK2 Mod Menu " + menu.GetName().Version + ": all 36 labels/categories/descriptions/orders, editable names, and independent persistence (configuration model only).");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static ConfigEntry<string> Entry(ConfigFile config, PickerId id)
        => config.Bind("Inventory Names", InventoryNameSettings.GetKey(id), string.Empty, "");

    private static void VerifyEmptyDescriptionRendering(string menuPath)
    {
        // Inspect the installed UI's branch without instantiating Unity objects.
        using (var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(menuPath))
        {
            var method = assembly.MainModule.Types.Single(t => t.FullName == "GK2ModMenu.ModSettingsWindow")
                .Methods.Single(m => m.Name == "AddItem");
            var code = method.Body.Instructions;
            var info = code.Single(i => (i.Operand as Mono.Cecil.FieldReference)?.Name == "Info" &&
                (i.Next?.Operand as Mono.Cecil.MethodReference)?.Name == "get_Length");
            var zero = info.Next.Next;
            var branch = zero.Next;
            var label = code.Single(i => i.Operand is Mono.Cecil.MethodReference call &&
                call.DeclaringType.FullName == "GK2ModMenu.NativeUi" && call.Name == "Label");
            Require(zero.OpCode == Mono.Cecil.Cil.OpCodes.Ldc_I4_0 &&
                (branch.OpCode == Mono.Cecil.Cil.OpCodes.Ble_S || branch.OpCode == Mono.Cecil.Cil.OpCodes.Ble) &&
                branch.Operand is Mono.Cecil.Cil.Instruction skip && skip.Offset > label.Offset,
                "GK2 Mod Menu no longer skips description-label creation for an empty description.");
        }
        Console.WriteLine("PASS GK2 Mod Menu omits empty description rows (installed UI metadata verified; no Unity rendering executed).");
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
