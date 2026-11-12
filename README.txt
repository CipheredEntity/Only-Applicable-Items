Only Applicable Items 0.14.1
============================

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
calculations, and consumption. Unsupported actions retain their original inventories.

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
Normal fluids blocked specifically by zombie collar limits show the game's existing
red warning in their normal item tooltip. Full pockets, special-fluid restrictions,
and uncertain contexts do not add that warning. Existing warning providers take
precedence; the enhancement shares the Embalming Fluids toggle.

Individual grave decoration selection displays "Tombstones" or "Fences",
with equivalent definitions and their quantities combined across accessible
inventories. Native eligibility and craft selection remain unchanged. Occupied
slots still require the normal removal operation before installing a replacement.

Zombie equipment selection displays "Collars", "Armor", "Weapons", or "Tools",
combining equivalent items from the player's inventory and bags. Zone storage
remains excluded, matching the game's normal access. Matching items rejected by
native rules stay visible but disabled. Equipment selection passes one real
single-item source to the game's insertion/replacement callback; combined totals
are display-only. Collar upgrades retain their existing one-unit behavior.
Armor, Weapons, and Tools with the same full definition ID as the item currently
equipped in that slot stay visible with their total quantity but disabled. Empty
slots and different definitions retain the native selection rules.

Resurrection-table collar selection also displays "Collars", combining equivalent
collars from the player, bags, and the zone storage normally accessible there.
All owned accessible collars remain visible; collars outside the body's native
skull limits are grayed out and unselectable. Selection retains one real source
item for the game's later one-unit consumption when resurrection is prepared.
This feature has its own Resurrection Collars setting, independent of upgrades.
The same setting hides the selected resurrection collar slot's stack counter,
so the slot represents the single collar required. Inventory quantities and the
real selected item remain unchanged. Toggling refreshes an open selected slot;
disabling restores its original native counter.

Prayer-stand sermon selection displays one "Prayers" inventory with combined
quantities from the player, bags, and normally accessible zone storage. The
game's CanUsePray rule identifies valid sermon items. Different full definitions
and quality variants stay separate. Attendance requirements, success chance,
execution, and rewards remain unchanged; insufficient attendance still blocks
starting the sermon in the prayer interface, not choosing it in the picker.

Study-table selection displays two virtual inventories in order: "Study"
and "Decompose". Both remain present when empty. Native eligibility identifies
available targets. Study quantity labels are hidden; Decompose still
shows combined quantities. Both category headers use the native icon-free layout.
The selected-item slot also hides its quantity for research, using the game's
resolved survey classification. Decomposition retains its native selected-slot
counter. Disabling Study Table restores the selected slot's vanilla display,
including while the interface is open. Real selections and requirements are unchanged.
The game's resolved survey definition determines the
category. Equivalent full definitions have combined quantities. Research completion,
requirements, and consumption remain native: research retains its main item;
decomposition consumes its configured quantity. Missing supporting resources still
affect the normal craft button rather than hiding an otherwise eligible item.

Merchant selling displays one "Sellable" inventory across the backpack and
directly held bags. Its native section header is visible without a sack icon;
the original Keeper panel title is unchanged. Currently accepted full item definitions are selectable;
items bought only at higher defined tiers remain visible but disabled. Products
the merchant never buys at its current or higher tiers are hidden. Qualities stay
separate. Stackable items use native quantity dialogs and distributed removal;
nonstackable items show combined ownership but transfer only one unit per action.
Merchant stock, deal slots, prices, acceptance, rewards, and cancellation remain
native. Stock, eligibility, source identity, and full destination capacity are
checked again before each consolidated sale, including dialog confirmation.
Capacity validation initializes the destination's native occupancy cache, verifies
it against actual contents, and bounds quantities by real slots and matching stack
space. A fresh six-slot deal with 50-per-stack carrots is limited to 300, preventing
the native -1 cache from incorrectly permitting 350. This protection applies only
to active consolidated selling; native transactions and cancellation are retained.

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
  Grave Decorations = true
  Zombie Equipment = true
  Resurrection Collars = true
  Prayers = true
  Study Table = true
  Shop Selling = true

  [Inventory Names]
  Seeds =
  Fertilizers =
  Alchemical Ingredients =
  Body Parts - Brains =
  Body Parts - Hearts =
  Body Parts - Guts =
  Body Parts - Skulls =
  Body Parts - Bones =
  Body Parts - Skins =
  Embalming Fluids =
  Grave Decorations - Tombstones =
  Grave Decorations - Fences =
  Zombie Equipment - Collars =
  Zombie Equipment - Armor =
  Zombie Equipment - Weapons =
  Zombie Equipment - Tools =
  Resurrection Collars =
  Prayers =
  Study Table - Study =
  Study Table - Decompose =
  Shop Selling =

  [Fixes]
  Window Sorting Order = true

