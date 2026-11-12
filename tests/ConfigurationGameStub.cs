using System.Collections.Generic;

namespace BepInEx.Configuration;

// Minimal in-memory Bind boundary for checking the production settings declarations.
// No configuration files are read or written by these stand-ins.
public sealed class ConfigFile
{
    private readonly Dictionary<string, object> entries = new Dictionary<string, object>();
    private readonly Dictionary<string, object> saved = new Dictionary<string, object>();

    public void LoadSavedValue<T>(string section, string key, T value)
        => saved.Add(section + "/" + key, value!);

    public bool ContainsKey(ConfigDefinition definition)
        => entries.ContainsKey(definition.Id);

    public bool Remove(ConfigDefinition definition)
        => entries.Remove(definition.Id);

    public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
    {
        string id = section + "/" + key;
        if (!entries.TryGetValue(id, out var entry))
        {
            T value = saved.TryGetValue(id, out var stored) ? (T)stored : defaultValue;
            saved.Remove(id);
            entries.Add(id, entry = new ConfigEntry<T> { Value = value });
        }
        return (ConfigEntry<T>)entry;
    }
}

public sealed class ConfigDefinition
{
    internal string Id { get; }
    public ConfigDefinition(string section, string key) => Id = section + "/" + key;
}

public sealed class ConfigEntry<T>
{
    public T Value { get; set; } = default!;
}
