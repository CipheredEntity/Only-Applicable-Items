# Embalming Fluids integration

`[Actions] Embalming Fluids = true` independently enables the "Embalming Fluids"
virtual inventory. Existing configuration entries are retained. Disabling restores
an open embalming picker on the next plugin frame; enabling applies on the next
picker opening. Other actions keep their own settings and contracts.

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
  are added by the mod. This picker has no custom failure-reason tooltip provider.

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
real surviving sources, and live settings. Native craft rules and consumption
are not simulated or executed; all 64 combinations of the six settings are checked.
The TryEmbalm/CanEmbalm signatures and ItemType.Embalm were also verified against
the reference Assembly-CSharp.dll through reflection-only metadata loading,
without executing game code.

Manual in-game checklist:

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
