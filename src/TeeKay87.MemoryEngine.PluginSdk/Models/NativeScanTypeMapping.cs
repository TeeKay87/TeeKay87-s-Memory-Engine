using System;
using System.Collections.Generic;
using System.Linq;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

/// <summary>
/// Declares that one Core Scan Type has a semantically equivalent native implementation
/// in a target plugin for the listed stages and Value Types.
/// </summary>
public sealed class NativeScanTypeMapping
{
    private readonly IReadOnlyList<string> _supportedValueTypeIds;

    public NativeScanTypeMapping(
        string coreScanTypeId,
        string nativeScanTypeId,
        bool availableForFirstScan,
        bool availableForNextScan,
        IEnumerable<string>? supportedValueTypeIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coreScanTypeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeScanTypeId);

        if (!availableForFirstScan && !availableForNextScan)
        {
            throw new ArgumentException("A native Scan Type mapping must be available for at least one scan stage.");
        }

        CoreScanTypeId = coreScanTypeId;
        NativeScanTypeId = nativeScanTypeId;
        AvailableForFirstScan = availableForFirstScan;
        AvailableForNextScan = availableForNextScan;

        _supportedValueTypeIds = Array.AsReadOnly((supportedValueTypeIds ?? Array.Empty<string>())
            .Select(id =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(id);
                return id;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    /// <summary>
    /// Stable Core-owned Scan Type id, for example standard.exact-value.
    /// </summary>
    public string CoreScanTypeId { get; }

    /// <summary>
    /// Opaque plugin-owned native identifier. Core and the host never interpret this value.
    /// </summary>
    public string NativeScanTypeId { get; }

    public bool AvailableForFirstScan { get; }

    public bool AvailableForNextScan { get; }

    /// <summary>
    /// Optional Value Type restriction. An empty collection means the mapping itself does not
    /// restrict Value Types; the native scanner may still reject a concrete shape or option set.
    /// </summary>
    public IReadOnlyList<string> SupportedValueTypeIds => _supportedValueTypeIds;

    public bool Supports(string coreScanTypeId, string valueTypeId, MemoryScanStage stage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coreScanTypeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueTypeId);

        if (!string.Equals(CoreScanTypeId, coreScanTypeId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        bool stageSupported = stage switch
        {
            MemoryScanStage.FirstScan => AvailableForFirstScan,
            MemoryScanStage.NextScan => AvailableForNextScan,
            _ => false
        };

        if (!stageSupported)
        {
            return false;
        }

        return _supportedValueTypeIds.Count == 0 ||
               _supportedValueTypeIds.Any(id => string.Equals(id, valueTypeId, StringComparison.OrdinalIgnoreCase));
    }
}
