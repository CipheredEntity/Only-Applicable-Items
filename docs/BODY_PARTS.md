# Body Parts integration

[Development guide](DEVELOPMENT.md) · [Configuration](CONFIGURATION.md)

`[Actions] Body Parts = true` independently controls Brains, Hearts, Guts, Skulls,
Bones, and Skins. The titles map explicitly to ItemType.Brain, Heart, Guts, Skull,
Bones, and Skin in BodyPartPickerContract. Existing settings are retained.
Each category has an independent blank-by-default name key, such as
`[Inventory Names] Body Parts - Brains`; the configuration reference lists all six.
Titles affect presentation only. They do not identify the selected body-part type.

## Native flow and scope

UIAutopsyWindowData.OnItemCellPressOrgans reads the clicked fixed cell's ItemType.
An empty slot opens UIMultiInventoryWindowData with TryInsertMainOrgan and
CanInsertOrgan. An occupied zombie slot uses TryChangeMainOrgan and CanChangeOrgan.
An occupied ordinary corpse slot starts extraction instead. Locked organ slots
remain governed by UIFixedTypeItemCell.TryUpdateUnknownOrgan. Pockets, extraction,
embalming, resurrection, and other item pickers are not intercepted as body parts.

BodyPartPickerContract requires either exact callback/predicate pair bound to the
same UIAutopsyWindowData instance. It validates all four method signatures and the
private selectedItemType field's type at initialization. Unknown categories and
delegate contexts retain vanilla. A separate initialization failure cannot remove
the existing garden/alchemy integration; no additional Harmony patches are needed.

## Display and item identity

The existing picker-constructor postfix supplies a body-part PickerPolicy<Item>.
Visibility includes nonempty items of the clicked category, even when the original
predicate rejects them. This includes matching surgical-mistake entries if any
exist in accessible inventories. Native predicates still reject mistakes and,
for zombies, check resulting skull totals against collar borders. The existing
failure-tooltip provider is retained, as are disabled mouse/controller behavior
and fresh predicate evaluation before dispatching a selection.

Accessible sources remain the original MultiInventory and its directly held bags.
The shared session groups by the complete definition ID, deduplicates aliases,
and displays uncapped combined counts using cell values only. Quality, skull
values, and bodyLinkedPerk belong to ItemDef; distinct IDs remain separate.
ValidatePlainItem rejects serialized properties, durability, bag status, contents,
and internal capacity. Any included unusual item aborts the whole projection and
preserves the original picker, including when that item is disabled. Unrelated
categories are excluded before validation. This deliberately avoids inventing a
semantic comparer for arbitrary instance properties.

## Selection, confirmation, and consumption

ApplicableItemsSession.Select rebuilds the snapshot and rebinds the clicked cell
to a selectable real source stack. The original callback and audio wrapper run
once. The real MultiInventory remains unchanged and is retained by the native
autopsy data after the picker opens; the display inventory never replaces it.

TryInsertMainOrgan/TryChangeMainOrgan capture that real item, close the picker,
and open UICraftSelectionWindowData. Confirming removes one unit by the captured
item's UID through currentMultiInventory.RemoveItemFromInventoryByUID. Replacement
also records the old organ in CraftElement.CustomItems. No mod consumption,
reservation, count assignment, synthetic UID, or transfer is introduced.

WgoData.DefineResultForInsertOrganCraft/DefineResultForChangeOrganCraft create the
incoming body part from the craft's autopsyItemId. The native operation does not
transfer arbitrary properties from the selected item. Inventory's nested-body
methods keep native organ/perk/skull notifications and success/failure outcomes.
Confirmation retains the native timing: the mod revalidates when selecting, but
does not reserve the chosen stack or add a second validation at craft confirmation.

Disabling Body Parts restores an open body-part picker on the next plugin frame;
enabling applies on its next opening. Other action policies remain independent.

## Validation

Automated checks compile the production BodyPartPickerContract with test-only
stand-ins for its game types, plus the real aggregation/snapshot/delegate code.
They cover all six titles for insertion/replacement, exact delegate ownership,
unsupported categories, full-ID variants, alias deduplication, disabled/mistake
visibility, changed availability, safety rejection, surviving source resolution,
and the shared live toggle. Configuration and policy checks also cover independent
settings and existing picker regressions. These do not execute
game assemblies, native collar arithmetic, Harmony, or Unity prefabs.

At startup the production contract validates the four method signatures and the
selectedItemType field's type. Build/package procedures and the distinction
between source inspection, stand-ins, and native execution are in the development guide.

Manual in-game checks:

1. Confirm the Body Parts startup log and default-true configuration entry.
2. For each of the six categories, open a corpse insertion picker after extraction
   and a zombie replacement picker. Check titles, scrolling, empty results,
   tooltips, mouse input, controller input, repeated opening, and cancellation.
3. Split identical parts among player inventory, bags, and zone storage; verify
   totals, variant separation, and that confirmation removes exactly one real
   part. Cancellation must not remove the chosen part.
4. Test zombies near collar limits. Blocked parts must remain visible and disabled;
   replacement must retain the original failure tooltip and native outcomes.
5. Confirm organ unlocks, surgical mistakes, perks, craft failure/success, and
   ordinary corpse extraction remain unchanged.
6. Toggle Body Parts while its picker is open and verify vanilla restoration.
   Toggle other actions independently and regression-test gardens and laboratories.
7. If a safe test setup provides an unusual property-bearing part, confirm that
   its category picker falls back without altering either source inventory.
