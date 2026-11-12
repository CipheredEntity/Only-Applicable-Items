# Shop Selling

Version 0.13.0 adds `[Actions] Shop Selling = true`. Stable `PickerId.ShopSelling`
maps to the centralized default title **Sellable**. Trading uses its own session,
separate Harmony owner, and independent initialization; shared picker sessions and
the global sorting fix do not depend on it. Names remain non-configurable.

Version 0.13.1 corrects section-header visibility and protects consolidated sales
against the native uninitialized capacity cache. Existing transactions are retained.

## Native integration

`Trading.FillVendorWindowData` creates the player panel from the backpack and
directly held bags. Nearby storage is excluded. `ShopSellingContract` validates
the source sections, exact native delegate owners/signatures, private trading
context fields, and the merchant window's player-widget/data fields. Unknown
panel customizations keep vanilla. Construction validates all visible instances
before replacing any sections.

`ShopSellingSession` uses the existing `PickerSnapshot`/`StackAggregation` helpers.
Display holders contain real representative references; only `UIItemCell.Value`
is set to a combined total. Stored Count, UID, ownership, and stack limits are
unchanged. The `PlainItemValidator` was extracted without changing its checks so
both existing actions and shops reject instance properties, durability, contents,
bags, and item inventory capacity. Unexpected nonstackable source counts also
restore vanilla. Every included instance is checked, including disabled items.

The category uses the native `InventoryHeaderWidgetData` with DrawHeader=true,
the centralized Sellable title, and an empty HeaderIconId. The native icon-free
decor/layout handles the header. The outer MultiInventoryWidget caption is left
unchanged, as is UIVendorWindow's separate Keeper label. Native headers return on
fallback/disable; no global header or prefab is modified.

Visibility uses native eligibility plus current/higher `VendorTierData.HasProduct`
and `IsBuyingProduct`. Full IDs preserve quality variants. Selectability uses the
original `Trading.PlayerItemsAvailableCondition`, hence `Vendor.CanBuyItemFromPlayer`.
`notBuying` products are not included merely because the merchant sells them.
Past tiers are not treated as future opportunities. Funds, happiness, and deal
capacity retain their native deal-level behavior. Future-tier rows retain native
current-tier price rendering; no future price is invented.

Dedicated hooks:

- Postfix `Trading.FillVendorWindowData`: build the selling-only session.
- Prefix `InventoryWidget.OnRedraw`: decorate only `ShopSellingInventoryData`.
- Prefix `UIVendorWindow.RedrawLite`: refresh snapshots outside pending operations.
- Postfix `UIVendorWindow.HideWindow`: release the session after native closing.
- Prefix `UIItemCountWindow.Open`: wrap confirmations only within this session's
  native quantity-opening scope. The scope is consumed before native window events.
- Prefix/finalizer `Trading.TryMoveItem`: revalidate only marked virtual sales and
  unwind the transfer scope while preserving native exceptions.

All hook targets are declared, non-generic methods. The count window inherits Hide
from a shared generic base, so it is deliberately **not** patched; IsShown polling
observes cancellation/closing. No buy, return, accept, reset, price-calculation,
inventory-add, or inventory-remove implementation is replaced.

## Transfer safety and refresh

Primary selection of stackable stock opens the native `OpenItemCountWindow` even
when its real representative count is one but the combined total is larger. Its
native stock limit, seller snap step, buttons, and per-unit price helper are retained;
the maximum is bounded by verified physical destination capacity. The price closure captures the full ID rather than a mutable
pooled cell. Secondary selection uses the original one-unit callback.

Nonstackables use that same one-unit callback for both actions. Bulk nonstackable
transfer is unsafe: native `Item.AddItemToInventory` inserts one while
`Trading.TryMoveItem` removes the requested count. The combined display must never
be passed as a real oversized stack.

Before dispatch and immediately at native transfer entry, the session verifies:

- The same live trading context, merchant, player root, staging inventory, panel,
  real item reference, and full ID are still present.
- All relevant source instances remain safe; fresh stock covers the requested
  quantity and agrees with native total counting.
- Native merchant eligibility still permits the sale, and the complete requested
  quantity fits in the real sell-deal inventory. Nonstackables are limited to one.

The pending dialog retains its validated opening maximum and its exact source cell/item.
Stock may decrease in that representative and still be usable if it remains owned
and total stock covers the request. Detached/rebound items and expired/replaced
dialogs cannot authorize transfers. Each request allows at most one native move.
Reentrant sales are rejected; gameplay callbacks are never replayed.

`ShopSellingCapacity.Available` is called before the native quantity opener,
again while binding its dialog, at transfer preflight, and at native transfer entry.
It supports the native six-slot sell-deal inventory only, rejects unexpected bags,
auto-expansion, aliases/invalid stacks/layouts, then reads the native InventoryFillSize
getter to initialize its -1 sentinel. The resulting cache must equal actual occupied
slots. A nonnegative stale cache is rejected rather than privately rewritten.
Physical capacity is matching full-ID stack space plus genuinely empty slots times
the item's stack limit, calculated using a wider integer to avoid overflow. The
final bound also honors native capacity/filter restrictions and available stock.
The scoped count-window prefix clamps Max before the native slider reads it; unsafe
confirmation counts are rejected instead of partially offered or silently retried.

