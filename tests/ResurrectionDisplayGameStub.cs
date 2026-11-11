using System;

// Only models the selected cell and the redraw boundary used by production code.
// The callback stands in for Harmony's postfix; native resurrection is not run.
public sealed class UIResurrectionWindow
{
    private readonly UIItemCell collarCell;
    public bool IsShown { get; set; } = true;
    public bool FailRedraw;
    public Action<UIResurrectionWindow>? AfterRedraw;
    public int NativeRedraws { get; private set; }
    public UIResurrectionWindow(UIItemCell cell) { collarCell = cell; }

    private void RedrawLite()
    {
        NativeRedraws++;
        if (FailRedraw)
            throw new InvalidOperationException("Native redraw failed");
        collarCell.Value = collarCell.DisplayingItem?.Count ?? 0;
        collarCell.UpdateCountLabel();
        AfterRedraw?.Invoke(this);
    }

    public void RedrawForCheck() => RedrawLite();
    public void Hide() { IsShown = false; }
}
