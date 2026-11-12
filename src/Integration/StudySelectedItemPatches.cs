namespace OnlyApplicableItems.Integration;

internal static class StudySelectedItemPatches
{
    // Harmony supplies only this window's private selected-slot field.
    public static void AfterRedraw(UIResourceBasedCraftWindow __instance, UIItemCell ___mainIngredient)
        => Plugin.Instance?.StudySelectedSlot?.AfterRedraw(__instance, ___mainIngredient);

    public static void AfterHidden(UIResourceBasedCraftWindow __instance)
        => Plugin.Instance?.StudySelectedSlot?.Forget(__instance);
}
