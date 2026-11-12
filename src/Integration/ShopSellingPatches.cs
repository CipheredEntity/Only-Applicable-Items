using System;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Integration;

internal static class ShopSellingPatches
{
    public static void AfterFilled(Trading __instance, UIVendorWindowData __0) => Plugin.Instance?.ShopSelling?.Attach(__instance, __0);
    public static void BeforeDraw(InventoryWidget __instance)
    {
        if (__instance.Data is ShopSellingInventoryData view)
            try { view.Session.Decorate(__instance, view); }
            catch (Exception exception) { view.Session.RequestFallback("drawing selling quantities", exception); }
    }
    public static void BeforeRedraw(UIVendorWindow __instance) => Plugin.Instance?.ShopSelling?.BeforeRedraw(__instance);
    public static void AfterHidden(UIVendorWindow __instance) => Plugin.Instance?.ShopSelling?.Hidden(__instance);
    public static bool BeforeCountOpen(UIItemCountWindow __instance, UIItemCountWindowData __0)
        => Plugin.Instance?.ShopSelling?.BeforeCountOpen(__instance, __0) ?? true;
    public static bool BeforeMove(Trading __instance, UIItemCell __0, int __1, Inventory __2, Inventory __3,
        ref bool __result, out ShopSaleRequest? __state)
    {
        __state = null;
        var feature = Plugin.Instance?.ShopSelling;
        if (feature == null || feature.BeforeMove(__instance, __0, __1, __2, __3, out __state)) return true;
        __result = false;
        return false;
    }
    public static Exception? AfterMove(ShopSaleRequest? __state, Exception? __exception)
    {
        // The request carries its owner; no gameplay retries or exception suppression.
        __state?.Feature.AfterMove(__state);
        return __exception;
    }
}
