using System;
using System.Collections.Generic;
using OnlyApplicableItems.Core;
using UnityEngine;

namespace OnlyApplicableItems.UI;

internal sealed class ApplicableItemsSession : IDisposable
{
    private readonly Plugin plugin;
    private readonly PickerPolicy<Item>[] policies;
    public PickerPolicy<Item> Policy => policies[0];
    private bool IsEnabled => Array.TrueForAll(policies, policy => policy.IsEnabled);
    private readonly MultiInventory realInventories;
    private readonly MultiInventoryWidgetData container;
    private readonly InventoryWidgetDataBase[] originalSections;
    private readonly InventoryWidgetDataBase template;
    private readonly Func<Item, bool> eligible;
    private readonly string originalHeader;
    private readonly string replacementHeader;
    private readonly HashSet<Inventory> subscriptions = new HashSet<Inventory>(ReferenceComparer<Inventory>.Instance);
    private ApplicableInventoryWidgetData[]? current;
    private MultiInventoryWidget? owner;
    private UIMultiInventoryWindow? window;
    private bool committed;
    private bool active;
    private bool dirty;
    private bool fallbackRequested;
    private float nextPoll;
    public bool IsDisposed { get; private set; }
    internal MultiInventory Sources => realInventories;
    internal bool IsCurrent => !IsDisposed && OwnsSections && owner != null &&
        ReferenceEquals(owner.Data, container) && owner.gameObject.activeInHierarchy;
    internal Action<UIItemCell, Action>? SelectionDispatch { get; set; }

    private bool OwnsSections => current != null && MatchesSections(current);

    private bool MatchesSections(IReadOnlyList<InventoryWidgetDataBase> sections)
    {
        if (container.inventoriesData.Count != sections.Count)
            return false;
        for (int i = 0; i < sections.Count; i++)
            if (!ReferenceEquals(container.inventoriesData[i], sections[i]))
                return false;
        return true;
    }

    public ApplicableItemsSession(Plugin plugin, PickerPolicy<Item> policy, UIMultiInventoryWindowData data,
        Func<Item, bool> eligible)
        : this(plugin, new[] { policy }, data, eligible)
    {
    }

