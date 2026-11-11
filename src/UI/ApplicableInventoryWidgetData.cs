using System.Collections.Generic;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.UI;

internal sealed class ApplicableInventoryWidgetData : InventoryWidgetData
{
    public ApplicableItemsSession Session { get; }
    public PickerSnapshot<Item> Snapshot { get; }
    public IReadOnlyList<StackGroup<Item>> Groups => Snapshot.Groups;

    public ApplicableInventoryWidgetData(ApplicableItemsSession session, Inventory display,
        PickerSnapshot<Item> snapshot, InventoryWidgetDataBase original)
        : base(display, new InventoryHeaderWidgetData(display, customHeaderId: session.Policy.Title) { DrawHeader = false },
            original.OnItemCellOver, original.OnItemCellOut, session.Select, null, null,
            session.Policy.CanSelect)
    {
        Session = session;
        Snapshot = snapshot;
        ExtraRedTooltipLocIdProvider = original.ExtraRedTooltipLocIdProvider;
    }
}
