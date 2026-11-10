using System;
using System.Collections.Generic;
using OnlyApplicableItems.Core;
using UnityEngine;

namespace OnlyApplicableItems.UI;

internal sealed class ApplicableItemsSession : IDisposable
{
    private readonly Plugin plugin;
    public PickerPolicy<Item> Policy { get; }
    private readonly MultiInventory realInventories;
    private readonly MultiInventoryWidgetData container;
    private readonly InventoryWidgetDataBase[] originalSections;
    private readonly InventoryWidgetDataBase template;
    private readonly Func<Item, bool> eligible;
    private readonly string originalHeader;
    private readonly HashSet<Inventory> subscriptions = new HashSet<Inventory>(ReferenceComparer<Inventory>.Instance);
    private ApplicableInventoryWidgetData? current;
    private MultiInventoryWidget? owner;
    private UIMultiInventoryWindow? window;
    private bool committed;
    private bool active;
    private bool dirty;
    private bool fallbackRequested;
    private float nextPoll;
    public bool IsDisposed { get; private set; }

    public ApplicableItemsSession(Plugin plugin, PickerPolicy<Item> policy, UIMultiInventoryWindowData data,
        Func<Item, bool> eligible)
    {
        this.plugin = plugin;
        Policy = policy;
        this.eligible = eligible;
        realInventories = data.MultiInventory;
        container = data.MultiInventoryWidgetData;
        originalSections = container.inventoriesData.ToArray();
        originalHeader = container.HeaderLocaleId;
        if (originalSections.Length == 0 || container.WidgetSelectionMode != MultiInventoryWidgetMode.Default)
            throw new NotSupportedException("The " + Policy.Title + " picker has an unsupported section layout.");
        template = originalSections[0];
        foreach (var section in originalSections)
        {
            if (section.CustomItemsAvailableCondition != eligible || section.CustomItemsNotShowCondition != null ||
                section.CustomItemSelectedCondition != null || section.ItemRelatedWidgetState != ItemRelatedWidgetState.Default ||
                section.OnItemCellPress != template.OnItemCellPress || section.OnItemCellPress2 != template.OnItemCellPress2 ||
                section.OnItemCellDown != template.OnItemCellDown ||
                section.ExtraRedTooltipLocIdProvider != template.ExtraRedTooltipLocIdProvider)
                throw new NotSupportedException("Another customization changed the " + Policy.Title + " picker's filtering or selection mode.");
        }
        if (template.OnItemCellPress == null)
            throw new NotSupportedException("The original " + Policy.Title + " callback is missing.");
    }

    public void Commit()
    {
        // Build completely before replacing anything. Failure here leaves vanilla data intact.
        var view = CreateView(BuildSnapshot());
        current = view;
        container.inventoriesData.Clear();
        container.inventoriesData.Add(view);
        container.HeaderLocaleId = Policy.Title;
        container.onWidgetHide += Dispose;
        committed = true;
    }

    private PickerSnapshot<Item> BuildSnapshot()
    {
        var sources = new List<StackSource<Item>>();
        var containers = new HashSet<Item>(ReferenceComparer<Item>.Instance);
        foreach (var inventory in realInventories.inventoryList)
        {
            if (inventory?.Data == null)
                throw new InvalidOperationException("An accessible inventory is no longer available.");
            Collect(inventory.Data);
            // Match the vanilla helper: direct contents plus one section per directly held bag.
            // Do not use GetInventoryFromBag here: it assigns the real bag's InventorySize.
            foreach (var item in inventory.Data.Inventory)
                if (item != null && item.IsBag)
                    Collect(item);
        }
        return PickerSnapshot<Item>.Build(sources, Policy);

        void Collect(Item sourceContainer)
        {
            if (!containers.Add(sourceContainer))
                return;
            foreach (var item in sourceContainer.Inventory)
            {
                if (item == null || item.IsEmpty)
                    continue;
                sources.Add(new StackSource<Item>(sourceContainer, item, item.id, item.Count, item.Definition.sortOrder));
            }
        }
    }

