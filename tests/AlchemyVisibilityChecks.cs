using System;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private static AlchemyVisibility<AlchemyIngredient, string> CreateAlchemyVisibility(FakeLaboratory laboratory)
        => new AlchemyVisibility<AlchemyIngredient, string>(
            item => item.Count > 0,
            item => item.CanBeUsedInAlchemy,
            item => item.Runes != 0 || item.Green != 0 || item.Blue != 0,
            item => item.HasSurvey ? item.Id : null,
            laboratory.Researched.Contains);

    private static void AlchemyVisibilityPreservesSurveyExceptions()
    {
        var laboratory = new FakeLaboratory();
        var visibility = CreateAlchemyVisibility(laboratory);
        var noSurveyNoRunes = new AlchemyIngredient("neutral", 1, 0, hasSurvey: false);
        var noSurveyWithRunes = new AlchemyIngredient("missing_research_path", 1, 1, hasSurvey: false);
        var unknownNeutral = new AlchemyIngredient("unknown_neutral", 1, 0);
        var known = new AlchemyIngredient("known", 1, 0);
        laboratory.Researched.Add(known.Id);
        Assert(visibility.CanDisplay(noSurveyNoRunes), "Vanilla allows zero-rune alchemical items with no survey.");
        Assert(!visibility.CanDisplay(noSurveyWithRunes), "Nonzero runes with no survey must remain hidden.");
        Assert(!visibility.CanDisplay(unknownNeutral), "A zero-rune item must still satisfy an existing survey.");
        Assert(visibility.CanDisplay(known), "Honor the completion API's successful result.");
        laboratory.Researched.Add(unknownNeutral.Id);
        Assert(visibility.CanDisplay(unknownNeutral), "Research changes must be observed without cached completion state.");
        laboratory.Researched.Remove(known.Id);
        Assert(!visibility.CanDisplay(known), "A failed completion result must not be bypassed.");
    }

    private static void AlchemyVisibilityHidesNonAlchemyEmptyAndNullItems()
    {
        int researchReads = 0;
        var visibility = new AlchemyVisibility<AlchemyIngredient, string>(
            item => item.Count > 0, item => item.CanBeUsedInAlchemy,
            _ => true, item => item.Id, _ => { researchReads++; return true; });
        Assert(!visibility.CanDisplay(null), "Null slots must remain hidden.");
        Assert(!visibility.CanDisplay(new AlchemyIngredient("empty", 0, 1)), "Empty stacks must remain hidden.");
        Assert(!visibility.CanDisplay(new AlchemyIngredient("stone", 99, 1, canBeUsedInAlchemy: false)),
            "Even researched non-alchemical items must remain hidden.");
        Assert(researchReads == 0, "Non-candidates must not trigger survey-completion queries.");
    }

    private static void AlchemyRuneBoundsRemainNativeSelectionRestrictions()
    {
        var laboratory = new FakeLaboratory();
        var red = new AlchemyIngredient("red_overflow", 10, 6);
        var green = new AlchemyIngredient("green_overflow", 20, 0, green: 6);
        var blue = new AlchemyIngredient("blue_overflow", 30, 0, blue: 6);
        var underflow = new AlchemyIngredient("negative_rune", 40, 0, green: -1);
        var items = new[] { red, green, blue, underflow };
        foreach (var item in items)
            laboratory.Researched.Add(item.Id);
        var visibility = CreateAlchemyVisibility(laboratory);
        var policy = new PickerPolicy<AlchemyIngredient>("Alchemical Ingredients", () => true,
            visibility.CanDisplay, laboratory.IsValid, _ => { });
        var snapshot = PickerSnapshot<AlchemyIngredient>.Build(Array.ConvertAll(items, AlchemySource), policy);
        Assert(snapshot.Groups.Count == 4, "Research visibility must not impose upper or lower rune bounds.");
        foreach (var item in items)
            Assert(!snapshot.TryResolveSelectable(item.Id, out _), "Every native rune restriction must still block selection.");
    }

    private static void AlchemyRuneAvailabilityChangesRefreshWithoutChangingStock()
    {
        var laboratory = new FakeLaboratory();
        var item = new AlchemyIngredient("ingredient", 100, 4);
        laboratory.Researched.Add(item.Id);
        var visibility = CreateAlchemyVisibility(laboratory);
        var policy = new PickerPolicy<AlchemyIngredient>("Alchemical Ingredients", () => true,
            visibility.CanDisplay, laboratory.IsValid, _ => { });
        var sources = new[] { AlchemySource(item) };
        laboratory.OtherRunes = 2;
        var disabled = PickerSnapshot<AlchemyIngredient>.Build(sources, policy);
        laboratory.OtherRunes = 1;
        var enabled = PickerSnapshot<AlchemyIngredient>.Build(sources, policy);
        Assert(StackAggregation.SameSnapshot(disabled.Groups, enabled.Groups), "The same entry and total must stay visible.");
        Assert(!disabled.SameAs(enabled) && enabled.TryResolveSelectable(item.Id, out _),
            "A change to native selectability alone must trigger a full cell redraw.");
        laboratory.OtherRunes = 2;
        var freshAtClick = PickerSnapshot<AlchemyIngredient>.Build(sources, policy);
        Assert(!enabled.SameAs(freshAtClick) && !freshAtClick.TryResolveSelectable(item.Id, out _),
            "Click-time revalidation must block an entry that exceeded the limit after it was drawn enabled.");
        Assert(freshAtClick.Groups.Count == 1 && freshAtClick.Groups[0].Total == 100 && item.Count == 100,
            "Rejected selection must preserve visibility, totals, and real stock.");
    }
}
