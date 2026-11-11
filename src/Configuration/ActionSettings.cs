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

    public ActionSettings(ConfigFile config)
    {
        Seeds = config.Bind("Actions", "Seeds", true,
            "Combine eligible seeds into one Seeds inventory in the garden planting picker. " +
            "Disabling restores an open picker on the next frame. Enabling takes effect the next time the seed picker opens.");
        Fertilizers = config.Bind("Actions", "Fertilizers", true,
            "Combine all owned fertilizers into one Fertilizers inventory in the garden fertilizer picker. " +
            "Already-applied fertilizers stay visible but disabled. " +
            "Disabling restores an open fertilizer picker on the next frame. Enabling takes effect the next time it opens.");
        AlchemicalIngredients = config.Bind("Actions", "Alchemical Ingredients", true,
            "Consolidate eligible alchemical ingredients and show total accessible stock in laboratory ingredient counters. " +
            "Researched ingredients blocked by rune limits remain visible but disabled; unresearched and non-alchemical items stay hidden. " +
            "Controls both improvements independently of Seeds and Fertilizers. Disabling restores the original picker and counters. " +
            "Enabling refreshes an open laboratory; consolidation applies the next time its ingredient picker opens.");
        BodyParts = config.Bind("Actions", "Body Parts", true,
            "Consolidate body parts for autopsy insertion and zombie replacement into Brains, Hearts, Guts, Skulls, Bones, or Skins. " +
            "Matching parts rejected by the game's rules remain visible but disabled. " +
            "Disabling restores an open body-part picker on the next frame. Enabling takes effect the next time it opens.");
        EmbalmingFluids = config.Bind("Actions", "Embalming Fluids", true,
            "Combine owned embalming fluids into one Embalming Fluids inventory. " +
            "Fluids blocked by the game's rules remain visible but disabled. " +
            "Disabling restores an open embalming picker on the next frame. Enabling takes effect the next time it opens.");
        GraveDecorations = config.Bind("Actions", "Grave Decorations", true,
            "Combine grave decorations into Tombstones or Fences inventories, preserving the game's eligibility and crafting rules. " +
            "Controls both categories independently of other actions. Disabling restores an open decoration picker on the next frame. " +
            "Enabling takes effect the next time it opens.");
        ZombieEquipment = config.Bind("Actions", "Zombie Equipment", true,
            "Consolidate Collars, Armor, Weapons, and Tools in zombie equipment selection. " +
            "Items rejected by the native rules remain visible but disabled. Unusual item data or non-single-item equipment restores the original picker. " +
            "Disabling restores an open equipment picker on the next frame. Enabling takes effect the next time it opens.");
    }
}
