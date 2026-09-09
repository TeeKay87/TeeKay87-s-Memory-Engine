namespace TeeKay87.MemoryEngine.PluginSdk.Scanning;

/// <summary>
/// Stable identifiers used when a Core-owned Scan Type crosses the Plugin SDK boundary,
/// for example when a plugin decides whether it can accelerate a native scan request.
/// Scan Type definitions and matching behavior are owned by Core.
/// </summary>
public static class StandardMemoryScanTypeIds
{
    public const string ExactValue = "standard.exact-value";
    public const string FuzzyValue = "standard.fuzzy-value";
    public const string BiggerThan = "standard.bigger-than";
    public const string SmallerThan = "standard.smaller-than";
    public const string Between = "standard.between";
    public const string UnknownInitialValue = "standard.unknown-initial-value";
    public const string UnknownInitialLowValue = "standard.unknown-initial-low-value";
    public const string IncreasedValue = "standard.increased-value";
    public const string DecreasedValue = "standard.decreased-value";
    public const string ChangedValue = "standard.changed-value";
    public const string UnchangedValue = "standard.unchanged-value";
    public const string IncreasedBy = "standard.increased-by";
    public const string DecreasedBy = "standard.decreased-by";
}