Window Sorting Order is a separate global fix, independent of all action settings.
It prevents repeated nested-window openings from accumulating sorting orders above
tooltips, including interfaces without consolidated inventories. It preserves
native 15-point increments and never rearranges existing canvases. Disabling stops
corrections immediately; enabling waits for a fresh root window (close all stacked
windows and reopen). Reentrant callbacks or unexpected state suspend correction
until that same fresh-root boundary. Existing inflated windows are not relayered.
See docs/WINDOW_SORTING.md for compatibility limits and validation.

All eleven action options are enabled by default and independent. Body Parts controls all
six body-part categories and both insertion and replacement. Grave Decorations
controls both Tombstones and Fences. Zombie Equipment controls Collars, Armor,
Weapons, and Tools together; Resurrection Collars controls only resurrection-table
collar selection. Both collar inventories default to "Collars", with distinct
internal identities. Study Table controls Study and Decompose together,
with a separate stable identity for each section. Set an option to false to use
the original interface for that action. Existing settings are kept when the new
entry is added. No configuration-manager plugin
is required. Compatible managers expose the entries using these same names.

Inventory Names has one editable name for each of the 21 virtual inventories.
All name settings default to blank, preserving the original displayed titles.
Each hint says which title is used when blank, for example: Leave blank to use "Seeds".
Existing saved names are retained; changing the default does not reset preferences.
Use the separate Zombie Equipment - Collars and Resurrection Collars entries even
though both display Collars when blank. Names may match, include spaces and punctuation,
or be left empty/whitespace-only to use the default. Editing names never changes
which items appear or can be selected. Names are kept when an action is disabled.

Name changes apply the next time that picker or shop opens. An open inventory keeps
its current title, including during stock refreshes or a pending sale. GK2 Mod Menu
is optional: it exposes all settings with readable labels and ordered entries.
Only grouped actions and Alchemical Ingredients have brief descriptions; the other
action toggles have none. GK2 Mod Menu omits those empty description rows.
Standard BepInEx files also work independently. Saved file edits are checked when
opening an inventory picker or shop; restarting the game is not required. Avoid
editing the same file in a menu and text editor at the same time.

Development-era setting migrations have been removed. Use Actions/Prayers;
the obsolete Actions/Sermons entry is ignored. Stable internal inventory IDs are
unchanged. See docs/CONFIGURATION.md for settings, compatibility, and validation.
A live setting change disables an open picker for that action on the next frame;
enabling applies to the next opening. When editing the file manually, changes are
loaded at the next inventory-picker or shop opening.

Shop Selling has a separate trading session. Disabling immediately blocks new
consolidated sales; an open selling quantity dialog must close before its captured
inventory cells are released and the native player panel is restored. Enabling
applies at the next shop opening. Safety fallback restores only that child panel,
without accepting, cancelling, returning, or clearing staged deals. Pending stale
confirmations do nothing. Unsupported instance properties use vanilla rather
than risk choosing a different instance through native definition-ID removal.
The existing native trading cancellation/full-inventory bugs are not repaired.
See docs/SHOP_SELLING.md for native limits and the manual validation checklist.

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

