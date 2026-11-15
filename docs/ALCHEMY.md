# Alchemical Ingredients

[Development guide](DEVELOPMENT.md) · [Configuration](CONFIGURATION.md)

The default **Alchemical Ingredients** title has its own blank-by-default
`[Inventory Names] Alchemical Ingredients` setting. The action setting controls
both consolidation and the laboratory quantity-display correction below.

## Scope and selection

AlchemyFeatures supplies an additional policy to the existing picker hook. It
requires the exact `UIAlchemyWindow.IsItemValidForMix(Item)` method on an alchemy
window and a single instance callback. The callback captures the destination
ingredient slot, so its target is not required to equal the predicate's target.
Compiler-generated closure names are never inspected.

Visibility uses AlchemyVisibility<Item, SurveyDef>, matching the non-slot-specific
front portion of the inspected native predicate. It rejects null/empty items and
definitions without canBeUsedInAlchemy, then delegates survey lookup to
GameBalance.GetSurveyDefForItemOrNull and completion to KnowledgeSystem.IsSurveyCompleted.
It preserves the native special cases: nonzero runes with no survey remain hidden;
zero-rune items with no survey are allowed; any existing survey must be completed
even for a zero-rune item. No completion state is cached or inferred from flags.

Visibility intentionally does not check rune totals. Researched ingredients that
would exceed any limit stay visible and aggregated. Selectability still uses the
unchanged IsItemValidForMix delegate, including both upper and lower rune bounds
and the other-slot sum computed by OnIngredientPressed. InventoryWidget draws
rejected entries with its native disabled state, and click-time snapshots reject
them before the original callback can run. Availability-only changes use the
existing full cell redraw, updating both appearance and interaction flags.

This split duplicates only the native predicate's front-end visibility conditions;
review them against IsItemValidForMix after game updates. Research completion and
all slot-dependent selection rules remain owned by the game. Boosts, the folio,
research-resource pickers, and other interfaces are outside the matcher. Existing
Seeds, Fertilizers, quantity correction, and consumption paths are unchanged.

PlainItem validation is shared with the garden integration. Meaningful unknown
instance properties, contents/capacity, bags, or durability cause picker fallback.
Full definition IDs preserve definition-based variants. Selection resolves a real
accessible representative and invokes the original slot/audio callback once.
No item is reserved, moved, copied, or consumed by consolidation. Repeated slot
selection remains legal whenever the native predicate accepts it.

## Display correction

The original slot callback stores its selected stack's Count in UIItemCell's
HasItemCount. UpdateCountInWindow multiplies each slot's one-unit requirement by
craftCount and the number of matching ingredient slots. This explains the visual
50/100 warning with two selected slots and 100 actual units in two stacks.

AlchemyPatches adds a prefix and finalizer to UIAlchemyWindow.UpdateCountInWindow.
The prefix gets active populated ingredient cells using AlchemyContract's validated
private ingredients field. QuantityDisplayScope prepares all accessible totals and
original HasItemCount values before changing any cells, and deduplicates cells and
ingredient IDs. Totals come from PlayerController.WorkerMultiInventory.GetTotalCount,
the same scope used by the native ingredient-availability checks, not from a picker
snapshot or an eligibility-filtered inventory.

Only HasItemCount is temporarily overridden. The original method still determines
requirements, duplicate-slot multipliers, number formatting, and warning colors.
The finalizer restores original caches on success and exceptions, and returns the
original game exception unchanged. Drawing state persists in the rendered label;
no stored Item.Count/UID, slot item, recipe, craftCount, or crafting logic is changed.
Preparation failures leave caches untouched; setter failures roll back writes.

For 100 accessible units, two matching slots each show 100/100 at batch 50; three
matching slots each show 100/99 at batch 33. The numerator is the shared stock in
each row. Disabling the correction renders the original cached stack quantities.

## Lifetime and refresh

The count prefix observes shown laboratories even if the option is initially off.
AlchemyFeatures tracks their last displayed totals and option state separately
from ApplicableItemsSession: the picker closes before the laboratory redraws.

Selections, batch changes, and boost changes already redraw through the native
RedrawAlchemyTabLite path. For background stock changes, the plugin checks totals
every half second and redraws only when they change. Live option changes trigger
an immediate redraw on the next plugin LateUpdate. Enabling the option updates lab
labels immediately while inventory consolidation starts on the next picker opening.

Refresh invokes RedrawAlchemyTabLite, not UpdateCountInWindow alone. The count
helper also multiplies the result cell's cached Value; the normal redraw then
redraws that result with the correct batch quantity. Calling the helper alone could
introduce an incorrect squared result counter. RedrawAlchemyTabLite preserves slot
selections and batch size and runs the original availability calculations.

A postfix on UIAlchemyWindow.Hide drops tracked window state. Destroyed/inactive
owners are also removed, and plugin teardown renders native counters before
removing patches. No per-slot backup survives a count-update invocation.

## Configuration and failure isolation

`[Actions] Alchemical Ingredients = true` is independent of the other actions.
It controls BOTH picker consolidation and quantity correction. False restores
an open consolidated ingredient picker via the existing session mechanism and
refreshes the laboratory with its native cached counters.

AlchemyContract validates its own methods and slot-field type. Its patches have
a separate Harmony owner (`ciphe.gk2.onlyapplicableitems.alchemy`), so initialization
failure removes only alchemy patches. Runtime reflection/count/display failures
disable both alchemy improvements for that plugin session and log once per failure
category; existing alchemy picker sessions then fall back. Restart after resolving
a compatibility failure. A rejected unusual item only falls back that picker;
the display correction can still use native ID-based totals safely.

## Validation limits

The checks execute production core policy, snapshot, and quantity-scope code with
plain test objects. They exercise repeated IDs, selection without reservations,
predicate isolation, visible-but-disabled rune overflow/underflow entries,
research/type hiding and native survey exceptions, availability-only refreshes,
stale-click rejection, independent setting combinations, corrected totals/colors under
a simulated native renderer, disabled-mode restoration, alias deduplication,
counting/setter rollback, exception cleanup, and cleanup after one restore fails.
They do not execute game assemblies or prove Harmony/Unity rendering behavior.
In-game regression checklist:

1. In small and larger laboratories, split researched ingredients across the
   player, bags, and native zone storage. Check full-ID variants, totals, and empty
   results. Unresearched/non-alchemical items must remain hidden; researched items
   outside either rune bound must remain visible but disabled.
2. Replace ingredients and check the other-slot rune sum, tooltips, scrolling,
   mouse/controller input, cancellation, and repeated openings.
3. With 100 units across two stacks of 50, select the same ingredient in two slots
   at batch 50: each row should show 100/100. Three matching slots at batch 33
   should show 100/99. Above-stock requirements must still warn. The result counter
   must remain the actual batch output, including after background stock changes.
4. Compare actual native crafting costs, research, boosts, folio/flask limits,
   source depletion/replenishment, and rewards with vanilla. Selection alone must
   not consume or reserve ingredients.
5. Toggle with the laboratory and picker open. Restore native stack-based labels
   when disabled, preserve selections/batch size, and apply consolidation on the
   next opening when enabled. Check custom names, close/reopen, scene changes,
   pooled reuse, and unrelated actions.
