using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;

namespace TeeKay87.MemoryEngine.Core.Settings;

/// <summary>
/// Owns the shared application settings document and exposes isolated plugin-scoped settings views.
/// </summary>
public sealed class JsonSettingsStore
{
    private const string PluginsPropertyName = "plugins";
    private static readonly object FileSyncRoot = new();
    private static readonly JsonNodeOptions NodeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public JsonSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = Path.GetFullPath(settingsPath);
    }

    public string? LoadApplicationString(string key)
    {
        ValidateKey(key);

        lock (FileSyncRoot)
        {
            JsonObject root = LoadRootCore();
            return ReadString(root, key);
        }
    }

    public void SaveApplicationString(string key, string value)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (FileSyncRoot)
        {
            JsonObject root = LoadRootCore();
            root[key] = value;
            SaveRootCore(root);
        }
    }

    public IPluginSettings CreatePluginSettings(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return new PluginSettingsScope(this, pluginId.Trim());
    }

    private bool TryGetPluginString(string pluginId, string key, out string value)
    {
        ValidateKey(key);

        lock (FileSyncRoot)
        {
            JsonObject root = LoadRootCore();
            if (root[PluginsPropertyName] is JsonObject plugins &&
                plugins[pluginId] is JsonObject pluginSettings)
            {
                string? storedValue = ReadString(pluginSettings, key);
                if (storedValue is not null)
                {
                    value = storedValue;
                    return true;
                }
            }
        }

        value = string.Empty;
        return false;
    }

    private bool TrySetPluginString(string pluginId, string key, string value)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            lock (FileSyncRoot)
            {
                JsonObject root = LoadRootCore();
                JsonObject plugins = GetOrCreateObject(root, PluginsPropertyName);
                JsonObject pluginSettings = GetOrCreateObject(plugins, pluginId);
                pluginSettings[key] = value;
                SaveRootCore(root);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private bool TryRemovePluginSetting(string pluginId, string key)
    {
        ValidateKey(key);

        try
        {
            lock (FileSyncRoot)
            {
                JsonObject root = LoadRootCore();
                if (root[PluginsPropertyName] is not JsonObject plugins ||
                    plugins[pluginId] is not JsonObject pluginSettings ||
                    !pluginSettings.Remove(key))
                {
                    return true;
                }

                if (pluginSettings.Count == 0)
                {
                    plugins.Remove(pluginId);
                }

                SaveRootCore(root);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private JsonObject LoadRootCore()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new JsonObject(NodeOptions);
            }

            string json = File.ReadAllText(_settingsPath);
            JsonNode? node = JsonNode.Parse(json, NodeOptions);
            return node as JsonObject ?? new JsonObject(NodeOptions);
        }
        catch (IOException)
        {
            return new JsonObject(NodeOptions);
        }
        catch (UnauthorizedAccessException)
        {
            return new JsonObject(NodeOptions);
        }
        catch (JsonException)
        {
            return new JsonObject(NodeOptions);
        }
    }

    private void SaveRootCore(JsonObject root)
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            string json = root.ToJsonString(WriteOptions);
            using (FileStream stream = new(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            using (StreamWriter writer = new(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
        {
            return existing;
        }

        JsonObject created = new(NodeOptions);
        parent[propertyName] = created;
        return created;
    }

    private static string? ReadString(JsonObject values, string key)
    {
        if (values[key] is not JsonValue value ||
            !value.TryGetValue(out string? storedValue) ||
            storedValue is null)
        {
            return null;
        }

        return storedValue;
    }

    private static void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
    }

    private sealed class PluginSettingsScope : IPluginSettings
    {
        private readonly JsonSettingsStore _owner;
        private readonly string _pluginId;

        public PluginSettingsScope(JsonSettingsStore owner, string pluginId)
        {
            _owner = owner;
            _pluginId = pluginId;
        }

        public bool TryGetString(string key, out string value)
        {
            return _owner.TryGetPluginString(_pluginId, key, out value);
        }

        public bool TrySetString(string key, string value)
        {
            return _owner.TrySetPluginString(_pluginId, key, value);
        }

        public bool TryRemove(string key)
        {
            return _owner.TryRemovePluginSetting(_pluginId, key);
        }
    }
}