    private ApplicableInventoryWidgetData CreateView(PickerSnapshot<Item> snapshot)
    {
        var groups = snapshot.Groups;
        var display = new Inventory("inventory", groups.Count);
        foreach (var group in groups)
            display.Data.Inventory.Add(group.Representative);
        // Only the new holder/list is populated. Stored items and their Count/UID/properties
        // are never copied, split, merged, or assigned to while constructing the view.
        return new ApplicableInventoryWidgetData(this, display, snapshot, template)
        {
            OnWidgetPressed = current?.OnWidgetPressed
        };
    }

    public void Decorate(InventoryWidget widget, ApplicableInventoryWidgetData view)
    {
        if (IsDisposed || !ReferenceEquals(current, view))
            return;
        if (!active)
        {
            window = widget.GetComponentInParent<UIMultiInventoryWindow>();
            owner = widget.GetComponentInParent<MultiInventoryWidget>();
            // Register before validating the hierarchy so even an initial binding failure
            // reaches deferred recovery, after the game's current draw has finished.
            active = true;
            plugin.Register(this);
            if (owner == null || !ReferenceEquals(owner.Data, container))
                throw new InvalidOperationException("The virtual section is attached to an unexpected picker.");
            foreach (var inventory in realInventories.inventoryList)
                Subscribe(inventory);
            foreach (var section in originalSections)
                Subscribe(section.Inventory);
            nextPoll = Time.unscaledTime + 0.5f;
        }

        if (widget.Cells.Count != view.Groups.Count)
            throw new InvalidOperationException("The virtual cell count does not match its source snapshot.");
        for (int i = 0; i < view.Groups.Count; i++)
        {
            var cell = widget.Cells[i];
            if (!ReferenceEquals(cell.DisplayingItem, view.Groups[i].Representative))
                throw new InvalidOperationException("A source stack changed while the picker was drawing.");
            cell.Value = view.Groups[i].Total;
            cell.UpdateCountLabelAsRegularItem();
        }
    }

    private void Subscribe(Inventory inventory)
    {
        if (subscriptions.Add(inventory))
        {
            inventory.OnItemsAdd += OnInventoryChanged;
            inventory.OnItemsRemove += OnInventoryChanged;
            inventory.OnBagAdded += OnBagChanged;
            inventory.OnBagRemoved += OnBagChanged;
        }
    }

    private void OnInventoryChanged(List<Item> _) => dirty = true;
    private void OnBagChanged(Item _) => dirty = true;

    public void Tick()
    {
        if (IsDisposed)
            return;
        if (owner == null)
        {
            // A different prefab hierarchy cannot safely be rebound. Close its owning
            // picker through the game instead of leaving a virtual section unusable.
            if (window != null && window.IsShown)
                window.Close();
            Dispose();
            return;
        }
        if (!ReferenceEquals(owner.Data, container) || !owner.gameObject.activeInHierarchy)
        {
            Dispose();
            return;
        }
        if (fallbackRequested || !Policy.IsEnabled)
        {
            RestoreVanilla();
            return;
        }
        // Poll occasionally as well: other systems can mutate Count without notifying this wrapper.
        if (!dirty && Time.unscaledTime < nextPoll)
            return;
        dirty = false;
        nextPoll = Time.unscaledTime + 0.5f;
        try
        {
            var snapshot = BuildSnapshot();
            if (current == null || !current.Snapshot.SameAs(snapshot))
                Refresh(snapshot);
        }
        catch (Exception exception)
        {
            RequestFallback("refreshing", exception);
            RestoreVanilla();
        }
    }

