using System;

// Only models the verified selected-slot draw boundary: the actual game creates
// a display Item from the selected ID and total stock. No craft code is simulated.
public sealed class UIResourceBasedCraftWindow
{
    private readonly UIItemCell mainIngredient;
    public Item? SelectedItem;
    public int MainIngredientCount;
    public bool IsShown = true;
    public bool FailRedraw;
    public int NativeRedraws;
    public Action<UIResourceBasedCraftWindow, UIItemCell>? AfterRedraw;

    public UIResourceBasedCraftWindow(UIItemCell cell) { mainIngredient = cell; }
    public void Redraw()
    {
        NativeRedraws++;
        if (FailRedraw) throw new InvalidOperationException("Native study redraw failed");
        mainIngredient.Draw(SelectedItem == null || SelectedItem.IsEmpty ? null :
            new Item(SelectedItem.id, SelectedItem.Definition.type, MainIngredientCount));
        AfterRedraw?.Invoke(this, mainIngredient);
    }
    public void Hide() { IsShown = false; }
}
