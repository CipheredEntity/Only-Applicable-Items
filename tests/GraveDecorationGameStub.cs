using System;

// Test-only stand-ins for the production contract; no game assemblies are executed.
// NativeRule can simulate an additional restriction, without redefining game crafts.
public enum GraveElementType { Top, Bot }

public static class GameConsts
{
    public static class Groups
    {
        public const string TOMBSTONE_ITEM_GROUP = "gravetop";
        public const string FENCE_ITEM_GROUP = "gravebot";
    }
}

public sealed class UIGraveElementWidgetData
{
    public GraveElementType GraveElementType { get; private set; }
    public Func<Item, bool> NativeRule = _ => true;
    public string? SelectedDefinition { get; private set; }
    public Action<UIItemCell> Callback => OnGraveItemSelected;
    public Func<Item, bool> Predicate => IsGraveItem;

    public UIGraveElementWidgetData(GraveElementType category) { GraveElementType = category; }
    private void OnGraveItemSelected(UIItemCell cell) { SelectedDefinition = cell.DisplayingItem!.id; }
    private bool IsGraveItem(Item item) => item != null && (GraveElementType switch
    {
        GraveElementType.Top => item.Definition.itemGroupIds.Contains("gravetop"),
        GraveElementType.Bot => item.Definition.itemGroupIds.Contains("gravebot"),
        _ => false
    }) && NativeRule(item);
}
