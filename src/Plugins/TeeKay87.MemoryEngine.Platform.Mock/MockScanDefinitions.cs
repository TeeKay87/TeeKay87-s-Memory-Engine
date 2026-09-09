using System;
using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Scanning;

namespace TeeKay87.MemoryEngine.Platform.Mock;

internal static class MockScanDefinitions
{
    public static IReadOnlyList<IMemoryValueType> ValueTypes { get; } = Array.AsReadOnly(new[]
    {
        StandardMemoryValueTypes.UInt8,
        StandardMemoryValueTypes.Int8,
        StandardMemoryValueTypes.UInt16,
        StandardMemoryValueTypes.Int16,
        StandardMemoryValueTypes.UInt32,
        StandardMemoryValueTypes.Int32,
        StandardMemoryValueTypes.UInt64,
        StandardMemoryValueTypes.Int64,
        StandardMemoryValueTypes.Float32,
        StandardMemoryValueTypes.Float64,
        StandardMemoryValueTypes.ByteArray
    });


    public static string DefaultValueTypeId => StandardMemoryValueTypeIds.Int32;

}
