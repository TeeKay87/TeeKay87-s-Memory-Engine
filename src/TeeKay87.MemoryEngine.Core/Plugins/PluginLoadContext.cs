using System;
using System.Reflection;
using System.Runtime.Loader;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;

namespace TeeKay87.MemoryEngine.Core.Plugins;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly AssemblyName PluginSdkAssemblyName =
        typeof(ITargetPlugin).Assembly.GetName();

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginAssemblyPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (AssemblyName.ReferenceMatchesDefinition(assemblyName, PluginSdkAssemblyName))
        {
            return null;
        }

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(libraryPath);
    }
}
