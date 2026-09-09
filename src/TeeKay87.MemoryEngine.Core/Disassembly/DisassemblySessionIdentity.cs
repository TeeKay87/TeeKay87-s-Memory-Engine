using System;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Disassembly;

public sealed record DisassemblySessionIdentity
{
    public DisassemblySessionIdentity(
        string pluginId,
        TargetProcess process,
        long connectionGeneration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(process);

        PluginId = pluginId;
        ProcessId = process.Id;
        ProcessName = process.Name;
        ConnectionGeneration = connectionGeneration;
    }

    public string PluginId { get; }

    public ulong ProcessId { get; }

    public string ProcessName { get; }

    public long ConnectionGeneration { get; }

    public bool Matches(
        string pluginId,
        TargetProcess process,
        long connectionGeneration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(process);

        return string.Equals(PluginId, pluginId, StringComparison.Ordinal) &&
               ProcessId == process.Id &&
               string.Equals(ProcessName, process.Name, StringComparison.Ordinal) &&
               ConnectionGeneration == connectionGeneration;
    }

    public void EnsureCurrent(
        string pluginId,
        TargetProcess process,
        long connectionGeneration)
    {
        if (!Matches(pluginId, process, connectionGeneration))
        {
            throw new InvalidOperationException(
                "The disassembly session belongs to a different plugin, target process, or connection generation.");
        }
    }
}
