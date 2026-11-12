using BepInEx.Configuration;

namespace OnlyApplicableItems.Configuration;

internal sealed class FixSettings
{
    public ConfigEntry<bool> WindowSortingOrder { get; }
    public FixSettings(ConfigFile config)
    {
        WindowSortingOrder = config.Bind("Fixes", "Window Sorting Order", true,
            SettingMetadata.Describe("Window Sorting Order",
                "Keep tooltips in front of inventory windows after repeated openings. Applies throughout the game. After enabling, close all windows and reopen them.", 10));
    }
}
