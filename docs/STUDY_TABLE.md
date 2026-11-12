# Study Table

`[Actions] Study Table = true` controls both sections independently of other
actions. The stable IDs `StudyResearchable` and `StudyDecomposable` have centralized
default titles **Researchable** and **Decomposable**, in that order. Both headers
remain visible when empty. Name customization is not implemented yet.

Researchable hides only its item quantity labels; Decomposable keeps its combined
counts. Both category headers use the game's icon-free header mode. The same
Study Table toggle controls these visuals, with vanilla restored when disabled.

## Native action and classification

StudyTablePickerContract recognizes UIResourceBasedCraftWindowData.IsItemAllowed
and a native callback declared on and bound to that same data instance. Unknown
owners, wrappers, or multicast delegates retain vanilla. No generated method
names, extra Harmony patches, or independently maintained item lists are used.

For each native-eligible nonempty item, GameBalance.GetSurveyDefForItemOrNull(id)
provides its survey. isScienceFuelCraft=false means Researchable; true means
Decomposable. Missing definitions are excluded. The resolver selects one effective
survey, preferring an exact ID then its first matching group definition. The mod
does not search for alternative surveys or switch completed research to science
decomposition. Full item IDs retain definition and quality distinctions.

The native cached allowed list excludes completed one-time surveys when the
study window is constructed. It does not exclude items for missing science,
faith, other resources, or insufficient quantities for a craft. These remain
normal requirements/action-button restrictions. The mod does not rebuild the
allowed list or add new completion/availability checks. Native group-based
completion and any cached-list staleness remain unchanged.

## Coordinated session

- The original MultiInventory remains the access authority: player, eligible
  current-zone storage, and bags exposed by the native helper. One source scan
  supplies both independent aggregations; source aliases are deduplicated.
- PickerSnapshot.BuildAll and view construction complete for both categories
  before either section is published. Duplicate section IDs or overlapping item
  membership reject the entire plan. Quantities remain checked sums without
  stack caps; no stored Count, UID, properties, or ownership is changed.
- ApplicableItemsSession now holds an ordered array of views. Existing actions
  use the same path with one view and retain their previous title/header behavior.
  Each view carries its own policy and section index for native availability and
  fresh click resolution. A source that changes category before clicking is
  rejected in its old category; both sections refresh without executing an action.
- Refresh replaces both data views together and redraws the existing child
  widgets. Section count stays fixed, including empty categories. Native redraw
  notifications rebuild controller navigation; the inspected navigation code
  skips empty groups. No custom controller navigation is introduced.
- Validation failure, ambiguity, layout changes, quantity overflow, or redraw
  failure restores the entire original picker. Disabling the shared toggle does
  the same on the next frame or before a click. Enabling applies on the next
  opening. Source subscriptions and hide/disposal callbacks are shared once.

The unchanged ValidatePlainItem rejects included items with instance properties,
durability, bag status, contents, or internal capacity. Study has a broad item
pool, so eligible tools/containers with such data can make the whole picker fall
back. Irrelevant items are filtered before validation. This safeguard is not relaxed.

## Study-only visuals and pooled-cell reuse

After the standard counter decorator computes and renders each combined total,
only the StudyResearchable policy hides UIItemCell's private countLabel GameObject.
StudyTableVisuals validates that field's name/type once, lazily, and never writes
Item.Count, the aggregate, UIItemCell.Value, drawCounter, callbacks, or requirements.
Other section IDs, including StudyDecomposable, retain normal counter rendering.
The selected study item slot and supporting-resource counters are not patched.
An incompatible label contract enters the existing whole-picker safe fallback.

Native UIItemCell.Draw/DrawCount explicitly sets counter visibility each time a
populated cell is drawn; other mod pickers also force their aggregate labels
visible. Consequently, no hidden flag remains on a pooled cell. DrawEmpty clears
the label text, and the next populated draw restores normal visibility. No prefab,
global counter setting, or global Draw patch is changed.

ApplicableInventoryWidgetData supplies an empty HeaderIconId only for the two
enabled study policies, keeping DrawHeader and CustomHeaderId intact. The native
InventoryHeaderWidget.UpdateHeader hides its icon and enables decorWhenNoHeaderIcon;
it restores the icon for subsequent nonempty IDs. Original header data and defaults
remain untouched. Check the native icon-free spacing/decoration visually in-game.

A read-only scan of the installed GameBalance.surveyDefs found all 395 primary-item
quantity expressions equal to 1. This supports hiding research stock labels in
the current build; the native code still supports other quantities, and those
requirements remain enforced if game data or other mods change them.

## Selection and consumption

The original callback receives a currently accessible real representative. It
retains SelectedItem and constructs CraftElementSurvey requirements using the
full selected ID and configured quantities. No synthetic total is supplied as an
item Count or consumption quantity. The study window already displays total stock.

Normal research requires the main item to be present but skips it when consuming
requirements. Decomposition consumes all configured requirements, including the
main item, through the native crafting multi-inventory; this already handles
distributed stacks. Science/table requirements, research completion, output,
timing, repeated decomposition, and source changes after selection remain native.
No reservation, UID-consumption replacement, or unrelated gameplay fix is added.

## Validation

Automated checks execute the production contract, coordinated session, view, and
snapshot code against in-memory boundaries. They cover fixed ordering/identities,
native predicate/resolver forwarding, source enumeration and aggregation, empty
sections, stale category clicks, atomic initial and refresh failures, shared live
toggle behavior, and regressions for every existing single-section picker identity.
They do not execute Unity rendering, actual controller input, Harmony interception,
the game's resolver implementation, knowledge logic, or native consumption.
Visual checks execute the production decorator and label-field access against
small UI boundaries, verifying retained aggregates/UIDs/counts, visible Decomposable
labels, icon-free study header data, pooled reuse in vanilla/every other picker,
and native counter/icon restoration after disabling or fallback.

Manual checklist:

1. Open with both categories populated, either empty, and both empty. Check headers,
   ordering, scrolling, mouse/controller focus, Back, and repeated opening.
2. Split equivalents across player, bags, and eligible zone storage. Check exact
   totals above stack caps, separate full IDs/qualities, and unchanged real stacks.
3. Research an eligible target. Check supporting requirements, completion sharing
   across survey groups, normal rewards, and that its main item remains intact.
4. Decompose repeatedly into science, including a configured cost distributed
   across stacks. Compare requirements, output, and exact consumption with vanilla.
5. Check missing resources and source changes, cancellation, and live disabling.
   Both virtual sections must restore together; other action toggles stay independent.
6. Test eligible unusual tools/containers: safe native fallback is expected. Recheck
   Seeds, Fertilizers, Alchemy, Body Parts, Embalming, Graves, Zombie Equipment,
   Resurrection Collars (including its selected-slot display), and Sermons.
7. Check Researchable quantity labels are hidden at 1 and larger combined totals,
   while Decomposable (including a total of 1), the selected-item slot, and resource
   requirements keep their quantities. Close/reopen and switch to vanilla storage
   or another picker to check pooled counters restore. Check both icon-free header
   titles and spacing, then disable Study Table to compare vanilla icons/counters.
