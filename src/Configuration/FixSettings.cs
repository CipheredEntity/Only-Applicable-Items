using BepInEx.Configuration;

namespace OnlyApplicableItems.Configuration;

internal sealed class FixSettings
{
    public ConfigEntry<bool> WindowSortingOrder { get; }
    public FixSettings(ConfigFile config)
    {
        WindowSortingOrder = config.Bind("Fixes", "Window Sorting Order", true,
            "Reuse surviving windows' original sorting allocations to prevent repeated nested windows overtaking tooltips. " +
            "Global and independent of every action setting. Existing canvases are never rearranged. " +
            "Disabling stops corrections; enabling or recovery after unexpected state waits for a fresh root window.");
    }
}
