# Zombie Equipment

The existing [Actions] Zombie Equipment = true setting controls "Collars", "Armor",
"Weapons", and "Tools" together. Its saved value is retained. Disabling restores
an open equipment picker on the next plugin frame; enabling applies on its next
opening. Other actions remain independently configurable.

## Native scope and action matching

Collars retain UpgradeCollar / ZombieWgoData.CanUpgradeCollarTo and the existing
linked-zombie identity check: their delegates belong to different objects.
Armor, weapons, and tools use InsertItem for empty slots or ReplaceItem for
occupied slots, with IsArmor, IsWeapon, or IsTool. Both delegates must be the exact
native methods bound to the same equipment-data instance. Unknown wrappers,
multicast delegates, crossed owners/action pairs, and unrelated predicates retain
vanilla. Missing optional insertion/replacement methods leave Collars supported
and log a warning. No new Harmony or equipment-removal patches are installed.

All four native pickers use addCurrentPlayerWorldZone:false: only player inventory
and directly held bags are projected from the original MultiInventory. Zone storage
is not added; the action retains its original inventory list for real transfers.

## Visibility and eligibility

| Title | Display membership | Native selectability |
| --- | --- | --- |
| Collars | ItemType.Collar | CanUpgradeCollarTo |
| Armor | ItemType.BodyArmor | IsArmor and different equipped full ID |
| Weapons | isWeapon, not isTool, type not Sword | IsWeapon and different equipped full ID |
| Tools | isTool | IsTool and different equipped full ID |

Empty/depleted entries are excluded. Matching-category items rejected by the native
predicate remain visible but disabled, and click-time eligibility is re-evaluated.
Collars retain their higher-capacity/skull-range requirements. Native Armor/Weapons/
Tools category rules and normal tooltips remain unchanged.

Armor/Weapons/Tools additionally disable candidates whose full definition ID equals
the item currently equipped in that slot. Comparisons are ordinal and use the full
ID, preserving variants. Live ZombieWgoData.Armor/Weapon/Hand getters are read on
each eligibility evaluation, including refresh and click-time validation. Matching
items remain visible and aggregated. Empty slots do not add a restriction. Collars
keeps its unchanged native upgrade predicate, and disabling Zombie Equipment restores
the original picker behavior.

ApplicableInventoryWidgetData passes Policy.CanSelect to the native widget for
gray-out and input blocking. This is the same predicate captured by snapshots and
rechecked before callback dispatch. Other action policies still use their original
native predicates.

## Safe aggregation and source identity

Read-only inspection of GameBalance.itemDefs in the installed resources.assets
found 10 armor, 13 zombie-weapon, and 39 tool definitions matching the native filters.
Every matching definition had stackCount=1, hasDurability=false, durDecreaseOnUse=0,
no internal inventory capacity, and no bag flag. Counts include NPC/test definitions
and do not establish obtainability. Full definition IDs preserve quality, damage,
and talent differences.

These observations are not universal invariants: Item.Count is writable and the
game supports serialized properties and conditional durability. ValidatePlainItem
still rejects properties, durability, bags, contents, or capacity. Armor/Weapons/Tools
additionally require both Definition.stackCount==1 and actual Count==1. Any included
unexpected item, even if disabled, causes the entire picker to fall back. Unrelated
categories are excluded before validation. Collars keep their existing validator
and one-unit behavior, including source quantities greater than one.

The session deduplicates aliases and decorates totals without moving items or
changing source Count/UID/properties. Five separate identical axes show one entry
with 5, but selection passes one actual axe with Count=1, not a synthetic aggregate.

## Original operations remain with the game

Selection rebuilds sources and native eligibility, rebinds to a current real item,
and invokes the original callback/audio wrapper once. UpgradeCollar still creates
a new collar by definition, assigns its GUID, removes one old collar without return,
and removes one source unit by UID.

InsertItem keeps the original transfer, equipped-GUID assignment, and source-UID
removal. ReplaceItem keeps its definition/count-based recreation of incoming and
outgoing items, return of the old item through the original access list, slot
assignment, and TryUpdateZombie notifications. No transfer order, property-reset
behavior, inventory-full recovery, or callback exception handling is changed.

Secondary-action unequipping stays unpatched. Its native return destination may
include zone storage, unlike the incoming picker. No empty-collar action, equipment
reservation, or mod consumption is introduced.

## Validation

Checks execute the production contract and aggregation/snapshot/delegate code with
test-only game stand-ins. They cover existing collar behavior, all three new titles
for insertion/replacement, exact owners and methods, native category exclusions,
disabled visibility, availability-only changes, source depletion, variant separation,
quantity/instance-data fallback, and one shared live toggle. All 128 combinations
of seven settings are checked. Native transfers, skull arithmetic, Unity, and
Harmony are not executed.
Additional checks cover same-ID disabled groups and totals, full-ID/slot isolation,
live equipped-item changes, stale selection rejection, safety/native-rule preservation,
and the production virtual-view constructor using the full policy for UI availability.
The collar methods/getter and all five insertion/replacement/filter signatures
were also verified against the reference Assembly-CSharp.dll using the installed
SDK's metadata-only reader, without executing game code.
The Armor/Weapon/Hand getters used by the duplicate-equipment restriction are also
validated in the reference assembly.

Collars was tested in-game before this extension. Manual validation remains:

1. Confirm the startup log reports equipment support and the existing setting is kept.
2. Open insertion and replacement pickers for each new category. Check titles,
   player/bag totals, excluded zone storage, scrolling, empty results, tooltips,
   mouse/controller navigation, cancellation, and repeated openings.
3. Put identical pieces in separate slots/bags; confirm combined quantities, separate
   full-ID variants, and transfer of exactly one real piece. Cancellation must not
   change inventories or equipped GUIDs.
4. Confirm native swords/tools exclusions in Weapons.
   For each category, confirm owned copies of the equipped full definition stay
   visible but disabled for mouse/controller selection; other variants and empty
   slots remain selectable when native rules allow them.
5. Replace and unequip items; check returned equipment, limited inventory space,
   work/talent/combat updates, and native notifications.
6. Regression-test Collars, including disabled upgrades, one-unit consumption,
   old-collar removal, and correct zombie ownership.
7. Disable with each picker open and verify vanilla restoration. Re-enable for the
   next opening and verify other feature toggles stay independent.
8. If a safe test setup supplies unusual counts/properties/stack limits, verify
   fallback without mutation. Regression-test all previous actions.
