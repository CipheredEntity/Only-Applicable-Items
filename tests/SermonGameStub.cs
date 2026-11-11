using System;

// Test-only native delegate boundaries. Rule represents the game's definition
// lookup; attendance, sermon execution, and rewards are deliberately not simulated.
public sealed class UIPrayWindowData
{
    public Func<Item, bool> Rule = _ => true;
    public string? SelectedDefinition { get; private set; }
    public Func<Item, bool> Predicate => CanUsePray;
    public Action<UIItemCell> Callback => cell => { SelectedDefinition = cell.DisplayingItem!.id; };
    private bool CanUsePray(Item item) => item != null && !item.IsEmpty &&
        item.Definition.type == ItemType.Preach && Rule(item);
    public bool OtherPredicate(Item item) => true;
}
