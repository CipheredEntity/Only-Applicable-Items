# PR Machine resources

Version 0.15.0 adds `[Actions] PR Machine = true` and stable
`PickerId.PrMachineResources`, titled **PR Resources** by default. The independent
`[Inventory Names] PR Machine` entry defaults to blank and uses the existing name
fallback, menu metadata, and next-opening configuration refresh.

## Native contract (1.012)

`UIPanicReductionMachineWindowData.OnResourcePickerPressed` constructs a loader
MultiInventory and a shared picker with OpenItemCountWindow/IsItemAllowed on the
same data owner. The native Load definitions determine eligibility by full ID.
The player, world-zone storage marked OpenInMultiInventory, and directly contained
bags are accessible. There is no bounded machine inventory/queue in this path;
supplies immediately extend the player's PR operating-days resource. The native
outer window retains its loading-day restriction and days-left display.

The native count dialog initially selects its maximum, formerly one source stack.
OnResourcePicked clamps to that stack and removes by UID before applying effects.
The UID helper's boolean means that *some* quantity was removed, not necessarily
the requested quantity. Merely increasing the dialog maximum or bypassing its
stack clamp would therefore be unsafe.

## Isolated integration

- PrMachineContract validates named callback/predicate methods, loader ownership,
  the machine window data field, and effect definitions. Picker and loader roots
  must match by inventory reference. The shared session retains responsibility
  for aggregation, validation, section ownership, refresh, and vanilla fallback.
- The shared session's optional SelectionDispatch wraps only this policy's native
  selection callback. All existing policies retain their original dispatch path.
- PrMachineFeatures uses a short quantity-opening scope and an independent prefix
  on the declared UIItemCountWindow.Open. It consumes the scope before window
  events, keeps native dialog controls, changes Max to the total, and replaces
  confirmation with a one-use request carrying a full ID and owner/session tokens.
- Confirmation requires the original live machine, current owned picker, original
  native roots, enabled setting, and a live matching dialog. Fresh source enumeration
  permits stock to move or be replenished within those roots. Insufficient stock,
  expired/replaced/cancelled dialogs, or changed ownership reject without mutation.
  The old pooled cell never determines which ID is loaded.
- PrMachineNative patches only OnResourcePicked and TryRemoveLoadedItem, under a
  separate Harmony owner. One guarded transpiler replaces the single selected-item
  getter with a scoped read of a detached count carrier. The native clamp remains;
  its input now describes the validated request. The carrier's fresh UID cannot
  consume real stock if the removal hook is absent. Outside the authorized scope,
  the getter, clamp, removal, and effects all retain native behavior.
- The removal prefix commits the exact prepared request once, then returns true to
  the original effect body. Operating days, late-day adjustment, activation,
  SelectedItem, selection expressions, redraws, and picker closing remain native.
  Reentrant native entry and removal attempts are blocked during that operation.
  Existing/late foreign patches to those two PR methods or the source access,
  count, split, UID-removal, and occupancy methods replaced by the raw commit
  cause conservative fallback rather than silently bypassing another mod.

## Consumption and failure boundary

PrMachineStock snapshots root identity, direct lists/bags, item references, counts,
full IDs, unique UIDs, and container property membership. Every eligible source
passes PlainItemValidator. Aliases, duplicate UIDs, invalid stack sizes, and
dynamically resized take-only containers remain vanilla. Deeper bag contents are
not silently included. Potential operating-day integer overflow is rejected.

All edits and notification lists are prepared before mutation. Final validation
uses raw fields and List indexers. Commit then performs only prevalidated raw count
and occupancy-cache stores and descending List.RemoveAt operations. Emitted field
accessors are resolved and warmed on detached data at initialization; no Item
methods, allocations, inventory observers, predicates, or reflection setters run
in the destructive phase. This is main-thread-only and has no fallible native
partial-removal loop to roll back. Partial stacks retain their original objects
and UIDs; exhausted stacks are removed without changing their detached counts,
matching native storage semantics. Nothing is physically merged or moved to make
the display.

Removal notifications use real source references, grouped by native root inventory,
and are delivered once after native effects (also if the native body throws).
Observers see complete consumption; a failing observer is logged, and notifications
for other roots still run. This ordering intentionally differs from native
per-stack removal, where a notification occurs before the effects. A mod requiring
that earlier timing needs explicit compatibility testing.

Native post-consumption effect/expression exceptions retain their exception identity
and are never retried or rolled back. No mod can safely undo arbitrary native
expression side effects. This implementation does not promise recovery from process
termination, memory corruption, or foreign code concurrently mutating Unity data
off the main thread. Such mutations are unsupported by the native inventory too.

## Automated and in-game validation

The two original investigation tests first failed against intentionally unsafe
models: trusting partial UID success, and prevalidation followed by notifying
per-stack removal. They now retain those reproductions and check the production
planner's safeguards. Regression checks exercise real production session, request,
planning and commit code against stand-ins. The Mono probe runs the actual Harmony
bridge/raw accessors against an in-memory PR effect boundary, and Cecil verifies
the matching seams and field shapes in the external 1.012 assembly. Neither suite
executes game assembly methods or claims to validate Unity rendering.

In-game checklist:

1. Split one supply type across backpack, bags, and accessible storage. Confirm
   the combined row/dialog maximum, a quantity larger than any stack, partial
   consumption, and complete exhaustion. Check exact before/after stock totals.
2. Test Hypno-devices and Newspapers together. Confirm their separate rows and
   correct operating-day increases. Non-supplies and inaccessible storage stay out.
3. Cancel, reopen, and change quantities using mouse and controller. The native
   non-vendor dialog initially selects the full combined maximum.
4. Verify a stopped/running machine, late-day loading, the normal loading-day
   restriction, upgrade state, days-left display, activation, and save/reload.
5. Change stock while a dialog is pending if another mod permits it. Sufficient
   current stock may load; insufficient stock must leave all remaining supplies
   untouched. Reopened windows and obsolete confirmations must consume nothing.
6. Disable PR Machine before opening and while a dialog is pending. Confirm native
   restoration, inert obsolete requests, independent actions, and custom-name
   persistence. Test blank/custom/duplicate names at the next picker opening.
7. Exercise trading and other quantity dialogs before/after PR loading. With UI or
   inventory mods, check warnings, complete stock conservation, and event-dependent
   displays; verify that incompatible PR patches leave the original picker intact.
