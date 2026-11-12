using BepInEx.Configuration;

namespace OnlyApplicableItems.Configuration;

internal sealed class FixSettings
{
    public ConfigEntry<bool> WindowSortingOrder { get; }
    public FixSettings(ConfigFile config)
    {
        WindowSortingOrder = config.Bind("Fixes", "Window Sorting Order", true,
            SettingMetadata.Describe("Window Sorting Order",
                "Prevents tooltips from appearing behind windows after repeatedly opening them.", 10));
    }
}
