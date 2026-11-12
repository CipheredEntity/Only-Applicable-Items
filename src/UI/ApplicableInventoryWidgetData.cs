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
        : base(display, CreateHeader(display, policy, showHeader),
            original.OnItemCellOver, original.OnItemCellOut, cell => session.Select(sectionIndex, cell), null, null,
            policy.CanSelect)
    {
        Session = session;
        Policy = policy;
        SectionIndex = sectionIndex;
        Snapshot = snapshot;
        ExtraRedTooltipLocIdProvider = original.ExtraRedTooltipLocIdProvider;
    }

    private static InventoryHeaderWidgetData CreateHeader(Inventory display, PickerPolicy<Item> policy, bool showHeader)
    {
        var header = new InventoryHeaderWidgetData(display, customHeaderId: policy.Title) { DrawHeader = showHeader };
        if ((policy.Id == PickerId.StudyResearchable || policy.Id == PickerId.StudyDecomposable) && policy.IsEnabled)
            header.HeaderIconId = string.Empty; // Native icon-free layout; title stays visible.
        return header;
    }
}
