using System;
using System.Collections.Generic;
using LazyBearTechnology;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;
using UnityEngine;

namespace OnlyApplicableItems.UI;

internal sealed class ShopSellingSession
{
    private readonly ShopSellingFeatures feature;
    private readonly Action<Item> validate;
    private readonly InventoryWidgetDataBase[] originals;
    private readonly string originalHeader;
    private readonly Func<Item, bool> nativeEligible;
    private readonly Action<UIItemCell> nativeSingle;
    private readonly Action<UIItemCell, Inventory, Inventory, UIItemCountWindowData.PriceCalculateDelegate> openCount;
    private readonly Func<string, int, int> unitPrice;
    private readonly MultiInventoryWidgetData container;
    private readonly Vendor vendor;
    private ShopSellingInventoryData? current;
    private MultiInventoryWidget? owner;
    internal UIVendorWindow? Window { get; private set; }
    internal Trading Trading { get; }
    internal UIVendorWindowData Data { get; }
    internal Inventory Source { get; }
    internal Inventory Destination { get; }
    internal ShopSellingFeatures Feature => feature;
    internal bool Detached { get; private set; }
    private bool fallback;
    private bool dirty = true;
    private bool navigationDirty;
    private bool bound;
    private float nextPoll;

    public ShopSellingSession(ShopSellingFeatures feature, Trading trading, UIVendorWindowData data, Action<Item> validate)
    {
        this.feature = feature; this.validate = validate;
        Trading = trading; Data = data; vendor = data.Vendor;
        Source = MainGame.PlayerData.inventory;
        Destination = feature.Contract.SellInventory(trading);
        container = data.PlayerMultiInventoryWidgetData;
        originals = container.inventoriesData.ToArray();
        originalHeader = container.HeaderLocaleId;
        if (!feature.Contract.IsCurrent(trading, data, Destination) || container.WidgetSelectionMode != MultiInventoryWidgetMode.Default ||
            container.onWidgetHide != null || originals.Length == 0)
            throw new NotSupportedException("Unsupported merchant player panel.");
        var expected = Containers();
        if (originals.Length != expected.Count) throw new NotSupportedException("Merchant source sections changed.");
        foreach (var original in originals)
        {
            if (!expected.Remove(original.Inventory.Data) ||
                !ShopSellingContract.Matches(original.CustomItemsAvailableCondition, feature.Contract.Predicate, trading) ||
                !ShopSellingContract.Matches(original.OnItemCellPress, feature.Contract.Primary, trading) ||
                !ShopSellingContract.Matches(original.OnItemCellPress2, feature.Contract.Secondary, trading) ||
                original.CustomItemsNotShowCondition != null || original.CustomItemSelectedCondition != null ||
                original.OnItemCellDown != null || original.ItemRelatedWidgetState != ItemRelatedWidgetState.Default ||
                original.ExtraRedTooltipLocIdProvider != originals[0].ExtraRedTooltipLocIdProvider ||
                original.OnItemCellOver != originals[0].OnItemCellOver || original.OnItemCellOut != originals[0].OnItemCellOut)
                throw new NotSupportedException("Another customization changed merchant sources or actions.");
        }
        nativeEligible = originals[0].CustomItemsAvailableCondition!; // Exact non-null delegates checked above.
        nativeSingle = originals[0].OnItemCellPress2!;
        openCount = feature.Contract.OpenCount(trading);
        unitPrice = feature.Contract.UnitPrice(trading);
    }

    private HashSet<Item> Containers()
    {
        var containers = new HashSet<Item>(ReferenceComparer<Item>.Instance) { Source.Data };
        foreach (var item in Source.Data.Inventory)
            if (item != null && !item.IsEmpty && item.IsBag) containers.Add(item);
        return containers;
    }

    private bool Relevant(Item item)
    {
        if (item == null || item.IsEmpty) return false;
        if (vendor.CurTier < 1 || vendor.CurTier > vendor.Definition.tierDataList.Count)
            throw new NotSupportedException("Invalid merchant tier.");
        if (nativeEligible(item)) return true;
        for (int i = vendor.CurTier - 1; i < vendor.Definition.tierDataList.Count; i++)
        {
            var tier = vendor.Definition.tierDataList[i];
            if (tier.HasProduct(item.id) && tier.IsBuyingProduct(item.id)) return true;
        }
        return false;
    }

