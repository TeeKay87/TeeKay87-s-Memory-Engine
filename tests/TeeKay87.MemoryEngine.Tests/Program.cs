using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.Core.Plugins;
using TeeKay87.MemoryEngine.Platform.Mock;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.Tests;

internal static class Program
{
    private static readonly List<(string Name, Func<Task> Test)> Tests = new()
    {
        ("Mock plugin metadata and capabilities", VerifyMockPluginMetadataAsync),
        ("Mock target process and memory map", VerifyMockTargetProcessAndMemoryMapAsync),
        ("Mock target memory read and write", VerifyMockTargetMemoryReadWriteAsync),
        ("Plugin host assembly discovery", VerifyPluginHostDiscoveryAsync)
    };

    public static async Task<int> Main()
    {
        int failures = 0;

        Console.WriteLine("TeeKay87's Memory Engine foundation verification");
        Console.WriteLine(new string('=', 54));

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
            ? $"All {Tests.Count} foundation checks passed."
            : $"{failures} of {Tests.Count} foundation checks failed.");

        return failures == 0 ? 0 : 1;
    }

    private static Task VerifyMockPluginMetadataAsync()
    {
        MockTargetPlugin plugin = new();

        AssertEqual("platform.mock.in-memory", plugin.Metadata.Id, "Unexpected plugin id.");
        AssertEqual("Development", plugin.Metadata.Platform, "Unexpected platform name.");
        AssertEqual(CpuArchitecture.X64, plugin.Metadata.Architecture.Cpu, "Unexpected CPU architecture.");
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

    private static Task VerifyPluginHostDiscoveryAsync()
    {
        string pluginAssemblyPath = typeof(MockTargetPlugin).Assembly.Location;
        string pluginDirectory = Path.GetDirectoryName(pluginAssemblyPath)
            ?? throw new InvalidOperationException("Could not resolve the mock plugin output directory.");

        using PluginHost host = new();
        PluginDiscoveryResult result = host.Discover(pluginDirectory);

        DiscoveredPlugin? mock = result.Plugins
            .SingleOrDefault(plugin => plugin.Instance.Metadata.Id == "platform.mock.in-memory");

        AssertTrue(mock is not null, "PluginHost did not discover the mock plugin assembly.");
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
