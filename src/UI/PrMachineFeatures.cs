using System;
using System.Collections.Generic;
using LazyBearTechnology;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.Integration;

namespace OnlyApplicableItems.UI;

internal sealed class PrMachineFeatures : IDisposable
{
    private readonly PrMachineContract contract;
    private readonly PrItemFields fields;
    private readonly Func<bool> enabled;
    private readonly Action<Item> validate;
    private readonly Action<string, Exception> warn;
    private readonly Action<UIPanicReductionMachineWindowData, UIItemCell, Item, PrMachineStock.Consumption> run;
    private readonly Action verify;
    private Request? opening, pending;
    private bool busy, disposed;

    private sealed class Request
    {
        internal readonly UIPanicReductionMachineWindowData Owner;
        internal readonly ApplicableItemsSession Session;
        internal readonly UIItemCell Cell;
        internal readonly Func<Item, bool> Eligible;
        internal readonly MultiInventory Sources;
        internal readonly PlayerData Player;
        internal readonly Inventory PlayerInventory;
        internal readonly Inventory[] Roots;
        internal readonly string Id;
        internal readonly int StackCount, Maximum;
        internal UIItemCountWindow? Dialog;
        internal UIItemCountWindowData? DialogData;
        internal bool Used;
        internal Request(UIPanicReductionMachineWindowData owner, ApplicableItemsSession session, UIItemCell cell,
            Func<Item, bool> eligible, MultiInventory sources, string id, int stackCount, int maximum)
        { Owner = owner; Session = session; Cell = cell; Eligible = eligible; Sources = sources;
            Player = MainGame.PlayerData; PlayerInventory = Player.inventory;
            Roots = sources.inventoryList.ToArray(); Id = id; StackCount = stackCount; Maximum = maximum; }
    }

    internal PrMachineFeatures(PrMachineContract contract, PrItemFields fields, Func<bool> enabled, Action<Item> validate,
        Action<string, Exception> warn, Action verify,
        Action<UIPanicReductionMachineWindowData, UIItemCell, Item, PrMachineStock.Consumption> run)
    { this.contract = contract; this.fields = fields; this.enabled = enabled; this.validate = validate;
        this.warn = warn; this.verify = verify; this.run = run; }

    internal PickerPolicy<Item>? GetPolicy(UIMultiInventoryWindowData data, Action<UIItemCell> callback, Func<Item, bool> predicate)
    {
        if (!contract.Matches(callback, predicate) || !(predicate.Target is UIPanicReductionMachineWindowData owner)) return null;
        verify();
        SameRoots(data.MultiInventory, contract.Source(owner));
        return new PickerPolicy<Item>(PickerId.PrMachineResources, () => !disposed && enabled(), predicate, predicate, validate);
    }
    internal void Bind(ApplicableItemsSession session, Func<Item, bool> predicate)
    {
        var owner = predicate.Target as UIPanicReductionMachineWindowData
            ?? throw new NotSupportedException("Missing native PR picker owner.");
        session.SelectionDispatch = (cell, native) => Open(owner, session, predicate, cell, native);
    }
    private static void SameRoots(MultiInventory a, MultiInventory b)
    {
        var set = new HashSet<Inventory>(a.inventoryList, ReferenceComparer<Inventory>.Instance);
        if (a.inventoryList.Count != set.Count || b.inventoryList.Count != set.Count || !set.SetEquals(b.inventoryList))
            throw new NotSupportedException("PR picker and loader access different inventories.");
    }
    private void Open(UIPanicReductionMachineWindowData owner, ApplicableItemsSession session, Func<Item, bool> eligible, UIItemCell cell, Action native)
    {
        if (disposed || !fields.OnThread || !enabled() || busy || opening != null || pending != null) return;
        busy = true;
        try
        {
            if (!session.IsCurrent || cell.DisplayingItem == null) return;
            verify();
            var sources = contract.Source(owner); SameRoots(session.Sources, sources);
            var stock = new PrMachineStock(fields, sources, eligible, validate);
            string id = cell.DisplayingItem.id;
            int total = stock.Total(id);
            if (total < 1) return;
            contract.ValidateEffect(owner, id, total);
            opening = new Request(owner, session, cell, eligible, sources, id, cell.DisplayingItem.Count, total);
        }
        catch (Exception exception) { Fallback(session, "opening", exception); return; }
        finally { busy = false; }
        try { native(); }
        finally { opening = null; }
    }

