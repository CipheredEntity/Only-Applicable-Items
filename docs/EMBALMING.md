# Embalming Fluids integration

[Development guide](DEVELOPMENT.md) · [Configuration](CONFIGURATION.md)

`[Actions] Embalming Fluids = true` independently enables the "Embalming Fluids"
virtual inventory. Existing configuration entries are retained. Disabling restores
an open embalming picker on the next plugin frame; enabling applies on the next
picker opening. Other actions keep their own settings and contracts.
`[Inventory Names] Embalming Fluids` independently customizes the next-open title;
blank values use **Embalming Fluids**. The same action toggle also controls the
supplemental native collar warning below.

## Scope and native rules

EmbalmInteractionHandler.Interact opens UIEmbalmWindow. Its data's
OnItemCellPressPocket opens UIMultiInventoryWindowData with TryEmbalm/CanEmbalm
when clicking the empty "+" pocket slot. EmbalmingPickerContract validates these
two method signatures and requires their exact delegates on one UIEmbalmWindowData
instance. The existing picker-constructor and InventoryWidget.OnRedraw patches
are reused. Contract initialization failure leaves other actions active.

Visibility includes every nonempty ItemType.Embalm item. Native CanEmbalm still
controls disabled appearance, input blocking, and fresh click-time validation:

- Normal fluids require fewer than six occupied pocket slots. Zombies also need
  resulting skull totals within collar borders.
- Crafts classified by CraftDef.ConsumesEmbalmItem require a zombie and return
  before the pocket-count and collar checks. The classification detects
  AddZombieTech/RespecZombie completion functions; these fluids are not retained
  in the body's pockets.
- There is no duplicate-fluid exclusion in CanEmbalm. Already applied fluids can
  instead change occupied slots and skull totals. No research or duplicate rules
  are added by the mod. Vanilla supplies no failure-reason tooltip provider here.

## Collar warning (virtual inventory only)

The consolidated section supplements ExtraRedTooltipLocIdProvider with the same
ui_operation_failed_desc localization key used by the autopsy replacement picker.
InventoryWidget.Redraw assigns it to UIItemCell.ExtraRedTooltipLocId; the unchanged
UITooltip.ShowItemCell appends the native red paragraph to normal item information.
Any existing provider's nonempty result takes precedence. No new Harmony patches,
custom tooltip rendering, eligibility rules, or configuration entries are added.

EmbalmingPickerContract reads the owning UIEmbalmWindowData's live bodyItem,
zombieWgoData, and isZombie fields through type-checked reflection. The warning
requires a valid zombie/body, a known non-ConsumesEmbalmItem craft, fewer than six
pockets according to BodyPocketInventoryWidget.GetOccupiedSlotCount, and a valid
equipped collar. The original CanEmbalm predicate must reject the fluid, and
ZombieWgoData.CanAddItemToBody must reject its resulting skull totals. All skull
arithmetic and minimum/maximum red/white collar bounds stay native. Missing craft,
collar, or body context and failed inspection suppress only the added message.

The provider belongs only to the virtual section; original section data and native
providers are untouched. It reads the existing Embalming Fluids setting live.
The optional policy warning result is included in snapshot comparisons, so the
existing half-second poll redraws when a rejection reason changes even if stock
and selectability do not. Normal native cell redraws overwrite the warning field;
native Flush also clears it when releasing pooled cells. Disabling/fallback
restores vanilla sections and their original providers.

The original MultiInventory supplies player inventory and qualifying current-zone
storage, with directly held bags projected by the shared session. Full definition
IDs determine equivalent groups and preserve variants. Combined counts decorate
the cell only, without stack caps or mutations to stored items. Alias references
are deduplicated. ValidatePlainItem falls back to the original picker for included
items with properties, durability, bag status, nested contents, or capacity, even
when a fluid is disabled. Non-fluids are excluded before that validation.

## Application and consumption

ApplicableItemsSession.Select rebuilds the snapshot, checks native selectability,
and rebinds the cell to a currently accessible real source. The original callback
and audio wrapper execute once; no synthetic source item or UID is passed back.

UIEmbalmWindowData.TryEmbalm rechecks CanEmbalm, looks up the craft by definition
ID, creates a one-operation CraftElement with a new definition-only custom item,
calls TryStartCraft, removes one source unit using the selected item's UID, and
closes the picker and embalming window. Selection starts the operation immediately;
there is no separate craft confirmation. Cancelling the picker consumes nothing.

WgoData's Embalm completion inserts a newly created definition-only item into the
body for normal fluids. Special fluids skip that insertion and use native craft
completion expressions. The mod does not change source consumption, skull/perk
updates, craft requirements, or completion effects, and does not transfer arbitrary
source instance properties into the body.

Native limitations deliberately preserved:

- A full pocket display has no empty "+" slot to open the picker, even though
  special fluids bypass the capacity test inside CanEmbalm.
- TryEmbalm ignores TryStartCraft's success result before removing the source unit.
- CanEmbalm does not always reject a missing craft definition, but TryEmbalm logs
  the missing craft and returns without consuming. The mod does not add a rule.

## Validation

Automated checks compile the production contract with test-only type/signature
stand-ins and execute production snapshot/aggregation code. They cover exact
delegate ownership and isolation, full-ID variants, aliases, blocked fluids,
availability-only changes and stale selection rejection, unusual-instance safety,
real surviving sources, and live settings. Additional warning checks cover collar
validation forwarding, full pockets/special fluids, missing context, failures,
reason-only refreshes, original provider precedence, live toggling/reopening, and
cell reuse across every picker identity and vanilla inventory data. Native craft
rules, skull arithmetic, actual consumption, and Unity rendering are not executed.
The production contract validates TryEmbalm/CanEmbalm signatures at startup and
uses the game's ItemType.Embalm constant. Optional warning-context field mismatch
suppresses the added warning rather than disabling otherwise valid consolidation.

Manual in-game checklist:

- On a zombie near a collar boundary, confirm blocked ordinary fluids show the
  same localized red paragraph as organ replacement while keeping normal tooltips.
  Check red and white limits, including reductions below collar minimums.
- Check full pockets, special fluids on ordinary corpses, missing/unknown context,
  and selectable fluids do not show a misleading collar warning. If body/collar
  state can change while the picker is open, verify warning-only changes refresh.
- Reopen with a different corpse, use mouse/controller tooltips, toggle the feature
  off, and open unrelated inventories to check that warning text never carries over.

1. Confirm the Embalming Fluids startup log and default-enabled independent entry.
2. With fluids split among player inventory, bags, and zone storage, check one
   titled inventory, exact totals, variant separation, and no unrelated items.
3. Check ordinary corpses and zombies near collar limits. Blocked fluid entries
   must remain visible and disabled for both mouse and controller input.
4. Apply the same normal fluid again where native rules allow; test occupied
   pockets, special zombie technology/reset fluids, and native craft effects.
5. Verify selecting consumes exactly one real source unit; cancelling consumes
   none. Test depletion of the original representative and repeated openings.
6. Check scrolling, empty results, tooltips, Back/close, and the unchanged full-
   pocket limitation. Disable while the picker is open to verify vanilla restore.
7. Regression-test Seeds, Fertilizers, Alchemical Ingredients (including counters),
   Body Parts, and unsupported interfaces with their settings independent.
