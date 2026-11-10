using BepInEx.Configuration;

namespace OnlyApplicableItems.Configuration;

internal sealed class ActionSettings
{
    public ConfigEntry<bool> Seeds { get; }
    public ConfigEntry<bool> Fertilizers { get; }
    public ConfigEntry<bool> AlchemicalIngredients { get; }

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
    }
}
