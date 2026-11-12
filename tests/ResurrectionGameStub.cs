using System;
using OnlyApplicableItems.Integration;

// Test-only delegate boundaries, not a simulation of resurrection or skull math.
// Both lambdas capture only this, matching the decompiled native opening method.
public sealed class UIResurrectionWindowData
{
    public Item? Collar { get; private set; }
    public Func<Item, bool> Rule = _ => true;
    public Action<UIItemCell> Callback => cell => { Collar = cell.DisplayingItem; };
    public Func<Item, bool> Predicate => item => item != null && !item.IsEmpty &&
        item.Definition.type == ItemType.Collar && Rule(item);
    public void OnCollarPressed(UIItemCell cell) { }
}

namespace OnlyApplicableItems
{
    // Exercise the production prefix/finalizer without loading Unity or Harmony.
    internal sealed class Plugin
    {
        public static Plugin? Instance { get; set; }
        public ResurrectionCollarPickerContract? ResurrectionCollars { get; set; }
        public UI.ResurrectionCollarDisplay? ResurrectionCollarSlot { get; set; }
        public UI.StudySelectedItemDisplay? StudySelectedSlot { get; set; }
        public System.Collections.Generic.HashSet<UI.ApplicableItemsSession> Sessions { get; } = new();
        public System.Collections.Generic.List<string> Warnings { get; } = new();
        public void Register(UI.ApplicableItemsSession session) => Sessions.Add(session);
        public void Unregister(UI.ApplicableItemsSession session) => Sessions.Remove(session);
        public void WarnOnce(string key, string message) { if (!Warnings.Contains(key)) Warnings.Add(key); }
    }
}
