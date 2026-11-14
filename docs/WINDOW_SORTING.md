# Global window sorting correction

Version 0.12.0 adds `[Fixes] Window Sorting Order = true`, independently of every
action setting and picker contract. A separate Harmony owner is initialized before
inventory integration; failure on either side does not disable the other.

## Native contract

`LazyBearTechnology.LazyWindowsStackController.AddToStack<T>` appends a window,
updates modality/active state, emits events, then assigns 400 to a root or adds 15
to `activeWindowSorting` for a nested window. The initial value is a mutable public
field, so the fix reads it rather than hardcoding 400. `RemoveFromStack<T>` removes
any matching window, emits events, and updates the active window without reducing
that counter. `UITooltip.Init` fixes the shared tooltip canvas at 700.

Window contents use reserved offsets: building tabs +4/+6, zombie content +5 and
tab labels up to +7. The fix leaves the native 15-point gap intact. End-game credits
temporarily override their own canvas to 30000; the allocator must not mistake that
rendering override for a stack assignment.

## Implementation and fallback

- `Core/WindowSortingTracker.cs` records each window opening's native allocation
  by reference identity. It has no canvas API and cannot change existing canvases.
- `Integration/WindowSortingFix.cs` validates private field types, method signatures,
  reference-type constraints, and the original increment/reset IL before patching.
  Only `AddToStack<LazyWidgetDataBase>` and `RemoveFromStack<LazyWidgetDataBase>`
  receive prefixes/finalizers, on a dedicated owner. Originals always run normally.
- The prefix validates the known stack/counter and may reconcile the counter.
  Successful additions record their exact native counter assignment. Successful
  removals reconcile to the maximum recorded allocation of surviving windows.
  Closing the last window leaves the idle counter alone; the next native root
  opening resets it. Surviving gaps are retained to avoid allocation collisions.
- Reentrancy suspends correction immediately. The original nested operations and
  events finish without replay or rollback. Finalizers balance operation depth and
  preserve native exceptions. State belongs to the originating tracker even if a
  callback disposes/reinitializes the feature.
- Duplicate/untracked membership, changed counter/base, skipped native operations,
  reflection failures, and unsafe arithmetic also suspend correction. Warnings
  are limited by the plugin's existing per-category logging. A fresh root window
  restarts tracking once the stack is empty. Unrecognized contracts fail at startup
  and leave native sorting active. No detours into canvas setters are installed.

Live disabling stops writes. Enabling an existing stack does not invent allocation
history: close all windows, then open a root. Unloading unpatches only this owner
and leaves the current counter/canvases intact; native allocation resumes from there.

## Generic-method verification

Every legal T derives from `LazyWidgetDataBase`, so every construction uses a
reference type. The shipped Mono 6.13 runtime shares their native method bodies.
Patching each concrete T independently risks replacing the same detour repeatedly;
the fix patches one canonical construction per method, with base-type arguments
and non-generic patch functions.

`tests/RuntimeProbe` embeds the installed game's Mono in a separate Windows x64
test process and uses its installed Harmony 2.9.0. It compiles the unchanged
decompiled controller source against lightweight window/canvas stand-ins and links
the production fix/tracker. No Unity engine, scene, save, or game assembly method
is executed. Cecil inspects the copied LazyBearTechnology.dll as metadata only.

Verified automatically: the canonical detour covers multiple reference-type
constructions, including a previously unused type after patching; 100 repeated
openings; native event order/counts, focus/modality; out-of-order closures; 30000
canvas overrides; exception identity; reentrancy; late initialization/live toggles;
foreign counter changes; unpatching; and disposal/reinitialization during callbacks.

From the repository in PowerShell, with the existing .NET SDK and local game:

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet build OnlyApplicableItems.csproj -c Release
dotnet run --project tests/OnlyApplicableItems.Checks.csproj -c Release
dotnet build tests/RuntimeProbe/RuntimeProbe.csproj -c Release
# Run only after the probe build succeeds:
& 'tests/RuntimeProbe/bin/Release/net472/RuntimeProbe.exe' `
  'C:/Program Files (x86)/Steam/steamapps/common/Graveyard Keeper 2' `
  (Resolve-Path '../Game Assemblies Copies/1.012/Managed').Path
```

The probe's tooling DLLs stay in ignored test output, never in the mod package.
Neither build nor probe installs the mod or launches the game. It requires the
external decompiled controller source and local runtime references; no downloads.

## Limits and in-game validation

This is allocation reuse, not global relayering. It does not compact existing gaps,
repair already-inflated open windows, change tooltips/fades, or solve genuinely deep
stacks reaching reserved layers. Unusual repeated out-of-order replacements can
carry elevated surviving assignments forward. A window/mod that remains visibly
animated after removing itself from the stack may depend on delayed order reuse;
native base closing hides synchronously, but custom behavior requires testing.

Other mods can patch the same generic native body via different constructions.
Changed state is detected conservatively, but arbitrary detours cannot be guaranteed
compatible. The probe verifies shipped Mono/Harmony and controller behavior with
stand-ins, not Unity rendering or every other mod's patches.

Manual checklist:

1. Repeat at least 100 study-table and alchemy picker cycles, including cancellation,
   with action settings on and off. Tooltips should remain above normal windows.
2. Exercise nested pause/settings/confirmation menus, inventory/trading, zombie tabs,
   conveyor selection, and all supported action pickers. Check mouse/controller
   focus, modal input blocking, and internal tab/icon layers.
3. Check credits, end-game credits if accessible, fades, dialog overlays, and the
   construction cursor without changing their intended layering or animations.
4. Disable while windows are open: future transitions should resume vanilla
   allocation. Re-enable mid-stack: wait until everything closes and a root reopens.
5. With other UI mods, inspect warnings and confirm any detected conflict falls
   back without blocking window closing, focus restoration, or gameplay.
