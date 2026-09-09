using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TeeKay87.MemoryEngine.Core.Scanning;
using TeeKay87.MemoryEngine.Core.Settings;
using TeeKay87.MemoryEngine.PluginSdk;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Core.Plugins;

public sealed class PluginHost : IDisposable
{
    private readonly List<PluginLoadContext> _loadContexts = new();
    private readonly JsonSettingsStore? _settingsStore;
    private bool _disposed;

    public PluginHost(JsonSettingsStore? settingsStore = null)
    {
        _settingsStore = settingsStore;
    }

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

                    if (_settingsStore is not null && plugin is IPluginSettingsConsumer settingsConsumer)
                    {
                        settingsConsumer.AttachSettings(
                            _settingsStore.CreatePluginSettings(plugin.Metadata.Id));
                    }

                    string? connectionSettingsError = ValidateConnectionSettings(
                        plugin.Metadata.Id,
                        plugin.ConnectionSettings);
                    if (connectionSettingsError is not null)
                    {
                        errors.Add(new PluginDiscoveryError(assemblyPath, connectionSettingsError));
                        continue;
                    }

                    string? scanCapabilitiesError = ValidateScanCapabilities(
                        plugin.Metadata.Id,
                        plugin.SupportedValueTypes,
                        plugin.SupportedScanOptions,
                        plugin.DefaultValueTypeId);
                    if (scanCapabilitiesError is not null)
                    {
                        errors.Add(new PluginDiscoveryError(assemblyPath, scanCapabilitiesError));
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

        if (metadata.Version is null)
        {
            return $"Plugin '{metadata.Id}' must define a version.";
        }

        if (metadata.Revision < 1)
        {
            return $"Plugin '{metadata.Id}' must define a revision of 1 or greater.";
        }

        if (metadata.ApiVersion is null)
        {
            return $"Plugin '{metadata.Id}' must define a Plugin API version.";
        }

        if (!PluginApiInfo.IsCompatible(metadata.ApiVersion))
        {
            return $"Plugin '{metadata.Id}' targets Plugin API {metadata.ApiVersion}, but this host provides {PluginApiInfo.CurrentVersion}.";
        }

        return null;
    }

    private static string? ValidateConnectionSettings(
        string pluginId,
        IReadOnlyList<TargetConnectionSettingDefinition>? settings)
    {
        if (settings is null)
        {
            return $"Plugin '{pluginId}' returned a null connection-settings collection.";
        }

        HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);

        foreach (TargetConnectionSettingDefinition setting in settings)
        {
            if (string.IsNullOrWhiteSpace(setting.Key))
            {
                return $"Plugin '{pluginId}' contains a connection setting with an empty key.";
            }

            if (string.IsNullOrWhiteSpace(setting.Label))
            {
                return $"Plugin '{pluginId}' connection setting '{setting.Key}' must define a label.";
            }

            if (!keys.Add(setting.Key))
            {
                return $"Plugin '{pluginId}' defines duplicate connection setting key '{setting.Key}'.";
            }
        }

