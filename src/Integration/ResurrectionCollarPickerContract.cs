using System;
using System.Reflection;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

internal sealed class ResurrectionCollarPickerContract
{
    [ThreadStatic] private static OpeningScope? opening;
    private readonly Func<bool> enabled;
    private readonly Action<Item> validateItem;
    public MethodInfo OpenPicker { get; }

    public ResurrectionCollarPickerContract(Func<bool> enabled, Action<Item> validateItem)
    {
        this.enabled = enabled;
        this.validateItem = validateItem;
        OpenPicker = typeof(UIResurrectionWindowData).GetMethod("OnCollarPressed",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
            null, new[] { typeof(UIItemCell) }, null)
            ?? throw new MissingMethodException(typeof(UIResurrectionWindowData).FullName, "OnCollarPressed");
        if (OpenPicker.ReturnType != typeof(void))
            throw new NotSupportedException("The resurrection collar-opening signature has changed.");
    }

    // Both native delegates are inline lambdas bound to the resurrection data.
    // Scope the constructor hook to the named opening method, without relying on
    // compiler-generated method names or replacing either native delegate.
    public IDisposable BeginOpening(UIResurrectionWindowData owner)
        => new OpeningScope(this, owner);

    public PickerPolicy<Item>? GetPolicy(Action<UIItemCell>? callback, Func<Item, bool>? predicate)
    {
        var scope = opening;
        if (scope == null || !ReferenceEquals(scope.Contract, this) ||
            callback == null || predicate == null ||
            callback.GetInvocationList().Length != 1 || predicate.GetInvocationList().Length != 1 ||
            !ReferenceEquals(callback.Target, scope.Owner) || !ReferenceEquals(predicate.Target, scope.Owner) ||
            callback.Method.DeclaringType != typeof(UIResurrectionWindowData) ||
            predicate.Method.DeclaringType != typeof(UIResurrectionWindowData))
            return null;

        return new PickerPolicy<Item>(PickerId.ResurrectionCollars, enabled,
            item => item != null && !item.IsEmpty && item.Definition.type == ItemType.Collar,
            predicate, validateItem);
    }

    private sealed class OpeningScope : IDisposable
    {
        public ResurrectionCollarPickerContract Contract { get; }
        public UIResurrectionWindowData Owner { get; }
        private readonly OpeningScope? previous;
        private bool disposed;

        public OpeningScope(ResurrectionCollarPickerContract contract, UIResurrectionWindowData owner)
        {
            Contract = contract;
            Owner = owner;
            previous = opening;
            opening = this;
        }

        public void Dispose()
        {
            disposed = true;
            // Normal finalizers unwind in reverse order. Also skip an already
            // disposed outer scope if another patch causes unusual re-entry.
            while (opening != null && opening.disposed)
                opening = opening.previous;
        }
    }
}
