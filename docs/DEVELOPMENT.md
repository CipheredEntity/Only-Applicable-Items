# Development guide

This guide describes **Only Applicable Items 0.15.0** for **Graveyard Keeper 2
1.012**. It is the entry point for architecture, local development, and validation.
[CONFIGURATION.md](CONFIGURATION.md) is the authoritative reference for settings,
names, and live changes. Feature documents below own the action-specific details.

## Purpose and architecture

The mod consolidates contextual item-selection inventories into action-specific
views. Equivalent plain items share a row and show their combined quantity, even
above the normal stack limit. The actual items stay in their original inventories.
Ordinary inventory management and unsupported actions retain their native UI.

The main components are:

| Location | Responsibility |
| --- | --- |
| [Plugin.cs](../src/Plugin.cs) | Configuration, contract discovery, Harmony registration, update scheduling, and shutdown |
| [Core](../src/Core) | Stable picker identities/default titles, policies, checked aggregation, snapshots, and display/sorting helpers |
| [Integration](../src/Integration) | Named native method/field contracts, delegate matching, Harmony entry points, and item validation |
| [UI](../src/UI) | Shared picker sessions, action-specific display corrections, and separate trading/PR adapters |
| [Configuration](../src/Configuration) | Independent action/fix settings, per-inventory names, optional menu metadata, and file reload |
| [tests](../tests) | Production-code regression harness and a separate shipped-Mono runtime probe |

Most actions use two shared Harmony entry points: a postfix on the supported
`UIMultiInventoryWindowData` constructor, and a prefix on `InventoryWidget.OnRedraw`.
Contracts recognize native callbacks and eligibility predicates, including their
owners, before supplying a `PickerPolicy<Item>`. Matching does not depend on
compiler-generated lambda names or displayed inventory titles.

`PickerPolicy` separates display membership from selectability and carries the
item validator and optional native-warning provider. `PickerSnapshot` and
`StackAggregation` retain source references and quantities. `ApplicableItemsSession`
publishes virtual `ApplicableInventoryWidgetData` views using those snapshots.
Study Table supplies two ordered policies; other shared pickers use one.

Shop Selling uses a dedicated trading session rather than the shared picker.
PR Machine reuses the shared view but adds a scoped quantity/consumption adapter.
Display corrections and the global sorting fix have their own narrowly scoped
owners. Their exact patches and exceptions to the common flow belong in the
feature documents, not in a universal selection or consumption implementation.

## Shared picker flow and lifetime

1. The original game constructor creates its `MultiInventory`, sections, native
   callbacks, and eligibility predicate. Configuration file changes are checked
   at this opening boundary, then the recognized action's setting and names apply.
2. The session validates the original layout and callbacks. It enumerates only
   the original inventory roots and their directly held bags, excluding empty
   stacks. It reads bag contents directly; calling `GetInventoryFromBag` here
   would assign the real bag's inventory size. No deeper bag traversal is added.
3. Policies filter and validate items, and group by full definition ID. Checked
   totals prevent overflow; reference identity prevents duplicate counting.
   All section snapshots and views are built before replacing any section.
4. New display holders contain references to real representatives. Only the
   cell's display values show aggregate counts. The native widget's availability
   mechanism supplies disabled appearance and input blocking.
5. A click rebuilds sources and rechecks selectability, rebinds to a current real
   representative, and dispatches the original callback/audio wrapper once.
   PR alone uses the optional `SelectionDispatch` to establish its loading scope.

Inventory notifications mark sessions dirty. A half-second poll also catches
direct count edits, bag changes, and availability/warning changes without an event.
Unchanged snapshots do not redraw. Refresh replaces the coordinated data views and
uses the child widget's `OnItemsAvailabilityChanged` redraw, preserving its section
count. Re-running the window's `Draw`/setup path during refresh would create
duplicate pooled sections. Native redraw notifications update controller navigation.

Hide/disposal removes source subscriptions, the hide handler, and plugin tracking.
Live disabling or recoverable failure rebuilds owned vanilla sections with the
native helper; enabling consolidation waits for the next opening. If owned
restoration fails, the picker closes instead of retrying a gameplay action.

## Development principles and compatibility

- **Keep display data virtual.** Do not change stored counts, UIDs, ownership,
  properties, or stack limits merely to show combined quantities. A display total
  is not permission to consume that quantity.
- **Preserve meaningful differences.** Full IDs retain definition/quality variants.
  `PlainItemValidator` rejects included items with serialized instance properties,
  durability, contents, internal capacity, or bag status. Disabled visible items
  receive the same validation. Separate rows cannot repair native ID-based
  transfers that discard instance data. Equipment and PR add stricter safeguards.
- **Reuse native rules deliberately.** Inventory access usually means the player,
  qualifying current-world-zone storage, and directly held bags, not a new radius
  search. Zombie equipment and trading exclude zone storage. Eligibility,
  quantities, UID-vs-ID selection, and consumption timing differ by action.
  Preserve native rules unless an explicitly scoped feature says otherwise.
