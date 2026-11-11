# Sermons

`[Actions] Sermons = true` independently controls the prayer stand's picker.
`PickerId.Sermons` and the centralized default title **Sermons** prepare this
category for future naming configuration; names are not configurable yet.

## Native contract and integration

UIPrayWindowData.HandlePraySlotPress constructs UIMultiInventoryWindowData with
CanUsePray and an inline callback bound to that prayer-data instance. The production
SermonPickerContract checks the exact predicate method/signature, single delegates,
matching owner, and callback declaring type without compiler-generated names.
Unknown wrappers/owners/predicates retain vanilla. Initialization failure is
isolated from existing action contracts. No new Harmony patches are added.

The policy uses the original CanUsePray delegate for both visibility and selection:
nonempty ItemType.Preach with a matching SermonDef. Other items, empty stacks, and
items without a sermon definition stay hidden. Attendance is not a picker rule;
valid sermons remain selectable even when there are too few parishioners.

The existing session projects only the original MultiInventory: player inventory,
eligible current-zone storage, and bags normally exposed by the game. Full item
definition IDs distinguish types and quality variants. Equivalent quantities are
summed without stack caps, aliases are deduplicated, and stored items are never
moved, copied into storage, merged, reserved, or mutated for display. Refresh and
click-time resolution use the original native predicate and real surviving sources.

ValidatePlainItem is reused without changes: properties, durability, bags, contents,
or internal capacity on included items cause the original picker to be retained
or restored. Irrelevant items are excluded before validation. Disabling Sermons
restores an open picker on the next frame; enabling applies on the next opening.
Existing action settings and saved values are not changed.

## Selection and execution remain native

The native callback uses the full selected ID to resolve SermonDef and creates a
CraftElementSermon with count one and an empty item-requirements list. It does not
retain the selected Item, UID, source container, or stack quantity. The prayer
window already renders its selected slot using new Item(SermonDef.id), so no
selected-slot display correction is needed. After selection, this code does not
recheck ownership of the original source stack. The mod adds no reservation or
new ownership restriction and does not interpret displayed stock as an execution
count or consumption cost.

PrayerStandInteractionHandler checks sermon_ready before opening. UIPrayWindowData
checks minParishioners and native craft status for starting. UIPrayWindow computes
success chance from visitors and difficulty. StartCraft and System_Pray scripts
retain execution, resource effects, and reward handling. Serialized definitions
and scripts can add behavior beyond the inspected C# methods; none are changed.

## Verification

Automated checks compile the production contract and shared snapshot logic against
test-only delegate/item stand-ins. They cover exact context detection, separation
from existing contracts, native-predicate forwarding, combined quantities and
quality/type distinctions, real sources and stock changes, unusual-item rejection,
default identity/title, and the actual configuration declarations. They do not
execute native eligibility lookup, Unity UI, Harmony interception, sermon scripts,
attendance calculations, or rewards.

Manual checklist:

1. Put identical sermons in player inventory, a bag, and accessible zone storage.
   Check one Sermons inventory, exact combined counts, and no irrelevant items.
2. Check different sermon types and qualities remain separate, including quantities
   beyond a normal stack limit, empty inventories, and repeated opening/closing.
3. Select with insufficient attendance: selection should succeed while the original
   prayer UI prevents starting. Compare success chance and readiness with vanilla.
4. Cancel, reselect, and execute. Compare resource effects and rewards with vanilla;
   aggregation must not multiply executions or consume additional sermon items.
5. Change accessible stock before clicking where possible, and compare changes
   after selection with vanilla's definition-based behavior. No new checks are added.
6. Toggle Sermons independently, including disabling an open picker. Check all
   previous action toggles, mouse/controller navigation, selected-slot appearance,
   and native tooltips. Unexpected properties should fall back without consuming.
