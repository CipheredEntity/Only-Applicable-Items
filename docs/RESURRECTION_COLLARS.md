# Resurrection collars

`[Actions] Resurrection Collars = true` independently controls the resurrection
table's collar picker. It defaults on without changing saved Zombie Equipment
or other settings. Both collar pickers display **Collars**, while their stable
IDs are `ResurrectionCollars` and `ZombieCollars`. Default titles are centralized
in `Core/PickerIdentity.cs`; name customization is not implemented.

## Integration

- A separate Harmony owner patches only
  `UIResurrectionWindowData.OnCollarPressed`, using a prefix and finalizer to
  bracket picker construction. Initialization failure leaves earlier features
  available. No native consumption or resurrection-start method is patched.
- The existing shared constructor postfix recognizes the active opening scope.
  It requires single callback/predicate delegates declared on and bound to that
  exact resurrection-data instance. Unknown wrappers, owners, or layouts retain
  vanilla. Compiler-generated lambda names are never matched.
- Scope restoration handles nested calls and exceptions; native exceptions are
  returned unchanged. After construction, the policy retains the original native
  predicate, so refresh and click-time validation do not require an active scope.
- The real `UIMultiInventoryWindowData.MultiInventory` remains untouched. Its
  default access includes the player, eligible current-zone storage, and bags
  exposed by the game's helper. Zombie upgrades still exclude zone storage.
- Visibility includes every nonempty ItemType.Collar. The unchanged native
  predicate uses ItemDef.SkullsInBorders(WhiteSkulls, RedSkulls) for selectability.
  Unsuitable collars remain visible and disabled; unrelated items stay hidden.
- Plain items group by full definition ID. The existing validator rejects
  instance properties, durability, bags, internal contents, or capacity, even
  for disabled collars, and restores the original interface on failure.
- The shared session decorates combined counts without changing stored Count,
  UID, or ownership. Selection refreshes real sources and forwards a currently
  accessible representative to the original callback. The callback retains the
  actual item in Collar and updates the normal resurrection UI.

Disabling restores an open picker on the next plugin frame; enabling applies on
the next opening. Neither setting alters a selection already retained by the
resurrection window.

## Delayed consumption and native limitations

The game reserves nothing when a collar is selected. PrepareResurrection later
asks the originally captured MultiInventory to remove one unit by selected UID,
then creates the zombie using the collar definition ID. One unit never requires
consumption across multiple stacks. No virtual count is passed to consumption.

CanStartResurrectionWithReason checks Collar.IsEmpty and skull ranges, but does
not verify that the UID still exists in an accessible inventory. Whole-stack
removal can leave the retained object nonempty, and the native multi-inventory
removal does not report failure to resurrection. Consequently, changes after
selection can leave a stale native selection. The mod preserves this behavior:
it does not reserve a collar, rebind after selection, prevent preparation, or
silently consume another equivalent stack. Gameplay reachability of those changes
requires in-game testing; this is not a stale-selection fix.

## Verification

The checks compile the production contract, scope patches, settings declarations,
and shared policy/snapshot/view code against test-only stand-ins. They verify
owner/action isolation, nested/exception cleanup, visible disabled entries,
equivalent totals and variants, source retention, settings independence, and
distinct collar IDs with unchanged titles. They do not execute native skull
arithmetic, Harmony interception, Unity rendering, or resurrection consumption.

Manual checks:

1. Put equivalent collars in player inventory, bags, and eligible zone storage.
   Check Collars, exact combined totals, separate definitions, and no other items.
2. Test bodies with different skull totals: unsuitable collars must remain
   visible but unselectable, and suitable collars must follow native eligibility.
3. Select, cancel, reopen, and prepare resurrection. Selection/cancellation alone
   consumes nothing; preparation consumes one unit from a real source.
4. Test depletion or movement before clicking, where gameplay permits it. If a
   source changes after selection, compare with vanilla; no new behavior is added.
5. Toggle Resurrection Collars and Zombie Equipment independently. Check live
   disabling, identical default titles, zone-access differences, and regression
   behavior for all existing actions, mouse input, controllers, and empty lists.
