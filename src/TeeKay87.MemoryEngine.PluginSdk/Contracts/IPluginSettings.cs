namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

/// <summary>
/// Provides a plugin-scoped settings namespace managed by the host.
/// Plugins use stable keys and never need to know how or where settings are persisted.
/// </summary>
public interface IPluginSettings
{
    bool TryGetString(string key, out string value);

    bool TrySetString(string key, string value);

    bool TryRemove(string key);
}

/// <summary>
/// Optional plugin contract used when a plugin wants access to its host-managed settings namespace.
/// </summary>
public interface IPluginSettingsConsumer
{
    void AttachSettings(IPluginSettings settings);
}
