# Only Applicable Items — Codex Instructions

## Project Overview

Only Applicable Items is a Graveyard Keeper 2 code mod that improves contextual item-selection interfaces by consolidating eligible items from multiple accessible inventories into a single action-specific display.

The mod is developed in C# using the game's supported modding framework.

## Project References

- `../Documentation Reference/README_MODDING.txt` contains the game's official modding instructions and should be consulted when configuring the project or implementing game integration.

- The current target game version is **1.012**.

- `../Decompiled Source/1.012/` contains the current
  decompiled game source for investigation and integration.

- `../Game Assemblies Copies/1.012/Managed/` contains
  the current compiled game assemblies for reference
  and build dependencies.

- The corresponding `1.011` directories contain the
  previous game version and should be used only when
  comparing changes or investigating regressions.

- Always prioritize the current target version when
  developing features or checking compatibility.

- All file creation and modification should occur only within the `Only Applicable Items` repository. All sibling directories are read-only.

Prioritize the current game's official modding documentation over older third-party setup instructions.

## Development Guidelines

- Develop features incrementally, beginning with garden seed selection.

- Keep support for individual gameplay actions isolated and independently configurable.

- Keep virtual inventory display names centralized and independent of internal inventory identifiers and gameplay logic.

- Leave unsupported item-selection interfaces unchanged.

- Reuse the game's existing item eligibility and consumption mechanisms wherever possible.

- Keep consolidated inventories virtual. Do not physically move, merge, or modify stored items merely to display them.

- Preserve meaningful differences between item instances.

- Prefer narrowly scoped changes over global modifications to game systems.

- Write clear, maintainable code without sacrificing correctness, performance, or compatibility. Prefer simple solutions where practical, but use more sophisticated approaches when technically justified. Document non-obvious implementation decisions.

## Safety and Testing

- Do not modify the game's original assemblies, decompiled reference source, or save files.

- Do not modify the game installation, install dependencies, or publish changes without explicit authorization.

- Confirm the build target and dependencies against the currently installed game's modding requirements.

- Explain significant implementation decisions and identify uncertainties.

- Run appropriate build checks when possible, and clearly distinguish successful compilation from features actually tested in-game.

- Do not commit or push Git changes unless explicitly requested.