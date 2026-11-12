# Configuration

The standard BepInEx file is
`BepInEx/config/ciphe.gk2.onlyapplicableitems.cfg`. No settings-menu mod is required.
There are exactly 33 bound settings: 11 default-on toggles in **Actions**, 21 string
entries in **Inventory Names**, and the default-on **Window Sorting Order** in
**Fixes**. Development-era setting migrations have been removed; obsolete entries
may remain as unbound data in an existing BepInEx file but have no effect.

## Inventory Names

| Configuration key | Default title | Stable ID |
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

Titles default to `PickerTitles.GetDefault` and remain independent even when the
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

## File edits and optional menus

BepInEx saves menu-driven ConfigEntry changes normally. ConfigurationReload checks
file modification time/length at shared-picker/shop opening, before reading action
toggles or titles. A changed file is loaded through BepInEx's own Reload method.
There is no background watcher, polling of configuration during a deal, or restart
requirement. Deleted/temporarily unreadable files do not prevent a window opening.
Avoid simultaneous text-editor and menu edits: BepInEx saves the full file.

The installed **GK2 Mod Menu 1.3.0.0** was inspected read-only at
`BepInEx/plugins/GK2ModMenu/GK2ModMenu.dll`, corresponding to the user's
[Nexus #216 reference](https://www.nexusmods.com/graveyardkeeper2/mods/216).
The Nexus page itself returned HTTP 403 during inspection.

Its `ConfigModel.ReadTags` recognizes a tag by the simple type name
`ConfigurationManagerAttributes` and reads public fields/properties. This plugin
provides only optional `DispName` and nullable `Order` fields in ConfigDescription
tags. Descriptions remain standard BepInEx descriptions; category names remain
the actual file sections. `BuildItems` sorts categories alphabetically and orders
entries within each category by descending Order. The mod controls category
placement, so we do not rename categories to force a different group order.
String entries use its normal text editor, with no custom drawers or controls.
Neither GK2 Mod Menu nor ConfigurationManager is referenced, packaged, or required.

## Validation

The pure regression harness covers all 21 unique keys/defaults, independent and
duplicate names, blank fallback, punctuation/markup-like values, the exact 33-entry
schema and metadata, unchanged policy delegates/snapshots, action-disabled names,
next-opening behavior, stock refreshes, duplicate Study section titles/visuals,
and the separate trading integration. Existing action and transfer tests still run.

The standalone shipped-Mono probe uses the installed real BepInEx library to save
and reload all names, disabled action preferences, and special/blank values. It
also checks manual file reload and invokes only GK2 Mod Menu's pure `BuildItems`
configuration model to verify all 33 labels/categories/descriptions/orders and
editable-entry persistence. It does not instantiate that plugin/window or run Unity
or game methods. Test files, including BepInEx's core config, are isolated under
the probe output directory and removed after each run. The menu-model check skips
explicitly if that optional DLL is unavailable; BepInEx checks remain independent.

In-game checks before release:

1. Confirm the three categories, 11 toggles, 21 editable names, help text, ordering,
   text entry, and reset-to-default behavior in GK2 Mod Menu (mouse/controller).
2. Rename every inventory, especially both Collars entries, both Study sections,
   and Sellable. Check section headings, window captions, and Keeper's caption.
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
