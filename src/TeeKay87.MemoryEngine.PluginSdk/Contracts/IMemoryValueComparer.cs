using System;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

/// <summary>
/// Optional companion contract for Value Types that support ordered and delta comparisons.
/// Core-owned Scan Types use this contract for Bigger/Smaller/Between and changed-by scans.
/// </summary>
public interface IMemoryValueComparer
{
    bool TryCompare(
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right,
        TargetArchitecture architecture,
        out int comparison);

    bool IsIncreasedBy(
        ReadOnlySpan<byte> current,
        ReadOnlySpan<byte> previous,
        ReadOnlySpan<byte> amount,
        TargetArchitecture architecture);

    bool IsDecreasedBy(
        ReadOnlySpan<byte> current,
        ReadOnlySpan<byte> previous,
        ReadOnlySpan<byte> amount,
        TargetArchitecture architecture);
}
