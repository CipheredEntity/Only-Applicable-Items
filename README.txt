Only Applicable Items 0.2.0
===========================

A BepInEx 5 plugin for Graveyard Keeper 2. Garden seed selection displays one
"Seeds" inventory containing eligible seeds and combined quantities from the
player, bags, and accessible zone storage. Garden fertilizer selection displays
one "Fertilizers" inventory containing all owned fertilizer types. Already-applied
fertilizers remain visible with their combined quantities, grayed out and
unselectable. Non-fertilizers are excluded. Both actions retain the game's own
selection, application, quantities, and consumption. Other actions are unaffected.

Configuration
-------------
After the plugin first loads, BepInEx creates:
  <game>/BepInEx/config/ciphe.gk2.onlyapplicableitems.cfg

  [Actions]
  Seeds = true
  Fertilizers = true

Both options are enabled by default and independent. Set either option to false
to use the original interface for that action. Existing Seeds settings are kept
when the new Fertilizers entry is added. No configuration-manager plugin is
required. Compatible managers expose the entries as "Seeds" and "Fertilizers".
A live setting change disables an open picker for that action on the next frame;
enabling applies to the next opening. For manual file edits, close the game,
edit the file, and launch again.

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
disabled fertilizer visibility, and click-time availability checks. They do not
load or execute the game. Integration details are in docs/IMPLEMENTATION.md.

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
1. Confirm BepInEx/LogOutput.log shows the plugin loaded with both actions enabled.
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
10. Check both toggles in all four combinations, including live changes. Toggling
    Seeds must not affect an open Fertilizers picker, or vice versa. Check repeated
    transitions between both pickers and stock/availability changes while open.
11. Open organ, alchemy, and normal inventory screens immediately after both
    consolidated pickers. They must keep their original behavior.

The prototype falls back to vanilla for unexpected instance properties on garden items
or an unsupported picker contract. These cases appear once per failure category
in LogOutput.log. The garden actions consume by definition ID and cannot select
a particular same-ID property variant; the mod does not invent consumption rules.

The Seeds implementation was tested in-game before the Fertilizers extension.
The extension and seed regression checks still need manual in-game validation.
Do not treat a successful build or logic checks as proof of Unity UI integration.
