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
   the native slot closure. BodyPartPickerContract requires exact insertion or
   replacement callback/predicate pairs on one UIAutopsyWindowData instance and
   reads its validated selectedItemType field. EmbalmingPickerContract requires
   TryEmbalm/CanEmbalm on the same UIEmbalmWindowData instance.
   GraveDecorationPickerContract requires OnGraveItemSelected/IsGraveItem on one
   UIGraveElementWidgetData and reads its public GraveElementType property.
   ZombieEquipmentPickerContract currently registers only UpgradeCollar with
   ZombieWgoData.CanUpgradeCollarTo, requiring the equipment owner's ZombieWgoData
   to be the predicate's exact target (these two delegates have different owners).
   Unknown predicates, multicast delegates, and unsupported
   actions use vanilla.
2. Prefix on `InventoryWidget.OnRedraw`. It only handles the mod's
   `ApplicableInventoryWidgetData` subclass. Normal section data is untouched.

The original real MultiInventory is retained. The new display holder has its own
list of references to real representative stacks. Only that new list is filled;
no inventory-add, merge, split, Count/UID assignment, or save operation is used
to build the display. Quantities are checked integer sums, without stack caps.

Action-specific contracts create independent PickerPolicy<Item> objects:

| Action | Included in display | Native selectability | Live setting / title |
| --- | --- | --- | --- |
| Seeds | IsSeedValid | IsSeedValid | Seeds |
| Fertilizers | Item.IsFertilizer | IsItemValid | Fertilizers |
| Alchemical Ingredients | Native type/research conditions via game survey APIs | IsItemValidForMix | Alchemical Ingredients |
| Body Parts | Matching ItemType, including disabled entries | CanInsertOrgan or CanChangeOrgan | Body Parts / six category titles |
| Embalming Fluids | ItemType.Embalm, including disabled entries | CanEmbalm | Embalming Fluids |
| Grave Decorations | gravetop or gravebot group | IsGraveItem | Grave Decorations / Tombstones or Fences |
| Zombie Equipment (Collars only) | ItemType.Collar, including disabled upgrades | CanUpgradeCollarTo | Zombie Equipment / Collars |

The session excludes empty stacks for all policies. Each policy rejects instance
properties, contents, internal inventory capacity, bag status, or durability.
Garden/alchemy actions consume by definition ID; autopsy captures a real item's
UID and removes one unit on craft confirmation, then recreates the result by ID.
Embalming consumes one real item by UID immediately on selection and leaves craft
effects/definition-based pocket insertion to the native callback and completion.
Grave decorations choose a native craft by full definition ID; the craft system
removes recipe quantities through its own multi-inventory, not the clicked UID.
Collar upgrades remove one source unit by UID through their original inventory
list, recreate the new collar by definition, and remove the old collar natively.
Unknown instance variants cause the entire picker
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
planting, application, mastery checks, recipe quantities, and autopsy operations
stay with the game. See [BODY_PARTS.md](BODY_PARTS.md) for confirmation timing,
variant safeguards, and the distinction between corpse insertion and zombie replacement.
See [EMBALMING.md](EMBALMING.md) for fluid rules, special zombie effects, and native
full-pocket/craft-start behavior, all left unchanged.
See [GRAVE_DECORATIONS.md](GRAVE_DECORATIONS.md) for ID-based craft selection,
native category-only eligibility, and the unchanged removal/replacement flow.
See [ZOMBIE_EQUIPMENT.md](ZOMBIE_EQUIPMENT.md) for collar owner matching, player/bag
access without zone storage, native upgrade rules, and deferred equipment support.

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
`Alchemical Ingredients = true`, `Body Parts = true`, `Embalming Fluids = true`,
`Grave Decorations = true`, and `Zombie Equipment = true`
entries. The keys are the GUI labels; no configuration-manager dependency or
custom metadata type is required. Each policy reads only its own live setting.
Disable takes effect on the next plugin frame for that action's open picker;
enable takes effect when its next picker opens. Existing values are retained.
The alchemy option also controls the laboratory counter correction described in
[ALCHEMY.md](ALCHEMY.md); open laboratory labels respond to live toggle changes.

Body Parts controls all six category titles and both autopsy picker paths through
one live setting. Its separate initialization failure leaves other actions active.
Embalming Fluids also has an independent contract initialization and live setting.
Grave Decorations has the same isolation, with one live setting for both titles.
Zombie Equipment currently controls Collars only; other equipment remains vanilla.
Its contract initialization failure cannot disable earlier action contracts.
Future actions should get their own settings and explicit delegate registrations,
then a reviewed selection policy. Keep the action gates: each action's consumption
and equipment transfers have different contracts.

## Remaining validation

Successful compilation and the pure-logic checks do not validate Harmony runtime
installation, Unity prefabs, pooled component behavior, or controller input.
The user validated Seeds, Fertilizers, Alchemical Ingredients, Body Parts, and
Embalming Fluids in-game before the Grave Decorations extension. Grave Decorations
and regression testing require in-game checks.
Automated checks cover
all existing aggregation cases plus fertilizer inclusion/disabled resolution,
availability-only refreshes, stale selection rejection, live independent settings,
slot-closure recognition, validation of disabled item variants, alchemy predicate
isolation, repeated ingredients, temporary quantity override cleanup, and the
production body-part contract using test-only game stand-ins. The latter exercise
both action pairs, all six categories, disabled entries, instance safety, real
source resolution, and the shared live setting. The production embalming contract
is checked with test-only game stand-ins for action isolation, disabled visibility,
variant safety, live native eligibility, real sources, and the independent setting.
The production grave-decoration contract is checked with test-only game stand-ins
for both groups/titles, action ownership, instance/variant safety, native disabled
states, real sources, and its shared setting. The production collar contract is
checked with test-only stand-ins for linked delegate owners, unsupported equipment
isolation, disabled visibility, variants, instance safety, and real sources.
Collars still needs in-game validation. Settings checks cover all 128
combinations of the seven independent options.
The game and saves were not launched or modified during development. Follow the
manual checklist in README.txt using the user's chosen in-game test setup.
