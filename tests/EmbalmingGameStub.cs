using System;

// Test-only stand-in for the production contract's reflection/delegate boundary.
// NativeRule simulates a changing eligibility result, not the game's craft rules.
public sealed class UIEmbalmWindowData
{
    public Func<Item, bool> NativeRule = _ => true;
    public Item? Selected { get; private set; }
    public Action<UIItemCell> Callback => TryEmbalm;
    public Func<Item, bool> Predicate => CanEmbalm;
    private void TryEmbalm(UIItemCell cell) { Selected = cell.DisplayingItem; }
    private bool CanEmbalm(Item item) => item != null && !item.IsEmpty &&
        item.Definition.type == ItemType.Embalm && NativeRule(item);
}
