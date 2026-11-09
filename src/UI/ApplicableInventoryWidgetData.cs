using System.Collections.Generic;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.UI;

internal sealed class ApplicableInventoryWidgetData : InventoryWidgetData
{
    public ApplicableItemsSession Session { get; }
    public IReadOnlyList<StackGroup<Item>> Groups { get; }

    public ApplicableInventoryWidgetData(ApplicableItemsSession session, Inventory display,
        IReadOnlyList<StackGroup<Item>> groups, InventoryWidgetDataBase original)
        : base(display, new InventoryHeaderWidgetData(display, customHeaderId: "Seeds") { DrawHeader = false },
            original.OnItemCellOver, original.OnItemCellOut, session.Select, null, null,
            original.CustomItemsAvailableCondition)
    {
        Session = session;
        Groups = groups;
        ExtraRedTooltipLocIdProvider = original.ExtraRedTooltipLocIdProvider;
    }
}
