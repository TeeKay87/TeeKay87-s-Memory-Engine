using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.Core.Plugins;
using TeeKay87.MemoryEngine.Core.Scanning;
using TeeKay87.MemoryEngine.Platform.Mock;
using TeeKay87.MemoryEngine.Platform.PS5;
using TeeKay87.MemoryEngine.PluginSdk;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Tests;

internal static class Program
{
    private static readonly List<(string Name, Func<Task> Test)> Tests = new()
    {
        ("Plugin API and independent plugin versions", VerifyPluginVersioningAsync),
        ("Mock plugin metadata and capabilities", VerifyMockPluginMetadataAsync),
        ("Mock target process and memory map", VerifyMockTargetProcessAndMemoryMapAsync),
        ("Mock target memory read and write", VerifyMockTargetMemoryReadWriteAsync),
        ("Shared 4-byte exact scanner and refinement", VerifySharedScannerAsync),
        ("Shared exact scanner supports all ps5debug value types", VerifySharedValueTypesAsync),
        ("PS5 plugin metadata and connection settings", VerifyPs5PluginMetadataAsync),
        ("PS5 ps5debug-NG connection handshake", VerifyPs5ConnectionHandshakeAsync),
        ("PS5 process enumeration protocol", VerifyPs5ProcessEnumerationAsync),
        ("PS5 memory-map enumeration protocol", VerifyPs5MemoryMapEnumerationAsync),
        ("PS5 raw memory-read protocol", VerifyPs5MemoryReadAsync),
        ("PS5 scan cancellation preserves command stream", VerifyPs5ScanCancellationAsync),
        ("PS5 native exact-value scan protocol", VerifyPs5NativeValueScanAsync),
        ("PS5 native value-type mapping protocol", VerifyPs5NativeValueTypesAsync),
        ("PS5 native exact-value refinement protocol", VerifyPs5NativeValueRefinementAsync),
        ("PS5 native scan cancellation preserves command stream", VerifyPs5NativeScanCancellationAsync),
        ("PS5 process suspend and resume protocol", VerifyPs5ProcessControlAsync),
        ("PS5 raw memory-write and read-back protocol", VerifyPs5MemoryWriteAsync),
        ("Plugin host assembly discovery", VerifyPluginHostDiscoveryAsync)
    };

