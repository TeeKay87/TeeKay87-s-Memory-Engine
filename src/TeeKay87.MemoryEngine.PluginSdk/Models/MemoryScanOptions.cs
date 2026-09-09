using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class MemoryScanOptions
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public MemoryScanOptions(IEnumerable<KeyValuePair<string, string>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Dictionary<string, string> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> pair in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Value);
            normalized[pair.Key] = pair.Value;
        }

        _values = new ReadOnlyDictionary<string, string>(normalized);
    }

    public static MemoryScanOptions Empty { get; } = new(Array.Empty<KeyValuePair<string, string>>());

    public IReadOnlyDictionary<string, string> Values => _values;

    public bool TryGetValue(string optionId, out string choiceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);
        return _values.TryGetValue(optionId, out choiceId!);
    }

    public string? GetValueOrDefault(string optionId)
    {
        return TryGetValue(optionId, out string choiceId) ? choiceId : null;
    }
}