    internal bool BeforeCountOpen(UIItemCountWindow window, UIItemCountWindowData data)
    {
        var request = opening; opening = null;
        if (pending != null) { pending.Used = true; pending = null; }
        if (request == null) return true;
        try
        {
            if (disposed || !enabled() || !request.Session.IsCurrent || data.Item.id != request.Id ||
                data.IsForVendor || data.Min != 1 || data.Max != request.StackCount || data.SnapStep != 1 || data.OnConfirm == null)
                throw new NotSupportedException("The native PR quantity dialog changed.");
            data.Max = request.Maximum;
            data.OnConfirm = count => Confirm(request, count);
            request.Dialog = window; request.DialogData = data; pending = request;
            return true;
        }
        catch (Exception exception) { Fallback(request.Session, "binding the quantity dialog", exception); return false; }
    }

    private void Confirm(Request request, int count)
    {
        if (disposed || !fields.OnThread || busy || request.Used || !ReferenceEquals(pending, request) ||
            request.Dialog == null || !contract.IsDialogCurrent(request.Dialog, request.DialogData)) return;
        request.Used = true; pending = null;
        if (!enabled() || !request.Session.IsCurrent || count < 1 || count > request.Maximum ||
            !ReferenceEquals(MainGame.PlayerData, request.Player) || !ReferenceEquals(request.Player.inventory, request.PlayerInventory)) return;
        busy = true;
        PrMachineStock.Consumption plan;
        Item carrier;
        try
        {
            verify();
            if (!contract.IsLive(LazyUI.GetWindow<UIPanicReductionMachineWindow>(), request.Owner) ||
                !ReferenceEquals(contract.Source(request.Owner), request.Sources)) return;
            SameRoots(request.Session.Sources, request.Sources);
            if (request.Sources.inventoryList.Count != request.Roots.Length) return;
            for (int i = 0; i < request.Roots.Length; i++)
                if (!ReferenceEquals(request.Roots[i], request.Sources.inventoryList[i])) return;
            // Re-enumerate current contents of the original native roots. A moved
            // or depleted representative is harmless; the saved full ID is authority,
            // never the contents of a potentially rebound pooled cell.
            var stock = new PrMachineStock(fields, request.Sources, request.Eligible, validate);
            if (stock.Total(request.Id) < count) return;
            contract.ValidateEffect(request.Owner, request.Id, count);
            if (disposed || !enabled() || !request.Session.IsCurrent ||
                !ReferenceEquals(MainGame.PlayerData, request.Player) || !ReferenceEquals(request.Player.inventory, request.PlayerInventory)) return;
            carrier = new Item(request.Id, count);
            plan = stock.Prepare(request.Id, count);
        }
        catch (Exception exception) { Fallback(request.Session, "validating a load", exception); return; }
        finally { busy = false; }
        // No recovery/replay after the game action starts. Notifications belong
        // to the native bridge's finally, including when native effects throw.
        busy = true;
        try { run(request.Owner, request.Cell, carrier, plan); }
        finally { busy = false; }
    }

    private void Fallback(ApplicableItemsSession session, string operation, Exception exception)
    {
        session.RequestFallback("PR Machine " + operation, exception);
        try { warn(operation, exception); } catch { }
    }
    internal void Tick()
    {
        if (pending != null && (disposed || !enabled() || !pending.Session.IsCurrent || pending.Dialog == null ||
            !contract.IsDialogCurrent(pending.Dialog, pending.DialogData)))
        { pending.Used = true; pending = null; }
    }
    public void Dispose()
    {
        disposed = true;
        if (pending != null) pending.Used = true;
        pending = null;
    }
}
