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

Two shared Harmony patches are installed after checking method signatures and
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
   ZombieEquipmentPickerContract registers UpgradeCollar with
   ZombieWgoData.CanUpgradeCollarTo, requiring the equipment owner's ZombieWgoData
   to be the predicate's exact target (these two delegates have different owners).
   Armor/Weapons/Tools require InsertItem or ReplaceItem paired with the exact
   IsArmor/IsWeapon/IsTool predicate on the same equipment-data instance.
   These three policies also reject the currently equipped full ID, read live from
   the zombie's Armor/Weapon/Hand getter, without hiding the aggregate entry.
   SermonPickerContract requires the exact UIPrayWindowData.CanUsePray predicate
   with a native callback declared on and bound to that same prayer-data instance.
   Both visibility and selection use CanUsePray without naming its inline callback.
   StudyTablePickerContract similarly requires IsItemAllowed on the same
   UIResourceBasedCraftWindowData owner as its native callback. The game's survey
   resolver and isScienceFuelCraft split eligible targets into two ordered policies.
   Unknown predicates, multicast delegates, and unsupported
   actions use vanilla.
2. Prefix on `InventoryWidget.OnRedraw`. It only handles the mod's
   `ApplicableInventoryWidgetData` subclass. Normal section data is untouched.

Resurrection collars additionally use an isolated prefix/finalizer on
`UIResurrectionWindowData.OnCollarPressed`. The opening scope identifies its
inline callback/predicate without compiler-generated names; both must belong to
that exact resurrection-data instance. The finalizer restores nested contexts
on success or exception and returns the original exception. It does not patch
eligibility, selection, or consumption. See RESURRECTION_COLLARS.md.

The selected resurrection collar slot also has an isolated display owner with
postfixes on UIResurrectionWindow.RedrawLite/Hide. It temporarily renders only
that slot with UIItemCell.Value=1, restores the original cached Value immediately,
and refreshes on the same Resurrection Collars toggle. It never changes stored
Count or the selected reference/UID, and installs no global UIItemCell patch.

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
| Zombie Equipment | Native slot category, including disabled entries | CanUpgradeCollarTo, IsArmor, IsWeapon, IsTool | Zombie Equipment / Collars, Armor, Weapons, Tools |
| Resurrection Collars | ItemType.Collar, including disabled entries | Original skull-range predicate | Resurrection Collars / Collars |
| Sermons | CanUsePray | CanUsePray | Sermons |
| Study Table | IsItemAllowed plus the resolved survey category | Same native eligibility/category | Study Table / Researchable, Decomposable |

Every policy has a stable PickerId independent of its displayed Title. Default
titles are centralized in Core/PickerIdentity.cs. ZombieCollars and
ResurrectionCollars both default to Collars, but use separate settings, contracts,
and diagnostic keys. Other policies retain their existing default labels. No
display-name configuration has been added; enum names, not numeric positions or
titles, identify pickers in diagnostic keys.

Sermons adds no new Harmony patches. Its original callback retains a SermonDef,
not a stack or UID, and creates one CraftElementSermon with empty item requirements.
Attendance, success chance, execution, and rewards stay in the original prayer
systems. See SERMONS.md for native scope and validation details.

The session accepts an ordered policy list, with previous actions represented by
one policy. BuildAll materializes sources once, builds independent snapshots,
and rejects duplicate identities or overlapping item membership. All snapshots
and virtual views finish before publishing any changes. Study Table keeps two
sections even when empty, using each section's own ID, header, availability, and
click route. Existing single-section actions retain the window title and hidden
section header. Multi-section pickers retain the original window caption and show
their section headers. A failure or live disabling restores the entire picker.
No additional Harmony patch or consumption logic is introduced. See STUDY_TABLE.md.

