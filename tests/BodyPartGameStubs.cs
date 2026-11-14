using System;

// Test-only stand-ins for the signatures inspected by the production autopsy contract.
// These are compiled only into the checks executable. No Unity/game assembly is run,
// and the configurable predicates do not claim to simulate actual collar calculations.
public enum ItemType { None, Brain, Heart, Guts, Skull, Bones, Skin, Flesh, Embalm, Collar, BodyArmor, Sword, Preach }

public sealed partial class ItemDef
{
    public ItemType type;
    public bool isOrganMistake;
    public int stackCount = 1;
    public bool hasDurability;
    public bool isTool;
    public bool isWeapon;
    public bool isBag;
    public int sortOrder;
    public System.Collections.Generic.List<string> itemGroupIds = new System.Collections.Generic.List<string>();
}

public sealed partial class Item
{
    public readonly string id;
    public readonly ItemDef Definition;
    public Guid UniqueId { get; } = Guid.NewGuid();
    private int count;
    public int Count { get => count; set => count = value; }
    public bool HasInstanceData;
    public int OccupiedPocketCount;
    public bool IsEmpty => Count <= 0;
    public bool IsBag => Definition.isBag;
    private int inventorySize;
    public int InventorySize { get => inventorySize; set => inventorySize = value; }
    private System.Collections.Generic.List<Item> inventory = new System.Collections.Generic.List<Item>();
    public System.Collections.Generic.List<Item> Inventory => inventory;

    public Item(string id, ItemType category, int count = 1)
    {
        this.id = id;
        Count = count;
        Definition = new ItemDef { type = category };
        Definition.id = id;
        TestItemDefinitions.Values[id] = Definition;
    }
}

public sealed class UIItemCell
{
    private readonly TMPro.TextMeshProUGUI countLabel = new();
    public Item? DisplayingItem;
    public int Value { get; set; }
    public Action<UIItemCell>? OnItemCellPress;
    public string? ExtraRedTooltipLocId;
    public int RenderedCount { get; private set; }
    public bool CounterVisible => countLabel.gameObject.activeSelf;
    public bool ThrowOnNextCountUpdate;
    public int CountUpdates { get; private set; }

    public void Draw(Item? item)
    {
        DisplayingItem = item;
        Value = item?.Count ?? 0;
        UpdateCountLabel();
    }

    public void UpdateCountLabelAsRegularItem() => UpdateCountLabel(forceDraw: true);

    // Minimal stand-in for the verified normal-collar counter branch, not Unity.
    public void UpdateCountLabel(int multiplier = 1, bool forceDraw = false)
    {
        CountUpdates++;
        RenderedCount = Value * multiplier;
        countLabel.gameObject.SetActive(RenderedCount > 1 || forceDraw);
        if (ThrowOnNextCountUpdate)
        {
            ThrowOnNextCountUpdate = false;
            throw new InvalidOperationException("Count-label renderer failed");
        }
    }
}

public sealed class UIAutopsyWindowData
{
    private ItemType selectedItemType;
    public bool PermitInsertion = true;
    public bool PermitReplacement = true;
    public Item? Selected { get; private set; }
    public Action<UIItemCell> InsertSelected => TryInsertMainOrgan;
    public Action<UIItemCell> ChangeSelected => TryChangeMainOrgan;
    public Func<Item, bool> InsertEligible => CanInsertOrgan;
    public Func<Item, bool> ChangeEligible => CanChangeOrgan;

    public UIAutopsyWindowData(ItemType category) { selectedItemType = category; }
    private void TryInsertMainOrgan(UIItemCell cell) { Selected = cell.DisplayingItem; }
    private void TryChangeMainOrgan(UIItemCell cell) { Selected = cell.DisplayingItem; }
    private bool CanInsertOrgan(Item item) => MatchesCategory(item) && PermitInsertion;
    private bool CanChangeOrgan(Item item) => MatchesCategory(item) && PermitReplacement;
    private bool MatchesCategory(Item item) => item != null && !item.IsEmpty &&
        item.Definition.type == selectedItemType && !item.Definition.isOrganMistake;
}
