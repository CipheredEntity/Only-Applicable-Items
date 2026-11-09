Only Applicable Items 0.1.0
===========================

A BepInEx 5 plugin for Graveyard Keeper 2. Garden seed selection displays one
"Seeds" inventory containing eligible seeds and combined quantities from the
player, bags, and accessible zone storage. Planting still uses the game's own
eligibility, quantities, and consumption. Other selection actions are unaffected.

Configuration
-------------
After the plugin first loads, BepInEx creates:
  <game>/BepInEx/config/ciphe.gk2.onlyapplicableitems.cfg

  [Actions]
  Seeds = true

Set Seeds to false to use the original picker. No configuration-manager plugin
is required. Compatible configuration managers can expose the built-in entry
as "Seeds". A live setting change disables an open consolidated picker on the
next frame; enabling applies to the next opening. For manual file edits, close
the game, edit the file, and launch again.

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
overflow recovery, snapshot changes, and exact delegate gating. They do not load
or execute the game. Details of the game integration are in docs/IMPLEMENTATION.md.

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
1. Confirm BepInEx/LogOutput.log shows the plugin loaded with Seeds enabled.
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
7. Open fertilizer, organ, alchemy, and normal inventory screens immediately
   after closing Seeds. They must keep their original behavior.

The prototype falls back to vanilla for unexpected instance properties on seeds
or an unsupported picker contract. These cases appear once per failure category
in LogOutput.log. Definition-ID planting does not support selecting a particular
same-ID property variant; the mod deliberately does not invent consumption rules.

In-game behavior still requires manual validation. Do not treat a successful
build or pure-logic checks as proof that Unity UI/Harmony integration works.