The native bug is specific: Item.inventoryFillSize starts at -1, but its capacity
method reads the raw field. An empty six-slot inventory can report 7*50=350, and
Item.AddItemToInventory can split seven stacks while inserting only six. Trading
then removes the requested 350. VendorDealInventoryWidget draws from Inventory.Count
and does not initialize that cache. v0.13.0 trusted that native answer; its original
test stand-in used actual counts and therefore missed the bug. The updated fixture
retains both the sentinel and split-before-insert behavior. Reading the public getter
in active selling corrects metadata only, never stored item counts or ownership.

Root inventory notifications mark the view dirty; periodic snapshots catch raw bag
changes and tier changes. Refresh is deferred while an owned dialog or transfer is
active, keeping pooled cells stable. Navigation rebuilding preserves valid focus
and runs only when the merchant is topmost and controller input is active.

Disabling blocks confirmations immediately. After any owned quantity dialog closes,
only the player child widget is rebuilt using the native source helper and original
callbacks. Current owned bags are re-enumerated so removed bags cannot remain
exposed by stale opening-time sections. A foreign panel takeover is left intact. No staged
deal is accepted, reset, cancelled, moved, or cleared by fallback or shutdown. Live
enabling waits for the next shop opening. Invalidated wrapped callbacks remain
inert after shutdown, including when their former cells are reused.

## Native limitations retained

- The native opener checks whether the entire representative stack fits before
  showing its dialog. If it does not, primary selection may do nothing even when
  a smaller amount would fit. The secondary one-unit action remains available.
- `ResetDeal` ignores failed returns and clears staging. Native acceptance is not
  an atomic transaction either. These existing bugs remain outside this feature.
- No global capacity method is patched. A freshly opened disabled or fallback shop
  retains native behavior, including the uninitialized-cache bug. Disabling after
  a supported sale does not deliberately reset an already valid cache to -1.
- Meaningful instance data cannot safely be supported simply by separate rows:
  native trading reconstructs items and removes by definition ID. Such stock uses
  the original player panel instead of promising instance-preserving transfers.
- Arbitrary other-mod changes inside a native add/remove callback remain subject
  to the game's non-atomic transaction behavior. Preflight checks are not a global
  inventory transaction system, and native exceptions are not rolled back/retried.

## Validation

The automated suite runs production contract/session/validation/patch functions
against test boundaries that retain the relevant cache, partial-add/singleton hazards.
It checks all existing picker regressions plus merchant filtering, grouping,
distributed sales, native price-helper forwarding, pending-dialog stock/capacity/
eligibility checks, safe fallback with staged goods, toggles, reentrant callbacks,
context changes, cleanup, and controller focus. Test failures print diagnostics
and return exit code 1 rather than opening unhandled-exception dialogs.
New regressions prove the fresh disabled 350->300 loss in the fixture, the enabled
300-unit dialog bound, rejection of an explicit 350-unit callback, 50 units left
owned after a valid sale, and all 350 owned after cancellation. Partial matching
stacks, mixed/full slots, changes at dialog binding/confirmation/native entry,
stale-cache fallback, and native header restoration are covered as well.

The standalone runtime probe additionally inspects copied Assembly-CSharp.dll
metadata for the actual shop hook/delegate signatures, fields, initial -1 sentinel,
and raw-cache capacity calculation. Its seven shipped
Mono/Harmony controller tests remain sorting-fix regressions, not shop gameplay
integration tests. No game assembly methods or Unity scenes execute in these tests.
Build/probe commands are documented in README.txt and WINDOW_SORTING.md.

In-game checklist:

1. Check full-ID totals across backpack/multiple bags, qualities, currently accepted
   stock, future-tier stock, never-bought stock, and an empty Sellable category.
   Confirm its icon-free section header is visible under the unchanged Keeper
   title through refresh/reopening, and native headers return when disabled.
2. Sell across several stacks/bags. Compare source/staged totals and native quantity
   limits, per-unit/dialog/deal prices, and partial/full six-slot deal capacity.
   Start a fresh enabled shop with 350 carrots in ordinary 50-unit stacks: Max=300,
   50 remain after offering 300, and all 350 are owned after cancellation. This first
   offer is essential because later native transfers normally initialize the cache.
3. With multiple identical nonstackables, verify both actions move one; each item
   occupies a real deal slot. Check mouse and controller tips/navigation.
4. Confirm acceptance, explicit cancellation, closing, purchases/returns, merchant
   funds, sell expressions, and town happiness behave as before.
5. While a quantity dialog is open, change source stock, remove a source bag, change
   merchant eligibility, or fill staging. Stale or partially fitting confirmations
   should leave both sides unchanged. Test dialog cancellation and reopening.
6. Toggle off with staged goods and an open quantity dialog. Confirmation must not
   sell; closing the dialog restores the original player panel with staging intact.
   Re-enable and reopen the merchant to reactivate consolidation.
7. Check serialized properties/durability/contents trigger vanilla fallback without
   resetting staged deals. Inspect the once-per-category warning when falling back.
8. Check refresh beneath unrelated modals, mouse/controller mode changes, window
   reopening and pooled-cell reuse, and all existing supported actions.

Actual Unity rendering, native price expressions, money/reward effects, and native
transaction behavior require in-game validation; automated stand-ins do not prove them.
