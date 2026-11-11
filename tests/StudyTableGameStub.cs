using System;
using System.Collections.Generic;

// Tests supply the native allowed list and resolver result. No knowledge system,
// balance precedence, or research/decomposition consumption is simulated here.
public sealed class SurveyDef { public bool isScienceFuelCraft; }
public static class GameBalance
{
    public static readonly Dictionary<string, SurveyDef> ResolvedSurveys = new();
    public static SurveyDef? GetSurveyDefForItemOrNull(string id)
        => ResolvedSurveys.TryGetValue(id, out var survey) ? survey : null;
}
public sealed class UIResourceBasedCraftWindowData
{
    public HashSet<string> Allowed { get; } = new();
    public string? SelectedDefinition { get; private set; }
    public Action<UIItemCell> Callback => cell => { SelectedDefinition = cell.DisplayingItem!.id; };
    public Func<Item, bool> Predicate => IsItemAllowed;
    private bool IsItemAllowed(Item item) => item != null && Allowed.Contains(item.id);
    public bool OtherPredicate(Item item) => true;
}
