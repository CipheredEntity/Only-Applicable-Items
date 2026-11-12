using System;
using System.Collections.Generic;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.UI;

internal sealed class StudySelectedItemDisplay : IDisposable
{
    private readonly Func<bool> enabled;
    private readonly Action<string, Exception> warn;
    private readonly Dictionary<UIResourceBasedCraftWindow, bool> windows =
        new Dictionary<UIResourceBasedCraftWindow, bool>(ReferenceComparer<UIResourceBasedCraftWindow>.Instance);
    private bool disposed;
    private bool faulted;
    private bool IsEnabled => !disposed && !faulted && enabled();
    public MethodInfo Redraw { get; }
    public MethodInfo Hide { get; }

    public StudySelectedItemDisplay(Func<bool> enabled, Action<string, Exception> warn)
    {
        this.enabled = enabled;
        this.warn = warn;
        var cell = typeof(UIResourceBasedCraftWindow).GetField("mainIngredient",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (cell == null || cell.FieldType != typeof(UIItemCell))
            throw new NotSupportedException("The study selected-item slot representation has changed.");
        Redraw = RequireMethod("Redraw");
        Hide = RequireMethod("Hide");
    }

    public void AfterRedraw(UIResourceBasedCraftWindow window, UIItemCell cell)
    {
        if (disposed || faulted)
            return;
        try
        {
            bool correct = IsEnabled;
            windows[window] = correct;
            if (!correct)
                return;
            if (cell == null)
                throw new InvalidOperationException("The study selected-item slot is missing.");
            var item = cell.DisplayingItem;
            if (item == null || item.IsEmpty)
                return;
            var survey = GameBalance.GetSurveyDefForItemOrNull(item.id);
            if (survey != null && !survey.isScienceFuelCraft)
                StudyTableVisuals.HideResearchQuantity(cell);
            // Decomposition and unknown definitions retain the native draw.
            // No selected reference, cached value, item count, or requirement is changed.
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
        foreach (var entry in new List<KeyValuePair<UIResourceBasedCraftWindow, bool>>(windows))
        {
            if (entry.Key == null || !entry.Key.IsShown)
            {
                windows.Remove(entry.Key!);
                continue;
            }
            if (entry.Value == IsEnabled)
                continue;
            try { entry.Key.Redraw(); }
            catch (Exception exception)
            {
                // Never retry a native draw that just failed.
                windows.Remove(entry.Key);
                faulted = true;
                RestoreVisibleWindows();
                warn("refreshing", exception);
                break;
            }
        }
    }

    public void Forget(UIResourceBasedCraftWindow window) => windows.Remove(window);

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        RestoreVisibleWindows();
    }

    private void RestoreVisibleWindows()
    {
        foreach (var entry in new List<KeyValuePair<UIResourceBasedCraftWindow, bool>>(windows))
        {
            if (!entry.Value || entry.Key == null || !entry.Key.IsShown)
                continue;
            try { entry.Key.Redraw(); }
            catch (Exception exception) { warn("restoring", exception); }
        }
        windows.Clear();
    }

    private static MethodInfo RequireMethod(string name)
    {
        var method = typeof(UIResourceBasedCraftWindow).GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
        if (method == null || method.ReturnType != typeof(void))
            throw new MissingMethodException(typeof(UIResourceBasedCraftWindow).FullName, name);
        return method;
    }
}
