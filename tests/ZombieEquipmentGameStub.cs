using System;

// Test-only stand-ins for the production collar contract's two-owner boundary.
// UpgradeRule supplies changing native results; skull/collar arithmetic is not simulated.
public sealed class ZombieWgoData
{
    public Func<Item, bool> UpgradeRule = _ => true;
    public Func<Item, bool> UpgradePredicate => CanUpgradeCollarTo;
    public bool CanUpgradeCollarTo(Item item) => item != null && !item.IsEmpty &&
        item.Definition.type == ItemType.Collar && UpgradeRule(item);
    public bool OtherPredicate(Item item) => true;
}

public sealed class ZombieEquipmentInventoryWidgetData
{
    public ZombieWgoData? ZombieWgoData { get; }
    public Item? Selected { get; private set; }
    public Action<UIItemCell> CollarCallback => UpgradeCollar;
    public Action<UIItemCell> InsertCallback => InsertItem;
    public Action<UIItemCell> ReplaceCallback => ReplaceItem;
    public Func<Item, bool> EquipmentPredicate => IsEquipment;
    public ZombieEquipmentInventoryWidgetData(ZombieWgoData? zombie) { ZombieWgoData = zombie; }
    private void UpgradeCollar(UIItemCell cell) { Selected = cell.DisplayingItem; }
    private void InsertItem(UIItemCell cell) { }
    private void ReplaceItem(UIItemCell cell) { }
    private bool IsEquipment(Item item) => true;
}
