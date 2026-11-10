# Zombie Equipment: Collars

`[Actions] Zombie Equipment = true` currently consolidates only the collar upgrade
picker into "Collars". The shared setting is reserved for incremental equipment
support. Armor, weapon, and tool insertion/replacement/removal are not intercepted.
Disabling restores an open collar picker on the next plugin frame; enabling takes
effect on its next opening. Existing action settings are retained and independent.

## Native scope and action recognition

ZombieEquipmentInventoryWidgetData.OnCollarCellPressed opens the shared picker
with UpgradeCollar and zombie.CanUpgradeCollarTo. Unlike the earlier direct action
pairs, the callback owner is equipment data and the predicate owner is the zombie.
ZombieEquipmentPickerContract validates both signatures and the ZombieWgoData
getter, requires single exact-method delegates, and verifies reference identity
between the equipment owner's zombie and the predicate target. Unknown wrappers,
multicast delegates, mismatched zombies, and every other equipment action retain
vanilla. Initialization failure is isolated from existing features. No new Harmony
patches are needed.

The native picker uses addCurrentPlayerWorldZone:false: only player inventory and
its directly held bags are projected. The mod uses the original MultiInventory
without expanding access or replacing the one retained by the action.

## Visibility, safety, and selection

Every nonempty ItemType.Collar in that access list is displayed. The unchanged
CanUpgradeCollarTo predicate disables equal/lower red-skull-capacity collars and
upgrades whose ranges cannot accommodate the zombie's current skull totals. The
game's existing requirement for a current collar remains untouched. No new reason
tooltip is added; normal item tooltips and disabled input behavior are retained.

The shared session groups plain collars by full definition ID, deduplicates source
aliases, and decorates cell totals without stack caps or changing real quantities.
Different definitions stay distinct. ValidatePlainItem still rejects included
collars with instance properties, durability, bag status, contents, or capacity;
the whole picker falls back safely, including for disabled unusual collars.
Unrelated equipment is excluded before validation.

Selection rebuilds the snapshot and native eligibility, then rebinds the cell to
a currently accessible real representative. Only the original callback runs the
upgrade. UpgradeCollar rechecks eligibility, creates a definition-only new collar
in the zombie inventory, updates equippedCollar to its GUID, removes one old
collar, removes one source unit by UID, triggers native inventory notifications,
and closes the picker. Old collars are not returned. The mod does not consume,
reserve, transfer, change failure handling, or create an empty-slot collar action.

## Deferred armor, weapons, and tools

The user reports that current equipment has no apparent durability, normally
occupies single-item slots, and identical definitions seem interchangeable. These
are useful gameplay observations, not universal item invariants. Before adding
those categories, verify relevant balance definitions (hasDurability/stackCount),
saved-instance properties, and native insert/replace/unequip behavior. Item still
supports per-instance properties and conditionally initializes durability. No
validator is relaxed and no new equipment aggregation policy is introduced here.

## Validation

Automated checks compile the production contract with test-only game stand-ins,
plus production snapshot/aggregation code. They cover exact linked owners,
different/null zombies, wrappers/multicast contexts, rejection of other equipment
and existing actions, blocked variants, availability-only changes, instance safety,
surviving source identity, and live settings. All 128 combinations of seven action
toggles are checked. Native skull arithmetic and actual upgrades are not simulated.
UpgradeCollar, CanUpgradeCollarTo, the ZombieWgoData getter, and ItemType.Collar
were also verified in the reference assembly using the installed SDK's metadata-
only reader, without executing game code.

Manual in-game checklist:

1. Confirm Zombie Equipment startup reports Collars only and the option defaults
   to true. Existing action settings should keep their values.
2. Open a zombie's collar picker. Check one Collars inventory, correct totals from
   player/bags, excluded zone storage, separate definitions, empty results, normal
   tooltips, scrolling, controller navigation, cancellation, and repeated openings.
3. Verify equal/lower-capacity and skull-incompatible collars remain visible but
   disabled. Valid upgrades must obey the game's exact collar borders.
4. Upgrade and verify exactly one source unit is removed, the new collar is
   equipped, the old collar is removed without return, and native refresh occurs.
5. Disable while open to check vanilla restoration. Re-enable for the next opening;
   test different zombies, representative depletion, and added/removed bags.
6. Confirm armor, weapon, tool, and unequip interactions remain native. Regression-
   test all previously supported actions and their independent settings.
