using BepInEx.Configuration;

namespace OnlyApplicableItems.Configuration;

internal sealed class ActionSettings
{
    public ConfigEntry<bool> Seeds { get; }
    public ConfigEntry<bool> Fertilizers { get; }

    public ActionSettings(ConfigFile config)
    {
        Seeds = config.Bind("Actions", "Seeds", true,
            "Combine eligible seeds into one Seeds inventory in the garden planting picker. " +
            "Disabling restores an open picker on the next frame. Enabling takes effect the next time the seed picker opens.");
        Fertilizers = config.Bind("Actions", "Fertilizers", true,
            "Combine all owned fertilizers into one Fertilizers inventory in the garden fertilizer picker. " +
            "Already-applied fertilizers stay visible but disabled. " +
            "Disabling restores an open fertilizer picker on the next frame. Enabling takes effect the next time it opens.");
    }
}
