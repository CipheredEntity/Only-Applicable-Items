# Garden Seeds and Fertilizers

[Development guide](DEVELOPMENT.md) · [Configuration](CONFIGURATION.md)

Two independent default-on settings control the garden pickers:
`[Actions] Seeds` and `[Actions] Fertilizers`. Their stable IDs and default titles
are `Seeds` and `Fertilizers`. The corresponding independent inventory-name entries
use those same keys. Names apply on the next opening; changing a name never changes
eligibility. Each setting restores its own open picker when disabled, leaving the
other action alone.

## Native integration and inventory access

`GardenPickerContract` validates the shared picker constructor, inventory redraw
notification, and native garden delegates. The fertilizer predicate is optional:
a missing fertilizer contract leaves working Seeds available. No garden crafting,
bed-perk, or inventory-removal method is patched.

Both pickers use the game's default `MultiInventory`: player inventory, qualifying
current-world-zone storage, and the directly held bags exposed by
`InventoryWidgetDataHelper`. The shared session retains those roots, groups plain
items by full definition ID, deduplicates reference aliases, and displays checked
totals without normal stack caps. It does not move or merge stored stacks.

Included items with serialized properties, durability, contents, bag status, or
internal capacity cause vanilla fallback, including visible disabled fertilizer.
Unrelated items are excluded before validation. Unsupported callbacks/layouts or
foreign section ownership follow the shared safeguards in the development guide.

## Seeds

`UIGardenBedWindow.OnSeedItemCellPress` constructs the picker with `OnSeedSelected`
and `IsSeedValid` on the same window. Exact native methods and owner must match.
The native predicate controls both visibility and selection: it rejects empty
items, seeds that fail `GardenInteractionHandler.IsSeedableSeed` for that bed, and
items without an applicable garden craft with requirements. Invalid seeds remain
hidden; the mod does not maintain its own seed lists or unlock checks.

Selecting resolves a currently accessible real source and invokes the original
callback/audio wrapper once. `OnSeedSelected` retains a new definition-only seed
and its craft, not the clicked UID or aggregate quantity. Selection/cancellation
does not consume seeds. Planting later uses `TryApplySeed` and `FormNeedItems`;
normal requirements are removed through native crafting inventories, including
distributed stacks. Mastery, bed type, assigned orders, quantities, success, and
growth remain native. The displayed representative need not be the consumed stack.

## Fertilizers

`UIGardenBedWindow.TryApplyFertilizer` opens with `IsItemValid` and a callback
capturing the clicked fertilizer slot. The matcher requires the exact native
predicate on a garden window and a single instance callback; requiring equal
callback/predicate targets would reject this native closure. No generated closure
name is matched.

Visibility uses `Item.IsFertilizer`; selectability uses native `IsItemValid`.
That predicate rejects fertilizer IDs already referenced by active bed perks.
Owned copies therefore remain visible and aggregated but gray and unselectable.
Availability changes refresh even when stock totals do not change, and selection
rechecks eligibility before a draw can reset the cell's interaction state.

The native callback immediately runs `AddFertilizerPerk` for the captured slot.
It may remove an existing slot perk, finds the garden craft, and calls
`GardenInteractionHandler.TryApplyFertilizer`, using `FormNeedItems` and
`ProcessInstantCraft`. Native recipes determine the cost and can draw it from
distributed stacks. There is no new mod consumption or confirmation dialog.
Closing without choosing consumes nothing; selecting is an immediate application,
unlike seed selection. Existing slot replacement order, assigned-order gating,
worker handling, perks, and failure behavior are preserved.

## Validation and manual checks

`tests/Program.cs` exercises aggregation, complete-ID separation, alias/overflow
handling, garden delegate ownership, fertilizer slot closures, disabled visibility,
availability-only changes, stale-click rejection, and validation of disabled
instances. Session/view compatibility regressions cover the common rendering,
restoration, and ownership path. These checks use stand-ins; they do not execute
the actual garden predicate, planting, fertilizer crafting, or Unity UI.

In-game regression checklist:

1. Split eligible seeds across player/bags/zone storage, including 20+35+50=105.
   Verify one Seeds view, separate full-ID variants, correct totals, and the native
   bed/vineyard rules. Selecting and cancelling must consume nothing.
2. Plant a recipe whose cost exceeds any individual stack but fits combined stock.
   Verify its exact native cost, normal mastery/growth behavior, and no source moves.
3. Open Fertilizers with several types/variants. Apply one, reopen, and verify
   remaining copies stay visible but disabled for mouse and controller input.
4. Test usable fertilizer on empty/occupied slots, distributed recipe costs,
   cancellation, assigned orders, and repeated openings. Verify immediate
   application and compare replacement/perk behavior with vanilla.
5. Check empty results, depletion, changed stock, added/removed bags, scrolling,
   tooltips, controller focus, Back/close, and pooled reuse in other inventories.
6. Test all four Seeds/Fertilizers toggle combinations and live disabling of each
   open picker. Re-enable for its next opening, test names, then exercise supported
   and unsupported interfaces to verify isolation.
