# Study Table

`[Actions] Study Table = true` controls both sections independently of other
actions. The stable IDs `StudyResearchable` and `StudyDecomposable` have centralized
default titles **Researchable** and **Decomposable**, in that order. Both headers
remain visible when empty. Name customization is not implemented yet.

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
