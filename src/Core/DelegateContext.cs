using System;
using System.Reflection;

namespace OnlyApplicableItems.Core;

internal static class DelegateContext
{
    public static bool Matches(Delegate? callback, Delegate? predicate, MethodInfo expectedCallback, MethodInfo expectedPredicate)
    {
        return callback != null && predicate != null
            && callback.GetInvocationList().Length == 1 && predicate.GetInvocationList().Length == 1
            && callback.Method == expectedCallback && predicate.Method == expectedPredicate
            && callback.Target != null && ReferenceEquals(callback.Target, predicate.Target);
    }
}
