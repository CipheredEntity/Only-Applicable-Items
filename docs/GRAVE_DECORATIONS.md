# Grave Decorations integration

`[Actions] Grave Decorations = true` independently enables both "Tombstones" and
"Fences". The setting is shared by the two categories and preserves existing
configuration values for all other features. Disabling restores an open decoration
picker on the next plugin frame; enabling applies on its next opening.

## Scope and eligibility

UIGraveElementWidgetData.OnGraveElementClicked opens UIMultiInventoryWindowData
with OnGraveItemSelected/IsGraveItem when its slot is empty. The public
GraveElementType property selects Top (headstones) or Bot (fences). Tool availability
is checked by the original grave slot UI before opening and is left untouched.

GraveDecorationPickerContract validates both method signatures and the category
getter, then requires the exact callback/predicate pair on the same widget-data
instance. Unknown categories, wrappers, multicast delegates, and unrelated actions
retain vanilla. Initialization failure is isolated from the other features.
No additional Harmony patches are installed.

Display membership is a nonempty item in GameConsts.Groups.TOMBSTONE_ITEM_GROUP
("gravetop") or FENCE_ITEM_GROUP ("gravebot"), according to the selected category.
The original IsGraveItem delegate still determines selectability. In the inspected
game it checks only group membership: no extra quality, research, or craft-availability
rule is added. If the native predicate rejects a displayed matching-group item,
the shared widget preserves its disabled appearance and blocks selection. There
is no custom failure-reason tooltip provider in this picker.

The original picker supplies player inventory, qualifying current-zone storage,
and directly held bags. Existing session logic preserves that access list,
deduplicates aliases, groups equivalent full definition IDs, and displays combined
counts without normal stack caps. Different IDs remain separate. Stored items,
counts, and UIDs are never changed to build the display. ValidatePlainItem falls
back for any included item with unknown properties, durability, bag status,
contents, or capacity, even when that item is disabled. Other groups are excluded
before validation, so unrelated unusual items cannot force fallback.

## Crafting, removal, and replacement

ApplicableItemsSession.Select rebuilds sources and native eligibility, then passes
a real current representative through the original callback/audio wrapper once.
OnGraveItemSelected uses only DisplayingItem.id: it chooses the first available
craft with a matching needItem.id and calls TryStartCraft. It does not use the
clicked stack's UID, count, or container. CraftElement uses the recipe's actual
requirements; CraftElementBase.RemoveCraftRequirements delegates to the grave's
GetCraftableMultiInventory().RemoveItems. The native system determines quantities
and real sources, including distributed stacks. It may consume an equivalent
stack other than the displayed representative. No mod consumption is introduced.

An occupied slot opens OpenGravePartRemoveWindow instead of the inventory picker.
FindGravePartRemoveCraft finds the configured removal recipe by its drop list.
The native confirmation/crafting system handles removal, drops, and world-part
changes. A replacement still requires removal first, then insertion into the
empty slot. The mod does not intercept removal, change drops, add direct swapping,
or change exhumation rules.

Native limitations remain: group membership alone can permit selecting an item
whose craft is unavailable or fails to start. The callback closes both windows
even in that case. Exact placement/removal costs, transfers, and effects remain
defined by the game's balance data. No new restrictions or fixes are introduced.

## Validation

Automated checks compile the production contract with test-only game stand-ins
and production aggregation/snapshot/delegate code. They cover both titles/groups,
one shared live toggle, unknown contexts/categories, variant separation, alias
deduplication, disabled visibility and availability-only refreshes, instance-data
rejection, real surviving sources, and preservation of the selected definition.
The shared settings check covers all 64 combinations of six action settings.
These checks do not execute native crafting, Unity, or Harmony.
The two callback/predicate signatures, public category getter, and group constants
were also verified against the reference Assembly-CSharp.dll using reflection-only
metadata loading, without executing game code.

Manual in-game checklist:

1. Confirm the Grave Decorations startup log and default-enabled configuration.
2. Open each empty slot; verify Tombstones and Fences, respectively, with
   only the correct category and exact totals across player/bags/zone storage.
3. Check full-ID variants, empty results, mouse/controller navigation, scrolling,
   tooltips, cancellation, depletion, and repeated openings.
4. Select decorations and check native craft quantities, installation, and quality.
   Cancelling must not start a craft or consume items. Test split source stacks.
5. Remove installed decorations through the normal confirmation, check returned
   drops, then install replacements. Check tool gating and exhumation remain native.
6. Disable while either picker is open to verify vanilla restoration. Re-enable
   for its next opening and check other feature toggles remain independent.
7. Regression-test all previously supported actions and ordinary inventory use.
