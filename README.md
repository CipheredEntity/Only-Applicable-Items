# Only Applicable Items

**A quality-of-life mod for Graveyard Keeper 2 that simplifies item selection by showing you only what you need, wherever it's stored.**

Tired of scrolling through multiple inventories and bags just to find the items you need for an action?

Only Applicable Items replaces supported item-selection menus with consolidated, context-specific inventories. Instead of searching through your backpack, bags, and nearby storage individually, you'll see a single organized selection of relevant items, with their quantities combined.

The mod is designed to make everyday interactions faster and more convenient while preserving the game's existing mechanics.

## Features

- **Context-specific inventories:** See relevant items for the action you're performing, without unrelated inventory clutter.
- **Combined quantities:** Identical items stored across multiple inventories appear together with their total available quantity.
- **Native item restrictions:** Items that belong to an action but cannot currently be used remain visible and disabled where appropriate.
- **Independent settings:** Enable or disable each supported action individually.
- **Customizable names:** Rename any of the virtual inventories to suit your preferences.
- **Non-destructive organization:** Items remain in their original locations. Consolidation does not physically move or merge stored items.

### Supported Actions

| Action | What Changes |
| --- | --- |
| **Seeds** | Shows eligible seeds when planting crops. |
| **Fertilizers** | Shows owned fertilizers, including already-applied types as disabled entries. |
| **Alchemical Ingredients** | Consolidates ingredients and corrects laboratory ingredient quantity displays. |
| **Body Parts** | Organizes body parts into appropriate categories when inserting or replacing them. |
| **Embalming Fluids** | Shows available embalming fluids, including unusable fluids as disabled entries. |
| **Grave Decorations** | Separates available tombstones and fences when decorating graves. |
| **Zombie Equipment** | Organizes collars, armor, weapons, and tools when equipping zombies. |
| **Resurrection Collars** | Shows owned collars during resurrection, with unsuitable collars disabled. |
| **Prayers** | Consolidates available prayers when preparing sermons. |
| **Study Table** | Separates items into Study and Decompose categories. |
| **Shop Selling** | Shows items relevant to the current merchant, including items they may buy at higher trading tiers. |
| **PR Machine** | Consolidates resources such as Hypno-devices and Newspapers, allowing quantities to be loaded across multiple stacks. |

### Additional Fix: Window Sorting Order

Includes an optional fix for a game interface issue that can cause tooltips to appear behind other windows after repeatedly opening nested menus.

This fix is enabled by default and can be disabled independently of the inventory features.

## How It Works

When you open a supported item-selection menu, Only Applicable Items gathers relevant items from the inventories the game normally makes accessible for that action.

This typically includes your backpack, directly held bags, and accessible nearby storage. Some actions, such as zombie equipment selection and shop selling, retain their original restrictions and do not include nearby storage.

Identical item definitions are displayed together with combined quantities, while different qualities and item variants remain separate.

The mod preserves native requirements and restrictions wherever possible. Unsupported interactions and incompatible inventory situations fall back to the game's original interface.

## Installation

Only Applicable Items uses the official modding support included with Graveyard Keeper 2. You do not need to install a separate copy of BepInEx.

### Manual Installation (Windows)

1. Download the latest release of Only Applicable Items.
2. Extract the `OnlyApplicableItems` folder into the game's code-mod directory:

   ```text
   %USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods\Code\
   ```

3. Your installation should contain the following structure:

   ```text
   Mods/
   └── Code/
       └── OnlyApplicableItems/
           ├── modinfo.json
           └── BepInEx/
               └── plugins/
                   └── OnlyApplicableItems/
                       └── OnlyApplicableItems.dll
   ```

4. Start or restart Graveyard Keeper 2.
5. Verify that Only Applicable Items appears in the game's Mod Menu and is enabled.

**Important:** Avoid installing duplicate copies of the plugin through different installation methods.

## Configuration

All features are enabled by default.

The mod provides three configuration categories:

- **Actions:** Enable or disable individual gameplay integrations.
- **Inventory Names:** Customize the displayed name of each virtual inventory. Leave a name blank to use its default.
- **Fixes:** Enable or disable the Window Sorting Order fix.

**Recommended:** Install [GK2 Mod Menu](https://www.nexusmods.com/graveyardkeeper2/mods/216) to conveniently configure Only Applicable Items through an in-game interface and change settings without restarting the game. GK2 Mod Menu is optional; Only Applicable Items works without it.

Alternatively, settings can be edited directly through the BepInEx configuration file:

```text
BepInEx/config/cipheredentity.gk2.onlyapplicableitems.cfg
```

The configuration file is generated after the plugin first loads.

Changes to inventory names take effect the next time the corresponding selection menu opens. Most action settings also take effect without restarting the game.

For a complete explanation of the settings, see the [Configuration Documentation](docs/CONFIGURATION.md).

## Compatibility

- **Game:** Graveyard Keeper 2
- **Developed for:** Version 1.012
- **Framework:** The game's included BepInEx 5 modding support
- **Additional required mods:** None

Only Applicable Items is designed to work alongside other mods where possible. If an unsupported inventory layout or unsafe item configuration is detected, the affected feature attempts to preserve or restore the original interface rather than risk modifying items incorrectly.

Compatibility with future game updates and every other mod cannot be guaranteed.

## Issues and Feedback

If you encounter a bug, unexpected behavior, or a compatibility problem, please report it through the repository's GitHub Issues page.

When reporting a problem, please include the game version, the affected feature, a description of what happened, and steps to reproduce it if possible.

## Development

Only Applicable Items is written in C# for Graveyard Keeper 2's BepInEx modding framework.

For information about the project's architecture, development environment, building from source, and testing, see the [Development Guide](docs/DEVELOPMENT.md).

Detailed technical documentation for individual features is also available in the [`docs`](docs/) directory.

### AI Assistance

Only Applicable Items was developed with the assistance of **OpenAI Codex**, an AI coding agent. Codex was used to assist with implementation, debugging, testing, and documentation. The project's direction, feature decisions, and in-game validation were guided and reviewed by the project maintainer.

## License

Only Applicable Items is licensed under the **GNU General Public License v3.0 (GPL-3.0-only)**.

You are free to use, modify, and redistribute this mod under the terms of the license. Distributed modified versions and other derivative works covered by the GPL must comply with its licensing and source-code requirements.

See the [LICENSE](LICENSE) file for the full terms.
