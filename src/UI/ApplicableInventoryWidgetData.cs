using System.Collections.Generic;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.UI;

internal sealed class ApplicableInventoryWidgetData : InventoryWidgetData
{
    public ApplicableItemsSession Session { get; }
    public PickerPolicy<Item> Policy { get; }
    public int SectionIndex { get; }
    public PickerSnapshot<Item> Snapshot { get; }
    public IReadOnlyList<StackGroup<Item>> Groups => Snapshot.Groups;

    public ApplicableInventoryWidgetData(ApplicableItemsSession session, Inventory display,
        PickerSnapshot<Item> snapshot, InventoryWidgetDataBase original)
        : this(session, display, snapshot, original, session.Policy, 0, false)
    {
    }

    public ApplicableInventoryWidgetData(ApplicableItemsSession session, Inventory display,
        PickerSnapshot<Item> snapshot, InventoryWidgetDataBase original, PickerPolicy<Item> policy,
        int sectionIndex, bool showHeader)
        : base(display, new InventoryHeaderWidgetData(display, customHeaderId: policy.Title) { DrawHeader = showHeader },
            original.OnItemCellOver, original.OnItemCellOut, cell => session.Select(sectionIndex, cell), null, null,
            policy.CanSelect)
    {
        Session = session;
        Policy = policy;
        SectionIndex = sectionIndex;
        Snapshot = snapshot;
        ExtraRedTooltipLocIdProvider = original.ExtraRedTooltipLocIdProvider;
    }
}