    public static async Task<int> Main()
    {
        int failures = 0;

        Console.WriteLine("TeeKay87's Memory Engine verification");
        Console.WriteLine(new string('=', 42));

        foreach ((string name, Func<Task> test) in Tests)
        {
            try
            {
                await test().ConfigureAwait(false);
                Console.WriteLine($"PASS  {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.WriteLine($"FAIL  {name}");
                Console.WriteLine($"      {exception.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? $"All {Tests.Count} checks passed."
            : $"{failures} of {Tests.Count} checks failed.");

        return failures == 0 ? 0 : 1;
    }

    private static Task VerifyPluginVersioningAsync()
    {
        MockTargetPlugin mock = new();
        Ps5TargetPlugin ps5 = new();

        AssertEqual(new Version(1, 2, 0), PluginApiInfo.CurrentVersion, "Unexpected Plugin API version.");
        AssertTrue(PluginApiInfo.IsCompatible(mock.Metadata.ApiVersion), "Mock plugin API version is incompatible.");
        AssertTrue(PluginApiInfo.IsCompatible(ps5.Metadata.ApiVersion), "PS5 plugin API version is incompatible.");
        AssertEqual("1.0.0.rev1", mock.Metadata.DisplayVersion, "Unexpected mock plugin display version.");
        AssertEqual("0.1.0.rev9", ps5.Metadata.DisplayVersion, "Unexpected PS5 plugin display version.");

        return Task.CompletedTask;
    }

    private static Task VerifyMockPluginMetadataAsync()
    {
        MockTargetPlugin plugin = new();

        AssertEqual(MockPluginInfo.Id, plugin.Metadata.Id, "Unexpected plugin id.");
        AssertEqual("Development", plugin.Metadata.Platform, "Unexpected platform name.");
        AssertEqual(CpuArchitecture.X64, plugin.Metadata.Architecture.Cpu, "Unexpected CPU architecture.");
        AssertEqual(0, plugin.ConnectionSettings.Count, "Mock plugin should not require connection settings.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.MemoryRead), "MemoryRead capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.MemoryWrite), "MemoryWrite capability is missing.");
        AssertFalse(plugin.Capabilities.HasFlag(TargetCapabilities.Debugger), "Mock plugin should not advertise debugger support.");

        return Task.CompletedTask;
    }

    private static async Task VerifyMockTargetProcessAndMemoryMapAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        IProcessProvider processProvider = session.GetRequiredService<IProcessProvider>();
        IForegroundProcessProvider foregroundProvider = session.GetRequiredService<IForegroundProcessProvider>();
        IMemoryMapProvider memoryMapProvider = session.GetRequiredService<IMemoryMapProvider>();

        IReadOnlyList<TargetProcess> processes = await processProvider
            .GetProcessesAsync(CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess? foreground = await foregroundProvider
            .GetForegroundProcessAsync(CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(1, processes.Count, "Mock target should expose exactly one process.");
        AssertEqual(MockTargetLayout.ProcessId, processes[0].Id, "Unexpected mock process id.");
        AssertTrue(foreground is not null, "Mock foreground process was not returned.");
        AssertEqual(MockTargetLayout.ProcessId, foreground!.Id, "Unexpected foreground process id.");

        IReadOnlyList<MemoryRegion> regions = await memoryMapProvider
            .GetMemoryRegionsAsync(processes[0], CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(1, regions.Count, "Mock target should expose exactly one memory region.");
        AssertEqual(MockTargetLayout.BaseAddress, regions[0].BaseAddress, "Unexpected region base address.");
        AssertEqual((ulong)MockTargetLayout.MemorySize, regions[0].Size, "Unexpected region size.");
    }

    private static async Task VerifyMockTargetMemoryReadWriteAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single();

        IMemoryReader reader = session.GetRequiredService<IMemoryReader>();
        IMemoryWriter writer = session.GetRequiredService<IMemoryWriter>();

        byte[] healthBytes = new byte[sizeof(int)];
        int bytesRead = await reader
            .ReadAsync(process, MockTargetLayout.HealthAddress, healthBytes, CancellationToken.None)
            .ConfigureAwait(false);
        float health = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(healthBytes));

        AssertEqual(sizeof(int), bytesRead, "Health read returned an unexpected byte count.");
        AssertEqual(100.0f, health, "Unexpected initial health value.");

        byte[] newMoneyBytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(newMoneyBytes, 9999999);

        await writer
            .WriteAsync(process, MockTargetLayout.MoneyAddress, newMoneyBytes, CancellationToken.None)
            .ConfigureAwait(false);

        byte[] verifyBytes = new byte[sizeof(int)];
        await reader
            .ReadAsync(process, MockTargetLayout.MoneyAddress, verifyBytes, CancellationToken.None)
            .ConfigureAwait(false);

        int money = BinaryPrimitives.ReadInt32LittleEndian(verifyBytes);
        AssertEqual(9999999, money, "Memory write was not preserved by the mock target.");
    }

    private static async Task VerifySharedScannerAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single();

        IReadOnlyList<MemoryRegion> regions = await session
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        IMemoryReader reader = session.GetRequiredService<IMemoryReader>();
        IMemoryWriter writer = session.GetRequiredService<IMemoryWriter>();
        MemoryScanner scanner = new();

        MemoryScanExecutionResult firstScan = await scanner
            .FirstScanInt32ExactAsync(
                process,
                regions,
                reader,
                session.Architecture,
                30,
                progress: null,
                CancellationToken.None)
            .ConfigureAwait(false);

        MemoryScanResult ammo = firstScan.Results.Single(result => result.Address == MockTargetLayout.AmmoAddress);
        AssertEqual("30", ammo.CurrentValue.DisplayText, "First Scan did not return the expected mock ammo value.");
        AssertTrue(ammo.PreviousValue is null, "First Scan should not assign a previous value.");

        byte[] updatedAmmo = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(updatedAmmo, 25);
        await writer
            .WriteAsync(process, MockTargetLayout.AmmoAddress, updatedAmmo, CancellationToken.None)
            .ConfigureAwait(false);

        MemoryScanExecutionResult nextScan = await scanner
            .NextScanInt32ExactAsync(
                process,
                regions,
                firstScan.Results,
                reader,
                session.Architecture,
                25,
                progress: null,
                CancellationToken.None)
            .ConfigureAwait(false);

        MemoryScanResult refinedAmmo = nextScan.Results.Single();
        AssertEqual(MockTargetLayout.AmmoAddress, refinedAmmo.Address, "Next Scan retained the wrong address.");
        AssertEqual("25", refinedAmmo.CurrentValue.DisplayText, "Next Scan did not read the updated value.");
        AssertEqual("30", refinedAmmo.PreviousValue?.DisplayText, "Next Scan did not preserve the previous value.");
        AssertEqual(0, firstScan.ReadFailureCount, "First Scan reported an unexpected memory-read failure.");
        AssertEqual(0, nextScan.ReadFailureCount, "Next Scan reported an unexpected memory-read failure.");
    }

    private static async Task VerifySharedValueTypesAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single();
        IReadOnlyList<MemoryRegion> regions = await session
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        IMemoryReader reader = session.GetRequiredService<IMemoryReader>();
        IMemoryWriter writer = session.GetRequiredService<IMemoryWriter>();
        MemoryScanner scanner = new();

        (MemoryValueType Type, string Text)[] fixtures =
        {
            (MemoryValueType.UInt8, "250"),
            (MemoryValueType.Int8, "-100"),
            (MemoryValueType.UInt16, "60000"),
            (MemoryValueType.Int16, "-12345"),
            (MemoryValueType.UInt32, "4000000000"),
            (MemoryValueType.Int32, "-123456789"),
            (MemoryValueType.UInt64, "18364758544493064720"),
            (MemoryValueType.Int64, "-1234567890123456789"),
            (MemoryValueType.Float32, "123.25"),
            (MemoryValueType.Float64, "-9876.5"),
            (MemoryValueType.ByteArray, "DE AD BE EF 01")
        };

        for (int index = 0; index < fixtures.Length; index++)
        {
            (MemoryValueType type, string text) = fixtures[index];
            AssertTrue(
                MemoryScanValueCodec.TryParse(text, type, session.Architecture, out MemoryScanValue? value, out string error) && value is not null,
                $"Could not parse {type} test value: {error}");

            ulong address = MockTargetLayout.BaseAddress + 0x2000UL + checked((ulong)(index * 0x100));
            await writer.WriteAsync(process, address, value!.Bytes, CancellationToken.None).ConfigureAwait(false);

            MemoryScanExecutionResult result = await scanner
                .FirstScanExactAsync(
                    process,
                    regions,
                    reader,
                    session.Architecture,
                    value,
                    progress: null,
                    CancellationToken.None)
                .ConfigureAwait(false);

            MemoryScanResult match = result.Results.Single(item => item.Address == address);
            AssertEqual(type, match.ValueType, $"Shared scanner returned the wrong type for {type}.");
            AssertEqual(value.DisplayText, match.CurrentValue.DisplayText, $"Shared scanner returned the wrong value for {type}.");
        }
    }