    private PickerSnapshot<Item> Snapshot()
    {
        if (!feature.Contract.IsCurrent(Trading, Data, Destination) || !ReferenceEquals(Data.Vendor, vendor) ||
            !ReferenceEquals(MainGame.PlayerData.inventory, Source) || !ReferenceEquals(Data.PlayerMultiInventoryWidgetData, container))
            throw new InvalidOperationException("The merchant context changed.");
        if (current != null && !OwnsSections)
            throw new NotSupportedException("Another customization replaced the selling sections.");
        var sources = new List<StackSource<Item>>();
        // Preserve native root-first, then directly held bag order for representatives.
        var containers = new List<Item> { Source.Data };
        var seen = new HashSet<Item>(ReferenceComparer<Item>.Instance) { Source.Data };
        foreach (var bag in Source.Data.Inventory)
            if (bag != null && !bag.IsEmpty && bag.IsBag && seen.Add(bag)) containers.Add(bag);
        foreach (var inventory in containers)
            foreach (var item in inventory.Inventory)
                if (item != null && !item.IsEmpty)
                    sources.Add(new StackSource<Item>(inventory, item, item.id, item.Count, item.Definition.sortOrder));
        var policy = new PickerPolicy<Item>(PickerId.ShopSelling, () => feature.Enabled, Relevant, nativeEligible, item =>
        {
            validate(item); // Every relevant instance, including disabled higher-tier items.
            if (item.Definition.stackCount < 1 || (item.Definition.stackCount == 1 && item.Count != 1))
                throw new NotSupportedException("Unexpected merchant source stack.");
        });
        return PickerSnapshot<Item>.Build(sources, policy);
    }

    internal void Commit()
    {
        var view = CreateView(Snapshot()); // Finish validation before touching native section data.
        container.SelectedWidgetData = null;
        container.inventoriesData.Clear(); container.inventoriesData.Add(view);
        current = view;
        Source.OnItemsAdd += Changed; Source.OnItemsRemove += Changed;
        Source.OnBagAdded += BagChanged; Source.OnBagRemoved += BagChanged;
        nextPoll = Time.unscaledTime + 0.5f;
    }

    private ShopSellingInventoryData CreateView(PickerSnapshot<Item> snapshot)
    {
        var inventory = new Inventory("inventory", snapshot.Groups.Count);
        foreach (var group in snapshot.Groups) inventory.Data.Inventory.Add(group.Representative);
        return new ShopSellingInventoryData(this, inventory, snapshot, originals[0], nativeEligible)
        { OnWidgetPressed = current?.OnWidgetPressed };
    }

    internal void Decorate(InventoryWidget widget, ShopSellingInventoryData view)
    {
        if (Detached || !ReferenceEquals(view, current)) return;
        var candidate = widget.GetComponentInParent<MultiInventoryWidget>();
        var window = widget.GetComponentInParent<UIVendorWindow>();
        if (candidate == null || window == null || !ReferenceEquals(candidate.Data, container) ||
            !ReferenceEquals(feature.Contract.PlayerWidget(window), candidate) || !ReferenceEquals(feature.Contract.WindowData(window), Data))
            throw new InvalidOperationException("Unexpected selling panel hierarchy.");
        owner = candidate; Window = window; bound = true;
        if (widget.Cells.Count != view.Snapshot.Groups.Count) throw new InvalidOperationException("Merchant cells changed.");
        for (int i = 0; i < widget.Cells.Count; i++)
        {
            var group = view.Snapshot.Groups[i]; var cell = widget.Cells[i];
            if (!ReferenceEquals(cell.DisplayingItem, group.Representative)) throw new InvalidOperationException("Merchant source cell changed.");
            cell.Value = group.Total;
            cell.UpdateCountLabelAsRegularItem();
        }
    }

    internal bool OwnsCell(UIItemCell cell)
    {
        if (owner == null || current == null) return false;
        foreach (var widget in owner.DrawnInventories)
            if (ReferenceEquals(widget.Data, current))
                foreach (var candidate in widget.Cells)
                    if (ReferenceEquals(cell, candidate)) return true;
        return false;
    }

