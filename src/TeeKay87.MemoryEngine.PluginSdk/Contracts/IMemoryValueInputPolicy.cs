namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

/// <summary>
/// Optional live-editing policy for an <see cref="IMemoryValueType"/>.
/// </summary>
/// <remarks>
/// Implementations should accept empty and other temporarily incomplete text when it can still
/// become valid through continued editing. Final value validity remains the responsibility of
/// <see cref="IMemoryValueType.TryParse"/>.
/// </remarks>
public interface IMemoryValueInputPolicy
{
    bool IsPotentiallyValidInput(string text);
}
