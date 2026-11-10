using System;

// Test-only stand-ins for the signatures inspected by the production autopsy contract.
// These are compiled only into the checks executable. No Unity/game assembly is run,
// and the configurable predicates do not claim to simulate actual collar calculations.
public enum ItemType { None, Brain, Heart, Guts, Skull, Bones, Skin, Flesh, Embalm, Collar }

public sealed class ItemDef
{
    public ItemType type;
    public bool isOrganMistake;
    public System.Collections.Generic.List<string> itemGroupIds = new System.Collections.Generic.List<string>();
}

public sealed class Item
{
    public readonly string id;
    public readonly ItemDef Definition;
    public int Count;
    public bool HasInstanceData;
    public bool IsEmpty => Count <= 0;

    public Item(string id, ItemType category, int count = 1)
    {
        this.id = id;
        Count = count;
        Definition = new ItemDef { type = category };
    }
}

public sealed class UIItemCell { public Item? DisplayingItem; }

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