    public ApplicableItemsSession(Plugin plugin, IReadOnlyList<PickerPolicy<Item>> policies, UIMultiInventoryWindowData data,
        Func<Item, bool> eligible, Func<PickerId, string>? getTitle = null)
    {
        this.plugin = plugin;
        if (policies.Count == 0)
            throw new ArgumentException("A picker needs at least one section.", nameof(policies));
        this.policies = new List<PickerPolicy<Item>>(policies).ToArray();
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
                section.OnItemCellPress != template.OnItemCellPress || section.OnItemCellPress2 != null ||
                section.OnItemCellDown != null ||
                section.ExtraRedTooltipLocIdProvider != template.ExtraRedTooltipLocIdProvider)
                throw new NotSupportedException("Another customization changed the " + Policy.Title + " picker's filtering or selection mode.");
        }
        if (template.OnItemCellPress == null)
            throw new NotSupportedException("The original " + Policy.Title + " callback is missing.");
        // Capture titles at opening. Only the recognized ordered Study pair gets
        // a separate window title; refresh, selection, and restoration stay native.
        replacementHeader = policies.Count == 1 ? PickerTitles.GetHeaderText(Policy.Id, Policy.Title) : originalHeader;
        if (policies.Count == 2 && policies[0].Id == PickerId.StudyResearchable && policies[1].Id == PickerId.StudyDecomposable)
            replacementHeader = PickerTitles.GetHeaderText(PickerId.StudyTableHeader,
                getTitle?.Invoke(PickerId.StudyTableHeader) ?? PickerTitles.GetDefault(PickerId.StudyTableHeader));
    }

    public void Commit()
    {
        // Build completely before replacing anything. Failure here leaves vanilla data intact.
        var views = CreateViews(BuildSnapshots());
        current = views;
        container.inventoriesData.Clear();
        container.inventoriesData.AddRange(views);
        container.HeaderLocaleId = replacementHeader;
        container.onWidgetHide += Dispose;
        committed = true;
    }

    private PickerSnapshot<Item>[] BuildSnapshots()
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
        return PickerSnapshot<Item>.BuildAll(sources, policies);

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

    private ApplicableInventoryWidgetData[] CreateViews(PickerSnapshot<Item>[] snapshots)
    {
        var views = new ApplicableInventoryWidgetData[policies.Length];
        for (int i = 0; i < policies.Length; i++)
        {
            var groups = snapshots[i].Groups;
            var display = new Inventory("inventory", groups.Count);
            foreach (var group in groups)
                display.Data.Inventory.Add(group.Representative);
            // Only new holders/lists are populated. Real Count/UID/ownership is untouched.
            views[i] = new ApplicableInventoryWidgetData(this, display, snapshots[i], template,
                policies[i], i, policies.Length > 1)
            {
                OnWidgetPressed = current == null ? null : current[i].OnWidgetPressed
            };
        }
        return views;
    }

    public void Decorate(InventoryWidget widget, ApplicableInventoryWidgetData view)
    {
        if (IsDisposed || current == null || view.SectionIndex < 0 || view.SectionIndex >= current.Length ||
            !ReferenceEquals(current[view.SectionIndex], view))
            return;
        if (!OwnsSections)
        {
            Dispose();
            return;
        }
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
            if (view.Policy.Id == PickerId.StudyResearchable && view.Policy.IsEnabled)
                StudyTableVisuals.HideResearchQuantity(cell);
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
        if (!OwnsSections)
        {
            Dispose();
            return;
        }
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
        if (fallbackRequested || !IsEnabled)
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
            var snapshots = BuildSnapshots();
            bool changed = current == null;
            for (int i = 0; current != null && i < snapshots.Length; i++)
                changed |= !current[i].Snapshot.SameAs(snapshots[i]);
            if (changed)
                Refresh(snapshots);
        }
        catch (Exception exception)
        {
            RequestFallback("refreshing", exception);
            RestoreVanilla();
        }
    }

    private void Refresh(PickerSnapshot<Item>[] snapshots)
    {
        if (!OwnsSections)
            throw new InvalidOperationException("The picker sections were replaced by another system.");
        var replacements = CreateViews(snapshots);
        var selected = container.SelectedWidgetData;
        int selectedIndex = selected == null ? -1 : container.inventoriesData.IndexOf(selected);
        current = replacements;
        for (int i = 0; i < replacements.Length; i++)
            container.inventoriesData[i] = replacements[i];
        if (selectedIndex >= 0)
            container.SelectedWidgetData = replacements[selectedIndex];
        if (!OwnsSections || (owner != null && !ReferenceEquals(owner.Data, container)))
        {
            Dispose();
            return;
        }
        // Redraw the existing child widget. Calling window.Redraw/child.Draw would run
        // MultiInventoryWidget.SetData again and create duplicate section widgets.
        var redraw = container.OnItemsAvailabilityChanged
            ?? throw new InvalidOperationException("The picker has no refresh callback.");
        redraw();
    }

    public void Select(UIItemCell cell) => Select(0, cell);

    public void Select(int sectionIndex, UIItemCell cell)
    {
        if (IsDisposed || current == null || sectionIndex < 0 || sectionIndex >= current.Length)
            return;
        if (!OwnsSections || (owner != null && !ReferenceEquals(owner.Data, container)))
        {
            Dispose();
            return;
        }
        if (!IsEnabled || fallbackRequested)
        {
            RestoreVanilla();
            return;
        }

        Item? representative = null;
        try
        {
            string? key = null;
            foreach (var group in current[sectionIndex].Groups)
                if (ReferenceEquals(group.Representative, cell.DisplayingItem))
                    key = group.Key;
            if (key == null)
                throw new InvalidOperationException("The clicked cell has no source group.");
            var fresh = BuildSnapshots();
            if (!OwnsSections || (owner != null && !ReferenceEquals(owner.Data, container)))
            {
                Dispose();
                return;
            }
            // A visible item may be disabled. Resolve only against freshly evaluated
            // selectability before Draw can restore a cell's default interaction state.
            if (!fresh[sectionIndex].TryResolveSelectable(key, out representative) || representative == null)
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

        if (!OwnsSections || (owner != null && !ReferenceEquals(owner.Data, container)))
        {
            Dispose();
            return;
        }
        // Deliberately outside the recovery catch: do not retry a game action after it starts.
        if (SelectionDispatch != null) SelectionDispatch(cell, () => template.OnItemCellPress!(cell));
        else template.OnItemCellPress!(cell);
    }

    public void RequestFallback(string operation, Exception exception)
    {
        if (IsDisposed || fallbackRequested)
            return;
        fallbackRequested = true;
        plugin.WarnOnce("session-" + Policy.Id + "-" + operation,
            "Could not safely continue " + operation + "; restoring the original " + Policy.Title + " picker. " + exception);
    }

    public void RestoreVanilla()
    {
        if (IsDisposed)
            return;
        var widget = owner;
        if (widget == null || !ReferenceEquals(widget.Data, container) || !OwnsSections)
        {
            Dispose();
            return;
        }
        IReadOnlyList<InventoryWidgetDataBase> expected = current!;
        try
        {
            // Re-discover bags through the game's normal helper for the original access list.
            var sections = InventoryWidgetDataHelper.GetWidgetsDataForMultiInventory(realInventories,
                template.OnItemCellOver, template.OnItemCellOut, template.OnItemCellPress,
                template.OnItemCellPress2, template.OnItemCellDown, eligible);
            foreach (var section in sections)
                section.ExtraRedTooltipLocIdProvider = template.ExtraRedTooltipLocIdProvider;
            if (!ReferenceEquals(widget.Data, container) || !OwnsSections)
                return;
            expected = current!;
            // Unsubscribe before Hide invokes other mods' callbacks. Keep the
            // owned views as an identity marker until native restoration is ready.
            Detach(restoreSections: false);
            widget.Hide();
            if (!ReferenceEquals(widget.Data, container) || !MatchesSections(expected))
                return;
            container.inventoriesData.Clear();
            container.inventoriesData.AddRange(sections);
            expected = sections;
            container.SelectedWidgetData = null;
            if (!ReferenceEquals(widget.Data, container) || !MatchesSections(expected))
                return;
            container.HeaderLocaleId = originalHeader;
            widget.Draw(container); // A fresh setup is safe after Hide cleared all sections.
        }
        catch (Exception exception)
        {
            plugin.WarnOnce("restore-" + Policy.Id, "Could not restore the original picker; closing it safely. " + exception);
            var window = widget.GetComponentInParent<UIMultiInventoryWindow>();
            if (window != null && ReferenceEquals(widget.Data, container) && MatchesSections(expected))
                window.Close();
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose() => Detach(restoreSections: true);

    private void Detach(bool restoreSections)
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
            if (restoreSections && OwnsSections && (owner == null || ReferenceEquals(owner.Data, container)))
            {
                container.inventoriesData.Clear();
                container.inventoriesData.AddRange(originalSections);
                container.SelectedWidgetData = null;
                container.HeaderLocaleId = originalHeader;
            }
        }
        current = null;
        SelectionDispatch = null;
        owner = null;
        window = null;
        plugin.Unregister(this);
    }
}