    private static Task VerifyPs5PluginMetadataAsync()
    {
        Ps5TargetPlugin plugin = new();

        AssertEqual(Ps5PluginInfo.Id, plugin.Metadata.Id, "Unexpected PS5 plugin id.");
        AssertEqual("PlayStation 5", plugin.Metadata.Platform, "Unexpected PS5 platform name.");
        AssertEqual("ps5debug-NG", plugin.Metadata.Backend, "Unexpected PS5 backend name.");
        AssertEqual(
            TargetCapabilities.Connect |
            TargetCapabilities.ProcessEnumeration |
            TargetCapabilities.ForegroundProcess |
            TargetCapabilities.MemoryRegionEnumeration |
            TargetCapabilities.MemoryRead |
            TargetCapabilities.MemoryWrite |
            TargetCapabilities.ProcessSuspend |
            TargetCapabilities.ProcessResume |
            TargetCapabilities.NativeValueScanning,
            plugin.Capabilities,
            "PS5 plugin should advertise its implemented connection, memory access, native scan, and process-control support.");
        AssertEqual(2, plugin.ConnectionSettings.Count, "PS5 plugin should define host and port settings.");
        AssertTrue(plugin.ConnectionSettings.Any(setting => setting.Key == "host" && setting.IsRequired), "PS5 host setting is missing.");
        AssertTrue(
            plugin.ConnectionSettings.Any(setting => setting.Key == "port" && setting.DefaultValue == "744"),
            "PS5 port setting is missing or has the wrong default.");

        return Task.CompletedTask;
    }

