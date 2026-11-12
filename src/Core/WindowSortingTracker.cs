using System;
using System.Collections.Generic;

namespace OnlyApplicableItems.Core;

// Records allocation history, never canvas values. All calls are on the UI thread.
internal sealed class WindowSortingTracker<T> where T : class
{
    private readonly Func<IReadOnlyList<T>> readStack;
    private readonly Func<int> readCounter;
    private readonly Action<int> writeCounter;
    private readonly Func<int> readInitial;
    private readonly Func<bool> isEnabled;
    private readonly Action<string, Exception> warn;
    private readonly Dictionary<T, int> assignments = new Dictionary<T, int>(ReferenceComparer<T>.Instance);
    private T[] knownStack = Array.Empty<T>();
    private bool enabled;
    private bool tracking;
    private bool disposed;
    private int depth;
    private int expectedCounter;
    private int initial;

    internal sealed class Operation
    {
        internal readonly T Window;
        internal readonly bool Adding;
        internal readonly T[] Before;
        internal readonly int Counter;
        internal readonly int Initial;
        internal readonly bool Participating;
        internal bool Completed;
        internal Operation(T window, bool adding, T[] before, int counter, int initial, bool participating)
        { Window = window; Adding = adding; Before = before; Counter = counter; Initial = initial; Participating = participating; }
    }

    public WindowSortingTracker(Func<IReadOnlyList<T>> readStack, Func<int> readCounter,
        Action<int> writeCounter, Func<int> readInitial, Func<bool> isEnabled, Action<string, Exception> warn)
    {
        this.readStack = readStack;
        this.readCounter = readCounter;
        this.writeCounter = writeCounter;
        this.readInitial = readInitial;
        this.isEnabled = isEnabled;
        this.warn = warn;
        enabled = isEnabled();
    }

    // Polling observes live toggles even when no window opens or closes that frame.
    public void ObserveConfiguration()
    {
        if (disposed) return;
        try
        {
            bool next = isEnabled();
            if (next != enabled)
            {
                enabled = next;
                Suspend(); // Enabling an existing stack must not invent its allocation history.
            }
        }
        catch (Exception exception) { enabled = false; Fail("reading configuration", exception); }
    }

    public Operation? Begin(T window, bool adding)
    {
        if (disposed) return null;
        ObserveConfiguration();
        depth++;
        try
        {
            if (depth != 1)
            {
                Fail("reentrant stack operation", new InvalidOperationException("Waiting for a fresh root window."));
                return new Operation(window, adding, Array.Empty<T>(), 0, 0, false);
            }
            T[] before = Snapshot();
            int counter = readCounter();
            int root = readInitial();
            if (!enabled) Suspend();
            else if (before.Length == 0)
            {
                assignments.Clear();
                knownStack = before;
                initial = root;
                expectedCounter = counter;
                tracking = true;
            }
            if (tracking && (!Same(before, knownStack) || counter != expectedCounter || root != initial))
                Fail("unexpected stack or counter change", new InvalidOperationException("Allocation history is no longer reliable."));

            if (tracking && before.Length > 0)
            {
                counter = Maximum(before);
                if (adding && counter > int.MaxValue - 15)
                    throw new OverflowException("The next native allocation would overflow.");
                SetCounter(counter);
            }
            return new Operation(window, adding, before, counter, root, tracking);
        }
        catch (Exception exception)
        {
            Fail("preparing a stack operation", exception);
            // The finalizer must balance depth even when preparing the operation fails.
            return new Operation(window, adding, Array.Empty<T>(), 0, 0, false);
        }
    }

    public void End(Operation? operation, Exception? nativeException)
    {
        if (operation == null || operation.Completed) return;
        operation.Completed = true;
        try
        {
            ObserveConfiguration();
            if (nativeException != null)
            {
                Fail("native stack exception", nativeException);
                return; // Never roll back, retry, or suppress native behavior.
            }
            if (disposed || !tracking || !operation.Participating || depth != 1) return;
            T[] after = Snapshot();
            if (readInitial() != operation.Initial)
                throw new InvalidOperationException("Initial sorting order changed during an operation.");
            if (operation.Adding)
            {
                if (Contains(operation.Before, operation.Window) || after.Length != operation.Before.Length + 1 ||
                    !ReferenceEquals(after[after.Length - 1], operation.Window))
                    throw new InvalidOperationException("Unexpected add operation.");
                for (int i = 0; i < operation.Before.Length; i++)
                    if (!ReferenceEquals(after[i], operation.Before[i]))
                        throw new InvalidOperationException("Surviving windows changed during addition.");
                int assigned = operation.Before.Length == 0 ? operation.Initial : checked(operation.Counter + 15);
                if (readCounter() != assigned)
                    throw new InvalidOperationException("Native allocation differs from the validated 15-point contract.");
                assignments.Add(operation.Window, assigned);
                expectedCounter = assigned;
            }
            else
            {
                var expected = new List<T>();
                foreach (T entry in operation.Before)
                    if (!ReferenceEquals(entry, operation.Window)) expected.Add(entry);
                if (!Same(after, expected) || readCounter() != operation.Counter)
                    throw new InvalidOperationException("Unexpected removal or counter change.");
                assignments.Remove(operation.Window);
                if (after.Length > 0) SetCounter(Maximum(after));
                else Suspend(); // Native AddToStack resets the next root; do not write an idle counter.
            }
            knownStack = after;
        }
        catch (Exception exception) { Fail("completing a stack operation", exception); }
        finally { depth--; }
    }

    public void Dispose() { disposed = true; Suspend(); }

    private void SetCounter(int value)
    {
        if (readCounter() != value) writeCounter(value);
        if (readCounter() != value) throw new InvalidOperationException("Counter assignment was not retained.");
        expectedCounter = value;
    }

    private int Maximum(IReadOnlyList<T> windows)
    {
        int maximum = int.MinValue;
        foreach (T window in windows)
        {
            if (!assignments.TryGetValue(window, out int value))
                throw new InvalidOperationException("An open window has no recorded allocation.");
            if (value > maximum) maximum = value;
        }
        return maximum;
    }

    private T[] Snapshot()
    {
        IReadOnlyList<T> stack = readStack();
        var copy = new T[stack.Count];
        var unique = new HashSet<T>(ReferenceComparer<T>.Instance);
        for (int i = 0; i < copy.Length; i++)
        {
            T window = stack[i];
            if (ReferenceEquals(window, null) || !unique.Add(window))
                throw new InvalidOperationException("Null or duplicate window in the native stack.");
            copy[i] = window;
        }
        return copy;
    }

    private static bool Contains(IReadOnlyList<T> list, T window)
    { foreach (T entry in list) if (ReferenceEquals(entry, window)) return true; return false; }

    private static bool Same(IReadOnlyList<T> a, IReadOnlyList<T> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (!ReferenceEquals(a[i], b[i])) return false;
        return true;
    }

    private void Suspend() { tracking = false; assignments.Clear(); knownStack = Array.Empty<T>(); }
    private void Fail(string operation, Exception exception)
    {
        Suspend();
        try { warn(operation, exception); } catch { /* Diagnostics must never break native UI. */ }
    }
}
