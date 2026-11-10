using System;

namespace OnlyApplicableItems.Core;

// The non-slot-specific part of UIAlchemyWindow.IsItemValidForMix. The game
// still supplies survey lookup/completion, and its full predicate controls selection.
internal sealed class AlchemyVisibility<TItem, TSurvey> where TItem : class where TSurvey : class
{
    private readonly Func<TItem, bool> isNonEmpty;
    private readonly Func<TItem, bool> canBeUsedInAlchemy;
    private readonly Func<TItem, bool> hasRunes;
    private readonly Func<TItem, TSurvey?> findSurvey;
    private readonly Func<TSurvey, bool> isSurveyCompleted;

    public AlchemyVisibility(Func<TItem, bool> isNonEmpty, Func<TItem, bool> canBeUsedInAlchemy,
        Func<TItem, bool> hasRunes, Func<TItem, TSurvey?> findSurvey, Func<TSurvey, bool> isSurveyCompleted)
    {
        this.isNonEmpty = isNonEmpty;
        this.canBeUsedInAlchemy = canBeUsedInAlchemy;
        this.hasRunes = hasRunes;
        this.findSurvey = findSurvey;
        this.isSurveyCompleted = isSurveyCompleted;
    }

    public bool CanDisplay(TItem? item)
    {
        if (item == null || !isNonEmpty(item) || !canBeUsedInAlchemy(item))
            return false;
        bool runesPresent = hasRunes(item);
        var survey = findSurvey(item);
        // Match vanilla: nonzero runes require a survey; even zero-rune items must
        // satisfy IsSurveyCompleted if a survey exists. No rune-limit checks here.
        return survey == null ? !runesPresent : isSurveyCompleted(survey);
    }
}