    private bool Usable => !Detached && !fallback && feature.Enabled && Window != null && Window.IsShown && owner != null &&
        ReferenceEquals(feature.Contract.WindowData(Window), Data) && ReferenceEquals(owner.Data, container) &&
        ReferenceEquals(Data.PlayerMultiInventoryWidgetData, container) &&
        OwnsSections;
    private bool OwnsSections => current != null && container.inventoriesData.Count == 1 && ReferenceEquals(container.inventoriesData[0], current);

    internal void Click(UIItemCell cell, bool secondary)
    {
        ShopSaleRequest? request = null; bool single = false;
        try
        {
            if (!Usable || feature.Busy || !OwnsCell(cell) || cell.DisplayingItem == null) return;
            var snapshot = Snapshot();
            foreach (var group in snapshot.Groups)
                if (ReferenceEquals(group.Representative, cell.DisplayingItem) && nativeEligible(group.Representative))
                {
                    request = new ShopSaleRequest(this, cell, group.Representative);
                    single = secondary || group.Total == 1 || group.Representative.Definition.stackCount == 1;
                    break;
                }
            dirty = true;
        }
        catch (Exception exception) { RequestFallback("preparing a sale", exception); return; }
        if (request == null) return;
        // Native actions run outside recovery catches. Never retry a transaction.
        if (single) feature.Transfer(request, 1, () => nativeSingle(cell));
        else feature.OpenQuantity(request, () => openCount(cell, Source, Destination, amount =>
        {
            int price = 0;
            for (int i = 0; i < amount; i++) price = unchecked(price + unitPrice(request.Id, i + 1));
            return price;
        }));
    }

    internal bool ValidateTransfer(ShopSaleRequest request, int count)
        => count >= 1 && (request.Item.Definition.stackCount != 1 || count == 1) && AvailableCapacity(request) >= count;

    internal int AvailableCapacity(ShopSaleRequest request)
    {
        if (!Usable || !request.Valid || !ReferenceEquals(request.Cell.DisplayingItem, request.Item) ||
            request.Item.id != request.Id || !OwnsCell(request.Cell)) return 0;
        var snapshot = Snapshot();
        foreach (var group in snapshot.Groups)
        {
            if (group.Key != request.Id) continue;
            bool owns = false;
            foreach (var source in group.Sources) if (ReferenceEquals(source.Item, request.Item)) owns = true;
            if (Source.Data.GetTotalCountInInventory(request.Id) != group.Total)
                throw new NotSupportedException("Native and displayed source totals disagree.");
            if (!owns || !nativeEligible(request.Item)) return 0;
            return ShopSellingCapacity.Available(Destination, request.Item.Definition, group.Total);
        }
        return 0;
    }

    internal void BeforeRedraw()
    {
        if (Detached || fallback || !feature.Enabled || feature.Busy) { dirty = true; return; }
        try { Refresh(false); }
        catch (Exception exception) { RequestFallback("refreshing selling stock", exception); }
    }

    private bool Refresh(bool force)
    {
        var snapshot = Snapshot();
        if (!force && current != null && current.Snapshot.SameAs(snapshot)) return false;
        var view = CreateView(snapshot);
        container.inventoriesData[0] = view; current = view;
        navigationDirty = true; dirty = true;
        return true;
    }

    internal void Tick()
    {
        if (Detached) return;
        if (!feature.Enabled) fallback = true;
        if (feature.Busy) return; // Retain pooled cells until native callbacks/dialogs finish.
        if (fallback) { Restore(); return; }
        if (bound && (owner == null || Window == null)) { Detach(); return; }
        if (owner == null || Window == null) return;
        if (!Window.IsShown) { Detach(); return; }
        if (!dirty && Time.unscaledTime < nextPoll)
        { if (navigationDirty) RefreshNavigation(); return; }
        dirty = false; nextPoll = Time.unscaledTime + 0.5f;
        try
        {
            if (Refresh(false))
            {
                Window.RedrawLite();
            }
            if (navigationDirty) RefreshNavigation();
        }
        catch (Exception exception) { RequestFallback("updating selling stock", exception); }
    }

