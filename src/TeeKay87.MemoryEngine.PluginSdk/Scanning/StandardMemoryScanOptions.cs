namespace TeeKay87.MemoryEngine.PluginSdk.Scanning;

public static class StandardMemoryScanOptionIds
{
    public const string Endianness = "standard.endianness";
    public const string Alignment = "standard.alignment";
    public const string FloatingPointRounding = "standard.floating-point-rounding";
}

public static class StandardMemoryScanOptionChoiceIds
{
    public const string LittleEndian = "little";
    public const string BigEndian = "big";
    public const string DefaultAlignment = "default";
    public const string FloatingPointStrict = "strict";
    public const string FloatingPointRelativeTolerance1E6 = "relative-tolerance-1e-6";
}
