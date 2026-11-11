using System;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal sealed class StudyTablePickerContract
{
    private readonly MethodInfo eligible;
    private readonly Func<bool> enabled;
    private readonly Action<Item> validateItem;

    public StudyTablePickerContract(Func<bool> enabled, Action<Item> validateItem)
    {
        this.enabled = enabled;
        this.validateItem = validateItem;
        eligible = typeof(UIResourceBasedCraftWindowData).GetMethod("IsItemAllowed",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
            null, new[] { typeof(Item) }, null)
            ?? throw new MissingMethodException(typeof(UIResourceBasedCraftWindowData).FullName, "IsItemAllowed");
        if (eligible.ReturnType != typeof(bool))
            throw new NotSupportedException("The study table's eligibility signature has changed.");
    }

    public PickerPolicy<Item>[]? GetPolicies(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
    {
        if (callback == null || !(predicate?.Target is UIResourceBasedCraftWindowData owner) ||
            !DelegateContext.MatchesPredicate(callback, predicate, eligible) ||
            !ReferenceEquals(callback.Target, owner) || callback.Method.DeclaringType != typeof(UIResourceBasedCraftWindowData))
            return null;

        bool Matches(Item item, bool science)
        {
            if (item == null || item.IsEmpty || !predicate(item))
                return false;
            var survey = GameBalance.GetSurveyDefForItemOrNull(item.id);
            return survey != null && survey.isScienceFuelCraft == science;
        }
        bool Research(Item item) => Matches(item, false);
        bool Decompose(Item item) => Matches(item, true);
        // Reuse the native cached eligibility list and resolver. Do not synthesize
        // alternative surveys, completion rules, or resource-based picker locks.
        return new[]
        {
            new PickerPolicy<Item>(PickerId.StudyResearchable, enabled, Research, Research, validateItem),
            new PickerPolicy<Item>(PickerId.StudyDecomposable, enabled, Decompose, Decompose, validateItem)
        };
    }
}
