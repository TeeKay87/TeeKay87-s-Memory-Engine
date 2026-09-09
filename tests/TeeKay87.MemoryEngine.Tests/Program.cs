using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TeeKay87.MemoryEngine.Core.Debugging;
using TeeKay87.MemoryEngine.Core.Diagnostics;
using TeeKay87.MemoryEngine.Core.Disassembly;
using TeeKay87.MemoryEngine.Core.Exporting;
using TeeKay87.MemoryEngine.Core.MemoryViewer;
using TeeKay87.MemoryEngine.Core.Operations;
using TeeKay87.MemoryEngine.Core.Plugins;
using TeeKay87.MemoryEngine.Core.Scanning;
using TeeKay87.MemoryEngine.Core.Scanning.Storage;
using TeeKay87.MemoryEngine.Core.Settings;
using TeeKay87.MemoryEngine.Platform.Mock;
using TeeKay87.MemoryEngine.Platform.PS5;
using TeeKay87.MemoryEngine.PluginSdk;
using TeeKay87.MemoryEngine.PluginSdk.Capabilities;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;
using TeeKay87.MemoryEngine.PluginSdk.Scanning;

namespace TeeKay87.MemoryEngine.Tests;

internal static class Program
{
    private static readonly List<(string Name, Func<Task> Test)> Tests = new()
    {
        ("Plugin API and independent plugin versions", VerifyPluginVersioningAsync),
        ("Debugger neutral model contracts", VerifyDebuggerModelContractsAsync),
        ("Debugger optional service contracts", VerifyDebuggerServiceContractsAsync),
        ("Debugger session target identity", VerifyDebuggerSessionIdentityAsync),
        ("Core debugger session lifecycle and event binding", VerifyDebuggerSessionCoordinatorLifecycleAsync),
        ("Core debugger attach safety and cleanup", VerifyDebuggerSessionCoordinatorSafetyAsync),
        ("Plugin-scoped settings persistence", VerifyPluginSettingsPersistenceAsync),
        ("Scan-result storage path validation", VerifyScanResultStoragePathValidationAsync),
        ("Scan-result application-session isolation", VerifyScanResultApplicationSessionIsolationAsync),
        ("Scan-result stale-session cleanup safety", VerifyScanResultStaleSessionCleanupAsync),
        ("Scan-result scan-session lifecycle", VerifyScanResultSessionLifecycleAsync),
        ("Disk-backed scan-result generation commits", VerifyDiskBackedResultGenerationCommitsAsync),
        ("Disk-backed Next Scan refines hidden results", VerifyDiskBackedNextScanUsesCompleteResultSetAsync),
        ("Disk-backed native stream exceeds legacy two-million limit", VerifyDiskBackedNativeStreamBeyondLegacyLimitAsync),
        ("Generic resident native result contract", VerifyResidentNativeResultContractAsync),
        ("Generic operation progress contract", VerifyOperationProgressContractAsync),
        ("Universal export text formats", VerifyUniversalExportTextFormatsAsync),
        ("Universal export structured JSON", VerifyUniversalExportJsonAsync),
        ("Universal export cancellation is transactional", VerifyUniversalExportCancellationAsync),
        ("Disk-backed Scan Results export exceeds display preview", VerifyDiskBackedScanResultExportAsync),
        ("Resident Scan Results export exceeds display preview", VerifyResidentScanResultExportAsync),
        ("Core-owned scan type catalog", VerifyPluginOwnedScanDefinitionContractsAsync),
        ("Core scan type comparison semantics", VerifyCoreScanTypeSemanticsAsync),
        ("Generic native Scan Type mapping contract", VerifyNativeScanTypeMappingContractAsync),
        ("Core scanner accepts plugin-defined scan definitions", VerifyPluginDefinedScannerAsync),
        ("Plugin value types and Core scan capabilities", VerifyPluginScanCapabilitiesAsync),
        ("Shared scan-option semantics", VerifySharedScanOptionsAsync),
        ("Plugin scan-option presentation and applicability", VerifyPluginScanOptionPresentationAsync),
        ("Mock plugin metadata and capabilities", VerifyMockPluginMetadataAsync),
        ("Mock debugger provider lifecycle", VerifyMockDebuggerProviderAsync),
        ("Mock debugger deterministic pause and continue events", VerifyMockDebuggerEventFlowAsync),
        ("Mock debugger thread enumeration and control", VerifyMockDebuggerThreadServicesAsync),
        ("Mock debugger register snapshots and writes", VerifyMockDebuggerRegisterServicesAsync),
        ("Mock debugger attachment exclusivity and target cleanup", VerifyMockDebuggerAttachmentIsolationAsync),
        ("Debugger workspace command and event source contract", VerifyDebuggerWorkspaceSourceAsync),
        ("Debugger workspace thread panel source contract", VerifyDebuggerThreadWorkspaceSourceAsync),
        ("Debugger workspace register panel source contract", VerifyDebuggerRegisterWorkspaceSourceAsync),
        ("Debugger register value codec source contract", VerifyDebuggerRegisterValueCodecSourceAsync),
        ("Debugger register edit dialog source contract", VerifyRegisterEditDialogSourceAsync),
        ("Debugger current instruction Disassembler integration", VerifyDebuggerCurrentInstructionIntegrationSourceAsync),
        ("Debugger target lifetime and stale-session source contract", VerifyDebuggerTargetLifetimeSourceAsync),
        ("Mock target process and memory map", VerifyMockTargetProcessAndMemoryMapAsync),
        ("Mock target memory read and write", VerifyMockTargetMemoryReadWriteAsync),
        ("Disassembly instruction model", VerifyDisassemblyInstructionModelAsync),
        ("Disassembly syntax token model", VerifyDisassemblySyntaxTokenModelAsync),
        ("Mock disassembly capability and provider", VerifyMockDisassemblyProviderAsync),
        ("Core disassembly bounded read", VerifyDisassemblyBoundedReadAsync),
        ("Core disassembly bidirectional context", VerifyDisassemblyBidirectionalContextAsync),
        ("Core disassembly continuous origin resolution", VerifyDisassemblyContinuousOriginResolutionAsync),
        ("Core disassembly region-boundary handling", VerifyDisassemblyRegionBoundaryAsync),
        ("Core disassembly unreadable-region rejection", VerifyDisassemblyUnreadableRegionRejectionAsync),
        ("Core disassembly instruction-order validation", VerifyDisassemblyInstructionOrderValidationAsync),
        ("Core disassembly cancellation", VerifyDisassemblyCancellationAsync),
        ("Disassembly session target identity", VerifyDisassemblySessionIdentityAsync),
        ("Disassembler direct target navigation contract", VerifyDisassemblerFollowTargetSourceAsync),
        ("Disassembly export source structured data", VerifyDisassemblyExportSourceAsync),
        ("Disassembler selection, copy, and export contract", VerifyDisassemblerSelectionCopyExportSourceAsync),
        ("Disassembler right-click preserves extended selection", VerifyDisassemblerRightClickSelectionSourceAsync),
        ("Disassembler multi-selection copy consistency", VerifyDisassemblerMultiSelectionCopySourceAsync),
        ("Disassembler readable region navigation contract", VerifyDisassemblerRegionNavigationSourceAsync),
        ("Mock custom disassembly architecture", VerifyMockCustomDisassemblyArchitectureAsync),
        ("Memory Viewer bounded readable window", VerifyMemoryViewerBoundedWindowAsync),
        ("Memory Viewer value-span highlighting", VerifyMemoryViewerValueSpanHighlightAsync),
        ("Memory Viewer value-span bindings are OneWay", VerifyMemoryViewerValueSpanBindingsAsync),
        ("Saved Addresses manual-entry toolbar layout", VerifyManualSavedAddressToolbarAsync),
        ("Saved Addresses manual-entry dialog contract", VerifyManualSavedAddressDialogAsync),
        ("Saved Addresses manual-entry address parser out initialization", VerifyManualSavedAddressAddressParserSourceAsync),
        ("Memory Viewer guarded-region rejection", VerifyMemoryViewerGuardedRegionRejectionAsync),
        ("Memory Viewer safe write and stale-data protection", VerifyMemoryViewerSafeWriteAsync),
        ("Memory Viewer write protection rejection", VerifyMemoryViewerWriteProtectionRejectionAsync),
        ("Memory Viewer readable region navigation", VerifyMemoryViewerRegionNavigationAsync),
        ("Shared 4-byte exact scanner and refinement", VerifySharedScannerAsync),
        ("Shared exact scanner supports all ps5debug value types", VerifySharedValueTypesAsync),
        ("Standard Value Type live-input policies", VerifyStandardValueInputPoliciesAsync),
        ("PS5 plugin metadata and connection settings", VerifyPs5PluginMetadataAsync),
        ("PS5 ps5debug-NG connection handshake", VerifyPs5ConnectionHandshakeAsync),
        ("PS5 debugger provider lifecycle and exclusivity", VerifyPs5DebuggerProviderLifecycleAsync),
        ("PS5 debugger attach pause continue and detach protocol", VerifyPs5DebuggerExecutionProtocolAsync),
        ("PS5 debugger asynchronous interrupt mapping", VerifyPs5DebuggerInterruptAsync),
        ("PS5 debugger dedicated transport isolation", VerifyPs5DebuggerTransportIsolationAsync),
        ("PS5 debugger thread enumeration and control protocol", VerifyPs5DebuggerThreadServicesAsync),
        ("PS5 debugger general register snapshot protocol", VerifyPs5DebuggerRegisterServicesAsync),
        ("Main workspace target controls and connection status layout", VerifyMainWorkspaceTargetLayoutAsync),
        ("Main workspace passive memory-map status removal", VerifyMainWorkspaceMemoryMapStatusRemovalAsync),
        ("Main workspace two-row target header standard", VerifyMainWorkspaceTwoRowHeaderAsync),
        ("Main workspace responsive target input widths", VerifyMainWorkspaceResponsiveTopInputWidthAsync),
        ("Shared button content alignment and vertical padding", VerifySharedButtonContentAlignmentAsync),
        ("PS5 x86-64 disassembly decoding", VerifyPs5DisassemblyDecodingAsync),
        ("PS5 x86-64 disassembly safety", VerifyPs5DisassemblySafetyAsync),
        ("PS5 process enumeration protocol", VerifyPs5ProcessEnumerationAsync),
        ("PS5 memory-map enumeration protocol", VerifyPs5MemoryMapEnumerationAsync),
        ("PS5 raw memory-read protocol", VerifyPs5MemoryReadAsync),
        ("PS5 scan cancellation preserves command stream", VerifyPs5ScanCancellationAsync),
        ("PS5 native exact-value scan protocol", VerifyPs5NativeValueScanAsync),
        ("PS5 native Core scan-type mapping protocol", VerifyPs5NativeMappedScanTypesAsync),
        ("PS5 native Unknown Initial snapshot mapping protocol", VerifyPs5NativeUnknownInitialSnapshotAsync),
        ("PS5 resident TurboScan bounded preview and refinement", VerifyPs5ResidentTurboScanAsync),
        ("PS5 native floating Changed/Unchanged bitwise protocol", VerifyPs5FloatingChangedUnchangedBitwiseAsync),
        ("PS5 native snapshot rejection preserves command stream", VerifyPs5NativeSnapshotRejectionRecoveryAsync),
        ("PS5 native custom-alignment protocol", VerifyPs5NativeCustomAlignmentAsync),
        ("PS5 native value-type mapping protocol", VerifyPs5NativeValueTypesAsync),
        ("PS5 native exact-value refinement protocol", VerifyPs5NativeValueRefinementAsync),
        ("PS5 native floating-point tolerance refinement", VerifyPs5NativeFloatingToleranceAsync),
        ("PS5 native scan cancellation preserves command stream", VerifyPs5NativeScanCancellationAsync),
        ("PS5 process suspend and resume protocol", VerifyPs5ProcessControlAsync),
        ("PS5 raw memory-write and read-back protocol", VerifyPs5MemoryWriteAsync),
        ("Plugin host assembly discovery", VerifyPluginHostDiscoveryAsync)
    };

    private static IMemoryScanType ExactScanType => MemoryScanTypeCatalog.All.Single(item =>
        string.Equals(item.Id, StandardMemoryScanTypeIds.ExactValue, StringComparison.OrdinalIgnoreCase));

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

