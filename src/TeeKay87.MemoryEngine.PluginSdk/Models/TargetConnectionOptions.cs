using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class TargetConnectionOptions
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public TargetConnectionOptions(IEnumerable<KeyValuePair<string, string>>? values = null)
    {
        Dictionary<string, string> copy = new(StringComparer.OrdinalIgnoreCase);

        if (values is not null)
        {
            foreach (KeyValuePair<string, string> entry in values)
            {
                copy[entry.Key] = entry.Value;
            }
        }

        _values = new ReadOnlyDictionary<string, string>(copy);
    }

    public static TargetConnectionOptions Empty { get; } = new();

    public IReadOnlyDictionary<string, string> Values => _values;

    public bool TryGetValue(string key, out string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_values.TryGetValue(key, out string? resolvedValue) && resolvedValue is not null)
        {
            value = resolvedValue;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
