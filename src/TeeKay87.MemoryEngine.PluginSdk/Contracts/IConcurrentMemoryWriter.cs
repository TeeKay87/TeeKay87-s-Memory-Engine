namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

/// <summary>
/// Optional memory-writer service that is safe to use while another operation is active
/// on the session's primary target transport.
/// </summary>
public interface IConcurrentMemoryWriter : IMemoryWriter
{
}