    private void Refresh(PickerSnapshot<Item> snapshot)
    {
        if (container.inventoriesData.Count != 1 || !ReferenceEquals(container.inventoriesData[0], current))
            throw new InvalidOperationException("The picker sections were replaced by another system.");
        var replacement = CreateView(snapshot);
        current = replacement;
        container.inventoriesData[0] = replacement;
        // Redraw the existing child widget. Calling window.Redraw/child.Draw would run
        // MultiInventoryWidget.SetData again and create duplicate section widgets.
        var redraw = container.OnItemsAvailabilityChanged
            ?? throw new InvalidOperationException("The picker has no refresh callback.");
        redraw();
    }

    public void Select(UIItemCell cell)
    {
        if (IsDisposed || current == null)
            return;
        if (!Policy.IsEnabled || fallbackRequested)
        {
            RestoreVanilla();
            return;
        }

        Item? representative = null;
        try
        {
            string? key = null;
            foreach (var group in current.Groups)
                if (ReferenceEquals(group.Representative, cell.DisplayingItem))
                    key = group.Key;
            if (key == null)
                throw new InvalidOperationException("The clicked cell has no source group.");
            var fresh = BuildSnapshot();
            // A visible item may be disabled. Resolve only against freshly evaluated
            // selectability before Draw can restore a cell's default interaction state.
            if (!fresh.TryResolveSelectable(key, out representative) || representative == null)
            {
                Refresh(fresh);
                return;
            }
            // Only the cell is rebound. The original callback receives a currently accessible
            // real item; the original action owns selection, application, and consumption.
            cell.Draw(representative);
        }
        catch (Exception exception)
        {
            RequestFallback("resolving a selection", exception);
            RestoreVanilla();
            return;
        }

        // Deliberately outside the recovery catch: do not retry a game action after it starts.
        template.OnItemCellPress(cell);
    }

    public void RequestFallback(string operation, Exception exception)
    {
        if (IsDisposed || fallbackRequested)
            return;
        fallbackRequested = true;
        plugin.WarnOnce("session-" + Policy.Title + "-" + operation,
            "Could not safely continue " + operation + "; restoring the original " + Policy.Title + " picker. " + exception);
    }

    public void RestoreVanilla()
    {
        if (IsDisposed)
            return;
        var widget = owner;
        if (widget == null || !ReferenceEquals(widget.Data, container))
        {
            Dispose();
            return;
        }
        try
        {
            // Re-discover bags through the game's normal helper for the original access list.
            var sections = InventoryWidgetDataHelper.GetWidgetsDataForMultiInventory(realInventories,
                template.OnItemCellOver, template.OnItemCellOut, template.OnItemCellPress,
                template.OnItemCellPress2, template.OnItemCellDown, eligible);
            foreach (var section in sections)
                section.ExtraRedTooltipLocIdProvider = template.ExtraRedTooltipLocIdProvider;
            widget.Hide(); // Releases old pooled widgets; onWidgetHide disposes this session.
            container.inventoriesData.Clear();
            container.inventoriesData.AddRange(sections);
            container.HeaderLocaleId = originalHeader;
            widget.Draw(container); // A fresh setup is safe after Hide cleared all sections.
        }
        catch (Exception exception)
        {
            plugin.WarnOnce("restore-" + Policy.Title, "Could not restore the original picker; closing it safely. " + exception);
            var window = widget.GetComponentInParent<UIMultiInventoryWindow>();
            if (window != null)
                window.Close();
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;
        IsDisposed = true;
        foreach (var inventory in subscriptions)
        {
            inventory.OnItemsAdd -= OnInventoryChanged;
            inventory.OnItemsRemove -= OnInventoryChanged;
            inventory.OnBagAdded -= OnBagChanged;
            inventory.OnBagRemoved -= OnBagChanged;
        }
        subscriptions.Clear();
        if (committed)
        {
            container.onWidgetHide -= Dispose;
            container.inventoriesData.Clear();
            container.inventoriesData.AddRange(originalSections);
            container.HeaderLocaleId = originalHeader;
        }
        current = null;
        owner = null;
        window = null;
        plugin.Unregister(this);
    }
}
