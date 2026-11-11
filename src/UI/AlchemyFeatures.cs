using System;
using System.Collections.Generic;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;
using UnityEngine;

namespace OnlyApplicableItems.UI;

internal sealed class AlchemyFeatures : IDisposable
{
    private readonly Plugin plugin;
    private readonly AlchemyContract contract;
    private readonly Action<Item> validateItem;
    private readonly Dictionary<UIAlchemyWindow, LaboratoryState> laboratories =
        new Dictionary<UIAlchemyWindow, LaboratoryState>(ReferenceComparer<UIAlchemyWindow>.Instance);
    private bool faulted;
    private bool disposed;
    public bool IsEnabled => !faulted && !disposed && plugin.Settings.AlchemicalIngredients.Value;

    public AlchemyFeatures(Plugin plugin, AlchemyContract contract, Action<Item> validateItem)
    {
        this.plugin = plugin;
        this.contract = contract;
        this.validateItem = validateItem;
    }

    public PickerPolicy<Item>? GetPolicy(Action<UIItemCell> callback, Func<Item, bool> predicate)
    {
        if (!(predicate?.Target is UIAlchemyWindow) ||
            !DelegateContext.MatchesPredicate(callback, predicate, contract.IngredientPredicate))
            return null;
        // Research/type controls visibility. The unchanged native predicate still disables
        // ingredients blocked by any rune limit and revalidates them before selection.
        return new PickerPolicy<Item>(PickerId.AlchemicalIngredients, () => IsEnabled,
            contract.CanDisplayIngredient, predicate, validateItem);
    }

    public QuantityDisplayScope<UIItemCell>? BeginCountUpdate(UIAlchemyWindow window)
    {
        if (disposed || !window.IsShown)
            return null;
        if (!laboratories.TryGetValue(window, out var state))
        {
            state = new LaboratoryState();
            laboratories.Add(window, state);
        }
        QuantityDisplayScope<UIItemCell>? scope = null;
        try
        {
            if (!IsEnabled)
            {
                state.LastEnabled = false;
                state.Totals.Clear();
                return null;
            }
            MultiInventory? inventories = null;
            scope = QuantityDisplayScope<UIItemCell>.Apply(contract.GetPopulatedCells(window),
                cell => cell.DisplayingItem.id,
                id => (inventories = inventories ?? MainGame.PlayerController.WorkerMultiInventory).GetTotalCount(id),
                cell => cell.HasItemCount,
                SetCachedCount);
            state.LastEnabled = true;
            state.Totals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var total in scope.Totals)
                state.Totals.Add(total.Key, total.Value);
            state.NextPoll = Time.unscaledTime + 0.5f;
            return scope;
        }
        catch (Exception exception)
        {
            try { scope?.Dispose(); }
            catch (Exception restoreFailure) { Disable("restoring a failed count update", restoreFailure); }
            Disable("counting", exception);
            state.LastEnabled = false;
            return null; // The original method still renders its untouched native counters.
        }
    }

    private static void SetCachedCount(UIItemCell cell, int count)
    {
        if (cell != null)
            cell.HasItemCount = count;
    }

    public void Tick()
    {
        if (disposed)
            return;
        foreach (var pair in new List<KeyValuePair<UIAlchemyWindow, LaboratoryState>>(laboratories))
        {
            var window = pair.Key;
            var state = pair.Value;
            if (window == null || !window.IsShown || !window.gameObject.activeInHierarchy)
            {
                laboratories.Remove(window!);
                continue;
            }
            try
            {
                // Option changes refresh immediately. Source changes are coalesced at 0.5s.
                bool refresh = IsEnabled != state.LastEnabled;
                if (!refresh && IsEnabled && Time.unscaledTime >= state.NextPoll)
                {
                    state.NextPoll = Time.unscaledTime + 0.5f;
                    refresh = !SameTotals(state.Totals, ReadTotals(window));
                }
                if (refresh)
                    contract.Redraw(window);
            }
            catch (Exception exception)
            {
                // Do not repeatedly invoke a native redraw that is itself failing.
                laboratories.Remove(window);
                Disable("refreshing", exception);
            }
        }
    }

    private Dictionary<string, int> ReadTotals(UIAlchemyWindow window)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        MultiInventory? inventories = null;
        foreach (var cell in contract.GetPopulatedCells(window))
        {
            string id = cell.DisplayingItem.id;
            if (!totals.ContainsKey(id))
                totals.Add(id, (inventories = inventories ?? MainGame.PlayerController.WorkerMultiInventory).GetTotalCount(id));
        }
        return totals;
    }

    private static bool SameTotals(Dictionary<string, int> left, Dictionary<string, int> right)
    {
        if (left.Count != right.Count)
            return false;
        foreach (var entry in left)
            if (!right.TryGetValue(entry.Key, out int total) || total != entry.Value)
                return false;
        return true;
    }

    public void Forget(UIAlchemyWindow window) => laboratories.Remove(window);

    public void Disable(string operation, Exception exception)
    {
        faulted = true;
        plugin.WarnOnce("alchemy-" + operation,
            "Alchemical Ingredients is inactive for this plugin session after a display failure while " + operation + ". " + exception);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true; // Hooks now let the native renderer use its original cached counts.
        foreach (var window in new List<UIAlchemyWindow>(laboratories.Keys))
        {
            if (window != null && window.IsShown && window.gameObject.activeInHierarchy)
            {
                try { contract.Redraw(window); }
                catch (Exception exception) { plugin.WarnOnce("alchemy-shutdown", "Could not restore laboratory labels. " + exception); }
            }
        }
        laboratories.Clear();
    }

    private sealed class LaboratoryState
    {
        public bool LastEnabled;
        public float NextPoll;
        public Dictionary<string, int> Totals = new Dictionary<string, int>(StringComparer.Ordinal);
    }
}