        return null;
    }

    private static string? ValidateScanCapabilities(
        string pluginId,
        IReadOnlyList<IMemoryValueType>? valueTypes,
        IReadOnlyList<IMemoryScanOption>? scanOptions,
        string? defaultValueTypeId)
    {
        if (valueTypes is null)
        {
            return $"Plugin '{pluginId}' returned a null supported-value-types collection.";
        }

        if (scanOptions is null)
        {
            return $"Plugin '{pluginId}' returned a null supported-scan-options collection.";
        }

        HashSet<string> uniqueValueTypeIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (IMemoryValueType? valueType in valueTypes)
        {
            if (valueType is null)
            {
                return $"Plugin '{pluginId}' contains a null value-type definition.";
            }

            if (string.IsNullOrWhiteSpace(valueType.Id))
            {
                return $"Plugin '{pluginId}' contains a value type with an empty id.";
            }

            if (string.IsNullOrWhiteSpace(valueType.DisplayName))
            {
                return $"Plugin '{pluginId}' value type '{valueType.Id}' must define a display name.";
            }

            if (valueType.DefaultAlignment <= 0)
            {
                return $"Plugin '{pluginId}' value type '{valueType.Id}' must define a positive default alignment.";
            }

            if (valueType.FixedSize is <= 0)
            {
                return $"Plugin '{pluginId}' value type '{valueType.Id}' must define a positive fixed size when a fixed size is supplied.";
            }

            if (!uniqueValueTypeIds.Add(valueType.Id))
            {
                return $"Plugin '{pluginId}' declares duplicate value type id '{valueType.Id}'.";
            }
        }

        HashSet<string> uniqueScanOptionIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (IMemoryScanOption? scanOption in scanOptions)
        {
            if (scanOption is null)
            {
                return $"Plugin '{pluginId}' contains a null scan-option definition.";
            }

            if (string.IsNullOrWhiteSpace(scanOption.Id))
            {
                return $"Plugin '{pluginId}' contains a scan option with an empty id.";
            }

            if (string.IsNullOrWhiteSpace(scanOption.DisplayName))
            {
                return $"Plugin '{pluginId}' scan option '{scanOption.Id}' must define a display name.";
            }

            if (!uniqueScanOptionIds.Add(scanOption.Id))
            {
                return $"Plugin '{pluginId}' declares duplicate scan option id '{scanOption.Id}'.";
            }

            if (scanOption.Choices is null || scanOption.Choices.Count == 0)
            {
                return $"Plugin '{pluginId}' scan option '{scanOption.Id}' must expose at least one choice.";
            }

            HashSet<string> uniqueChoiceIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (MemoryScanOptionChoice? choice in scanOption.Choices)
            {
                if (choice is null)
                {
                    return $"Plugin '{pluginId}' scan option '{scanOption.Id}' contains a null choice.";
                }

                if (string.IsNullOrWhiteSpace(choice.Id))
                {
                    return $"Plugin '{pluginId}' scan option '{scanOption.Id}' contains a choice with an empty id.";
                }

                if (string.IsNullOrWhiteSpace(choice.DisplayName))
                {
                    return $"Plugin '{pluginId}' scan option '{scanOption.Id}' choice '{choice.Id}' must define a display name.";
                }

                if (!uniqueChoiceIds.Add(choice.Id))
                {
                    return $"Plugin '{pluginId}' scan option '{scanOption.Id}' declares duplicate choice id '{choice.Id}'.";
                }
            }

            if (string.IsNullOrWhiteSpace(scanOption.DefaultChoiceId) ||
                !uniqueChoiceIds.Contains(scanOption.DefaultChoiceId))
            {
                return $"Plugin '{pluginId}' scan option '{scanOption.Id}' default choice '{scanOption.DefaultChoiceId}' is not present in Choices.";
            }

            if (scanOption is IMemoryScanOptionPresentation presentation)
            {
                if (!Enum.IsDefined(typeof(MemoryScanOptionPresentationKind), presentation.PresentationKind))
                {
                    return $"Plugin '{pluginId}' scan option '{scanOption.Id}' declares an unknown presentation kind '{presentation.PresentationKind}'.";
                }

                if (presentation.PresentationKind == MemoryScanOptionPresentationKind.Toggle)
                {
                    string toggleLabel = presentation.ToggleLabel;
                    string? checkedChoiceId = presentation.CheckedChoiceId;
                    string? uncheckedChoiceId = presentation.UncheckedChoiceId;

                    if (string.IsNullOrWhiteSpace(toggleLabel))
                    {
                        return $"Plugin '{pluginId}' toggle scan option '{scanOption.Id}' must define a toggle label.";
                    }

                    if (string.IsNullOrWhiteSpace(checkedChoiceId) ||
                        !uniqueChoiceIds.Contains(checkedChoiceId))
                    {
                        return $"Plugin '{pluginId}' toggle scan option '{scanOption.Id}' checked choice '{checkedChoiceId}' is not present in Choices.";
                    }

                    if (string.IsNullOrWhiteSpace(uncheckedChoiceId) ||
                        !uniqueChoiceIds.Contains(uncheckedChoiceId))
                    {
                        return $"Plugin '{pluginId}' toggle scan option '{scanOption.Id}' unchecked choice '{uncheckedChoiceId}' is not present in Choices.";
                    }

                    if (string.Equals(checkedChoiceId, uncheckedChoiceId, StringComparison.OrdinalIgnoreCase))
                    {
                        return $"Plugin '{pluginId}' toggle scan option '{scanOption.Id}' must map checked and unchecked states to different choices.";
                    }
                }
            }

            if (scanOption is IMemoryScanOptionApplicability applicability)
            {
                bool supportsAnyScanType = false;
                foreach (IMemoryScanType scanType in MemoryScanTypeCatalog.All)
                {
                    try
                    {
                        supportsAnyScanType |=
                            (scanType.AvailableForFirstScan && applicability.SupportsScanType(scanType, MemoryScanStage.FirstScan)) ||
                            (scanType.AvailableForNextScan && applicability.SupportsScanType(scanType, MemoryScanStage.NextScan));
                    }
                    catch (Exception exception)
                    {
                        return $"Plugin '{pluginId}' scan option '{scanOption.Id}' failed while checking scan type '{scanType.Id}': {exception.Message}";
                    }
                }

                if (!supportsAnyScanType)
                {
                    return $"Plugin '{pluginId}' scan option '{scanOption.Id}' does not support any Core scan type.";
                }
            }

            bool supportsAnyValueType = false;
            foreach (IMemoryValueType valueType in valueTypes)
            {
                try
                {
                    supportsAnyValueType |= scanOption.SupportsValueType(valueType);
                }
                catch (Exception exception)
                {
                    return $"Plugin '{pluginId}' scan option '{scanOption.Id}' failed while checking value type '{valueType.Id}': {exception.Message}";
                }
            }

            if (valueTypes.Count > 0 && !supportsAnyValueType)
            {
                return $"Plugin '{pluginId}' scan option '{scanOption.Id}' does not support any declared value type.";
            }
        }

        if (!string.IsNullOrWhiteSpace(defaultValueTypeId) &&
            !uniqueValueTypeIds.Contains(defaultValueTypeId))
        {
            return $"Plugin '{pluginId}' default value type '{defaultValueTypeId}' is not present in SupportedValueTypes.";
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