    private static async Task VerifyPs5ConnectionHandshakeAsync()
    {
        await using Ps5ProtocolTestServer server = new();
        Ps5TargetPlugin plugin = new();

        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        AssertTrue(session.IsConnected, "PS5 session did not report a connected state after the protocol handshake.");
        AssertTrue(session.GetService<IProcessProvider>() is not null, "PS5 process service was not exposed after connection.");
        AssertTrue(session.GetService<IMemoryMapProvider>() is not null, "PS5 memory-map service was not exposed after connection.");
        AssertTrue(session.GetService<IMemoryReader>() is not null, "PS5 memory-read service was not exposed after connection.");
        AssertTrue(session.GetService<IMemoryWriter>() is not null, "PS5 memory-write service was not exposed after connection.");
        AssertTrue(session.GetService<INativeValueScanner>() is null, "PS5 native value-scanner service should be hidden when the server does not advertise TurboScan support.");
        AssertTrue(session.GetService<INativeValueScanRefiner>() is null, "PS5 native refinement service should be hidden when the server does not advertise TurboScan support.");
        AssertTrue(session.GetService<IForegroundProcessProvider>() is not null, "PS5 preferred-process service was not exposed after connection.");
        AssertTrue(session.GetService<IProcessControl>() is not null, "PS5 process-control service was not exposed after connection.");

        await server.Completion.ConfigureAwait(false);
        await session.DisposeAsync().ConfigureAwait(false);

        AssertFalse(session.IsConnected, "PS5 session remained connected after disposal.");
    }

