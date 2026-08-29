using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Plugins;

public sealed class PluginHost : IDisposable
{
    private readonly List<PluginLoadContext> _loadContexts = new();
    private bool _disposed;

    public PluginDiscoveryResult Discover(string pluginDirectory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        UnloadPlugins();

        List<DiscoveredPlugin> plugins = new();
        List<PluginDiscoveryError> errors = new();

        if (!Directory.Exists(pluginDirectory))
        {
            return new PluginDiscoveryResult(plugins, errors);
        }

        HashSet<string> pluginIds = new(StringComparer.OrdinalIgnoreCase);

        foreach (string assemblyPath in Directory
                     .EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            DiscoverAssembly(assemblyPath, pluginIds, plugins, errors);
        }

        return new PluginDiscoveryResult(plugins, errors);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UnloadPlugins();
        _disposed = true;
    }

    private void DiscoverAssembly(
        string assemblyPath,
        ISet<string> pluginIds,
        ICollection<DiscoveredPlugin> plugins,
        ICollection<PluginDiscoveryError> errors)
    {
        PluginLoadContext loadContext = new(assemblyPath);
        bool keepContext = false;

        try
        {
            Assembly assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
            IReadOnlyList<Type> pluginTypes = GetLoadableTypes(assembly)
                .Where(type =>
                    !type.IsAbstract &&
                    !type.IsInterface &&
                    type.IsPublic &&
                    typeof(ITargetPlugin).IsAssignableFrom(type))
                .ToArray();

            foreach (Type pluginType in pluginTypes)
            {
                try
                {
                    if (Activator.CreateInstance(pluginType) is not ITargetPlugin plugin)
                    {
                        errors.Add(new PluginDiscoveryError(
                            assemblyPath,
                            $"{pluginType.FullName} could not be created as an ITargetPlugin."));
                        continue;
                    }

                    string? metadataError = ValidateMetadata(plugin.Metadata);
                    if (metadataError is not null)
                    {
                        errors.Add(new PluginDiscoveryError(assemblyPath, metadataError));
                        continue;
                    }

                    if (!pluginIds.Add(plugin.Metadata.Id))
                    {
                        errors.Add(new PluginDiscoveryError(
                            assemblyPath,
                            $"Plugin id '{plugin.Metadata.Id}' is already loaded."));
                        continue;
                    }

                    plugins.Add(new DiscoveredPlugin(plugin, assemblyPath));
                    keepContext = true;
                }
                catch (Exception exception)
                {
                    errors.Add(new PluginDiscoveryError(
                        assemblyPath,
                        $"Failed to create {pluginType.FullName}: {exception.Message}"));
                }
            }
        }
        catch (BadImageFormatException)
        {
            errors.Add(new PluginDiscoveryError(
                assemblyPath,
                "The file is not a compatible .NET plugin assembly."));
        }
        catch (FileLoadException exception)
        {
            errors.Add(new PluginDiscoveryError(assemblyPath, exception.Message));
        }
        catch (Exception exception)
        {
            errors.Add(new PluginDiscoveryError(
                assemblyPath,
                $"Plugin discovery failed: {exception.Message}"));
        }
        finally
        {
            if (keepContext)
            {
                _loadContexts.Add(loadContext);
            }
            else
            {
                loadContext.Unload();
            }
        }
    }

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null).Cast<Type>().ToArray();
        }
    }

    private static string? ValidateMetadata(PluginMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.Id))
        {
            return "Plugin metadata must define a non-empty id.";
        }

        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            return $"Plugin '{metadata.Id}' must define a non-empty name.";
        }

        if (string.IsNullOrWhiteSpace(metadata.Platform))
        {
            return $"Plugin '{metadata.Id}' must define a platform.";
        }

        if (string.IsNullOrWhiteSpace(metadata.Backend))
        {
            return $"Plugin '{metadata.Id}' must define a backend.";
        }

        return null;
    }

    private void UnloadPlugins()
    {
        foreach (PluginLoadContext loadContext in _loadContexts)
        {
            loadContext.Unload();
        }

        _loadContexts.Clear();
    }
}
