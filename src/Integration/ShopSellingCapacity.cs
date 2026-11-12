using System;
using System.Collections.Generic;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

// Only the native six-slot sell-deal inventory is supported here. This is not a
// replacement for general inventory/bag capacity rules or for native transactions.
internal static class ShopSellingCapacity
{
    internal static int Available(Inventory destination, ItemDef definition, int requested)
    {
        if (requested < 1 || definition.stackCount < 1) return 0;
        var data = destination.Data;
        int slots = data.InventorySize;
        if (slots != 6 || data.IsBag || data.HasProperty<AutoExpandSerializedItemProperty>())
            throw new NotSupportedException("The native sell-deal inventory layout changed.");

        int occupied = 0;
        long existingSpace = 0;
        var seen = new HashSet<Item>(ReferenceComparer<Item>.Instance);
        foreach (var item in data.Inventory)
        {
            // Null/empty entries, bags, aliases and oversized stacks are not valid
            // rows for the fixed deal widget. Never turn them into extra capacity.
            if (item == null || item.IsEmpty || item.IsBag || !seen.Add(item) ||
                item.Definition.stackCount < 1 || item.Count > item.Definition.stackCount)
                throw new NotSupportedException("Unexpected item data in the sell-deal inventory.");
            occupied++;
            if (item.id == definition.id)
            {
                if (item.Definition.stackCount != definition.stackCount)
                    throw new NotSupportedException("Inconsistent stack limits for one item definition.");
                existingSpace += definition.stackCount - item.Count;
            }
        }
        if (occupied > slots) throw new NotSupportedException("The sell-deal inventory exceeds its slot limit.");

        // Native capacity reads the raw cache, initially -1. Initialize it via its
        // own getter before *any* native capacity query or add can use that value.
        // Do not write private fields or repair an unexplained nonnegative cache.
        if (data.InventoryFillSize != occupied || data.InventorySize != slots)
            throw new NotSupportedException("The sell-deal occupancy cache disagrees with its actual contents.");

        long physical = existingSpace + (long)(slots - occupied) * definition.stackCount;
        int native = data.CanAddItemCountToInventory(definition, requested);
        if (native < 0) throw new NotSupportedException("Invalid native sell-deal capacity.");
        // Retain native filters while bounding the answer by actual slots/stacks.
        return (int)Math.Min(Math.Min((long)requested, physical), native);
    }
}
