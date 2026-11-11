using System;

namespace OnlyApplicableItems.Integration;

internal static class ResurrectionCollarPatches
{
    public static void BeforeOpening(UIResurrectionWindowData __instance, out IDisposable? __state)
    {
        __state = Plugin.Instance?.ResurrectionCollars?.BeginOpening(__instance);
    }

    // Always unwind the opening context, including when the game throws. Never
    // suppress or retry the original action, and never intercept consumption.
    public static Exception? AfterOpening(IDisposable? __state, Exception? __exception)
    {
        __state?.Dispose();
        return __exception;
    }
}
