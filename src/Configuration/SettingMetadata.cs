using BepInEx.Configuration;

namespace OnlyApplicableItems.Configuration;

// GK2 Mod Menu and ConfigurationManager discover this optional tag by its type
// name and public members. Neither mod is referenced or required by this plugin.
internal sealed class ConfigurationManagerAttributes
{
    public string DispName = string.Empty;
    public int? Order;
}

internal static class SettingMetadata
{
    public static ConfigDescription Describe(string name, string description, int order)
        => new ConfigDescription(description, null,
            new ConfigurationManagerAttributes { DispName = name, Order = order });
}
