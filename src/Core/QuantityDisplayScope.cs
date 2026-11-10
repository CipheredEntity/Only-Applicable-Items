using System;
using System.Collections.Generic;

namespace OnlyApplicableItems.Core;

// Supplies owned totals only while the native renderer runs. Stored stacks, slot
// selections, required quantities, and crafting calculations are outside this scope.
internal sealed class QuantityDisplayScope<T> : IDisposable where T : class
{
    private readonly List<Entry> entries;
    private readonly Action<T, int> setCachedCount;
    private int applied;
    private bool disposed;
    public IReadOnlyDictionary<string, int> Totals { get; }

    private QuantityDisplayScope(List<Entry> entries, Dictionary<string, int> totals, Action<T, int> setCachedCount)
    {
        this.entries = entries;
        Totals = totals;
        this.setCachedCount = setCachedCount;
    }

    public static QuantityDisplayScope<T> Apply(IEnumerable<T> cells, Func<T, string> getId,
        Func<string, int> getTotal, Func<T, int> getCachedCount, Action<T, int> setCachedCount)
    {
        var entries = new List<Entry>();
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new HashSet<T>(ReferenceComparer<T>.Instance);
        // Prepare everything first: a counting failure must not leave partially changed cells.
        foreach (var cell in cells)
        {
            if (!seen.Add(cell))
                continue;
            string id = getId(cell);
            if (!totals.TryGetValue(id, out int total))
            {
                total = getTotal(id);
                if (total < 0)
                    throw new InvalidOperationException("The accessible quantity is outside the supported display range.");
                totals.Add(id, total);
            }
            entries.Add(new Entry(cell, getCachedCount(cell), total));
        }

        var scope = new QuantityDisplayScope<T>(entries, totals, setCachedCount);
        try
        {
            foreach (var entry in entries)
            {
                // Include this entry in rollback even if its setter changes the value and then throws.
                scope.applied++;
                setCachedCount(entry.Cell, entry.Total);
            }
            return scope;
        }
        catch (Exception applyFailure)
        {
            try { scope.Dispose(); }
            catch (Exception restoreFailure) { throw new AggregateException(applyFailure, restoreFailure); }
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        Exception? failure = null;
        for (int i = applied - 1; i >= 0; i--)
        {
            try { setCachedCount(entries[i].Cell, entries[i].Original); }
            catch (Exception exception) { failure = failure ?? exception; }
        }
        if (failure != null)
            throw failure;
    }

    private sealed class Entry
    {
        public T Cell { get; }
        public int Original { get; }
        public int Total { get; }
        public Entry(T cell, int original, int total) { Cell = cell; Original = original; Total = total; }
    }
}
