using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using OnlyApplicableItems.Core;

namespace OnlyApplicableItems.Integration;

// These accessors contain only ldfld/stfld. No Item methods, allocations, inventory
// events, reflection writes, or mod callbacks are allowed in the commit phase.
internal sealed class PrItemFields
{
    internal readonly Func<Item, int> Count = Read<Item, int>("count");
    internal readonly Action<Item, int> SetCount = Write<Item, int>("count");
    internal readonly Func<Item, List<Item>> Items = Read<Item, List<Item>>("inventory");
    internal readonly Func<Item, int> Size = Read<Item, int>("inventorySize");
    internal readonly Func<Item, Dictionary<Type, SerializedItemProperty>> Properties = Read<Item, Dictionary<Type, SerializedItemProperty>>("properties");
    internal readonly Action<Item, int> SetFill = Write<Item, int>("inventoryFillSize");
    internal readonly Func<Inventory, Item> Root = Read<Inventory, Item>("inventoryItem");
    private readonly int thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
    internal bool OnThread => thread == System.Threading.Thread.CurrentThread.ManagedThreadId;
    internal PrItemFields()
    {
        // Resolve/JIT both writers before any real consumption can begin.
        // Named Item constructors resolve definitions, even for "empty"; game
        // balance is not loaded during plugin Awake. This probe needs only fields.
        var probe = new Item();
        SetCount(probe, 0); SetFill(probe, 0);
        Count(probe); Items(probe); Root(new Inventory());
    }

    private static FieldInfo Field<T, V>(string name)
    {
        var field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null || field.FieldType != typeof(V))
            throw new NotSupportedException("Unsupported PR inventory field: " + typeof(T).Name + "." + name);
        return field;
    }
    private static Func<T, V> Read<T, V>(string name)
    {
        var field = Field<T, V>(name);
        var method = new DynamicMethod("pr_read_" + name, typeof(V), new[] { typeof(T) }, typeof(PrItemFields).Module, true);
        var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ret);
        return (Func<T, V>)method.CreateDelegate(typeof(Func<T, V>));
    }
    private static Action<T, V> Write<T, V>(string name)
    {
        var field = Field<T, V>(name);
        if (field.IsInitOnly) throw new NotSupportedException("Readonly PR field: " + name);
        var method = new DynamicMethod("pr_write_" + name, typeof(void), new[] { typeof(T), typeof(V) }, typeof(PrItemFields).Module, true);
        var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field); il.Emit(OpCodes.Ret);
        return (Action<T, V>)method.CreateDelegate(typeof(Action<T, V>));
    }
}

internal sealed class PrMachineStock
{
    private sealed class Entry
    {
        internal readonly Item Item;
        internal readonly int Count, Index;
        internal readonly string Id;
        internal Entry(Item item, int count, int index) { Item = item; Count = count; Index = index; Id = item.id; }
    }
    private sealed class Container
    {
        internal readonly Inventory Root;
        internal readonly Item Holder;
        internal readonly List<Item> Items;
        internal readonly Entry[] Entries;
        internal readonly Dictionary<Type, SerializedItemProperty>? Properties;
        internal readonly KeyValuePair<Type, SerializedItemProperty>[] PropertyEntries;
        internal Container(Inventory root, Item holder, PrItemFields fields)
        {
            Root = root; Holder = holder; Items = fields.Items(holder);
            Properties = fields.Properties(holder);
            PropertyEntries = Properties == null ? Array.Empty<KeyValuePair<Type, SerializedItemProperty>>() :
                new List<KeyValuePair<Type, SerializedItemProperty>>(Properties).ToArray();
            Entries = new Entry[Items.Count];
            for (int i = 0; i < Entries.Length; i++)
            {
                var item = Items[i] ?? throw new NotSupportedException("Null PR source item.");
                Entries[i] = new Entry(item, fields.Count(item), i);
            }
            if (holder.HasProperty<TakeOnlySerializedItemProperty>() && holder.HasProperty<AutoExpandSerializedItemProperty>())
                throw new NotSupportedException("PR sources with dynamically resized take-only storage remain vanilla.");
        }
    }
    private readonly PrItemFields fields;
    private readonly MultiInventory roots;
    private readonly Inventory[] rootOrder;
    private readonly Item[] rootItems;
    private readonly List<Container> containers = new List<Container>();
    private readonly Dictionary<string, int> totals = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly HashSet<Item> allowed = new HashSet<Item>(ReferenceComparer<Item>.Instance);

    internal PrMachineStock(PrItemFields fields, MultiInventory roots, Func<Item, bool> eligible, Action<Item> validate)
    {
        if (!fields.OnThread) throw new InvalidOperationException("PR inventory access must stay on the initializing thread.");
        this.fields = fields; this.roots = roots; rootOrder = roots.inventoryList.ToArray();
        rootItems = new Item[rootOrder.Length];
        var seenContainers = new HashSet<Item>(ReferenceComparer<Item>.Instance);
        var seenItems = new HashSet<Item>(ReferenceComparer<Item>.Instance);
        var seenUids = new HashSet<Guid>();
        for (int i = 0; i < rootOrder.Length; i++)
        {
            var root = rootOrder[i] ?? throw new NotSupportedException("Missing PR inventory.");
            var holder = fields.Root(root); rootItems[i] = holder;
            Collect(root, holder);
            // Exactly the picker helper's one level of directly held bags.
            foreach (var item in fields.Items(holder).ToArray())
                if (item.IsBag) Collect(root, item);
        }
        if (!Unchanged()) throw new InvalidOperationException("PR stock changed during validation.");

        void Collect(Inventory root, Item holder)
        {
            if (!seenContainers.Add(holder)) return;
            var container = new Container(root, holder, fields); containers.Add(container);
            foreach (var entry in container.Entries)
            {
                if (!seenItems.Add(entry.Item)) throw new NotSupportedException("Aliased PR inventory item.");
                if (entry.Count <= 0 || !eligible(entry.Item)) continue;
                validate(entry.Item);
                if (entry.Item.Definition.stackCount < 1 || entry.Count > entry.Item.Definition.stackCount ||
                    !Guid.TryParse(entry.Item.UniqueId.ToString(), out var uid) || uid == Guid.Empty || !seenUids.Add(uid))
                    throw new NotSupportedException("Invalid PR source stack or duplicate UID.");
                allowed.Add(entry.Item);
                totals.TryGetValue(entry.Id, out int total);
                totals[entry.Id] = checked(total + entry.Count);
            }
        }
    }

