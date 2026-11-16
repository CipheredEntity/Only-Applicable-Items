# Configuration

[Development guide](DEVELOPMENT.md)

The standard BepInEx file is
`BepInEx/config/cipheredentity.gk2.onlyapplicableitems.cfg`. No settings-menu mod is required.
There are exactly 35 bound settings: 12 default-on toggles in **Actions**, 22 string
entries in **Inventory Names**, and the default-on **Window Sorting Order** in
**Fixes**. Development-era setting migrations have been removed; obsolete entries
may remain as unbound data in an existing BepInEx file but have no effect.

The declarations in `src/Configuration/ActionSettings.cs`, `FixSettings.cs`, and
`InventoryNameSettings.cs`, and the identities/titles in `src/Core/PickerIdentity.cs`
are the source of truth. All setting names below are literal keys, independent of
the configurable text shown in the inventory window.

## Actions and live behavior

Every action defaults to `true`. This is the declared descending menu order;
existing orders are retained, with PR Machine last. Blank descriptions below are
actual empty strings, not placeholder text or extra menu rows.

| `[Actions]` key | Description |
| --- | --- |
| Seeds | |
| Fertilizers | |
| Alchemical Ingredients | Also corrects ingredient quantity counters. |
| Body Parts | Controls Brains, Hearts, Guts, Skulls, Bones, and Skins. |
| Embalming Fluids | |
| Grave Decorations | Controls Tombstones and Fences. |
| Zombie Equipment | Controls Collars, Armor, Weapons, and Tools. |
| Resurrection Collars | |
| Prayers | |
| Study Table | Controls Study and Decompose. |
| Shop Selling | |
| PR Machine | |

Menu-driven changes update ConfigEntry values directly. Shared picker sessions
restore their owned vanilla sections on the next plugin frame or before a click
when disabled. Enabling consolidation applies on the next picker opening. An
already-retained native selection is not cancelled, reserved, or replaced.

Action-specific effects share their action's setting:

- Alchemical Ingredients also refreshes open laboratory quantity displays.
- Resurrection Collars also refreshes its selected-slot quantity display.
- Study Table controls both section visuals and its research-only selected-slot
  quantity correction; those open displays respond to live changes.
- Shop Selling blocks new confirmations when disabled, then waits for its owned
  quantity dialog to close before releasing captured cells and restoring the player
  panel. Staged goods are not accepted, cancelled, or cleared. Enabling waits for
  the next merchant session.
- PR Machine invalidates pending quantity requests when disabled. Its shared picker
  restores normally; obsolete confirmation callbacks remain inert.

