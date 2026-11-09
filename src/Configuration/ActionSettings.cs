using BepInEx.Configuration;

namespace OnlyApplicableItems.Configuration;

internal sealed class ActionSettings
{
    public ConfigEntry<bool> Seeds { get; }

    public ActionSettings(ConfigFile config)
    {
        Seeds = config.Bind("Actions", "Seeds", true,
            "Combine eligible seeds into one Seeds inventory in the garden planting picker. " +
            "Disabling restores an open picker on the next frame. Enabling takes effect the next time the seed picker opens.");
    }
}
