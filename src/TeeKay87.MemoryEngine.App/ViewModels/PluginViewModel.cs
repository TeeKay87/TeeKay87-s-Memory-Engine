using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TeeKay87.MemoryEngine.Core.Plugins;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed partial class PluginViewModel
{
    public PluginViewModel(DiscoveredPlugin discoveredPlugin)
    {
        ArgumentNullException.ThrowIfNull(discoveredPlugin);

        Instance = discoveredPlugin.Instance;
        AssemblyPath = discoveredPlugin.AssemblyPath;
        Metadata = Instance.Metadata;
        Capabilities = EnumerateCapabilities(Instance.Capabilities);
    }

    public ITargetPlugin Instance { get; }

    public PluginMetadata Metadata { get; }

    public string AssemblyPath { get; }

    public string Name => Metadata.Name;

    public string Platform => Metadata.Platform;

    public string Backend => Metadata.Backend;

    public string Version => Metadata.Version.ToString();

    public string Description => Metadata.Description;

    public string ArchitectureDisplay =>
        $"{Metadata.Architecture.Cpu} · {Metadata.Architecture.PointerWidthBits}-bit pointers · " +
        $"{Metadata.Architecture.Endianness} endian";

    public IReadOnlyList<string> Capabilities { get; }

    private static IReadOnlyList<string> EnumerateCapabilities(TargetCapabilities capabilities)
    {
        return Enum.GetValues<TargetCapabilities>()
            .Where(capability =>
                capability != TargetCapabilities.None &&
                IsSingleFlag(capability) &&
                capabilities.HasFlag(capability))
            .Select(capability => SplitPascalCaseRegex().Replace(capability.ToString(), " $1"))
            .ToArray();
    }

    private static bool IsSingleFlag(TargetCapabilities capability)
    {
        ulong value = (ulong)capability;
        return value != 0 && (value & (value - 1)) == 0;
    }

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex SplitPascalCaseRegex();
}