See the [feature index](DEVELOPMENT.md#supported-actions) for precise scopes and
fallback behavior. A declared toggle can exist even when that integration failed
initialization. Startup failures do not become active merely by toggling off/on;
resolve the logged compatibility problem and restart where the feature requires it.
`[Actions] Prayers` is the supported key; an obsolete `Sermons` key is not bound.

## Inventory Names

All 22 name settings default to an empty string. Their descriptions are exactly
`Leave blank to use "Default Name".`, substituting the corresponding fallback below.
Existing saved values are preserved; no preference migration or reset is performed.

| Configuration key | Fallback title | Stable ID |
| --- | --- | --- |
| Seeds | Seeds | Seeds |
| Fertilizers | Fertilizers | Fertilizers |
| Alchemical Ingredients | Alchemical Ingredients | AlchemicalIngredients |
| Body Parts - Brains | Brains | Brains |
| Body Parts - Hearts | Hearts | Hearts |
| Body Parts - Guts | Guts | Guts |
| Body Parts - Skulls | Skulls | Skulls |
| Body Parts - Bones | Bones | Bones |
| Body Parts - Skins | Skins | Skins |
| Embalming Fluids | Embalming Fluids | EmbalmingFluids |
| Grave Decorations - Tombstones | Tombstones | Tombstones |
| Grave Decorations - Fences | Fences | Fences |
| Zombie Equipment - Collars | Collars | ZombieCollars |
| Zombie Equipment - Armor | Armor | ZombieArmor |
| Zombie Equipment - Weapons | Weapons | ZombieWeapons |
| Zombie Equipment - Tools | Tools | ZombieTools |
| Resurrection Collars | Collars | ResurrectionCollars |
| Prayers | Prayers | Sermons |
| Study Table - Study | Study | StudyResearchable |
| Study Table - Decompose | Decompose | StudyDecomposable |
| Shop Selling | Sellable | ShopSelling |
| PR Machine | PR Resources | PrMachineResources |

Blank settings resolve through `PickerTitles.GetDefault` and remain independent even when the
displayed names match. Empty/whitespace-only values use the default without
rewriting the preference. Spaces, punctuation, accents, and repeated names are
accepted. Custom headers use native TextMeshPro no-parse tags so markup-like text
is displayed literally and locale-token-like names are not translated as keys.
The game's existing typography/font support still applies. Very long names may
clip or wrap according to the existing game layout; names are not truncated.

InventoryNameSettings applies names to fresh policies at the shared picker's
opening boundary, preserving all eligibility, filtering, validation, warning,
and live-toggle delegates. Study section identities/order and their quantity/icon
rules remain independent of names. Shop Selling captures its name separately
when its trading session is created; it changes the section header, preserving
the surrounding Keeper caption and transaction controls.

An open session keeps its captured titles during refresh, selection, and pending
quantity dialogs. The next opening reads the current preferences. Disabled actions
keep all name entries bound and saved. Disabling an action retains its existing
native restoration behavior; name edits never trigger fallback or alter gameplay.

## Fixes

`[Fixes] Window Sorting Order = true` is the only fix setting and is independent
of all action toggles. Its description is
`Prevents tooltips from appearing behind windows after repeatedly opening them.`
Disabling stops correction; enabling while windows are already open waits for the
stack to close and a fresh root to open. Existing canvases are not rearranged.
See [WINDOW_SORTING.md](WINDOW_SORTING.md) for the allocation contract and limits.

## File edits and optional menus

BepInEx saves menu-driven ConfigEntry changes normally. ConfigurationReload checks
file modification time/length at shared-picker/shop opening, before reading action
toggles or titles. A changed file is loaded through BepInEx's own Reload method.
There is no background watcher, polling of configuration during a deal, or restart
requirement. Deleted/temporarily unreadable files do not prevent a window opening.
Avoid simultaneous text-editor and menu edits: BepInEx saves the full file.

The recorded local compatibility target is **GK2 Mod Menu 1.3.0.0**, inspected at
`BepInEx/plugins/GK2ModMenu/GK2ModMenu.dll`; the optional menu is listed as
[GK2 Mod Menu (Nexus #216)](https://www.nexusmods.com/graveyardkeeper2/mods/216).
Compatibility evidence comes from its local DLL/model checks, not an assumption
about future menu versions.

Its `ConfigModel.ReadTags` recognizes a tag by the simple type name
`ConfigurationManagerAttributes` and reads public fields/properties. This plugin
provides only optional `DispName` and nullable `Order` fields in ConfigDescription
tags. Descriptions remain standard BepInEx descriptions; category names remain
the actual file sections. `BuildItems` sorts categories alphabetically and orders
entries within each category by descending Order. The menu controls category
placement, so we do not rename categories to force a different group order.
String entries use its normal text editor, with no custom drawers or controls.
Neither GK2 Mod Menu nor ConfigurationManager is referenced, packaged, or required.

The installed menu's `ModSettingsWindow.AddItem` creates a hint label only when
`Item.Info.Length > 0`. Empty descriptions therefore add no description row;
no menu layout patch is necessary. This branch is checked through assembly
metadata in the standalone probe; final visual layout still needs in-game testing.

## Validation

The pure regression harness covers all 22 unique keys/defaults, independent and
duplicate names, blank fallback, punctuation/markup-like values, the exact 35-entry
schema and metadata, exact compact descriptions/ordering, unchanged policy delegates/snapshots, action-disabled names,
next-opening behavior, stock refreshes, duplicate Study section titles/visuals,
and the separate trading integration. Existing action and transfer tests still run.

The standalone shipped-Mono probe uses the installed real BepInEx library to save
and reload all names, disabled action preferences, and special/blank values. All
22 fresh blank settings are separately saved/reloaded and checked against their
original fallback titles before custom names are tested. It
also checks manual file reload and invokes only GK2 Mod Menu's pure `BuildItems`
configuration model to verify all 35 labels/categories/descriptions/orders and
editable-entry persistence. It does not instantiate that plugin/window or run Unity
or game methods. Test files, including BepInEx's core config, are isolated under
the probe output directory and removed after each run. The menu-model check skips
explicitly if that optional DLL is unavailable; BepInEx checks remain independent.

In-game checks before release:

1. Confirm the three categories, 12 toggles, 22 editable names, help text, ordering,
   text entry, blank name defaults, empty action-description spacing, and
   reset-to-default behavior in GK2 Mod Menu (mouse/controller).
2. Rename every inventory, especially both Collars entries, both Study sections,
   Sellable, and PR Resources. Check section headings, window captions, and Keeper's caption.
3. Try duplicate names, blank values, accents, punctuation, literal `<b>` and
   `</noparse>`, and a locale-like name such as `ui_multiinventory`. Check font
   coverage and long-title clipping; verify quantities/tooltips remain correct.
4. Change a name while its picker/shop is open. Refresh stock and complete/cancel
   a selection or staged sale. Reopen and verify that only the new session changes.
5. Disable an action, edit its names, re-enable it, and restart the game. Check
   persistence and independent toggles. Test plain-file edits without either menu
   mod installed, reopening a picker/shop without restarting.
6. Recheck native eligibility, item consumption, collar warnings, Study visuals,
   trading-capacity safety, and the independent sorting fix.