Keep those copyrighted reference files outside this repository. The production
build does not copy them to its output or package. Never ship BepInEx, Harmony,
or game DLLs. The optional runtime probe copies tooling dependencies only into
its ignored test output; that directory is never packaged.

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
Warning checks cover native collar-check forwarding, unrelated restrictions,
reason-only refreshes, native provider precedence, live toggling, and cell reuse.
Grave decoration checks cover both titles/groups, the shared live toggle, exact
action isolation, variant/instance safety, disabled visibility, and real sources.
See docs/GRAVE_DECORATIONS.md for integration details and in-game validation.
Equipment checks exercise the production contract for all four categories, native
disabled entries, single-item safety, source resolution, and the shared setting.
See docs/ZOMBIE_EQUIPMENT.md for scope and a manual checklist.
Resurrection checks exercise the production scope/contract and exception cleanup,
disabled visibility, source references, default titles/identities, and the actual
settings declarations using in-memory game/configuration stand-ins. See
docs/RESURRECTION_COLLARS.md for integration details and native limitations.
Sermon checks exercise the production action contract, exact native predicate
forwarding, source/variant aggregation, safety fallback, and independent settings.
See docs/SERMONS.md for integration details and an in-game checklist.
Study Table checks also execute the production coordinated session against
in-memory inventory/UI boundaries: empty sections, independent aggregation,
atomic build/refresh fallback, native callback forwarding, and shared toggling.
Single-section session regressions cover every existing picker identity. These
checks do not run Unity rendering or actual controller input. See docs/STUDY_TABLE.md.

Shop Selling checks execute the production session, shared item validator, contract,
and patch forwarding against stand-in inventories/trading/UI. They cover full-ID
and quality grouping, future-tier restrictions, distributed transfers, one-unit
nonstackable moves, native-entry revalidation, stale dialogs, partial-capacity
rejection, preserved staging on fallback, live toggles, reentrancy, controller focus,
and failure/unload handling. The native-cache regression fixture preserves the -1
sentinel and split-before-insert loss: disabled selling reproduces 350 removed and
300 staged; enabled selling caps at 300 and conserves all 350 through cancellation.
Header checks assert section visibility, icon-free data, unchanged Keeper title,
and native restoration. The runtime probe also checks the copied game DLL's
shop hook/delegate signatures, fields, and raw-cache behavior as metadata. These do not execute actual
game trading or prove Unity rendering, pricing expressions, and transaction effects.

Window-sorting checks cover allocation history, nesting, out-of-order closure,
reentrancy, native failures, unexpected state, live settings, and cleanup. A separate
Windows x64 probe verifies the actual Harmony hooks on the shipped Mono runtime,
using the unchanged decompiled controller with stand-in windows/canvases. It also
inspects the reference DLL's generic signatures and allocation IL without executing
game assembly methods. It launches no Unity/game process. Commands and the remaining
in-game checklist are in docs/WINDOW_SORTING.md.

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
15. Check the seven independent toggles (automated checks cover all 128 combinations).
    Disable/re-enable Alchemical
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
18. Open both empty grave-decoration slots. Confirm Tombstones/Fences titles,
    exact totals across player/bags/storage, separate variants, normal craft costs,
    cancellation, and live Grave Decorations changes. Remove and replace existing
    decorations through the original flow. See docs/GRAVE_DECORATIONS.md.
19. Open a zombie's collar upgrade picker. Confirm Collars, totals across player
    inventory/bags, excluded zone storage, distinct variants, disabled equal/lower
    and incompatible collars, one-unit source consumption, and native old-collar
    removal. Check cancellation, live Zombie Equipment changes, and unchanged
    collar behavior. See docs/ZOMBIE_EQUIPMENT.md.
20. For Armor, Weapons, and Tools, test insertion and replacement, equivalent pieces
    in separate slots/bags, definition variants, real one-item transfers, cancellation,
    unequipping, and inventory-space handling. Confirm the shared toggle controls
    all four categories and unexpected counts/properties fall back to vanilla.
    Confirm the currently equipped full definition stays visible but disabled in
    Armor/Weapons/Tools, while other variants remain selectable. Check empty slots,
    equipment changes, and that Collars retains its normal upgrade rules.
21. At a resurrection table, check Collars totals across player/bags/zone storage,
    separate collar definitions, unsuitable collars visible but disabled, selection
    consuming nothing, preparation consuming one real source unit, cancellation,
    and repeated opening. Toggle Resurrection Collars and Zombie Equipment
    independently, including while each picker is open. Both titles stay Collars.
    Select a stack of ten: the selected slot should show its icon without a
    quantity, while inventories retain full counts. Disable Resurrection Collars
    while the table is open to restore the native selected-slot quantity; re-enable
    to hide it again. Verify preparation still consumes exactly one collar.
    See docs/RESURRECTION_COLLARS.md for the delayed-selection limitation.