StudyResearchable additionally hides only its rendered count-label object after
normal aggregation, preserving cached totals and real items. Its validated private
label access is resolved lazily so unrelated pickers are unaffected. Both study
header data objects use an empty HeaderIconId for the game's icon-free layout;
all other header defaults remain unchanged. Native cell draws restore visibility
on pooled reuse. These visuals share the Study Table toggle and fallback lifecycle.

The selected study item slot has a separate display owner with postfixes on
UIResourceBasedCraftWindow.Redraw/Hide. The redraw postfix receives mainIngredient,
resolves its displayed ID through the native survey resolver, and hides only a
research counter. Decomposable, empty, unknown, and disabled cases retain native
rendering. Toggle changes/shutdown redraw tracked visible windows; selection,
requirements, inventory data, and execution remain untouched. See STUDY_TABLE.md.

The session excludes empty stacks for all policies. Each policy rejects instance
properties, contents, internal inventory capacity, bag status, or durability.
Armor/Weapons/Tools also require a declared stack limit of one and actual source
Count of one; this stricter policy never applies to Collars or earlier actions.
Garden/alchemy actions consume by definition ID; autopsy captures a real item's
UID and removes one unit on craft confirmation, then recreates the result by ID.
Embalming consumes one real item by UID immediately on selection and leaves craft
effects/definition-based pocket insertion to the native callback and completion.
Grave decorations choose a native craft by full definition ID; the craft system
removes recipe quantities through its own multi-inventory, not the clicked UID.
Collar upgrades remove one source unit by UID through their original inventory
list, recreate the new collar by definition, and remove the old collar natively.
Other equipment callbacks receive a real singleton, retain native UID assignment,
and perform insertion/replacement/old-item returns without mod transfer logic.
Unknown instance variants cause the entire picker
to fall back rather than silently combine them, including when a visible
fertilizer is currently disabled. Ordinary items group by their full definition
ID, preserving definition-based quality differences.

The policy's CanSelect is passed as the virtual widget's CustomItemsAvailableCondition,
so InventoryWidget.Redraw and UIItemCell implement disabled appearance and
input blocking. The quantity decorator changes counters only, not widget state.
For most actions this is the original native predicate. Armor/Weapons/Tools combine
it with the same-equipped-full-ID restriction; snapshots, UI, and fresh selection
validation therefore agree. Disabling the action restores the native predicate.

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
access without zone storage, native upgrade/transfer rules, and singleton safety.

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

Refresh replaces all section data together and calls OnItemsAvailabilityChanged,
which the game binds to the multi-inventory widget's Redraw. This preserves the
section count and lets each InventoryWidget.SetData unsubscribe from its old holder.
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

A separate global `[Fixes] Window Sorting Order` setting and Harmony owner correct
allocation-counter accumulation independently of picker startup and action gates.
They track original stack allocations without changing surviving canvases, suspend
on reentrancy/unexpected state, and resume at a fresh root. The canonical generic
patches are verified on shipped Mono/Harmony with a standalone controller probe.
See [WINDOW_SORTING.md](WINDOW_SORTING.md) for the contract, limitations, and tests.

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
Zombie Equipment controls all four categories. Missing optional insertion/replacement
methods leave Collars supported; initialization failure cannot disable earlier actions.
Future actions should get their own settings and explicit delegate registrations,
then a reviewed selection policy. Keep the action gates: each action's consumption
and equipment transfers have different contracts.

## Remaining validation

Successful compilation and the pure-logic checks do not validate Harmony runtime
installation, Unity prefabs, pooled component behavior, or controller input. The
separate window-sorting runtime probe verifies its generic Harmony interception on
shipped Mono with stand-in windows, not Unity rendering or the picker patches.
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
The user validated Collars in-game before the Armor/Weapons/Tools extension.
New checks cover all insertion/replacement contexts, native exclusions, singleton
safety, source identity, availability changes, and the shared equipment setting.
The new equipment paths require in-game validation. Settings checks cover all 128
combinations of the seven independent options.
The game and saves were not launched or modified during development. Follow the
manual checklist in README.txt using the user's chosen in-game test setup.