        AssertEqual(new Version(2, 13, 0), PluginApiInfo.CurrentVersion, "Unexpected Plugin API version.");
        AssertTrue(PluginApiInfo.IsCompatible(new Version(2, 12, 0)), "Plugin API 2.13 host rejected an older compatible 2.12 plugin contract.");
        AssertTrue(PluginApiInfo.IsCompatible(mock.Metadata.ApiVersion), "Mock plugin API version is incompatible.");
        AssertTrue(PluginApiInfo.IsCompatible(ps5.Metadata.ApiVersion), "PS5 plugin API version is incompatible.");
        AssertEqual("1.0.0.rev10", mock.Metadata.DisplayVersion, "Unexpected mock plugin display version.");
        AssertEqual(new Version(2, 13, 0), mock.Metadata.ApiVersion, "Unexpected Mock plugin API version.");
        AssertEqual("0.1.0.rev27", ps5.Metadata.DisplayVersion, "Unexpected PS5 plugin display version.");
        AssertEqual(new Version(2, 13, 0), ps5.Metadata.ApiVersion, "Unexpected PS5 plugin API version.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerModelContractsAsync()
    {
        byte[] sourceValue = { 0x34, 0x12 };
        DebuggerRegister register = new(
            "ip",
            "Instruction Pointer",
            bitWidth: 16,
            value: sourceValue,
            group: "General",
            role: DebuggerRegisterRole.InstructionPointer,
            canWrite: true,
            valueEncoding: DebuggerRegisterValueEncoding.UnsignedLittleEndian);

        sourceValue[0] = 0x00;
        AssertEqual(2, register.Value.Length, "Debugger register value width changed unexpectedly.");
        AssertEqual((byte)0x34, register.Value.Span[0], "Debugger register did not retain an independent value snapshot.");
        AssertEqual(DebuggerRegisterRole.InstructionPointer, register.Role, "Debugger register role was not retained.");
        AssertTrue(register.CanWrite, "Debugger register write capability was not retained.");
        AssertEqual(DebuggerRegisterValueEncoding.UnsignedLittleEndian, register.ValueEncoding,
            "Debugger register value encoding was not retained.");

        AssertTrue(
            typeof(DebuggerRegister).GetConstructor(new[]
            {
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(ReadOnlyMemory<byte>),
                typeof(string),
                typeof(DebuggerRegisterRole),
                typeof(bool)
            }) is not null,
            "Plugin API 2.13 removed the existing seven-parameter DebuggerRegister constructor required by older compiled plugins.");

        AssertThrows<ArgumentException>(
            () => _ = new DebuggerRegister("bad", "Bad", 64, new byte[] { 0x01 }),
            "Debugger register accepted a byte payload that did not match its declared bit width.");

        DebuggerBreakpointRequest request = new(
            0x1234,
            size: 4,
            kind: DebuggerBreakpointKind.Hardware,
            access: DebuggerBreakpointAccess.Write,
            isTemporary: true);
        DebuggerBreakpoint breakpoint = new("bp-1", request);
        AssertEqual(0x1234UL, breakpoint.Request.Address, "Debugger breakpoint address changed unexpectedly.");
        AssertTrue(breakpoint.Request.IsTemporary, "Debugger temporary-breakpoint metadata was not retained.");

        DebuggerThreadInfo thread = new(7, "Worker", DebuggerThreadState.Stopped);
        AssertEqual(7UL, thread.Id, "Debugger thread id changed unexpectedly.");
        AssertEqual(DebuggerThreadState.Stopped, thread.State, "Debugger thread state changed unexpectedly.");

        DebuggerStackFrame frame = new(
            index: 0,
            instructionAddress: 0x4000,
            stackPointer: 0x8000,
            framePointer: 0x8100,
            returnAddress: 0x4010,
            moduleName: "game.bin");
        AssertEqual(0x4000UL, frame.InstructionAddress, "Debugger stack-frame instruction address changed unexpectedly.");
        AssertEqual("game.bin", frame.ModuleName, "Debugger stack-frame module name changed unexpectedly.");

        DateTimeOffset timestamp = new(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);
        DebuggerEvent debugEvent = new(
            DebuggerEventKind.Breakpoint,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.Breakpoint,
            threadId: 7,
            instructionPointer: 0x4000,
            message: "Breakpoint hit.",
            timestamp: timestamp);
        AssertEqual(DebuggerStopReason.Breakpoint, debugEvent.StopReason, "Debugger stop reason changed unexpectedly.");
        AssertEqual(timestamp, debugEvent.Timestamp, "Debugger event timestamp changed unexpectedly.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerServiceContractsAsync()
    {
        TargetProcess process = new(99, "debug-target");
        TestDebuggerSession session = new(process, DebuggerExecutionState.Paused);

        IDebuggerThreadService threadService = session.GetRequiredService<IDebuggerThreadService>();
        IDebuggerThreadControlService threadControlService = session.GetRequiredService<IDebuggerThreadControlService>();
        AssertTrue(ReferenceEquals(session, threadService), "Debugger session did not expose its optional thread-enumeration service.");
        AssertTrue(ReferenceEquals(session, threadControlService), "Debugger session did not expose its optional thread-control service.");
        AssertThrows<NotSupportedException>(
            () => session.GetRequiredService<IDebuggerRegisterService>(),
            "Debugger session returned an undeclared optional register service.");

        TargetCapabilities debuggerCapabilities =
            TargetCapabilities.Debugger |
            TargetCapabilities.ThreadEnumeration |
            TargetCapabilities.ThreadControl |
            TargetCapabilities.RegisterAccess |
            TargetCapabilities.Breakpoints |
            TargetCapabilities.Watchpoints |
            TargetCapabilities.CallStack |
            TargetCapabilities.StepExecution;

        MockTargetPlugin mock = new();
        Ps5TargetPlugin ps5 = new();
        TargetCapabilities rev7Capabilities =
            TargetCapabilities.Debugger |
            TargetCapabilities.ThreadEnumeration |
            TargetCapabilities.ThreadControl |
            TargetCapabilities.RegisterAccess;
        AssertEqual(
            rev7Capabilities,
            mock.Capabilities & debuggerCapabilities,
            "Mock plugin did not advertise exactly the debugger/thread/register capabilities implemented in rev7.");
        AssertEqual(
            TargetCapabilities.None,
            mock.Capabilities & (debuggerCapabilities & ~rev7Capabilities),
            "Mock plugin advertised debugger capabilities beyond the rev7 register scope.");
        AssertEqual(
            rev7Capabilities,
            ps5.Capabilities & debuggerCapabilities,
            "PS5 plugin did not advertise exactly the debugger/thread/register capabilities implemented in rev7.");
        AssertEqual(
            TargetCapabilities.None,
            ps5.Capabilities & (debuggerCapabilities & ~rev7Capabilities),
            "PS5 plugin advertised debugger capabilities beyond the rev7 register scope.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerSessionIdentityAsync()
    {
        TargetProcess process = new(0x1234, "eboot.bin", "Game");
        DebuggerSessionIdentity identity = new("platform.test", process, connectionGeneration: 17);

        AssertTrue(
            identity.Matches("platform.test", new TargetProcess(0x1234, "eboot.bin", "Different label"), 17),
            "Debugger identity incorrectly treated display-only process text as session identity.");
        AssertFalse(
            identity.Matches("platform.test", process, 18),
            "Debugger identity accepted a different connection generation.");
        AssertFalse(
            identity.Matches("platform.other", process, 17),
            "Debugger identity accepted a different plugin id.");
        AssertThrows<InvalidOperationException>(
            () => identity.EnsureCurrent("platform.test", new TargetProcess(0x9999, "eboot.bin"), 17),
            "Debugger identity accepted a different process id.");

        return Task.CompletedTask;
    }

    private static async Task VerifyDebuggerSessionCoordinatorLifecycleAsync()
    {
        TargetProcess process = new(123, "eboot.bin");
        DebuggerSessionIdentity identity = new("platform.test", process, connectionGeneration: 4);
        TestDebuggerSession backendSession = new(process, DebuggerExecutionState.Running);
        TestDebuggerProvider provider = new(backendSession);
        await using DebuggerSessionCoordinator coordinator = new(identity, process, provider);

        List<DebuggerSessionState> states = new();
        List<DebuggerEventContext> events = new();
        coordinator.StateChanged += (_, args) => states.Add(args.CurrentState);
        coordinator.EventReceived += (_, args) => events.Add(args.Context);

        await coordinator.AttachAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerSessionState.Running, coordinator.State, "Debugger coordinator did not map the attached running state.");
        AssertTrue(coordinator.IsAttached, "Debugger coordinator did not report an attached session.");
        AssertEqual(1, provider.AttachCount, "Debugger provider was attached an unexpected number of times.");
        AssertTrue(
            ReferenceEquals(backendSession, coordinator.GetService<IDebuggerThreadService>()),
            "Debugger coordinator did not expose the active session's optional service.");

        await coordinator.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerSessionState.Paused, coordinator.State, "Debugger coordinator did not transition to Paused.");
        AssertEqual(1, events.Count, "Debugger pause event was not forwarded exactly once.");
        AssertEqual(1L, events[0].Sequence, "Debugger event sequence did not start at one.");
        AssertTrue(ReferenceEquals(identity, events[0].Identity), "Debugger event was not associated with its session identity.");
        AssertEqual(DebuggerStopReason.PauseRequested, events[0].Event.StopReason, "Debugger pause event lost its stop reason.");

        await coordinator.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerSessionState.Running, coordinator.State, "Debugger coordinator did not transition back to Running.");
        AssertEqual(2, events.Count, "Debugger continue event was not forwarded exactly once.");
        AssertEqual(2L, events[1].Sequence, "Debugger event sequence was not monotonic.");

        await coordinator.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerSessionState.Detached, coordinator.State, "Debugger coordinator did not return to Detached.");
        AssertFalse(coordinator.IsAttached, "Debugger coordinator remained attached after detach.");
        AssertEqual(1, backendSession.DetachCount, "Debugger backend detach was not invoked exactly once.");
        AssertTrue(backendSession.IsDisposed, "Debugger backend session was not disposed after detach.");
        AssertTrue(states.Contains(DebuggerSessionState.Attaching), "Debugger coordinator never exposed Attaching state.");
        AssertTrue(states.Contains(DebuggerSessionState.Detaching), "Debugger coordinator never exposed Detaching state.");
    }

    private static async Task VerifyDebuggerSessionCoordinatorSafetyAsync()
    {
        TargetProcess requestedProcess = new(123, "eboot.bin");
        TargetProcess wrongProcess = new(456, "other.bin");
        DebuggerSessionIdentity identity = new("platform.test", requestedProcess, connectionGeneration: 9);
        TestDebuggerSession wrongSession = new(wrongProcess, DebuggerExecutionState.Running);
        TestDebuggerProvider provider = new(wrongSession);
        await using DebuggerSessionCoordinator coordinator = new(identity, requestedProcess, provider);

        await AssertThrowsAsync<InvalidOperationException>(
            () => coordinator.AttachAsync(CancellationToken.None),
            "Debugger coordinator accepted a backend session attached to a different process.").ConfigureAwait(false);
        AssertEqual(DebuggerSessionState.Faulted, coordinator.State, "Failed debugger attach did not enter Faulted state.");
        AssertTrue(wrongSession.IsDisposed, "Failed debugger attach did not dispose the backend session.");

        await coordinator.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerSessionState.Detached, coordinator.State, "Debugger fault reset did not return to Detached.");

        TestDebuggerSession validSession = new(requestedProcess, DebuggerExecutionState.Running);
        provider.Session = validSession;
        await coordinator.AttachAsync(CancellationToken.None).ConfigureAwait(false);

        int forwardedEvents = 0;
        coordinator.EventReceived += (_, _) => forwardedEvents++;
        validSession.RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Breakpoint,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.Breakpoint,
            threadId: 1,
            instructionPointer: 0x1000));
        AssertEqual(1, forwardedEvents, "Active debugger event was not forwarded.");

        await coordinator.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        validSession.RaiseEvent(new DebuggerEvent(
            DebuggerEventKind.Other,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.Other));
        AssertEqual(1, forwardedEvents, "Debugger coordinator forwarded an event after the session was detached.");
    }

    private static Task VerifyPluginSettingsPersistenceAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            string settingsPath = Path.Combine(testRoot, "settings.json");
            JsonSettingsStore store = new(settingsPath);
            store.SaveApplicationString("themeId", "dark");
            store.SaveApplicationString("scanResultsStorageLocation", @"C:\ScanResults");
            store.SaveApplicationString("savedAddressesUpdateIntervalMilliseconds", "500");
            store.SaveApplicationString("frozenWriteIntervalMilliseconds", "250");

            IPluginSettings ps5Settings = store.CreatePluginSettings(Ps5PluginInfo.Id);
            IPluginSettings mockSettings = store.CreatePluginSettings(MockPluginInfo.Id);

            AssertTrue(ps5Settings.TrySetString("connection.host", "192.168.1.50"), "PS5 plugin setting could not be saved.");
            AssertTrue(ps5Settings.TrySetString("connection.port", "744"), "PS5 plugin port setting could not be saved.");
            AssertTrue(mockSettings.TrySetString("connection.host", "mock-host"), "Mock plugin setting could not be saved.");

            JsonSettingsStore reloaded = new(settingsPath);
            IPluginSettings reloadedPs5 = reloaded.CreatePluginSettings(Ps5PluginInfo.Id);
            IPluginSettings reloadedMock = reloaded.CreatePluginSettings(MockPluginInfo.Id);

            AssertTrue(reloadedPs5.TryGetString("connection.host", out string ps5Host), "Persisted PS5 host setting was not found.");
            AssertEqual("192.168.1.50", ps5Host, "Persisted PS5 host setting changed unexpectedly.");
            AssertTrue(reloadedPs5.TryGetString("connection.port", out string ps5Port), "Persisted PS5 port setting was not found.");
            AssertEqual("744", ps5Port, "Persisted PS5 port setting changed unexpectedly.");
            AssertTrue(reloadedMock.TryGetString("connection.host", out string mockHost), "Persisted Mock setting was not found.");
            AssertEqual("mock-host", mockHost, "Plugin settings namespaces were not isolated.");
            AssertEqual("dark", reloaded.LoadApplicationString("themeId"), "Plugin settings writes did not preserve the application theme setting.");
            AssertEqual(
                @"C:\ScanResults",
                reloaded.LoadApplicationString("scanResultsStorageLocation"),
                "Plugin settings writes did not preserve the application scan-storage setting.");
            AssertEqual(
                "500",
                reloaded.LoadApplicationString("savedAddressesUpdateIntervalMilliseconds"),
                "Plugin settings writes did not preserve the Saved Addresses update-interval setting.");
            AssertEqual(
                "250",
                reloaded.LoadApplicationString("frozenWriteIntervalMilliseconds"),
                "Plugin settings writes did not preserve the Frozen write-interval setting.");

            AssertTrue(reloadedPs5.TryRemove("connection.port"), "Persisted plugin setting could not be removed.");
            AssertFalse(reloadedPs5.TryGetString("connection.port", out _), "Removed plugin setting remained readable.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }

        return Task.CompletedTask;
    }

    private static Task VerifyScanResultStoragePathValidationAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            string configuredPath = Path.Combine(testRoot, "ScanResults");
            string validatedPath = ScanResultStoragePathValidator.ValidateAndPrepare(configuredPath);
            AssertTrue(Directory.Exists(validatedPath), "Storage path validation did not create the configured directory.");
            AssertEqual(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredPath)),
                validatedPath,
                "Storage path validation returned an unexpected normalized path.");

            string filePath = Path.Combine(testRoot, "not-a-directory.bin");
            File.WriteAllBytes(filePath, new byte[] { 0x01 });
            AssertFalse(
                ScanResultStoragePathValidator.TryValidateAndPrepare(filePath, out _, out string error),
                "Storage path validation accepted a file as a directory.");
            AssertTrue(!string.IsNullOrWhiteSpace(error), "Storage path validation did not explain an invalid path.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }

        return Task.CompletedTask;
    }

    private static Task VerifyScanResultApplicationSessionIsolationAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            string storageRoot = Path.Combine(testRoot, "ScanResults");
            RecordingApplicationLogger logger = new();
            ScanResultStorageManager firstManager = new(storageRoot, logger);
            Guid firstApplicationSessionId = firstManager.ApplicationSessionId;
            string firstApplicationSessionPath = firstManager.ApplicationSessionPath;

            IScanResultStorageSession interruptedSession = firstManager.CreateScanSession(
                new ScanResultSessionContext("test.plugin", 0x1234, "test.byte", "test.equals"));
            using (IScanResultWriter interruptedWriter = interruptedSession.CreateResultWriter(valueSize: 1, alignment: 1))
            {
                interruptedWriter.Write(0x1000, new byte[] { 0x32 });
            }
            AssertEqual(
                ScanResultSessionState.Writing,
                interruptedSession.State,
                "Interrupted scan session did not remain in Writing after an abandoned first-generation writer.");

            // Simulate a crash by intentionally not disposing firstManager. A new application
            // session must never adopt the old directory, even when the old scan was incomplete.
            using ScanResultStorageManager secondManager = new(storageRoot, logger);
            AssertTrue(
                secondManager.ApplicationSessionId != firstApplicationSessionId,
                "A new application launch reused the previous application-session id.");
            AssertFalse(
                string.Equals(secondManager.ApplicationSessionPath, firstApplicationSessionPath, StringComparison.OrdinalIgnoreCase),
                "A new application launch reused the previous application-session path.");
            AssertFalse(
                Directory.Exists(firstApplicationSessionPath),
                "Startup cleanup did not remove the positively identified stale application session.");
            AssertTrue(
                Directory.Exists(secondManager.ApplicationSessionPath),
                "The new application-session directory was not created.");
            AssertTrue(
                logger.Messages.Any(message => message.Contains("Stale scan-storage session detected", StringComparison.Ordinal)),
                "Startup cleanup did not log stale-session detection.");

            interruptedSession.Dispose();
            firstManager.Dispose();
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }

        return Task.CompletedTask;
    }

    private static Task VerifyScanResultStaleSessionCleanupAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            string storageRoot = Path.Combine(testRoot, "ScanResults");
            Directory.CreateDirectory(storageRoot);
            string unrelatedGuidDirectory = Path.Combine(storageRoot, Guid.NewGuid().ToString("D"));
            Directory.CreateDirectory(unrelatedGuidDirectory);
            File.WriteAllText(Path.Combine(unrelatedGuidDirectory, "user-data.txt"), "not managed by Memory Engine");

            using ScanResultStorageManager manager = new(storageRoot);
            AssertTrue(
                Directory.Exists(unrelatedGuidDirectory),
                "Startup cleanup deleted a GUID-named directory that lacked Memory Engine session metadata.");
            AssertTrue(
                File.Exists(Path.Combine(unrelatedGuidDirectory, "user-data.txt")),
                "Startup cleanup modified unrelated user data in the configured storage root.");
            AssertFalse(
                manager.ApplicationSessionPath.StartsWith(unrelatedGuidDirectory, StringComparison.OrdinalIgnoreCase),
                "The current application session adopted an unrelated stale directory.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }

        return Task.CompletedTask;
    }

    private static Task VerifyScanResultSessionLifecycleAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            using ScanResultStorageManager manager = new(Path.Combine(testRoot, "ScanResults"));
            ScanResultSessionContext context = new(
                "test.plugin",
                0xCAFE,
                "test.byte",
                "test.equals",
                RecordFormatVersion: 1);

            IScanResultStorageSession firstSession = manager.CreateScanSession(context);
            Guid firstScanSessionId = firstSession.ScanSessionId;
            AssertEqual(
                ScanResultSessionState.Creating,
                firstSession.State,
                "A new scan session did not start in Creating state.");
            AssertFalse(firstSession.IsCommitted, "A new scan session was incorrectly considered committed.");

            using (IScanResultWriter firstWriter = firstSession.CreateResultWriter(valueSize: 1, alignment: 1))
            {
                AssertEqual(
                    ScanResultSessionState.Writing,
                    firstSession.State,
                    "Scan session did not enter Writing when the first result generation was opened.");
                AssertFalse(firstSession.IsCommitted, "Writing scan data was incorrectly considered committed.");
                firstWriter.Write(0x1000, new byte[] { 0x32 });
                firstWriter.Write(0x1001, new byte[] { 0x32 });
                firstWriter.CommitAsync(CancellationToken.None).GetAwaiter().GetResult();
            }

            AssertTrue(firstSession.IsCommitted, "A published scan-result generation was not considered committed.");
            AssertEqual<long?>(2, firstSession.ResultCount, "Committed scan-result count was not retained.");
            AssertTrue(File.Exists(firstSession.MetadataPath), "Committed scan metadata file is missing.");
            string committedMetadata = File.ReadAllText(firstSession.MetadataPath);
            AssertTrue(
                committedMetadata.Contains("\"state\": \"Committed\"", StringComparison.Ordinal),
                "Committed scan metadata did not persist its state.");
            AssertTrue(
                committedMetadata.Contains("\"recordFormatVersion\": 1", StringComparison.Ordinal),
                "Committed scan metadata did not persist its record format version.");

            // A compatible Next Scan keeps this same logical session; no new storage session
            // is created merely by reading or refining the committed session.
            AssertEqual(firstScanSessionId, firstSession.ScanSessionId, "The active scan-session identity changed unexpectedly.");

            string firstSessionPath = firstSession.SessionPath;
            firstSession.Invalidate();
            AssertEqual(
                ScanResultSessionState.Invalidated,
                firstSession.State,
                "New Scan invalidation did not make the old session ineligible.");
            AssertFalse(Directory.Exists(firstSessionPath), "Invalidated scan-session data was not cleaned up when deletion was possible.");

            IScanResultStorageSession secondSession = manager.CreateScanSession(context);
            AssertTrue(
                secondSession.ScanSessionId != firstScanSessionId,
                "A later First Scan reused a previous scan-session id.");
            secondSession.Cancel();
            AssertEqual(
                ScanResultSessionState.Cancelled,
                secondSession.State,
                "Cancelled scan session did not retain a non-committed terminal state.");
            AssertFalse(secondSession.IsCommitted, "Cancelled scan data was incorrectly considered committed.");

            IScanResultStorageSession failedSession = manager.CreateScanSession(context);
            failedSession.Fail("simulated failure");
            AssertEqual(
                ScanResultSessionState.Failed,
                failedSession.State,
                "Failed scan session did not retain a non-committed terminal state.");
            AssertFalse(failedSession.IsCommitted, "Failed scan data was incorrectly considered committed.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }

        return Task.CompletedTask;
    }

    private static async Task VerifyDiskBackedResultGenerationCommitsAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            using ScanResultStorageManager manager = new(Path.Combine(testRoot, "ScanResults"));
            using IScanResultStorageSession session = manager.CreateScanSession(
                new ScanResultSessionContext("test.plugin", 0x1001, "test.byte", "test.equals", RecordFormatVersion: 1));

            using (IScanResultWriter firstWriter = session.CreateResultWriter(valueSize: 1, alignment: 1))
            {
                firstWriter.Write(0x1000, new byte[] { 0x11 });
                firstWriter.Write(0x1001, new byte[] { 0x22 });
                await firstWriter.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            using (IScanResultSet firstSet = session.OpenResultSet())
            {
                AssertEqual(2L, firstSet.Count, "First disk-backed generation retained an unexpected result count.");
                ScanResultRecordBatch firstBatch = await ReadFirstScanResultBatchAsync(firstSet).ConfigureAwait(false);
                AssertEqual((ulong)0x1000, firstBatch.Addresses.Span[0], "First generation stored the wrong first address.");
                AssertEqual((byte)0x22, firstBatch.GetCurrentValueData(1).Span[0], "First generation stored the wrong second value.");
            }

            using (IScanResultWriter abandonedWriter = session.CreateResultWriter(valueSize: 1, alignment: 1))
            {
                abandonedWriter.Write(0x2000, new byte[] { 0x33 });
            }

            using (IScanResultSet afterAbandonedWrite = session.OpenResultSet())
            {
                AssertEqual(2L, afterAbandonedWrite.Count, "An incomplete replacement generation displaced the previous committed result set.");
            }

            using (IScanResultWriter secondWriter = session.CreateResultWriter(valueSize: 1, alignment: 1))
            {
                secondWriter.Write(0x3000, new byte[] { 0x44 });
                await secondWriter.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            using IScanResultSet secondSet = session.OpenResultSet();
            AssertEqual(1L, secondSet.Count, "Second committed generation retained an unexpected result count.");
            ScanResultRecordBatch secondBatch = await ReadFirstScanResultBatchAsync(secondSet).ConfigureAwait(false);
            AssertEqual((ulong)0x3000, secondBatch.Addresses.Span[0], "Second committed generation did not replace the active result set.");
            AssertEqual((byte)0x44, secondBatch.GetCurrentValueData(0).Span[0], "Second committed generation stored the wrong value.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }
    }

    private static async Task VerifyDiskBackedNextScanUsesCompleteResultSetAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            MockTargetPlugin plugin = new();
            await using ITargetSession targetSession = await plugin
                .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
                .ConfigureAwait(false);

            TargetProcess process = (await targetSession
                    .GetRequiredService<IProcessProvider>()
                    .GetProcessesAsync(CancellationToken.None)
                    .ConfigureAwait(false))
                .Single();
            IReadOnlyList<MemoryRegion> regions = await targetSession
                .GetRequiredService<IMemoryMapProvider>()
                .GetMemoryRegionsAsync(process, CancellationToken.None)
                .ConfigureAwait(false);
            IMemoryReader reader = targetSession.GetRequiredService<IMemoryReader>();
            IMemoryWriter writerService = targetSession.GetRequiredService<IMemoryWriter>();

            IMemoryValueType valueType = new TestByteValueType();
            IMemoryScanType scanType = new TestByteScanType();
            MemoryScanValue zero = ParseValue(valueType, "0", targetSession.Architecture);
            MemoryScanValue marker = ParseValue(valueType, "255", targetSession.Architecture);
            const int previewLimit = 50_000;
            ulong hiddenAddress = MockTargetLayout.BaseAddress + 60_000;

            using ScanResultStorageManager manager = new(Path.Combine(testRoot, "ScanResults"));
            using IScanResultStorageSession storageSession = manager.CreateScanSession(
                new ScanResultSessionContext(
                    plugin.Metadata.Id,
                    process.Id,
                    valueType.Id,
                    scanType.Id,
                    RecordFormatVersion: 1));
            MemoryScanner scanner = new();

            using (IScanResultWriter firstWriter = storageSession.CreateResultWriter(valueSize: 1, alignment: 1))
            {
                MemoryScanExecutionResult first = await scanner
                    .FirstScanToStorageAsync(
                        process,
                        regions,
                        reader,
                        firstWriter,
                        targetSession.Architecture,
                        valueType,
                        scanType,
                        new[] { zero },
                        MemoryScanOptions.Empty,
                        previewLimit,
                        progress: null,
                        CancellationToken.None)
                    .ConfigureAwait(false);
                await firstWriter.CommitAsync(CancellationToken.None).ConfigureAwait(false);

                AssertTrue(first.TotalResultCount > previewLimit, "First disk-backed scan did not create a result set larger than the UI preview.");
                AssertEqual(previewLimit, first.Results.Count, "First disk-backed scan did not enforce the requested preview limit.");
                AssertFalse(first.Results.Any(item => item.Address == hiddenAddress), "The test address unexpectedly appeared inside the bounded UI preview.");
            }

            using IScanResultSet previousResults = storageSession.OpenResultSet();
            AssertTrue(previousResults.Count > previewLimit, "Committed disk-backed result set did not retain results beyond the UI preview.");

            await writerService
                .WriteAsync(process, hiddenAddress, new byte[] { 0xFF }, CancellationToken.None)
                .ConfigureAwait(false);

            using (IScanResultWriter nextWriter = storageSession.CreateResultWriter(valueSize: 1, alignment: 1))
            {
                MemoryScanExecutionResult next = await scanner
                    .NextScanFromStorageAsync(
                        process,
                        regions,
                        previousResults,
                        reader,
                        nextWriter,
                        targetSession.Architecture,
                        valueType,
                        scanType,
                        new[] { marker },
                        MemoryScanOptions.Empty,
                        previewLimit,
                        progress: null,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                AssertEqual(1L, next.TotalResultCount, "Next Scan did not refine the complete disk-backed candidate set.");
                AssertEqual(1, next.Results.Count, "Next Scan preview contained an unexpected number of survivors.");
                AssertEqual(hiddenAddress, next.Results[0].Address, "Next Scan failed to retain a survivor that was outside the First Scan UI preview.");

                await nextWriter.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }
    }

    private static async Task VerifyDiskBackedNativeStreamBeyondLegacyLimitAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            const ulong baseAddress = 0x40000000;
            int resultCount = checked(MemoryScanner.MaximumResultCount + 1);
            MemoryRegion region = new(
                baseAddress,
                checked((ulong)resultCount + 0x1000UL),
                MemoryProtection.Read | MemoryProtection.Private,
                "Synthetic Native Scan",
                "Synthetic");
            IMemoryValueType valueType = new TestByteValueType();
            TargetArchitecture architecture = new(CpuArchitecture.X64, 64, 64, Endianness.Little);
            await using SyntheticNativeValueScanResultStream nativeResults = new(
                baseAddress,
                resultCount,
                batchSize: 65_536,
                value: 0x5A);

            using ScanResultStorageManager manager = new(Path.Combine(testRoot, "ScanResults"));
            using IScanResultStorageSession session = manager.CreateScanSession(
                new ScanResultSessionContext("test.native", 0x2002, valueType.Id, "test.equals", RecordFormatVersion: 1));
            using IScanResultWriter writer = session.CreateResultWriter(valueSize: 1, alignment: 1);

            MemoryScanner scanner = new();
            MemoryScanExecutionResult result = await scanner
                .ConsumeNativeScanStreamAsync(
                    new[] { region },
                    nativeResults,
                    writer,
                    architecture,
                    valueType,
                    maximumPreviewResults: 32,
                    previousResults: null,
                    progress: null,
                    CancellationToken.None)
                .ConfigureAwait(false);

            AssertEqual((long)resultCount, result.TotalResultCount, "Disk-backed native stream was truncated at the legacy two-million result limit.");
            AssertEqual(32, result.Results.Count, "Disk-backed native stream did not keep its UI preview bounded.");
            AssertEqual(baseAddress, result.Results[0].Address, "Disk-backed native stream stored an unexpected first preview address.");
            AssertEqual(baseAddress + checked((ulong)resultCount - 1), nativeResults.LastProducedAddress, "Synthetic native stream did not enumerate every advertised result.");

            await writer.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            using IScanResultSet stored = session.OpenResultSet();
            AssertEqual((long)resultCount, stored.Count, "Committed disk-backed native stream did not retain every result.");

            ulong lastStoredAddress = 0;
            long storedCount = 0;
            await foreach (ReadOnlyMemory<ulong> batch in stored
                               .ReadAddressBatchesAsync(65_536, CancellationToken.None)
                               .ConfigureAwait(false))
            {
                storedCount += batch.Length;
                lastStoredAddress = batch.Span[^1];
            }

            AssertEqual((long)resultCount, storedCount, "Disk-backed native result reader did not enumerate every committed record.");
            AssertEqual(baseAddress + checked((ulong)resultCount - 1), lastStoredAddress, "Disk-backed native result reader returned an unexpected final address.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }
    }

    private static async Task VerifyResidentNativeResultContractAsync()
    {
        const ulong baseAddress = 0x50000000;
        const int resultCount = 6;
        TargetArchitecture architecture = new(CpuArchitecture.X64, 64, 64, Endianness.Little);
        IMemoryValueType valueType = new TestByteValueType();
        MemoryRegion region = new(
            baseAddress,
            0x1000,
            MemoryProtection.Read | MemoryProtection.Private,
            "Resident Test",
            "Synthetic");
        await using SyntheticResidentResultSet resident = new(
            baseAddress,
            resultCount,
            currentValue: 0x31,
            previousValue: 0x21);

        MemoryScanner scanner = new();
        MemoryScanExecutionResult preview = await scanner
            .ReadNativeResidentPreviewAsync(
                new[] { region },
                resident,
                architecture,
                valueType,
                maximumPreviewResults: 3,
                includePreviousValues: true,
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual((long)resultCount, preview.TotalResultCount, "Resident preview lost the authoritative total result count.");
        AssertEqual(3, preview.Results.Count, "Resident preview did not stay bounded to the requested visible result limit.");
        AssertEqual(3L, resident.LastWindowMaximumRecords, "Resident preview requested unrelated result records from the backend.");
        AssertEqual("49", preview.Results[0].CurrentValue.DisplayText, "Resident preview decoded an unexpected current value.");
        AssertEqual("33", preview.Results[0].PreviousValue?.DisplayText, "Resident preview did not retain previous-value data.");

        NativeValueScanRequest request = new(
            valueType.Id,
            StandardMemoryScanTypeIds.ChangedValue,
            valueSize: 1,
            alignment: 1,
            inputValues: Array.Empty<ReadOnlyMemory<byte>>());
        AssertTrue(resident.CanRefine(request), "Synthetic resident set unexpectedly rejected a compatible native refinement.");

        string testRoot = CreateTemporaryDirectory();
        try
        {
            using ScanResultStorageManager manager = new(Path.Combine(testRoot, "ScanResults"));
            using IScanResultStorageSession session = manager.CreateScanSession(
                new ScanResultSessionContext("test.resident", 0x5005, valueType.Id, StandardMemoryScanTypeIds.UnknownInitialValue));
            using IScanResultWriter writer = session.CreateResultWriter(valueSize: 1, alignment: 1);

            await scanner.MaterializeNativeResidentResultSetAsync(
                    new[] { region },
                    resident,
                    writer,
                    progress: null,
                    CancellationToken.None)
                .ConfigureAwait(false);
            await writer.CommitAsync(CancellationToken.None).ConfigureAwait(false);

            using IScanResultSet stored = session.OpenResultSet();
            AssertEqual((long)resultCount, stored.Count, "Resident materialization did not persist the complete result set.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }
    }

    private static Task VerifyOperationProgressContractAsync()
    {
        OperationProgress indeterminate = new("Preparing...", fraction: null, "Waiting for work size");
        AssertTrue(indeterminate.IsIndeterminate, "Null progress fraction must represent indeterminate progress.");
        AssertEqual(0d, indeterminate.Percentage, "Indeterminate progress must not expose a non-zero determinate percentage.");

        OperationProgress determinate = new("Writing...", 0.67d, "67 / 100 records");
        AssertFalse(determinate.IsIndeterminate, "A numeric progress fraction was treated as indeterminate.");
        AssertEqual(67d, determinate.Percentage, "Progress fraction was not converted to percentage correctly.");
        AssertEqual("67 / 100 records", determinate.Detail, "Progress detail text was not retained.");

        AssertThrows<ArgumentOutOfRangeException>(
            () => _ = new OperationProgress("Invalid", 1.01d),
            "Operation progress accepted a fraction above 1.0.");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ = new OperationProgress("Invalid", -0.01d),
            "Operation progress accepted a negative fraction.");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ = new OperationProgress("Invalid", double.NaN),
            "Operation progress accepted NaN as a determinate fraction.");

        return Task.CompletedTask;
    }

    private static async Task VerifyUniversalExportTextFormatsAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            TargetArchitecture architecture = new(
                CpuArchitecture.X64,
                pointerWidthBits: 64,
                addressWidthBits: 64,
                endianness: Endianness.Little);
            MemoryScanResult[] results =
            {
                CreateExportTestResult(0x1000, 123, 100, "region-a", "module,main|section\tA", architecture),
                CreateExportTestResult(0x2000, -7, null, "region-b", null, architecture)
            };
            Dictionary<string, ExportCellValue> metadata = new(StringComparer.Ordinal)
            {
                ["pluginId"] = ExportCellValue.FromString("test.plugin")
            };
            MemoryScanResultExportSource source = MemoryScanResultExportSource.FromMaterialized(results, metadata);
            TabularExportService service = new();
            string[] columns =
            {
                MemoryScanResultExportColumnIds.Address,
                MemoryScanResultExportColumnIds.Value,
                MemoryScanResultExportColumnIds.Protection,
                MemoryScanResultExportColumnIds.RegionOrModule
            };

            string csvPath = Path.Combine(testRoot, "results.csv");
            await service.ExportAsync(
                csvPath,
                TabularExportFormat.Csv,
                source,
                columns,
                progress: null,
                CancellationToken.None).ConfigureAwait(false);
            string csv = File.ReadAllText(csvPath);
            AssertTrue(csv.StartsWith("Address,Value,Protection,Region / Module", StringComparison.Ordinal),
                "CSV export header changed unexpectedly.");
            AssertTrue(csv.Contains("0x1000,123,\"Read, Write\",\"module,main|section\tA\"", StringComparison.Ordinal),
                "CSV export did not include or quote the memory protection and region fields correctly.");

            string tsvPath = Path.Combine(testRoot, "results.tsv");
            await service.ExportAsync(
                tsvPath,
                TabularExportFormat.Tsv,
                source,
                columns,
                progress: null,
                CancellationToken.None).ConfigureAwait(false);
            string tsv = File.ReadAllText(tsvPath);
            AssertTrue(tsv.StartsWith("Address\tValue\tProtection\tRegion / Module", StringComparison.Ordinal),
                "TSV export header changed unexpectedly.");
            AssertTrue(tsv.Contains("\"module,main|section\tA\"", StringComparison.Ordinal),
                "TSV export did not quote an embedded tab.");

            string markdownPath = Path.Combine(testRoot, "results.md");
            await service.ExportAsync(
                markdownPath,
                TabularExportFormat.MarkdownTable,
                source,
                columns,
                progress: null,
                CancellationToken.None).ConfigureAwait(false);
            string markdown = File.ReadAllText(markdownPath);
            AssertTrue(markdown.Contains("module,main\\|section", StringComparison.Ordinal),
                "Markdown export did not escape an embedded table separator.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }
    }

    private static async Task VerifyUniversalExportJsonAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            TargetArchitecture architecture = new(
                CpuArchitecture.X64,
                pointerWidthBits: 64,
                addressWidthBits: 64,
                endianness: Endianness.Little);
            MemoryScanResult[] results =
            {
                CreateExportTestResult(0x1234, 42, 41, "rw", "game.exe", architecture),
                CreateExportTestResult(0x5678, 99, null, "rw", null, architecture)
            };
            Dictionary<string, ExportCellValue> metadata = new(StringComparer.Ordinal)
            {
                ["pluginId"] = ExportCellValue.FromString("test.plugin"),
                ["sourceRowCount"] = ExportCellValue.FromInt64(results.LongLength)
            };
            MemoryScanResultExportSource source = MemoryScanResultExportSource.FromMaterialized(results, metadata);
            string path = Path.Combine(testRoot, "results.json");

            TabularExportService service = new();
            TabularExportResult exportResult = await service.ExportAsync(
                path,
                TabularExportFormat.Json,
                source,
                new[]
                {
                    MemoryScanResultExportColumnIds.Address,
                    MemoryScanResultExportColumnIds.Previous,
                    MemoryScanResultExportColumnIds.Protection
                },
                progress: null,
                CancellationToken.None).ConfigureAwait(false);

            AssertEqual(2L, exportResult.RowsWritten, "JSON export wrote an unexpected row count.");
            string json = File.ReadAllText(path);
            AssertTrue(json.Contains("\n  \"type\"", StringComparison.Ordinal),
                "JSON export is not written with human-readable indentation.");
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            AssertEqual("scan-results", root.GetProperty("type").GetString(), "JSON export type metadata changed unexpectedly.");
            AssertEqual(1, root.GetProperty("schemaVersion").GetInt32(), "JSON export schema version changed unexpectedly.");
            AssertEqual(2L, root.GetProperty("rowCount").GetInt64(), "JSON export rowCount changed unexpectedly.");
            AssertEqual(
                "test.plugin",
                root.GetProperty("metadata").GetProperty("pluginId").GetString(),
                "JSON export metadata did not preserve pluginId.");
            JsonElement rows = root.GetProperty("rows");
            AssertEqual(2, rows.GetArrayLength(), "JSON export rows array has an unexpected length.");
            AssertEqual("0x1234", rows[0].GetProperty("address").GetString(), "JSON export address formatting changed unexpectedly.");
            AssertEqual("41", rows[0].GetProperty("previous").GetString(), "JSON export previous value changed unexpectedly.");
            AssertEqual("Read, Write", rows[0].GetProperty("protection").GetString(), "JSON export protection changed unexpectedly.");
            AssertEqual(JsonValueKind.Null, rows[1].GetProperty("previous").ValueKind, "Missing Previous value must remain JSON null.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }
    }

    private static async Task VerifyUniversalExportCancellationAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            string destination = Path.Combine(testRoot, "existing.csv");
            File.WriteAllText(destination, "original-content");
            using CancellationTokenSource cancellationSource = new();
            CancellingExportDataSource source = new(cancellationSource);
            TabularExportService service = new();

            bool cancelled = false;
            try
            {
                await service.ExportAsync(
                    destination,
                    TabularExportFormat.Csv,
                    source,
                    new[] { "value" },
                    progress: null,
                    cancellationSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            AssertTrue(cancelled, "Cancelled export did not propagate cancellation.");
            AssertEqual("original-content", File.ReadAllText(destination),
                "Cancelled export replaced an existing completed destination file.");
            AssertEqual(0, Directory.GetFiles(testRoot, ".*.tmp").Length,
                "Cancelled export left a temporary partial file behind.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }
    }

    private static async Task VerifyDiskBackedScanResultExportAsync()
    {
        const int resultCount = 50_005;
        string testRoot = CreateTemporaryDirectory();
        try
        {
            using ScanResultStorageManager manager = new(Path.Combine(testRoot, "ScanResults"));
            using IScanResultStorageSession session = manager.CreateScanSession(
                new ScanResultSessionContext("test.plugin", 0x55, StandardMemoryValueTypeIds.Int32, StandardMemoryScanTypeIds.ExactValue));
            using (IScanResultWriter writer = session.CreateResultWriter(valueSize: 4, alignment: 4))
            {
                byte[] valueBytes = new byte[4];
                for (int index = 0; index < resultCount; index++)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(valueBytes, index);
                    writer.Write(checked(0x100000UL + (ulong)(index * 4)), valueBytes);
                }

                await writer.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            using IScanResultSet resultSet = session.OpenResultSet();
            TargetArchitecture architecture = new(
                CpuArchitecture.X64,
                pointerWidthBits: 64,
                addressWidthBits: 64,
                endianness: Endianness.Little);
            MemoryRegion[] memoryRegions =
            {
                new(
                    0x100000UL,
                    checked((ulong)resultCount * 4UL),
                    MemoryProtection.Read | MemoryProtection.Write,
                    "export-test")
            };
            MemoryScanResultExportSource source = MemoryScanResultExportSource.FromDiskBacked(
                resultSet,
                StandardMemoryValueTypes.Int32,
                architecture,
                memoryRegions,
                new Dictionary<string, ExportCellValue>());
            AssertEqual(4, source.Columns.Count,
                "Complete disk-backed scan export must expose Address, Value, Type, and map-derived Protection.");

            string destination = Path.Combine(testRoot, "complete.csv");
            TabularExportService service = new();
            TabularExportResult exportResult = await service.ExportAsync(
                destination,
                TabularExportFormat.Csv,
                source,
                new[]
                {
                    MemoryScanResultExportColumnIds.Address,
                    MemoryScanResultExportColumnIds.Value,
                    MemoryScanResultExportColumnIds.Type,
                    MemoryScanResultExportColumnIds.Protection
                },
                progress: null,
                CancellationToken.None).ConfigureAwait(false);

            AssertEqual((long)resultCount, exportResult.RowsWritten,
                "Complete disk-backed Scan Results export did not write every stored result.");
            AssertEqual((long)resultCount + 1, File.ReadLines(destination).LongCount(),
                "Complete disk-backed Scan Results export appears limited to the 50,000-row presentation preview.");
            AssertTrue(File.ReadLines(destination).Skip(1).First().EndsWith("\"Read, Write\"", StringComparison.Ordinal),
                "Complete disk-backed Scan Results export did not resolve Protection from the memory map.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }
    }


    private static async Task VerifyResidentScanResultExportAsync()
    {
        const int resultCount = 50_005;
        string testRoot = CreateTemporaryDirectory();
        try
        {
            await using SyntheticResidentResultSet resident = new(
                baseAddress: 0x200000,
                resultCount,
                currentValue: 77,
                previousValue: 66);
            TargetArchitecture architecture = new(
                CpuArchitecture.X64,
                pointerWidthBits: 64,
                addressWidthBits: 64,
                endianness: Endianness.Little);
            MemoryRegion[] memoryRegions =
            {
                new(
                    0x200000UL,
                    checked((ulong)resultCount),
                    MemoryProtection.Read,
                    "resident-export-test")
            };
            MemoryScanResultExportSource source = MemoryScanResultExportSource.FromNativeResident(
                resident,
                StandardMemoryValueTypes.UInt8,
                architecture,
                memoryRegions,
                new Dictionary<string, ExportCellValue>());

            AssertTrue(source.UsesNativeResidentSource,
                "Native resident Scan Results export did not retain its source classification.");
            AssertEqual(4, source.Columns.Count,
                "Complete resident scan export must expose Address, Value, Type, and map-derived Protection.");

            string destination = Path.Combine(testRoot, "resident-complete.csv");
            TabularExportService service = new();
            TabularExportResult exportResult = await service.ExportAsync(
                destination,
                TabularExportFormat.Csv,
                source,
                new[]
                {
                    MemoryScanResultExportColumnIds.Address,
                    MemoryScanResultExportColumnIds.Value,
                    MemoryScanResultExportColumnIds.Type,
                    MemoryScanResultExportColumnIds.Protection
                },
                progress: null,
                CancellationToken.None).ConfigureAwait(false);

            AssertEqual((long)resultCount, exportResult.RowsWritten,
                "Complete resident Scan Results export did not write every backend-resident result.");
            AssertEqual((long)resultCount, resident.LastWindowMaximumRecords,
                "Resident export did not request the authoritative complete result count.");
            AssertEqual((long)resultCount + 1, File.ReadLines(destination).LongCount(),
                "Complete resident Scan Results export appears limited to the 50,000-row presentation preview.");
            AssertTrue(File.ReadLines(destination).Skip(1).First().EndsWith(",Read", StringComparison.Ordinal),
                "Complete resident Scan Results export did not resolve Protection from the memory map.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }
    }

    private static MemoryScanResult CreateExportTestResult(
        ulong address,
        int currentValue,
        int? previousValue,
        string? region,
        string? module,
        TargetArchitecture architecture,
        MemoryProtection protection = MemoryProtection.Read | MemoryProtection.Write)
    {
        byte[] currentBytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(currentBytes, currentValue);
        MemoryScanValue current = StandardMemoryValueTypes.Int32.CreateValue(currentBytes, 4, architecture);

        MemoryScanValue? previous = null;
        if (previousValue is int previousInt)
        {
            byte[] previousBytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(previousBytes, previousInt);
            previous = StandardMemoryValueTypes.Int32.CreateValue(previousBytes, 4, architecture);
        }

        return new MemoryScanResult(address, current, previous, region, module, protection);
    }

    private static Task VerifyPluginOwnedScanDefinitionContractsAsync()
    {
        AssertTrue(
            typeof(MemoryScanner).Assembly.GetType("TeeKay87.MemoryEngine.Core.Scanning.MemoryValueTypeCatalog") is null,
            "Core must not contain a concrete MemoryValueType catalog.");
        AssertTrue(
            typeof(MemoryScanner).Assembly.GetType("TeeKay87.MemoryEngine.Core.Scanning.MemoryScanTypeCatalog") is not null,
            "Core must own the shared MemoryScanType catalog.");
        AssertTrue(
            typeof(MemoryScanner).Assembly.GetType("TeeKay87.MemoryEngine.Core.Scanning.MemoryScanValueCodec") is null,
            "Core must not contain a concrete value-type codec.");
        AssertTrue(
            typeof(IMemoryValueType).Assembly.GetType("TeeKay87.MemoryEngine.PluginSdk.Models.MemoryValueType") is null,
            "Plugin SDK must not contain the obsolete rev2 MemoryValueType enum.");

        IMemoryValueType[] standardTypes = StandardMemoryValueTypes.NumericAndByteArray.ToArray();
        AssertEqual(11, standardTypes.Length, "Unexpected reusable standard value-type count.");
        AssertTrue(standardTypes.Take(10).All(item => item is IMemoryValueComparer), "All standard numeric Value Types must support Core ordered/delta scans.");
        AssertTrue(StandardMemoryValueTypes.ByteArray.FixedSize is null, "Array of Bytes must remain variable-width.");

        string[] expectedScanTypeIds =
        {
            StandardMemoryScanTypeIds.ExactValue,
            StandardMemoryScanTypeIds.FuzzyValue,
            StandardMemoryScanTypeIds.BiggerThan,
            StandardMemoryScanTypeIds.SmallerThan,
            StandardMemoryScanTypeIds.Between,
            StandardMemoryScanTypeIds.UnknownInitialValue,
            StandardMemoryScanTypeIds.UnknownInitialLowValue,
            StandardMemoryScanTypeIds.IncreasedValue,
            StandardMemoryScanTypeIds.DecreasedValue,
            StandardMemoryScanTypeIds.ChangedValue,
            StandardMemoryScanTypeIds.UnchangedValue,
            StandardMemoryScanTypeIds.IncreasedBy,
            StandardMemoryScanTypeIds.DecreasedBy
        };

        AssertTrue(
            MemoryScanTypeCatalog.All.Select(item => item.Id).SequenceEqual(expectedScanTypeIds),
            "Core Scan Type catalog order or contents changed unexpectedly.");
        AssertEqual(StandardMemoryScanTypeIds.ExactValue, MemoryScanTypeCatalog.DefaultScanTypeId, "Unexpected Core default Scan Type.");
        AssertTrue(ExactScanType.AvailableForFirstScan && ExactScanType.AvailableForNextScan, "Exact Value must support both scan stages.");

        return Task.CompletedTask;
    }

    private static Task VerifyCoreScanTypeSemanticsAsync()
    {
        TargetArchitecture architecture = new(CpuArchitecture.X64, 64, 64, Endianness.Little);
        IMemoryValueType valueType = StandardMemoryValueTypes.Int32;
        MemoryScanValue negativeTen = ParseValue(valueType, "-10", architecture);
        MemoryScanValue zero = ParseValue(valueType, "0", architecture);
        MemoryScanValue ten = ParseValue(valueType, "10", architecture);
        MemoryScanValue twenty = ParseValue(valueType, "20", architecture);
        MemoryScanValue thirty = ParseValue(valueType, "30", architecture);
        ReadOnlySpan<byte> currentTwenty = twenty.Bytes.Span;

        IMemoryScanType Get(string id) => MemoryScanTypeCatalog.All.Single(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));

        AssertTrue(Get(StandardMemoryScanTypeIds.BiggerThan).IsMatch(valueType, currentTwenty, null, new[] { ten }, architecture, MemoryScanStage.FirstScan), "Bigger Than did not match 20 > 10.");
        AssertTrue(Get(StandardMemoryScanTypeIds.SmallerThan).IsMatch(valueType, currentTwenty, null, new[] { thirty }, architecture, MemoryScanStage.FirstScan), "Smaller Than did not match 20 < 30.");
        AssertTrue(Get(StandardMemoryScanTypeIds.Between).IsMatch(valueType, currentTwenty, null, new[] { ten, thirty }, architecture, MemoryScanStage.FirstScan), "Between did not include a value inside the inclusive range.");
        AssertTrue(Get(StandardMemoryScanTypeIds.UnknownInitialValue).IsMatch(valueType, currentTwenty, null, Array.Empty<MemoryScanValue>(), architecture, MemoryScanStage.FirstScan), "Unknown Initial Value must retain every fixed-width First Scan candidate.");

        IMemoryScanType unknownLow = Get(StandardMemoryScanTypeIds.UnknownInitialLowValue);
        AssertTrue(unknownLow.IsMatch(valueType, currentTwenty, null, new[] { thirty }, architecture, MemoryScanStage.FirstScan), "Unknown Initial Low Value did not retain a positive value below the upper limit.");
        AssertFalse(unknownLow.IsMatch(valueType, zero.Bytes.Span, null, new[] { thirty }, architecture, MemoryScanStage.FirstScan), "Unknown Initial Low Value must exclude zero.");
        AssertFalse(unknownLow.IsMatch(valueType, negativeTen.Bytes.Span, null, new[] { thirty }, architecture, MemoryScanStage.FirstScan), "Unknown Initial Low Value must exclude negative values.");
        AssertFalse(unknownLow.IsMatch(valueType, thirty.Bytes.Span, null, new[] { twenty }, architecture, MemoryScanStage.FirstScan), "Unknown Initial Low Value retained a value above the upper limit.");
        AssertFalse(unknownLow.TryValidateInputValues(valueType, new[] { zero }, architecture, MemoryScanStage.FirstScan, out _), "Unknown Initial Low Value accepted a zero upper limit.");

        IMemoryScanType fuzzy = Get(StandardMemoryScanTypeIds.FuzzyValue);
        MemoryScanValue fuzzyTarget = ParseValue(StandardMemoryValueTypes.Float32, "100", architecture);
        MemoryScanValue fuzzyInside = ParseValue(StandardMemoryValueTypes.Float32, "100.5", architecture);
        MemoryScanValue fuzzyBoundary = ParseValue(StandardMemoryValueTypes.Float32, "101", architecture);
        AssertTrue(fuzzy.SupportsValueType(StandardMemoryValueTypes.Float32), "Fuzzy Value must support Float.");
        AssertTrue(fuzzy.SupportsValueType(StandardMemoryValueTypes.Float64), "Fuzzy Value must support Double.");
        AssertFalse(fuzzy.SupportsValueType(StandardMemoryValueTypes.Int32), "Fuzzy Value must remain floating-point only.");
        AssertTrue(fuzzy.IsMatch(StandardMemoryValueTypes.Float32, fuzzyInside.Bytes.Span, null, new[] { fuzzyTarget }, architecture, MemoryScanStage.FirstScan), "Fuzzy Value rejected an absolute difference below 1.0.");
        AssertFalse(fuzzy.IsMatch(StandardMemoryValueTypes.Float32, fuzzyBoundary.Bytes.Span, null, new[] { fuzzyTarget }, architecture, MemoryScanStage.FirstScan), "Fuzzy Value must use a strict < 1.0 boundary.");

        AssertTrue(Get(StandardMemoryScanTypeIds.IncreasedValue).IsMatch(valueType, currentTwenty, ten, Array.Empty<MemoryScanValue>(), architecture, MemoryScanStage.NextScan), "Increased Value did not compare against the previous snapshot.");
        AssertTrue(Get(StandardMemoryScanTypeIds.DecreasedValue).IsMatch(valueType, currentTwenty, thirty, Array.Empty<MemoryScanValue>(), architecture, MemoryScanStage.NextScan), "Decreased Value did not compare against the previous snapshot.");
        AssertTrue(Get(StandardMemoryScanTypeIds.ChangedValue).IsMatch(valueType, currentTwenty, ten, Array.Empty<MemoryScanValue>(), architecture, MemoryScanStage.NextScan), "Changed Value did not detect a changed snapshot.");
        AssertTrue(Get(StandardMemoryScanTypeIds.UnchangedValue).IsMatch(valueType, currentTwenty, twenty, Array.Empty<MemoryScanValue>(), architecture, MemoryScanStage.NextScan), "Unchanged Value did not preserve an equal snapshot.");

        byte[] nanPayloadA = { 0x01, 0x00, 0xC0, 0x7F };
        byte[] nanPayloadB = { 0x02, 0x00, 0xC0, 0x7F };
        MemoryScanValue previousNan = new(
            StandardMemoryValueTypeIds.Float32,
            StandardMemoryValueTypes.Float32.DisplayName,
            nanPayloadA,
            "NaN",
            sizeof(float));
        AssertTrue(
            Get(StandardMemoryScanTypeIds.UnchangedValue).IsMatch(
                StandardMemoryValueTypes.Float32,
                nanPayloadA,
                previousNan,
                Array.Empty<MemoryScanValue>(),
                architecture,
                MemoryScanStage.NextScan),
            "Unchanged Value must compare floating snapshots by stored bytes so an unchanged NaN payload remains unchanged.");
        AssertFalse(
            Get(StandardMemoryScanTypeIds.ChangedValue).IsMatch(
                StandardMemoryValueTypes.Float32,
                nanPayloadA,
                previousNan,
                Array.Empty<MemoryScanValue>(),
                architecture,
                MemoryScanStage.NextScan),
            "Changed Value incorrectly treated an unchanged NaN payload as changed.");
        AssertTrue(
            Get(StandardMemoryScanTypeIds.ChangedValue).IsMatch(
                StandardMemoryValueTypes.Float32,
                nanPayloadB,
                previousNan,
                Array.Empty<MemoryScanValue>(),
                architecture,
                MemoryScanStage.NextScan),
            "Changed Value did not detect a changed floating-point NaN payload.");
        AssertTrue(Get(StandardMemoryScanTypeIds.IncreasedBy).IsMatch(valueType, currentTwenty, ten, new[] { ten }, architecture, MemoryScanStage.NextScan), "Increased By did not match an exact +10 delta.");
        AssertTrue(Get(StandardMemoryScanTypeIds.DecreasedBy).IsMatch(valueType, currentTwenty, thirty, new[] { ten }, architecture, MemoryScanStage.NextScan), "Decreased By did not match an exact -10 delta.");

        AssertFalse(Get(StandardMemoryScanTypeIds.BiggerThan).SupportsValueType(StandardMemoryValueTypes.ByteArray), "Array of Bytes must not be offered for ordered Scan Types.");
        AssertFalse(Get(StandardMemoryScanTypeIds.IncreasedBy).SupportsValueType(StandardMemoryValueTypes.ByteArray), "Array of Bytes must not be offered for delta Scan Types.");

        return Task.CompletedTask;
    }

    private static Task VerifyNativeScanTypeMappingContractAsync()
    {
        NativeScanTypeMapping mapping = new(
            StandardMemoryScanTypeIds.ExactValue,
            "test.native.exact",
            availableForFirstScan: true,
            availableForNextScan: false,
            supportedValueTypeIds: new[] { StandardMemoryValueTypeIds.Int32 });

        AssertTrue(mapping.Supports(StandardMemoryScanTypeIds.ExactValue, StandardMemoryValueTypeIds.Int32, MemoryScanStage.FirstScan), "Native mapping rejected its declared Core Scan Type/value/stage combination.");
        AssertFalse(mapping.Supports(StandardMemoryScanTypeIds.ExactValue, StandardMemoryValueTypeIds.Int32, MemoryScanStage.NextScan), "Native mapping ignored its stage restriction.");
        AssertFalse(mapping.Supports(StandardMemoryScanTypeIds.ExactValue, StandardMemoryValueTypeIds.Int32, (MemoryScanStage)99), "Native mapping accepted an unknown scan stage.");
        AssertFalse(mapping.Supports(StandardMemoryScanTypeIds.ExactValue, StandardMemoryValueTypeIds.Float32, MemoryScanStage.FirstScan), "Native mapping ignored its Value Type restriction.");
        AssertFalse(mapping.Supports(StandardMemoryScanTypeIds.BiggerThan, StandardMemoryValueTypeIds.Int32, MemoryScanStage.FirstScan), "Native mapping ignored its Core Scan Type identity.");
        AssertEqual("test.native.exact", mapping.NativeScanTypeId, "Native mapping rewrote the plugin-owned native id.");

        return Task.CompletedTask;
    }

    private static async Task VerifyPluginDefinedScannerAsync()
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
        IMemoryWriter writer = session.GetRequiredService<IMemoryWriter>();
        IMemoryReader reader = session.GetRequiredService<IMemoryReader>();

        IMemoryValueType valueType = new TestByteValueType();
        IMemoryScanType scanType = new TestByteScanType();
        AssertTrue(
            valueType.TryParse("171", session.Architecture, out MemoryScanValue? input, out string parseError) && input is not null,
            $"Custom plugin value type could not parse its input: {parseError}");

        ulong address = checked(MockTargetLayout.BaseAddress + 0x1800UL);
        await writer.WriteAsync(process, address, input!.Bytes, CancellationToken.None).ConfigureAwait(false);

        MemoryScanExecutionResult result = await new MemoryScanner()
            .FirstScanAsync(
                process,
                regions,
                reader,
                session.Architecture,
                valueType,
                scanType,
                new[] { input },
                progress: null,
                CancellationToken.None)
            .ConfigureAwait(false);

        MemoryScanResult match = result.Results.Single(item => item.Address == address);
        AssertEqual(TestByteValueType.TypeId, match.ValueTypeId, "Core rejected or rewrote a plugin-defined Value Type id.");
        AssertEqual("171", match.CurrentValue.DisplayText, "Core did not use the plugin-defined value formatter.");
    }

    private static Task VerifyPluginScanCapabilitiesAsync()
    {
        MockTargetPlugin mock = new();
        Ps5TargetPlugin ps5 = new();

        string[] expectedValueTypeIds =
        {
            StandardMemoryValueTypeIds.UInt8,
            StandardMemoryValueTypeIds.Int8,
            StandardMemoryValueTypeIds.UInt16,
            StandardMemoryValueTypeIds.Int16,
            StandardMemoryValueTypeIds.UInt32,
            StandardMemoryValueTypeIds.Int32,
            StandardMemoryValueTypeIds.UInt64,
            StandardMemoryValueTypeIds.Int64,
            StandardMemoryValueTypeIds.Float32,
            StandardMemoryValueTypeIds.Float64,
            StandardMemoryValueTypeIds.ByteArray
        };

        AssertTrue(
            ps5.SupportedValueTypes.Select(item => item.Id).SequenceEqual(expectedValueTypeIds),
            "PS5 plugin does not declare the expected ps5debug-NG Value Types in UI order.");
        AssertTrue(
            mock.SupportedValueTypes.Select(item => item.Id).SequenceEqual(expectedValueTypeIds),
            "Mock plugin should expose the implemented reusable standard Value Types.");
        AssertEqual(StandardMemoryValueTypeIds.Int32, ps5.DefaultValueTypeId, "Unexpected PS5 default Value Type.");
        AssertEqual(StandardMemoryValueTypeIds.Int32, mock.DefaultValueTypeId, "Unexpected Mock default Value Type.");
        AssertEqual("1 Byte", StandardMemoryValueTypes.Int8.DisplayName, "Signed 1-byte display name should use the standard unsuffixed label.");
        AssertEqual("2 Bytes", StandardMemoryValueTypes.Int16.DisplayName, "Signed 2-byte display name should use the standard unsuffixed label.");
        AssertEqual("4 Bytes", StandardMemoryValueTypes.Int32.DisplayName, "Signed 4-byte display name should use the standard unsuffixed label.");
        AssertEqual("8 Bytes", StandardMemoryValueTypes.Int64.DisplayName, "Signed 8-byte display name should use the standard unsuffixed label.");
        AssertEqual("1 Byte (Unsigned)", StandardMemoryValueTypes.UInt8.DisplayName, "Unsigned 1-byte display name should remain explicit.");
        AssertEqual("2 Bytes (Unsigned)", StandardMemoryValueTypes.UInt16.DisplayName, "Unsigned 2-byte display name should remain explicit.");
        AssertEqual("4 Bytes (Unsigned)", StandardMemoryValueTypes.UInt32.DisplayName, "Unsigned 4-byte display name should remain explicit.");
        AssertEqual("8 Bytes (Unsigned)", StandardMemoryValueTypes.UInt64.DisplayName, "Unsigned 8-byte display name should remain explicit.");

        AssertTrue(
            ps5.SupportedScanOptions.Select(item => item.Id).SequenceEqual(new[]
            {
                StandardMemoryScanOptionIds.Endianness,
                StandardMemoryScanOptionIds.Alignment,
                StandardMemoryScanOptionIds.FloatingPointRounding
            }),
            "PS5 plugin does not expose the expected scan options in UI order.");
        AssertEqual(0, ((ITargetPlugin)mock).SupportedScanOptions.Count, "Mock plugin should rely on the API 2.0-compatible empty scan-option default.");

        IMemoryScanOption endianness = ps5.SupportedScanOptions[0];
        IMemoryScanOption alignment = ps5.SupportedScanOptions[1];
        IMemoryScanOption floatingPoint = ps5.SupportedScanOptions[2];
        AssertEqual(StandardMemoryScanOptionChoiceIds.LittleEndian, endianness.DefaultChoiceId, "Unexpected PS5 Endianness default.");
        AssertEqual(StandardMemoryScanOptionChoiceIds.DefaultAlignment, alignment.DefaultChoiceId, "Unexpected PS5 Alignment default.");
        AssertEqual(StandardMemoryScanOptionChoiceIds.FloatingPointStrict, floatingPoint.DefaultChoiceId, "Unexpected PS5 floating-point default.");
        AssertTrue(endianness.LockAfterFirstScan && alignment.LockAfterFirstScan,
            "PS5 Endianness and Alignment must lock after First Scan.");
        AssertFalse(floatingPoint.LockAfterFirstScan,
            "PS5 Floating-point rounding should remain configurable for later Exact Value Next Scans.");
        AssertTrue(floatingPoint.SupportsValueType(StandardMemoryValueTypes.Float32), "Floating-point option should support Float.");
        AssertTrue(floatingPoint.SupportsValueType(StandardMemoryValueTypes.Float64), "Floating-point option should support Double.");
        AssertFalse(floatingPoint.SupportsValueType(StandardMemoryValueTypes.Int32), "Floating-point option must not apply to Int32.");

        return Task.CompletedTask;
    }

    private static Task VerifyPluginScanOptionPresentationAsync()
    {
        Ps5TargetPlugin ps5 = new();
        IMemoryScanOption endianness = ps5.SupportedScanOptions.Single(option =>
            string.Equals(option.Id, StandardMemoryScanOptionIds.Endianness, StringComparison.OrdinalIgnoreCase));
        IMemoryScanOption floatingPoint = ps5.SupportedScanOptions.Single(option =>
            string.Equals(option.Id, StandardMemoryScanOptionIds.FloatingPointRounding, StringComparison.OrdinalIgnoreCase));

        IMemoryScanOptionPresentation endiannessPresentation = endianness as IMemoryScanOptionPresentation
            ?? throw new InvalidOperationException("PS5 Endianness should expose scan-option presentation metadata.");
        AssertEqual(MemoryScanOptionPresentationKind.Toggle, endiannessPresentation.PresentationKind,
            "PS5 Endianness should use the generic toggle presentation.");
        AssertEqual("Little-endian byte order", endiannessPresentation.ToggleLabel,
            "Unexpected PS5 Endianness toggle label.");
        AssertEqual(StandardMemoryScanOptionChoiceIds.LittleEndian, endiannessPresentation.CheckedChoiceId,
            "Checked Endianness state must select Little Endian.");
        AssertEqual(StandardMemoryScanOptionChoiceIds.BigEndian, endiannessPresentation.UncheckedChoiceId,
            "Unchecked Endianness state must select Big Endian.");

        IMemoryScanOptionApplicability floatingApplicability = floatingPoint as IMemoryScanOptionApplicability
            ?? throw new InvalidOperationException("PS5 Floating-point rounding should expose Scan Type applicability metadata.");
        IMemoryScanType exact = MemoryScanTypeCatalog.All.Single(scanType =>
            string.Equals(scanType.Id, StandardMemoryScanTypeIds.ExactValue, StringComparison.OrdinalIgnoreCase));
        IMemoryScanType fuzzy = MemoryScanTypeCatalog.All.Single(scanType =>
            string.Equals(scanType.Id, StandardMemoryScanTypeIds.FuzzyValue, StringComparison.OrdinalIgnoreCase));
        IMemoryScanType between = MemoryScanTypeCatalog.All.Single(scanType =>
            string.Equals(scanType.Id, StandardMemoryScanTypeIds.Between, StringComparison.OrdinalIgnoreCase));

        AssertTrue(floatingApplicability.SupportsScanType(exact, MemoryScanStage.FirstScan),
            "Floating-point rounding should be relevant to Exact Value First Scan.");
        AssertTrue(floatingApplicability.SupportsScanType(exact, MemoryScanStage.NextScan),
            "Floating-point rounding should be relevant to Exact Value Next Scan.");
        AssertFalse(floatingApplicability.SupportsScanType(fuzzy, MemoryScanStage.FirstScan),
            "Floating-point rounding must not be shown for Fuzzy Value.");
        AssertFalse(floatingApplicability.SupportsScanType(between, MemoryScanStage.NextScan),
            "Floating-point rounding must not be shown for Between.");

        return Task.CompletedTask;
    }

    private static async Task VerifySharedScanOptionsAsync()
    {
        TargetArchitecture littleEndian = new(CpuArchitecture.X64, 64, 64, Endianness.Little);
        TargetArchitecture bigEndian = new(CpuArchitecture.X64, 64, 64, Endianness.Big);

        MemoryScanValue bigEndianValue = ParseValue(StandardMemoryValueTypes.Int32, "16909060", bigEndian);
        AssertTrue(
            bigEndianValue.Bytes.Span.SequenceEqual(new byte[] { 0x01, 0x02, 0x03, 0x04 }),
            "Big-endian Int32 parsing did not preserve the selected scan byte order.");

        MemoryScanValue floatTarget = ParseValue(StandardMemoryValueTypes.Float32, "100", littleEndian);
        byte[] nearbyFloat = new byte[sizeof(float)];
        BinaryPrimitives.WriteInt32LittleEndian(nearbyFloat, BitConverter.SingleToInt32Bits(100.00005f));

        bool strictMatch = ExactScanType.IsMatch(
            StandardMemoryValueTypes.Float32,
            nearbyFloat,
            null,
            new[] { floatTarget },
            littleEndian,
            MemoryScanStage.FirstScan,
            new MemoryScanOptions(new[]
            {
                new KeyValuePair<string, string>(
                    StandardMemoryScanOptionIds.FloatingPointRounding,
                    StandardMemoryScanOptionChoiceIds.FloatingPointStrict)
            }));
        bool tolerantMatch = ExactScanType.IsMatch(
            StandardMemoryValueTypes.Float32,
            nearbyFloat,
            null,
            new[] { floatTarget },
            littleEndian,
            MemoryScanStage.FirstScan,
            new MemoryScanOptions(new[]
            {
                new KeyValuePair<string, string>(
                    StandardMemoryScanOptionIds.FloatingPointRounding,
                    StandardMemoryScanOptionChoiceIds.FloatingPointRelativeTolerance1E6)
            }));

        AssertFalse(strictMatch, "Strict floating-point matching accepted a nearby non-equal value.");
        AssertTrue(tolerantMatch, "ps5debug-NG-compatible floating-point tolerance rejected a nearby value.");

        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single();
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);
        IMemoryWriter writer = session.GetRequiredService<IMemoryWriter>();
        IMemoryReader reader = session.GetRequiredService<IMemoryReader>();
        MemoryScanValue unalignedValue = ParseValue(StandardMemoryValueTypes.Int32, "1357911", session.Architecture);
        ulong unalignedAddress = checked(MockTargetLayout.BaseAddress + 0x1801UL);
        await writer.WriteAsync(process, unalignedAddress, unalignedValue.Bytes, CancellationToken.None).ConfigureAwait(false);

        MemoryScanExecutionResult alignedOne = await new MemoryScanner().FirstScanAsync(
            process,
            regions,
            reader,
            session.Architecture,
            StandardMemoryValueTypes.Int32,
            ExactScanType,
            new[] { unalignedValue },
            new MemoryScanOptions(new[]
            {
                new KeyValuePair<string, string>(StandardMemoryScanOptionIds.Alignment, "1")
            }),
            progress: null,
            CancellationToken.None).ConfigureAwait(false);

        AssertTrue(alignedOne.Results.Any(item => item.Address == unalignedAddress),
            "Custom 1-byte alignment did not expose the deliberately unaligned Int32 candidate.");
    }

    private static Task VerifyMockPluginMetadataAsync()
    {
        MockTargetPlugin plugin = new();

        AssertEqual(MockPluginInfo.Id, plugin.Metadata.Id, "Unexpected plugin id.");
        AssertEqual("Development", plugin.Metadata.Platform, "Unexpected platform name.");
        AssertEqual(CpuArchitecture.Unknown, plugin.Metadata.Architecture.Cpu, "Mock Target should declare a custom/unknown CPU architecture rather than x64.");
        AssertEqual(0, plugin.ConnectionSettings.Count, "Mock plugin should not require connection settings.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.MemoryRead), "MemoryRead capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.MemoryWrite), "MemoryWrite capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.Disassembly), "Disassembly capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.Debugger), "Mock debugger capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.ThreadEnumeration), "Mock thread-enumeration capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.ThreadControl), "Mock thread-control capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.RegisterAccess), "Mock register-access capability is missing.");

        return Task.CompletedTask;
    }

    private static async Task VerifyMockDebuggerProviderAsync()
    {
        MockTargetPlugin plugin = new();
        AssertEqual("1.0.0.rev10", plugin.Metadata.DisplayVersion, "Unexpected Mock debugger plugin revision.");
        AssertEqual(new Version(2, 13, 0), plugin.Metadata.ApiVersion, "Mock debugger backend must target Plugin API 2.13.0.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.Debugger), "Mock debugger capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.ThreadEnumeration), "Mock thread-enumeration capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.ThreadControl), "Mock thread-control capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.RegisterAccess), "Mock rev7 register-access capability is missing.");
        AssertFalse(plugin.Capabilities.HasFlag(TargetCapabilities.Breakpoints), "Mock rev7 should not advertise breakpoints yet.");

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = (await targetSession
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single();

        IDebuggerProvider provider = targetSession.GetRequiredService<IDebuggerProvider>();
        await using IDebuggerSession debugger = await provider
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(DebuggerExecutionState.Running, debugger.State, "Mock debugger did not begin in the running state after attach.");
        AssertEqual(process.Id, debugger.Process.Id, "Mock debugger attached to an unexpected process id.");
        AssertEqual(process.Name, debugger.Process.Name, "Mock debugger attached to an unexpected process name.");
        AssertTrue(debugger.GetService<IDebuggerThreadService>() is not null, "Mock did not expose its thread-enumeration service.");
        AssertTrue(debugger.GetService<IDebuggerThreadControlService>() is not null, "Mock did not expose its thread-control service.");
        AssertTrue(debugger.GetService<IDebuggerRegisterService>() is not null, "Mock rev7 did not expose its register service.");

        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Detached, debugger.State, "Mock debugger did not enter Detached state.");
    }

    private static async Task VerifyMockDebuggerEventFlowAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession targetSession = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = (await targetSession
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single();
        IDebuggerProvider provider = targetSession.GetRequiredService<IDebuggerProvider>();
        await using IDebuggerSession debugger = await provider
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        List<DebuggerEvent> events = new();
        debugger.EventReceived += (_, args) => events.Add(args.Event);

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Paused, debugger.State, "Mock debugger did not pause deterministically.");
        AssertEqual(1, events.Count, "Mock debugger did not emit exactly one pause event.");
        AssertEqual(DebuggerEventKind.Paused, events[0].Kind, "Mock pause event kind changed unexpectedly.");
        AssertEqual(DebuggerStopReason.PauseRequested, events[0].StopReason, "Mock pause event lost its stop reason.");
        AssertEqual<ulong?>(1UL, events[0].ThreadId, "Mock pause event lost its deterministic thread id.");
        AssertEqual<ulong?>(MockTargetLayout.CodeAddress, events[0].InstructionPointer, "Mock pause event lost its deterministic instruction pointer.");
        AssertEqual("Mock target paused.", events[0].Message, "Mock pause event message changed unexpectedly.");

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Running, debugger.State, "Mock debugger did not continue deterministically.");
        AssertEqual(2, events.Count, "Mock debugger did not emit exactly one continue event.");
        AssertEqual(DebuggerEventKind.Resumed, events[1].Kind, "Mock continue event kind changed unexpectedly.");
        AssertEqual(DebuggerStopReason.None, events[1].StopReason, "Mock continue event reported an unexpected stop reason.");
        AssertEqual<ulong?>(MockTargetLayout.CodeAddress, events[1].InstructionPointer, "Mock continue event lost its deterministic instruction pointer.");
        AssertEqual("Mock target resumed.", events[1].Message, "Mock continue event message changed unexpectedly.");

        await AssertThrowsAsync<InvalidOperationException>(
            () => debugger.ContinueAsync(CancellationToken.None),
            "Mock debugger accepted Continue while already running.").ConfigureAwait(false);
    }

    private static async Task VerifyMockDebuggerThreadServicesAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession targetSession = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = (await targetSession
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single();
        await using IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        IDebuggerThreadService threadService = debugger.GetRequiredService<IDebuggerThreadService>();
        IDebuggerThreadControlService threadControl = debugger.GetRequiredService<IDebuggerThreadControlService>();

        IReadOnlyList<DebuggerThreadInfo> threads = await threadService
            .GetThreadsAsync(CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(3, threads.Count, "Mock debugger did not expose the deterministic rev6 thread set.");
        AssertEqual("Main", threads.Single(thread => thread.Id == 1).Name, "Mock main thread name changed unexpectedly.");
        AssertTrue(threads.All(thread => thread.State == DebuggerThreadState.Running), "Mock attached threads did not begin in Running state.");

        await threadControl.SuspendThreadAsync(2, CancellationToken.None).ConfigureAwait(false);
        threads = await threadService.GetThreadsAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerThreadState.Suspended, threads.Single(thread => thread.Id == 2).State, "Mock thread suspend was not reflected by enumeration.");

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        threads = await threadService.GetThreadsAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerThreadState.Stopped, threads.Single(thread => thread.Id == 1).State, "Mock non-suspended thread did not map to Stopped while the target was paused.");
        AssertEqual(DebuggerThreadState.Suspended, threads.Single(thread => thread.Id == 2).State, "Mock suspended thread lost its individual state while the target was paused.");

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        await threadControl.ResumeThreadAsync(2, CancellationToken.None).ConfigureAwait(false);
        threads = await threadService.GetThreadsAsync(CancellationToken.None).ConfigureAwait(false);
        AssertTrue(threads.All(thread => thread.State == DebuggerThreadState.Running), "Mock thread resume did not restore the deterministic Running state.");
    }

    private static async Task VerifyMockDebuggerRegisterServicesAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession targetSession = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = (await targetSession
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single();
        await using IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        IDebuggerRegisterService registerService = debugger.GetRequiredService<IDebuggerRegisterService>();
        await AssertThrowsAsync<InvalidOperationException>(
            () => registerService.GetRegistersAsync(1, CancellationToken.None),
            "Mock debugger exposed register snapshots while the target was running.").ConfigureAwait(false);

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        IReadOnlyList<DebuggerRegister> registers = await registerService
            .GetRegistersAsync(1, CancellationToken.None)
            .ConfigureAwait(false);

        DebuggerRegister instructionPointer = registers.Single(item => item.Role == DebuggerRegisterRole.InstructionPointer);
        AssertEqual(MockTargetLayout.CodeAddress, BinaryPrimitives.ReadUInt64LittleEndian(instructionPointer.Value.Span),
            "Mock instruction-pointer register did not match the deterministic pause location.");
        AssertEqual(DebuggerRegisterValueEncoding.UnsignedLittleEndian, instructionPointer.ValueEncoding,
            "Mock register snapshot did not declare its neutral numeric encoding.");
        AssertTrue(registers.Any(item => item.Role == DebuggerRegisterRole.StackPointer),
            "Mock register snapshot is missing the semantic stack-pointer role.");
        AssertTrue(registers.Any(item => item.Role == DebuggerRegisterRole.FramePointer),
            "Mock register snapshot is missing the semantic frame-pointer role.");
        AssertTrue(registers.All(item => item.CanWrite),
            "Mock register snapshot should remain fully writable for deterministic write-path testing.");

        const ulong replacement = 0x1122334455667788UL;
        byte[] replacementBytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(replacementBytes, replacement);
        await registerService
            .WriteRegisterAsync(1, new DebuggerRegisterWriteRequest("rax", replacementBytes), CancellationToken.None)
            .ConfigureAwait(false);
        registers = await registerService.GetRegistersAsync(1, CancellationToken.None).ConfigureAwait(false);
        DebuggerRegister updated = registers.Single(item => item.Id == "rax");
        AssertEqual(replacement, BinaryPrimitives.ReadUInt64LittleEndian(updated.Value.Span),
            "Mock register write did not survive immediate read-back.");

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        await AssertThrowsAsync<InvalidOperationException>(
            () => registerService.GetRegistersAsync(1, CancellationToken.None),
            "Mock debugger retained a usable register snapshot after the target resumed.").ConfigureAwait(false);
    }

    private static async Task VerifyMockDebuggerAttachmentIsolationAsync()
    {
        MockTargetPlugin plugin = new();
        ITargetSession targetSession = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = (await targetSession
                .GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single();
        IDebuggerProvider provider = targetSession.GetRequiredService<IDebuggerProvider>();
        IDebuggerSession first = await provider
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        await AssertThrowsAsync<InvalidOperationException>(
            async () =>
            {
                await provider.AttachAsync(process, CancellationToken.None).ConfigureAwait(false);
            },
            "Mock debugger allowed two simultaneous attachments to the same target.").ConfigureAwait(false);

        await AssertThrowsAsync<ArgumentException>(
            async () =>
            {
                await provider
                    .AttachAsync(new TargetProcess(process.Id + 1, process.Name), CancellationToken.None)
                    .ConfigureAwait(false);
            },
            "Mock debugger accepted a process that does not belong to its target session.").ConfigureAwait(false);

        await first.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await first.DisposeAsync().ConfigureAwait(false);
        await using IDebuggerSession second = await provider
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Running, second.State, "Mock debugger could not reattach after a completed detach/dispose cycle.");

        await targetSession.DisposeAsync().ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Detached, second.State, "Target-session disposal did not dispose the active Mock debugger session.");
        AssertThrows<ObjectDisposedException>(
            () => targetSession.GetService<IDebuggerProvider>(),
            "Disposed Mock target session continued exposing debugger services.");
    }

    private static Task VerifyDebuggerWorkspaceSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string windowXaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string viewModelSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerViewModel.cs"));
        string mainWindowXaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml"));
        string mainWindowSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml.cs"));

        foreach (string command in new[] { "AttachCommand", "PauseCommand", "ContinueCommand", "DetachCommand", "ClearEventsCommand" })
        {
            AssertTrue(
                windowXaml.Contains($"Command=\"{{Binding {command}}}\"", StringComparison.Ordinal),
                $"Debugger workspace does not bind {command}.");
        }

        foreach (string column in new[] { "Sequence", "Time", "Kind", "State", "StopReason", "Thread", "InstructionPointer", "Message" })
        {
            AssertTrue(
                windowXaml.Contains($"Binding=\"{{Binding {column}}}\"", StringComparison.Ordinal),
                $"Debugger event table does not expose the {column} column.");
        }

        AssertTrue(
            mainWindowXaml.Contains("SelectedPlugin.SupportsDebugger", StringComparison.Ordinal) &&
            mainWindowXaml.Contains("SelectedPlugin.CanOpenDebugger", StringComparison.Ordinal) &&
            mainWindowXaml.Contains("OpenDebuggerButton_Click", StringComparison.Ordinal),
            "Main workspace does not expose a capability-driven Debugger entry point.");
        AssertTrue(
            mainWindowSource.Contains("new DebuggerViewModel", StringComparison.Ordinal) &&
            mainWindowSource.Contains("plugin.ConnectionGeneration", StringComparison.Ordinal),
            "Debugger window is not bound to the current target connection generation.");
        AssertTrue(
            viewModelSource.Contains("DebuggerSessionCoordinator", StringComparison.Ordinal) &&
            viewModelSource.Contains("context.Identity.Matches", StringComparison.Ordinal) &&
            viewModelSource.Contains("IsCurrentDebuggerTarget", StringComparison.Ordinal),
            "Debugger workspace does not route lifecycle/events through the shared coordinator and target identity checks.");

        string combined = string.Concat(windowXaml, viewModelSource, mainWindowSource);
        foreach (string forbidden in new[] { "ps5debug", "Ps5Debug", "RIP", "RSP", "RBP", "x86", "x64" })
        {
            string pattern = $@"(?<![A-Za-z0-9_]){Regex.Escape(forbidden)}(?![A-Za-z0-9_])";
            AssertFalse(
                Regex.IsMatch(combined, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                $"Debugger host workspace leaked platform-specific token '{forbidden}'.");
        }

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerThreadWorkspaceSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string windowXaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string viewModelSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerViewModel.cs"));

        foreach (string binding in new[]
        {
            "Threads",
            "SelectedThread",
            "RefreshThreadsCommand",
            "SuspendThreadCommand",
            "ResumeThreadCommand",
            "HasThreadEnumerationCapability",
            "HasThreadControlCapability"
        })
        {
            AssertTrue(
                windowXaml.Contains(binding, StringComparison.Ordinal),
                $"Debugger thread workspace is missing binding '{binding}'.");
        }

        foreach (string column in new[] { "IdText", "Name", "State" })
        {
            AssertTrue(
                windowXaml.Contains($"Binding=\"{{Binding {column}}}\"", StringComparison.Ordinal),
                $"Debugger thread table does not expose the neutral {column} field.");
        }

        AssertTrue(
            viewModelSource.Contains("IDebuggerThreadService", StringComparison.Ordinal) &&
            viewModelSource.Contains("IDebuggerThreadControlService", StringComparison.Ordinal) &&
            viewModelSource.Contains("TargetCapabilities.ThreadEnumeration", StringComparison.Ordinal) &&
            viewModelSource.Contains("TargetCapabilities.ThreadControl", StringComparison.Ordinal),
            "Debugger thread workspace does not require neutral thread services and matching capabilities.");
        AssertTrue(
            viewModelSource.Contains("preferredThreadId", StringComparison.Ordinal) &&
            viewModelSource.Contains("Threads.FirstOrDefault(thread => thread.Id == selectedId.Value)", StringComparison.Ordinal),
            "Debugger thread refresh does not preserve selection by neutral thread id.");

        string combined = string.Concat(windowXaml, viewModelSource);
        foreach (string forbidden in new[] { "LWP", "ptrace", "PS5", "ps5debug", "WindowsThread", "HANDLE" })
        {
            string pattern = $@"(?<![A-Za-z0-9_]){Regex.Escape(forbidden)}(?![A-Za-z0-9_])";
            AssertFalse(
                Regex.IsMatch(combined, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                $"Debugger thread host UI leaked platform-specific token '{forbidden}'.");
        }

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerRegisterWorkspaceSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string windowXaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string viewModelSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerViewModel.cs"));
        string registerViewModelSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerRegisterViewModel.cs"));

        foreach (string binding in new[]
        {
            "Registers",
            "SelectedRegister",
            "RefreshRegistersCommand",
            "HasRegisterAccessCapability",
            "CurrentInstructionPointerText",
            "CanEditSelectedRegister",
            "CanNavigateToCurrentInstruction"
        })
        {
            AssertTrue(windowXaml.Contains(binding, StringComparison.Ordinal),
                $"Debugger register workspace is missing binding '{binding}'.");
        }

        AssertTrue(
            viewModelSource.Contains("IDebuggerRegisterService", StringComparison.Ordinal) &&
            viewModelSource.Contains("TargetCapabilities.RegisterAccess", StringComparison.Ordinal) &&
            viewModelSource.Contains("DebuggerRegisterRole.InstructionPointer", StringComparison.Ordinal),
            "Debugger register workspace is not driven by the neutral register service, capability, and semantic instruction-pointer role.");
        AssertTrue(
            viewModelSource.Contains("WriteSelectedRegisterAsync", StringComparison.Ordinal) &&
            viewModelSource.Contains("read-back verification", StringComparison.OrdinalIgnoreCase),
            "Debugger register editing does not retain the write-and-read-back verification path.");
        AssertTrue(
            registerViewModelSource.Contains("DebuggerRegisterValueCodec.Format", StringComparison.Ordinal),
            "Register row presentation bypasses the neutral register-value formatter.");

        string combined = string.Concat(windowXaml, viewModelSource, registerViewModelSource);
        foreach (string forbidden in new[] { "LWP", "ptrace", "PS5", "ps5debug", "RIP", "RAX", "x86", "x64" })
        {
            string pattern = $@"(?<![A-Za-z0-9_]){Regex.Escape(forbidden)}(?![A-Za-z0-9_])";
            AssertFalse(
                Regex.IsMatch(combined, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                $"Debugger register host UI leaked platform-specific token '{forbidden}'.");
        }

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerRegisterValueCodecSourceAsync()
    {
        string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerRegisterValueCodec.cs"));
        AssertTrue(
            source.Contains("DebuggerRegisterValueEncoding.Bytes", StringComparison.Ordinal) &&
            source.Contains("DebuggerRegisterValueEncoding.UnsignedLittleEndian", StringComparison.Ordinal) &&
            source.Contains("DebuggerRegisterValueEncoding.UnsignedBigEndian", StringComparison.Ordinal),
            "Register formatter/parser does not cover all neutral value encodings.");
        AssertTrue(
            source.Contains("BigInteger", StringComparison.Ordinal) &&
            source.Contains("register.BitWidth", StringComparison.Ordinal) &&
            source.Contains("TryGetUInt64", StringComparison.Ordinal),
            "Register codec is not width-aware or does not provide neutral semantic-address extraction.");
        AssertTrue(
            source.Contains("\"0\" + compact", StringComparison.Ordinal),
            "Unsigned hexadecimal parsing does not guard against BigInteger two's-complement interpretation when the top hex bit is set.");

        foreach (string forbidden in new[] { "RIP", "RSP", "RBP", "RAX", "PS5", "ptrace", "x86", "x64" })
        {
            AssertFalse(source.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"Register value codec leaked platform-specific token '{forbidden}'.");
        }

        return Task.CompletedTask;
    }

    private static Task VerifyRegisterEditDialogSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string xaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "RegisterEditDialog.xaml"));
        string source = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "RegisterEditDialog.xaml.cs"));

        AssertTrue(xaml.Contains("Write &amp; Verify", StringComparison.Ordinal),
            "Register edit dialog is missing the explicit Write & Verify action.");
        AssertTrue(
            source.Contains("DebuggerRegisterValueCodec.TryParse", StringComparison.Ordinal) &&
            source.Contains("_register.CanWrite", StringComparison.Ordinal) &&
            source.Contains("Change the value before writing", StringComparison.Ordinal),
            "Register edit dialog bypasses neutral parsing, write capability, or unchanged-value protection.");

        string combined = string.Concat(xaml, source);
        foreach (string forbidden in new[] { "RIP", "RAX", "PS5", "ptrace", "x86", "x64" })
        {
            AssertFalse(combined.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"Register edit dialog leaked platform-specific token '{forbidden}'.");
        }

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerCurrentInstructionIntegrationSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string windowSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml.cs"));
        string viewModelSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerViewModel.cs"));
        string mainWindowSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml.cs"));

        AssertTrue(
            windowSource.Contains("CurrentInstructionPointer", StringComparison.Ordinal) &&
            windowSource.Contains("_canOpenDisassembler", StringComparison.Ordinal) &&
            windowSource.Contains("_openDisassembler", StringComparison.Ordinal),
            "Debugger window does not route current-instruction navigation through host callbacks.");
        AssertTrue(
            mainWindowSource.Contains("CanOpenDisassemblerFromDebugger", StringComparison.Ordinal) &&
            mainWindowSource.Contains("OpenDisassemblerFromDebugger", StringComparison.Ordinal) &&
            mainWindowSource.Contains("OpenDisassembler(plugin, targetProcess, address)", StringComparison.Ordinal),
            "Debugger current-instruction navigation does not reuse the existing MainWindow Disassembler launcher.");
        AssertTrue(
            viewModelSource.Contains("DebuggerRegisterRole.InstructionPointer", StringComparison.Ordinal) &&
            !viewModelSource.Contains("register.Id == \"rip\"", StringComparison.OrdinalIgnoreCase),
            "Debugger current-instruction resolution is tied to a platform register name instead of the semantic role.");
        AssertTrue(
            viewModelSource.Contains("_plugin.CanOpenDisassemblerForTarget(_targetProcess)", StringComparison.Ordinal),
            "Debugger current-instruction navigation is not gated by the target's normal Disassembler availability.");
        AssertTrue(
            viewModelSource.Contains("? address\n                : null;", StringComparison.Ordinal),
            "Debugger current-instruction state is not cleared when a refreshed register snapshot has no semantic instruction pointer.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerTargetLifetimeSourceAsync()
    {
        string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PluginViewModel.cs"));

        AssertTrue(
            source.Contains("HashSet<DebuggerSessionCoordinator> _debuggerSessions", StringComparison.Ordinal),
            "PluginViewModel does not track active debugger coordinators.");
        AssertTrue(
            source.Contains("CreateDebuggerSessionCoordinator", StringComparison.Ordinal) &&
            source.Contains("ReleaseDebuggerSessionAsync", StringComparison.Ordinal) &&
            source.Contains("DisposeDebuggerSessionsAsync", StringComparison.Ordinal),
            "Debugger coordinator ownership helpers are missing from the target host.");
        AssertTrue(
            source.Contains("await DisposeDebuggerSessionsAsync().ConfigureAwait(true);\n                await _session.DisposeAsync().ConfigureAwait(true);", StringComparison.Ordinal),
            "Target disconnect/replacement does not dispose debugger sessions before the target session.");
        AssertTrue(
            source.Contains("await DisposeDebuggerSessionsAsync().ConfigureAwait(true);\n            await ResetNativeScanSessionIfAvailableAsync", StringComparison.Ordinal),
            "Active Target changes do not dispose debugger sessions before switching target context.");
        AssertTrue(
            source.Contains("connectionGeneration == _connectionGeneration", StringComparison.Ordinal) &&
            source.Contains("AreSameProcess(activeProcess.Process, targetProcess)", StringComparison.Ordinal),
            "Debugger target validation does not bind process identity to connection generation.");

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

    private static Task VerifyDisassemblyInstructionModelAsync()
    {
        byte[] sourceBytes = { 0x20, 0x04 };
        DisassembledInstruction instruction = new(
            0x1000,
            sourceBytes,
            "call",
            "0x1006",
            DisassemblyFlowControl.Call,
            branchTarget: 0x1006,
            isValid: true);

        sourceBytes[0] = 0xFF;

        AssertEqual(0x1000UL, instruction.Address, "Instruction address changed unexpectedly.");
        AssertEqual(2, instruction.Length, "Instruction length did not match its raw-byte count.");
        AssertEqual((byte)0x20, instruction.RawBytes.Span[0], "Instruction raw bytes were not defensively copied.");
        AssertEqual("call", instruction.Mnemonic, "Instruction mnemonic changed unexpectedly.");
        AssertEqual("0x1006", instruction.Operands, "Instruction operands changed unexpectedly.");
        AssertEqual(DisassemblyFlowControl.Call, instruction.FlowControl, "Instruction flow-control classification changed unexpectedly.");
        AssertEqual<ulong?>(0x1006UL, instruction.BranchTarget, "Instruction direct target changed unexpectedly.");
        AssertTrue(instruction.IsValid, "Valid instruction was marked invalid.");

        DisassembledInstruction invalid = new(
            0x2000,
            new byte[] { 0xFF },
            "invalid",
            string.Empty,
            DisassemblyFlowControl.Other,
            branchTarget: null,
            isValid: false);
        AssertFalse(invalid.IsValid, "Invalid instruction did not preserve its validity state.");

        AssertThrows<ArgumentException>(
            () => _ = new DisassembledInstruction(
                0x3000,
                new byte[] { 0x40 },
                "return",
                string.Empty,
                DisassemblyFlowControl.Return,
                branchTarget: 0x4000),
            "Instruction model accepted a direct branch target on a non-branch flow-control type.");

        return Task.CompletedTask;
    }

    private static Task VerifyDisassemblySyntaxTokenModelAsync()
    {
        List<DisassemblyTextToken> sourceTokens = new()
        {
            new("call", DisassemblyTextTokenKind.FlowControlMnemonic),
            new(" ", DisassemblyTextTokenKind.Text),
            new("rax", DisassemblyTextTokenKind.Register),
            new(",", DisassemblyTextTokenKind.Text),
            new("5", DisassemblyTextTokenKind.Number)
        };

        DisassembledInstruction instruction = new(
            0x4000,
            new byte[] { 0x90 },
            "call",
            "rax,5",
            DisassemblyFlowControl.Call,
            branchTarget: null,
            isValid: true,
            syntaxTokens: sourceTokens);

        sourceTokens.Clear();

        AssertEqual(5, instruction.SyntaxTokens.Count,
            "Instruction syntax tokens were not defensively copied from the provider collection.");
        AssertEqual(DisassemblyTextTokenKind.FlowControlMnemonic, instruction.SyntaxTokens[0].Kind,
            "Flow-control mnemonic token kind changed unexpectedly.");
        AssertEqual(DisassemblyTextTokenKind.Register, instruction.SyntaxTokens[2].Kind,
            "Register token kind changed unexpectedly.");
        AssertEqual(DisassemblyTextTokenKind.Number, instruction.SyntaxTokens[^1].Kind,
            "Number token kind changed unexpectedly.");
        AssertEqual(
            "call rax,5",
            string.Concat(instruction.SyntaxTokens.Select(token => token.Text)),
            "Syntax token text did not preserve the provider-formatted instruction.");

        DisassembledInstruction legacyConstructor = new(
            0x5000,
            new byte[] { 0x90 },
            "nop",
            string.Empty);
        AssertEqual(0, legacyConstructor.SyntaxTokens.Count,
            "The legacy instruction constructor unexpectedly fabricated syntax metadata.");

        AssertThrows<ArgumentException>(
            () => _ = new DisassemblyTextToken(string.Empty, DisassemblyTextTokenKind.Text),
            "The syntax token model accepted an empty token.");

        return Task.CompletedTask;
    }

    private static async Task VerifyMockDisassemblyProviderAsync()
    {
        MockTargetPlugin plugin = new();
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.Disassembly),
            "Mock plugin does not advertise its disassembly provider.");

        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        IDisassemblerProvider provider = session.GetRequiredService<IDisassemblerProvider>();
        AssertTrue(provider.SupportsArchitecture(session.Architecture),
            "Mock disassembly provider rejected the Mock Target architecture.");

        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single();
        byte[] codeBytes = new byte[MockTargetLayout.CodeBytes.Length];
        int bytesRead = await session.GetRequiredService<IMemoryReader>()
            .ReadAsync(process, MockTargetLayout.CodeAddress, codeBytes, CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(codeBytes.Length, bytesRead, "Mock code fixture read returned an unexpected byte count.");
        AssertTrue(codeBytes.AsSpan().SequenceEqual(MockTargetLayout.CodeBytes.Span),
            "Mock target did not expose the deterministic disassembly byte fixture.");

        IReadOnlyList<DisassembledInstruction> instructions = await provider
            .DisassembleAsync(MockTargetLayout.CodeAddress, codeBytes, session.Architecture, CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(7, instructions.Count, "Mock disassembly fixture produced an unexpected instruction count.");
        AssertEqual("load", instructions[0].Mnemonic, "Mock fixture first instruction was decoded incorrectly.");
        AssertTrue(instructions[0].SyntaxTokens.Any(token => token.Kind == DisassemblyTextTokenKind.Register),
            "Mock fixture did not expose neutral register syntax metadata.");
        AssertTrue(instructions[0].SyntaxTokens.Any(token => token.Kind == DisassemblyTextTokenKind.Number),
            "Mock fixture did not expose neutral number syntax metadata.");
        AssertEqual(
            $"{instructions[0].Mnemonic} {instructions[0].Operands}",
            string.Concat(instructions[0].SyntaxTokens.Select(token => token.Text)),
            "Mock syntax tokens changed the rendered instruction text.");
        AssertEqual("call", instructions[1].Mnemonic, "Mock fixture call instruction was decoded incorrectly.");
        AssertEqual(DisassemblyTextTokenKind.FlowControlMnemonic, instructions[1].SyntaxTokens[0].Kind,
            "Mock call mnemonic was not classified as flow control for presentation.");
        AssertEqual(DisassemblyFlowControl.Call, instructions[1].FlowControl, "Mock call flow-control type was incorrect.");
        AssertEqual<ulong?>(MockTargetLayout.CodeAddress + 8UL, instructions[1].BranchTarget,
            "Mock call direct target was resolved incorrectly.");
        AssertEqual(DisassemblyFlowControl.ConditionalJump, instructions[3].FlowControl,
            "Mock conditional jump flow-control type was incorrect.");
        AssertEqual<ulong?>(MockTargetLayout.CodeAddress + 10UL, instructions[3].BranchTarget,
            "Mock conditional jump direct target was resolved incorrectly.");
        AssertEqual(DisassemblyFlowControl.Return, instructions[^1].FlowControl,
            "Mock fixture final return instruction was decoded incorrectly.");
    }

    private static async Task VerifyDisassemblyBoundedReadAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single();
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);

        DisassemblySnapshot snapshot = await new DisassemblyReader()
            .ReadAsync(
                session.GetRequiredService<IMemoryReader>(),
                session.GetRequiredService<IDisassemblerProvider>(),
                process,
                regions,
                session.Architecture,
                MockTargetLayout.CodeAddress,
                MockTargetLayout.CodeBytes.Length,
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(MockTargetLayout.CodeAddress, snapshot.RequestedAddress,
            "Disassembly snapshot lost the requested origin address.");
        AssertEqual(MockTargetLayout.CodeAddress, snapshot.StartAddress,
            "Disassembly snapshot did not start at the requested decode address.");
        AssertEqual(MockTargetLayout.CodeBytes.Length, snapshot.Bytes.Length,
            "Disassembly read did not remain bounded to the requested byte count.");
        AssertTrue(snapshot.Bytes.Span.SequenceEqual(MockTargetLayout.CodeBytes.Span),
            "Disassembly snapshot bytes changed between the memory reader and Core result.");
        AssertEqual(7, snapshot.Instructions.Count, "Core disassembly snapshot lost decoded instructions.");
        AssertEqual(regions[0], snapshot.Region, "Disassembly snapshot lost containing-region context.");
        AssertEqual(session.Architecture, snapshot.Architecture, "Disassembly snapshot lost target architecture context.");

        ulong previousEnd = snapshot.StartAddress;
        foreach (DisassembledInstruction instruction in snapshot.Instructions)
        {
            AssertTrue(instruction.Address >= previousEnd,
                "Core disassembly snapshot returned instructions out of address order.");
            previousEnd = checked(instruction.Address + (ulong)instruction.Length);
        }
    }

    private static async Task VerifyDisassemblyBidirectionalContextAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single();
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);

        DisassemblyReader reader = new();
        DisassemblySnapshot centered = await reader
            .ReadAroundAsync(
                session.GetRequiredService<IMemoryReader>(),
                session.GetRequiredService<IDisassemblerProvider>(),
                process,
                regions,
                session.Architecture,
                MockTargetLayout.CodeAddress,
                DisassemblyReader.DefaultContextByteCount,
                DisassemblyReader.DefaultContextByteCount,
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(
            MockTargetLayout.CodeAddress - (ulong)DisassemblyReader.DefaultContextByteCount,
            centered.StartAddress,
            "Bidirectional disassembly did not include the configured pre-origin context.");
        AssertEqual(MockTargetLayout.CodeAddress, centered.RequestedAddress,
            "Bidirectional disassembly lost the requested origin address.");
        AssertEqual(
            DisassemblyReader.DefaultContextByteCount * 2,
            centered.Bytes.Length,
            "Bidirectional disassembly did not return the expected 512-before/512-after context size.");
        AssertEqual(
            MockTargetLayout.CodeAddress + (ulong)DisassemblyReader.DefaultContextByteCount,
            centered.EndAddressExclusive,
            "Bidirectional disassembly did not end at the expected post-origin boundary.");
        AssertTrue(
            centered.Instructions.Any(instruction =>
                instruction.Address == MockTargetLayout.CodeAddress &&
                string.Equals(instruction.Mnemonic, "load", StringComparison.Ordinal)),
            "Bidirectional disassembly did not preserve exact-origin forward decoding.");

        MemoryRegion nearStartRegion = new(
            MockTargetLayout.CodeAddress,
            1024,
            MemoryProtection.Read | MemoryProtection.Execute,
            "Mock Near-Start Code",
            MockTargetLayout.ProcessName);
        ulong nearStartOrigin = MockTargetLayout.CodeAddress + 2UL;

        DisassemblySnapshot nearStart = await reader
            .ReadAroundAsync(
                session.GetRequiredService<IMemoryReader>(),
                session.GetRequiredService<IDisassemblerProvider>(),
                process,
                new[] { nearStartRegion },
                session.Architecture,
                nearStartOrigin,
                DisassemblyReader.DefaultContextByteCount,
                DisassemblyReader.DefaultContextByteCount,
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(nearStartRegion.BaseAddress, nearStart.StartAddress,
            "Bidirectional disassembly crossed the containing region while collecting pre-origin context.");
        AssertEqual(nearStartOrigin, nearStart.RequestedAddress,
            "Near-start bidirectional disassembly lost the requested origin address.");
        AssertEqual(
            2 + DisassemblyReader.DefaultContextByteCount,
            nearStart.Bytes.Length,
            "Bidirectional disassembly did not independently clamp the unavailable pre-origin context.");
        AssertTrue(
            nearStart.RequestedAddress >= nearStart.StartAddress &&
            nearStart.RequestedAddress < nearStart.EndAddressExclusive,
            "Bidirectional disassembly returned a snapshot that does not contain its requested origin.");
    }

    private static async Task VerifyDisassemblyContinuousOriginResolutionAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single();
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);

        ulong originInsideInstruction = MockTargetLayout.CodeAddress + 1UL;
        DisassemblySnapshot snapshot = await new DisassemblyReader()
            .ReadAroundAsync(
                session.GetRequiredService<IMemoryReader>(),
                session.GetRequiredService<IDisassemblerProvider>(),
                process,
                regions,
                session.Architecture,
                originInsideInstruction,
                beforeByteCount: 1,
                afterByteCount: 4,
                cancellationToken: CancellationToken.None)
            .ConfigureAwait(false);

        DisassembledInstruction? containingInstruction = snapshot.Instructions.SingleOrDefault(instruction =>
            instruction.Address <= originInsideInstruction &&
            checked(instruction.Address + (ulong)instruction.Length) > originInsideInstruction);

        AssertTrue(containingInstruction is not null,
            "Continuous disassembly did not return an instruction containing an origin that falls inside an instruction.");
        DisassembledInstruction resolvedInstruction = containingInstruction!;
        AssertEqual(MockTargetLayout.CodeAddress, resolvedInstruction.Address,
            "Continuous disassembly did not preserve the instruction start before the requested origin.");
        AssertEqual(2, resolvedInstruction.Length,
            "Continuous disassembly split the instruction that contains the requested origin.");
        AssertEqual("load", resolvedInstruction.Mnemonic,
            "Continuous disassembly changed the instruction containing the requested origin.");
        AssertFalse(snapshot.Instructions.Any(instruction => instruction.Address == originInsideInstruction),
            "Continuous disassembly incorrectly started a second decode stream at an origin inside an instruction.");
    }

    private static async Task VerifyDisassemblyRegionBoundaryAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single();
        MemoryRegion boundedRegion = new(
            MockTargetLayout.CodeAddress,
            5,
            MemoryProtection.Read | MemoryProtection.Execute,
            "Mock Bounded Code",
            MockTargetLayout.ProcessName);

        DisassemblySnapshot snapshot = await new DisassemblyReader()
            .ReadAsync(
                session.GetRequiredService<IMemoryReader>(),
                session.GetRequiredService<IDisassemblerProvider>(),
                process,
                new[] { boundedRegion },
                session.Architecture,
                MockTargetLayout.CodeAddress,
                DisassemblyReader.DefaultWindowByteCount,
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(5, snapshot.Bytes.Length,
            "Disassembly read crossed the containing memory-region boundary.");
        AssertEqual(boundedRegion.EndAddressExclusive, snapshot.EndAddressExclusive,
            "Disassembly snapshot end did not clamp to the containing region.");
        AssertEqual(3, snapshot.Instructions.Count,
            "Boundary-truncated mock code produced an unexpected instruction count.");
        AssertFalse(snapshot.Instructions[^1].IsValid,
            "A boundary-truncated two-byte Mock instruction was incorrectly marked valid.");
        AssertEqual(1, snapshot.Instructions[^1].Length,
            "Boundary-truncated invalid instruction did not retain the available source byte.");
    }

    private static async Task VerifyDisassemblyUnreadableRegionRejectionAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single();
        IMemoryReader memoryReader = session.GetRequiredService<IMemoryReader>();
        IDisassemblerProvider provider = session.GetRequiredService<IDisassemblerProvider>();
        DisassemblyReader reader = new();

        MemoryRegion guardedRegion = new(
            MockTargetLayout.CodeAddress,
            (ulong)MockTargetLayout.CodeBytes.Length,
            MemoryProtection.Read | MemoryProtection.Execute | MemoryProtection.Guard,
            "Guarded Code");
        MemoryRegion writeOnlyRegion = new(
            MockTargetLayout.CodeAddress,
            (ulong)MockTargetLayout.CodeBytes.Length,
            MemoryProtection.Write,
            "Write Only Code");

        await AssertThrowsAsync<InvalidOperationException>(
            () => reader.ReadAsync(
                memoryReader,
                provider,
                process,
                new[] { guardedRegion },
                session.Architecture,
                MockTargetLayout.CodeAddress,
                cancellationToken: CancellationToken.None),
            "Disassembly accepted a guarded memory region as readable.").ConfigureAwait(false);

        await AssertThrowsAsync<InvalidOperationException>(
            () => reader.ReadAsync(
                memoryReader,
                provider,
                process,
                new[] { writeOnlyRegion },
                session.Architecture,
                MockTargetLayout.CodeAddress,
                cancellationToken: CancellationToken.None),
            "Disassembly accepted a write-only memory region as readable.").ConfigureAwait(false);
    }

    private static async Task VerifyDisassemblyInstructionOrderValidationAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single();
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);

        await AssertThrowsAsync<InvalidOperationException>(
            () => new DisassemblyReader().ReadAsync(
                session.GetRequiredService<IMemoryReader>(),
                new OutOfOrderDisassemblerProvider(),
                process,
                regions,
                session.Architecture,
                MockTargetLayout.CodeAddress,
                requestedByteCount: 2,
                cancellationToken: CancellationToken.None),
            "Core accepted out-of-order instruction records from a disassembly provider.").ConfigureAwait(false);
    }

    private static async Task VerifyDisassemblyCancellationAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single();
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await AssertThrowsAsync<OperationCanceledException>(
            () => new DisassemblyReader().ReadAsync(
                session.GetRequiredService<IMemoryReader>(),
                session.GetRequiredService<IDisassemblerProvider>(),
                process,
                regions,
                session.Architecture,
                MockTargetLayout.CodeAddress,
                cancellationToken: cancellation.Token),
            "Core disassembly ignored a cancelled operation.").ConfigureAwait(false);
    }

    private static Task VerifyDisassemblySessionIdentityAsync()
    {
        TargetProcess process = new(MockTargetLayout.ProcessId, MockTargetLayout.ProcessName, "Display Name A");
        DisassemblySessionIdentity identity = new(MockPluginInfo.Id, process, connectionGeneration: 7);

        AssertTrue(
            identity.Matches(
                MockPluginInfo.Id,
                new TargetProcess(MockTargetLayout.ProcessId, MockTargetLayout.ProcessName, "Display Name B"),
                7),
            "Disassembly target identity should use stable process id/name rather than presentation-only display text.");
        AssertFalse(identity.Matches("another.plugin", process, 7),
            "Disassembly target identity ignored plugin identity.");
        AssertFalse(identity.Matches(MockPluginInfo.Id, new TargetProcess(process.Id + 1, process.Name), 7),
            "Disassembly target identity ignored process id.");
        AssertFalse(identity.Matches(MockPluginInfo.Id, new TargetProcess(process.Id, process.Name + ".other"), 7),
            "Disassembly target identity ignored process name.");
        AssertFalse(identity.Matches(MockPluginInfo.Id, process, 8),
            "Disassembly target identity ignored connection generation.");

        AssertThrows<InvalidOperationException>(
            () => identity.EnsureCurrent(MockPluginInfo.Id, process, 8),
            "Disassembly target identity did not reject a stale connection generation.");

        return Task.CompletedTask;
    }

    private static async Task VerifyMemoryViewerBoundedWindowAsync()
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
        MemoryViewerReader viewerReader = new();

        MemoryViewSnapshot middle = await viewerReader
            .ReadWindowAsync(
                reader,
                process,
                regions,
                MockTargetLayout.AmmoAddress,
                MemoryViewerReader.DefaultWindowByteCount,
                MemoryViewerReader.DefaultBytesPerRow,
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(
            MemoryViewerReader.DefaultWindowByteCount,
            middle.Bytes.Length,
            "Memory Viewer did not return the requested bounded window size in the middle of a readable region.");
        AssertTrue(
            middle.StartAddress <= MockTargetLayout.AmmoAddress &&
            middle.EndAddressExclusive > MockTargetLayout.AmmoAddress,
            "Memory Viewer window does not contain the requested address.");
        AssertTrue(
            middle.StartAddress >= regions[0].BaseAddress &&
            middle.EndAddressExclusive <= regions[0].EndAddressExclusive,
            "Memory Viewer window crossed the containing memory-region boundary.");

        MemoryViewSnapshot atStart = await viewerReader
            .ReadWindowAsync(
                reader,
                process,
                regions,
                regions[0].BaseAddress + 1,
                MemoryViewerReader.DefaultWindowByteCount,
                MemoryViewerReader.DefaultBytesPerRow,
                CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(
            regions[0].BaseAddress,
            atStart.StartAddress,
            "Memory Viewer did not clamp a near-start request to the region base address.");

        ulong nearEndAddress = regions[0].EndAddressExclusive - 2;
        MemoryViewSnapshot atEnd = await viewerReader
            .ReadWindowAsync(
                reader,
                process,
                regions,
                nearEndAddress,
                MemoryViewerReader.DefaultWindowByteCount,
                MemoryViewerReader.DefaultBytesPerRow,
                CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(
            regions[0].EndAddressExclusive,
            atEnd.EndAddressExclusive,
            "Memory Viewer did not clamp a near-end request to the region end address.");

        MemoryRegion unalignedRegion = new(
            checked(regions[0].BaseAddress + 3),
            checked(regions[0].Size - 7),
            regions[0].Protection,
            regions[0].Name,
            regions[0].ModuleName);
        ulong unalignedNearEndAddress = unalignedRegion.EndAddressExclusive - 2;
        MemoryViewSnapshot unalignedAtEnd = await viewerReader
            .ReadWindowAsync(
                reader,
                process,
                new[] { unalignedRegion },
                unalignedNearEndAddress,
                MemoryViewerReader.DefaultWindowByteCount,
                MemoryViewerReader.DefaultBytesPerRow,
                CancellationToken.None)
            .ConfigureAwait(false);
        AssertTrue(
            unalignedAtEnd.StartAddress <= unalignedNearEndAddress &&
            unalignedAtEnd.EndAddressExclusive > unalignedNearEndAddress,
            "Memory Viewer row alignment moved an unaligned near-end requested address outside the returned page.");
        AssertEqual(
            unalignedRegion.EndAddressExclusive,
            unalignedAtEnd.EndAddressExclusive,
            "Memory Viewer did not preserve the unaligned containing-region end when clamping a near-end request.");
    }

    private static Task VerifyMemoryViewerValueSpanHighlightAsync()
    {
        MemoryViewHighlightSpan fourByteSpan = new(0x100CUL, 4);
        MemoryViewRowHighlight contained = fourByteSpan.GetRowHighlight(0x1000UL, 16);
        AssertEqual(12, contained.StartIndex,
            "Memory Viewer highlight span started at the wrong byte inside a row.");
        AssertEqual(4, contained.ByteCount,
            "Memory Viewer did not preserve the complete in-row value span.");

        MemoryViewHighlightSpan crossingSpan = new(0x100EUL, 4);
        MemoryViewRowHighlight crossingFirst = crossingSpan.GetRowHighlight(0x1000UL, 16);
        MemoryViewRowHighlight crossingSecond = crossingSpan.GetRowHighlight(0x1010UL, 16);
        AssertEqual(14, crossingFirst.StartIndex,
            "Memory Viewer cross-row highlighting started at the wrong byte in the first row.");
        AssertEqual(2, crossingFirst.ByteCount,
            "Memory Viewer cross-row highlighting did not preserve the first row portion.");
        AssertEqual(0, crossingSecond.StartIndex,
            "Memory Viewer cross-row highlighting did not resume at the next row start.");
        AssertEqual(2, crossingSecond.ByteCount,
            "Memory Viewer cross-row highlighting did not preserve the second row portion.");

        MemoryViewRowHighlight outside = crossingSpan.GetRowHighlight(0x1020UL, 16);
        AssertTrue(outside.IsEmpty,
            "Memory Viewer highlighted a row that does not overlap the requested value span.");

        MemoryViewHighlightSpan nearMaximum = new(ulong.MaxValue - 1UL, 4);
        MemoryViewRowHighlight maximumRow = nearMaximum.GetRowHighlight(ulong.MaxValue - 3UL, 4);
        AssertEqual(2, maximumRow.StartIndex,
            "Memory Viewer high-address highlighting calculated the wrong row offset.");
        AssertEqual(2, maximumRow.ByteCount,
            "Memory Viewer high-address highlighting overflowed instead of clamping at the address-space end.");

        bool rejectedEmptySpan = false;
        try
        {
            _ = new MemoryViewHighlightSpan(0x1000UL, 0);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejectedEmptySpan = true;
        }

        AssertTrue(rejectedEmptySpan,
            "Memory Viewer accepted a zero-length highlight span.");
        return Task.CompletedTask;
    }

    private static Task VerifyMemoryViewerValueSpanBindingsAsync()
    {
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MemoryViewerWindow.xaml");
        AssertTrue(File.Exists(xamlPath),
            "Memory Viewer XAML fixture should be copied beside the verification executable.");

        string xaml = File.ReadAllText(xamlPath);
        string[] readOnlyRunProperties =
        {
            "HexPrefix",
            "HighlightedHexBytes",
            "HexSuffix",
            "AsciiPrefix",
            "HighlightedAscii",
            "AsciiSuffix"
        };

        foreach (string propertyName in readOnlyRunProperties)
        {
            AssertTrue(
                xaml.Contains($"{{Binding {propertyName}, Mode=OneWay}}", StringComparison.Ordinal),
                $"Memory Viewer Run.Text binding for {propertyName} must be explicitly OneWay.");
            AssertFalse(
                xaml.Contains($"{{Binding {propertyName}}}", StringComparison.Ordinal),
                $"Memory Viewer Run.Text binding for {propertyName} must not fall back to WPF's default binding mode.");
        }

        return Task.CompletedTask;
    }

    private static Task VerifyManualSavedAddressToolbarAsync()
    {
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml");
        AssertTrue(File.Exists(xamlPath),
            "MainWindow XAML fixture should be copied beside the verification executable.");

        string xaml = File.ReadAllText(xamlPath);
        int addIndex = xaml.IndexOf("Content=\"Add Manually\"", StringComparison.Ordinal);
        int removeAllIndex = xaml.IndexOf("Content=\"Remove All\"", StringComparison.Ordinal);
        int exportIndex = xaml.IndexOf("Content=\"Export\"", StringComparison.Ordinal);

        AssertTrue(addIndex >= 0, "Saved Addresses toolbar is missing Add Manually.");
        AssertTrue(removeAllIndex > addIndex, "Saved Addresses toolbar must place Remove All after Add Manually.");
        AssertTrue(exportIndex > removeAllIndex, "Saved Addresses toolbar must place Export after Remove All.");
        AssertTrue(
            xaml.Contains("IsEnabled=\"{Binding SelectedPlugin.CanAddSavedAddressManually}\"", StringComparison.Ordinal),
            "Add Manually must be gated by the Active Target/manual-entry availability state.");
        AssertTrue(
            xaml.Contains("Click=\"AddManualSavedAddressButton_Click\"", StringComparison.Ordinal),
            "Add Manually is not wired to its dialog-opening handler.");

        return Task.CompletedTask;
    }

    private static Task VerifyManualSavedAddressDialogAsync()
    {
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ManualSavedAddressDialog.xaml");
        AssertTrue(File.Exists(xamlPath),
            "Manual Saved Address dialog XAML fixture should be copied beside the verification executable.");

        string xaml = File.ReadAllText(xamlPath);
        string[] requiredFunctionalControls =
        {
            "x:Name=\"AddressTextBox\"",
            "input:TextBoxInputFilter.Mode=\"HexAddress\"",
            "x:Name=\"DescriptionTextBox\"",
            "x:Name=\"ValueTypeComboBox\"",
            "x:Name=\"LengthTextBox\"",
            "input:TextBoxInputFilter.Mode=\"UnsignedInteger\"",
            "x:Name=\"CurrentValueTextBox\"",
            "Click=\"AddButton_Click\""
        };

        foreach (string control in requiredFunctionalControls)
        {
            AssertTrue(xaml.Contains(control, StringComparison.Ordinal),
                $"Manual Saved Address dialog is missing required functional control markup: {control}");
        }

        string[] plannedControls =
        {
            "Hexadecimal display (planned)",
            "Start bit",
            "Unicode",
            "Code page",
            "Pointer (planned)",
            "Base address",
            "Add Offset",
            "Remove Offset"
        };

        foreach (string label in plannedControls)
        {
            AssertTrue(xaml.Contains(label, StringComparison.Ordinal),
                $"Manual Saved Address dialog is missing the planned manual-entry placeholder: {label}");
        }

        AssertTrue(
            xaml.Contains("These controls reserve planned functionality", StringComparison.Ordinal),
            "Planned manual-entry controls must state clearly that they are not implemented yet.");
        AssertTrue(
            xaml.Contains("IsEnabled=\"False\"", StringComparison.Ordinal),
            "Planned manual-entry controls must remain visibly disabled.");

        return Task.CompletedTask;
    }

    private static Task VerifyManualSavedAddressAddressParserSourceAsync()
    {
        string sourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ManualSavedAddressDialog.xaml.cs");
        AssertTrue(File.Exists(sourcePath),
            "Manual Saved Address dialog code-behind fixture should be copied beside the verification executable.");

        string source = File.ReadAllText(sourcePath);
        int methodIndex = source.IndexOf("private static bool TryParseHexAddress", StringComparison.Ordinal);
        AssertTrue(methodIndex >= 0, "Manual Saved Address hexadecimal parser method was not found.");

        int assignmentIndex = source.IndexOf("address = 0;", methodIndex, StringComparison.Ordinal);
        int lengthGuardIndex = source.IndexOf("candidate.Length is > 0 and <= 16", methodIndex, StringComparison.Ordinal);
        AssertTrue(assignmentIndex > methodIndex && lengthGuardIndex > assignmentIndex,
            "TryParseHexAddress must assign its out address before the short-circuit length guard can return.");

        return Task.CompletedTask;
    }


    private static Task VerifyDisassemblerFollowTargetSourceAsync()
    {
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml");
        string windowSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml.cs");
        string viewModelSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerViewModel.cs");
        string rowViewModelSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblyInstructionViewModel.cs");

        foreach (string path in new[] { xamlPath, windowSourcePath, viewModelSourcePath, rowViewModelSourcePath })
        {
            AssertTrue(File.Exists(path),
                $"Disassembler Follow Target fixture is missing: {Path.GetFileName(path)}");
        }

        string xaml = File.ReadAllText(xamlPath);
        AssertTrue(xaml.Contains("Header=\"Follow Target\"", StringComparison.Ordinal),
            "Disassembler context menu is missing Follow Target.");
        AssertTrue(xaml.Contains("Tag=\"FollowTarget\"", StringComparison.Ordinal),
            "Disassembler Follow Target menu item is missing its stable context-menu tag.");
        AssertTrue(xaml.Contains("MouseDoubleClick=\"DisassemblyDataGrid_MouseDoubleClick\"", StringComparison.Ordinal),
            "Disassembler list is not wired for double-click target following.");
        AssertTrue(xaml.Contains("PreviewKeyDown=\"DisassemblyDataGrid_PreviewKeyDown\"", StringComparison.Ordinal),
            "Disassembler list is not wired for Enter-key target following.");

        string rowSource = File.ReadAllText(rowViewModelSourcePath);
        AssertTrue(rowSource.Contains("public bool CanFollowTarget", StringComparison.Ordinal),
            "Disassembly row model is missing neutral target-follow eligibility.");
        AssertTrue(rowSource.Contains("BranchTarget.HasValue", StringComparison.Ordinal),
            "Disassembly row target following must require a known direct BranchTarget.");
        AssertTrue(rowSource.Contains("DisassemblyFlowControl.Call", StringComparison.Ordinal) &&
                   rowSource.Contains("DisassemblyFlowControl.Jump", StringComparison.Ordinal) &&
                   rowSource.Contains("DisassemblyFlowControl.ConditionalJump", StringComparison.Ordinal),
            "Disassembly row target following must be limited to call/jump flow-control records.");

        string viewModelSource = File.ReadAllText(viewModelSourcePath);
        AssertTrue(viewModelSource.Contains("public ICommand FollowTargetCommand", StringComparison.Ordinal),
            "Disassembler ViewModel is missing FollowTargetCommand.");
        AssertTrue(viewModelSource.Contains("await NavigateAndRecordAsync(target.Value)", StringComparison.Ordinal),
            "Follow Target must reuse the successful-address navigation/history path.");

        string windowSource = File.ReadAllText(windowSourcePath);
        AssertTrue(windowSource.Contains("ContextMenuUtilities.SetItemEnabled", StringComparison.Ordinal) &&
                   windowSource.Contains("\"FollowTarget\"", StringComparison.Ordinal),
            "Disassembler context-menu enablement is not tied to direct-target availability.");
        AssertTrue(windowSource.Contains("FollowTargetCommand.Execute(null)", StringComparison.Ordinal),
            "Disassembler mouse/keyboard/context-menu paths do not execute FollowTargetCommand.");

        string combinedSource = rowSource + viewModelSource + windowSource;
        AssertFalse(combinedSource.Contains("PlayStation 5", StringComparison.OrdinalIgnoreCase),
            "Disassembler Follow Target host logic must remain platform-neutral.");
        AssertFalse(combinedSource.Contains("Iced", StringComparison.OrdinalIgnoreCase),
            "Disassembler Follow Target host logic must not depend on the PS5 x86-64 decoder implementation.");

        return Task.CompletedTask;
    }

    private static async Task VerifyDisassemblyExportSourceAsync()
    {
        string testRoot = CreateTemporaryDirectory();
        try
        {
            DisassembledInstruction[] instructions =
            {
                new(
                    0x401000,
                    new byte[] { 0xE8, 0x10, 0x00, 0x00, 0x00 },
                    "call",
                    "0x401015",
                    DisassemblyFlowControl.Call,
                    0x401015),
                new(
                    0x401005,
                    new byte[] { 0xC3 },
                    "ret",
                    string.Empty,
                    DisassemblyFlowControl.Return)
            };
            MemoryRegion region = new(
                0x400000,
                0x200000,
                MemoryProtection.Read | MemoryProtection.Execute,
                "Executable",
                "eboot.bin");
            Dictionary<string, ExportCellValue> metadata = new(StringComparer.Ordinal)
            {
                ["scope"] = ExportCellValue.FromString("displayed"),
                ["requestedAddress"] = ExportCellValue.FromString("0x401000")
            };
            DisassemblyExportSource source = new(
                instructions,
                region,
                moduleBaseAddress: 0x400000,
                metadata);

            AssertEqual("disassembly", source.Type, "Disassembly export type changed unexpectedly.");
            AssertEqual(1, source.SchemaVersion, "Disassembly export schema version changed unexpectedly.");
            AssertEqual(2L, source.Count, "Disassembly export row count is incorrect.");
            AssertTrue(source.Columns.Any(column => column.Id == DisassemblyExportColumnIds.Mnemonic),
                "Disassembly export is missing the structured mnemonic column.");
            AssertTrue(source.Columns.Any(column => column.Id == DisassemblyExportColumnIds.BranchTarget),
                "Disassembly export is missing the structured branch-target column.");
            AssertTrue(source.Columns.Any(column => column.Id == DisassemblyExportColumnIds.ModuleRelativeAddress),
                "Disassembly export is missing module-relative address data.");

            string jsonPath = Path.Combine(testRoot, "disassembly.json");
            TabularExportResult result = await new TabularExportService()
                .ExportAsync(
                    jsonPath,
                    TabularExportFormat.Json,
                    source,
                    source.Columns.Select(column => column.Id).ToArray(),
                    progress: null,
                    CancellationToken.None)
                .ConfigureAwait(false);
            AssertEqual(2L, result.RowsWritten, "Disassembly JSON export wrote an unexpected number of rows.");

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            JsonElement root = document.RootElement;
            AssertEqual("disassembly", root.GetProperty("type").GetString(),
                "Disassembly JSON type marker is incorrect.");
            JsonElement firstRow = root.GetProperty("rows")[0];
            AssertEqual("0x401000", firstRow.GetProperty("address").GetString(),
                "Disassembly JSON address did not preserve hexadecimal presentation.");
            AssertEqual("E8 10 00 00 00", firstRow.GetProperty("bytes").GetString(),
                "Disassembly JSON raw bytes are incorrect.");
            AssertEqual("call", firstRow.GetProperty("mnemonic").GetString(),
                "Disassembly JSON mnemonic is incorrect.");
            AssertEqual("Call", firstRow.GetProperty("flowControl").GetString(),
                "Disassembly JSON flow-control classification is incorrect.");
            AssertEqual("0x401015", firstRow.GetProperty("branchTarget").GetString(),
                "Disassembly JSON direct target is incorrect.");
            AssertEqual("eboot.bin + 0x1000", firstRow.GetProperty("moduleRelativeAddress").GetString(),
                "Disassembly JSON module-relative address is incorrect.");
            AssertTrue(firstRow.GetProperty("valid").GetBoolean(),
                "Disassembly JSON validity state is incorrect.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }
    }

    private static Task VerifyDisassemblerSelectionCopyExportSourceAsync()
    {
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml");
        string windowSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml.cs");
        string viewModelSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerViewModel.cs");

        string xaml = File.ReadAllText(xamlPath);
        AssertTrue(xaml.Contains("SelectionMode=\"Extended\"", StringComparison.Ordinal),
            "Disassembler must support Ctrl/Shift multi-selection.");
        foreach (string header in new[]
                 {
                     "Copy Address",
                     "Copy Bytes",
                     "Copy Instruction",
                     "Copy Address + Instruction",
                     "Copy Selected",
                     "Export..."
                 })
        {
            AssertTrue(xaml.Contains($"Header=\"{header}\"", StringComparison.Ordinal),
                $"Disassembler context menu is missing '{header}'.");
        }
        AssertTrue(xaml.Contains("Click=\"ExportButton_Click\"", StringComparison.Ordinal),
            "Disassembler workspace is missing its visible Export button.");

        string windowSource = File.ReadAllText(windowSourcePath);
        AssertTrue(windowSource.Contains("GetSelectedRowsInDisplayOrder", StringComparison.Ordinal),
            "Disassembler copy/export selection is not normalized to display order.");
        AssertTrue(windowSource.Contains("Clipboard.SetText", StringComparison.Ordinal),
            "Disassembler copy actions are not connected to the clipboard.");
        AssertTrue(windowSource.Contains("DataExportDialogService", StringComparison.Ordinal) &&
                   windowSource.Contains("TabularExportService", StringComparison.Ordinal),
            "Disassembler does not reuse the universal export workflow.");
        AssertTrue(windowSource.Contains("ExportDestinationPicker.TryChoose", StringComparison.Ordinal),
            "Disassembler does not reuse the shared export destination picker.");

        string viewModelSource = File.ReadAllText(viewModelSourcePath);
        AssertTrue(viewModelSource.Contains("Displayed Instructions", StringComparison.Ordinal) &&
                   viewModelSource.Contains("Selected Instructions", StringComparison.Ordinal),
            "Disassembler export scopes must include Displayed and Selected.");
        AssertTrue(viewModelSource.Contains("new DisassemblyExportSource", StringComparison.Ordinal),
            "Disassembler export scopes are not backed by the structured Core disassembly export source.");

        return Task.CompletedTask;
    }

    private static Task VerifyDisassemblerRightClickSelectionSourceAsync()
    {
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml");
        string windowSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml.cs");
        string viewModelSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerViewModel.cs");

        string xaml = File.ReadAllText(xamlPath);
        AssertTrue(
            xaml.Contains(
                "PreviewMouseRightButtonDown=\"DisassemblyDataGrid_PreviewMouseRightButtonDown\"",
                StringComparison.Ordinal),
            "Disassembler does not intercept right-click before DataGrid can collapse an extended selection.");

        string windowSource = File.ReadAllText(windowSourcePath);
        AssertTrue(
            windowSource.Contains(
                "row.IsSelected && dataGrid.SelectedItems.Count > 1",
                StringComparison.Ordinal) &&
            windowSource.Contains("e.Handled = true;", StringComparison.Ordinal),
            "Disassembler right-click handling does not preserve an existing multi-selection.");
        AssertTrue(
            windowSource.Contains(
                "if (!dataGrid.SelectedItems.Contains(contextInstruction))",
                StringComparison.Ordinal) &&
            windowSource.Contains("dataGrid.SelectedItems.Clear();", StringComparison.Ordinal),
            "Disassembler must still collapse selection when right-clicking an unselected row.");

        int contextMenuStart = windowSource.IndexOf(
            "private void DisassemblyDataGrid_ContextMenuOpening",
            StringComparison.Ordinal);
        int followTargetStart = windowSource.IndexOf(
            "private void FollowTargetMenuItem_Click",
            StringComparison.Ordinal);
        AssertTrue(
            contextMenuStart >= 0 && followTargetStart > contextMenuStart,
            "Disassembler context-menu source fixture could not be isolated.");

        string contextMenuSource = windowSource[contextMenuStart..followTargetStart];
        AssertTrue(
            !contextMenuSource.Contains(
                "viewModel.SelectedInstruction = contextInstruction",
                StringComparison.Ordinal),
            "Disassembler context-menu opening must not rewrite the TwoWay SelectedItem and collapse extended selection.");
        AssertTrue(
            contextMenuSource.Contains(
                "viewModel.CanFollowTarget(contextInstruction)",
                StringComparison.Ordinal),
            "Disassembler context-menu Follow Target enablement must evaluate the clicked row without changing selection.");

        string viewModelSource = File.ReadAllText(viewModelSourcePath);
        AssertTrue(
            viewModelSource.Contains(
                "internal bool CanFollowTarget(DisassemblyInstructionViewModel? instruction)",
                StringComparison.Ordinal),
            "Disassembler ViewModel is missing row-specific Follow Target capability evaluation.");

        return Task.CompletedTask;
    }

    private static Task VerifyDisassemblerMultiSelectionCopySourceAsync()
    {
        string windowSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml.cs");
        string windowSource = File.ReadAllText(windowSourcePath);

        foreach (string projection in new[]
                 {
                     "row => row.Address",
                     "row => row.Bytes",
                     "row => row.Instruction",
                     "row => $\"{row.Address}: {row.Instruction}\""
                 })
        {
            AssertTrue(
                windowSource.Contains(projection, StringComparison.Ordinal),
                $"Disassembler granular copy action is not projected from the selected-row set: {projection}");
        }

        AssertTrue(
            windowSource.Contains(
                "Func<DisassemblyInstructionViewModel, string> formatter",
                StringComparison.Ordinal) &&
            windowSource.Contains("GetSelectedRowsInDisplayOrder();", StringComparison.Ordinal) &&
            windowSource.Contains("rows.Select(formatter)", StringComparison.Ordinal),
            "Disassembler granular copy actions must share the display-ordered selected-row formatter.");

        AssertTrue(
            !windowSource.Contains(
                "CopyText(instruction.Address, \"address\")",
                StringComparison.Ordinal) &&
            !windowSource.Contains(
                "CopyText(instruction.Bytes, \"instruction bytes\")",
                StringComparison.Ordinal) &&
            !windowSource.Contains(
                "CopyText(instruction.Instruction, \"instruction\")",
                StringComparison.Ordinal),
            "Disassembler granular copy actions must not fall back to the single context row when multiple rows remain selected.");

        return Task.CompletedTask;
    }

    private static Task VerifyDisassemblerRegionNavigationSourceAsync()
    {
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml");
        string viewModelSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerViewModel.cs");
        string xaml = File.ReadAllText(xamlPath);
        string source = File.ReadAllText(viewModelSourcePath);

        foreach (string command in new[]
                 {
                     "PreviousRegionCommand",
                     "RegionStartCommand",
                     "RegionEndCommand",
                     "NextRegionCommand"
                 })
        {
            AssertTrue(xaml.Contains($"Command=\"{{Binding {command}}}\"", StringComparison.Ordinal),
                $"Disassembler UI is missing {command}.");
            AssertTrue(source.Contains($"public ICommand {command}", StringComparison.Ordinal),
                $"Disassembler ViewModel is missing {command}.");
        }

        AssertTrue(source.Contains("MemoryViewerRegionNavigator.FindPreviousReadableRegion", StringComparison.Ordinal) &&
                   source.Contains("MemoryViewerRegionNavigator.FindNextReadableRegion", StringComparison.Ordinal),
            "Disassembler region navigation must reuse the existing readable-region navigator rather than duplicate it.");
        AssertTrue(source.Contains("await NavigateAndRecordAsync(previousRegion.BaseAddress)", StringComparison.Ordinal) &&
                   source.Contains("await NavigateAndRecordAsync(nextRegion.BaseAddress)", StringComparison.Ordinal),
            "Disassembler region navigation must enter the same successful-address history path as Go To.");
        AssertTrue(source.Contains("ModuleRelativeText", StringComparison.Ordinal) &&
                   source.Contains("Origin: {moduleName} + 0x", StringComparison.Ordinal),
            "Disassembler is missing module-relative origin presentation.");

        return Task.CompletedTask;
    }

    private static async Task VerifyMockCustomDisassemblyArchitectureAsync()
    {
        MockTargetPlugin plugin = new();
        AssertEqual(CpuArchitecture.Unknown, plugin.Metadata.Architecture.Cpu,
            "Mock Target must not advertise the synthetic instruction set as x64.");
        AssertEqual(64, plugin.Metadata.Architecture.AddressWidthBits,
            "Mock Target address width changed unexpectedly.");
        AssertEqual(64, plugin.Metadata.Architecture.PointerWidthBits,
            "Mock Target pointer width changed unexpectedly.");

        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);
        IDisassemblerProvider provider = session.GetRequiredService<IDisassemblerProvider>();
        AssertTrue(provider.SupportsArchitecture(session.Architecture),
            "Mock disassembler must support the Mock Target's custom architecture descriptor.");
        AssertFalse(provider.SupportsArchitecture(new TargetArchitecture(
                CpuArchitecture.X64,
                64,
                64,
                Endianness.Little)),
            "Mock disassembler must not claim that its synthetic instruction set is x86-64.");
    }

    private static async Task VerifyMemoryViewerGuardedRegionRejectionAsync()
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
        MemoryViewerReader viewerReader = new();
        MemoryRegion guardedRegion = new(
            MockTargetLayout.BaseAddress,
            MockTargetLayout.MemorySize,
            MemoryProtection.Read | MemoryProtection.Guard | MemoryProtection.Private,
            "Guarded Test Region",
            MockTargetLayout.ProcessName);

        bool rejected = false;
        try
        {
            await viewerReader
                .ReadWindowAsync(
                    reader,
                    process,
                    new[] { guardedRegion },
                    MockTargetLayout.AmmoAddress,
                    MemoryViewerReader.DefaultWindowByteCount,
                    MemoryViewerReader.DefaultBytesPerRow,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        AssertTrue(rejected, "Memory Viewer accepted a guarded memory region as readable.");
    }

    private static async Task VerifyMemoryViewerSafeWriteAsync()
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
        MemoryViewerWriter viewerWriter = new();

        byte[] originalBytes = new byte[sizeof(int)];
        int originalRead = await reader
            .ReadAsync(process, MockTargetLayout.AmmoAddress, originalBytes, CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(sizeof(int), originalRead, "Memory Viewer safe-write setup did not read the expected byte count.");
        AssertEqual(30, BinaryPrimitives.ReadInt32LittleEndian(originalBytes), "Unexpected initial mock ammo value.");

        byte[] staleExpectedBytes = originalBytes.ToArray();
        staleExpectedBytes[0] ^= 0x7F;
        byte[] staleReplacementBytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(staleReplacementBytes, 31);

        MemoryViewWriteResult staleResult = await viewerWriter
            .WriteAndVerifyAsync(
                reader,
                writer,
                process,
                regions,
                MockTargetLayout.AmmoAddress,
                staleExpectedBytes,
                staleReplacementBytes,
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(
            MemoryViewWriteOutcome.SourceChanged,
            staleResult.Outcome,
            "Memory Viewer did not reject a write whose displayed source bytes were stale.");
        AssertFalse(staleResult.WriteWasIssued, "Memory Viewer reported a write for stale source bytes.");

        byte[] afterRejectedWrite = new byte[sizeof(int)];
        await reader
            .ReadAsync(process, MockTargetLayout.AmmoAddress, afterRejectedWrite, CancellationToken.None)
            .ConfigureAwait(false);
        AssertTrue(
            afterRejectedWrite.AsSpan().SequenceEqual(originalBytes),
            "Memory Viewer stale-data rejection modified target memory.");

        MemoryViewWriteResult failedVerificationResult = await viewerWriter
            .WriteAndVerifyAsync(
                reader,
                new NoOpMemoryWriter(),
                process,
                regions,
                MockTargetLayout.AmmoAddress,
                originalBytes,
                staleReplacementBytes,
                CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(
            MemoryViewWriteOutcome.VerificationFailed,
            failedVerificationResult.Outcome,
            "Memory Viewer did not report a read-back mismatch after a write was issued.");
        AssertTrue(
            failedVerificationResult.WriteWasIssued,
            "Memory Viewer verification-failure result did not record that a write was issued.");

        byte[] replacementBytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(replacementBytes, 31);
        MemoryViewWriteResult verifiedResult = await viewerWriter
            .WriteAndVerifyAsync(
                reader,
                writer,
                process,
                regions,
                MockTargetLayout.AmmoAddress,
                originalBytes,
                replacementBytes,
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(
            MemoryViewWriteOutcome.Verified,
            verifiedResult.Outcome,
            "Memory Viewer did not report a verified safe write.");
        AssertTrue(verifiedResult.WriteWasIssued, "Memory Viewer verified result did not record that a write was issued.");
        AssertEqual(sizeof(int), verifiedResult.ByteCount, "Memory Viewer safe-write byte count was incorrect.");

        byte[] readBack = new byte[sizeof(int)];
        await reader
            .ReadAsync(process, MockTargetLayout.AmmoAddress, readBack, CancellationToken.None)
            .ConfigureAwait(false);
        AssertTrue(
            readBack.AsSpan().SequenceEqual(replacementBytes),
            "Memory Viewer verified write was not preserved by the target.");

        MemoryViewWriteResult unchangedResult = await viewerWriter
            .WriteAndVerifyAsync(
                reader,
                writer,
                process,
                regions,
                MockTargetLayout.AmmoAddress,
                replacementBytes,
                replacementBytes,
                CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(
            MemoryViewWriteOutcome.NoChanges,
            unchangedResult.Outcome,
            "Memory Viewer did not recognize an unchanged edit as a no-op.");
        AssertFalse(unchangedResult.WriteWasIssued, "Memory Viewer issued a write for unchanged bytes.");
    }

    private static async Task VerifyMemoryViewerWriteProtectionRejectionAsync()
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
        MemoryViewerWriter viewerWriter = new();

        byte[] originalBytes = new byte[sizeof(int)];
        await reader
            .ReadAsync(process, MockTargetLayout.AmmoAddress, originalBytes, CancellationToken.None)
            .ConfigureAwait(false);
        byte[] replacementBytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(replacementBytes, 31);

        MemoryRegion readOnlyRegion = new(
            MockTargetLayout.BaseAddress,
            MockTargetLayout.MemorySize,
            MemoryProtection.Read | MemoryProtection.Private,
            "Read-only Test Region",
            MockTargetLayout.ProcessName);

        bool rejected = false;
        try
        {
            await viewerWriter
                .WriteAndVerifyAsync(
                    reader,
                    writer,
                    process,
                    new[] { readOnlyRegion },
                    MockTargetLayout.AmmoAddress,
                    originalBytes,
                    replacementBytes,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        AssertTrue(rejected, "Memory Viewer accepted a write into a non-writable memory region.");

        byte[] afterRejectedWrite = new byte[sizeof(int)];
        await reader
            .ReadAsync(process, MockTargetLayout.AmmoAddress, afterRejectedWrite, CancellationToken.None)
            .ConfigureAwait(false);
        AssertTrue(
            afterRejectedWrite.AsSpan().SequenceEqual(originalBytes),
            "Memory Viewer protection rejection modified target memory.");
    }

    private static Task VerifyMemoryViewerRegionNavigationAsync()
    {
        MemoryRegion firstReadable = new(
            0x1000,
            0x100,
            MemoryProtection.Read,
            "First");
        MemoryRegion guarded = new(
            0x2000,
            0x100,
            MemoryProtection.Read | MemoryProtection.Guard,
            "Guarded");
        MemoryRegion unreadable = new(
            0x2800,
            0x100,
            MemoryProtection.Write,
            "Write-only");
        MemoryRegion current = new(
            0x3000,
            0x200,
            MemoryProtection.Read | MemoryProtection.Write,
            "Current");
        MemoryRegion nextReadable = new(
            0x5000,
            0x100,
            MemoryProtection.Read | MemoryProtection.Execute,
            "Next");

        MemoryRegion[] regions =
        {
            nextReadable,
            unreadable,
            current,
            guarded,
            firstReadable
        };

        AssertEqual(
            firstReadable,
            MemoryViewerRegionNavigator.FindPreviousReadableRegion(regions, current),
            "Memory Viewer region navigation did not skip guarded/unreadable regions when moving backward.");
        AssertEqual(
            nextReadable,
            MemoryViewerRegionNavigator.FindNextReadableRegion(regions, current),
            "Memory Viewer region navigation did not skip guarded/unreadable regions when moving forward.");
        AssertEqual<MemoryRegion?>(
            null,
            MemoryViewerRegionNavigator.FindPreviousReadableRegion(regions, firstReadable),
            "Memory Viewer region navigation reported a previous readable region before the first readable region.");
        AssertEqual<MemoryRegion?>(
            null,
            MemoryViewerRegionNavigator.FindNextReadableRegion(regions, nextReadable),
            "Memory Viewer region navigation reported a next readable region after the final readable region.");

        return Task.CompletedTask;
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
        IMemoryValueType int32 = StandardMemoryValueTypes.Int32;
        IMemoryScanType exact = ExactScanType;
        MemoryScanValue firstValue = ParseValue(int32, "30", session.Architecture);

        MemoryScanExecutionResult firstScan = await scanner
            .FirstScanAsync(
                process,
                regions,
                reader,
                session.Architecture,
                int32,
                exact,
                new[] { firstValue },
                progress: null,
                CancellationToken.None)
            .ConfigureAwait(false);

        MemoryScanResult ammo = firstScan.Results.Single(result => result.Address == MockTargetLayout.AmmoAddress);
        AssertEqual("30", ammo.CurrentValue.DisplayText, "First Scan did not return the expected mock ammo value.");
        AssertTrue(ammo.PreviousValue is null, "First Scan should not assign a previous value.");
        AssertEqual(
            MemoryProtection.Read | MemoryProtection.Write | MemoryProtection.Private,
            ammo.Protection,
            "First Scan did not preserve the containing memory region protection.");

        MemoryScanValue nextValue = ParseValue(int32, "25", session.Architecture);
        await writer
            .WriteAsync(process, MockTargetLayout.AmmoAddress, nextValue.Bytes, CancellationToken.None)
            .ConfigureAwait(false);

        MemoryScanExecutionResult nextScan = await scanner
            .NextScanAsync(
                process,
                regions,
                firstScan.Results,
                reader,
                session.Architecture,
                int32,
                exact,
                new[] { nextValue },
                progress: null,
                CancellationToken.None)
            .ConfigureAwait(false);

        MemoryScanResult refinedAmmo = nextScan.Results.Single();
        AssertEqual(MockTargetLayout.AmmoAddress, refinedAmmo.Address, "Next Scan retained the wrong address.");
        AssertEqual("25", refinedAmmo.CurrentValue.DisplayText, "Next Scan did not read the updated value.");
        AssertEqual("30", refinedAmmo.PreviousValue?.DisplayText, "Next Scan did not preserve the previous value.");
        AssertEqual(ammo.Protection, refinedAmmo.Protection, "Next Scan did not preserve the containing memory protection.");
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
        IMemoryScanType exact = ExactScanType;

        (IMemoryValueType Type, string Text)[] fixtures =
        {
            (StandardMemoryValueTypes.UInt8, "250"),
            (StandardMemoryValueTypes.Int8, "-100"),
            (StandardMemoryValueTypes.UInt16, "60000"),
            (StandardMemoryValueTypes.Int16, "-12345"),
            (StandardMemoryValueTypes.UInt32, "4000000000"),
            (StandardMemoryValueTypes.Int32, "-123456789"),
            (StandardMemoryValueTypes.UInt64, "18364758544493064720"),
            (StandardMemoryValueTypes.Int64, "-1234567890123456789"),
            (StandardMemoryValueTypes.Float32, "123.25"),
            (StandardMemoryValueTypes.Float64, "-9876.5"),
            (StandardMemoryValueTypes.ByteArray, "DE AD BE EF 01")
        };

        for (int index = 0; index < fixtures.Length; index++)
        {
            (IMemoryValueType type, string text) = fixtures[index];
            MemoryScanValue value = ParseValue(type, text, session.Architecture);

            ulong address = MockTargetLayout.BaseAddress + 0x2000UL + checked((ulong)(index * 0x100));
            await writer.WriteAsync(process, address, value.Bytes, CancellationToken.None).ConfigureAwait(false);

            MemoryScanExecutionResult result = await scanner
                .FirstScanAsync(
                    process,
                    regions,
                    reader,
                    session.Architecture,
                    type,
                    exact,
                    new[] { value },
                    progress: null,
                    CancellationToken.None)
                .ConfigureAwait(false);

            MemoryScanResult match = result.Results.Single(item => item.Address == address);
            AssertEqual(type.Id, match.ValueTypeId, $"Shared scanner returned the wrong type for {type.DisplayName}.");
            AssertEqual(value.DisplayText, match.CurrentValue.DisplayText, $"Shared scanner returned the wrong value for {type.DisplayName}.");
        }
    }

    private static Task VerifyStandardValueInputPoliciesAsync()
    {
        IMemoryValueInputPolicy signedInteger = (IMemoryValueInputPolicy)StandardMemoryValueTypes.Int32;
        AssertTrue(signedInteger.IsPotentiallyValidInput(string.Empty), "Signed integer policy must allow an empty edit state.");
        AssertTrue(signedInteger.IsPotentiallyValidInput("-"), "Signed integer policy must allow a leading minus while editing.");
        AssertTrue(signedInteger.IsPotentiallyValidInput("0x"), "Signed integer policy must allow an incomplete hexadecimal prefix while editing.");
        AssertTrue(signedInteger.IsPotentiallyValidInput("0xDEADBEEF"), "Signed integer policy rejected hexadecimal input.");
        AssertTrue(signedInteger.IsPotentiallyValidInput("-12345"), "Signed integer policy rejected decimal input.");
        AssertFalse(signedInteger.IsPotentiallyValidInput("12.5"), "Signed integer policy accepted a decimal point.");
        AssertFalse(signedInteger.IsPotentiallyValidInput("hello"), "Signed integer policy accepted alphabetic input.");

        IMemoryValueInputPolicy unsignedInteger = (IMemoryValueInputPolicy)StandardMemoryValueTypes.UInt32;
        AssertTrue(unsignedInteger.IsPotentiallyValidInput("+123"), "Unsigned integer policy rejected an explicit positive sign.");
        AssertFalse(unsignedInteger.IsPotentiallyValidInput("-1"), "Unsigned integer policy accepted a negative value.");

        IMemoryValueInputPolicy floatingPoint = (IMemoryValueInputPolicy)StandardMemoryValueTypes.Float64;
        AssertTrue(floatingPoint.IsPotentiallyValidInput("-."), "Floating-point policy must allow an incomplete signed decimal edit state.");
        AssertTrue(floatingPoint.IsPotentiallyValidInput("1."), "Floating-point policy rejected a trailing decimal point edit state.");
        AssertTrue(floatingPoint.IsPotentiallyValidInput("1e-"), "Floating-point policy rejected an incomplete exponent edit state.");
        AssertTrue(floatingPoint.IsPotentiallyValidInput("-1.25e+3"), "Floating-point policy rejected scientific notation.");
        AssertTrue(floatingPoint.IsPotentiallyValidInput("Infinity"), "Floating-point policy rejected the invariant Infinity token.");
        AssertFalse(floatingPoint.IsPotentiallyValidInput("1.2.3"), "Floating-point policy accepted multiple decimal points.");
        AssertFalse(floatingPoint.IsPotentiallyValidInput("12abc"), "Floating-point policy accepted arbitrary alphabetic input.");

        IMemoryValueInputPolicy byteArray = (IMemoryValueInputPolicy)StandardMemoryValueTypes.ByteArray;
        AssertTrue(byteArray.IsPotentiallyValidInput("DE AD BE EF"), "Array-of-Bytes policy rejected spaced hexadecimal bytes.");
        AssertTrue(byteArray.IsPotentiallyValidInput("0xDE,0xAD"), "Array-of-Bytes policy rejected supported prefixed/comma-separated input.");
        AssertTrue(byteArray.IsPotentiallyValidInput("DE-AD"), "Array-of-Bytes policy rejected the supported hyphen separator.");
        AssertFalse(byteArray.IsPotentiallyValidInput("GG"), "Array-of-Bytes policy accepted non-hexadecimal input.");

        IMemoryValueType customValueType = new TestByteValueType();
        AssertFalse(customValueType is IMemoryValueInputPolicy,
            "A custom plugin Value Type must not be forced to implement the optional live-input policy.");

        return Task.CompletedTask;
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
            TargetCapabilities.NativeValueScanning |
            TargetCapabilities.Disassembly |
            TargetCapabilities.Debugger |
            TargetCapabilities.ThreadEnumeration |
            TargetCapabilities.ThreadControl |
            TargetCapabilities.RegisterAccess,
            plugin.Capabilities,
            "PS5 plugin should advertise its implemented connection, memory access, native scan, process-control, disassembly, debugger, thread-control, and register-read support.");
        AssertEqual(2, plugin.ConnectionSettings.Count, "PS5 plugin should define host and port settings.");
        AssertTrue(plugin.ConnectionSettings.Any(setting => setting.Key == "host" && setting.IsRequired), "PS5 host setting is missing.");
        AssertTrue(
            plugin.ConnectionSettings.Any(setting => setting.Key == "port" && setting.DefaultValue == "744"),
            "PS5 port setting is missing or has the wrong default.");

        RecordingPluginSettings rememberedSettings = new();
        rememberedSettings.TrySetString("connection.host", "192.168.0.42");
        rememberedSettings.TrySetString("connection.port", "9020");
        Ps5TargetPlugin rememberedPlugin = new();
        rememberedPlugin.AttachSettings(rememberedSettings);
        AssertTrue(
            rememberedPlugin.ConnectionSettings.Any(setting => setting.Key == "host" && setting.DefaultValue == "192.168.0.42"),
            "PS5 plugin did not expose the remembered host as the connection-field default.");
        AssertTrue(
            rememberedPlugin.ConnectionSettings.Any(setting => setting.Key == "port" && setting.DefaultValue == "9020"),
            "PS5 plugin did not expose the remembered port as the connection-field default.");

        return Task.CompletedTask;
    }

    private static async Task VerifyPs5ConnectionHandshakeAsync()
    {
        await using Ps5ProtocolTestServer server = new();
        Ps5TargetPlugin plugin = new();
        RecordingPluginSettings settings = new();
        plugin.AttachSettings(settings);

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
        AssertTrue(session.GetService<INativeValueScanStreamProvider>() is null, "PS5 streaming native scanner should be hidden when the server does not advertise TurboScan support.");
        AssertTrue(session.GetService<INativeValueScanStreamRefiner>() is null, "PS5 streaming native refinement should be hidden when the server does not advertise TurboScan support.");
        AssertTrue(settings.TryGetString("connection.host", out string savedHost), "Successful PS5 connection did not save the host setting.");
        AssertEqual("127.0.0.1", savedHost, "Successful PS5 connection saved the wrong host setting.");
        AssertTrue(settings.TryGetString("connection.port", out string savedPort), "Successful PS5 connection did not save the port setting.");
        AssertEqual(server.Port.ToString(CultureInfo.InvariantCulture), savedPort, "Successful PS5 connection saved the wrong port setting.");
        AssertTrue(session.GetService<IForegroundProcessProvider>() is not null, "PS5 preferred-process service was not exposed after connection.");
        AssertTrue(session.GetService<IProcessControl>() is not null, "PS5 process-control service was not exposed after connection.");
        AssertTrue(session.GetService<IDisassemblerProvider>() is not null, "PS5 x86-64 disassembly service was not exposed after connection.");
        AssertTrue(session.GetService<IDebuggerProvider>() is not null, "PS5 debugger provider was not exposed after connection.");

        await server.Completion.ConfigureAwait(false);
        await session.DisposeAsync().ConfigureAwait(false);

        AssertFalse(session.IsConnected, "PS5 session remained connected after disposal.");
    }

    private static async Task VerifyPs5DebuggerProviderLifecycleAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveDebugger: true, debuggerSessionCount: 2);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        IDebuggerProvider provider = targetSession.GetRequiredService<IDebuggerProvider>();
        TargetProcess process = new(2222, "eboot.bin");

        IDebuggerSession first = await provider.AttachAsync(process, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Running, first.State, "PS5 debugger did not enter Running after attach.");
        await AssertThrowsAsync<InvalidOperationException>(
            () => provider.AttachAsync(process, CancellationToken.None),
            "PS5 debugger provider accepted a second simultaneous debugger session.");
        await first.DisposeAsync().ConfigureAwait(false);

        IDebuggerSession second = await provider.AttachAsync(process, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Running, second.State, "PS5 debugger provider did not release ownership after session disposal.");
        await second.DisposeAsync().ConfigureAwait(false);

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerExecutionProtocolAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveDebugger: true);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        IDebuggerProvider provider = targetSession.GetRequiredService<IDebuggerProvider>();
        IDebuggerSession debugger = await provider
            .AttachAsync(new TargetProcess(2222, "eboot.bin"), CancellationToken.None)
            .ConfigureAwait(false);
        List<DebuggerEvent> events = new();
        debugger.EventReceived += (_, args) => events.Add(args.Event);

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Paused, debugger.State, "PS5 debugger Pause did not enter Paused state.");
        AssertEqual(1, events.Count, "PS5 debugger Pause did not emit exactly one neutral event.");
        AssertEqual(DebuggerStopReason.PauseRequested, events[0].StopReason, "PS5 debugger Pause lost the requested-stop reason.");

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Running, debugger.State, "PS5 debugger Continue did not return to Running state.");
        AssertEqual(2, events.Count, "PS5 debugger Continue did not emit exactly one additional neutral event.");
        AssertEqual(DebuggerEventKind.Resumed, events[1].Kind, "PS5 debugger Continue emitted the wrong neutral event kind.");

        AssertTrue(
            server.DebuggerActions.SequenceEqual(new byte[] { 1, 0 }),
            "PS5 debugger did not map Pause/Continue to ps5debug-NG stop-go actions 1/0.");

        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Detached, debugger.State, "PS5 debugger Detach did not enter Detached state.");
        await debugger.DisposeAsync().ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerInterruptAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveDebugger: true);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(new TargetProcess(2222, "eboot.bin"), CancellationToken.None)
            .ConfigureAwait(false);

        TaskCompletionSource<DebuggerEvent> eventReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) => eventReceived.TrySetResult(args.Event);

        const uint threadId = 0x2A;
        const uint waitStatus = 5u << 8;
        const ulong instructionPointer = 0x0000000042D430UL;
        await server.SendDebuggerInterruptAsync(threadId, waitStatus, instructionPointer, "GameMain").ConfigureAwait(false);

        DebuggerEvent debugEvent = await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Paused, debugger.State, "PS5 async debugger interrupt did not leave the neutral session Paused.");
        AssertEqual(DebuggerExecutionState.Paused, debugEvent.ExecutionState, "PS5 async debugger event reported the wrong execution state.");
        AssertEqual(DebuggerStopReason.Signal, debugEvent.StopReason, "PS5 async debugger event did not map the wait signal to Signal stop reason.");
        AssertEqual((ulong)threadId, debugEvent.ThreadId, "PS5 async debugger event lost the LWP/thread id.");
        AssertEqual(instructionPointer, debugEvent.InstructionPointer, "PS5 async debugger event parsed the wrong instruction pointer offset.");
        AssertTrue(debugEvent.Message?.Contains("GameMain", StringComparison.Ordinal) == true,
            "PS5 async debugger event lost the thread name.");

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await debugger.DisposeAsync().ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerTransportIsolationAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveFollowUpProcessList: true,
            serveDebugger: true);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        IProcessProvider processProvider = targetSession.GetRequiredService<IProcessProvider>();
        IReadOnlyList<TargetProcess> initialProcesses = await processProvider
            .GetProcessesAsync(CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = initialProcesses.Single(item => item.Id == 2222);

        IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        IReadOnlyList<TargetProcess> followUpProcesses = await processProvider
            .GetProcessesAsync(CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(3, followUpProcesses.Count, "The ordinary PS5 command stream stopped working while the dedicated debugger transport was attached.");
        AssertEqual(DebuggerExecutionState.Running, debugger.State, "Ordinary PS5 command traffic changed the debugger state.");

        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await debugger.DisposeAsync().ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerThreadServicesAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveDebugger: true);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        await using IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(new TargetProcess(2222, "eboot.bin"), CancellationToken.None)
            .ConfigureAwait(false);

        IDebuggerThreadService threadService = debugger.GetRequiredService<IDebuggerThreadService>();
        IDebuggerThreadControlService threadControl = debugger.GetRequiredService<IDebuggerThreadControlService>();
        IReadOnlyList<DebuggerThreadInfo> threads = await threadService
            .GetThreadsAsync(CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(3, threads.Count, "PS5 debugger thread-list response was parsed with the wrong count.");
        AssertEqual("MainThread", threads.Single(thread => thread.Id == 0x101).Name, "PS5 debugger thread-info name was not decoded.");
        AssertEqual("Worker", threads.Single(thread => thread.Id == 0x102).Name, "PS5 debugger worker thread-info name was not decoded.");
        AssertTrue(threads.All(thread => thread.State == DebuggerThreadState.Running), "PS5 attached thread state did not map to Running.");

        await threadControl.SuspendThreadAsync(0x102, CancellationToken.None).ConfigureAwait(false);
        threads = await threadService.GetThreadsAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerThreadState.Suspended, threads.Single(thread => thread.Id == 0x102).State, "PS5 debugger did not retain successful per-thread suspend state.");

        await threadControl.ResumeThreadAsync(0x102, CancellationToken.None).ConfigureAwait(false);
        threads = await threadService.GetThreadsAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerThreadState.Running, threads.Single(thread => thread.Id == 0x102).State, "PS5 debugger did not clear successful per-thread suspend state after resume.");
        AssertEqual(2, server.DebuggerThreadActions.Count, "PS5 debugger sent an unexpected number of per-thread control commands.");
        AssertEqual("suspend:0x102", server.DebuggerThreadActions[0], "PS5 debugger sent the wrong suspend-thread command/body.");
        AssertEqual("resume:0x102", server.DebuggerThreadActions[1], "PS5 debugger sent the wrong resume-thread command/body.");

        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerRegisterServicesAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveDebugger: true);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        await using IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(new TargetProcess(2222, "eboot.bin"), CancellationToken.None)
            .ConfigureAwait(false);

        IDebuggerRegisterService registerService = debugger.GetRequiredService<IDebuggerRegisterService>();
        await AssertThrowsAsync<InvalidOperationException>(
            () => registerService.GetRegistersAsync(0x102, CancellationToken.None),
            "PS5 debugger exposed general registers while the target was running.").ConfigureAwait(false);

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        IReadOnlyList<DebuggerRegister> registers = await registerService
            .GetRegistersAsync(0x102, CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(26, registers.Count, "PS5 general-register mapper returned an unexpected register count.");
        AssertTrue(registers.All(register => !register.CanWrite),
            "PS5 register snapshots must remain read-only while the upstream SETREGS path is unverified.");
        AssertTrue(registers.All(register => register.ValueEncoding == DebuggerRegisterValueEncoding.UnsignedLittleEndian),
            "PS5 register snapshot did not declare the expected neutral little-endian numeric encoding.");

        DebuggerRegister instructionPointer = registers.Single(register => register.Role == DebuggerRegisterRole.InstructionPointer);
        DebuggerRegister stackPointer = registers.Single(register => register.Role == DebuggerRegisterRole.StackPointer);
        DebuggerRegister framePointer = registers.Single(register => register.Role == DebuggerRegisterRole.FramePointer);
        AssertEqual(0x0000000000420102UL, BinaryPrimitives.ReadUInt64LittleEndian(instructionPointer.Value.Span),
            "PS5 RIP offset was mapped incorrectly into the semantic instruction-pointer register.");
        AssertEqual(0x7000000000000102UL, BinaryPrimitives.ReadUInt64LittleEndian(stackPointer.Value.Span),
            "PS5 RSP offset was mapped incorrectly into the semantic stack-pointer register.");
        AssertEqual(0x7000000000000202UL, BinaryPrimitives.ReadUInt64LittleEndian(framePointer.Value.Span),
            "PS5 RBP offset was mapped incorrectly into the semantic frame-pointer register.");
        AssertEqual(1, server.DebuggerRegisterReadThreadIds.Count,
            "PS5 register service sent an unexpected number of GETREGS requests.");
        AssertEqual((uint)0x102, server.DebuggerRegisterReadThreadIds[0],
            "PS5 register service sent GETREGS for the wrong LWP id.");

        await AssertThrowsAsync<NotSupportedException>(
            () => registerService.WriteRegisterAsync(
                0x102,
                new DebuggerRegisterWriteRequest("rax", new byte[8]),
                CancellationToken.None),
            "PS5 register service exposed the currently unverified upstream register-write path.").ConfigureAwait(false);
        AssertEqual(1, server.DebuggerRegisterReadThreadIds.Count,
            "Rejected PS5 register editing unexpectedly generated extra backend traffic.");

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static Task VerifyMainWorkspaceTargetLayoutAsync()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        int targetStripStart = xaml.IndexOf("<!-- Target and connection controls", StringComparison.Ordinal);
        int firstRowStart = xaml.IndexOf("Permanent first row:", targetStripStart, StringComparison.Ordinal);
        int secondRowStart = xaml.IndexOf("Permanent second row:", firstRowStart, StringComparison.Ordinal);
        AssertTrue(targetStripStart >= 0 && firstRowStart > targetStripStart && secondRowStart > firstRowStart,
            "Main workspace target strip row markers could not be located.");

        string firstRow = xaml[firstRowStart..secondRowStart];
        int platformIndex = firstRow.IndexOf("Text=\"Platform\"", StringComparison.Ordinal);
        int settingsIndex = firstRow.IndexOf("SelectedPlugin.ConnectionSettings", StringComparison.Ordinal);
        int connectIndex = firstRow.IndexOf("Content=\"Connect\"", StringComparison.Ordinal);
        int disconnectIndex = firstRow.IndexOf("Content=\"Disconnect\"", StringComparison.Ordinal);
        int processIndex = firstRow.IndexOf("Text=\"Target Process\"", StringComparison.Ordinal);
        int refreshIndex = firstRow.IndexOf("Content=\"Refresh\"", StringComparison.Ordinal);
        int activeIndex = firstRow.IndexOf("Content=\"Set Active Target\"", StringComparison.Ordinal);
        AssertTrue(platformIndex >= 0 &&
                   settingsIndex > platformIndex &&
                   connectIndex > settingsIndex &&
                   disconnectIndex > connectIndex &&
                   processIndex > disconnectIndex &&
                   refreshIndex > processIndex &&
                   activeIndex > refreshIndex,
            "The first target row does not follow Platform -> plugin inputs -> Connect -> Disconnect -> Target Process -> Refresh -> Set Active Target.");

        int secondRowEnd = xaml.IndexOf("SelectedPlugin.ConnectionErrorText", secondRowStart, StringComparison.Ordinal);
        AssertTrue(secondRowEnd > secondRowStart, "The end of the second target row could not be located.");
        string secondRow = xaml[secondRowStart..secondRowEnd];
        int reloadIndex = secondRow.IndexOf("Content=\"Reload Plugins\"", StringComparison.Ordinal);
        int disassemblerIndex = secondRow.IndexOf("Content=\"Disassembler...\"", StringComparison.Ordinal);
        int debuggerIndex = secondRow.IndexOf("Content=\"Debugger...\"", StringComparison.Ordinal);
        AssertTrue(reloadIndex >= 0 && disassemblerIndex > reloadIndex && debuggerIndex > disassemblerIndex,
            "The second target row does not follow Reload Plugins -> Disassembler -> Debugger.");
        AssertTrue(secondRow.Contains("Orientation=\"Horizontal\"", StringComparison.Ordinal) &&
                   secondRow.Contains("HorizontalAlignment=\"Left\"", StringComparison.Ordinal),
            "The second-row action lane is not left-aligned and horizontal.");

        AssertFalse(xaml.Contains("SelectedPlugin.ActiveProcessText", StringComparison.Ordinal),
            "Main workspace still displays the removed Active <process> text.");
        AssertFalse(xaml.Contains("SelectedPlugin.ProcessStatusText", StringComparison.Ordinal),
            "Main workspace still displays process enumeration/status prose.");
        AssertFalse(xaml.Contains("SelectedPlugin.ConnectionStatusText", StringComparison.Ordinal),
            "Main workspace still renders variable connection-operation status instead of the permanent binary indicator.");

        int statusBarStart = xaml.IndexOf("<Border Grid.Row=\"3\"", StringComparison.Ordinal);
        string statusBar = statusBarStart >= 0 ? xaml[statusBarStart..] : string.Empty;
        AssertTrue(statusBar.Contains("SelectedPlugin.IsConnected", StringComparison.Ordinal) &&
                   statusBar.Contains("Value=\"Not connected\"", StringComparison.Ordinal) &&
                   statusBar.Contains("Value=\"Connected\"", StringComparison.Ordinal) &&
                   statusBar.Contains("SuccessTextBrush", StringComparison.Ordinal) &&
                   statusBar.Contains("DangerButtonBorderBrush", StringComparison.Ordinal),
            "Bottom status bar does not contain the binary red/green connection-status box at its left edge.");

        return Task.CompletedTask;
    }

    private static Task VerifyMainWorkspaceMemoryMapStatusRemovalAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string mainWindowXaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml"));
        string pluginViewModelSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "PluginViewModel.cs"));

        AssertFalse(
            mainWindowXaml.Contains("SelectedPlugin.MemoryRegionStatusText", StringComparison.Ordinal),
            "Main workspace still renders passive memory-map status prose in the permanent target header.");
        AssertTrue(
            mainWindowXaml.Contains("SelectedPlugin.MemoryRegionErrorText", StringComparison.Ordinal),
            "Removing passive memory-map status also removed the established visible memory-map error surface.");
        AssertTrue(
            pluginViewModelSource.Contains("GetMemoryRegionsAsync", StringComparison.Ordinal) &&
            pluginViewModelSource.Contains("ActiveMemoryRegions = regions", StringComparison.Ordinal),
            "Memory-map status cleanup altered or removed the underlying Active Target memory-map load path.");

        return Task.CompletedTask;
    }

    private static Task VerifyMainWorkspaceTwoRowHeaderAsync()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        string metrics = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "UiMetrics.cs"));
        int targetStripStart = xaml.IndexOf("<!-- Target and connection controls", StringComparison.Ordinal);
        int targetStripEnd = xaml.IndexOf("<!-- Main memory workspace", targetStripStart, StringComparison.Ordinal);
        AssertTrue(targetStripStart >= 0 && targetStripEnd > targetStripStart,
            "Main workspace target strip could not be isolated.");

        string targetStrip = xaml[targetStripStart..targetStripEnd];
        AssertEqual(1, Regex.Matches(targetStrip, "Permanent first row:").Count,
            "The target strip should contain exactly one permanent first row.");
        AssertEqual(1, Regex.Matches(targetStrip, "Permanent second row:").Count,
            "The target strip should contain exactly one permanent second row.");
        AssertTrue(metrics.Contains("TopTargetInputMaxWidth = 180d", StringComparison.Ordinal),
            "The shared top-target input maximum is not centralized at 180 units.");
        AssertTrue(Regex.Matches(targetStrip, "UiMetrics.TopTargetInputMaxWidth").Count >= 3,
            "Platform, plugin connection inputs, and Target Process are not all capped by the shared 180-unit top-target maximum.");
        AssertTrue(Regex.Matches(targetStrip, "TopTargetInputWidth, RelativeSource").Count >= 3,
            "Platform, plugin connection inputs, and Target Process are not all consuming the shared responsive top-target width.");
        AssertTrue(targetStrip.Contains("SelectedPlugin.ConnectionSettings", StringComparison.Ordinal) &&
                   targetStrip.IndexOf("SelectedPlugin.ConnectionSettings", StringComparison.Ordinal) <
                   targetStrip.IndexOf("Content=\"Connect\"", StringComparison.Ordinal),
            "Plugin-declared connection inputs are not placed before Connect on the permanent first row.");

        return Task.CompletedTask;
    }

    private static Task VerifyMainWorkspaceResponsiveTopInputWidthAsync()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        string codeBehind = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml.cs"));
        string metrics = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "UiMetrics.cs"));

        AssertTrue(metrics.Contains("TopTargetInputMaxWidth = 180d", StringComparison.Ordinal),
            "The responsive target-input maximum is not 180 units.");
        AssertTrue(xaml.Contains("x:Name=\"TopTargetFirstRow\"", StringComparison.Ordinal) &&
                   xaml.Contains("LayoutUpdated=\"TopTargetFirstRow_LayoutUpdated\"", StringComparison.Ordinal),
            "The first target row is not wired to the responsive width recalculation.");
        AssertTrue(codeBehind.Contains("TargetConnectionBar.ActualWidth", StringComparison.Ordinal) &&
                   codeBehind.Contains("TargetConnectionBar.Padding.Left", StringComparison.Ordinal) &&
                   codeBehind.Contains("TargetConnectionBar.Padding.Right", StringComparison.Ordinal) &&
                   codeBehind.Contains("TopTargetConnectionSettings.Items.Count", StringComparison.Ordinal) &&
                   codeBehind.Contains("GetHorizontalOuterWidth(ConnectButton)", StringComparison.Ordinal) &&
                   codeBehind.Contains("GetHorizontalOuterWidth(DisconnectButton)", StringComparison.Ordinal),
            "Responsive target-input sizing does not account for the padded target-bar content width, dynamic plugin input count, and fixed connection actions.");
        AssertTrue(codeBehind.Contains("Math.Max(0d, rowWidth - fixedWidth - spacing)", StringComparison.Ordinal) &&
                   codeBehind.Contains("Math.Min(\n            UiMetrics.TopTargetInputMaxWidth", StringComparison.Ordinal),
            "Responsive target-input sizing is not clamped from zero through the shared 180-unit maximum.");
        AssertTrue(Regex.Matches(xaml, "MaxWidth=\"{x:Static application:UiMetrics.TopTargetInputMaxWidth}\"").Count >= 3,
            "Ordinary first-row target/plugin inputs are not all explicitly capped at the shared maximum.");
        AssertFalse(xaml.Contains("MinWidth=\"{x:Static application:UiMetrics.TopTargetInputMaxWidth}\"", StringComparison.Ordinal),
            "Ordinary first-row target/plugin inputs incorrectly use the maximum as a minimum width.");

        return Task.CompletedTask;
    }

    private static Task VerifySharedButtonContentAlignmentAsync()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ButtonStyles.xaml"));
        AssertTrue(xaml.Contains("<Setter Property=\"Height\" Value=\"{x:Static application:UiMetrics.StandardControlHeight}\" />", StringComparison.Ordinal),
            "Shared buttons no longer retain the standard control height.");
        AssertTrue(xaml.Contains("<Setter Property=\"Padding\" Value=\"14,2\" />", StringComparison.Ordinal),
            "Shared button vertical content padding was not reduced while preserving horizontal padding.");
        AssertTrue(xaml.Contains("<Setter Property=\"HorizontalContentAlignment\" Value=\"Center\" />", StringComparison.Ordinal) &&
                   xaml.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Center\" />", StringComparison.Ordinal),
            "Shared button content alignment is not centered in both axes.");
        AssertTrue(Regex.Matches(xaml, "<Setter Property=\"VerticalAlignment\" Value=\"Center\" />").Count >= 2,
            "Generated AccessText/TextBlock button content is not explicitly vertically centered.");

        return Task.CompletedTask;
    }

    private static async Task VerifyPs5DisassemblyDecodingAsync()
    {
        await using Ps5ProtocolTestServer server = new();
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        IDisassemblerProvider provider = session.GetRequiredService<IDisassemblerProvider>();
        const ulong startAddress = 0x0000000010000000;
        byte[] code =
        {
            0x90,
            0x48, 0x89, 0xD8,
            0x48, 0x83, 0xC0, 0x05,
            0x48, 0x83, 0xE8, 0x02,
            0x48, 0x83, 0xF8, 0x03,
            0xE8, 0x05, 0x00, 0x00, 0x00,
            0xEB, 0x03,
            0x75, 0x01,
            0xC3,
            0x90,
            0x48, 0x8B, 0x05, 0x78, 0x56, 0x34, 0x12
        };

        IReadOnlyList<DisassembledInstruction> instructions = await provider
            .DisassembleAsync(startAddress, code, session.Architecture, CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(11, instructions.Count, "PS5 x86-64 fixture produced an unexpected instruction count.");
        AssertEqual("nop", instructions[0].Mnemonic, "PS5 x86-64 NOP mnemonic was decoded incorrectly.");
        AssertEqual(1, instructions[0].Length, "PS5 x86-64 NOP length was decoded incorrectly.");
        AssertEqual("mov", instructions[1].Mnemonic, "PS5 x86-64 MOV mnemonic was decoded incorrectly.");
        AssertEqual(3, instructions[1].Length, "PS5 x86-64 MOV length was decoded incorrectly.");
        AssertTrue(instructions[1].Operands.Contains("rax", StringComparison.OrdinalIgnoreCase) &&
                   instructions[1].Operands.Contains("rbx", StringComparison.OrdinalIgnoreCase),
            "PS5 x86-64 MOV operands were not formatted as registers.");
        AssertTrue(instructions[1].SyntaxTokens.Any(token => token.Kind == DisassemblyTextTokenKind.Mnemonic),
            "PS5 x86-64 provider did not expose a neutral mnemonic syntax token.");
        AssertTrue(instructions[1].SyntaxTokens.Any(token => token.Kind == DisassemblyTextTokenKind.Register),
            "PS5 x86-64 provider did not expose neutral register syntax tokens.");
        AssertEqual(
            $"{instructions[1].Mnemonic} {instructions[1].Operands}",
            string.Concat(instructions[1].SyntaxTokens.Select(token => token.Text)),
            "PS5 x86-64 syntax tokenization changed the rendered MOV text.");
        AssertEqual("add", instructions[2].Mnemonic, "PS5 x86-64 ADD mnemonic was decoded incorrectly.");
        AssertEqual("sub", instructions[3].Mnemonic, "PS5 x86-64 SUB mnemonic was decoded incorrectly.");
        AssertEqual("cmp", instructions[4].Mnemonic, "PS5 x86-64 CMP mnemonic was decoded incorrectly.");

        ulong expectedFlowTarget = startAddress + 0x1AUL;
        AssertEqual(DisassemblyFlowControl.Call, instructions[5].FlowControl, "PS5 CALL flow-control classification was incorrect.");
        AssertEqual(DisassemblyTextTokenKind.FlowControlMnemonic, instructions[5].SyntaxTokens[0].Kind,
            "PS5 CALL mnemonic was not classified as flow control for presentation.");
        AssertTrue(instructions[5].SyntaxTokens.Any(token => token.Kind == DisassemblyTextTokenKind.Number),
            "PS5 direct CALL target was not exposed as neutral numeric syntax metadata.");
        AssertEqual<ulong?>(expectedFlowTarget, instructions[5].BranchTarget, "PS5 CALL direct target was decoded incorrectly.");
        AssertEqual(DisassemblyFlowControl.Jump, instructions[6].FlowControl, "PS5 JMP flow-control classification was incorrect.");
        AssertEqual<ulong?>(expectedFlowTarget, instructions[6].BranchTarget, "PS5 JMP direct target was decoded incorrectly.");
        AssertEqual(DisassemblyFlowControl.ConditionalJump, instructions[7].FlowControl, "PS5 conditional branch classification was incorrect.");
        AssertEqual<ulong?>(expectedFlowTarget, instructions[7].BranchTarget, "PS5 conditional branch target was decoded incorrectly.");
        AssertEqual(DisassemblyFlowControl.Return, instructions[8].FlowControl, "PS5 RET flow-control classification was incorrect.");
        AssertEqual("nop", instructions[9].Mnemonic, "PS5 post-branch NOP was decoded incorrectly.");
        AssertEqual("mov", instructions[10].Mnemonic, "PS5 RIP-relative MOV mnemonic was decoded incorrectly.");
        AssertEqual(7, instructions[10].Length, "PS5 RIP-relative MOV length was decoded incorrectly.");
        AssertTrue(!string.IsNullOrWhiteSpace(instructions[10].Operands),
            "PS5 RIP-relative MOV did not expose formatted operands.");
        AssertTrue(instructions.All(instruction => instruction.IsValid),
            "PS5 deterministic valid-code fixture unexpectedly produced an invalid instruction.");

        int consumedByteCount = instructions.Sum(instruction => instruction.Length);
        AssertEqual(code.Length, consumedByteCount,
            "PS5 x86-64 provider did not consume the deterministic byte fixture exactly once.");

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DisassemblySafetyAsync()
    {
        await using Ps5ProtocolTestServer server = new();
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);

        IDisassemblerProvider provider = session.GetRequiredService<IDisassemblerProvider>();
        AssertTrue(provider.SupportsArchitecture(session.Architecture),
            "PS5 disassembly provider rejected the connected PS5 x86-64 architecture.");
        AssertFalse(provider.SupportsArchitecture(new TargetArchitecture(
                CpuArchitecture.X86,
                pointerWidthBits: 32,
                addressWidthBits: 32,
                endianness: Endianness.Little)),
            "PS5 disassembly provider accepted a 32-bit x86 target.");
        AssertFalse(provider.SupportsArchitecture(new TargetArchitecture(
                CpuArchitecture.X64,
                pointerWidthBits: 64,
                addressWidthBits: 64,
                endianness: Endianness.Big)),
            "PS5 disassembly provider accepted a big-endian x86-64 target.");

        IReadOnlyList<DisassembledInstruction> invalid = await provider
            .DisassembleAsync(
                0x0000000020000000,
                new byte[] { 0x0F },
                session.Architecture,
                CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(1, invalid.Count, "PS5 truncated x86-64 fixture did not produce one bounded invalid record.");
        AssertFalse(invalid[0].IsValid, "PS5 truncated x86-64 fixture was incorrectly marked valid.");
        AssertEqual("invalid", invalid[0].Mnemonic, "PS5 invalid instruction marker changed unexpectedly.");
        AssertEqual(1, invalid[0].Length, "PS5 invalid instruction did not retain its source byte.");
        AssertEqual<ulong?>(null, invalid[0].BranchTarget, "PS5 invalid instruction fabricated a branch target.");

        IReadOnlyList<DisassembledInstruction> indirectFlow = await provider
            .DisassembleAsync(
                0x0000000020000008,
                new byte[] { 0xFF, 0xD0, 0xFF, 0xE0 },
                session.Architecture,
                CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(2, indirectFlow.Count, "PS5 indirect-flow fixture produced an unexpected instruction count.");
        AssertEqual(DisassemblyFlowControl.Call, indirectFlow[0].FlowControl, "PS5 indirect CALL flow-control classification was incorrect.");
        AssertEqual<ulong?>(null, indirectFlow[0].BranchTarget, "PS5 indirect CALL fabricated a direct target.");
        AssertEqual(DisassemblyFlowControl.Jump, indirectFlow[1].FlowControl, "PS5 indirect JMP flow-control classification was incorrect.");
        AssertEqual<ulong?>(null, indirectFlow[1].BranchTarget, "PS5 indirect JMP fabricated a direct target.");

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(
            () => provider.DisassembleAsync(
                0x0000000020000010,
                new byte[] { 0x90 },
                session.Architecture,
                cancelled.Token),
            "PS5 x86-64 disassembly provider ignored a cancelled decode request.").ConfigureAwait(false);

        await AssertThrowsAsync<NotSupportedException>(
            () => provider.DisassembleAsync(
                0x0000000020000020,
                new byte[] { 0x90 },
                new TargetArchitecture(
                    CpuArchitecture.Arm64,
                    pointerWidthBits: 64,
                    addressWidthBits: 64,
                    endianness: Endianness.Little),
                CancellationToken.None),
            "PS5 x86-64 provider decoded an unsupported architecture instead of rejecting it.").ConfigureAwait(false);

        await server.Completion.ConfigureAwait(false);
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
        IMemoryValueType valueType = StandardMemoryValueTypes.Int32;
        IMemoryScanType scanType = ExactScanType;
        MemoryScanValue inputValue = ParseValue(valueType, int.MinValue.ToString(CultureInfo.InvariantCulture), session.Architecture);
        Task<MemoryScanExecutionResult> scanTask = scanner.FirstScanAsync(
            process,
            regions,
            session.GetRequiredService<IMemoryReader>(),
            session.Architecture,
            valueType,
            scanType,
            new[] { inputValue },
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
        AssertTrue(session.GetService<INativeValueScanStreamProvider>() is not null, "PS5 streaming TurboScan service was not exposed after capability probing.");
        AssertTrue(session.GetService<INativeValueScanStreamRefiner>() is not null, "PS5 streaming TurboScan refinement service was not exposed after capability probing.");

        INativeScanTypeMappingProvider nativeMappings = session.GetRequiredService<INativeScanTypeMappingProvider>();
        AssertEqual(11, nativeMappings.NativeScanTypeMappings.Count, "Unexpected PS5 semantically equivalent native Scan Type mapping count.");
        AssertTrue(nativeMappings.NativeScanTypeMappings.Any(mapping => mapping.CoreScanTypeId == StandardMemoryScanTypeIds.ExactValue && mapping.NativeScanTypeId == "ps5debug-ng.compare.0"), "PS5 Exact Value native mapping is missing or incorrect.");
        AssertTrue(nativeMappings.NativeScanTypeMappings.Any(mapping => mapping.CoreScanTypeId == StandardMemoryScanTypeIds.FuzzyValue && mapping.NativeScanTypeId == "ps5debug-ng.compare.1"), "PS5 Fuzzy Value native mapping is missing or incorrect.");
        AssertTrue(nativeMappings.NativeScanTypeMappings.Any(mapping => mapping.CoreScanTypeId == StandardMemoryScanTypeIds.UnknownInitialValue && mapping.NativeScanTypeId == "ps5debug-ng.snapshot.include-zeros"), "PS5 Unknown Initial Value must use the snapshot mapping that explicitly includes zero-valued candidates.");
        AssertTrue(nativeMappings.NativeScanTypeMappings.Any(mapping => mapping.CoreScanTypeId == StandardMemoryScanTypeIds.UnknownInitialLowValue && mapping.NativeScanTypeId == "ps5debug-ng.compare.12"), "PS5 Unknown Initial Low Value native mapping is missing or incorrect.");
        AssertFalse(nativeMappings.NativeScanTypeMappings.Any(mapping => mapping.CoreScanTypeId == StandardMemoryScanTypeIds.UnknownInitialValue && mapping.NativeScanTypeId == "ps5debug-ng.compare.11"), "PS5 cmpType 11 must not be used directly for Core Unknown Initial Value because that comparator excludes zero values.");
        AssertFalse(nativeMappings.NativeScanTypeMappings.Any(mapping => mapping.CoreScanTypeId == StandardMemoryScanTypeIds.IncreasedBy), "PS5 Increased By must use Core fallback because payload-width wrapping semantics are not fully equivalent to Core.");
        AssertFalse(nativeMappings.NativeScanTypeMappings.Any(mapping => mapping.CoreScanTypeId == StandardMemoryScanTypeIds.DecreasedBy), "PS5 Decreased By must use Core fallback because payload-width wrapping semantics are not fully equivalent to Core.");

        MemoryScanner scanner = new();
        IMemoryValueType valueType = StandardMemoryValueTypes.Int32;
        IMemoryScanType scanType = ExactScanType;
        MemoryScanValue inputValue = ParseValue(valueType, "10002", session.Architecture);
        MemoryScanExecutionResult result = await scanner
            .FirstScanNativeAsync(
                process,
                regions,
                session.GetRequiredService<INativeValueScanner>(),
                session.Architecture,
                valueType,
                scanType,
                new[] { inputValue },
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

    private static async Task VerifyPs5NativeMappedScanTypesAsync()
    {
        TargetArchitecture architecture = new(CpuArchitecture.X64, 64, 64, Endianness.Little);
        MemoryScanValue lower = ParseValue(StandardMemoryValueTypes.Int32, "9000", architecture);
        MemoryScanValue upper = ParseValue(StandardMemoryValueTypes.Int32, "11000", architecture);
        MemoryScanValue returnedCurrent = ParseValue(StandardMemoryValueTypes.Int32, "9999", architecture);
        byte[] betweenPayload = lower.Bytes.ToArray().Concat(upper.Bytes.ToArray()).ToArray();

        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true,
            serveNativeRefinement: true,
            nativeStartCompareType: 4,
            nativeRefinementCompareType: 9,
            nativeStartComparisonData: betweenPayload,
            nativeRefinementValueData: Array.Empty<byte>(),
            nativeRefinementReturnedCurrentValueData: returnedCurrent.Bytes.ToArray());
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single(item => item.Id == 2222);
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);

        IMemoryScanType between = MemoryScanTypeCatalog.All.Single(item => item.Id == StandardMemoryScanTypeIds.Between);
        IMemoryScanType changed = MemoryScanTypeCatalog.All.Single(item => item.Id == StandardMemoryScanTypeIds.ChangedValue);
        MemoryScanner scanner = new();
        MemoryScanExecutionResult first = await scanner.FirstScanNativeAsync(
            process,
            regions,
            session.GetRequiredService<INativeValueScanner>(),
            session.Architecture,
            StandardMemoryValueTypes.Int32,
            between,
            new[] { lower, upper },
            CancellationToken.None).ConfigureAwait(false);

        AssertEqual(2, first.Results.Count, "Native PS5 Between First Scan did not return the expected resident results.");

        MemoryScanExecutionResult next = await scanner.NextScanNativeAsync(
            process,
            regions,
            first.Results,
            session.GetRequiredService<INativeValueScanRefiner>(),
            session.Architecture,
            StandardMemoryValueTypes.Int32,
            changed,
            Array.Empty<MemoryScanValue>(),
            CancellationToken.None).ConfigureAwait(false);

        AssertEqual(1, next.Results.Count, "Native PS5 Changed Value refinement did not return the expected survivor.");
        AssertEqual(returnedCurrent.DisplayText, next.Results[0].CurrentValue.DisplayText, "Native PS5 mapped refinement did not preserve the returned current value.");

        await session.GetRequiredService<INativeValueScanRefiner>()
            .ResetAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5NativeUnknownInitialSnapshotAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true,
            serveNativeRefinement: true,
            nativeStartCompareType: 11,
            nativeRefinementCompareType: 9,
            nativeStartSnapshot: true,
            nativeSnapshotProgressRecordCount: 1500,
            nativeStartComparisonData: Array.Empty<byte>(),
            nativeRefinementValueData: Array.Empty<byte>(),
            nativeRefinementReturnedCurrentValueData: BitConverter.GetBytes(10001));
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single(item => item.Id == 2222);
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);

        IMemoryScanType unknown = MemoryScanTypeCatalog.All.Single(item => item.Id == StandardMemoryScanTypeIds.UnknownInitialValue);
        IMemoryScanType changed = MemoryScanTypeCatalog.All.Single(item => item.Id == StandardMemoryScanTypeIds.ChangedValue);
        MemoryScanner scanner = new();
        MemoryScanExecutionResult first = await scanner.FirstScanNativeAsync(
            process,
            regions,
            session.GetRequiredService<INativeValueScanner>(),
            session.Architecture,
            StandardMemoryValueTypes.Int32,
            unknown,
            Array.Empty<MemoryScanValue>(),
            CancellationToken.None).ConfigureAwait(false);

        AssertEqual(2, first.Results.Count, "Native PS5 Unknown Initial Value snapshot did not return all expected candidates.");

        MemoryScanExecutionResult next = await scanner.NextScanNativeAsync(
            process,
            regions,
            first.Results,
            session.GetRequiredService<INativeValueScanRefiner>(),
            session.Architecture,
            StandardMemoryValueTypes.Int32,
            changed,
            Array.Empty<MemoryScanValue>(),
            CancellationToken.None).ConfigureAwait(false);

        AssertEqual(1, next.Results.Count, "PS5 snapshot session did not transition into native Changed Value refinement correctly.");

        await session.GetRequiredService<INativeValueScanRefiner>()
            .ResetAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5ResidentTurboScanAsync()
    {
        byte[] initialValue = BitConverter.GetBytes(10002);
        byte[] refinedValue = BitConverter.GetBytes(10001);
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true,
            serveNativeRefinement: true,
            nativeStartCompareType: 0,
            nativeRefinementCompareType: 9,
            nativeRefinementProgressRecordCount: 1500,
            nativeValueData: initialValue,
            nativeStartComparisonData: initialValue,
            nativeRefinementValueData: Array.Empty<byte>(),
            nativeRefinementReturnedCurrentValueData: refinedValue);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single(item => item.Id == 2222);
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);
        MemoryScanner scanner = new();
        MemoryScanValue exactValue = ParseValue(StandardMemoryValueTypes.Int32, "10002", session.Architecture);
        IMemoryScanType exact = MemoryScanTypeCatalog.All.Single(item => item.Id == StandardMemoryScanTypeIds.ExactValue);
        IMemoryScanType changed = MemoryScanTypeCatalog.All.Single(item => item.Id == StandardMemoryScanTypeIds.ChangedValue);

        INativeValueScanResultStream firstStream = await scanner.StartFirstScanNativeStreamAsync(
                process,
                regions,
                session.GetRequiredService<INativeValueScanStreamProvider>(),
                session.Architecture,
                StandardMemoryValueTypes.Int32,
                exact,
                new[] { exactValue },
                MemoryScanOptions.Empty,
                CancellationToken.None)
            .ConfigureAwait(false);
        try
        {
            AssertTrue(
                firstStream is INativeValueScanResidentResultSet,
                "PS5 TurboScan streaming did not expose the API 2.9 resident result contract.");
            INativeValueScanResidentResultSet firstResident = (INativeValueScanResidentResultSet)firstStream;
            AssertTrue(firstResident.IsAuthoritative, "Integer Exact Value TurboScan must be authoritative.");

            MemoryScanExecutionResult firstPreview = await scanner.ReadNativeResidentPreviewAsync(
                    regions,
                    firstResident,
                    session.Architecture,
                    StandardMemoryValueTypes.Int32,
                    maximumPreviewResults: 2,
                    includePreviousValues: false,
                    CancellationToken.None)
                .ConfigureAwait(false);
            AssertEqual(2L, firstPreview.TotalResultCount, "PS5 resident preview reported an unexpected total result count.");
            AssertEqual(2, firstPreview.Results.Count, "PS5 resident preview did not fetch the expected bounded window.");

            AssertTrue(
                scanner.CanRefineNativeResidentResultSet(
                    firstResident,
                    session.Architecture,
                    StandardMemoryValueTypes.Int32,
                    changed,
                    Array.Empty<MemoryScanValue>(),
                    MemoryScanOptions.Empty),
                "PS5 resident result set unexpectedly rejected Changed Value refinement.");

            INativeValueScanResultStream refinedStream = await scanner.StartNextScanNativeStreamAsync(
                    process,
                    firstResident,
                    session.GetRequiredService<INativeValueScanStreamRefiner>(),
                    session.Architecture,
                    StandardMemoryValueTypes.Int32,
                    changed,
                    Array.Empty<MemoryScanValue>(),
                    MemoryScanOptions.Empty,
                    CancellationToken.None)
                .ConfigureAwait(false);
            try
            {
                AssertTrue(
                    refinedStream is INativeValueScanResidentResultSet,
                    "PS5 native refinement did not return a replacement resident result handle.");
                INativeValueScanResidentResultSet refinedResident = (INativeValueScanResidentResultSet)refinedStream;
                MemoryScanExecutionResult refinedPreview = await scanner.ReadNativeResidentPreviewAsync(
                        regions,
                        refinedResident,
                        session.Architecture,
                        StandardMemoryValueTypes.Int32,
                        maximumPreviewResults: 1,
                        includePreviousValues: true,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                AssertEqual(1L, refinedPreview.TotalResultCount, "PS5 resident refinement returned an unexpected survivor count.");
                AssertEqual("10001", refinedPreview.Results[0].CurrentValue.DisplayText, "PS5 resident refinement decoded an unexpected current value.");
                AssertEqual("10002", refinedPreview.Results[0].PreviousValue?.DisplayText, "PS5 resident refinement did not expose the previous scan value.");
            }
            finally
            {
                await refinedStream.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await firstStream.DisposeAsync().ConfigureAwait(false);
        }

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5FloatingChangedUnchangedBitwiseAsync()
    {
        await VerifyPs5FloatingBitwiseRefinementCaseAsync(
                StandardMemoryValueTypes.Float32,
                nativeStartValueType: 8,
                nativeRefinementValueType: 4,
                nativeRefinementCompareType: 9,
                initialBytes: new byte[] { 0x01, 0x00, 0xC0, 0x7F },
                refinedBytes: new byte[] { 0x02, 0x00, 0xC0, 0x7F },
                scanTypeId: StandardMemoryScanTypeIds.ChangedValue)
            .ConfigureAwait(false);

        await VerifyPs5FloatingBitwiseRefinementCaseAsync(
                StandardMemoryValueTypes.Float64,
                nativeStartValueType: 9,
                nativeRefinementValueType: 6,
                nativeRefinementCompareType: 10,
                initialBytes: new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0x7F },
                refinedBytes: new byte[] { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0x7F },
                scanTypeId: StandardMemoryScanTypeIds.UnchangedValue)
            .ConfigureAwait(false);
    }

    private static async Task VerifyPs5FloatingBitwiseRefinementCaseAsync(
        IMemoryValueType valueType,
        byte nativeStartValueType,
        byte nativeRefinementValueType,
        byte nativeRefinementCompareType,
        byte[] initialBytes,
        byte[] refinedBytes,
        string scanTypeId)
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true,
            serveNativeRefinement: true,
            nativeValueType: nativeStartValueType,
            nativeAlignment: checked((byte)valueType.DefaultAlignment),
            nativeStartCompareType: 11,
            nativeRefinementValueType: nativeRefinementValueType,
            nativeRefinementCompareType: nativeRefinementCompareType,
            nativeStartSnapshot: true,
            nativeValueData: initialBytes,
            nativeStartComparisonData: Array.Empty<byte>(),
            nativeRefinementValueData: Array.Empty<byte>(),
            nativeRefinementReturnedCurrentValueData: refinedBytes);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single(item => item.Id == 2222);
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);
        IMemoryScanType unknown = MemoryScanTypeCatalog.All.Single(item =>
            item.Id == StandardMemoryScanTypeIds.UnknownInitialValue);
        IMemoryScanType refinement = MemoryScanTypeCatalog.All.Single(item => item.Id == scanTypeId);
        MemoryScanner scanner = new();

        INativeValueScanResultStream firstStream = await scanner.StartFirstScanNativeStreamAsync(
                process,
                regions,
                session.GetRequiredService<INativeValueScanStreamProvider>(),
                session.Architecture,
                valueType,
                unknown,
                Array.Empty<MemoryScanValue>(),
                MemoryScanOptions.Empty,
                CancellationToken.None)
            .ConfigureAwait(false);
        try
        {
            INativeValueScanResidentResultSet firstResident = (INativeValueScanResidentResultSet)firstStream;
            await scanner.ReadNativeResidentPreviewAsync(
                    regions,
                    firstResident,
                    session.Architecture,
                    valueType,
                    maximumPreviewResults: 2,
                    includePreviousValues: false,
                    CancellationToken.None)
                .ConfigureAwait(false);

            INativeValueScanResultStream refinedStream = await scanner.StartNextScanNativeStreamAsync(
                    process,
                    firstResident,
                    session.GetRequiredService<INativeValueScanStreamRefiner>(),
                    session.Architecture,
                    valueType,
                    refinement,
                    Array.Empty<MemoryScanValue>(),
                    MemoryScanOptions.Empty,
                    CancellationToken.None)
                .ConfigureAwait(false);
            try
            {
                INativeValueScanResidentResultSet refinedResident = (INativeValueScanResidentResultSet)refinedStream;
                MemoryScanExecutionResult preview = await scanner.ReadNativeResidentPreviewAsync(
                        regions,
                        refinedResident,
                        session.Architecture,
                        valueType,
                        maximumPreviewResults: 1,
                        includePreviousValues: true,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                AssertEqual(1L, preview.TotalResultCount, $"PS5 native {valueType.DisplayName} {refinement.DisplayName} returned an unexpected survivor count.");
                AssertTrue(preview.Results[0].PreviousValue is not null, $"PS5 native {valueType.DisplayName} {refinement.DisplayName} did not expose the previous value.");
                AssertTrue(
                    preview.Results[0].CurrentValue.Bytes.Span.SequenceEqual(refinedBytes),
                    $"PS5 native {valueType.DisplayName} {refinement.DisplayName} returned unexpected current bytes.");
                AssertTrue(
                    preview.Results[0].PreviousValue!.Bytes.Span.SequenceEqual(initialBytes),
                    $"PS5 native {valueType.DisplayName} {refinement.DisplayName} returned unexpected previous bytes.");
            }
            finally
            {
                await refinedStream.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await firstStream.DisposeAsync().ConfigureAwait(false);
        }

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5NativeSnapshotRejectionRecoveryAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true,
            serveFollowUpProcessList: true,
            nativeStartCompareType: 11,
            nativeStartSnapshot: true,
            nativeSnapshotProgressRecordCount: 1500,
            nativeSnapshotStored: false,
            nativeStartComparisonData: Array.Empty<byte>());
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
        IProcessProvider processProvider = session.GetRequiredService<IProcessProvider>();
        TargetProcess process = (await processProvider
            .GetProcessesAsync(CancellationToken.None)
            .ConfigureAwait(false)).Single(item => item.Id == 2222);
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);

        IMemoryScanType unknown = MemoryScanTypeCatalog.All.Single(item =>
            item.Id == StandardMemoryScanTypeIds.UnknownInitialValue);
        MemoryScanner scanner = new();
        bool rejected = false;

        try
        {
            await scanner.StartFirstScanNativeStreamAsync(
                    process,
                    regions,
                    session.GetRequiredService<INativeValueScanStreamProvider>(),
                    session.Architecture,
                    StandardMemoryValueTypes.Int32,
                    unknown,
                    Array.Empty<MemoryScanValue>(),
                    MemoryScanOptions.Empty,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }

        AssertTrue(rejected, "A ps5debug-NG snapshot rejection must surface as a native-acceleration fallback signal.");

        IReadOnlyList<TargetProcess> followUpProcesses = await processProvider
            .GetProcessesAsync(CancellationToken.None)
            .ConfigureAwait(false);
        AssertTrue(
            followUpProcesses.Any(item => item.Id == 2222),
            "The ps5debug-NG command stream was not reusable after a rejected snapshot response.");

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5NativeCustomAlignmentAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true,
            nativeAlignment: 1);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single(item => item.Id == 2222);
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);
        MemoryScanValue value = ParseValue(StandardMemoryValueTypes.Int32, "10002", session.Architecture);
        MemoryScanOptions scanOptions = new(new[]
        {
            new KeyValuePair<string, string>(StandardMemoryScanOptionIds.Alignment, "1"),
            new KeyValuePair<string, string>(StandardMemoryScanOptionIds.Endianness, StandardMemoryScanOptionChoiceIds.LittleEndian),
            new KeyValuePair<string, string>(StandardMemoryScanOptionIds.FloatingPointRounding, StandardMemoryScanOptionChoiceIds.FloatingPointStrict)
        });

        MemoryScanExecutionResult result = await new MemoryScanner().FirstScanNativeAsync(
            process,
            regions,
            session.GetRequiredService<INativeValueScanner>(),
            session.Architecture,
            StandardMemoryValueTypes.Int32,
            ExactScanType,
            new[] { value },
            scanOptions,
            CancellationToken.None).ConfigureAwait(false);

        AssertEqual(2, result.Results.Count, "PS5 native scan with 1-byte alignment returned an unexpected result count.");
        await session.GetRequiredService<INativeValueScanRefiner>()
            .ResetAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5NativeValueTypesAsync()
    {
        TargetArchitecture ps5Architecture = new(CpuArchitecture.X64, 64, 64, Endianness.Little);
        (IMemoryValueType Type, string Text, byte WireType)[] fixtures =
        {
            (StandardMemoryValueTypes.UInt8, "250", 0),
            (StandardMemoryValueTypes.Int8, "-100", 1),
            (StandardMemoryValueTypes.UInt16, "60000", 2),
            (StandardMemoryValueTypes.Int16, "-12345", 3),
            (StandardMemoryValueTypes.UInt32, "4000000000", 4),
            (StandardMemoryValueTypes.Int32, "-123456789", 5),
            (StandardMemoryValueTypes.UInt64, "18364758544493064720", 6),
            (StandardMemoryValueTypes.Int64, "-1234567890123456789", 7),
            (StandardMemoryValueTypes.Float32, "123.25", 8),
            (StandardMemoryValueTypes.Float64, "-9876.5", 9),
            (StandardMemoryValueTypes.ByteArray, "DE AD BE EF 01", 10)
        };

        foreach ((IMemoryValueType type, string text, byte wireType) in fixtures)
        {
            MemoryScanValue value = ParseValue(type, text, ps5Architecture);

            await using Ps5ProtocolTestServer server = new(
                serveProcessList: true,
                serveMemoryMap: true,
                serveNativeScan: true,
                nativeValueType: wireType,
                nativeAlignment: checked((byte)value.Alignment),
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
                .FirstScanNativeAsync(
                    process,
                    regions,
                    session.GetRequiredService<INativeValueScanner>(),
                    session.Architecture,
                    type,
                    ExactScanType,
                    new[] { value },
                    CancellationToken.None)
                .ConfigureAwait(false);

            AssertEqual(2, result.Results.Count, $"PS5 native {type.DisplayName} scan returned an unexpected result count.");
            AssertTrue(result.Results.All(item => item.ValueTypeId == type.Id), $"PS5 native {type.DisplayName} scan returned the wrong Value Type.");
            AssertTrue(result.Results.All(item => item.CurrentValue.DisplayText == value.DisplayText), $"PS5 native {type.DisplayName} scan returned the wrong display value.");

            INativeValueScanRefiner refiner = session.GetRequiredService<INativeValueScanRefiner>();
            if (type.Id is StandardMemoryValueTypeIds.Float32 or StandardMemoryValueTypeIds.Float64)
            {
                bool strictExactFallbackRequested = false;
                try
                {
                    await refiner
                        .RefineAsync(
                            process,
                            result.Results.Select(item => item.Address).ToArray(),
                            new NativeValueScanRequest(
                                type.Id,
                                StandardMemoryScanTypeIds.ExactValue,
                                value.Size,
                                value.Alignment,
                                new[] { value.Bytes }),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (NotSupportedException)
                {
                    strictExactFallbackRequested = true;
                }

                AssertTrue(
                    strictExactFallbackRequested,
                    $"PS5 native {type.DisplayName} refinement must request shared-Core fallback to preserve strict Exact Value semantics.");
            }

            // Every native First Scan above leaves an authoritative TurboScan session resident.
            // Strict Float/Double refinement correctly requests Core fallback without ending that
            // session, so the fixture must explicitly reset it before waiting for the server.
            await refiner.ResetAsync(CancellationToken.None).ConfigureAwait(false);
            await server.Completion.ConfigureAwait(false);
        }
    }

    private static async Task VerifyPs5NativeValueRefinementAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true,
            serveNativeRefinement: true,
            nativeRefinementReturnedCurrentValueData: new byte[] { 0x0F, 0x27, 0x00, 0x00 });
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

        IMemoryValueType valueType = StandardMemoryValueTypes.Int32;
        IMemoryScanType scanType = ExactScanType;
        MemoryScanValue firstValue = ParseValue(valueType, "10002", session.Architecture);
        MemoryScanValue nextValue = ParseValue(valueType, "10001", session.Architecture);
        MemoryScanValue returnedCurrentValue = ParseValue(valueType, "9999", session.Architecture);
        MemoryScanner scanner = new();
        MemoryScanExecutionResult firstScan = await scanner
            .FirstScanNativeAsync(
                process,
                regions,
                session.GetRequiredService<INativeValueScanner>(),
                session.Architecture,
                valueType,
                scanType,
                new[] { firstValue },
                CancellationToken.None)
            .ConfigureAwait(false);

        MemoryScanExecutionResult nextScan = await scanner
            .NextScanNativeAsync(
                process,
                regions,
                firstScan.Results,
                session.GetRequiredService<INativeValueScanRefiner>(),
                session.Architecture,
                valueType,
                scanType,
                new[] { nextValue },
                CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(1, nextScan.Results.Count, "Native PS5 refinement did not return the expected survivor count.");
        AssertEqual((ulong)0x0000000200000080, nextScan.Results[0].Address, "Native PS5 refinement retained the wrong address.");
        AssertEqual(
            returnedCurrentValue.DisplayText,
            nextScan.Results[0].CurrentValue.DisplayText,
            "Native PS5 refinement rejected or rewrote the current-value payload returned for an authoritative TurboScan survivor.");
        AssertEqual("10002", nextScan.Results[0].PreviousValue?.DisplayText, "Native PS5 refinement did not preserve the host-side previous value.");

        await session
            .GetRequiredService<INativeValueScanRefiner>()
            .ResetAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5NativeFloatingToleranceAsync()
    {
        TargetArchitecture architecture = new(CpuArchitecture.X64, 64, 64, Endianness.Little);
        MemoryScanValue firstValue = ParseValue(StandardMemoryValueTypes.Float32, "100", architecture);
        MemoryScanValue nextValue = ParseValue(StandardMemoryValueTypes.Float32, "100.00005", architecture);

        await using Ps5ProtocolTestServer server = new(
            serveProcessList: true,
            serveMemoryMap: true,
            serveNativeScan: true,
            serveNativeRefinement: true,
            nativeValueType: 8,
            nativeAlignment: 4,
            nativeValueData: firstValue.Bytes.ToArray(),
            nativeRefinementValueData: nextValue.Bytes.ToArray());
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions connectionOptions = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession session = await plugin
            .ConnectAsync(connectionOptions, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single(item => item.Id == 2222);
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);
        MemoryScanOptions scanOptions = new(new[]
        {
            new KeyValuePair<string, string>(StandardMemoryScanOptionIds.Endianness, StandardMemoryScanOptionChoiceIds.LittleEndian),
            new KeyValuePair<string, string>(StandardMemoryScanOptionIds.Alignment, StandardMemoryScanOptionChoiceIds.DefaultAlignment),
            new KeyValuePair<string, string>(StandardMemoryScanOptionIds.FloatingPointRounding, StandardMemoryScanOptionChoiceIds.FloatingPointRelativeTolerance1E6)
        });
        MemoryScanner scanner = new();

        MemoryScanExecutionResult firstScan = await scanner.FirstScanNativeAsync(
            process,
            regions,
            session.GetRequiredService<INativeValueScanner>(),
            architecture,
            StandardMemoryValueTypes.Float32,
            ExactScanType,
            new[] { firstValue },
            scanOptions,
            CancellationToken.None).ConfigureAwait(false);

        MemoryScanExecutionResult nextScan = await scanner.NextScanNativeAsync(
            process,
            regions,
            firstScan.Results,
            session.GetRequiredService<INativeValueScanRefiner>(),
            architecture,
            StandardMemoryValueTypes.Float32,
            ExactScanType,
            new[] { nextValue },
            scanOptions,
            CancellationToken.None).ConfigureAwait(false);

        AssertEqual(1, nextScan.Results.Count, "Tolerant Float refinement did not remain on the native ps5debug-NG path.");
        AssertEqual(nextValue.DisplayText, nextScan.Results[0].CurrentValue.DisplayText, "Tolerant Float refinement returned the wrong current value.");
        AssertEqual(firstValue.DisplayText, nextScan.Results[0].PreviousValue?.DisplayText, "Tolerant Float refinement lost the previous value.");

        await session.GetRequiredService<INativeValueScanRefiner>()
            .ResetAsync(CancellationToken.None).ConfigureAwait(false);
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
            StandardMemoryValueTypeIds.Int32,
            StandardMemoryScanTypeIds.ExactValue,
            valueSize: sizeof(int),
            alignment: sizeof(int),
            inputValues: new[] { (ReadOnlyMemory<byte>)valueBytes });

        using CancellationTokenSource cancellation = new();
        Task<IReadOnlyList<NativeValueScanResult>> scanTask = session
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
        string testRoot = CreateTemporaryDirectory();
        try
        {
            string pluginDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins");
            AssertTrue(Directory.Exists(pluginDirectory), "Plugin deployment directory was not created for discovery verification.");
            AssertTrue(
                File.Exists(Path.Combine(pluginDirectory, "TeeKay87.MemoryEngine.Platform.PS5.deps.json")),
                "PS5 plugin dependency metadata was not deployed for isolated discovery.");
            AssertTrue(
                File.Exists(Path.Combine(pluginDirectory, "Iced.dll")),
                "PS5 plugin private Iced dependency was not deployed for isolated discovery.");

            JsonSettingsStore settingsStore = new(Path.Combine(testRoot, "settings.json"));
            IPluginSettings ps5Settings = settingsStore.CreatePluginSettings(Ps5PluginInfo.Id);
            AssertTrue(ps5Settings.TrySetString("connection.host", "10.0.0.55"), "Could not prepare the plugin-host settings fixture.");
            AssertTrue(ps5Settings.TrySetString("connection.port", "9021"), "Could not prepare the plugin-host port fixture.");

            using PluginHost host = new(settingsStore);
            PluginDiscoveryResult result = host.Discover(pluginDirectory);

            DiscoveredPlugin? mock = result.Plugins
                .SingleOrDefault(plugin => plugin.Instance.Metadata.Id == MockPluginInfo.Id);
            DiscoveredPlugin? ps5 = result.Plugins
                .SingleOrDefault(plugin => plugin.Instance.Metadata.Id == Ps5PluginInfo.Id);

            AssertTrue(mock is not null, "PluginHost did not discover the mock plugin assembly.");
            DiscoveredPlugin resolvedPs5 = ps5
                ?? throw new InvalidOperationException("PluginHost did not discover the PS5 plugin assembly.");
            AssertEqual(0, result.Errors.Count, "Plugin discovery reported unexpected errors.");
            AssertTrue(
                resolvedPs5.Instance.ConnectionSettings.Any(setting => setting.Key == "host" && setting.DefaultValue == "10.0.0.55"),
                "PluginHost did not attach the PS5 plugin's scoped settings before connection fields were read.");
            AssertTrue(
                resolvedPs5.Instance.ConnectionSettings.Any(setting => setting.Key == "port" && setting.DefaultValue == "9021"),
                "PluginHost did not attach the remembered PS5 port before connection fields were read.");
        }
        finally
        {
            TryDeleteDirectory(testRoot);
        }

        return Task.CompletedTask;
    }


    private static async Task<ScanResultRecordBatch> ReadFirstScanResultBatchAsync(IScanResultSet resultSet)
    {
        await foreach (ScanResultRecordBatch batch in resultSet
                           .ReadBatchesAsync(16, CancellationToken.None)
                           .ConfigureAwait(false))
        {
            return batch;
        }

        throw new InvalidOperationException("Expected at least one stored scan-result batch.");
    }

    private static MemoryScanValue ParseValue(
        IMemoryValueType valueType,
        string text,
        TargetArchitecture architecture)
    {
        if (!valueType.TryParse(text, architecture, out MemoryScanValue? value, out string error) || value is null)
        {
            throw new InvalidOperationException(
                $"Could not parse '{text}' as {valueType.DisplayName}: {error}");
        }

        return value;
    }

    private sealed class SyntheticResidentResultSet : INativeValueScanResidentResultSet
    {
        private readonly ulong _baseAddress;
        private readonly int _resultCount;
        private readonly byte _currentValue;
        private readonly byte _previousValue;
        private bool _disposed;

        public SyntheticResidentResultSet(
            ulong baseAddress,
            int resultCount,
            byte currentValue,
            byte previousValue)
        {
            _baseAddress = baseAddress;
            _resultCount = resultCount;
            _currentValue = currentValue;
            _previousValue = previousValue;
        }

        public long Count => _resultCount;

        public int ValueSize => 1;

        public int Alignment => 1;

        public bool IsAuthoritative => true;

        public long LastWindowMaximumRecords { get; private set; }

        public bool CanRefine(NativeValueScanRequest request)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return request.ValueSize == ValueSize && request.Alignment == Alignment;
        }

        public async IAsyncEnumerable<NativeValueScanResultBatch> ReadResultBatchesAsync(
            long startIndex,
            long maximumRecords,
            int maximumRecordsPerBatch,
            bool includePreviousValues,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (startIndex < 0 || startIndex > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }

            if (maximumRecords < 0 || maximumRecordsPerBatch <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRecords));
            }

            LastWindowMaximumRecords = maximumRecords;
            long remaining = Math.Min(maximumRecords, Count - startIndex);
            long produced = 0;
            while (produced < remaining)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = checked((int)Math.Min(maximumRecordsPerBatch, remaining - produced));
                ulong[] addresses = new ulong[count];
                byte[] currentValues = new byte[count];
                byte[] previousValues = includePreviousValues ? new byte[count] : Array.Empty<byte>();
                for (int index = 0; index < count; index++)
                {
                    addresses[index] = checked(_baseAddress + checked((ulong)(startIndex + produced + index)));
                    currentValues[index] = _currentValue;
                    if (includePreviousValues)
                    {
                        previousValues[index] = _previousValue;
                    }
                }

                produced += count;
                yield return includePreviousValues
                    ? new NativeValueScanResultBatch(
                        addresses,
                        currentValues,
                        previousValues,
                        valueSize: 1,
                        sourceRecordsProcessed: checked((ulong)produced))
                    : new NativeValueScanResultBatch(
                        addresses,
                        currentValues,
                        valueSize: 1,
                        sourceRecordsProcessed: checked((ulong)produced));
                await Task.Yield();
            }
        }

        public async IAsyncEnumerable<ReadOnlyMemory<ulong>> ReadAddressBatchesAsync(
            int maximumRecordsPerBatch,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (NativeValueScanResultBatch batch in ReadResultBatchesAsync(
                               0,
                               Count,
                               maximumRecordsPerBatch,
                               includePreviousValues: false,
                               cancellationToken: cancellationToken)
                               .ConfigureAwait(false))
            {
                yield return batch.Addresses;
            }
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SyntheticNativeValueScanResultStream : INativeValueScanResultStream
    {
        private readonly ulong _baseAddress;
        private readonly int _resultCount;
        private readonly int _batchSize;
        private readonly byte _value;
        private bool _enumerated;

        public SyntheticNativeValueScanResultStream(
            ulong baseAddress,
            int resultCount,
            int batchSize,
            byte value)
        {
            if (resultCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resultCount));
            }

            if (batchSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(batchSize));
            }

            _baseAddress = baseAddress;
            _resultCount = resultCount;
            _batchSize = batchSize;
            _value = value;
        }

        public ulong SourceResultCount => checked((ulong)_resultCount);

        public ulong LastProducedAddress { get; private set; }

        public async IAsyncEnumerable<NativeValueScanResultBatch> ReadBatchesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_enumerated)
            {
                throw new InvalidOperationException("Synthetic native result stream can only be consumed once.");
            }

            _enumerated = true;
            int produced = 0;
            while (produced < _resultCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(_batchSize, _resultCount - produced);
                ulong[] addresses = new ulong[count];
                byte[] values = new byte[count];
                for (int index = 0; index < count; index++)
                {
                    ulong address = checked(_baseAddress + checked((ulong)(produced + index)));
                    addresses[index] = address;
                    values[index] = _value;
                    LastProducedAddress = address;
                }

                produced += count;
                yield return new NativeValueScanResultBatch(
                    addresses,
                    values,
                    valueSize: 1,
                    sourceRecordsProcessed: checked((ulong)produced));
                await Task.Yield();
            }
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class OutOfOrderDisassemblerProvider : IDisassemblerProvider
    {
        public bool SupportsArchitecture(TargetArchitecture architecture)
        {
            ArgumentNullException.ThrowIfNull(architecture);
            return true;
        }

        public Task<IReadOnlyList<DisassembledInstruction>> DisassembleAsync(
            ulong startAddress,
            ReadOnlyMemory<byte> bytes,
            TargetArchitecture architecture,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(architecture);
            cancellationToken.ThrowIfCancellationRequested();

            if (bytes.Length < 2)
            {
                throw new ArgumentException("The ordering test requires at least two bytes.", nameof(bytes));
            }

            IReadOnlyList<DisassembledInstruction> instructions = new[]
            {
                new DisassembledInstruction(
                    checked(startAddress + 1UL),
                    bytes.Span.Slice(1, 1),
                    "second",
                    string.Empty),
                new DisassembledInstruction(
                    startAddress,
                    bytes.Span.Slice(0, 1),
                    "first",
                    string.Empty)
            };

            return Task.FromResult(instructions);
        }
    }

    private sealed class CancellingExportDataSource : IExportDataSource
    {
        private readonly CancellationTokenSource _cancellationSource;

        public CancellingExportDataSource(CancellationTokenSource cancellationSource)
        {
            _cancellationSource = cancellationSource ?? throw new ArgumentNullException(nameof(cancellationSource));
        }

        public string Type => "cancellation-test";

        public int SchemaVersion => 1;

        public long Count => 2;

        public IReadOnlyList<ExportColumn> Columns { get; } = new[]
        {
            new ExportColumn("value", "Value")
        };

        public IReadOnlyDictionary<string, ExportCellValue> Metadata { get; } =
            new Dictionary<string, ExportCellValue>();

        public async IAsyncEnumerable<ExportRowBatch> ReadBatchesAsync(
            IReadOnlyList<string> columnIds,
            int maximumRowsPerBatch,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new ExportRowBatch(
                rowCount: 1,
                columnCount: 1,
                new[] { ExportCellValue.FromString("first") });

            _cancellationSource.Cancel();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            yield return new ExportRowBatch(
                rowCount: 1,
                columnCount: 1,
                new[] { ExportCellValue.FromString("second") });
        }
    }

    private sealed class TestByteValueType : IMemoryValueType
    {
        public const string TypeId = "test.byte";

        public string Id => TypeId;

        public string DisplayName => "Test Byte";

        public string Category => "Test";

        public string Description => "Test-only plugin-defined byte value.";

        public string InputDescription => "Enter a decimal byte value.";

        public int? FixedSize => 1;

        public int DefaultAlignment => 1;

        public bool TryParse(
            string text,
            TargetArchitecture architecture,
            out MemoryScanValue? value,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(architecture);

            if (!byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte parsed))
            {
                value = null;
                error = "Enter a decimal value from 0 to 255.";
                return false;
            }

            value = new MemoryScanValue(
                Id,
                DisplayName,
                new[] { parsed },
                parsed.ToString(CultureInfo.InvariantCulture),
                DefaultAlignment);
            error = string.Empty;
            return true;
        }

        public bool TryResolveScanShape(
            IReadOnlyList<MemoryScanValue> inputValues,
            TargetArchitecture architecture,
            out int valueSize,
            out int alignment,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(inputValues);
            ArgumentNullException.ThrowIfNull(architecture);

            valueSize = 1;
            alignment = 1;
            error = string.Empty;
            return true;
        }

        public MemoryScanValue CreateValue(
            ReadOnlySpan<byte> bytes,
            int alignment,
            TargetArchitecture architecture)
        {
            ArgumentNullException.ThrowIfNull(architecture);

            if (bytes.Length != 1)
            {
                throw new ArgumentException("Test Byte requires exactly one byte.", nameof(bytes));
            }

            return new MemoryScanValue(
                Id,
                DisplayName,
                bytes,
                bytes[0].ToString(CultureInfo.InvariantCulture),
                alignment);
        }

        public bool ValuesEqual(
            ReadOnlySpan<byte> left,
            ReadOnlySpan<byte> right,
            TargetArchitecture architecture)
        {
            ArgumentNullException.ThrowIfNull(architecture);
            return left.SequenceEqual(right);
        }
    }

    private sealed class TestByteScanType : IMemoryScanType
    {
        public string Id => "test.equals";

        public string DisplayName => "Test Equals";

        public string Description => "Test-only plugin-defined exact comparison.";

        public bool AvailableForFirstScan => true;

        public bool AvailableForNextScan => true;

        public int? InputValueCount => 1;

        public bool SupportsValueType(IMemoryValueType valueType)
        {
            ArgumentNullException.ThrowIfNull(valueType);
            return string.Equals(valueType.Id, TestByteValueType.TypeId, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsMatch(
            IMemoryValueType valueType,
            ReadOnlySpan<byte> currentValue,
            MemoryScanValue? previousValue,
            IReadOnlyList<MemoryScanValue> inputValues,
            TargetArchitecture architecture,
            MemoryScanStage stage)
        {
            ArgumentNullException.ThrowIfNull(valueType);
            ArgumentNullException.ThrowIfNull(inputValues);
            ArgumentNullException.ThrowIfNull(architecture);

            return SupportsValueType(valueType) &&
                   inputValues.Count == 1 &&
                   currentValue.SequenceEqual(inputValues[0].Bytes.Span);
        }
    }

    private sealed class TestDebuggerProvider : IDebuggerProvider
    {
        public TestDebuggerProvider(TestDebuggerSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public TestDebuggerSession Session { get; set; }

        public int AttachCount { get; private set; }

        public Task<IDebuggerSession> AttachAsync(
            TargetProcess process,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(process);
            cancellationToken.ThrowIfCancellationRequested();
            AttachCount++;
            return Task.FromResult<IDebuggerSession>(Session);
        }
    }

    private sealed class TestDebuggerSession : IDebuggerSession, IDebuggerThreadService, IDebuggerThreadControlService
    {
        private DebuggerExecutionState _state;

        public TestDebuggerSession(TargetProcess process, DebuggerExecutionState initialState)
        {
            Process = process ?? throw new ArgumentNullException(nameof(process));
            _state = initialState;
        }

        public TargetProcess Process { get; }

        public DebuggerExecutionState State => _state;

        public int DetachCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public event EventHandler<DebuggerEventEventArgs>? EventReceived;

        public Task PauseAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();
            _state = DebuggerExecutionState.Paused;
            RaiseEvent(new DebuggerEvent(
                DebuggerEventKind.Paused,
                _state,
                DebuggerStopReason.PauseRequested,
                threadId: 1,
                instructionPointer: 0x1000));
            return Task.CompletedTask;
        }

        public Task ContinueAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();
            _state = DebuggerExecutionState.Running;
            RaiseEvent(new DebuggerEvent(
                DebuggerEventKind.Resumed,
                _state,
                DebuggerStopReason.None,
                threadId: 1,
                instructionPointer: 0x1000));
            return Task.CompletedTask;
        }

        public Task DetachAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();
            DetachCount++;
            _state = DebuggerExecutionState.Detached;
            return Task.CompletedTask;
        }

        public TService? GetService<TService>() where TService : class
        {
            EnsureNotDisposed();
            return this as TService;
        }

        public Task<IReadOnlyList<DebuggerThreadInfo>> GetThreadsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();
            IReadOnlyList<DebuggerThreadInfo> threads = new[]
            {
                new DebuggerThreadInfo(1, "Main", DebuggerThreadState.Stopped)
            };
            return Task.FromResult(threads);
        }

        public Task SuspendThreadAsync(ulong threadId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();
            return Task.CompletedTask;
        }

        public Task ResumeThreadAsync(ulong threadId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNotDisposed();
            return Task.CompletedTask;
        }

        public void RaiseEvent(DebuggerEvent debugEvent)
        {
            ArgumentNullException.ThrowIfNull(debugEvent);
            EventReceived?.Invoke(this, new DebuggerEventEventArgs(debugEvent));
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            _state = DebuggerExecutionState.Detached;
            return ValueTask.CompletedTask;
        }

        private void EnsureNotDisposed()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "TeeKay87.MemoryEngine.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private sealed class NoOpMemoryWriter : IMemoryWriter
    {
        public Task WriteAsync(
            TargetProcess process,
            ulong address,
            ReadOnlyMemory<byte> source,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(process);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPluginSettings : IPluginSettings
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public bool TryGetString(string key, out string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            if (_values.TryGetValue(key, out string? storedValue) && storedValue is not null)
            {
                value = storedValue;
                return true;
            }

            value = string.Empty;
            return false;
        }

        public bool TrySetString(string key, string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            _values[key] = value;
            return true;
        }

        public bool TryRemove(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            _values.Remove(key);
            return true;
        }
    }

    private sealed class RecordingApplicationLogger : IApplicationLogger
    {
        public List<string> Messages { get; } = new();

        public void Info(string message)
        {
            Messages.Add(message);
        }

        public void Warning(string message)
        {
            Messages.Add(message);
        }

        public void Error(string message, Exception? exception = null)
        {
            Messages.Add(exception is null ? message : $"{message}: {exception.Message}");
        }
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
