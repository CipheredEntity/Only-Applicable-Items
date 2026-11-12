using System;
using System.Collections.Generic;

namespace OnlyApplicableItems.Core;

// Visibility and selectability are deliberately independent: an action's relevant
// items can remain visible while the native predicate disables interaction.
internal sealed class PickerPolicy<T> where T : class
{
    public PickerId Id { get; }
    public string Title { get; }
    public bool IsEnabled => enabled();
    public Func<T, bool> IncludeInDisplay { get; }
    public Func<T, bool> CanSelect { get; }
    public Action<T> ValidateItem { get; }
    public Func<T, string?>? ExtraRedTooltipLocIdProvider { get; }
    private readonly Func<bool> enabled;

    public PickerPolicy(PickerId id, Func<bool> enabled, Func<T, bool> includeInDisplay,
        Func<T, bool> canSelect, Action<T> validateItem, Func<T, string?>? extraRedTooltipLocIdProvider = null,
        string? title = null)
    {
        Id = id;
        Title = title ?? PickerTitles.GetDefault(id);
        this.enabled = enabled;
        IncludeInDisplay = includeInDisplay;
        CanSelect = canSelect;
        ValidateItem = validateItem;
        ExtraRedTooltipLocIdProvider = extraRedTooltipLocIdProvider;
    }

    public PickerPolicy<T> WithTitle(string title)
        => new PickerPolicy<T>(Id, enabled, IncludeInDisplay, CanSelect, ValidateItem, ExtraRedTooltipLocIdProvider, title);
}

internal sealed class PickerSnapshot<T> where T : class
{
    public IReadOnlyList<StackGroup<T>> Groups { get; }
    private readonly bool[] selectable;
    private readonly string?[]? warnings;

    private PickerSnapshot(List<StackGroup<T>> groups, PickerPolicy<T> policy)
    {
        Groups = groups;
        selectable = new bool[groups.Count];
        if (policy.ExtraRedTooltipLocIdProvider != null)
            warnings = new string?[groups.Count];
        for (int i = 0; i < groups.Count; i++)
        {
            selectable[i] = policy.CanSelect(groups[i].Representative);
            if (warnings != null)
                warnings[i] = policy.ExtraRedTooltipLocIdProvider!(groups[i].Representative);
        }
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
        return new PickerSnapshot<T>(StackAggregation.Build(included), policy);
    }

    // Materialize sources once and finish every section before publishing any
    // view. Ambiguous membership or a failure in either section rejects the plan.
    public static PickerSnapshot<T>[] BuildAll(IEnumerable<StackSource<T>> sources,
        IReadOnlyList<PickerPolicy<T>> policies)
    {
        if (policies.Count == 0)
            throw new ArgumentException("A picker needs at least one section.", nameof(policies));
        var materialized = new List<StackSource<T>>(sources);
        var ids = new HashSet<PickerId>();
        var assigned = new HashSet<T>(ReferenceComparer<T>.Instance);
        var result = new PickerSnapshot<T>[policies.Count];
        for (int i = 0; i < policies.Count; i++)
        {
            if (!ids.Add(policies[i].Id))
                throw new NotSupportedException("Picker sections must have distinct identities.");
            result[i] = Build(materialized, policies[i]);
            foreach (var group in result[i].Groups)
                foreach (var source in group.Sources)
                    if (!assigned.Add(source.Item))
                        throw new NotSupportedException("An item belongs to more than one picker section.");
        }
        return result;
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
            // A disabled item can change rejection reason without changing stock or selectability.
            if (selectable[i] != other.selectable[i] ||
                !string.Equals(warnings?[i], other.warnings?[i], StringComparison.Ordinal))
                return false;
        return true;
    }
}
