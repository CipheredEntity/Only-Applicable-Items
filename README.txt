Only Applicable Items 0.5.0
===========================

A BepInEx 5 plugin for Graveyard Keeper 2. Garden seed selection displays one
"Seeds" inventory containing eligible seeds and combined quantities from the
player, bags, and accessible zone storage. Garden fertilizer selection displays
one "Fertilizers" inventory containing all owned fertilizer types. Already-applied
fertilizers remain visible with their combined quantities, grayed out and
unselectable. Non-fertilizers are excluded.

Alchemy ingredient selection displays one "Alchemical Ingredients" inventory
containing research-eligible alchemical items and combined quantities. Ingredients
blocked by rune limits stay visible, grayed out and unselectable. Non-alchemical
items and those that fail the native research requirements stay hidden. The
native selection restrictions still apply, including repeated ingredient slots.
Laboratory ingredient counters show total accessible stock rather than just the
originally selected stack. All actions retain the game's own selection, crafting
calculations, and consumption. Unsupported actions are unaffected.

Autopsy body-part insertion and zombie replacement display one category inventory:
"Brains", "Hearts", "Guts", "Skulls", "Bones", or "Skins". Equivalent full item
definitions have combined quantities; different qualities and other definition
variants remain separate. Matching parts blocked by the native rules stay visible
but disabled, with the original failure tooltips. Items remain in their real
inventories; confirming the normal operation consumes one real source part.
Ordinary corpses still require extraction before inserting into an occupied slot.

Embalming fluid selection displays one "Embalming Fluids" inventory with combined
quantities from the same accessible sources. Unusable fluids remain visible but
disabled. The game retains its normal corpse/zombie rules, application effects,
and immediate consumption of one real source item when a fluid is selected.

Configuration
-------------
After the plugin first loads, BepInEx creates:
  <game>/BepInEx/config/ciphe.gk2.onlyapplicableitems.cfg

  [Actions]
  Seeds = true
  Fertilizers = true
  Alchemical Ingredients = true
  Body Parts = true
  Embalming Fluids = true

All five options are enabled by default and independent. Body Parts controls all
six categories and both insertion and replacement. Set an option to false to use
the original interface for that action. Existing settings are kept when the new
entry is added. No configuration-manager plugin
is required. Compatible managers expose the entries using these same names.
A live setting change disables an open picker for that action on the next frame;
enabling applies to the next opening. For manual file edits, close the game,
edit the file, and launch again.

Alchemical Ingredients controls BOTH the picker and laboratory counter correction.
Its label correction refreshes an open laboratory when toggled. Disabling restores
the original stack-based counters, including the game's known visual bug. Enabling
consolidation applies on the next picker opening. Counters also refresh within
about half a second when accessible ingredient stock changes while the lab is open.
Every duplicate ingredient row uses the same total stock; the game's existing
denominator accounts for how many slots use it (e.g. 100/100 for two slots x 50).

Unexpected per-instance properties, durability, or contents cause a picker to
fall back to the original interface rather than combining potentially distinct
items. Body-part operations retain the game's original results and failure rules;
the mod does not change how the game recreates an inserted part by definition.

Build prerequisites and paths
----------------------------
The official game instructions are in ../Documentation Reference/README_MODDING.txt.
This project targets net472, using the installed .NET SDK and external Unity/BCL
reference copies. It has no NuGet package dependencies and does not download a
targeting pack. BepInEx 5.4.23.5 and Harmony are referenced from the game's
BepInEx/core directory; Unity Mono and game DLLs come from:
  ../Game Assemblies Copies/Managed

Keep those copyrighted reference files outside this repository. They are not
copied to build or package output. Never ship BepInEx, Harmony, or game DLLs.

The default game path is the standard Program Files (x86) Steam library. For a
different installation, create the git-ignored OnlyApplicableItems.local.props:

  <Project>
    <PropertyGroup>
      <GameInstallPath>D:\SteamLibrary\steamapps\common\Graveyard Keeper 2</GameInstallPath>
    </PropertyGroup>
  </Project>

GameManagedPath and BepInExCorePath can also be overridden in that file or with
MSBuild -p:Name=Value arguments. Paths are only used for reading references.

Build and checks
----------------
From this repository in PowerShell:

  $env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet'
  $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
  $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
  dotnet build OnlyApplicableItems.csproj -c Release
  dotnet run --project tests/OnlyApplicableItems.Checks.csproj -c Release

The checks use the installed .NET 10 SDK and exercise aggregation, source identity,
overflow recovery, snapshot changes, action detection, independent settings,
disabled fertilizer visibility, alchemy eligibility/repeated selection, and
temporary laboratory quantity overrides including failure rollback. Body-part
checks run the production action contract with test-only stand-ins, covering
both action pairs, all six titles, disabled entries, variant safety, live settings,
and real source resolution. They do not load or execute the game. Integration
details are in docs/IMPLEMENTATION.md, docs/ALCHEMY.md, docs/BODY_PARTS.md, and
docs/EMBALMING.md. Embalming checks exercise the production contract with test-only
game stand-ins and the shared snapshot logic; no game assemblies are executed.

Build output:
  bin/Release/net472/OnlyApplicableItems.dll

