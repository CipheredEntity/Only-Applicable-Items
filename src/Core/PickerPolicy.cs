using System;
using System.Collections.Generic;

namespace OnlyApplicableItems.Core;

// Visibility and selectability are deliberately independent: applied fertilizers
// remain visible, while the original game predicate still controls interaction.
internal sealed class PickerPolicy<T> where T : class
{
    public string Title { get; }
    public bool IsEnabled => enabled();
    public Func<T, bool> IncludeInDisplay { get; }
    public Func<T, bool> CanSelect { get; }
    public Action<T> ValidateItem { get; }
    private readonly Func<bool> enabled;

    public PickerPolicy(string title, Func<bool> enabled, Func<T, bool> includeInDisplay,
        Func<T, bool> canSelect, Action<T> validateItem)
    {
        Title = title;
        this.enabled = enabled;
        IncludeInDisplay = includeInDisplay;
        CanSelect = canSelect;
        ValidateItem = validateItem;
    }
}

internal sealed class PickerSnapshot<T> where T : class
{
    public IReadOnlyList<StackGroup<T>> Groups { get; }
    private readonly bool[] selectable;

    private PickerSnapshot(List<StackGroup<T>> groups, Func<T, bool> canSelect)
    {
        Groups = groups;
        selectable = new bool[groups.Count];
        for (int i = 0; i < groups.Count; i++)
            selectable[i] = canSelect(groups[i].Representative);
    }

    public static PickerSnapshot<T> Build(IEnumerable<StackSource<T>> sources, PickerPolicy<T> policy)
    {
        var included = new List<StackSource<T>>();
        foreach (var source in sources)
        {
            if (source.Count <= 0 || !policy.IncludeInDisplay(source.Item))
                continue;
            // Validate visible items even when they cannot currently be selected.
            policy.ValidateItem(source.Item);
            included.Add(source);
        }
        return new PickerSnapshot<T>(StackAggregation.Build(included), policy.CanSelect);
    }

    public bool TryResolveSelectable(string key, out T? item)
    {
        for (int i = 0; i < Groups.Count; i++)
        {
            if (Groups[i].Key == key && selectable[i])
            {
                item = Groups[i].Representative;
                return true;
            }
        }
        item = null;
        return false;
    }

    public bool SameAs(PickerSnapshot<T> other)
    {
        if (!StackAggregation.SameSnapshot(Groups, other.Groups))
            return false;
        for (int i = 0; i < selectable.Length; i++)
            if (selectable[i] != other.selectable[i])
                return false;
        return true;
    }
}