    internal int Total(string id) => totals.TryGetValue(id, out int total) ? total : 0;

    // This final check calls only raw field readers and List indexers; none can
    // run a native predicate or notify observers between validation and mutation.
    private bool Unchanged()
    {
        if (!fields.OnThread) return false;
        if (roots.inventoryList.Count != rootOrder.Length) return false;
        for (int i = 0; i < rootOrder.Length; i++)
            if (!ReferenceEquals(roots.inventoryList[i], rootOrder[i]) || !ReferenceEquals(fields.Root(rootOrder[i]), rootItems[i])) return false;
        foreach (var container in containers)
        {
            if (!ReferenceEquals(fields.Items(container.Holder), container.Items) || container.Items.Count != container.Entries.Length) return false;
            var properties = fields.Properties(container.Holder);
            if (!ReferenceEquals(properties, container.Properties) || (properties?.Count ?? 0) != container.PropertyEntries.Length) return false;
            int propertyIndex = 0;
            if (properties != null)
                foreach (var property in properties)
                {
                    var expected = container.PropertyEntries[propertyIndex++];
                    if (!ReferenceEquals(property.Key, expected.Key) || !ReferenceEquals(property.Value, expected.Value)) return false;
                }
            foreach (var entry in container.Entries)
            {
                if (!ReferenceEquals(container.Items[entry.Index], entry.Item) || fields.Count(entry.Item) != entry.Count || entry.Item.id != entry.Id) return false;
                if (allowed.Contains(entry.Item) && (fields.Size(entry.Item) != 0 || fields.Items(entry.Item).Count != 0 ||
                    (fields.Properties(entry.Item)?.Count ?? 0) != 0)) return false;
            }
        }
        return true;
    }

    internal Consumption Prepare(string id, int count)
    {
        if (count < 1 || Total(id) < count) throw new InvalidOperationException("Insufficient PR supplies.");
        return new Consumption(this, id, count);
    }

    internal sealed class Consumption
    {
        private sealed class Change
        {
            internal readonly Container Container;
            internal readonly List<Entry> Remove = new List<Entry>();
            internal readonly List<KeyValuePair<Entry, int>> Reduce = new List<KeyValuePair<Entry, int>>();
            internal int Fill;
            internal Change(Container container) { Container = container; }
        }
        private readonly PrMachineStock stock;
        private readonly List<Change> changes = new List<Change>();
        private readonly Dictionary<Inventory, List<Item>> notifications = new Dictionary<Inventory, List<Item>>(ReferenceComparer<Inventory>.Instance);
        private bool attempted, notified;
        internal bool Committed { get; private set; }
        internal int Count { get; }
        internal string Id { get; }
        internal Consumption(PrMachineStock stock, string id, int count)
        {
            this.stock = stock; Count = count; Id = id;
            int left = count;
            foreach (var container in stock.containers)
            {
                var change = new Change(container);
                foreach (var entry in container.Entries)
                {
                    if (entry.Count > 0 && entry.Id != "empty" && !string.IsNullOrEmpty(entry.Id)) change.Fill++;
                    if (left == 0 || entry.Id != id || !stock.allowed.Contains(entry.Item)) continue;
                    int take = Math.Min(left, entry.Count); left -= take;
                    if (take == entry.Count) { change.Remove.Add(entry); change.Fill--; }
                    else change.Reduce.Add(new KeyValuePair<Entry, int>(entry, entry.Count - take));
                    if (!notifications.TryGetValue(container.Root, out var items))
                        notifications.Add(container.Root, items = new List<Item>());
                    // Match native UID-removal notifications: the actual source,
                    // including its remaining Count for a partially consumed stack.
                    items.Add(entry.Item);
                }
                if (change.Remove.Count != 0 || change.Reduce.Count != 0) changes.Add(change);
            }
            if (left != 0) throw new InvalidOperationException("Incomplete PR consumption plan.");
        }

        internal bool TryCommit()
        {
            if (attempted) return false;
            attempted = true;
            if (!stock.Unchanged()) return false;
            // All lists/indices, counts, notification payloads and accessors are
            // prepared. These stores/removals allocate nothing and call no game
            // methods or observers. Main-thread execution cannot reenter here.
            foreach (var change in changes)
            {
                foreach (var reduction in change.Reduce) stock.fields.SetCount(reduction.Key.Item, reduction.Value);
                for (int i = change.Remove.Count - 1; i >= 0; i--) change.Container.Items.RemoveAt(change.Remove[i].Index);
                stock.fields.SetFill(change.Container.Holder, change.Fill);
            }
            Committed = true;
            return true;
        }

        internal void Notify(Action<Exception> warn)
        {
            if (!Committed || notified) return;
            notified = true;
            // After native effects, so an observer cannot interrupt consumption
            // halfway or prevent the matching effects by throwing. Never retry.
            foreach (var pair in notifications)
                try { pair.Key.NotifyItemsRemoved(pair.Value); }
                catch (Exception exception) { try { warn(exception); } catch { } }
        }
    }
}
