using System;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal static class AlchemyPatches
{
    public static void BeforeCountUpdate(UIAlchemyWindow __instance, out QuantityDisplayScope<UIItemCell>? __state)
    {
        __state = Plugin.Instance?.Alchemy?.BeginCountUpdate(__instance);
    }

    // A Harmony finalizer runs after success and exceptions. Return the original
    // exception unchanged: restoring UI caches must never suppress a game failure.
    public static Exception? AfterCountUpdate(QuantityDisplayScope<UIItemCell>? __state, Exception? __exception)
    {
        try { __state?.Dispose(); }
        catch (Exception exception) { Plugin.Instance?.Alchemy?.Disable("restoring cached quantities", exception); }
        return __exception;
    }

    public static void AfterLaboratoryHidden(UIAlchemyWindow __instance)
    {
        Plugin.Instance?.Alchemy?.Forget(__instance);
    }
}