    private static async Task VerifyPs5ProcessEnumerationAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveProcessList: true);
        Ps5TargetPlugin plugin = new();

        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        IProcessProvider processProvider = session.GetRequiredService<IProcessProvider>();
        IReadOnlyList<TargetProcess> processes = await processProvider
            .GetProcessesAsync(CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(3, processes.Count, "Unexpected PS5 process count.");
        AssertEqual((ulong)101, processes[0].Id, "Unexpected first PS5 process id.");
        AssertEqual("SceShellCore", processes[0].Name, "Unexpected first PS5 process name.");
        AssertEqual((ulong)2222, processes[1].Id, "Unexpected game process id.");
        AssertEqual("eboot.bin", processes[1].Name, "Unexpected game process name.");
        AssertEqual((ulong)3333, processes[2].Id, "Unexpected third PS5 process id.");
        AssertEqual("WebProcess", processes[2].Name, "Unexpected third PS5 process name.");

        TargetProcess? preferredProcess = await session
            .GetRequiredService<IForegroundProcessProvider>()
            .GetForegroundProcessAsync(CancellationToken.None)
            .ConfigureAwait(false);
        AssertTrue(preferredProcess is not null, "PS5 preferred process was not returned.");
        AssertEqual((ulong)2222, preferredProcess!.Id, "PS5 preferred process should select eboot.bin when present.");
        AssertEqual("eboot.bin", preferredProcess.Name, "PS5 preferred process should be eboot.bin.");

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5MemoryMapEnumerationAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveProcessList: true, serveMemoryMap: true);
        Ps5TargetPlugin plugin = new();

        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single(item => item.Id == 2222);

        IReadOnlyList<MemoryRegion> regions = await session
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(3, regions.Count, "Unexpected PS5 memory-region count.");

        AssertEqual((ulong)0x0000000100000000, regions[0].BaseAddress, "Unexpected first PS5 region base address.");
        AssertEqual((ulong)0x10000, regions[0].Size, "Unexpected first PS5 region size.");
        AssertEqual("eboot.bin", regions[0].Name, "Unexpected first PS5 region name.");
        AssertEqual(
            MemoryProtection.Read | MemoryProtection.Execute,
            regions[0].Protection,
            "Unexpected first PS5 region protection.");

        AssertEqual((ulong)0x0000000200000000, regions[1].BaseAddress, "Unexpected second PS5 region base address.");
        AssertEqual((ulong)0x20000, regions[1].Size, "Unexpected second PS5 region size.");
        AssertEqual("data", regions[1].Name, "Unexpected second PS5 region name.");
        AssertEqual(
            MemoryProtection.Read | MemoryProtection.Write,
            regions[1].Protection,
            "Unexpected second PS5 region protection.");

        AssertEqual((ulong)0x0000000300000000, regions[2].BaseAddress, "Unexpected third PS5 region base address.");
        AssertEqual((ulong)0x1000, regions[2].Size, "Unexpected third PS5 region size.");
        AssertEqual<string?>(null, regions[2].Name, "Unnamed PS5 regions should remain unnamed.");
        AssertEqual(MemoryProtection.Read, regions[2].Protection, "Unexpected third PS5 region protection.");

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5MemoryReadAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveMemoryRead: true);
        Ps5TargetPlugin plugin = new();

        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single(item => item.Id == 2222);

        IReadOnlyList<MemoryRegion> regions = await session
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        ulong address = checked(regions[0].BaseAddress + 0x20);
        byte[] buffer = new byte[64];
        int bytesRead = await session
            .GetRequiredService<IMemoryReader>()
            .ReadAsync(process, address, buffer, CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(buffer.Length, bytesRead, "PS5 memory read returned an unexpected byte count.");
        for (int index = 0; index < buffer.Length; index++)
        {
            byte expected = checked((byte)((address + (ulong)index) & 0xFF));
            AssertEqual(expected, buffer[index], $"Unexpected PS5 memory byte at offset {index}.");
        }

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5ScanCancellationAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryRead: true,
            serveFollowUpProcessList: true,
            memoryReadDelayMilliseconds: 100);
        Ps5TargetPlugin plugin = new();

        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        IProcessProvider processProvider = session.GetRequiredService<IProcessProvider>();
        TargetProcess process = (await processProvider
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single(item => item.Id == 2222);

        MemoryRegion[] regions =
        {
            new(
                0x0000000100000000,
                checked((ulong)MemoryScanner.DefaultChunkSize * 2),
                MemoryProtection.Read,
                "cancellation-test")
        };

        using CancellationTokenSource cancellation = new();
        MemoryScanner scanner = new();
        Task<MemoryScanExecutionResult> scanTask = scanner.FirstScanInt32ExactAsync(
            process,
            regions,
            session.GetRequiredService<IMemoryReader>(),
            session.Architecture,
            int.MinValue,
            progress: null,
            cancellation.Token);

        await server.MemoryReadRequestReceived.ConfigureAwait(false);
        cancellation.Cancel();

        bool cancelled = false;
        try
        {
            await scanTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        AssertTrue(cancelled, "The shared scanner did not observe cancellation after the in-flight PS5 read completed.");

        IReadOnlyList<TargetProcess> processesAfterCancellation = await processProvider
            .GetProcessesAsync(CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(3, processesAfterCancellation.Count, "The PS5 command stream was not reusable after scan cancellation.");
        AssertEqual("eboot.bin", processesAfterCancellation[1].Name, "The follow-up PS5 process list was corrupted after cancellation.");

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5NativeValueScanAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true);
        Ps5TargetPlugin plugin = new();

        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single(item => item.Id == 2222);

        IReadOnlyList<MemoryRegion> regions = await session
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        AssertTrue(session.GetService<INativeValueScanner>() is not null, "PS5 TurboScan service was not exposed after capability probing.");

        MemoryScanner scanner = new();
        MemoryScanExecutionResult result = await scanner
            .FirstScanInt32ExactNativeAsync(
                process,
                regions,
                session.GetRequiredService<INativeValueScanner>(),
                session.Architecture,
                10002,
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(2, result.Results.Count, "Native PS5 scan did not retain the expected aligned mapped results.");
        AssertEqual((ulong)0x0000000200000040, result.Results[0].Address, "Unexpected first native PS5 scan address.");
        AssertEqual((ulong)0x0000000200000080, result.Results[1].Address, "Unexpected second native PS5 scan address.");
        AssertTrue(result.Results.All(item => item.CurrentValue.DisplayText == "10002"), "Native PS5 scan did not preserve the requested Int32 value.");
        AssertEqual(0, result.ReadFailureCount, "Native PS5 scan should not report host memory-read failures.");

        await session
            .GetRequiredService<INativeValueScanRefiner>()
            .ResetAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5NativeValueTypesAsync()
    {
        TargetArchitecture ps5Architecture = new(CpuArchitecture.X64, 64, 64, Endianness.Little);
        (MemoryValueType Type, string Text, byte WireType)[] fixtures =
        {
            (MemoryValueType.UInt8, "250", 0),
            (MemoryValueType.Int8, "-100", 1),
            (MemoryValueType.UInt16, "60000", 2),
            (MemoryValueType.Int16, "-12345", 3),
            (MemoryValueType.UInt32, "4000000000", 4),
            (MemoryValueType.Int32, "-123456789", 5),
            (MemoryValueType.UInt64, "18364758544493064720", 6),
            (MemoryValueType.Int64, "-1234567890123456789", 7),
            (MemoryValueType.Float32, "123.25", 8),
            (MemoryValueType.Float64, "-9876.5", 9),
            (MemoryValueType.ByteArray, "DE AD BE EF 01", 10)
        };

        foreach ((MemoryValueType type, string text, byte wireType) in fixtures)
        {
            AssertTrue(
                MemoryScanValueCodec.TryParse(text, type, ps5Architecture, out MemoryScanValue? value, out string error) && value is not null,
                $"Could not parse PS5 {type} fixture: {error}");

            await using Ps5ProtocolTestServer server = new(
                serveProcessList: true,
                serveMemoryMap: true,
                serveNativeScan: true,
                nativeValueType: wireType,
                nativeAlignment: checked((byte)value!.Alignment),
                nativeValueData: value.Bytes.ToArray());
            Ps5TargetPlugin plugin = new();
            TargetConnectionOptions options = new(new[]
            {
                new KeyValuePair<string, string>("host", "127.0.0.1"),
                new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
            });

            await using ITargetSession session = await plugin
                .ConnectAsync(options, CancellationToken.None)
                .ConfigureAwait(false);
            TargetProcess process = (await session
                    .GetRequiredService<IProcessProvider>()
                    .GetProcessesAsync(CancellationToken.None)
                    .ConfigureAwait(false))
                .Single(item => item.Id == 2222);
            IReadOnlyList<MemoryRegion> regions = await session
                .GetRequiredService<IMemoryMapProvider>()
                .GetMemoryRegionsAsync(process, CancellationToken.None)
                .ConfigureAwait(false);

            MemoryScanExecutionResult result = await new MemoryScanner()
                .FirstScanExactNativeAsync(
                    process,
                    regions,
                    session.GetRequiredService<INativeValueScanner>(),
                    value,
                    CancellationToken.None)
                .ConfigureAwait(false);

            AssertEqual(2, result.Results.Count, $"PS5 native {type} scan returned an unexpected result count.");
            AssertTrue(result.Results.All(item => item.ValueType == type), $"PS5 native {type} scan returned the wrong value type.");
            AssertTrue(result.Results.All(item => item.CurrentValue.DisplayText == value.DisplayText), $"PS5 native {type} scan returned the wrong display value.");

            INativeValueScanRefiner refiner = session.GetRequiredService<INativeValueScanRefiner>();
            if (type is MemoryValueType.Float32 or MemoryValueType.Float64)
            {
                bool strictExactFallbackRequested = false;
                try
                {
                    await refiner
                        .RefineAsync(
                            process,
                            result.Results.Select(item => item.Address).ToArray(),
                            new NativeValueScanRequest(
                                type,
                                ValueScanComparison.ExactValue,
                                value.Bytes.Span,
                                value.Alignment),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (NotSupportedException)
                {
                    strictExactFallbackRequested = true;
                }

                AssertTrue(
                    strictExactFallbackRequested,
                    $"PS5 native {type} refinement must request shared-Core fallback to preserve strict Exact Value semantics.");
            }
            else
            {
                await refiner.ResetAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await server.Completion.ConfigureAwait(false);
        }
    }

    private static async Task VerifyPs5NativeValueRefinementAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true,
            serveNativeRefinement: true);
        Ps5TargetPlugin plugin = new();

        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single(item => item.Id == 2222);

        IReadOnlyList<MemoryRegion> regions = await session
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        MemoryScanner scanner = new();
        MemoryScanExecutionResult firstScan = await scanner
            .FirstScanInt32ExactNativeAsync(
                process,
                regions,
                session.GetRequiredService<INativeValueScanner>(),
                session.Architecture,
                10002,
                CancellationToken.None)
            .ConfigureAwait(false);

        MemoryScanExecutionResult nextScan = await scanner
            .NextScanInt32ExactNativeAsync(
                process,
                regions,
                firstScan.Results,
                session.GetRequiredService<INativeValueScanRefiner>(),
                session.Architecture,
                10001,
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(1, nextScan.Results.Count, "Native PS5 refinement did not return the expected survivor count.");
        AssertEqual((ulong)0x0000000200000080, nextScan.Results[0].Address, "Native PS5 refinement retained the wrong address.");
        AssertEqual("10001", nextScan.Results[0].CurrentValue.DisplayText, "Native PS5 refinement did not preserve the requested current value.");
        AssertEqual("10002", nextScan.Results[0].PreviousValue?.DisplayText, "Native PS5 refinement did not preserve the host-side previous value.");

        await session
            .GetRequiredService<INativeValueScanRefiner>()
            .ResetAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5NativeScanCancellationAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true,
            serveFollowUpProcessList: true,
            nativeScanDelayMilliseconds: 100);
        Ps5TargetPlugin plugin = new();

        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        IProcessProvider processProvider = session.GetRequiredService<IProcessProvider>();
        TargetProcess process = (await processProvider
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single(item => item.Id == 2222);

        IReadOnlyList<MemoryRegion> regions = await session
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        byte[] valueBytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(valueBytes, 10002);
        NativeValueScanRequest request = new(
            MemoryValueType.Int32,
            ValueScanComparison.ExactValue,
            valueBytes,
            alignment: sizeof(int));

        using CancellationTokenSource cancellation = new();
        Task<IReadOnlyList<ulong>> scanTask = session
            .GetRequiredService<INativeValueScanner>()
            .ScanAsync(process, regions, request, cancellation.Token);

        await server.NativeScanRequestReceived.ConfigureAwait(false);
        cancellation.Cancel();

        bool cancelled = false;
        try
        {
            await scanTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        AssertTrue(cancelled, "The PS5 native scanner did not observe cancellation after draining the in-flight scan response.");

        IReadOnlyList<TargetProcess> processesAfterCancellation = await processProvider
            .GetProcessesAsync(CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(3, processesAfterCancellation.Count, "The PS5 command stream was not reusable after native scan cancellation.");
        AssertEqual("eboot.bin", processesAfterCancellation[1].Name, "The follow-up process list was corrupted after native scan cancellation.");

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5ProcessControlAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveProcessControl: true);
        Ps5TargetPlugin plugin = new();

        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single(item => item.Id == 2222);

        IProcessControl control = session.GetRequiredService<IProcessControl>();
        await control.SuspendAsync(process, CancellationToken.None).ConfigureAwait(false);
        await control.ResumeAsync(process, CancellationToken.None).ConfigureAwait(false);

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5MemoryWriteAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveMemoryRead: true,
            serveMemoryWrite: true);
        Ps5TargetPlugin plugin = new();

        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single(item => item.Id == 2222);

        IReadOnlyList<MemoryRegion> regions = await session
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        MemoryRegion writableRegion = regions.Single(region =>
            region.Protection.HasFlag(MemoryProtection.Read) &&
            region.Protection.HasFlag(MemoryProtection.Write));

        ulong address = checked(writableRegion.BaseAddress + 0x40);
        byte[] requested = { 0xDE, 0xAD, 0xBE, 0xEF, 0x12, 0x34, 0x56, 0x78 };

        await session
            .GetRequiredService<IMemoryWriter>()
            .WriteAsync(process, address, requested, CancellationToken.None)
            .ConfigureAwait(false);

        byte[] readBack = new byte[requested.Length];
        int bytesRead = await session
            .GetRequiredService<IMemoryReader>()
            .ReadAsync(process, address, readBack, CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(requested.Length, bytesRead, "PS5 memory write read-back returned an unexpected byte count.");
        AssertTrue(requested.SequenceEqual(readBack), "PS5 memory write was not preserved by the protocol fixture.");

        await server.Completion.ConfigureAwait(false);
    }

    private static Task VerifyPluginHostDiscoveryAsync()
    {
        string pluginAssemblyPath = typeof(MockTargetPlugin).Assembly.Location;
        string pluginDirectory = Path.GetDirectoryName(pluginAssemblyPath)
            ?? throw new InvalidOperationException("Could not resolve the plugin output directory.");

        using PluginHost host = new();
        PluginDiscoveryResult result = host.Discover(pluginDirectory);

        DiscoveredPlugin? mock = result.Plugins
            .SingleOrDefault(plugin => plugin.Instance.Metadata.Id == MockPluginInfo.Id);
        DiscoveredPlugin? ps5 = result.Plugins
            .SingleOrDefault(plugin => plugin.Instance.Metadata.Id == Ps5PluginInfo.Id);

        AssertTrue(mock is not null, "PluginHost did not discover the mock plugin assembly.");
        AssertTrue(ps5 is not null, "PluginHost did not discover the PS5 plugin assembly.");
        AssertEqual(0, result.Errors.Count, "Plugin discovery reported unexpected errors.");

        return Task.CompletedTask;
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; actual: {actual}.");
        }
    }
}
