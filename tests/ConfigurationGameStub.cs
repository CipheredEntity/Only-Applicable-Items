using System.Collections.Generic;

namespace BepInEx.Configuration;

// Minimal in-memory Bind boundary for checking the production settings declarations.
// No configuration files are read or written by these stand-ins.
public sealed class ConfigFile
{
    private readonly Dictionary<string, object> entries = new Dictionary<string, object>();
    private readonly Dictionary<string, object> saved = new Dictionary<string, object>();
    public IReadOnlyDictionary<string, object> Entries => entries;

    public void LoadSavedValue<T>(string section, string key, T value)
        => saved.Add(section + "/" + key, value!);

    public bool ContainsKey(ConfigDefinition definition)
        => entries.ContainsKey(definition.Id);

    public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        => Bind(section, key, defaultValue, new ConfigDescription(description));

    public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, ConfigDescription description)
    {
        string id = section + "/" + key;
        if (!entries.TryGetValue(id, out var entry))
        {
            T value = saved.TryGetValue(id, out var stored) ? (T)stored : defaultValue;
            saved.Remove(id);
            entries.Add(id, entry = new ConfigEntry<T>
            {
                Value = value, DefaultValue = defaultValue, Description = description,
                Definition = new ConfigDefinition(section, key)
            });
        }
        return (ConfigEntry<T>)entry;
    }
}

public sealed class ConfigDefinition
{
    internal string Id { get; }
    public string Section { get; }
    public string Key { get; }
    public ConfigDefinition(string section, string key)
    { Section = section; Key = key; Id = section + "/" + key; }
}

public sealed class ConfigDescription
{
    public string Description { get; }
    public object[] Tags { get; }
    public ConfigDescription(string description, object? acceptableValues = null, params object[] tags)
    { Description = description; Tags = tags; }
}

public sealed class ConfigEntry<T>
{
    public T Value { get; set; } = default!;
    public T DefaultValue { get; set; } = default!;
    public ConfigDescription Description { get; set; } = new("");
    public ConfigDefinition Definition { get; set; } = null!;
}
