using System;
using System.Collections.Generic;
using System.Reflection;
using LazyBearTechnology;

namespace OnlyApplicableItems.Integration;

internal sealed class ShopSellingContract
{
    public MethodInfo Fill { get; } = Method(typeof(Trading), "FillVendorWindowData", typeof(void), typeof(UIVendorWindowData), typeof(string), typeof(Action));
    public MethodInfo Move { get; } = Method(typeof(Trading), "TryMoveItem", typeof(bool), typeof(UIItemCell), typeof(int), typeof(Inventory), typeof(Inventory));
    public MethodInfo Draw { get; } = Method(typeof(InventoryWidget), "OnRedraw", typeof(void));
    public MethodInfo Redraw { get; } = Method(typeof(UIVendorWindow), "RedrawLite", typeof(void));
    public MethodInfo Hide { get; } = Method(typeof(UIVendorWindow), "HideWindow", typeof(void));
    public MethodInfo CountOpen { get; } = Method(typeof(UIItemCountWindow), "Open", typeof(void), typeof(UIItemCountWindowData));
    internal MethodInfo Predicate { get; } = Method(typeof(Trading), "PlayerItemsAvailableCondition", typeof(bool), typeof(Item));
    internal MethodInfo Primary { get; } = Method(typeof(Trading), "OnPlayerItemPress1", typeof(void), typeof(UIItemCell));
    internal MethodInfo Secondary { get; } = Method(typeof(Trading), "OnPlayerItemPress2", typeof(void), typeof(UIItemCell));
    private readonly MethodInfo openCount = Method(typeof(Trading), "OpenItemCountWindow", typeof(void), typeof(UIItemCell), typeof(Inventory), typeof(Inventory), typeof(UIItemCountWindowData.PriceCalculateDelegate));
    private readonly MethodInfo unitPrice = Method(typeof(Trading), "GetSingleItemCostInPlayerInventory", typeof(int), typeof(string), typeof(int));
    private readonly FieldInfo cached = Field(typeof(Trading), "cachedWindowData", typeof(UIVendorWindowData));
    private readonly FieldInfo sell = Field(typeof(Trading), "sellInventory", typeof(Inventory));
    private readonly FieldInfo windowData = Field(typeof(LazyWidget<UIVendorWindowData>), "data", typeof(UIVendorWindowData));
    private readonly FieldInfo playerWidget = Field(typeof(UIVendorWindow), "playerInventoryWidget", typeof(MultiInventoryWidget));

    public bool IsCurrent(Trading trading, UIVendorWindowData data, Inventory destination) =>
        ReferenceEquals(cached.GetValue(trading), data) && ReferenceEquals(sell.GetValue(trading), destination);
    public Inventory SellInventory(Trading trading) => (Inventory)sell.GetValue(trading)!;
    public UIVendorWindowData? WindowData(UIVendorWindow window) => (UIVendorWindowData?)windowData.GetValue(window);
    public MultiInventoryWidget PlayerWidget(UIVendorWindow window) => (MultiInventoryWidget)playerWidget.GetValue(window)!;
    public Action<UIItemCell, Inventory, Inventory, UIItemCountWindowData.PriceCalculateDelegate> OpenCount(Trading trading) =>
        (Action<UIItemCell, Inventory, Inventory, UIItemCountWindowData.PriceCalculateDelegate>)openCount.CreateDelegate(
            typeof(Action<UIItemCell, Inventory, Inventory, UIItemCountWindowData.PriceCalculateDelegate>), trading);
    public Func<string, int, int> UnitPrice(Trading trading) => (Func<string, int, int>)unitPrice.CreateDelegate(typeof(Func<string, int, int>), trading);

    internal static bool Matches(Delegate? callback, MethodInfo method, Trading owner) => callback != null &&
        callback.GetInvocationList().Length == 1 && callback.Method == method && ReferenceEquals(callback.Target, owner);
    private static MethodInfo Method(Type type, string name, Type result, params Type[] parameters)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null);
        if (method == null || method.DeclaringType != type || method.ReturnType != result || method.IsGenericMethod)
            throw new MissingMethodException(type.FullName, name);
        return method;
    }
    private static FieldInfo Field(Type type, string name, Type expected)
    {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null || field.FieldType != expected) throw new MissingFieldException(type.FullName, name);
        return field;
    }
}