A Release build also assembles a local item folder, without installing it:
  artifacts/OnlyApplicableItems/
    modinfo.json
    BepInEx/plugins/OnlyApplicableItems/OnlyApplicableItems.dll

Manual in-game installation
--------------------------
Recommended local testing follows the game's official item layout. Copy the
built artifacts/OnlyApplicableItems folder yourself into:
  %USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods\Code\

It should become Mods/Code/OnlyApplicableItems/modinfo.json with BepInEx/plugins
under that same folder. The game manages the copy into its installation and
loads a new or updated plugin after a restart.

Alternatively, for direct BepInEx testing, manually put just the plugin DLL in:
  <game>/BepInEx/plugins/OnlyApplicableItems/OnlyApplicableItems.dll

Use one route at a time to avoid duplicate copies of the same plugin.
No build step installs the DLL, launches the game, or accesses save files.

Manual validation checklist
---------------------------
1. Confirm BepInEx/LogOutput.log shows the plugin and alchemy integration loaded.
2. Open garden seed selection with eligible stacks split across player inventory,
   a bag, and zone storage. Confirm a single Seeds display with the exact total
   (for example, 20 + 35 + 50 = 105) and no ineligible entries.
3. Check different full seed definitions/qualities stay separate and the bed's
   own eligibility rules still apply (including different bed types).
4. Plant when no individual stack meets the recipe cost but their combined
   quantities do. Confirm the game removes exactly its normal cost from real
   sources. Selecting or cancelling alone must consume nothing.
5. Check no eligible seeds, depleted stacks, changing inventory contents, removed
   or added bags, and repeated opening/cancellation. Check scrolling, tooltips,
   mouse input, controller focus, and Back/close with zero entries.
6. Disable Seeds. Confirm the original picker; if changing through a live config
   manager, also check disabling while the consolidated picker is open.
7. Open fertilizer selection with fertilizers in multiple source inventories.
   Confirm a single Fertilizers display, correct combined quantities, no unrelated
   items, and separate entries for distinct fertilizer definitions/qualities.
8. Apply a fertilizer, reopen the picker, and confirm that owned stacks of that
   fertilizer remain visible with the disabled appearance and cannot be selected
   using mouse or controller. Check empty and already-occupied fertilizer slots.
9. Select a usable fertilizer. Verify exactly the game's recipe cost is consumed,
   including when the cost is distributed across stacks. Closing alone must
   consume nothing; selecting a fertilizer applies immediately, unlike seeds.
10. Check the garden toggles in all four combinations, including live changes. Toggling
    Seeds must not affect an open Fertilizers picker, or vice versa. Check repeated
    transitions between both pickers and stock/availability changes while open.
11. Open organ, alchemy, and normal inventory screens immediately after both
    consolidated garden pickers. Organ selection and normal inventory management
    must retain their original behavior; alchemy follows its own option.
12. Open ingredient selection in small and larger laboratories. Confirm only
    research-eligible alchemical ingredients and correct totals across
    player/bags/storage. Ingredients exceeding any rune limit must remain visible
    with their combined count but disabled for both mouse and controller. Verify
    that unresearched and non-alchemical items stay hidden, and replacing a slot
    recalculates which visible entries can be selected.
13. Select the same ingredient in two or three slots. With 100 units across two
    stacks of 50, two matching slots and a batch of 50 should each show 100/100
    without a false red warning. A requirement above 100 should still turn red.
    Check the output counter remains the batch size, including after stock changes.
14. Verify selection alone consumes nothing and actual crafting consumes exactly
    the original requirements across real stacks. Check boosts and flask limits
    retain their normal behavior. No recipe or consumption code is patched.
15. Check all 32 combinations of the five toggles. Disable/re-enable Alchemical
    Ingredients with the lab open and verify native/corrected counters switch
    without losing selections or resetting batch size. Check stock depletion and
    replenishment, closing/reopening, scene changes, and unsupported pickers.
16. For all six body-part categories, check corpse insertion and zombie replacement,
    combined quantities across player/bags/storage, distinct variants, blocked
    entries and failure tooltips, confirmation consuming exactly one real part,
    cancellation, and the independent Body Parts toggle. Check disabling while
    its picker is open. The detailed checklist is in docs/BODY_PARTS.md.
17. Open embalming fluid selection for ordinary corpses and zombies. Check combined
    counts across player/bags/storage, distinct variants, blocked fluids, immediate
    one-item consumption, cancellation, special zombie-effect fluids, and live
    Embalming Fluids toggle changes. See docs/EMBALMING.md for native limitations
    and a detailed checklist.

The mod falls back to vanilla for unexpected instance properties on included items
or an unsupported picker contract. These cases appear once per failure category
in LogOutput.log. Garden and alchemy actions consume by definition ID. Autopsy
removes one real source item by UID, then recreates its result by definition ID;
embalming also consumes by UID and uses definition-based results. The mod does not
invent instance comparisons or consumption rules.

Seeds, Fertilizers, and Alchemical Ingredients were tested in-game before the Body
Parts extension. Body Parts and regression checks require manual in-game validation.
Embalming Fluids also requires in-game validation; automated checks do not prove
Unity rendering, native craft effects, or Harmony runtime behavior.
An alchemy reflection/display failure disables that feature for the current plugin
session and logs a warning; restart after resolving it. Garden features remain
available. Do not treat a successful build or logic checks as proof of UI integration.