- **Respect other UI owners.** Shared section ownership requires the exact count,
  order, and view references published by the session. Foreign takeover detaches
  our handlers without replacing foreign headers, selection, or sections. Check
  ownership again after callbacks. Unsupported secondary/down callbacks, hidden
  filters, custom selection states, or incompatible layouts retain vanilla.
- **Do not replay gameplay.** Pre-dispatch display/validation failures can restore
  UI. Once native gameplay starts, exceptions are not caught to retry the action.
  PR's prepared consumption and deferred notifications have their own failure
  boundary; trading still uses native non-atomic transactions.
- **Keep patches and settings isolated.** Use explicit contracts and only the
  required native entry points. Feature-specific startup failure should leave
  other integrations available. Some dependencies are shared: losing the shared
  picker contract disables its dependent pickers, while trading and sorting
  initialize independently. Optional display/warning failures have documented
  action-specific behavior.
- **Treat startup as a separate phase.** Game balance may be unavailable in
  `Awake`. PR accessor warm-up uses detached parameterless `Item()`/`Inventory()`
  objects, never the definition-resolving named item constructor. Do not force
  balance loading just to validate fields or patch methods.

Pooled UI must restore its normal draw state when reused. Keep display-only
changes separate from actual quantities and restore temporary caches in finalizers.
Logs use stable identities and failure categories, never custom names as identity.
Warnings are generally limited to once per category per plugin lifetime.
Initialization messages in `BepInEx/LogOutput.log` establish feature readiness;
having a configuration entry does not prove that its integration initialized.

After a game update, compare the actual contracts, fields, callbacks, inventory
semantics, and relevant IL against the versioned reference. Ignore metadata-token
and generated-name churn. Recheck serialized balance data and Unity prefab/pooling
behavior in-game where source or stand-ins cannot establish it.

## Supported actions

These are default titles; configurable names and exact keys live in
[Configuration](CONFIGURATION.md).