22. At the prayer stand, check Prayers totals across player/bags/zone storage,
    separate types and qualities, hidden unrelated items, and unchanged sermon
    selection and prayer controls. Test insufficient attendance, low success
    chance, cancellation, source changes, and the independent live Prayers toggle.
    Compare execution costs and rewards with vanilla. See docs/SERMONS.md.
23. At the study table, check ordered Study/Decompose headers, combined
    quantities across player/bags/storage, distinct qualities, and either/both
    categories empty. Test normal research completion, repeated science decomposition,
    insufficient requirements, cancellation, source changes, controller navigation,
    and live Study Table disabling. Unexpected studyable item properties should
    restore the entire native picker. Recheck existing single-section actions.
    Study labels should stay hidden while its totals still aggregate;
    Decompose counters and requirement counters remain native. The selected-item
    slot should hide only research quantities and retain decomposition quantities.
    Check category switching, clearing, reopening, and live toggling in the study
    window. Check header spacing without sack icons, and that counters
    return when the same cells are reused in other pickers or vanilla inventories.
    See docs/STUDY_TABLE.md for the detailed checklist and native behavior.

24. With Window Sorting Order enabled, repeat study/alchemy/other picker openings
    100 times, with action consolidation both enabled and disabled. Check tooltip
    layering, nested menus, mouse/controller focus, tab layers, credits, and fades.
    Disable the fix to confirm original allocation behavior. Enable mid-stack and
    verify correction waits until all windows close and a new root opens.

25. In shops, check Sellable totals across backpack/bags, quality variants, future
    tiers shown disabled, and never-bought items hidden. Confirm Sellable appears
    as an icon-free section under the unchanged Keeper title, including after
    refresh/reopening, and native headers return when disabled. Sell stackable quantities
    crossing bag/stack boundaries and transfer identical nonstackable items one at
    a time. Check both mouse buttons/controller actions, prices, town happiness,
    full/partial staging, acceptance, cancellation, and closing. Change stock or
    capacity while a quantity dialog is open and verify stale confirmation is
    rejected. Toggle with staged goods and an open dialog: no automatic cancellation
    or loss of staging. See docs/SHOP_SELLING.md for the detailed checklist.
    On a freshly opened enabled shop with 350 carrots in 50-unit stacks, the first
    dialog must allow at most 300. Offer 300, verify 50 remain owned, then cancel
    and verify all 350 are present. Repeat with partial stacks and mixed staged items.

The mod falls back to vanilla for unexpected instance properties on included items
or an unsupported picker contract. These cases appear once per failure category
in LogOutput.log. Garden and alchemy actions consume by definition ID. Autopsy
removes one real source item by UID, then recreates its result by definition ID;
embalming also consumes by UID and uses definition-based results. The mod does not
invent instance comparisons or consumption rules.

Seeds, Fertilizers, Alchemical Ingredients, Body Parts, and Embalming Fluids were
tested in-game before the Grave Decorations extension. Grave Decorations and
regression checks require manual in-game validation; automated checks do not prove
Unity rendering, native craft effects, or Harmony runtime behavior.
Collars was tested in-game before the Armor/Weapons/Tools extension. The new paths
and regression checks require in-game validation; automated checks do not execute
native transfers, equipment assignment, or collar arithmetic.
Resurrection Collars requires in-game validation of Harmony scoping, Unity rendering,
and delayed native consumption; the automated checks do not execute those systems.
Prayers also requires in-game validation of picker rendering and native sermon
execution; its automated checks do not run the game or serialized prayer scripts.
Study Table requires in-game validation of both headers, empty-group controller
navigation, native survey effects, and the shared session's single-section UI paths.
An alchemy reflection/display failure disables that feature for the current plugin
session and logs a warning; restart after resolving it. Garden features remain
available. Do not treat a successful build or logic checks as proof of UI integration.
