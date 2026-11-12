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
            "Armor, Weapons, and Tools matching the currently equipped item stay visible but disabled. " +
            "Items rejected by the native rules remain visible but disabled. Unusual item data or non-single-item equipment restores the original picker. " +
            "Disabling restores an open equipment picker on the next frame. Enabling takes effect the next time it opens.");
        ResurrectionCollars = config.Bind("Actions", "Resurrection Collars", true,
            "Combine collars into one Collars inventory at the resurrection table, including accessible zone storage. " +
            "Collars rejected by the native skull requirements remain visible but disabled. " +
            "Also hide the selected resurrection collar slot's stack counter; disabling restores its native display. " +
            "Independent of Zombie Equipment. Disabling restores an open resurrection collar picker on the next frame; " +
            "enabling applies the next time it opens. Selection and later one-unit consumption remain native.");
        Sermons = config.Bind("Actions", "Sermons", true,
            "Combine native-eligible sermons into one Sermons inventory at the prayer stand. " +
            "Preserves full item definitions and quality variants. Attendance, success chance, execution, and rewards remain native. " +
            "Disabling restores an open sermon picker on the next frame; enabling applies the next time it opens.");
        StudyTable = config.Bind("Actions", "Study Table", true,
            "Consolidate the study picker into Researchable and Decomposable, in that order, including empty sections. " +
            "Hide Researchable quantity labels and both category header icons; Decomposable quantities stay visible. " +
            "Also hide the selected-item quantity for research, using the native survey classification; disabling restores the native slot display. " +
            "Uses native eligibility and survey resolution; requirements, completion, and consumption remain unchanged. " +
            "Disabling restores both sections to the original picker on the next frame; enabling applies the next time it opens.");
    }
}
