using System.Collections.Generic;

namespace BepInEx.Configuration;

// Minimal in-memory Bind boundary for checking the production settings declarations.
// No configuration files are read or written by these stand-ins.
public sealed class ConfigFile
{
    private readonly Dictionary<string, object> entries = new Dictionary<string, object>();
    public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
    {
        string id = section + "/" + key;
        if (!entries.TryGetValue(id, out var entry))
            entries.Add(id, entry = new ConfigEntry<T> { Value = defaultValue });
        return (ConfigEntry<T>)entry;
    }
}

public sealed class ConfigEntry<T>
{
    public T Value { get; set; } = default!;
}
