using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OnlyApplicableItems.Core;

// This component knows nothing about Unity or consumption. It only describes existing stacks.
internal sealed class StackSource<T> where T : class
{
    public object Container { get; }
    public T Item { get; }
    public string Key { get; }
    public int Count { get; }
    public int SortOrder { get; }

    public StackSource(object container, T item, string key, int count, int sortOrder)
    {
        Container = container;
        Item = item;
        Key = key;
        Count = count;
        SortOrder = sortOrder;
    }
}

internal sealed class StackGroup<T> where T : class
{
    public string Key { get; }
    public int SortOrder { get; }
    public int Total { get; private set; }
    public List<StackSource<T>> Sources { get; } = new List<StackSource<T>>();
    public T Representative => Sources[0].Item;

    public StackGroup(StackSource<T> source)
    {
        Key = source.Key;
        SortOrder = source.SortOrder;
        Add(source);
    }

    public void Add(StackSource<T> source)
    {
        Total = checked(Total + source.Count);
        Sources.Add(source);
    }
}

internal static class StackAggregation
{
    public static List<StackGroup<T>> Build<T>(IEnumerable<StackSource<T>> sources) where T : class
    {
        var groups = new Dictionary<string, StackGroup<T>>(StringComparer.Ordinal);
        var seen = new HashSet<T>(ReferenceComparer<T>.Instance);
        foreach (var source in sources)
        {
            if (source.Count <= 0 || !seen.Add(source.Item))
                continue;
            if (groups.TryGetValue(source.Key, out var group))
                group.Add(source);
            else
                groups.Add(source.Key, new StackGroup<T>(source));
        }

        var result = new List<StackGroup<T>>(groups.Values);
        result.Sort((a, b) => a.SortOrder != b.SortOrder
            ? a.SortOrder.CompareTo(b.SortOrder)
            : StringComparer.Ordinal.Compare(a.Key, b.Key));
        return result;
    }

    public static bool SameSnapshot<T>(IReadOnlyList<StackGroup<T>> a, IReadOnlyList<StackGroup<T>> b) where T : class
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Key != b[i].Key || a[i].Total != b[i].Total || a[i].Sources.Count != b[i].Sources.Count)
                return false;
            for (int j = 0; j < a[i].Sources.Count; j++)
            {
                var left = a[i].Sources[j];
                var right = b[i].Sources[j];
                if (!ReferenceEquals(left.Item, right.Item) || !ReferenceEquals(left.Container, right.Container) || left.Count != right.Count)
                    return false;
            }
        }
        return true;
    }
}

internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
{
    public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
