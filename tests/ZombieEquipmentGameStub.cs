using System;

// Test-only stand-ins for the production equipment contract's delegate boundaries.
// Rules supply changing native results; transfer logic and skull arithmetic are not simulated.
public sealed class ZombieWgoData
{
    public Func<Item, bool> UpgradeRule = _ => true;
    public Item? Armor { get; set; }
    public Item? Weapon { get; set; }
    public Item? Hand { get; set; }
    public Item? Collar { get; set; }
    public Func<Item, bool> AddToBodyRule = _ => true;
    public int AddToBodyCalls;
    public bool CanAddItemToBody(Item item) { AddToBodyCalls++; return AddToBodyRule(item); }
    public Func<Item, bool> UpgradePredicate => CanUpgradeCollarTo;
    public bool CanUpgradeCollarTo(Item item) => item != null && !item.IsEmpty &&
        item.Definition.type == ItemType.Collar && UpgradeRule(item);
    public bool OtherPredicate(Item item) => true;
}

public sealed class ZombieEquipmentInventoryWidgetData
{
    public ZombieWgoData? ZombieWgoData { get; }
    public Item? Selected { get; private set; }
    public string? SelectedAction { get; private set; }
    public Func<Item, bool> EquipmentRule = _ => true;
    public Action<UIItemCell> CollarCallback => UpgradeCollar;
    public Action<UIItemCell> InsertCallback => InsertItem;
    public Action<UIItemCell> ReplaceCallback => ReplaceItem;
    public Func<Item, bool> EquipmentPredicate => IsEquipment;
    public Func<Item, bool> ArmorPredicate => IsArmor;
    public Func<Item, bool> WeaponPredicate => IsWeapon;
    public Func<Item, bool> ToolPredicate => IsTool;
    public ZombieEquipmentInventoryWidgetData(ZombieWgoData? zombie) { ZombieWgoData = zombie; }
    private void UpgradeCollar(UIItemCell cell) { Selected = cell.DisplayingItem; }
    private void InsertItem(UIItemCell cell) { Selected = cell.DisplayingItem; SelectedAction = "Insert"; }
    public void ReplaceItem(UIItemCell cell) { Selected = cell.DisplayingItem; SelectedAction = "Replace"; }
    private bool IsEquipment(Item item) => true;
    private bool IsArmor(Item item) => item != null && item.Definition.type == ItemType.BodyArmor && EquipmentRule(item);
    private bool IsTool(Item item) => item != null && !item.IsEmpty && item.Definition.isTool && EquipmentRule(item);
    private bool IsWeapon(Item item) => item != null && !item.IsEmpty && item.Definition.isWeapon &&
        !item.Definition.isTool && item.Definition.type != ItemType.Sword && EquipmentRule(item);
}
