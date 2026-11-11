namespace OnlyApplicableItems.Integration;

internal static class ResurrectionCollarDisplayPatches
{
    public static void AfterRedraw(UIResurrectionWindow __instance)
        => Plugin.Instance?.ResurrectionCollarSlot?.AfterRedraw(__instance);

    public static void AfterHidden(UIResurrectionWindow __instance)
        => Plugin.Instance?.ResurrectionCollarSlot?.Forget(__instance);
}
