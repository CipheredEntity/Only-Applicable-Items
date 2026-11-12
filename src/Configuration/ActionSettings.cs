using BepInEx.Configuration;

namespace OnlyApplicableItems.Configuration;

internal sealed class ActionSettings
{
    public ConfigEntry<bool> Seeds { get; }
    public ConfigEntry<bool> Fertilizers { get; }
    public ConfigEntry<bool> AlchemicalIngredients { get; }
    public ConfigEntry<bool> BodyParts { get; }
    public ConfigEntry<bool> EmbalmingFluids { get; }
    public ConfigEntry<bool> GraveDecorations { get; }
    public ConfigEntry<bool> ZombieEquipment { get; }
    public ConfigEntry<bool> ResurrectionCollars { get; }
    public ConfigEntry<bool> Sermons { get; }
    public ConfigEntry<bool> StudyTable { get; }
    public ConfigEntry<bool> ShopSelling { get; }

    public ActionSettings(ConfigFile config)
    {
        Seeds = Bind("Seeds", "Combine available seeds when choosing what to plant.", 110);
        Fertilizers = Bind("Fertilizers", "Combine fertilizers when treating a garden bed. Unavailable fertilizers remain visible but grayed out.", 100);
        AlchemicalIngredients = Bind("Alchemical Ingredients", "Combine researched alchemy ingredients and show their total available quantities in the laboratory. Ingredients blocked by rune limits remain visible but grayed out.", 90);
        BodyParts = Bind("Body Parts", "Combine matching body parts when inserting or replacing organs. Unavailable parts remain visible but grayed out.", 80);
        EmbalmingFluids = Bind("Embalming Fluids", "Combine embalming fluids. Unavailable fluids remain visible but grayed out, with a warning when a zombie's collar prevents use.", 70);
        GraveDecorations = Bind("Grave Decorations", "Combine tombstones and fences when decorating a grave.", 60);
        ZombieEquipment = Bind("Zombie Equipment", "Combine collars, armor, weapons, and tools when equipping zombies. Unavailable or already-equipped equipment remains visible but grayed out.", 50);
        ResurrectionCollars = Bind("Resurrection Collars", "Combine collars at the resurrection table. Unsuitable collars remain visible but grayed out. Hide the selected collar's stack quantity.", 40);
        Sermons = Bind("Prayers", "Combine prayers when choosing one at the prayer stand.", 30);
        StudyTable = Bind("Study Table", "Group study-table items into Study and Decompose inventories. Hide quantities for items being studied and sack icons on both headers.", 20);
        ShopSelling = Bind("Shop Selling", "Combine items the merchant buys or will buy at a higher tier. Higher-tier items remain visible but grayed out.", 10);

        ConfigEntry<bool> Bind(string name, string description, int order)
            => config.Bind("Actions", name, true, SettingMetadata.Describe(name, description, order));
    }
}
