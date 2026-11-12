using System;
using System.IO;
using BepInEx.Configuration;

namespace OnlyApplicableItems.Configuration;

// BepInEx does not watch config files. Check only when opening a picker/shop;
// never reload settings or replace titles in the middle of an active session.
internal sealed class ConfigurationReload
{
    private readonly ConfigFile config;
    private readonly Action<Exception> warn;
    private DateTime modified;
    private long length = -1;

    public ConfigurationReload(ConfigFile config, Action<Exception> warn)
    {
        this.config = config;
        this.warn = warn;
        try { Remember(new FileInfo(config.ConfigFilePath)); }
        catch (Exception) { } // Retry safely at the next opening.
    }

    public void Refresh()
    {
        try
        {
            var file = new FileInfo(config.ConfigFilePath);
            if (!file.Exists || (file.LastWriteTimeUtc == modified && file.Length == length)) return;
            config.Reload();
            Remember(new FileInfo(config.ConfigFilePath));
        }
        catch (Exception exception)
        {
            // A temporarily locked or malformed file must not break a gameplay window.
            try { warn(exception); } catch (Exception) { }
        }
    }

    private void Remember(FileInfo file)
    {
        if (!file.Exists) return;
        modified = file.LastWriteTimeUtc;
        length = file.Length;
    }
}