| Action | Default view(s) and brief scope | Detailed reference |
| --- | --- | --- |
| Seeds | Seeds; native garden seed eligibility | [Garden](GARDEN.md#seeds) |
| Fertilizers | Fertilizers; applied types remain visible but disabled | [Garden](GARDEN.md#fertilizers) |
| Alchemical Ingredients | Researched ingredients, disabled rune-limit entries, laboratory totals | [Alchemy](ALCHEMY.md) |
| Body Parts | Brains, Hearts, Guts, Skulls, Bones, Skins; insertion/replacement | [Body Parts](BODY_PARTS.md) |
| Embalming Fluids | Fluids, disabled entries, native collar-limit warning | [Embalming](EMBALMING.md) |
| Grave Decorations | Tombstones and Fences; native placement/removal flow | [Grave Decorations](GRAVE_DECORATIONS.md) |
| Zombie Equipment | Collars, Armor, Weapons, Tools; native equipment operations | [Zombie Equipment](ZOMBIE_EQUIPMENT.md) |
| Resurrection Collars | Collars; separate resurrection selection and selected-slot display | [Resurrection Collars](RESURRECTION_COLLARS.md) |
| Prayers | Prayers; native sermon definitions, execution unchanged | [Prayers](SERMONS.md) |
| Study Table | Study and Decompose; coordinated sections and study-only visuals | [Study Table](STUDY_TABLE.md) |
| Shop Selling | Sellable; merchant relevance and guarded native offers | [Shop Selling](SHOP_SELLING.md) |
| PR Machine | PR Resources; full combined loading with native effects once | [PR Machine](PR_MACHINE.md) |

[Window Sorting Order](WINDOW_SORTING.md) is a separate global fix, including
windows outside these actions. It changes the allocation counter, not existing
canvases, and has independent configuration and compatibility boundaries.

## Prerequisites and external references

Use the [official modding contract](<../../Documentation Reference/README_MODDING.txt>)
and [AGENTS.md](../AGENTS.md) for local development rules. The supported plugin
framework is BepInEx 5 for Unity Mono; this project targets `net472`, not BepInEx 6
or IL2CPP. The checks project targets `net10.0`, so the local workflow requires a
.NET 10 SDK. The standalone probe additionally requires Windows x64 and the
installed game's shipped Mono. Recorded local tooling is BepInEx 5.4.23.5,
Mono 6.13, and Harmony assembly version 2.9.0.0; inspect installed copies after updates.

Keep all proprietary references outside the repository. Paths below are relative
to the repository root, not to `docs/`:

| Reference | Location |
| --- | --- |
| Game/Unity/BCL assembly copies | `../Game Assemblies Copies/1.012/Managed/` |
| Decompiled gameplay source | `../Decompiled Source/1.012/Assembly-CSharp/` |
| Decompiled framework/controller source | `../Decompiled Source/1.012/LazyBearTechnology/LazyBearTechnology/` |
| BepInEx/Harmony tooling | `<game>/BepInEx/core/` |
| Prior comparison baseline only | Matching `1.011` reference directories |

`OnlyApplicableItems.csproj` defaults to the current assembly copies. The runtime
probe uses the same version and links the external controller source in place.
It does not copy decompiled source into this repository. If retargeting, update
both projects and the probe's separate controller link together.

The default installation is the Program Files (x86) Steam library. Override local
paths in the git-ignored `OnlyApplicableItems.local.props`, for example:

```xml
<Project>
  <PropertyGroup>
    <GameInstallPath>D:\SteamLibrary\steamapps\common\Graveyard Keeper 2</GameInstallPath>
  </PropertyGroup>
</Project>
```

`GameManagedPath` and `BepInExCorePath` also accept local-property or MSBuild
`-p:Name=Value` overrides. The projects reference Unity's BCL copies, have no NuGet
package dependencies, and use a local restore source rather than downloading a
targeting pack. Do not install dependencies or modify external references as an
incidental part of development.

## Build and checks

Run from the repository root in PowerShell. Stop on a failed command before using
its outputs or launching the probe; a stale executable is not validation of a
failed build.

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'

dotnet build OnlyApplicableItems.csproj -c Release
dotnet run --project tests/OnlyApplicableItems.Checks.csproj -c Release
dotnet build tests/RuntimeProbe/RuntimeProbe.csproj -c Release
# Run only after the probe build succeeds; substitute your installation path:
& 'tests/RuntimeProbe/bin/Release/net472/RuntimeProbe.exe' `
  'C:/Program Files (x86)/Steam/steamapps/common/Graveyard Keeper 2' `
  (Resolve-Path '../Game Assemblies Copies/1.012/Managed').Path
```

The logic harness also accepts exact test names, for example:

```powershell
dotnet run --project tests/OnlyApplicableItems.Checks.csproj -c Release -- SharedPickerForeignSectionsSurviveRestore
```

| Validation | What it establishes | What it does not establish |
| --- | --- | --- |
| Release build | Compilation against the selected external references | Runtime startup, UI binding, or actual game behavior |
| `tests/OnlyApplicableItems.Checks.csproj` | Production core/contracts/sessions/display adapters against in-memory boundaries | Native game predicates/effects, Unity pooling/rendering/input, or Harmony installation |
| `tests/RuntimeProbe` controller checks | Actual Harmony hooks on shipped Mono, with externally linked controller source and stand-in windows/canvases | Actual Unity windows, animation timing, or arbitrary other mods |
| PR runtime checks | Actual PR bridge/raw accessors on an in-memory effect boundary, cold-start access, reentrancy and cleanup | Execution of the real PR effect body or serialized balance expressions |
| Metadata/configuration probe checks | Copied assembly contracts/IL, native constructor dependency shapes, real BepInEx persistence/reload, optional GK2 Mod Menu model/metadata | Unity menu rendering or a complete `Plugin.Awake`/game-start sequence |

The last recorded automated baseline before this documentation reorganization
passed 195 logic checks, seven controller checks, nine PR checks, and the
configuration/metadata checks. Use each run's actual output as the current result.
The named test registry and feature test files are the coverage authority. The
legacy exhaustive action-policy test covers 11 toggles; separate PR/configuration
tests cover the twelfth. Do not describe that as all combinations of 12 actions.

The probe is a standalone host, not the game. It executes no methods from loaded
game assemblies, launches no Unity scene, and touches no save. Controller source
is recompiled into its fixture; PR effects are modeled. Real BepInEx test configs
are isolated under probe output and cleaned up. Optional menu-model checks report
a skip if the menu DLL is absent. Tooling DLLs remain in ignored test output.

## Artifacts and manual release validation

The main DLL is `bin/Release/net472/OnlyApplicableItems.dll`. Release builds also
refresh this local item layout:

```text
artifacts/OnlyApplicableItems/
  modinfo.json
  BepInEx/plugins/OnlyApplicableItems/OnlyApplicableItems.dll
```

Game, Unity, BCL, BepInEx, Harmony, and test outputs must not be packaged. No build
step installs the mod or starts the game. Keep plugin, project, and manifest
versions consistent when intentionally changing the release version.

For user-authorized manual testing, the official local item route is the item
folder under `%USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods\Code\`.
Alternatively, direct BepInEx testing uses just the DLL under
`<game>/BepInEx/plugins/OnlyApplicableItems/`. Use one route to avoid duplicate
copies; the game's item sync can replace a directly copied DLL. Restart to load a
new build and verify the loaded version and readiness messages in `LogOutput.log`.

Each feature document has its own focused checklist. Across a release, also check
mouse/controller navigation, empty results, pooled reuse between features and
vanilla inventories, live toggles, custom names, scene/window transitions, and
other UI mods. Compare real before/after quantities and native effects; test both
cancellation and completion. Configuration layout/persistence and global layering
have their own checklists.

Earlier user reports of working features are not a complete 0.15.0/1.012 release
test record. PR's post-fix activation is not yet documented; follow its
[startup and loading checks](PR_MACHINE.md#startup-and-readiness). Manual checklists
describe required regression coverage, not invented pass results.
