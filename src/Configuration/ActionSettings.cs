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
        Seeds = Bind("Seeds", string.Empty, 110);
        Fertilizers = Bind("Fertilizers", string.Empty, 100);
        AlchemicalIngredients = Bind("Alchemical Ingredients", "Also corrects ingredient quantity counters.", 90);
        BodyParts = Bind("Body Parts", "Controls Brains, Hearts, Guts, Skulls, Bones, and Skins.", 80);
        EmbalmingFluids = Bind("Embalming Fluids", string.Empty, 70);
        GraveDecorations = Bind("Grave Decorations", "Controls Tombstones and Fences.", 60);
        ZombieEquipment = Bind("Zombie Equipment", "Controls Collars, Armor, Weapons, and Tools.", 50);
        ResurrectionCollars = Bind("Resurrection Collars", string.Empty, 40);
        Sermons = Bind("Prayers", string.Empty, 30);
        StudyTable = Bind("Study Table", "Controls Study and Decompose.", 20);
        ShopSelling = Bind("Shop Selling", string.Empty, 10);

        ConfigEntry<bool> Bind(string name, string description, int order)
            => config.Bind("Actions", name, true, SettingMetadata.Describe(name, description, order));
    }
}
