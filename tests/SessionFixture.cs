using System;
using System.Collections.Generic;
using OnlyApplicableItems.Core;
using OnlyApplicableItems.UI;

namespace OnlyApplicableItems.Checks;

internal static partial class Program
{
    private sealed class SessionFixture : IDisposable
    {
        public Plugin Plugin { get; } = new();
        public UIMultiInventoryWindow Window { get; } = new();
        public UIMultiInventoryWindowData Data { get; }
        public ApplicableItemsSession Session { get; }
        public InventoryWidgetDataBase[] Originals { get; }
        public MultiInventoryWidgetData Container => Data.MultiInventoryWidgetData;

        public SessionFixture(IReadOnlyList<PickerPolicy<Item>> policies, MultiInventory sources,
            Func<Item, bool> eligible, Action<UIItemCell>? callback = null)
        {
            UnityEngine.Time.unscaledTime = 0;
            Data = new UIMultiInventoryWindowData(sources, callback ?? (_ => { }), eligible);
            Originals = Container.inventoriesData.ToArray();
            Session = new ApplicableItemsSession(Plugin, policies, Data, eligible);
        }
        public void Open() { Session.Commit(); Window.Widget.Draw(Container); }
        public ApplicableInventoryWidgetData View(int index) => (ApplicableInventoryWidgetData)Container.inventoriesData[index];
        public void Dispose()
        {
            if (Window.Widget.DrawnInventories.Count > 0) Window.Close();
            Session.Dispose();
        }
    }

    private static MultiInventory SessionSources(params Item[] items)
    {
        var inventory = new Inventory("player", 12);
        inventory.Data.Inventory.AddRange(items);
        var result = new MultiInventory();
        result.inventoryList.Add(inventory);
        return result;
    }
}
