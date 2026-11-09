using System;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Integration;

internal static class PickerPatches
{
    // Positional arguments avoid depending on parameter names surviving game updates.
    public static void AfterPickerConstructed(UIMultiInventoryWindowData __instance,
        Action<UIItemCell> __1, Func<Item, bool> __2)
    {
        Plugin.Instance?.TryConsolidate(__instance, __1, __2);
    }

    public static void BeforeInventoryRedrawNotification(InventoryWidget __instance)
    {
        if (__instance.Data is ApplicableInventoryWidgetData view)
        {
            try
            {
                view.Session.Decorate(__instance, view);
            }
            catch (Exception exception)
            {
                view.Session.RequestFallback("drawing", exception);
            }
        }
    }
}
