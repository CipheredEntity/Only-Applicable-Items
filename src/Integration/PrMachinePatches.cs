namespace OnlyApplicableItems.Integration;

internal static class PrMachinePatches
{
    public static bool BeforeCountOpen(UIItemCountWindow __instance, UIItemCountWindowData __0)
        => Plugin.Instance?.PrMachine?.BeforeCountOpen(__instance, __0) ?? true;
}
