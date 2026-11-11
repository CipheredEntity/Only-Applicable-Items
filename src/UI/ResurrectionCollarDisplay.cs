using System;
using System.Collections.Generic;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.UI;

// Only the resurrection window's selected slot is rendered as one collar. The
// selected Item and even the cell's native cached Value are left unchanged.
internal sealed class ResurrectionCollarDisplay : IDisposable
{
    private readonly Func<bool> enabled;
    private readonly Action<string, Exception> warn;
    private readonly FieldInfo collarCell;
    private readonly Dictionary<UIResurrectionWindow, bool> windows =
        new Dictionary<UIResurrectionWindow, bool>(ReferenceComparer<UIResurrectionWindow>.Instance);
    private bool faulted;
    private bool disposed;
    private bool IsEnabled => !disposed && !faulted && enabled();
    public MethodInfo Redraw { get; }
    public MethodInfo Hide { get; }

    public ResurrectionCollarDisplay(Func<bool> enabled, Action<string, Exception> warn)
    {
        this.enabled = enabled;
        this.warn = warn;
        collarCell = typeof(UIResurrectionWindow).GetField("collarCell", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(UIResurrectionWindow).FullName, "collarCell");
        if (collarCell.FieldType != typeof(UIItemCell))
            throw new NotSupportedException("The selected resurrection collar cell representation has changed.");
        Redraw = RequireMethod("RedrawLite");
        Hide = RequireMethod("Hide");
    }

    public void AfterRedraw(UIResurrectionWindow window)
    {
        if (disposed || faulted)
            return;
        try
        {
            bool correct = IsEnabled;
            windows[window] = correct;
            if (!correct)
                return; // Disabled means the native draw is completely untouched.
            var cell = (UIItemCell?)collarCell.GetValue(window)
                ?? throw new InvalidOperationException("The selected resurrection collar cell is missing.");
            var item = cell.DisplayingItem;
            if (item == null || item.IsEmpty || item.Definition.type != ItemType.Collar)
                return;

            int nativeValue = cell.Value;
            try
            {
                cell.Value = 1;
                // Unlike UpdateCountLabelAsRegularItem, this does NOT force a
                // visible counter. Native collar rendering hides a count of one.
                cell.UpdateCountLabel();
            }
            finally { cell.Value = nativeValue; }
        }
        catch (Exception exception)
        {
            faulted = true;
            RestoreVisibleWindows();
            warn("drawing", exception);
        }
    }

    public void Tick()
    {
        if (disposed || faulted)
            return;
        foreach (var entry in new List<KeyValuePair<UIResurrectionWindow, bool>>(windows))
        {
            var window = entry.Key;
            if (window == null || !window.IsShown)
            {
                windows.Remove(window!);
                continue;
            }
            if (entry.Value == IsEnabled)
                continue;
            try
            {
                // The native redraw restores the original counter when disabled;
                // its postfix applies the correction when enabled. No item Draw
                // call, inventory mutation, or resurrection action is substituted.
                Redraw.Invoke(window, null);
            }
            catch (Exception exception)
            {
                faulted = true;
                windows.Remove(window); // Do not retry a native redraw that just failed.
                RestoreVisibleWindows();
                warn("refreshing", exception);
                break;
            }
        }
    }

    public void Forget(UIResurrectionWindow window) => windows.Remove(window);

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true; // The redraw postfix now leaves native rendering alone.
        RestoreVisibleWindows();
    }

    private void RestoreVisibleWindows()
    {
        foreach (var entry in new List<KeyValuePair<UIResurrectionWindow, bool>>(windows))
        {
            if (!entry.Value || entry.Key == null || !entry.Key.IsShown)
                continue;
            try { Redraw.Invoke(entry.Key, null); }
            catch (Exception exception) { warn("restoring", exception); }
        }
        windows.Clear();
    }

    private static MethodInfo RequireMethod(string name)
    {
        var method = typeof(UIResurrectionWindow).GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
            null, Type.EmptyTypes, null);
        if (method == null || method.ReturnType != typeof(void))
            throw new MissingMethodException(typeof(UIResurrectionWindow).FullName, name);
        return method;
    }
}
