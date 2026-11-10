# Contextual picker implementation

## Verified game contract

Reviewed against the supplied game update and the installed Steam build 26404424.
The local official `../Documentation Reference/README_MODDING.txt` specifies
BepInEx 5 for Unity Mono and net472 or netstandard2.0. The installed BepInEx
assembly is 5.4.23.5; its installed 0Harmony assembly is 2.9.0.0.

The current source still has the relevant flow:

- `UIGardenBedWindow.OnSeedItemCellPress` constructs `UIMultiInventoryWindowData`
  with `OnSeedSelected` and `IsSeedValid` delegates on the same window instance.
- `UIGardenBedWindow.TryApplyFertilizer` constructs the same picker with
  `IsItemValid` and a callback capturing the clicked fertilizer slot. IsItemValid
  is used only for fertilizer picking in the inspected garden-window source.
  `Item.IsFertilizer` identifies display candidates; IsItemValid rejects fertilizers
  referenced by active bed perks through PerkDef.fertilizerItemId.
- That constructor uses `MultiInventory` and `InventoryWidgetDataHelper` for
  player, zone storage, and directly contained bags. It wraps clicks with audio.
- `MultiInventoryWidget.SetData` creates pooled sections; `Redraw` draws existing
  sections. `LazyWidget.Draw(data)` calls SetData even if data has not changed.
- `InventoryWidget.OnRedraw` runs after the cells are filled, even when its
  notification is suppressed by the parent widget.
- `UIItemCell.Value` and `UpdateCountLabelAsRegularItem` can show a quantity
  independent of the referenced `Item.Count`.
- `OnSeedSelected` selects a definition ID. Planting later creates a requirement
  through `GardenInteractionHandler.TryApplySeed/FormNeedItems`.
  `CraftElementBase.RemoveCraftRequirements` uses the craftable object's real
  multi-inventory; `MultiInventory.RemoveItems` handles distributed quantities.
- Fertilizer selection immediately calls AddFertilizerPerk/TryApplyFertilizer,
  which uses ProcessInstantCraft and the same distributed requirements removal.
  The original closure retains slot assignment and replacement behavior.
- Closing reaches `MultiInventoryWidget.Hide`, which releases pooled sections
  and calls `MultiInventoryWidgetData.onWidgetHide`.

No architecture-breaking changes were found in this update.

## Integration and isolation

Two explicit Harmony patches are installed after checking method signatures and
the private item-property field's type:

1. Postfix on the exact `UIMultiInventoryWindowData` constructor. Both original
   delegate methods and their common target must match the seed action. The
   fertilizer branch instead requires the exact UIGardenBedWindow.IsItemValid
   predicate on a garden window and a single instance callback. Its callback
   target is a slot closure, so comparing it to the predicate's target would
   incorrectly reject the native action. No compiler-generated names are used.
   Alchemy uses its exact UIAlchemyWindow.IsItemValidForMix predicate and preserves
   the native slot closure. Unknown predicates, multicast delegates, and unsupported
   actions use vanilla.
2. Prefix on `InventoryWidget.OnRedraw`. It only handles the mod's
   `ApplicableInventoryWidgetData` subclass. Normal section data is untouched.

The original real MultiInventory is retained. The new display holder has its own
list of references to real representative stacks. Only that new list is filled;
no inventory-add, merge, split, Count/UID assignment, or save operation is used
to build the display. Quantities are checked integer sums, without stack caps.

GardenPickerContract creates independent PickerPolicy<Item> objects:

| Action | Included in display | Native selectability | Live setting / title |
| --- | --- | --- | --- |
| Seeds | IsSeedValid | IsSeedValid | Seeds |
| Fertilizers | Item.IsFertilizer | IsItemValid | Fertilizers |
| Alchemical Ingredients | Native type/research conditions via game survey APIs | IsItemValidForMix | Alchemical Ingredients |

The session excludes empty stacks for both policies. Each policy rejects instance
properties, contents, internal inventory capacity, bag status, or durability.
All three actions consume by definition ID. Unknown variants cause the entire picker
to fall back rather than silently combine them, including when a visible
fertilizer is currently disabled. Ordinary items group by their full definition
ID, preserving definition-based quality differences.

The original CustomItemsAvailableCondition is retained on the virtual widget
data, so InventoryWidget.Redraw and UIItemCell implement disabled appearance and
input blocking. The quantity decorator changes counters only, not widget state.

The click wrapper rebuilds and revalidates sources, rebinds the cell to a real
current representative, and invokes the original click/audio callback once.
Fresh snapshots only resolve selectable entries. This is essential for fertilizers:
visibility alone must never authorize application, especially before cell.Draw
can reset interaction state. Game callback exceptions are not caught and retried.
The plugin never consumes items or changes bed perks. Native slot replacement,
planting, application, mastery checks, and recipe quantities stay with the game.

## Refresh and lifetime

The game access list is captured from the original MultiInventory. Each scan
reads direct root contents and the contents of directly held bags, matching the
game helper without modifying bag InventorySize. It does not recursively count
bags twice. Reference identity deduplicates aliases; source records preserve
container and stack references and captured counts.

Subscriptions to accessible inventory and original bag wrapper notifications
mark sessions dirty. A half-second scan also catches direct Count changes and
new/removed bags without relying on a particular wrapper's events. PickerSnapshot
also captures per-group selectability. Perk changes therefore trigger refresh
even when stock is identical. Unchanged snapshots do not redraw. Main-thread
LateUpdate coalesces updates.

Refresh replaces the single section's data and calls OnItemsAvailabilityChanged,
which the game binds to the child widget's Redraw. This preserves the section
count and lets InventoryWidget.SetData unsubscribe from its old display holder.
It does not call the shared window's Redraw, which would add duplicate sections.
Availability changes use that full child redraw, not just SetWidgetState: the
native UIItemCell.Draw also updates its separate isInteractable flag.

onWidgetHide disposes source subscriptions and restores original data references.
Owner destruction/reuse and plugin teardown also clean up. Disabling the session's action or
a recoverable runtime failure releases the child sections and redraws vanilla
sections generated by the game's original helper. If that restoration itself
fails, the picker is closed rather than retrying an item action.

Warnings are limited to one per failure category per plugin lifetime. No
per-frame or per-item informational messages are emitted.

## Configuration and future scope

Config.Bind creates independent `[Actions] Seeds = true`, `Fertilizers = true`,
and `Alchemical Ingredients = true` entries. The keys are the GUI labels; no configuration-manager dependency or
custom metadata type is required. Each policy reads only its own live setting.
Disable takes effect on the next plugin frame for that action's open picker;
enable takes effect when its next picker opens. Existing values are retained.
The alchemy option also controls the laboratory counter correction described in
[ALCHEMY.md](ALCHEMY.md); open laboratory labels respond to live toggle changes.

Future actions should get their own settings and explicit delegate registrations,
then a reviewed selection policy. Do not remove the action gates: UID-based
consumption and equipment transfers have different contracts.

## Remaining validation

Successful compilation and the pure-logic checks do not validate Harmony runtime
installation, Unity prefabs, pooled component behavior, or controller input.
The user validated Seeds and Fertilizers in-game before the alchemy extension.
The new alchemy path and regression testing still require in-game checks. Unit checks cover
all existing aggregation cases plus fertilizer inclusion/disabled resolution,
availability-only refreshes, stale selection rejection, live independent settings,
slot-closure recognition, validation of disabled item variants, alchemy predicate
isolation, repeated ingredients, and temporary quantity override cleanup.
The game and saves were not launched or modified during development. Follow the
manual checklist in README.txt using the user's chosen in-game test setup.
