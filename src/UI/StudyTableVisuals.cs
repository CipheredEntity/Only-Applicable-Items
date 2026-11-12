using System;
using System.Reflection;
using TMPro;

namespace OnlyApplicableItems.UI;

internal static class StudyTableVisuals
{
    // Resolve only when Researchable actually draws; a changed counter contract
    // must not prevent unrelated pickers from initializing or rendering.
    private static readonly Lazy<FieldInfo> countLabel = new Lazy<FieldInfo>(() =>
    {
        var field = typeof(UIItemCell).GetField("countLabel",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (field == null || field.FieldType != typeof(TextMeshProUGUI))
            throw new NotSupportedException("The study item quantity label representation has changed.");
        return field;
    });

    public static void HideResearchQuantity(UIItemCell cell)
    {
        var label = (TextMeshProUGUI?)countLabel.Value.GetValue(cell);
        if (label == null)
            throw new InvalidOperationException("The study item quantity label is missing.");
        // Do not change Count, Value, drawCounter, or any item/callback. Native
        // Draw/DrawCount explicitly resets visibility when the cell is reused.
        label.gameObject.SetActive(false);
    }
}
