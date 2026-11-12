using System;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;

namespace OnlyApplicableItems.UI;

internal sealed class ShopSellingFeatures : IDisposable
{
    internal ShopSellingContract Contract { get; }
    private readonly Func<bool> enabled;
    private readonly Action<Item> validate;
    private readonly Action<string, Exception> warn;
    private readonly Func<PickerId, string> getTitle;
    private readonly Action? refreshSettings;
    private ShopSellingSession? session;
    private ShopSaleRequest? opening;
    private ShopSaleRequest? pending;
    private UIItemCountWindow? pendingWindow;
    private ShopSaleRequest? transfer;
    private int transferCount;
    private bool moving;
    private bool disposed;
    internal bool Enabled => !disposed && enabled();
    internal bool Busy => opening != null || pending != null || transfer != null;

    public ShopSellingFeatures(ShopSellingContract contract, Func<bool> enabled, Action<Item> validate, Action<string, Exception> warn,
        Func<PickerId, string>? getTitle = null, Action? refreshSettings = null)
    {
        Contract = contract; this.enabled = enabled; this.validate = validate; this.warn = warn;
        this.getTitle = getTitle ?? PickerTitles.GetDefault; this.refreshSettings = refreshSettings;
    }

    internal string GetTitle(PickerId id) => getTitle(id);

    internal void Attach(Trading trading, UIVendorWindowData data)
    {
        refreshSettings?.Invoke();
        if (!Enabled) return;
        ShopSellingSession? candidate = null;
        try
        {
            if (session != null && !session.Detached && session.Window != null && session.Window.IsShown)
                throw new NotSupportedException("A merchant session was replaced without closing.");
            session?.Detach();
            candidate = new ShopSellingSession(this, trading, data, validate);
            candidate.Commit(); session = candidate;
        }
        catch (Exception exception) { candidate?.Detach(); Warn("opening the selling panel", exception); }
    }

    internal void BeforeRedraw(UIVendorWindow window)
    { if (ReferenceEquals(session?.Window, window)) session!.BeforeRedraw(); }
    internal void Hidden(UIVendorWindow window)
    { if (ReferenceEquals(session?.Window, window)) session!.Detach(); }

    internal void OpenQuantity(ShopSaleRequest request, Action native)
    {
        if (Busy) return;
        try
        {
            // Prime/verify the cache before the native opener computes its Max.
            if (request.Session.AvailableCapacity(request) < 1) { request.Valid = false; return; }
        }
        catch (Exception exception) { request.Session.RequestFallback("checking quantity dialog capacity", exception); return; }
        opening = request;
        try { native(); }
        catch (Exception exception) { request.Session.RequestFallback("opening a native quantity dialog", exception); throw; }
        finally { opening = null; RestoreAfterDisposal(); }
    }

    internal bool BeforeCountOpen(UIItemCountWindow window, UIItemCountWindowData data)
    {
        var request = opening;
        opening = null; // Consume before native window events can open another dialog.
        if (pending != null) { pending.Valid = false; pending.Session.MarkDirty(); pending = null; pendingWindow = null; }
        if (request == null) return true;
        try
        {
            if (!Enabled || data.Item.id != request.Id || data.Min != 1 || data.Max < 1 || data.SnapStep != 1 ||
                !data.IsForVendor || data.OnConfirm == null || request.Item.Definition.stackCount == 1)
                throw new NotSupportedException("The native selling quantity dialog changed.");
            int capacity = request.Session.AvailableCapacity(request);
            if (capacity < 1) { request.Valid = false; request.Session.MarkDirty(); return false; }
            data.Max = Math.Min(data.Max, capacity);
            var nativeConfirm = data.OnConfirm;
            int maximum = data.Max;
            data.OnConfirm = count =>
            {
                if (count >= 1 && count <= maximum) Transfer(request, count, () => nativeConfirm(count));
                else request.Valid = false;
            };
            pending = request; pendingWindow = window;
            return true;
        }
        catch (Exception exception) { request.Session.RequestFallback("binding a quantity dialog", exception); return false; }
    }

    internal void Transfer(ShopSaleRequest request, int count, Action native)
    {
        if (transfer != null || opening != null || (pending != null && !ReferenceEquals(pending, request))) return;
        if (ReferenceEquals(pending, request) && (pendingWindow == null || !pendingWindow.IsShown)) return;
        try
        {
            if (!request.Session.ValidateTransfer(request, count)) { request.Valid = false; request.Session.MarkDirty(); return; }
        }
        catch (Exception exception) { request.Session.RequestFallback("validating a sale", exception); return; }
        transfer = request; transferCount = count;
        try { native(); }
        catch (Exception exception) { request.Session.RequestFallback("executing a native sale", exception); throw; }
        finally { request.Valid = false; request.Session.MarkDirty(); transfer = null; moving = false; RestoreAfterDisposal(); }
    }

    // Only transfers originating in our virtual panel are guarded. Buy, return,
    // accept and cancel paths retain their native implementation and callbacks.
    internal bool BeforeMove(Trading trading, UIItemCell cell, int count, Inventory from, Inventory to, out ShopSaleRequest? state)
    {
        state = null;
        var request = transfer;
        if (request == null)
            return session == null || session.Detached || !session.OwnsCell(cell);
        try
        {
            if (moving || request.Used || !ReferenceEquals(trading, request.Session.Trading) ||
                !ReferenceEquals(cell, request.Cell) || !ReferenceEquals(from, request.Session.Source) ||
                !ReferenceEquals(to, request.Session.Destination) || count != transferCount ||
                !request.Session.ValidateTransfer(request, count)) return false;
            request.Used = true; moving = true; state = request;
            return true;
        }
        catch (Exception exception) { request.Session.RequestFallback("checking a native transfer", exception); return false; }
    }
    internal void AfterMove(ShopSaleRequest? state)
    { if (state != null) { moving = false; state.Session.MarkDirty(); } }
    internal void Invalidate(ShopSellingSession owner)
    {
        if (ReferenceEquals(pending?.Session, owner)) pending!.Valid = false;
        if (ReferenceEquals(opening?.Session, owner)) opening!.Valid = false;
        if (ReferenceEquals(transfer?.Session, owner)) transfer!.Valid = false;
    }
    internal void Warn(string operation, Exception exception)
    { try { warn(operation, exception); } catch { /* Diagnostics must not interrupt trading. */ } }
    public void Tick()
    {
        if (pending != null && (pendingWindow == null || !pendingWindow.IsShown))
        {
            pending.Valid = false; pending.Session.MarkDirty(); pending = null; pendingWindow = null;
        }
        try { session?.Tick(); }
        catch (Exception exception) { session?.RequestFallback("restoring the selling panel", exception); }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (pending != null)
        {
            pending.Valid = false;
            // Wrapped confirmations remain inert after unload, even if cells get reused.
            pending = null; pendingWindow = null;
        }
        RestoreAfterDisposal();
    }
    private void RestoreAfterDisposal()
    {
        if (!disposed || Busy) return;
        try { session?.Restore(); }
        catch (Exception exception) { Warn("restoring after shutdown", exception); }
    }
}