    private void RefreshNavigation()
    {
        if (!LazyInput.IsGamepadActive) { navigationDirty = false; return; }
        if (Window == null || !Window.IsShownAndTop) return;
        var controller = Window.GetComponent<GamepadNavigationController>();
        if (controller == null) return;
        var focused = controller.FocusedItem;
        controller.ReinitItems(focusOnFirstActive: false);
        if (focused != null && focused.isActiveAndEnabled && focused.Active) controller.SetFocusedItem(focused);
        else controller.FocusOnFirstActive();
        navigationDirty = false;
    }

    internal void RequestFallback(string operation, Exception exception)
    {
        fallback = true;
        feature.Invalidate(this);
        feature.Warn(operation, exception);
    }
    internal void MarkDirty() => dirty = true;
    private void Changed(List<Item> items) => dirty = true;
    private void BagChanged(Item item) => dirty = true;

    internal void Restore()
    {
        if (Detached || feature.Busy) return;
        // Hide only the player's child widget. Never close the merchant or reset deals.
        var widget = owner;
        bool redraw = widget != null && Window != null && Window.IsShown &&
            ReferenceEquals(feature.Contract.WindowData(Window), Data) && ReferenceEquals(widget.Data, container) &&
            ReferenceEquals(Data.PlayerMultiInventoryWidgetData, container) && OwnsSections;
        // Re-enumerate native sources: opening-time sections can refer to bags no
        // longer owned. Reuse the game's helper and exact original callbacks.
        List<InventoryWidgetDataBase>? restored = null;
        if (redraw)
        {
            var template = originals[0];
            restored = InventoryWidgetDataHelper.GetWidgetsDataForInventory(MainGame.PlayerData.inventory,
                template.OnItemCellOver, template.OnItemCellOut, template.OnItemCellPress, template.OnItemCellPress2,
                template.OnItemCellDown, nativeEligible);
            foreach (var section in restored)
            {
                section.DrawEmptyCellsAsDisabledWhenUnavailable = true;
                section.ExtraRedTooltipLocIdProvider = template.ExtraRedTooltipLocIdProvider;
            }
        }
        Detach(restored);
        if (redraw)
        {
            widget!.Hide();
            widget.Draw(container);
            Window!.RedrawLite();
            RefreshNavigation();
        }
    }

    internal void Detach(List<InventoryWidgetDataBase>? restored = null)
    {
        if (Detached) return;
        Detached = true;
        feature.Invalidate(this);
        Source.OnItemsAdd -= Changed; Source.OnItemsRemove -= Changed;
        Source.OnBagAdded -= BagChanged; Source.OnBagRemoved -= BagChanged;
        if (OwnsSections)
        {
            container.inventoriesData.Clear();
            if (restored != null) container.inventoriesData.AddRange(restored);
            else container.inventoriesData.AddRange(originals);
            container.SelectedWidgetData = null;
            container.HeaderLocaleId = originalHeader;
        }
        current = null;
    }
}

internal sealed class ShopSellingInventoryData : InventoryWidgetData
{
    internal ShopSellingSession Session { get; }
    internal PickerSnapshot<Item> Snapshot { get; }
    public ShopSellingInventoryData(ShopSellingSession session, Inventory inventory, PickerSnapshot<Item> snapshot,
        InventoryWidgetDataBase original, Func<Item, bool> eligible)
        : base(inventory, new InventoryHeaderWidgetData(inventory, headerIconId: string.Empty,
            customHeaderId: PickerTitles.GetDefault(PickerId.ShopSelling)) { DrawHeader = true },
            original.OnItemCellOver, original.OnItemCellOut, cell => session.Click(cell, false), cell => session.Click(cell, true), null, eligible)
    {
        Session = session; Snapshot = snapshot;
        DrawEmptyCellsAsDisabledWhenUnavailable = true;
        ExtraRedTooltipLocIdProvider = original.ExtraRedTooltipLocIdProvider;
    }
}

internal sealed class ShopSaleRequest
{
    internal readonly ShopSellingSession Session;
    internal readonly UIItemCell Cell;
    internal readonly Item Item;
    internal readonly string Id;
    internal bool Valid = true;
    internal bool Used;
    internal ShopSellingFeatures Feature => Session.Feature;
    internal ShopSaleRequest(ShopSellingSession session, UIItemCell cell, Item item)
    { Session = session; Cell = cell; Item = item; Id = item.id; }
}
