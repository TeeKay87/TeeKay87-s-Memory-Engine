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
using TeeKay87.MemoryEngine.Core.Debugging.Snapshots;
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
        ("Debugger triggered breakpoint event context", VerifyDebuggerTriggeredBreakpointEventAsync),
        ("Core watchpoint trigger resolution", VerifyWatchpointTriggerResolutionAsync),
        ("Debugger snapshot capture model", VerifyDebuggerSnapshotCaptureModelAsync),
        ("Debugger snapshot JSON round trip", VerifyDebuggerSnapshotJsonRoundTripAsync),
        ("Debugger snapshot JSON validation", VerifyDebuggerSnapshotJsonValidationAsync),
        ("Debugger snapshot export cancellation is transactional", VerifyDebuggerSnapshotExportCancellationAsync),
        ("Debugger snapshot live capture source contract", VerifyDebuggerSnapshotLiveCaptureSourceAsync),
        ("Debugger snapshot pairwise and group comparer", VerifyDebuggerSnapshotComparerAsync),
        ("Disassembler breakpoint/watchpoint context action source contract", VerifyDisassemblerBreakpointWatchpointActionSourceAsync),
        ("Call Stack Comparer workspace source contract", VerifyCallStackComparerWorkspaceSourceAsync),
        ("Debugger list export source contract", VerifyDebuggerListExportSourceAsync),
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
        ("Mock debugger deterministic call stack", VerifyMockDebuggerCallStackServicesAsync),
        ("Mock debugger native Step Into", VerifyMockDebuggerStepServicesAsync),
        ("Mock debugger software breakpoint lifecycle and hits", VerifyMockDebuggerBreakpointServicesAsync),
        ("Mock debugger hardware watchpoint lifecycle and hits", VerifyMockDebuggerWatchpointServicesAsync),
        ("Debugger breakpoint request validation service", VerifyDebuggerBreakpointValidationServiceAsync),
        ("Mock debugger attachment exclusivity and target cleanup", VerifyMockDebuggerAttachmentIsolationAsync),
        ("Debugger workspace command and event source contract", VerifyDebuggerWorkspaceSourceAsync),
        ("Debugger workspace thread panel source contract", VerifyDebuggerThreadWorkspaceSourceAsync),
        ("Debugger workspace register panel source contract", VerifyDebuggerRegisterWorkspaceSourceAsync),
        ("Debugger thread/register pane splitter source contract", VerifyDebuggerPaneSplitterSourceAsync),
        ("Debugger register value codec source contract", VerifyDebuggerRegisterValueCodecSourceAsync),
        ("Debugger register edit dialog source contract", VerifyRegisterEditDialogSourceAsync),
        ("Debugger current instruction Disassembler integration", VerifyDebuggerCurrentInstructionIntegrationSourceAsync),
        ("Debugger breakpoint manager source contract", VerifyDebuggerBreakpointManagerSourceAsync),
        ("Debugger breakpoint dialog source contract", VerifyBreakpointDialogSourceAsync),
        ("Debugger hardware watchpoint manager source contract", VerifyDebuggerWatchpointManagerSourceAsync),
        ("Debugger breakpoint classification source contract", VerifyDebuggerBreakpointClassificationSourceAsync),
        ("Main workspace debugger address actions source contract", VerifyMainWorkspaceDebuggerAddressActionsSourceAsync),
        ("Debugger call-stack and stepping workspace source contract", VerifyDebuggerCallStackAndSteppingSourceAsync),
        ("Debugger breakpoint-aware Step Over source contract", VerifyDebuggerBreakpointAwareStepOverSourceAsync),
        ("Debugger workspace mode switcher source contract", VerifyDebuggerWorkspaceModeSwitcherSourceAsync),
        ("Debugger workspace switch-button theme source contract", VerifyDebuggerWorkspaceSwitchButtonThemeSourceAsync),
        ("Debugger workspace panel layout source contract", VerifyDebuggerWorkspacePanelLayoutSourceAsync),
        ("Modeless tool-window independent z-order source contract", VerifyToolWindowIndependentZOrderSourceAsync),
        ("Main-window tool cleanup source contract", VerifyMainWindowToolCleanupSourceAsync),
        ("Main-window shutdown re-entry guard source contract", VerifyMainWindowShutdownReentrySourceAsync),
        ("Debugger Run to Address source contract", VerifyDebuggerRunToAddressSourceAsync),
        ("Debugger interrupted composed-operation cleanup source contract", VerifyDebuggerInterruptedComposedOperationCleanupSourceAsync),
        ("Debugger target lifetime and stale-session source contract", VerifyDebuggerTargetLifetimeSourceAsync),
        ("Mock target process and memory map", VerifyMockTargetProcessAndMemoryMapAsync),
        ("Mock target memory read and write", VerifyMockTargetMemoryReadWriteAsync),
        ("Disassembly instruction model", VerifyDisassemblyInstructionModelAsync),
        ("Disassembly syntax token model", VerifyDisassemblySyntaxTokenModelAsync),
        ("Mock disassembly capability and provider", VerifyMockDisassemblyProviderAsync),
        ("Core disassembly bounded read", VerifyDisassemblyBoundedReadAsync),
        ("Core disassembly debugger-byte overlay", VerifyDisassemblyDebuggerByteOverlayAsync),
        ("Core debugger disassembly overlay lifecycle", VerifyDebuggerDisassemblyOverlayLifecycleAsync),
        ("Core disassembly bidirectional context", VerifyDisassemblyBidirectionalContextAsync),
        ("Core disassembly continuous origin resolution", VerifyDisassemblyContinuousOriginResolutionAsync),
        ("Core disassembly region-boundary handling", VerifyDisassemblyRegionBoundaryAsync),
        ("Core disassembly unreadable-region rejection", VerifyDisassemblyUnreadableRegionRejectionAsync),
        ("Core disassembly instruction-order validation", VerifyDisassemblyInstructionOrderValidationAsync),
        ("Core disassembly cancellation", VerifyDisassemblyCancellationAsync),
        ("Disassembly session target identity", VerifyDisassemblySessionIdentityAsync),
        ("Disassembler direct target navigation contract", VerifyDisassemblerFollowTargetSourceAsync),
        ("Disassembler debugger markers and logical instruction source contract", VerifyDisassemblerDebuggerMarkersSourceAsync),
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
        ("PS5 debugger server-side call-stack protocol", VerifyPs5DebuggerCallStackServicesAsync),
        ("PS5 debugger native Step Into protocol and completion", VerifyPs5DebuggerStepServicesAsync),
        ("PS5 debugger extended register capability gating", VerifyPs5DebuggerExtendedRegisterCapabilityGatingAsync),
        ("PS5 debugger optional register timeout isolation", VerifyPs5DebuggerOptionalRegisterTimeoutIsolationAsync),
        ("PS5 debugger software breakpoint protocol and hit mapping", VerifyPs5DebuggerBreakpointServicesAsync),
        ("PS5 debugger logical software-breakpoint stop context", VerifyPs5DebuggerLogicalSoftwareBreakpointStopContextAsync),
        ("PS5 debugger safe detach restores software breakpoints", VerifyPs5DebuggerSafeDetachBreakpointCleanupAsync),
        ("PS5 debugger safe detach clears hardware watchpoints", VerifyPs5DebuggerSafeDetachWatchpointCleanupAsync),
        ("PS5 debugger hardware watchpoint protocol and hit mapping", VerifyPs5DebuggerWatchpointServicesAsync),
        ("Main workspace target controls and connection status layout", VerifyMainWorkspaceTargetLayoutAsync),
        ("Main status bar content alignment source contract", VerifyMainStatusBarContentAlignmentAsync),
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

        AssertEqual(new Version(2, 18, 0), PluginApiInfo.CurrentVersion, "Unexpected Plugin API version.");
        AssertTrue(PluginApiInfo.IsCompatible(new Version(2, 12, 0)), "Plugin API 2.18 host rejected an older compatible 2.12 plugin contract.");
        AssertTrue(PluginApiInfo.IsCompatible(mock.Metadata.ApiVersion), "Mock plugin API version is incompatible.");
        AssertTrue(PluginApiInfo.IsCompatible(ps5.Metadata.ApiVersion), "PS5 plugin API version is incompatible.");
        AssertEqual("1.0.1.rev17", mock.Metadata.DisplayVersion, "Unexpected mock plugin display version.");
        AssertEqual(new Version(2, 18, 0), mock.Metadata.ApiVersion, "Unexpected Mock plugin API version.");
        AssertEqual("0.1.2.rev39", ps5.Metadata.DisplayVersion, "Unexpected PS5 plugin display version.");
        AssertEqual(new Version(2, 18, 0), ps5.Metadata.ApiVersion, "Unexpected PS5 plugin API version.");

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
            "Plugin API 2.15 removed the existing seven-parameter DebuggerRegister constructor required by older compiled plugins.");

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

    private static Task VerifyDebuggerTriggeredBreakpointEventAsync()
    {
        DebuggerBreakpointRequest request = new(
            0x2000,
            size: 4,
            kind: DebuggerBreakpointKind.Hardware,
            access: DebuggerBreakpointAccess.Write);
        DebuggerBreakpoint watchpoint = new("wp-1", request);
        DebuggerEvent watchpointEvent = new(
            DebuggerEventKind.Watchpoint,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.Watchpoint,
            threadId: 3,
            instructionPointer: 0x4000,
            triggeredBreakpoint: watchpoint,
            message: "Watchpoint hit.");

        AssertTrue(ReferenceEquals(watchpoint, watchpointEvent.TriggeredBreakpoint),
            "Debugger watchpoint event did not retain the triggered neutral breakpoint snapshot.");
        AssertEqual<ulong?>(0x4000, watchpointEvent.InstructionPointer,
            "Debugger watchpoint event replaced the accessing instruction pointer with the watched data address.");
        AssertEqual(0x2000UL, watchpointEvent.TriggeredBreakpoint!.Request.Address,
            "Debugger watchpoint event lost the watched data address.");

        DebuggerEvent legacyEvent = new(
            DebuggerEventKind.Breakpoint,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.Breakpoint,
            threadId: 3,
            instructionPointer: 0x4010,
            message: "Legacy constructor path.");
        AssertTrue(legacyEvent.TriggeredBreakpoint is null,
            "Existing DebuggerEvent construction unexpectedly created triggered-breakpoint context.");
        AssertTrue(
            typeof(DebuggerEvent).GetConstructor(new[]
            {
                typeof(DebuggerEventKind),
                typeof(DebuggerExecutionState),
                typeof(DebuggerStopReason),
                typeof(ulong?),
                typeof(ulong?),
                typeof(string),
                typeof(DateTimeOffset?)
            }) is not null,
            "Plugin API 2.16 removed the pre-existing DebuggerEvent constructor required by older compiled plugins.");

        DebuggerBreakpointValidationResult valid = DebuggerBreakpointValidationResult.Valid();
        DebuggerBreakpointValidationResult invalid = DebuggerBreakpointValidationResult.Invalid("blocked");
        AssertTrue(valid.IsValid && string.IsNullOrEmpty(valid.Message),
            "Debugger breakpoint validation success result is malformed.");
        AssertFalse(invalid.IsValid, "Debugger breakpoint validation failure result is malformed.");
        AssertEqual("blocked", invalid.Message, "Debugger breakpoint validation failure message changed unexpectedly.");

        return Task.CompletedTask;
    }

    private static Task VerifyWatchpointTriggerResolutionAsync()
    {
        DebuggerBreakpoint watchpoint = new(
            "wp-resolve",
            new DebuggerBreakpointRequest(
                0x9000,
                4,
                DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.Write));
        DebuggerEvent sourceEvent = new(
            DebuggerEventKind.Watchpoint,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.Watchpoint,
            threadId: 3,
            instructionPointer: 0x4005,
            triggeredBreakpoint: watchpoint);

        DisassembledInstruction trigger = new(
            0x4002,
            new byte[] { 0x01, 0x70, 0x40 },
            "add",
            "[rax+40h],esi");
        DisassembledInstruction stop = new(
            0x4005,
            new byte[] { 0x90 },
            "nop",
            string.Empty);
        DisassemblySnapshot snapshot = new(
            requestedAddress: 0x4005,
            startAddress: 0x4002,
            bytes: new byte[] { 0x01, 0x70, 0x40, 0x90 },
            region: new MemoryRegion(0x4000, 0x100, MemoryProtection.Read | MemoryProtection.Execute),
            architecture: new TargetArchitecture(CpuArchitecture.X64, 64, 64, Endianness.Little),
            instructions: new[] { trigger, stop });

        DebuggerEvent resolved = new DebuggerWatchpointTriggerResolver().Resolve(sourceEvent, snapshot);
        AssertEqual<ulong?>(0x4005, resolved.InstructionPointer,
            "Watchpoint trigger resolution changed the authoritative stop/current instruction pointer.");
        AssertEqual<ulong?>(0x4002, resolved.TriggerInstructionAddress,
            "Watchpoint trigger resolution did not identify the logical instruction ending at the stop RIP.");
        AssertEqual(DebuggerTriggerResolution.DisassemblyDerived, resolved.TriggerResolution,
            "Watchpoint trigger resolution did not report its derivation source.");
        AssertEqual<ulong?>(0x9000, resolved.WatchedAddress,
            "Watchpoint trigger resolution lost the watched memory address.");
        AssertEqual<DebuggerBreakpointAccess?>(DebuggerBreakpointAccess.Write, resolved.WatchpointAccess,
            "Watchpoint trigger resolution lost the watchpoint access mode.");
        AssertEqual<int?>(4, resolved.WatchpointSize,
            "Watchpoint trigger resolution lost the watchpoint size.");

        DebuggerEvent exact = sourceEvent.WithTriggerInstruction(0x4001, DebuggerTriggerResolution.BackendExact);
        DebuggerEvent preserved = new DebuggerWatchpointTriggerResolver().Resolve(exact, snapshot);
        AssertTrue(ReferenceEquals(exact, preserved),
            "Core replaced a backend-exact trigger instruction with a disassembly-derived result.");

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
        TargetCapabilities rev17Capabilities =
            TargetCapabilities.Debugger |
            TargetCapabilities.Breakpoints |
            TargetCapabilities.Watchpoints |
            TargetCapabilities.ThreadEnumeration |
            TargetCapabilities.ThreadControl |
            TargetCapabilities.RegisterAccess |
            TargetCapabilities.CallStack |
            TargetCapabilities.StepExecution;
        AssertEqual(
            rev17Capabilities,
            mock.Capabilities & debuggerCapabilities,
            "Mock plugin did not advertise exactly the debugger capabilities implemented through rev17.");
        AssertEqual(
            rev17Capabilities,
            ps5.Capabilities & debuggerCapabilities,
            "PS5 plugin did not advertise exactly the debugger capabilities implemented through rev17.");

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
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.Breakpoints), "Mock breakpoint capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.Watchpoints), "Mock watchpoint capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.CallStack), "Mock call-stack capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.StepExecution), "Mock step-execution capability is missing.");

        return Task.CompletedTask;
    }

    private static async Task VerifyMockDebuggerProviderAsync()
    {
        MockTargetPlugin plugin = new();
        AssertEqual("1.0.1.rev17", plugin.Metadata.DisplayVersion, "Unexpected Mock debugger plugin revision.");
        AssertEqual(new Version(2, 18, 0), plugin.Metadata.ApiVersion, "Mock debugger backend must target Plugin API 2.18.0.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.Debugger), "Mock debugger capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.ThreadEnumeration), "Mock thread-enumeration capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.ThreadControl), "Mock thread-control capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.RegisterAccess), "Mock rev7 register-access capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.Breakpoints), "Mock breakpoint capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.Watchpoints), "Mock watchpoint capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.CallStack), "Mock call-stack capability is missing.");
        AssertTrue(plugin.Capabilities.HasFlag(TargetCapabilities.StepExecution), "Mock step-execution capability is missing.");

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
        AssertTrue(debugger.GetService<IDebuggerRegisterService>() is not null, "Mock did not expose its register service.");
        AssertTrue(debugger.GetService<IDebuggerBreakpointService>() is not null, "Mock did not expose its breakpoint service.");
        AssertTrue(debugger.GetService<IDebuggerBreakpointStateService>() is not null, "Mock did not expose its breakpoint state service.");
        AssertTrue(debugger.GetService<IDebuggerCallStackService>() is not null, "Mock did not expose its call-stack service.");
        AssertTrue(debugger.GetService<IDebuggerStepService>() is not null, "Mock did not expose its step service.");

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
        AssertEqual(22, registers.Count,
            "Mock register snapshot did not include the expected extended register fixtures.");
        AssertTrue(registers.Where(item => item.Group is "General" or "Control").All(item => item.CanWrite),
            "Mock general/control register rows must remain writable for deterministic write-path testing.");

        DebuggerRegister floatingPoint = registers.Single(item => item.Id == "fp0");
        DebuggerRegister vector128 = registers.Single(item => item.Id == "vector0");
        DebuggerRegister vector256 = registers.Single(item => item.Id == "vector1");
        DebuggerRegister debug = registers.Single(item => item.Id == "debug0");
        AssertEqual(80, floatingPoint.BitWidth, "Mock floating-point fixture has the wrong register width.");
        AssertEqual("Floating Point", floatingPoint.Group, "Mock floating-point fixture has the wrong neutral group.");
        AssertEqual(128, vector128.BitWidth, "Mock 128-bit SIMD fixture has the wrong register width.");
        AssertEqual(256, vector256.BitWidth, "Mock 256-bit SIMD fixture has the wrong register width.");
        AssertEqual("SIMD", vector256.Group, "Mock wide-vector fixture has the wrong neutral group.");
        AssertEqual("Debug", debug.Group, "Mock debug-register fixture has the wrong neutral group.");
        AssertTrue(new[] { floatingPoint, vector128, vector256, debug }.All(item => !item.CanWrite),
            "Mock extended register fixtures should remain read-only while general-register write testing stays unchanged.");
        AssertTrue(new[] { floatingPoint, vector128, vector256, debug }.All(
                item => item.ValueEncoding == DebuggerRegisterValueEncoding.UnsignedLittleEndian),
            "Mock extended register fixtures did not retain neutral little-endian unsigned encoding.");

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

    private static async Task VerifyMockDebuggerCallStackServicesAsync()
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

        IDebuggerCallStackService callStack = debugger.GetRequiredService<IDebuggerCallStackService>();
        await AssertThrowsAsync<InvalidOperationException>(
            () => callStack.GetCallStackAsync(1, CancellationToken.None),
            "Mock debugger exposed a call stack while the target was running.").ConfigureAwait(false);

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        IReadOnlyList<DebuggerStackFrame> frames = await callStack
            .GetCallStackAsync(1, CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(3, frames.Count, "Mock debugger did not return its deterministic three-frame call stack.");
        AssertEqual(0, frames[0].Index, "Mock top call frame has the wrong index.");
        AssertEqual(MockTargetLayout.CodeAddress, frames[0].InstructionAddress,
            "Mock top call frame does not match the paused instruction pointer.");
        AssertEqual<ulong?>(MockTargetLayout.CodeAddress + 0x4, frames[0].ReturnAddress,
            "Mock top call frame lost its deterministic return address.");
        AssertEqual(process.Name, frames[0].ModuleName,
            "Mock top call frame lost its deterministic module name.");
        AssertEqual("Main.Current", frames[0].SymbolName,
            "Mock top call frame lost its deterministic symbol name.");
        AssertEqual(MockTargetLayout.CodeAddress + 0x4, frames[1].InstructionAddress,
            "Mock caller frame has the wrong instruction address.");
        AssertEqual("Main.Caller", frames[1].SymbolName,
            "Mock caller frame lost its deterministic symbol name.");
        AssertEqual<ulong?>(null, frames[2].ReturnAddress,
            "Mock root call frame should not advertise a return address.");
        AssertEqual("MockEntry", frames[2].SymbolName,
            "Mock root call frame lost its deterministic symbol name.");

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        await AssertThrowsAsync<InvalidOperationException>(
            () => callStack.GetCallStackAsync(1, CancellationToken.None),
            "Mock debugger retained usable call frames after Continue.").ConfigureAwait(false);
    }

    private static async Task VerifyMockDebuggerStepServicesAsync()
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

        IDebuggerStepService stepping = debugger.GetRequiredService<IDebuggerStepService>();
        IDebuggerRegisterService registers = debugger.GetRequiredService<IDebuggerRegisterService>();
        await AssertThrowsAsync<InvalidOperationException>(
            () => stepping.StepAsync(DebuggerStepKind.Into, 1, CancellationToken.None),
            "Mock debugger accepted Step Into while the target was running.").ConfigureAwait(false);

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        TaskCompletionSource<DebuggerEvent> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.StepCompleted)
            {
                completed.TrySetResult(args.Event);
            }
        };

        await stepping.StepAsync(DebuggerStepKind.Into, 1, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Running, debugger.State,
            "Mock Step Into did not immediately transition the target to Running.");

        DebuggerEvent stepEvent = await completed.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        AssertEqual(DebuggerEventKind.StepCompleted, stepEvent.Kind,
            "Mock native stepping did not produce StepCompleted.");
        AssertEqual(DebuggerStopReason.StepCompleted, stepEvent.StopReason,
            "Mock native stepping did not preserve the StepCompleted stop reason.");
        AssertEqual<ulong?>(1, stepEvent.ThreadId,
            "Mock native stepping completed on the wrong thread.");
        AssertEqual<ulong?>(MockTargetLayout.CodeAddress + 0x2, stepEvent.InstructionPointer,
            "Mock native stepping stopped at the wrong deterministic instruction.");
        AssertEqual(DebuggerExecutionState.Paused, debugger.State,
            "Mock native stepping did not return the debugger to Paused.");

        IReadOnlyList<DebuggerRegister> snapshot = await registers
            .GetRegistersAsync(1, CancellationToken.None)
            .ConfigureAwait(false);
        DebuggerRegister ip = snapshot.Single(item => item.Role == DebuggerRegisterRole.InstructionPointer);
        AssertEqual(MockTargetLayout.CodeAddress + 0x2, BinaryPrimitives.ReadUInt64LittleEndian(ip.Value.Span),
            "Mock Step Into did not update the selected thread's instruction pointer.");

        await AssertThrowsAsync<NotSupportedException>(
            () => stepping.StepAsync(DebuggerStepKind.Over, 1, CancellationToken.None),
            "Mock backend unexpectedly implemented native Step Over instead of leaving composition to the host.").ConfigureAwait(false);
        await AssertThrowsAsync<NotSupportedException>(
            () => stepping.StepAsync(DebuggerStepKind.Out, 1, CancellationToken.None),
            "Mock backend unexpectedly implemented native Step Out instead of leaving composition to the host.").ConfigureAwait(false);
    }

    private static async Task VerifyMockDebuggerBreakpointServicesAsync()
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

        IDebuggerBreakpointService breakpoints = debugger.GetRequiredService<IDebuggerBreakpointService>();
        IDebuggerBreakpointStateService states = debugger.GetRequiredService<IDebuggerBreakpointStateService>();

        await AssertThrowsAsync<InvalidOperationException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(
                    MockTargetLayout.AmmoAddress,
                    1,
                    DebuggerBreakpointKind.Software,
                    DebuggerBreakpointAccess.Execute),
                CancellationToken.None),
            "Mock debugger accepted a software execute breakpoint in the mapped data area.").ConfigureAwait(false);
        await AssertThrowsAsync<InvalidOperationException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(
                    0x80000104,
                    1,
                    DebuggerBreakpointKind.Software,
                    DebuggerBreakpointAccess.Execute),
                CancellationToken.None),
            "Mock debugger accepted a software execute breakpoint outside the target memory map.").ConfigureAwait(false);

        DebuggerBreakpoint persistent = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                MockTargetLayout.CodeAddress + 8,
                1,
                DebuggerBreakpointKind.Software,
                DebuggerBreakpointAccess.Execute),
            CancellationToken.None).ConfigureAwait(false);
        AssertTrue(persistent.IsEnabled, "Mock software breakpoint did not begin enabled.");

        await states.SetBreakpointEnabledAsync(persistent.Id, false, CancellationToken.None).ConfigureAwait(false);
        AssertFalse((await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false)).Single().IsEnabled,
            "Mock breakpoint disable state was not retained.");
        await states.SetBreakpointEnabledAsync(persistent.Id, true, CancellationToken.None).ConfigureAwait(false);

        TaskCompletionSource<DebuggerEvent> firstHit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.Breakpoint)
            {
                firstHit.TrySetResult(args.Event);
            }
        };

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        DebuggerEvent hit = await firstHit.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        AssertEqual(DebuggerStopReason.Breakpoint, hit.StopReason, "Mock breakpoint hit lost its stop reason.");
        AssertEqual<ulong?>(persistent.Request.Address, hit.InstructionPointer, "Mock breakpoint hit reported the wrong instruction pointer.");
        AssertEqual(DebuggerExecutionState.Paused, debugger.State, "Mock breakpoint hit did not pause the debugger session.");
        AssertEqual(1, (await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false)).Count,
            "Persistent mock breakpoint was removed after a hit.");

        await breakpoints.RemoveBreakpointAsync(persistent.Id, CancellationToken.None).ConfigureAwait(false);
        DebuggerBreakpoint temporary = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                MockTargetLayout.CodeAddress + 4,
                1,
                DebuggerBreakpointKind.Software,
                DebuggerBreakpointAccess.Execute,
                isTemporary: true),
            CancellationToken.None).ConfigureAwait(false);
        TaskCompletionSource<DebuggerEvent> temporaryHit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.Breakpoint &&
                args.Event.InstructionPointer == temporary.Request.Address)
            {
                temporaryHit.TrySetResult(args.Event);
            }
        };

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        _ = await temporaryHit.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        AssertEqual(0, (await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false)).Count,
            "Temporary mock breakpoint remained after its first hit.");
    }

    private static async Task VerifyDebuggerBreakpointValidationServiceAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession targetSession = await plugin
            .ConnectAsync(new TargetConnectionOptions(new Dictionary<string, string>()), CancellationToken.None)
            .ConfigureAwait(false);
        IDebuggerProvider provider = targetSession.GetRequiredService<IDebuggerProvider>();
        TargetProcess process = (await targetSession.GetRequiredService<IProcessProvider>()
                .GetProcessesAsync(CancellationToken.None)
                .ConfigureAwait(false))
            .Single();
        await using IDebuggerSession debugger = await provider
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        IDebuggerBreakpointValidationService validation =
            debugger.GetRequiredService<IDebuggerBreakpointValidationService>();
        IDebuggerBreakpointService breakpoints = debugger.GetRequiredService<IDebuggerBreakpointService>();

        DebuggerBreakpointRequest codeBreakpoint = new(
            MockTargetLayout.CodeAddress,
            1,
            DebuggerBreakpointKind.Software,
            DebuggerBreakpointAccess.Execute);
        AssertTrue(validation.ValidateBreakpointRequest(codeBreakpoint).IsValid,
            "Mock validator rejected a legal software execute breakpoint.");

        DebuggerBreakpointRequest dataBreakpoint = new(
            MockTargetLayout.HealthAddress,
            1,
            DebuggerBreakpointKind.Software,
            DebuggerBreakpointAccess.Execute);
        AssertFalse(validation.ValidateBreakpointRequest(dataBreakpoint).IsValid,
            "Mock validator accepted a software breakpoint outside executable code.");

        DebuggerBreakpointRequest writeWatchpoint = new(
            MockTargetLayout.HealthAddress,
            4,
            DebuggerBreakpointKind.Hardware,
            DebuggerBreakpointAccess.Write);
        AssertTrue(validation.ValidateBreakpointRequest(writeWatchpoint).IsValid,
            "Mock validator rejected a legal aligned write watchpoint.");
        AssertFalse(validation.ValidateBreakpointRequest(new DebuggerBreakpointRequest(
                MockTargetLayout.HealthAddress + 1,
                4,
                DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.Write)).IsValid,
            "Mock validator accepted a misaligned hardware watchpoint.");

        DebuggerBreakpoint added = await breakpoints
            .AddBreakpointAsync(codeBreakpoint, CancellationToken.None)
            .ConfigureAwait(false);
        AssertFalse(validation.ValidateBreakpointRequest(codeBreakpoint).IsValid,
            "Mock validator accepted a duplicate software breakpoint.");
        await breakpoints.RemoveBreakpointAsync(added.Id, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task VerifyMockDebuggerWatchpointServicesAsync()
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
        IDebuggerBreakpointService breakpoints = debugger.GetRequiredService<IDebuggerBreakpointService>();
        IDebuggerBreakpointStateService states = debugger.GetRequiredService<IDebuggerBreakpointStateService>();

        await AssertThrowsAsync<NotSupportedException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(
                    MockTargetLayout.HealthAddress,
                    4,
                    DebuggerBreakpointKind.Hardware,
                    DebuggerBreakpointAccess.Execute),
                CancellationToken.None),
            "Mock accepted an execute request through the hardware-watchpoint path.").ConfigureAwait(false);
        await AssertThrowsAsync<InvalidOperationException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(
                    MockTargetLayout.HealthAddress + 2,
                    4,
                    DebuggerBreakpointKind.Hardware,
                    DebuggerBreakpointAccess.Write),
                CancellationToken.None),
            "Mock accepted a misaligned four-byte hardware watchpoint.").ConfigureAwait(false);
        await AssertThrowsAsync<InvalidOperationException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(
                    MockTargetLayout.BaseAddress + (ulong)MockTargetLayout.MemorySize,
                    1,
                    DebuggerBreakpointKind.Hardware,
                    DebuggerBreakpointAccess.Read),
                CancellationToken.None),
            "Mock accepted a hardware watchpoint outside the target map.").ConfigureAwait(false);

        DebuggerBreakpoint persistent = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                MockTargetLayout.HealthAddress,
                4,
                DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.Write),
            CancellationToken.None).ConfigureAwait(false);
        AssertTrue(persistent.IsEnabled, "Mock hardware watchpoint did not begin enabled.");

        await states.SetBreakpointEnabledAsync(persistent.Id, false, CancellationToken.None).ConfigureAwait(false);
        AssertFalse((await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false)).Single().IsEnabled,
            "Mock hardware watchpoint disable state was not retained.");
        await states.SetBreakpointEnabledAsync(persistent.Id, true, CancellationToken.None).ConfigureAwait(false);

        TaskCompletionSource<DebuggerEvent> firstHit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.Watchpoint)
            {
                firstHit.TrySetResult(args.Event);
            }
        };

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        DebuggerEvent hit = await firstHit.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        AssertEqual(DebuggerStopReason.Watchpoint, hit.StopReason, "Mock hardware watchpoint hit lost its stop reason.");
        AssertEqual<ulong?>(MockTargetLayout.CodeAddress + 6, hit.InstructionPointer,
            "Mock hardware watchpoint did not preserve the post-access stop instruction pointer.");
        AssertEqual<ulong?>(MockTargetLayout.CodeAddress + 4, hit.TriggerInstructionAddress,
            "Mock hardware watchpoint did not identify its deterministic trigger instruction separately.");
        AssertEqual(DebuggerTriggerResolution.BackendExact, hit.TriggerResolution,
            "Mock hardware watchpoint did not mark its deterministic trigger as backend-exact.");
        AssertEqual(persistent.Id, hit.TriggeredBreakpoint?.Id,
            "Mock hardware watchpoint event did not identify the triggered watchpoint.");
        AssertEqual(MockTargetLayout.HealthAddress, hit.TriggeredBreakpoint!.Request.Address,
            "Mock hardware watchpoint event lost the watched data address.");
        AssertEqual(1, (await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false)).Count,
            "Persistent mock hardware watchpoint was removed after its first hit.");

        await breakpoints.RemoveBreakpointAsync(persistent.Id, CancellationToken.None).ConfigureAwait(false);
        DebuggerBreakpoint temporary = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                MockTargetLayout.AmmoAddress,
                4,
                DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.ReadWrite,
                isTemporary: true),
            CancellationToken.None).ConfigureAwait(false);
        TaskCompletionSource<DebuggerEvent> temporaryHit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.Watchpoint &&
                args.Event.TriggeredBreakpoint?.Id == temporary.Id)
            {
                temporaryHit.TrySetResult(args.Event);
            }
        };

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        _ = await temporaryHit.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        AssertEqual(0, (await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false)).Count,
            "Temporary mock hardware watchpoint remained after its first hit.");
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
            mainWindowSource.Contains("long connectionGeneration = plugin.ConnectionGeneration;", StringComparison.Ordinal) &&
            Regex.IsMatch(
                mainWindowSource,
                @"DebuggerViewModel\s+\w+\s*=\s*new(?:\s+DebuggerViewModel)?\s*\(\s*plugin\s*,\s*targetProcess\s*,\s*connectionGeneration\s*\)",
                RegexOptions.CultureInvariant),
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

    private static Task VerifyDebuggerPaneSplitterSourceAsync()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerWindow.xaml"));

        AssertTrue(
            xaml.Contains("controls:ProportionalGridSplitter", StringComparison.Ordinal) &&
            xaml.Contains("Style=\"{StaticResource RowWorkspaceSplitterStyle}\"", StringComparison.Ordinal) &&
            xaml.Contains("ResizeDirection=\"Rows\"", StringComparison.Ordinal) &&
            xaml.Contains("ResizeBehavior=\"PreviousAndNext\"", StringComparison.Ordinal),
            "Debugger Threads/Registers layout does not reuse the shared proportional row splitter.");
        AssertTrue(
            xaml.Contains("MinimumPreviousRatio=\"0.2\"", StringComparison.Ordinal) &&
            xaml.Contains("MaximumPreviousRatio=\"0.8\"", StringComparison.Ordinal),
            "Debugger Threads/Registers splitter does not retain relative 20/80 movement limits.");
        AssertTrue(
            xaml.Contains("<Setter Property=\"Height\" Value=\"*\" />", StringComparison.Ordinal) &&
            xaml.Contains("<RowDefinition Height=\"*\" MinHeight=\"120\" />", StringComparison.Ordinal),
            "Debugger Threads/Registers panes are not configured to share the available height from an equal star-sized baseline.");
        AssertTrue(
            xaml.Contains("<DataTrigger Binding=\"{Binding HasRegisterAccessCapability}\" Value=\"False\">", StringComparison.Ordinal) &&
            xaml.Contains("<Setter Property=\"Height\" Value=\"0\" />", StringComparison.Ordinal),
            "Debugger register row does not collapse when the backend lacks register access.");
        AssertFalse(
            xaml.Contains("Height=\"210\"", StringComparison.Ordinal),
            "Debugger Registers pane still uses the former fixed 210-unit height.");

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

    private static Task VerifyDebuggerBreakpointManagerSourceAsync()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string viewModel = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerViewModel.cs"));
        string codeBehind = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerWindow.xaml.cs"));

        AssertContains(xaml, "Content=\"Breakpoints / Watchpoints\"", "Debugger workspace is missing the Breakpoints / Watchpoints workspace selector.");
        AssertContains(xaml, "ItemsSource=\"{Binding Breakpoints}\"", "Breakpoint manager does not bind its neutral collection.");
        AssertContains(xaml, "Command=\"{Binding EnableBreakpointCommand}\"", "Breakpoint manager is missing Enable.");
        AssertContains(xaml, "Command=\"{Binding DisableBreakpointCommand}\"", "Breakpoint manager is missing Disable.");
        AssertContains(xaml, "Command=\"{Binding RemoveBreakpointCommand}\"", "Breakpoint manager is missing Remove.");
        AssertContains(xaml, "RemoveAllBreakpointsButton_Click", "Breakpoint manager is missing destructive Remove All handling.");
        AssertContains(xaml, "HasBreakpointManagementCapability", "Breakpoint/watchpoint manager is not capability gated.");
        AssertContains(xaml, "<Setter Property=\"Height\" Value=\"13*\" />", "Breakpoint manager does not start at the requested upper-pane height ratio.");
        AssertContains(xaml, "<RowDefinition Height=\"7*\" MinHeight=\"150\" />", "Debugger events pane does not start at the requested lower-pane height ratio.");
        AssertContains(viewModel, "IDebuggerBreakpointService", "Debugger ViewModel does not use the neutral breakpoint service.");
        AssertContains(viewModel, "IDebuggerBreakpointStateService", "Debugger ViewModel does not use the optional breakpoint-state service.");
        AssertContains(viewModel, "DebuggerBreakpointKind.Software", "Debugger ViewModel does not create software breakpoints through the neutral model.");
        AssertContains(viewModel, "DebuggerBreakpointAccess.Execute", "Debugger ViewModel does not constrain rev11 manager adds to execute breakpoints.");
        AssertContains(viewModel, "_deferredStopContextEvent", "Debugger ViewModel is missing deferred stop-context state for immediate breakpoint re-hits.");
        AssertContains(viewModel, "_continueInProgress", "Debugger ViewModel is missing Continue-specific stop-context deferral.");
        AssertContains(viewModel, "else if (_continueInProgress || _pendingInterruptedComposedExecutionOperation is not null)", "Debugger ViewModel does not defer a paused breakpoint/interruption event that arrives while an execution command is still completing.");
        AssertContains(viewModel, "RefreshDeferredStopContextIfNeeded();", "Debugger ViewModel does not replay deferred stop context after Continue completes.");
        AssertContains(codeBehind, "BreakpointDialog", "Debugger window does not open the breakpoint-add dialog.");
        AssertContains(codeBehind, "ConfirmationDialogTone.Danger", "Remove All breakpoints is not protected by the shared destructive confirmation dialog.");
        return Task.CompletedTask;
    }

    private static Task VerifyBreakpointDialogSourceAsync()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "BreakpointDialog.xaml"));
        string code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "BreakpointDialog.xaml.cs"));
        AssertContains(xaml, "Add Breakpoint / Watchpoint", "Breakpoint dialog does not describe the combined breakpoint/watchpoint scope.");
        AssertContains(xaml, "Software Execute Breakpoint", "Breakpoint dialog is missing the existing software execute-breakpoint option.");
        AssertContains(xaml, "Hardware Watchpoint", "Breakpoint dialog is missing hardware-watchpoint selection.");
        AssertContains(xaml, "Temporary (remove after first hit)", "Breakpoint dialog is missing temporary lifetime selection.");
        AssertContains(xaml, "DangerButtonStyle", "Breakpoint dialog Cancel action does not use the shared dismissive/danger styling.");
        AssertContains(code, "NumberStyles.AllowHexSpecifier", "Breakpoint dialog does not validate hexadecimal addresses.");
        AssertContains(code, "candidate.StartsWith(\"0x\"", "Breakpoint dialog does not accept the standard 0x address prefix.");
        AssertContains(code, "DebuggerBreakpointRequest", "Breakpoint dialog does not return the neutral breakpoint/watchpoint request model.");
        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerWatchpointManagerSourceAsync()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string viewModel = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerViewModel.cs"));
        string codeBehind = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerWindow.xaml.cs"));
        string dialogXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "BreakpointDialog.xaml"));
        string dialogCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "BreakpointDialog.xaml.cs"));

        AssertContains(xaml, "HasBreakpointManagementCapability", "Debugger manager does not remain visible for watchpoint-only backends.");
        AssertContains(xaml, "Header=\"Size\" Binding=\"{Binding Size}\"", "Debugger manager does not display hardware-watchpoint size metadata.");
        AssertContains(viewModel, "TargetCapabilities.Watchpoints", "Debugger ViewModel does not gate hardware-watchpoint creation by the neutral capability.");
        AssertContains(viewModel, "public async Task<bool> AddBreakpointAsync(DebuggerBreakpointRequest request)",
            "Debugger ViewModel does not route generic neutral breakpoint/watchpoint requests.");
        AssertContains(viewModel, "SelectedBreakpoint?.Breakpoint.Request.Access == DebuggerBreakpointAccess.Execute",
            "Debugger ViewModel can still navigate a data watchpoint address directly to the Disassembler.");
        AssertContains(codeBehind, "viewModel.HasWatchpointCapability", "Debugger window does not pass watchpoint capability into the add dialog.");
        AssertContains(codeBehind, "dialog.Request is DebuggerBreakpointRequest request", "Debugger window does not consume the dialog's neutral request.");
        AssertContains(dialogXaml, "Access", "Hardware-watchpoint dialog is missing access selection.");
        AssertContains(dialogXaml, "Size (bytes)", "Hardware-watchpoint dialog is missing generic byte-size input.");
        AssertContains(dialogXaml, "input:TextBoxInputFilter.Mode=\"HexAddress\"",
            "Breakpoint/watchpoint dialog address input does not reuse the host hexadecimal live filter.");
        AssertContains(dialogXaml, "input:TextBoxInputFilter.Mode=\"UnsignedInteger\"",
            "Hardware-watchpoint size input does not reuse the host unsigned-integer live filter.");
        AssertContains(dialogCode, "DebuggerBreakpointKind.Hardware", "Breakpoint dialog does not construct a neutral hardware request.");
        AssertFalse(dialogCode.Contains("ps5debug", StringComparison.OrdinalIgnoreCase),
            "Generic WPF breakpoint dialog contains PS5-specific backend rules.");
        AssertFalse(dialogCode.Contains("DR7", StringComparison.OrdinalIgnoreCase),
            "Generic WPF breakpoint dialog contains architecture-specific debug-register encoding.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerBreakpointClassificationSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string xaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string viewModel = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerBreakpointViewModel.cs"));

        AssertContains(xaml, "Header=\"Type\" Binding=\"{Binding Type}\"",
            "Debugger breakpoint table does not classify records as Breakpoint or Watchpoint.");
        AssertContains(xaml, "Header=\"Mechanism\" Binding=\"{Binding Mechanism}\"",
            "Debugger breakpoint table does not expose Software/Hardware as a separate mechanism column.");
        AssertContains(viewModel, "Breakpoint.Request.Access == DebuggerBreakpointAccess.Execute",
            "Debugger breakpoint classification is not based on execute-vs-data semantics.");
        AssertContains(viewModel, "? \"Breakpoint\"",
            "Debugger breakpoint classification is missing the Breakpoint label.");
        AssertContains(viewModel, ": \"Watchpoint\";",
            "Debugger breakpoint classification is missing the Watchpoint label.");
        AssertContains(viewModel, "public string Mechanism => Breakpoint.Request.Kind.ToString();",
            "Debugger breakpoint mechanism no longer exposes the neutral Software/Hardware kind.");
        return Task.CompletedTask;
    }

    private static Task VerifyMainWorkspaceDebuggerAddressActionsSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string xaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml"));
        string source = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml.cs"));
        string debuggerViewModel = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerViewModel.cs"));
        string toolManager = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "ToolWindowManager.cs"));

        AssertTrue(Regex.Matches(xaml, "Header=\"Add Breakpoint\"").Count == 2,
            "Scan Results and Saved Addresses do not both expose Add Breakpoint.");
        AssertTrue(Regex.Matches(xaml, "Header=\"Add Watchpoint\"").Count == 2,
            "Scan Results and Saved Addresses do not both expose Add Watchpoint.");
        AssertContains(xaml, "Tag=\"AddBreakpoint\"",
            "Debugger address-action menu lacks the AddBreakpoint enablement tag.");
        AssertContains(xaml, "Tag=\"AddWatchpoint\"",
            "Debugger address-action menu lacks the AddWatchpoint enablement tag.");
        AssertContains(source, "FindAttachedDebugger(plugin, targetProcess)",
            "Main workspace debugger shortcuts do not require an already-open attached Debugger for the same target.");
        AssertContains(source, "ValidateAddressActionRequest",
            "Main workspace debugger shortcuts bypass backend request validation.");
        AssertContains(source, "DebuggerBreakpointKind.Software",
            "Add Breakpoint does not construct a neutral software execute-breakpoint request.");
        AssertContains(source, "DebuggerBreakpointKind.Hardware",
            "Add Watchpoint does not construct a neutral hardware watchpoint request.");
        AssertContains(source, "DebuggerBreakpointAccess.Write",
            "Add Watchpoint does not default to Write access for the selected data address.");
        AssertContains(source, "scanResult.Result.CurrentValue.Size",
            "Scan Result watchpoint shortcuts do not preserve the selected value width.");
        AssertContains(source, "savedAddress.ValueSize",
            "Saved Address watchpoint shortcuts do not preserve the saved value width.");
        AssertContains(debuggerViewModel, "IDebuggerBreakpointValidationService",
            "Debugger address-action gating does not use the optional neutral validation service.");
        AssertContains(toolManager, "FindDataContext<TDataContext>",
            "Tool-window manager cannot resolve the already-open Debugger workspace used by address shortcuts.");
        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerCallStackAndSteppingSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string xaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string viewModel = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerViewModel.cs"));
        string codeBehind = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml.cs"));
        string frameViewModel = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerStackFrameViewModel.cs"));
        string mainWindowSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml.cs"));
        string pluginViewModelSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "PluginViewModel.cs"));

        AssertContains(xaml, "Content=\"Breakpoints / Watchpoints\"",
            "Debugger upper-right workspace is missing the Breakpoints / Watchpoints selector button.");
        AssertContains(xaml, "Content=\"Call Stack\"",
            "Debugger upper-right workspace is missing the Call Stack selector button.");
        AssertContains(xaml, "Command=\"{Binding ShowBreakpointsWorkspaceCommand}\"",
            "Debugger upper-right workspace does not route the Breakpoints / Watchpoints selector through the ViewModel.");
        AssertContains(xaml, "Command=\"{Binding ShowCallStackWorkspaceCommand}\"",
            "Debugger upper-right workspace does not route the Call Stack selector through the ViewModel.");
        AssertContains(xaml, "ItemsSource=\"{Binding CallFrames}\"",
            "Call Stack grid is not bound to the neutral call-frame collection.");
        AssertContains(xaml, "Header=\"Instruction Address\" Binding=\"{Binding InstructionAddressText}\"",
            "Call Stack grid is missing its compact instruction-address column.");
        AssertContains(xaml, "Header=\"Module\" Binding=\"{Binding ModuleName}\"",
            "Call Stack grid is missing module context.");
        AssertContains(xaml, "Header=\"Symbol\" Binding=\"{Binding SymbolName}\"",
            "Call Stack grid is missing symbol context.");
        AssertContains(xaml, "Text=\"{Binding SelectedStackFrame.StackPointerText}\"",
            "Selected Call Stack details are missing SP.");
        AssertContains(xaml, "Text=\"{Binding SelectedStackFrame.FramePointerText}\"",
            "Selected Call Stack details are missing FP.");
        AssertContains(xaml, "Text=\"{Binding SelectedStackFrame.ReturnAddressText}\"",
            "Selected Call Stack details are missing the return address.");
        AssertContains(xaml, "CallStackDataGrid_MouseDoubleClick",
            "Call Stack does not support direct Disassembler navigation by double-click.");
        AssertContains(xaml, "OpenSelectedCallFrameMemoryViewerButton_Click",
            "Call Stack is missing Memory Viewer navigation.");
        AssertContains(xaml, "Command=\"{Binding StepIntoCommand}\"",
            "Debugger execution controls are missing Step Into.");
        AssertContains(xaml, "Command=\"{Binding StepOverCommand}\"",
            "Debugger execution controls are missing Step Over.");
        AssertContains(xaml, "Command=\"{Binding StepOutCommand}\"",
            "Debugger execution controls are missing Step Out.");
        AssertContains(xaml, "Style=\"{StaticResource ColumnWorkspaceSplitterStyle}\"",
            "Debugger does not reuse the shared vertical workspace splitter style.");
        AssertContains(xaml, "Text=\"Events\"",
            "Debugger Events header was lost during the rev17 workspace re-layout.");
        AssertContains(xaml, "Content=\"Clear Events\"",
            "Debugger Events header is missing its local Clear Events action.");

        AssertContains(viewModel, "ObservableCollection<DebuggerStackFrameViewModel> CallFrames",
            "Debugger ViewModel is missing its call-frame collection.");
        AssertContains(viewModel, "IDebuggerCallStackService",
            "Debugger ViewModel does not consume the neutral call-stack service.");
        AssertContains(viewModel, "IDebuggerStepService",
            "Debugger ViewModel does not consume the neutral step service.");
        AssertContains(viewModel, "DebuggerStepKind.Into",
            "Debugger ViewModel does not route native Step Into through the neutral step contract.");
        AssertContains(viewModel, "instruction.FlowControl != DisassemblyFlowControl.Call",
            "Step Over does not use existing disassembly flow-control information to distinguish calls.");
        AssertContains(viewModel, "await RunToAddressAsync(returnAddress, \"Step Over\")",
            "Step Over does not compose call handling from a temporary run-to target.");
        AssertContains(viewModel, "await RunToAddressAsync(returnAddress, \"Step Out\")",
            "Step Out does not compose execution from the selected frame's return address.");
        AssertContains(viewModel, "isTemporary: true",
            "Debugger stepping composition is missing temporary breakpoint semantics.");
        AssertContains(viewModel, "_deferredStopContextEvent",
            "Rev17 stepping does not reuse deferred stop-context handling.");
        AssertContains(codeBehind, "OpenSelectedCallFrameDisassembler",
            "Debugger window code-behind is missing Call Stack -> Disassembler navigation.");
        AssertContains(codeBehind, "_openMemoryViewer(frame.InstructionAddress)",
            "Debugger window code-behind is missing Call Stack -> Memory Viewer navigation.");
        AssertContains(frameViewModel, "public string InstructionAddressText",
            "Call Stack presentation model is missing a formatted instruction address.");
        AssertContains(mainWindowSource, "CanOpenMemoryViewerFromDebugger",
            "Main window does not bind debugger Call Stack navigation to the current target lifetime.");
        AssertContains(pluginViewModelSource, "CanOpenMemoryViewerForTarget",
            "PluginViewModel is missing the target-safe Memory Viewer navigation gate used by Call Stack.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerBreakpointAwareStepOverSourceAsync()
    {
        string viewModel = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerViewModel.cs"));

        AssertContains(viewModel, "Dictionary<ulong, DisassembledInstruction> _softwareBreakpointInstructionCache",
            "Debugger ViewModel does not retain the original decoded instruction for software execute breakpoints.");
        AssertContains(viewModel, "CaptureSoftwareBreakpointInstructionAsync(request)",
            "Software breakpoint creation does not capture the original instruction before backend patching.");
        AssertContains(viewModel, "_softwareBreakpointInstructionCache[request.Address] = originalInstruction",
            "Successfully created software execute breakpoints do not retain their original decoded instruction.");
        AssertContains(viewModel, "_softwareBreakpointInstructionCache.Remove(request.Address);",
            "A fresh software execute breakpoint whose optional capture fails can retain stale instruction metadata from an earlier breakpoint at the same address.");
        AssertContains(viewModel, "GetLogicalSoftwareBreakpointInstruction(instructionPointer)",
            "Step Over does not consult breakpoint-aware original-instruction state.");
        AssertContains(viewModel, "debugEvent.TriggeredBreakpoint",
            "Breakpoint-aware Step Over is not tied to the neutral breakpoint that actually triggered the stop.");
        AssertContains(viewModel, "breakpoint.Request.Kind == DebuggerBreakpointKind.Software",
            "Breakpoint-aware Step Over does not restrict logical-stop reuse to software breakpoints.");
        AssertContains(viewModel, "breakpoint.Request.Access == DebuggerBreakpointAccess.Execute",
            "Breakpoint-aware Step Over does not restrict logical-stop reuse to execute breakpoints.");
        AssertContains(viewModel, "SetLogicalSoftwareBreakpointStop(context.Event)",
            "Debugger events do not establish the matching logical software-breakpoint stop context.");
        AssertContains(viewModel, "ClearSoftwareBreakpointInstructionState();",
            "Debugger lifecycle cleanup does not clear cached software-breakpoint instruction state.");
        AssertContains(viewModel, "Breakpoint creation must remain available even when optional instruction capture fails.",
            "Optional original-instruction capture is not explicitly isolated from breakpoint creation failure.");

        int addMethodIndex = viewModel.IndexOf(
            "public async Task<bool> AddBreakpointAsync(DebuggerBreakpointRequest request)",
            StringComparison.Ordinal);
        int addCaptureIndex = viewModel.IndexOf(
            "CaptureSoftwareBreakpointInstructionAsync(request)",
            addMethodIndex,
            StringComparison.Ordinal);
        int backendAddIndex = viewModel.IndexOf(
            ".AddBreakpointAsync(request, _lifetimeCancellation.Token)",
            addMethodIndex,
            StringComparison.Ordinal);
        int addCacheIndex = viewModel.IndexOf(
            "_softwareBreakpointInstructionCache[request.Address] = originalInstruction",
            backendAddIndex,
            StringComparison.Ordinal);
        AssertTrue(
            addMethodIndex >= 0 && addCaptureIndex > addMethodIndex && backendAddIndex > addCaptureIndex && addCacheIndex > backendAddIndex,
            "Original instruction capture must occur before software breakpoint installation and must be retained only after the backend add succeeds.");

        int stepOverMethodIndex = viewModel.IndexOf("private async Task StepOverAsync()", StringComparison.Ordinal);
        int cachedLookupIndex = viewModel.IndexOf(
            "GetLogicalSoftwareBreakpointInstruction(instructionPointer)",
            stepOverMethodIndex,
            StringComparison.Ordinal);
        int liveDisassemblyIndex = viewModel.IndexOf(
            ".ReadDisassemblyContextAsync(",
            cachedLookupIndex,
            StringComparison.Ordinal);
        AssertTrue(stepOverMethodIndex >= 0 && cachedLookupIndex > stepOverMethodIndex && liveDisassemblyIndex > cachedLookupIndex,
            "Step Over must prefer the original cached call instruction before falling back to breakpoint-patched live disassembly.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerWorkspaceModeSwitcherSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string xaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string viewModel = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerViewModel.cs"));

        AssertFalse(xaml.Contains("<TabControl", StringComparison.Ordinal),
            "Debugger upper-right workspace must not reintroduce the WPF TabControl that repeatedly clipped its header edge at runtime.");
        AssertFalse(xaml.Contains("<TabItem", StringComparison.Ordinal),
            "Debugger upper-right workspace must not reintroduce WPF TabItems.");
        AssertContains(xaml, "Command=\"{Binding ShowBreakpointsWorkspaceCommand}\"",
            "Breakpoints / Watchpoints selector button is not bound to its workspace-switch command.");
        AssertContains(xaml, "Command=\"{Binding ShowCallStackWorkspaceCommand}\"",
            "Call Stack selector button is not bound to its workspace-switch command.");
        AssertContains(xaml, "Visibility=\"{Binding IsBreakpointsWorkspaceSelected, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            "Breakpoints / Watchpoints content is not controlled by the selected workspace state.");
        AssertContains(xaml, "Visibility=\"{Binding IsCallStackWorkspaceSelected, Converter={StaticResource BooleanToVisibilityConverter}}\"",
            "Call Stack content is not controlled by the selected workspace state.");

        AssertContains(viewModel, "public bool IsBreakpointsWorkspaceSelected",
            "Debugger ViewModel does not expose Breakpoints / Watchpoints workspace selection state.");
        AssertContains(viewModel, "public bool IsCallStackWorkspaceSelected",
            "Debugger ViewModel does not expose Call Stack workspace selection state.");
        AssertContains(viewModel, "public ICommand ShowBreakpointsWorkspaceCommand",
            "Debugger ViewModel is missing the Breakpoints / Watchpoints workspace-switch command.");
        AssertContains(viewModel, "public ICommand ShowCallStackWorkspaceCommand",
            "Debugger ViewModel is missing the Call Stack workspace-switch command.");
        AssertContains(viewModel, "_isCallStackWorkspaceSelected = !HasBreakpointManagementCapability && HasCallStackCapability;",
            "Debugger workspace does not default to Breakpoints / Watchpoints when both views are available or fall back to Call Stack when it is the only view.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerWorkspaceSwitchButtonThemeSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string xaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string buttonStyles = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "ButtonStyles.xaml"));
        string controlStyles = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "ControlStyles.xaml"));

        AssertContains(buttonStyles, "x:Key=\"DebuggerWorkspaceSwitchButtonStyle\"",
            "Shared button styles are missing the compact Debugger workspace selector base style.");
        AssertContains(buttonStyles, "BasedOn=\"{StaticResource SecondaryButtonStyle}\"",
            "Debugger workspace selector does not reuse the normal application button template.");
        AssertContains(buttonStyles, "<Setter Property=\"Height\" Value=\"28\" />",
            "Debugger workspace selector does not use the requested compact height.");
        AssertContains(buttonStyles, "<Setter Property=\"FontSize\" Value=\"12\" />",
            "Debugger workspace selector does not use the requested smaller text size.");
        AssertContains(buttonStyles, "Binding=\"{Binding IsBreakpointsWorkspaceSelected}\" Value=\"True\"",
            "Breakpoints / Watchpoints selector has no persistent selected-state trigger.");
        AssertContains(buttonStyles, "Binding=\"{Binding IsCallStackWorkspaceSelected}\" Value=\"True\"",
            "Call Stack selector has no persistent selected-state trigger.");
        AssertTrue(Regex.Matches(buttonStyles, Regex.Escape("<Setter Property=\"BorderBrush\" Value=\"{DynamicResource AccentBrush}\" />")).Count >= 2,
            "Both workspace selector buttons must use the theme accent outline when selected.");
        AssertTrue(Regex.Matches(buttonStyles, Regex.Escape("<Setter Property=\"BorderThickness\" Value=\"2\" />")).Count >= 2,
            "Both workspace selector buttons must make their selected outline clearly visible.");
        AssertContains(xaml, "Style=\"{StaticResource DebuggerBreakpointsSwitchButtonStyle}\"",
            "Breakpoints / Watchpoints selector does not use its shared selected-state button style.");
        AssertContains(xaml, "Style=\"{StaticResource DebuggerCallStackSwitchButtonStyle}\"",
            "Call Stack selector does not use its shared selected-state button style.");
        AssertFalse(controlStyles.Contains("WorkspaceTabControlStyle", StringComparison.Ordinal),
            "Unused workspace TabControl styling remains in active shared resources after the button-switcher replacement.");
        AssertFalse(controlStyles.Contains("WorkspaceTabItemStyle", StringComparison.Ordinal),
            "Unused workspace TabItem styling remains in active shared resources after the button-switcher replacement.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerWorkspacePanelLayoutSourceAsync()
    {
        string xaml = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "DebuggerWindow.xaml"));

        AssertContains(xaml, "Width=\"1240\"",
            "Debugger default width was not increased for the expanded workspace.");
        AssertContains(xaml, "Height=\"780\"",
            "Debugger default height was not increased for the expanded workspace.");
        AssertFalse(xaml.Contains("Text=\"{Binding Breakpoints.Count}\"", StringComparison.Ordinal),
            "Breakpoints / Watchpoints count remains in the removed inner title row.");
        AssertFalse(xaml.Contains("Text=\"{Binding CallFrames.Count}\"", StringComparison.Ordinal),
            "Call Stack count remains in the removed inner title row.");
        AssertFalse(xaml.Contains("Text=\"Breakpoints / Watchpoints\" Style=\"{StaticResource SectionTitleTextStyle}\"", StringComparison.Ordinal),
            "Breakpoints / Watchpoints still duplicates its selector label as an inner title.");
        AssertFalse(xaml.Contains("Text=\"Call Stack\" Style=\"{StaticResource SectionTitleTextStyle}\"", StringComparison.Ordinal),
            "Call Stack still duplicates its selector label as an inner title.");
        AssertContains(xaml, "Grid.Column=\"2\" HorizontalAlignment=\"Right\"",
            "Breakpoints / Watchpoints footer is missing its right-aligned action group.");
        AssertContains(xaml, "Content=\"Add...\"",
            "Breakpoints / Watchpoints footer is missing Add.");
        AssertContains(xaml, "Command=\"{Binding RefreshBreakpointsCommand}\"",
            "Breakpoints / Watchpoints footer is missing Refresh.");
        AssertContains(xaml, "Command=\"{Binding RefreshCallStackCommand}\"",
            "Call Stack footer is missing its moved Refresh action.");
        AssertContains(xaml, "OpenSelectedCallFrameDisassemblerButton_Click",
            "Call Stack footer lost Disassembler navigation during the layout refresh.");
        AssertContains(xaml, "OpenSelectedCallFrameMemoryViewerButton_Click",
            "Call Stack footer lost Memory Viewer navigation during the layout refresh.");

        return Task.CompletedTask;
    }

    private static Task VerifyToolWindowIndependentZOrderSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string mainWindowSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml.cs"));
        string managerSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "ToolWindowManager.cs"));

        AssertEqual(3, Regex.Matches(mainWindowSource, Regex.Escape("_toolWindowManager.Show(window, this);")).Count,
            "Debugger, Disassembler, and Memory Viewer must all use the shared modeless tool-window launcher.");
        AssertContains(managerSource, "window.Owner = placementOwner;",
            "Tool-window launch no longer preserves CenterOwner placement during initial Show.");
        AssertContains(managerSource, "window.Show();",
            "Tool-window manager no longer opens tools modelessly.");
        AssertContains(managerSource, "window.Owner = null;",
            "Tool-window manager does not release WPF ownership after modeless launch, so the main window would remain below its tools.");
        AssertFalse(managerSource.Contains("Topmost", StringComparison.Ordinal),
            "Tool-window z-order must not be implemented with Topmost.");

        return Task.CompletedTask;
    }

    private static Task VerifyMainWindowToolCleanupSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string mainWindowSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml.cs"));
        string managerSource = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "ToolWindowManager.cs"));

        AssertContains(mainWindowSource, "protected override void OnClosing(CancelEventArgs e)",
            "Main window does not coordinate modeless tool cleanup before application shutdown.");
        AssertContains(mainWindowSource, "e.Cancel = true;",
            "Main-window shutdown is not held while asynchronous tool cleanup completes.");
        AssertContains(mainWindowSource, "_ = CompleteToolWindowShutdownAsync();",
            "Main-window closing does not hand asynchronous tool cleanup to the dedicated shutdown coordinator.");
        AssertContains(mainWindowSource, "await _toolWindowManager.CloseAllAsync().ConfigureAwait(true);",
            "Main window does not await tool-window cleanup before completing its close.");
        AssertContains(managerSource, "window.IsEnabled = false;",
            "Tool-window manager does not disable tracked tools before asynchronous shutdown cleanup, allowing new tool actions to race application exit.");
        AssertContains(managerSource, "dataContext is IAsyncDisposable",
            "Tool-window manager does not await asynchronous DataContext cleanup such as the Debugger detach path.");
        AssertContains(managerSource, "dataContext is IDisposable",
            "Tool-window manager does not run synchronous DataContext cleanup for existing tool workspaces.");
        AssertContains(managerSource, "window.Close();",
            "Tool-window manager does not close the tracked window after its cleanup attempt.");
        AssertContains(managerSource, "window.Closed += ToolWindow_Closed;",
            "Tool-window manager does not untrack windows that users close normally.");

        return Task.CompletedTask;
    }

    private static Task VerifyMainWindowShutdownReentrySourceAsync()
    {
        string mainWindowSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "MainWindow.xaml.cs"));

        AssertContains(mainWindowSource, "private async Task CompleteToolWindowShutdownAsync()",
            "Main-window tool cleanup is not isolated from the synchronous WPF OnClosing stack.");
        AssertContains(mainWindowSource, "_ = Dispatcher.BeginInvoke(new Action(Close));",
            "Main-window final Close is not queued through the dispatcher with the awaitable DispatcherOperation explicitly discarded.");

        int helperStart = mainWindowSource.IndexOf("private async Task CompleteToolWindowShutdownAsync()", StringComparison.Ordinal);
        int onClosedStart = mainWindowSource.IndexOf("protected override void OnClosed", helperStart, StringComparison.Ordinal);
        AssertTrue(helperStart >= 0 && onClosedStart > helperStart,
            "Main-window shutdown helper could not be isolated for close re-entry verification.");

        string helper = mainWindowSource[helperStart..onClosedStart];
        AssertFalse(Regex.IsMatch(helper, @"(?m)^\s*Close\(\);\s*$"),
            "Main-window shutdown helper still calls Close directly and can re-enter WPF while the original close request is active.");
        AssertContains(helper, "_toolWindowShutdownCompleted = true;",
            "Main-window shutdown helper no longer arms the completed guard before queuing final close.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerRunToAddressSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string xaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string viewModel = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerViewModel.cs"));
        string codeBehind = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml.cs"));
        string dialogXaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "RunToAddressDialog.xaml"));
        string dialogCode = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "RunToAddressDialog.xaml.cs"));

        AssertContains(xaml, "Click=\"RunToAddressButton_Click\"",
            "Debugger execution controls are missing Run to Address.");
        AssertContains(xaml, "IsEnabled=\"{Binding CanRunToAddress}\"",
            "Run to Address is not gated by current debugger state/capabilities.");
        AssertContains(codeBehind, "RunToAddressDialog",
            "Debugger window does not use the dedicated Run to Address dialog.");
        AssertContains(codeBehind, "await viewModel.RunToAddressAsync(address)",
            "Run to Address dialog result is not routed into the debugger ViewModel.");
        AssertContains(viewModel, "public Task RunToAddressAsync(ulong address)",
            "Debugger ViewModel is missing its Run to Address operation.");
        AssertContains(viewModel, "DebuggerBreakpointKind.Software",
            "Run to Address does not compose execution from the existing software-breakpoint service.");
        AssertContains(viewModel, "DebuggerBreakpointAccess.Execute",
            "Run to Address does not require an execute breakpoint.");
        AssertContains(viewModel, "isTemporary: true",
            "Run to Address does not create a temporary breakpoint.");
        AssertContains(viewModel, "ApplyPostExecutionCommandState(",
            "Debugger execution commands do not reconcile status text against the coordinator's final state after asynchronous stop events.");
        AssertContains(viewModel, "finalState == DebuggerSessionState.Paused && _deferredStopContextEvent is null",
            "Debugger execution status reconciliation can overwrite a deferred paused breakpoint/step event message.");
        AssertContains(dialogXaml, "input:TextBoxInputFilter.Mode=\"HexAddress\"",
            "Run to Address does not reuse the application's hexadecimal address input filter.");
        AssertContains(dialogXaml, "Style=\"{StaticResource DangerButtonStyle}\"",
            "Run to Address Cancel does not use the shared dismissive/danger styling.");
        AssertContains(dialogCode, "NumberStyles.AllowHexSpecifier",
            "Run to Address does not parse hexadecimal addresses explicitly.");
        AssertContains(dialogCode, "candidate.StartsWith(\"0x\"",
            "Run to Address does not accept the standard 0x address prefix.");
        AssertFalse(dialogCode.Contains("ps5debug", StringComparison.OrdinalIgnoreCase),
            "Generic Run to Address UI contains PS5-specific backend logic.");

        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerInterruptedComposedOperationCleanupSourceAsync()
    {
        string viewModel = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerViewModel.cs"));

        AssertContains(viewModel, "ComposedExecutionOperation? _activeComposedExecutionOperation",
            "Debugger ViewModel does not track the currently active composed execution operation.");
        AssertContains(viewModel, "ComposedExecutionOperation? _pendingInterruptedComposedExecutionOperation",
            "Debugger ViewModel does not retain an interrupted composed operation until cleanup can run safely.");
        AssertContains(viewModel, "existing.Id,",
            "Composed execution does not retain the exact breakpoint id used as its stop target.");
        AssertContains(viewModel, "ownsTemporaryBreakpoint);",
            "Composed execution does not distinguish an operation-owned temporary breakpoint from a reused persistent breakpoint.");
        AssertContains(viewModel, "IsComposedExecutionTargetEvent(operation, context.Event)",
            "Debugger stop events are not matched against the active composed-operation target.");
        AssertContains(viewModel, "operation.OwnsTemporaryBreakpoint",
            "Interrupted-stop handling can remove a breakpoint that was not created by the composed operation.");
        AssertContains(viewModel, "CleanupComposedExecutionOperationAsync(interruptedOperation, debugEvent)",
            "Deferred stop-context processing does not clean an interrupted composed operation before refreshing the paused context.");
        AssertContains(viewModel, "_continueInProgress || _pendingInterruptedComposedExecutionOperation is not null",
            "A paused interruption can be lost while the execution command is still completing.");
        AssertContains(viewModel, "RefreshDeferredStopContextIfNeeded();",
            "Manual Pause completion does not release deferred composed-operation cleanup.");
        AssertContains(viewModel, ".RemoveBreakpointAsync(operation.BreakpointId, _lifetimeCancellation.Token)",
            "Interrupted composed operations do not remove their owned temporary breakpoint through the neutral breakpoint service.");
        AssertContains(viewModel, "ClearComposedExecutionState();",
            "Debugger lifecycle cleanup does not clear active/deferred composed-operation state.");

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

    private static async Task VerifyDisassemblyDebuggerByteOverlayAsync()
    {
        MockTargetPlugin plugin = new();
        await using ITargetSession session = await plugin
            .ConnectAsync(TargetConnectionOptions.Empty, CancellationToken.None)
            .ConfigureAwait(false);

        TargetProcess process = (await session.GetRequiredService<IProcessProvider>()
            .GetProcessesAsync(CancellationToken.None).ConfigureAwait(false)).Single();
        IReadOnlyList<MemoryRegion> regions = await session.GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None).ConfigureAwait(false);
        IMemoryReader memoryReader = session.GetRequiredService<IMemoryReader>();
        IMemoryWriter memoryWriter = session.GetRequiredService<IMemoryWriter>();

        await memoryWriter
            .WriteAsync(process, MockTargetLayout.CodeAddress, new byte[] { 0xCC }, CancellationToken.None)
            .ConfigureAwait(false);

        DisassemblyOverlay overlay = new(
            new[]
            {
                new DisassemblyByteOverlay(
                    MockTargetLayout.CodeAddress,
                    MockTargetLayout.CodeBytes.Span.Slice(0, 2))
            },
            new[]
            {
                new DisassemblyMarker(MockTargetLayout.CodeAddress, "Breakpoint"),
                new DisassemblyMarker(MockTargetLayout.CodeAddress + 2UL, "Watchpoint hit")
            });

        DisassemblySnapshot snapshot = await new DisassemblyReader()
            .ReadAsync(
                memoryReader,
                session.GetRequiredService<IDisassemblerProvider>(),
                process,
                regions,
                session.Architecture,
                MockTargetLayout.CodeAddress,
                requestedByteCount: 4,
                overlay: overlay,
                cancellationToken: CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual((byte)0x10, snapshot.Bytes.Span[0],
            "The logical disassembly view did not restore the original first byte over debugger INT3 instrumentation.");
        AssertEqual((byte)0x2A, snapshot.Bytes.Span[1],
            "The logical disassembly view did not preserve the rest of the original instruction bytes.");
        AssertEqual("load", snapshot.Instructions[0].Mnemonic,
            "The disassembler decoded debugger INT3 instrumentation instead of the restored logical instruction.");
        AssertTrue(snapshot.Markers.Any(marker =>
                marker.Address == MockTargetLayout.CodeAddress && marker.Text == "Breakpoint"),
            "The logical disassembly snapshot lost its breakpoint marker.");
        AssertTrue(snapshot.Markers.Any(marker =>
                marker.Address == MockTargetLayout.CodeAddress + 2UL && marker.Text == "Watchpoint hit"),
            "The logical disassembly snapshot lost its watchpoint-hit marker.");

        byte[] rawTargetBytes = new byte[2];
        int bytesRead = await memoryReader
            .ReadAsync(process, MockTargetLayout.CodeAddress, rawTargetBytes, CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(2, bytesRead, "The target-memory verification read returned an unexpected byte count.");
        AssertEqual((byte)0xCC, rawTargetBytes[0],
            "The presentation overlay wrote its logical bytes back into target memory.");
        AssertEqual((byte)0x2A, rawTargetBytes[1],
            "The presentation overlay unexpectedly changed adjacent target memory.");
    }

    private static Task VerifyDebuggerDisassemblyOverlayLifecycleAsync()
    {
        DebuggerDisassemblyOverlayState state = new();
        ulong address = 0x401000;
        DisassembledInstruction originalInstruction = new(
            address,
            new byte[] { 0xE8, 0x10, 0x00, 0x00, 0x00 },
            "call",
            "0x401015",
            DisassemblyFlowControl.Call,
            0x401015);
        DebuggerBreakpoint breakpoint = new(
            "sw-1",
            new DebuggerBreakpointRequest(
                address,
                1,
                DebuggerBreakpointKind.Software,
                DebuggerBreakpointAccess.Execute),
            isEnabled: true);

        state.RememberSoftwareBreakpointInstruction(originalInstruction);
        state.SynchronizeBreakpoints(new[] { breakpoint }, DebuggerSessionState.Paused);
        DisassemblyOverlay activeOverlay = state.CreateOverlay();
        AssertEqual(1, activeOverlay.ByteOverlays.Count,
            "An enabled software breakpoint did not expose its original instruction bytes to Disassembler.");
        AssertTrue(activeOverlay.Markers.Any(marker => marker.Address == address && marker.Text == "Breakpoint"),
            "An enabled software breakpoint did not expose a Disassembler marker.");

        state.SynchronizeBreakpoints(Array.Empty<DebuggerBreakpoint>(), DebuggerSessionState.Paused);
        DisassemblyOverlay stagedOverlay = state.CreateOverlay();
        AssertEqual(1, stagedOverlay.ByteOverlays.Count,
            "Removing a software breakpoint while paused stopped masking backend INT3 before staged cleanup could finish.");
        AssertFalse(stagedOverlay.Markers.Any(marker => marker.Text.StartsWith("Breakpoint", StringComparison.Ordinal)),
            "A removed breakpoint remained visible as an active Disassembler marker during staged backend cleanup.");

        state.CompleteStagedSoftwareBreakpointRetirements();
        AssertTrue(state.CreateOverlay().IsEmpty,
            "Completed software-breakpoint retirement left stale logical bytes or markers in the Disassembler overlay.");

        DebuggerBreakpoint watchpoint = new(
            "hw-1",
            new DebuggerBreakpointRequest(
                0x500000,
                4,
                DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.Write));
        state.ObserveEvent(new DebuggerEvent(
            DebuggerEventKind.Watchpoint,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.Watchpoint,
            threadId: 7,
            instructionPointer: 0x402003,
            triggeredBreakpoint: watchpoint));
        DisassemblyOverlay unresolvedWatchpointOverlay = state.CreateOverlay();
        AssertTrue(unresolvedWatchpointOverlay.Markers.Any(marker =>
                marker.Address == 0x402003 && marker.Text == "Watchpoint stop (trigger unresolved)"),
            "An unresolved watchpoint stop falsely presented the current instruction as the triggering instruction.");

        state.ObserveEvent(new DebuggerEvent(
            DebuggerEventKind.Watchpoint,
            DebuggerExecutionState.Paused,
            DebuggerStopReason.Watchpoint,
            threadId: 7,
            instructionPointer: 0x402003,
            triggeredBreakpoint: watchpoint,
            triggerInstructionAddress: 0x402000,
            triggerResolution: DebuggerTriggerResolution.DisassemblyDerived));
        DisassemblyOverlay watchpointOverlay = state.CreateOverlay();
        AssertTrue(watchpointOverlay.Markers.Any(marker =>
                marker.Address == 0x402000 && marker.Text == "Watchpoint hit"),
            "A resolved watchpoint event did not move the Disassembler marker to the triggering instruction.");
        AssertFalse(watchpointOverlay.Markers.Any(marker =>
                marker.Address == 0x402003 && marker.Text == "Watchpoint hit"),
            "A resolved watchpoint event still marked the post-access stop instruction as the trigger.");
        AssertTrue(watchpointOverlay.Markers.Any(marker =>
                marker.Address == 0x402003 && marker.Text == "Stop / Current IP"),
            "A resolved watchpoint event did not preserve the real stop/current instruction as a separate Disassembler marker.");

        state.ObserveEvent(new DebuggerEvent(
            DebuggerEventKind.Resumed,
            DebuggerExecutionState.Running));
        AssertTrue(state.CreateOverlay().IsEmpty,
            "A stale watchpoint-hit marker survived target resume.");

        return Task.CompletedTask;
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
        AssertFalse(xaml.Contains("The visible range is decoded as one continuous stream.", StringComparison.Ordinal),
            "Disassembler still contains the removed visible-range explanatory text.");

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

    private static Task VerifyDisassemblerDebuggerMarkersSourceAsync()
    {
        string xamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml");
        string debuggerViewModelPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerViewModel.cs");
        string pluginViewModelPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PluginViewModel.cs");
        string disassemblerViewModelPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerViewModel.cs");
        string rowViewModelPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblyInstructionViewModel.cs");

        foreach (string path in new[]
                 {
                     xamlPath,
                     debuggerViewModelPath,
                     pluginViewModelPath,
                     disassemblerViewModelPath,
                     rowViewModelPath
                 })
        {
            AssertTrue(File.Exists(path),
                $"Disassembler debugger-marker fixture is missing: {Path.GetFileName(path)}");
        }

        string xaml = File.ReadAllText(xamlPath);
        int addressColumn = xaml.IndexOf("Header=\"Address\"", StringComparison.Ordinal);
        int bytesColumn = xaml.IndexOf("Header=\"Bytes\"", StringComparison.Ordinal);
        int markersColumn = xaml.IndexOf("Header=\"Markers\"", StringComparison.Ordinal);
        int instructionColumn = xaml.IndexOf("Header=\"Instruction\"", StringComparison.Ordinal);
        AssertTrue(
            addressColumn >= 0 && bytesColumn > addressColumn && markersColumn > bytesColumn && instructionColumn > markersColumn,
            "Disassembler columns must remain ordered Address, Bytes, Markers, Instruction.");
        AssertTrue(xaml.Contains("Binding=\"{Binding Markers}\"", StringComparison.Ordinal),
            "Disassembler Markers column is not bound to row marker metadata.");

        string rowSource = File.ReadAllText(rowViewModelPath);
        AssertTrue(rowSource.Contains("public string Markers { get; }", StringComparison.Ordinal),
            "Disassembly rows do not expose debugger marker text.");
        AssertTrue(rowSource.Contains("public bool IsWatchpointHitRow { get; }", StringComparison.Ordinal) &&
                   rowSource.Contains("public bool IsWatchpointStopRow { get; }", StringComparison.Ordinal),
            "Disassembly rows do not expose separate resolved-hit and stop/current highlight state.");

        string disassemblerSource = File.ReadAllText(disassemblerViewModelPath);
        AssertTrue(disassemblerSource.Contains("snapshot.Markers", StringComparison.Ordinal),
            "Disassembler does not map snapshot marker metadata onto displayed rows.");
        AssertTrue(disassemblerSource.Contains("_currentSnapshot.Markers", StringComparison.Ordinal),
            "Disassembler export scopes do not preserve marker metadata.");

        string debuggerSource = File.ReadAllText(debuggerViewModelPath);
        AssertTrue(debuggerSource.Contains("RememberSoftwareBreakpointInstruction", StringComparison.Ordinal),
            "Debugger does not publish original software-breakpoint instructions for logical disassembly.");
        AssertTrue(debuggerSource.Contains("SynchronizeBreakpoints", StringComparison.Ordinal),
            "Debugger breakpoint refresh does not synchronize Disassembler marker state.");
        AssertTrue(debuggerSource.Contains("ObserveEvent", StringComparison.Ordinal),
            "Debugger events do not publish watchpoint-hit marker state.");
        AssertTrue(debuggerSource.Contains("StageSoftwareBreakpointRetirement", StringComparison.Ordinal),
            "Debugger does not preserve original instruction presentation while paused breakpoint cleanup is staged.");

        string pluginSource = File.ReadAllText(pluginViewModelPath);
        AssertTrue(pluginSource.Contains("GetDebuggerDisassemblyOverlay", StringComparison.Ordinal) &&
                   pluginSource.Contains("disassemblyOverlay", StringComparison.Ordinal),
            "Host disassembly reads are not connected to the matching debugger session's logical overlay.");

        string combinedHostSource = debuggerSource + pluginSource + disassemblerSource + rowSource;
        AssertFalse(combinedHostSource.Contains("Ps5DebuggerSession", StringComparison.Ordinal),
            "Logical Disassembler presentation must remain host-neutral rather than depending on the PS5 debugger implementation.");

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
            DisassemblyMarker[] markers =
            {
                new(0x401000, "Breakpoint"),
                new(0x401005, "Watchpoint hit")
            };
            DisassemblyExportSource source = new(
                instructions,
                region,
                moduleBaseAddress: 0x400000,
                metadata,
                markers);

            AssertEqual("disassembly", source.Type, "Disassembly export type changed unexpectedly.");
            AssertEqual(1, source.SchemaVersion, "Disassembly export schema version changed unexpectedly.");
            AssertEqual(2L, source.Count, "Disassembly export row count is incorrect.");
            AssertTrue(source.Columns.Any(column => column.Id == DisassemblyExportColumnIds.Markers),
                "Disassembly export is missing the Markers column.");
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
            AssertEqual("Breakpoint", firstRow.GetProperty("markers").GetString(),
                "Disassembly JSON marker metadata is incorrect.");
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
        string exportDialogXamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DataExportDialog.xaml");
        string exportDialogCodePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DataExportDialog.xaml.cs");
        string exportDialogXaml = File.ReadAllText(exportDialogXamlPath);
        string exportDialogCode = File.ReadAllText(exportDialogCodePath);
        AssertTrue(exportDialogXaml.Contains("<ComboBox.ItemTemplate>", StringComparison.Ordinal) &&
                   exportDialogXaml.Contains("Text=\"{Binding DisplayName}\"", StringComparison.Ordinal) &&
                   !exportDialogXaml.Contains("DisplayMemberPath=\"DisplayName\"", StringComparison.Ordinal),
            "Universal export dialog must render scope and format option DisplayName values through explicit item templates.");
        AssertContains(exportDialogXaml, "Click=\"SelectNoColumnsButton_Click\"",
            "Universal export dialog does not expose Select None next to Select All.");
        AssertContains(exportDialogXaml, "x:Name=\"ContinueButton\"",
            "Universal export dialog Continue action is not addressable for live column-selection gating.");
        AssertContains(exportDialogCode, "ContinueButton.IsEnabled = hasSelectedColumn",
            "Universal export dialog does not disable Continue immediately when no columns are selected.");
        AssertContains(exportDialogCode, "ValidationTextBlock.Text = hasSelectedColumn ? string.Empty : \"Select at least one column.\"",
            "Universal export dialog validation text is not synchronized with live column selection.");

        string xamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml");
        string windowSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerWindow.xaml.cs");
        string viewModelSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "DisassemblerViewModel.cs");
        string pluginViewModelSourcePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PluginViewModel.cs");
        string mainWindowXamlPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml");

        foreach (string path in new[] { xamlPath, windowSourcePath, viewModelSourcePath, pluginViewModelSourcePath, mainWindowXamlPath })
        {
            AssertTrue(File.Exists(path),
                $"Disassembler/export UI fixture is missing: {Path.GetFileName(path)}");
        }

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
        AssertContains(viewModelSource, "public bool CanExport => !_disposed && !IsBusy && Instructions.Count > 0;",
            "Disassembler Export must be disabled when there are no displayed instructions.");

        string pluginViewModelSource = File.ReadAllText(pluginViewModelSourcePath);
        AssertContains(pluginViewModelSource, "public bool CanExportScanResults =>\n        HasVisibleScanResults &&",
            "Scan Results Export must be disabled when there are no results.");
        AssertContains(pluginViewModelSource, "public bool CanExportSavedAddresses =>\n        HasSavedAddresses &&",
            "Saved Addresses Export must be disabled when there are no saved addresses.");

        string mainWindowXaml = File.ReadAllText(mainWindowXamlPath);
        AssertContains(mainWindowXaml, "IsEnabled=\"{Binding SelectedPlugin.HasSavedAddresses}\"",
            "Saved Addresses Remove All must be disabled when the list is empty.");

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
            TargetCapabilities.Breakpoints |
            TargetCapabilities.Watchpoints |
            TargetCapabilities.ThreadEnumeration |
            TargetCapabilities.ThreadControl |
            TargetCapabilities.RegisterAccess |
            TargetCapabilities.CallStack |
            TargetCapabilities.StepExecution,
            plugin.Capabilities,
            "PS5 plugin should advertise its implemented connection, memory access, native scan, process-control, disassembly, debugger, breakpoint/watchpoint, thread/register, call-stack, and stepping support.");
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

        AssertEqual(76, registers.Count, "PS5 register mapper returned an unexpected general/FPU-SIMD/FS-GS register count.");
        AssertTrue(registers.All(register => !register.CanWrite),
            "PS5 register snapshots must remain read-only while upstream register-write paths are unverified.");
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

        DebuggerRegister fcw = registers.Single(register => register.Id == "fcw");
        DebuggerRegister st0 = registers.Single(register => register.Id == "st0");
        DebuggerRegister xmm0 = registers.Single(register => register.Id == "xmm0");
        DebuggerRegister ymm0 = registers.Single(register => register.Id == "ymm0");
        DebuggerRegister fsBase = registers.Single(register => register.Id == "fsbase");
        DebuggerRegister gsBase = registers.Single(register => register.Id == "gsbase");

        AssertEqual(16, fcw.BitWidth, "PS5 FPU control-word width was mapped incorrectly.");
        AssertEqual((ushort)0x037F, BinaryPrimitives.ReadUInt16LittleEndian(fcw.Value.Span),
            "PS5 FPU control word was mapped from the wrong offset.");
        AssertEqual(80, st0.BitWidth, "PS5 x87 register width was mapped incorrectly.");
        AssertEqual("FPU", st0.Group, "PS5 x87 register did not retain its neutral FPU group.");
        AssertEqual(128, xmm0.BitWidth, "PS5 XMM register width was mapped incorrectly.");
        AssertEqual(256, ymm0.BitWidth, "PS5 YMM register width was mapped incorrectly.");
        AssertEqual("SIMD", ymm0.Group, "PS5 YMM register did not retain its neutral SIMD group.");
        AssertTrue(xmm0.Value.Span.SequenceEqual(Enumerable.Range(0, 16).Select(index => (byte)(0x20 + index)).ToArray()),
            "PS5 XMM0 lower 128-bit payload was mapped from the wrong FPU-state offset.");
        AssertTrue(ymm0.Value.Span[..16].SequenceEqual(xmm0.Value.Span),
            "PS5 YMM0 lower half does not match the corresponding XMM0 state.");
        AssertTrue(ymm0.Value.Span[16..].SequenceEqual(Enumerable.Range(0, 16).Select(index => (byte)(0x80 + index)).ToArray()),
            "PS5 YMM0 upper 128-bit payload was mapped from the wrong xstate offset.");
        AssertFalse(registers.Any(register => register.Id.StartsWith("dr", StringComparison.Ordinal)),
            "PS5 paused register refresh exposed debug-register rows even though GETDBREGS is backend-blocked for an already stopped target.");
        AssertEqual(0x7200000000000102UL, BinaryPrimitives.ReadUInt64LittleEndian(fsBase.Value.Span),
            "PS5 FS base was mapped incorrectly.");
        AssertEqual(0x7300000000000102UL, BinaryPrimitives.ReadUInt64LittleEndian(gsBase.Value.Span),
            "PS5 GS base was mapped incorrectly.");

        AssertEqual(1, server.DebuggerRegisterReadThreadIds.Count,
            "PS5 register service sent an unexpected number of GETREGS requests.");
        AssertEqual((uint)0x102, server.DebuggerRegisterReadThreadIds[0],
            "PS5 register service sent GETREGS for the wrong LWP id.");
        AssertEqual(1, server.DebuggerFloatingPointRegisterReadThreadIds.Count,
            "PS5 register service sent an unexpected number of GETFPREGS requests.");
        AssertEqual((uint)0x102, server.DebuggerFloatingPointRegisterReadThreadIds[0],
            "PS5 register service sent GETFPREGS for the wrong LWP id.");
        AssertEqual(0, server.DebuggerDebugRegisterReadThreadIds.Count,
            "PS5 paused register service must not send GETDBREGS because the current ps5debug-NG handler can block when the target is already stopped.");
        AssertEqual(1, server.DebuggerFsGsBaseReadThreadIds.Count,
            "PS5 register service sent an unexpected number of GETFSGSBASE requests.");
        AssertEqual((uint)0x102, server.DebuggerFsGsBaseReadThreadIds[0],
            "PS5 register service sent GETFSGSBASE for the wrong LWP id.");

        await AssertThrowsAsync<NotSupportedException>(
            () => registerService.WriteRegisterAsync(
                0x102,
                new DebuggerRegisterWriteRequest("rax", new byte[8]),
                CancellationToken.None),
            "PS5 register service exposed the currently unverified upstream register-write path.").ConfigureAwait(false);
        AssertEqual(1, server.DebuggerRegisterReadThreadIds.Count,
            "Rejected PS5 register editing unexpectedly generated extra general-register backend traffic.");
        AssertEqual(1, server.DebuggerFloatingPointRegisterReadThreadIds.Count,
            "Rejected PS5 register editing unexpectedly generated extra floating-point backend traffic.");
        AssertEqual(0, server.DebuggerDebugRegisterReadThreadIds.Count,
            "Rejected PS5 register editing unexpectedly generated GETDBREGS traffic.");
        AssertEqual(1, server.DebuggerFsGsBaseReadThreadIds.Count,
            "Rejected PS5 register editing unexpectedly generated extra FS/GS backend traffic.");

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerCallStackServicesAsync()
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

        IDebuggerCallStackService callStack = debugger.GetRequiredService<IDebuggerCallStackService>();
        await AssertThrowsAsync<InvalidOperationException>(
            () => callStack.GetCallStackAsync(0x102, CancellationToken.None),
            "PS5 debugger exposed call frames while the target was running.").ConfigureAwait(false);

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        IReadOnlyList<DebuggerStackFrame> frames = await callStack
            .GetCallStackAsync(0x102, CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(2, frames.Count,
            "PS5 server-side call-stack response was parsed with the wrong frame count.");
        AssertEqual(0x0000000000420102UL, frames[0].InstructionAddress,
            "PS5 top call frame did not use the selected thread's paused RIP.");
        AssertEqual<ulong?>(0x7000000000000102UL, frames[0].StackPointer,
            "PS5 top call frame parsed the wrong stack pointer.");
        AssertEqual<ulong?>(0x7000000000000202UL, frames[0].FramePointer,
            "PS5 top call frame parsed the wrong frame pointer.");
        AssertEqual<ulong?>(0x420202UL, frames[0].ReturnAddress,
            "PS5 top call frame parsed the wrong return address.");
        AssertEqual(0x420202UL, frames[1].InstructionAddress,
            "PS5 caller frame did not use the preceding frame's return address.");
        AssertEqual<ulong?>(0x420302UL, frames[1].ReturnAddress,
            "PS5 caller frame parsed the wrong return address.");

        AssertEqual(1, server.DebuggerStackRequests.Count,
            "PS5 call-stack service sent an unexpected number of CMD_PROC_READ_STACK requests.");
        (uint ProcessId, ulong FramePointer, ulong StackPointer, uint Depth) request = server.DebuggerStackRequests[0];
        AssertEqual((uint)2222, request.ProcessId,
            "PS5 call-stack request used the wrong process id.");
        AssertEqual(0x7000000000000202UL, request.FramePointer,
            "PS5 call-stack request sent the wrong RBP value.");
        AssertEqual(0x7000000000000102UL, request.StackPointer,
            "PS5 call-stack request sent the wrong RSP value.");
        AssertEqual((uint)64, request.Depth,
            "PS5 call-stack request did not use the backend's bounded maximum depth.");

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerStepServicesAsync()
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

        IDebuggerStepService stepping = debugger.GetRequiredService<IDebuggerStepService>();
        await AssertThrowsAsync<InvalidOperationException>(
            () => stepping.StepAsync(DebuggerStepKind.Into, 0x102, CancellationToken.None),
            "PS5 debugger accepted Step Into while the target was running.").ConfigureAwait(false);

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        TaskCompletionSource<DebuggerEvent> completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.StepCompleted)
            {
                completed.TrySetResult(args.Event);
            }
        };

        await stepping.StepAsync(DebuggerStepKind.Into, 0x102, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Running, debugger.State,
            "PS5 Step Into did not immediately transition the neutral debugger state to Running.");
        AssertTrue(server.DebuggerThreadActions.Contains("step:0x102", StringComparer.Ordinal),
            "PS5 Step Into did not send CMD_DEBUG_STEP_THREAD for the selected thread.");

        const ulong steppedInstructionPointer = 0x0000000000420103UL;
        await server.SendDebuggerInterruptAsync(
            0x102,
            5u << 8,
            steppedInstructionPointer,
            "Worker").ConfigureAwait(false);

        DebuggerEvent stepEvent = await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Paused, debugger.State,
            "PS5 Step Into completion did not return the neutral debugger state to Paused.");
        AssertEqual(DebuggerStopReason.StepCompleted, stepEvent.StopReason,
            "PS5 native step interrupt was not classified as StepCompleted.");
        AssertEqual<ulong?>(0x102, stepEvent.ThreadId,
            "PS5 native step completion was attributed to the wrong thread.");
        AssertEqual<ulong?>(steppedInstructionPointer, stepEvent.InstructionPointer,
            "PS5 native step completion reported the wrong RIP.");

        await AssertThrowsAsync<NotSupportedException>(
            () => stepping.StepAsync(DebuggerStepKind.Over, 0x102, CancellationToken.None),
            "PS5 backend unexpectedly implemented native Step Over instead of host composition.").ConfigureAwait(false);
        await AssertThrowsAsync<NotSupportedException>(
            () => stepping.StepAsync(DebuggerStepKind.Out, 0x102, CancellationToken.None),
            "PS5 backend unexpectedly implemented native Step Out instead of host composition.").ConfigureAwait(false);

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerExtendedRegisterCapabilityGatingAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveDebugger: true,
            debuggerCapabilityLevel: null);
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

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        IDebuggerRegisterService registerService = debugger.GetRequiredService<IDebuggerRegisterService>();
        IReadOnlyList<DebuggerRegister> registers = await registerService
            .GetRegistersAsync(0x102, CancellationToken.None)
            .ConfigureAwait(false);

        AssertEqual(26, registers.Count,
            "PS5 debugger did not fall back to the verified general-register snapshot when the backend omitted the extended-register capability level.");
        AssertTrue(registers.Any(register => register.Role == DebuggerRegisterRole.InstructionPointer),
            "PS5 capability gating removed the verified semantic instruction-pointer register.");
        AssertFalse(registers.Any(register => register.Id is "fcw" or "xmm0" or "ymm0" or "dr7" or "fsbase"),
            "PS5 capability gating exposed extended rows without backend capability advertisement.");
        AssertEqual(0, server.DebuggerFloatingPointRegisterReadThreadIds.Count,
            "PS5 capability gating still sent GETFPREGS without backend capability advertisement.");
        AssertEqual(0, server.DebuggerDebugRegisterReadThreadIds.Count,
            "PS5 capability gating still sent GETDBREGS without backend capability advertisement.");
        AssertEqual(0, server.DebuggerFsGsBaseReadThreadIds.Count,
            "PS5 capability gating still sent GETFSGSBASE without backend capability advertisement.");

        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerOptionalRegisterTimeoutIsolationAsync()
    {
        await using Ps5ProtocolTestServer server = new(
            serveDebugger: true,
            silenceDebuggerFloatingPointRegisterRead: true);
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

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        IDebuggerRegisterService registerService = debugger.GetRequiredService<IDebuggerRegisterService>();
        IReadOnlyList<DebuggerRegister> registers = await registerService
            .GetRegistersAsync(0x102, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(6))
            .ConfigureAwait(false);

        AssertEqual(28, registers.Count,
            "PS5 debugger did not return general plus the surviving FS/GS register group after a silent GETFPREGS probe timeout.");
        AssertTrue(registers.Any(register => register.Role == DebuggerRegisterRole.InstructionPointer),
            "PS5 optional-register timeout fallback lost the verified general-register stop context.");
        AssertFalse(registers.Any(register => register.Id is "fcw" or "st0" or "xmm0" or "ymm0"),
            "PS5 optional-register timeout fallback retained the unavailable FPU/SIMD block.");
        AssertFalse(registers.Any(register => register.Id.StartsWith("dr", StringComparison.Ordinal)),
            "PS5 optional-register timeout fallback exposed debug registers even though paused GETDBREGS is intentionally suppressed.");
        AssertTrue(registers.Any(register => register.Id == "fsbase"),
            "PS5 optional-register timeout fallback lost a succeeding FS/GS-base probe.");
        AssertEqual(1, server.DebuggerFloatingPointRegisterReadThreadIds.Count,
            "PS5 optional-register timeout test did not observe exactly one GETFPREGS probe.");
        AssertEqual(0, server.DebuggerDebugRegisterReadThreadIds.Count,
            "PS5 optional-register timeout path sent GETDBREGS even though the target was already paused.");
        AssertEqual(1, server.DebuggerFsGsBaseReadThreadIds.Count,
            "PS5 optional-register timeout test did not observe the independent GETFSGSBASE probe.");

        registers = await registerService
            .GetRegistersAsync(0x102, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(6))
            .ConfigureAwait(false);
        AssertEqual(28, registers.Count,
            "PS5 cached optional-register fallback changed shape on the second paused refresh.");
        AssertEqual(1, server.DebuggerFloatingPointRegisterReadThreadIds.Count,
            "PS5 debugger retried a timed-out optional register command in the same attached session.");
        AssertEqual(0, server.DebuggerDebugRegisterReadThreadIds.Count,
            "PS5 debugger sent GETDBREGS on a later paused refresh after the backend-safety suppression should have remained in effect.");
        AssertEqual(2, server.DebuggerFsGsBaseReadThreadIds.Count,
            "PS5 debugger stopped refreshing a healthy FS/GS-base group after another group timed out.");

        await debugger.ContinueAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Running, debugger.State,
            "PS5 debugger owner transport did not remain usable after an isolated optional-register timeout.");
        await debugger.DetachAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5))
            .ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerBreakpointServicesAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveMemoryMap: true, serveDebugger: true);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = new(2222, "eboot.bin");
        _ = await targetSession
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        await using IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        IDebuggerBreakpointService breakpoints = debugger.GetRequiredService<IDebuggerBreakpointService>();
        IDebuggerBreakpointValidationService validation =
            debugger.GetRequiredService<IDebuggerBreakpointValidationService>();
        IDebuggerBreakpointStateService states = debugger.GetRequiredService<IDebuggerBreakpointStateService>();

        AssertFalse(validation.ValidateBreakpointRequest(new DebuggerBreakpointRequest(
                0x0000000200000100,
                1,
                DebuggerBreakpointKind.Software,
                DebuggerBreakpointAccess.Execute)).IsValid,
            "PS5 validator accepted a software execute breakpoint in a mapped non-executable region.");
        AssertTrue(validation.ValidateBreakpointRequest(new DebuggerBreakpointRequest(
                0x0000000100001000,
                1,
                DebuggerBreakpointKind.Software,
                DebuggerBreakpointAccess.Execute)).IsValid,
            "PS5 validator rejected a legal software execute breakpoint.");

        await AssertThrowsAsync<InvalidOperationException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(0x0000000200000100, 1, DebuggerBreakpointKind.Software, DebuggerBreakpointAccess.Execute),
                CancellationToken.None),
            "PS5 debugger accepted a software execute breakpoint in a mapped non-executable region.").ConfigureAwait(false);
        await AssertThrowsAsync<InvalidOperationException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(0x80000104, 1, DebuggerBreakpointKind.Software, DebuggerBreakpointAccess.Execute),
                CancellationToken.None),
            "PS5 debugger accepted a software execute breakpoint outside the current target memory map.").ConfigureAwait(false);
        AssertEqual(0, server.DebuggerBreakpointActions.Count,
            "PS5 rejected breakpoint validation mutated backend breakpoint state.");

        DebuggerBreakpoint persistent = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(0x0000000100001000, 1, DebuggerBreakpointKind.Software, DebuggerBreakpointAccess.Execute),
            CancellationToken.None).ConfigureAwait(false);
        AssertEqual(1, server.DebuggerBreakpointActions.Count, "PS5 breakpoint add did not issue exactly one backend request.");
        AssertTrue(server.DebuggerBreakpointActions[0].Enabled, "PS5 breakpoint add did not enable the backend slot.");
        AssertEqual(0x0000000100001000UL, server.DebuggerBreakpointActions[0].Address, "PS5 breakpoint add sent the wrong address.");
        AssertFalse(validation.ValidateBreakpointRequest(persistent.Request).IsValid,
            "PS5 validator accepted a duplicate active software breakpoint.");

        await states.SetBreakpointEnabledAsync(persistent.Id, false, CancellationToken.None).ConfigureAwait(false);
        await states.SetBreakpointEnabledAsync(persistent.Id, true, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(3, server.DebuggerBreakpointActions.Count, "PS5 running breakpoint state changes did not reach the backend.");

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        await states.SetBreakpointEnabledAsync(persistent.Id, false, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(3, server.DebuggerBreakpointActions.Count,
            "PS5 paused breakpoint disable reached the backend immediately and could resume the target unexpectedly.");
        await states.SetBreakpointEnabledAsync(persistent.Id, true, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(3, server.DebuggerBreakpointActions.Count,
            "PS5 paused breakpoint re-enable did not cancel the staged disable locally.");

        DebuggerBreakpoint removedWhilePaused = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(0x0000000100003000, 1, DebuggerBreakpointKind.Software, DebuggerBreakpointAccess.Execute),
            CancellationToken.None).ConfigureAwait(false);
        int actionsBeforePausedRemove = server.DebuggerBreakpointActions.Count;
        await breakpoints.RemoveBreakpointAsync(removedWhilePaused.Id, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(actionsBeforePausedRemove, server.DebuggerBreakpointActions.Count,
            "PS5 paused breakpoint removal reached the backend immediately and could resume the target unexpectedly.");

        DebuggerBreakpoint temporary = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(0x0000000100002000, 1, DebuggerBreakpointKind.Software, DebuggerBreakpointAccess.Execute, isTemporary: true),
            CancellationToken.None).ConfigureAwait(false);
        TaskCompletionSource<DebuggerEvent> hitSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.Breakpoint)
            {
                hitSource.TrySetResult(args.Event);
            }
        };

        await server.SendDebuggerInterruptAsync(0x101, 0x0000057F, temporary.Request.Address, "MainThread").ConfigureAwait(false);
        DebuggerEvent hit = await hitSource.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        AssertEqual(DebuggerStopReason.Breakpoint, hit.StopReason, "PS5 interrupt at a managed software breakpoint was not classified as a breakpoint hit.");
        AssertEqual<ulong?>(temporary.Request.Address, hit.InstructionPointer, "PS5 breakpoint event lost the corrected breakpoint address.");
        AssertEqual(1, (await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false)).Count,
            "PS5 temporary breakpoint remained visible after its first hit.");

        int actionsBeforeContinue = server.DebuggerBreakpointActions.Count;
        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(actionsBeforeContinue + 2, server.DebuggerBreakpointActions.Count,
            "PS5 continue did not flush both staged paused-removal and temporary-breakpoint cleanups.");
        (uint Slot, bool Enabled, ulong Address)[] flushed = server.DebuggerBreakpointActions
            .Skip(actionsBeforeContinue)
            .ToArray();
        AssertTrue(flushed.All(action => !action.Enabled),
            "PS5 staged breakpoint cleanup unexpectedly enabled a backend slot.");
        AssertTrue(flushed.Any(action => action.Address == removedWhilePaused.Request.Address),
            "PS5 continue did not flush the breakpoint removed while paused.");
        AssertTrue(flushed.Any(action => action.Address == temporary.Request.Address),
            "PS5 continue did not flush temporary-breakpoint cleanup.");

        await breakpoints.RemoveBreakpointAsync(persistent.Id, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(0, (await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false)).Count,
            "PS5 persistent breakpoint remained after removal.");

        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerLogicalSoftwareBreakpointStopContextAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveMemoryMap: true, serveDebugger: true);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = new(2222, "eboot.bin");
        _ = await targetSession
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        await using IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);

        IDebuggerBreakpointService breakpoints = debugger.GetRequiredService<IDebuggerBreakpointService>();
        IDebuggerRegisterService registers = debugger.GetRequiredService<IDebuggerRegisterService>();
        IDebuggerCallStackService callStack = debugger.GetRequiredService<IDebuggerCallStackService>();
        IDebuggerStepService stepping = debugger.GetRequiredService<IDebuggerStepService>();

        const uint threadId = 0x101;
        const ulong breakpointAddress = 0x0000000100002000UL;
        DebuggerBreakpoint breakpoint = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                breakpointAddress,
                1,
                DebuggerBreakpointKind.Software,
                DebuggerBreakpointAccess.Execute),
            CancellationToken.None).ConfigureAwait(false);

        TaskCompletionSource<DebuggerEvent> hitSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.Breakpoint)
            {
                hitSource.TrySetResult(args.Event);
            }
        };

        await server.SendDebuggerInterruptAsync(
            threadId,
            0x0000057F,
            breakpointAddress,
            "MainThread").ConfigureAwait(false);
        DebuggerEvent hit = await hitSource.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        AssertEqual<ulong?>(breakpointAddress, hit.InstructionPointer,
            "PS5 logical software-breakpoint event did not retain the backend interrupt snapshot address.");

        int generalReadsBeforeSnapshot = server.DebuggerRegisterReadThreadIds.Count;
        IReadOnlyList<DebuggerRegister> stopRegisters = await registers
            .GetRegistersAsync(threadId, CancellationToken.None)
            .ConfigureAwait(false);
        DebuggerRegister stopRip = stopRegisters.Single(item => item.Id == "rip");
        ulong stopInstructionPointer = BinaryPrimitives.ReadUInt64LittleEndian(stopRip.Value.Span);
        AssertEqual(breakpointAddress, stopInstructionPointer,
            "PS5 register inspection did not use the software-breakpoint interrupt snapshot as the logical stop context.");
        AssertEqual(generalReadsBeforeSnapshot, server.DebuggerRegisterReadThreadIds.Count,
            "PS5 logical software-breakpoint register inspection unexpectedly replaced the interrupt snapshot with live post-step registers.");

        IReadOnlyList<DebuggerStackFrame> stopFrames = await callStack
            .GetCallStackAsync(threadId, CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(breakpointAddress, stopFrames[0].InstructionAddress,
            "PS5 call-stack frame zero did not use the logical software-breakpoint stop address.");

        int nativeStepActionsBefore = server.DebuggerThreadActions.Count(action =>
            string.Equals(action, $"step:0x{threadId:X}", StringComparison.Ordinal));
        TaskCompletionSource<DebuggerEvent> stepCompletedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.StepCompleted)
            {
                stepCompletedSource.TrySetResult(args.Event);
            }
        };

        await stepping.StepAsync(DebuggerStepKind.Into, threadId, CancellationToken.None).ConfigureAwait(false);
        DebuggerEvent stepCompleted = await stepCompletedSource.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Paused, debugger.State,
            "PS5 logical software-breakpoint Step Into did not finish in Paused state.");
        AssertEqual(nativeStepActionsBefore, server.DebuggerThreadActions.Count(action =>
                string.Equals(action, $"step:0x{threadId:X}", StringComparison.Ordinal)),
            "PS5 logical software-breakpoint Step Into issued a second native step after the backend had already transparently stepped the breakpointed instruction.");
        AssertEqual<ulong?>(0x0000000000420101UL, stepCompleted.InstructionPointer,
            "PS5 logical software-breakpoint Step Into did not report the backend's live post-step RIP.");

        IReadOnlyList<DebuggerRegister> postStepRegisters = await registers
            .GetRegistersAsync(threadId, CancellationToken.None)
            .ConfigureAwait(false);
        ulong postStepInstructionPointer = BinaryPrimitives.ReadUInt64LittleEndian(
            postStepRegisters.Single(item => item.Id == "rip").Value.Span);
        AssertEqual(0x0000000000420101UL, postStepInstructionPointer,
            "PS5 software-breakpoint snapshot was not consumed after the logical Step Into completed.");

        await breakpoints.RemoveBreakpointAsync(breakpoint.Id, CancellationToken.None).ConfigureAwait(false);
        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerSafeDetachBreakpointCleanupAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveMemoryMap: true, serveDebugger: true);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = new(2222, "eboot.bin");
        _ = await targetSession
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        IDebuggerBreakpointService breakpoints = debugger.GetRequiredService<IDebuggerBreakpointService>();

        DebuggerBreakpoint active = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                0x0000000100001000,
                1,
                DebuggerBreakpointKind.Software,
                DebuggerBreakpointAccess.Execute),
            CancellationToken.None).ConfigureAwait(false);
        DebuggerBreakpoint pending = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                0x0000000100002000,
                1,
                DebuggerBreakpointKind.Software,
                DebuggerBreakpointAccess.Execute,
                isTemporary: true),
            CancellationToken.None).ConfigureAwait(false);

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        await breakpoints.RemoveBreakpointAsync(pending.Id, CancellationToken.None).ConfigureAwait(false);
        int actionCountBeforeDetach = server.DebuggerBreakpointActions.Count;
        AssertEqual(2, actionCountBeforeDetach,
            "PS5 paused temporary-breakpoint removal reached the backend before safe detach.");

        await debugger.DetachAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Detached, debugger.State,
            "PS5 safe detach did not finish in Detached state.");

        (uint Slot, bool Enabled, ulong Address)[] detachCleanup = server.DebuggerBreakpointActions
            .Skip(actionCountBeforeDetach)
            .ToArray();
        AssertEqual(2, detachCleanup.Length,
            "PS5 safe detach did not explicitly restore every active or staged software-breakpoint slot before backend teardown.");
        AssertTrue(detachCleanup.All(action => !action.Enabled),
            "PS5 safe detach sent an enabling breakpoint action during teardown cleanup.");
        AssertTrue(detachCleanup.Any(action => action.Address == active.Request.Address),
            "PS5 safe detach did not restore the still-active software breakpoint before backend teardown.");
        AssertTrue(detachCleanup.Any(action => action.Address == pending.Request.Address),
            "PS5 safe detach did not flush the staged paused-removal breakpoint before backend teardown.");

        await debugger.DisposeAsync().ConfigureAwait(false);
        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerSafeDetachWatchpointCleanupAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveMemoryMap: true, serveDebugger: true);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = new(2222, "eboot.bin");
        _ = await targetSession
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        IDebuggerBreakpointService breakpoints = debugger.GetRequiredService<IDebuggerBreakpointService>();

        DebuggerBreakpoint active = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                0x0000000200000100,
                4,
                DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.Write),
            CancellationToken.None).ConfigureAwait(false);
        DebuggerBreakpoint temporary = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                0x0000000200000108,
                8,
                DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.ReadWrite,
                isTemporary: true),
            CancellationToken.None).ConfigureAwait(false);

        (uint Slot, bool Enabled, uint Length, uint BreakType, ulong Address) temporaryAction =
            server.DebuggerWatchpointActions[^1];
        TaskCompletionSource<DebuggerEvent> hitSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.Watchpoint &&
                args.Event.TriggeredBreakpoint?.Id == temporary.Id)
            {
                hitSource.TrySetResult(args.Event);
            }
        };

        await server.SendDebuggerInterruptAsync(
            0x101,
            0x0000057F,
            0x0000000100001820,
            "MainThread",
            debugStatus: 1UL << checked((int)temporaryAction.Slot)).ConfigureAwait(false);
        _ = await hitSource.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        AssertTrue((await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false))
                .Any(item => item.Id == active.Id),
            "PS5 active persistent hardware watchpoint disappeared before disposal cleanup.");
        AssertFalse((await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false))
                .Any(item => item.Id == temporary.Id),
            "PS5 temporary hardware watchpoint remained visible after its triggering hit.");

        int actionCountBeforeDispose = server.DebuggerWatchpointActions.Count;
        AssertEqual(2, actionCountBeforeDispose,
            "PS5 watchpoint setup emitted an unexpected number of backend actions before disposal cleanup.");

        await debugger.DisposeAsync().ConfigureAwait(false);
        AssertEqual(DebuggerExecutionState.Detached, debugger.State,
            "PS5 debugger disposal did not finish in Detached state.");

        (uint Slot, bool Enabled, uint Length, uint BreakType, ulong Address)[] disposeCleanup =
            server.DebuggerWatchpointActions
                .Skip(actionCountBeforeDispose)
                .ToArray();
        AssertEqual(2, disposeCleanup.Length,
            "PS5 debugger disposal did not explicitly clear every active or staged hardware-watchpoint slot before backend teardown.");
        AssertTrue(disposeCleanup.All(action => !action.Enabled),
            "PS5 debugger disposal sent an enabling hardware-watchpoint action during teardown cleanup.");
        AssertTrue(disposeCleanup.Any(action => action.Address == active.Request.Address),
            "PS5 debugger disposal did not clear the still-active hardware watchpoint before backend teardown.");
        AssertTrue(disposeCleanup.Any(action => action.Address == temporary.Request.Address),
            "PS5 debugger disposal did not flush the staged temporary hardware-watchpoint cleanup before backend teardown.");

        await server.Completion.ConfigureAwait(false);
    }

    private static async Task VerifyPs5DebuggerWatchpointServicesAsync()
    {
        await using Ps5ProtocolTestServer server = new(serveMemoryMap: true, serveDebugger: true);
        Ps5TargetPlugin plugin = new();
        TargetConnectionOptions options = new(new[]
        {
            new KeyValuePair<string, string>("host", "127.0.0.1"),
            new KeyValuePair<string, string>("port", server.Port.ToString(CultureInfo.InvariantCulture))
        });

        await using ITargetSession targetSession = await plugin
            .ConnectAsync(options, CancellationToken.None)
            .ConfigureAwait(false);
        TargetProcess process = new(2222, "eboot.bin");
        _ = await targetSession
            .GetRequiredService<IMemoryMapProvider>()
            .GetMemoryRegionsAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        await using IDebuggerSession debugger = await targetSession
            .GetRequiredService<IDebuggerProvider>()
            .AttachAsync(process, CancellationToken.None)
            .ConfigureAwait(false);
        IDebuggerBreakpointService breakpoints = debugger.GetRequiredService<IDebuggerBreakpointService>();
        IDebuggerBreakpointStateService states = debugger.GetRequiredService<IDebuggerBreakpointStateService>();

        await AssertThrowsAsync<NotSupportedException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(
                    0x0000000200000100,
                    4,
                    DebuggerBreakpointKind.Hardware,
                    DebuggerBreakpointAccess.Read),
                CancellationToken.None),
            "PS5 debugger accepted a read-only hardware watchpoint even though the backend cannot encode it distinctly.").ConfigureAwait(false);
        await AssertThrowsAsync<NotSupportedException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(
                    0x0000000200000100,
                    3,
                    DebuggerBreakpointKind.Hardware,
                    DebuggerBreakpointAccess.Write),
                CancellationToken.None),
            "PS5 debugger accepted an unsupported three-byte hardware watchpoint.").ConfigureAwait(false);
        await AssertThrowsAsync<InvalidOperationException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(
                    0x0000000200000102,
                    4,
                    DebuggerBreakpointKind.Hardware,
                    DebuggerBreakpointAccess.Write),
                CancellationToken.None),
            "PS5 debugger accepted a misaligned four-byte hardware watchpoint.").ConfigureAwait(false);
        await AssertThrowsAsync<InvalidOperationException>(
            () => breakpoints.AddBreakpointAsync(
                new DebuggerBreakpointRequest(
                    0x80000100,
                    4,
                    DebuggerBreakpointKind.Hardware,
                    DebuggerBreakpointAccess.Write),
                CancellationToken.None),
            "PS5 debugger accepted a hardware watchpoint outside the current target memory map.").ConfigureAwait(false);
        AssertEqual(0, server.DebuggerWatchpointActions.Count,
            "Rejected PS5 hardware-watchpoint validation mutated backend debug-register state.");

        DebuggerBreakpoint persistent = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                0x0000000200000100,
                4,
                DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.Write),
            CancellationToken.None).ConfigureAwait(false);
        AssertEqual(1, server.DebuggerWatchpointActions.Count, "PS5 hardware watchpoint add did not issue exactly one backend request.");
        (uint Slot, bool Enabled, uint Length, uint BreakType, ulong Address) first = server.DebuggerWatchpointActions[0];
        AssertEqual(0u, first.Slot, "PS5 first hardware watchpoint did not use the first DR slot.");
        AssertTrue(first.Enabled, "PS5 hardware watchpoint add did not enable the backend slot.");
        AssertEqual(3u, first.Length, "PS5 four-byte hardware watchpoint used the wrong DR7 length encoding.");
        AssertEqual(1u, first.BreakType, "PS5 write hardware watchpoint used the wrong DR7 access encoding.");
        AssertEqual(persistent.Request.Address, first.Address, "PS5 hardware watchpoint add sent the wrong address.");

        await debugger.PauseAsync(CancellationToken.None).ConfigureAwait(false);
        await states.SetBreakpointEnabledAsync(persistent.Id, false, CancellationToken.None).ConfigureAwait(false);
        await states.SetBreakpointEnabledAsync(persistent.Id, true, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(3, server.DebuggerWatchpointActions.Count,
            "PS5 paused hardware-watchpoint Disable/Enable did not update the backend directly.");
        AssertEqual(DebuggerExecutionState.Paused, debugger.State,
            "PS5 hardware-watchpoint state changes unexpectedly changed the debugger execution state.");

        DebuggerBreakpoint temporary = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                0x0000000200000108,
                8,
                DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.ReadWrite,
                isTemporary: true),
            CancellationToken.None).ConfigureAwait(false);
        (uint Slot, bool Enabled, uint Length, uint BreakType, ulong Address) temporaryAction = server.DebuggerWatchpointActions[^1];
        AssertEqual(2u, temporaryAction.Length, "PS5 eight-byte hardware watchpoint used the wrong DR7 length encoding.");
        AssertEqual(3u, temporaryAction.BreakType, "PS5 read/write hardware watchpoint used the wrong DR7 access encoding.");

        TaskCompletionSource<DebuggerEvent> hitSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.Watchpoint &&
                args.Event.TriggeredBreakpoint?.Id == temporary.Id)
            {
                hitSource.TrySetResult(args.Event);
            }
        };

        const ulong accessingInstruction = 0x0000000100001800;
        await server.SendDebuggerInterruptAsync(
            0x101,
            0x0000057F,
            accessingInstruction,
            "MainThread",
            debugStatus: 1UL << checked((int)temporaryAction.Slot)).ConfigureAwait(false);
        DebuggerEvent hit = await hitSource.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        AssertEqual(DebuggerStopReason.Watchpoint, hit.StopReason, "PS5 DR6 watchpoint hit was not classified as a watchpoint stop.");
        AssertEqual<ulong?>(accessingInstruction, hit.InstructionPointer,
            "PS5 watchpoint event replaced the accessing instruction pointer with the watched data address.");
        AssertEqual(temporary.Request.Address, hit.TriggeredBreakpoint!.Request.Address,
            "PS5 watchpoint event lost the watched data address.");
        AssertEqual(DebuggerBreakpointAccess.ReadWrite, hit.TriggeredBreakpoint.Request.Access,
            "PS5 watchpoint event lost the triggered access metadata.");
        AssertEqual(1, (await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false)).Count,
            "PS5 temporary hardware watchpoint remained visible after its first hit.");

        int actionsBeforeContinue = server.DebuggerWatchpointActions.Count;
        await debugger.ContinueAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(actionsBeforeContinue + 1, server.DebuggerWatchpointActions.Count,
            "PS5 Continue did not flush temporary hardware-watchpoint cleanup before resume.");
        (uint Slot, bool Enabled, uint Length, uint BreakType, ulong Address) flushed = server.DebuggerWatchpointActions[^1];
        AssertFalse(flushed.Enabled, "PS5 temporary hardware-watchpoint cleanup unexpectedly enabled its backend slot.");
        AssertEqual(temporaryAction.Slot, flushed.Slot, "PS5 temporary hardware-watchpoint cleanup targeted the wrong backend slot.");

        TaskCompletionSource<DebuggerEvent> inferredHitSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.Watchpoint &&
                args.Event.TriggeredBreakpoint?.Id == persistent.Id &&
                args.Event.Message?.Contains("did not preserve DR6", StringComparison.Ordinal) == true)
            {
                inferredHitSource.TrySetResult(args.Event);
            }
        };

        const ulong inferredInstruction = 0x0000000100001810;
        await server.SendDebuggerInterruptAsync(
            0x101,
            0x0000057F,
            inferredInstruction,
            "MainThread",
            debugStatus: 0).ConfigureAwait(false);
        DebuggerEvent inferredHit = await inferredHitSource.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        AssertEqual(DebuggerStopReason.Watchpoint, inferredHit.StopReason,
            "PS5 single-watchpoint DR6 fallback did not retain the neutral watchpoint stop reason.");
        AssertEqual<ulong?>(inferredInstruction, inferredHit.InstructionPointer,
            "PS5 single-watchpoint DR6 fallback lost the accessing instruction pointer.");

        DebuggerBreakpoint ambiguousTemporary = await breakpoints.AddBreakpointAsync(
            new DebuggerBreakpointRequest(
                0x0000000200000110,
                4,
                DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.Write,
                isTemporary: true),
            CancellationToken.None).ConfigureAwait(false);
        TaskCompletionSource<DebuggerEvent> ambiguousStopSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        debugger.EventReceived += (_, args) =>
        {
            if (args.Event.Kind == DebuggerEventKind.Other &&
                args.Event.StopReason == DebuggerStopReason.Signal &&
                args.Event.Message?.Contains("exact watchpoint cannot be attributed safely", StringComparison.Ordinal) == true)
            {
                ambiguousStopSource.TrySetResult(args.Event);
            }
        };

        await server.SendDebuggerInterruptAsync(
            0x101,
            0x0000057F,
            0x0000000100001820,
            "MainThread",
            debugStatus: 0).ConfigureAwait(false);
        DebuggerEvent ambiguousStop = await ambiguousStopSource.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        AssertTrue(ambiguousStop.TriggeredBreakpoint is null,
            "PS5 zero-DR6 multi-watchpoint stop guessed a triggered watchpoint without backend slot status.");
        AssertTrue((await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false))
                .Any(item => item.Id == ambiguousTemporary.Id),
            "PS5 zero-DR6 multi-watchpoint stop removed a temporary watchpoint without knowing which slot triggered.");

        await breakpoints.RemoveBreakpointAsync(ambiguousTemporary.Id, CancellationToken.None).ConfigureAwait(false);
        await breakpoints.RemoveBreakpointAsync(persistent.Id, CancellationToken.None).ConfigureAwait(false);
        AssertEqual(0, (await breakpoints.GetBreakpointsAsync(CancellationToken.None).ConfigureAwait(false)).Count,
            "PS5 persistent hardware watchpoint remained after removal.");
        AssertEqual(0, server.DebuggerDebugRegisterReadThreadIds.Count,
            "PS5 hardware-watchpoint hit mapping reintroduced the unsafe paused GETDBREGS command path.");

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
        string disassemblerButton = secondRow[secondRow.LastIndexOf("<Button", disassemblerIndex, StringComparison.Ordinal)..disassemblerIndex];
        string debuggerButton = secondRow[secondRow.LastIndexOf("<Button", debuggerIndex, StringComparison.Ordinal)..debuggerIndex];
        AssertTrue(disassemblerButton.Contains("Style=\"{StaticResource PrimaryButtonStyle}\"", StringComparison.Ordinal) &&
                   debuggerButton.Contains("Style=\"{StaticResource PrimaryButtonStyle}\"", StringComparison.Ordinal),
            "Disassembler and Debugger must use the theme-driven primary action style used by the active First Scan action.");

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

    private static Task VerifyMainStatusBarContentAlignmentAsync()
    {
        string xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        int statusBarStart = xaml.IndexOf("<Border Grid.Row=\"3\"", StringComparison.Ordinal);
        AssertTrue(statusBarStart >= 0, "Main-window status bar could not be located.");
        string statusBar = xaml[statusBarStart..];

        AssertContains(statusBar, "<Border Grid.Column=\"0\"\n                        Padding=\"8,2\"\n                        VerticalAlignment=\"Center\"",
            "Connection state is not centered on the status-bar row.");
        AssertContains(statusBar, "Text=\"{Binding StatusText}\"\n                           VerticalAlignment=\"Center\"",
            "Main status text is not centered on the status-bar row.");
        AssertContains(statusBar, "Text=\"{Binding ErrorText}\"\n                           VerticalAlignment=\"Center\"",
            "Main error text is not centered on the status-bar row.");
        AssertContains(statusBar, "Text=\"{Binding SelectedPlugin.ScanStatusBarText}\"\n                           VerticalAlignment=\"Center\"",
            "Scan status text is not centered on the status-bar row.");
        AssertContains(statusBar, "Grid.Column=\"8\"\n                            Orientation=\"Horizontal\"\n                            VerticalAlignment=\"Center\"",
            "Scan progress group is not centered on the status-bar row.");
        AssertContains(statusBar, "Text=\"{Binding DisplayVersion}\"\n                           VerticalAlignment=\"Center\"",
            "Application version is not centered on the status-bar row.");

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

        IDisassemblyWatchpointResolver watchpointResolver = session.GetRequiredService<IDisassemblyWatchpointResolver>();
        IReadOnlyList<DisassembledInstruction> watchpointInstructions = await provider
            .DisassembleAsync(
                0x8033D981,
                new byte[] { 0x01, 0x70, 0x40 },
                session.Architecture,
                CancellationToken.None)
            .ConfigureAwait(false);
        DebuggerRegister rax = new(
            "rax",
            "RAX",
            64,
            BitConverter.GetBytes(0x00000002394E4000UL),
            "General",
            DebuggerRegisterRole.None,
            canWrite: false,
            DebuggerRegisterValueEncoding.UnsignedLittleEndian);
        DisassemblyWatchpointTarget? watchpointTarget = watchpointResolver.ResolveWatchpointTarget(
            watchpointInstructions.Single(),
            new[] { rax });
        AssertTrue(watchpointTarget is not null,
            "PS5 disassembly watchpoint resolver did not resolve a simple register+displacement memory write.");
        AssertEqual(0x00000002394E4040UL, watchpointTarget!.Address,
            "PS5 disassembly watchpoint resolver produced the wrong effective memory address.");
        AssertEqual(4, watchpointTarget.Size,
            "PS5 disassembly watchpoint resolver produced the wrong memory-access width.");
        AssertEqual(DebuggerBreakpointAccess.ReadWrite, watchpointTarget.Access,
            "PS5 disassembly watchpoint resolver produced the wrong watchpoint access mode for a read-modify-write instruction.");

        IReadOnlyList<DisassembledInstruction> leaInstructions = await provider
            .DisassembleAsync(
                0x8033D990,
                new byte[] { 0x48, 0x8D, 0x48, 0x40 },
                session.Architecture,
                CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual<DisassemblyWatchpointTarget?>(null,
            watchpointResolver.ResolveWatchpointTarget(leaInstructions.Single(), new[] { rax }),
            "PS5 disassembly watchpoint resolver treated LEA as a real memory access.");

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

    private static Task VerifyDebuggerSnapshotCaptureModelAsync()
    {
        DebuggerSnapshotCaptureService service = new();
        DebuggerSnapshotCaptureRequest request = CreateSnapshotRequest(
            label: "Player hit 1",
            group: "Player",
            moduleBase: 0x10000000,
            registerValue: 1,
            uniqueFrameOffset: 0x300);
        DebuggerSnapshot snapshot = service.Capture(request);

        AssertTrue(snapshot.SnapshotId != Guid.Empty, "Snapshot capture did not generate a stable snapshot id.");
        AssertEqual("Player hit 1", snapshot.Label, "Snapshot label was not preserved.");
        AssertEqual("Player", snapshot.Group, "Snapshot group was not preserved.");
        AssertEqual(DebuggerSnapshotCaptureSource.Live, snapshot.CaptureMode, "Live capture source was not preserved.");
        AssertEqual((ulong)0x10000020, snapshot.Event.InstructionPointer!.Value, "Snapshot stop/current IP changed during composition.");
        AssertEqual((ulong)0x10000010, snapshot.Event.TriggerInstructionAddress!.Value, "Snapshot trigger IP changed during composition.");
        AssertEqual(DebuggerTriggerResolution.DisassemblyDerived, snapshot.Event.TriggerResolution, "Snapshot trigger-resolution provenance was not preserved.");
        AssertEqual("Mock Main", snapshot.Event.ThreadName, "Snapshot stopped-thread name was not preserved.");
        AssertEqual(DebuggerSnapshotBreakpointType.Watchpoint, snapshot.Breakpoints[0].Type, "Snapshot breakpoint/watchpoint semantic type was not preserved.");
        AssertEqual(DebuggerBreakpointKind.Hardware, snapshot.Breakpoints[0].Mechanism, "Snapshot breakpoint/watchpoint mechanism was not preserved.");
        AssertEqual(3, snapshot.CallStack.Count, "Snapshot call stack did not preserve every frame.");
        AssertEqual((ulong)0x100, snapshot.CallStack[1].ModuleOffset!.Value, "Module-relative frame offset was not preserved.");
        AssertEqual("0100000000000000", snapshot.Registers[0].RawBytes, "Snapshot register raw bytes changed.");

        DebuggerSnapshot renamed = snapshot.WithMetadata(label: "Renamed", group: "Enemy");
        AssertEqual("Player hit 1", snapshot.Label, "Updating snapshot metadata mutated the original immutable snapshot.");
        AssertEqual("Renamed", renamed.Label, "Snapshot metadata copy did not update the label.");
        AssertEqual("Enemy", renamed.Group, "Snapshot metadata copy did not update the group.");

        string[] mutableMarkers = ["Original marker"];
        DebuggerSnapshotCaptureRequest mutableRequest = request with
        {
            Disassembly =
            [
                request.Disassembly[0] with { Markers = mutableMarkers },
                request.Disassembly[1]
            ]
        };
        DebuggerSnapshot frozen = service.Capture(mutableRequest);
        mutableMarkers[0] = "Mutated after capture";
        AssertEqual("Original marker", frozen.Disassembly[0].Markers[0],
            "Snapshot capture retained a mutable instruction-marker collection.");
        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerSnapshotJsonRoundTripAsync()
    {
        DebuggerSnapshot snapshot = new DebuggerSnapshotCaptureService().Capture(CreateSnapshotRequest(
            label: "Round trip",
            group: "Player",
            moduleBase: 0x7FFF00000000,
            registerValue: 0x1122334455667788,
            uniqueFrameOffset: 0x444));
        DebuggerSnapshotJsonSerializer serializer = new();
        string json = serializer.Serialize(snapshot);

        AssertContains(json, "\"schema\": \"teekay87-memory-engine-debugger-snapshot\"", "Snapshot JSON did not emit the canonical schema id.");
        AssertContains(json, "\"schemaVersion\": 1", "Snapshot JSON did not emit schema version 1.");
        AssertContains(json, "\"instructionPointer\": \"0x7FFF00000020\"", "Snapshot JSON did not preserve the 64-bit stop address as a hexadecimal string.");
        AssertContains(json, "\"triggerInstructionAddress\": \"0x7FFF00000010\"", "Snapshot JSON did not preserve the 64-bit trigger address as a hexadecimal string.");

        DebuggerSnapshot restored = serializer.Deserialize(json);
        AssertEqual(snapshot.SnapshotId, restored.SnapshotId, "Snapshot id changed during JSON round trip.");
        AssertEqual(snapshot.CapturedAtUtc, restored.CapturedAtUtc, "Snapshot capture timestamp changed during JSON round trip.");
        AssertEqual(snapshot.Event.InstructionPointer, restored.Event.InstructionPointer, "Stop/current IP changed during JSON round trip.");
        AssertEqual(snapshot.Event.TriggerInstructionAddress, restored.Event.TriggerInstructionAddress, "Trigger IP changed during JSON round trip.");
        AssertEqual(snapshot.Registers[0].RawBytes, restored.Registers[0].RawBytes, "Raw register bytes changed during JSON round trip.");
        AssertEqual(snapshot.Disassembly[0].Bytes, restored.Disassembly[0].Bytes, "Logical disassembly bytes changed during JSON round trip.");
        AssertEqual(snapshot.Breakpoints[0].Address, restored.Breakpoints[0].Address, "Breakpoint context changed during JSON round trip.");
        AssertEqual(snapshot.Memory[0].Bytes, restored.Memory[0].Bytes, "Bounded memory context changed during JSON round trip.");
        AssertEqual(snapshot.Source.CaptureSource, restored.Source.CaptureSource, "Original capture provenance changed during JSON round trip.");
        AssertEqual(snapshot.CaptureMode, restored.CaptureMode, "Snapshot capture mode changed during JSON round trip.");
        AssertTrue(restored.Disassembly[0].Markers is ICollection<string> { IsReadOnly: true },
            "Imported snapshot instruction markers were not frozen as read-only data.");
        DebuggerSnapshotComparison importedComparison = new DebuggerSnapshotComparer().Compare(snapshot, restored);
        AssertTrue(importedComparison.Items.Where(item => item.Category != "Summary").All(item => item.Classification == DebuggerComparisonClassification.Identical),
            "Imported snapshot did not compare identically to its pre-export source.");
        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerSnapshotJsonValidationAsync()
    {
        DebuggerSnapshot snapshot = new DebuggerSnapshotCaptureService().Capture(CreateSnapshotRequest(
            label: "Validation",
            group: "Player",
            moduleBase: 0x10000000,
            registerValue: 1,
            uniqueFrameOffset: 0x300));
        DebuggerSnapshotJsonSerializer serializer = new();
        string json = serializer.Serialize(snapshot);

        AssertThrows<InvalidDataException>(
            () => serializer.Deserialize(json.Replace(
                "teekay87-memory-engine-debugger-snapshot",
                "not-a-debugger-snapshot",
                StringComparison.Ordinal)),
            "Snapshot importer accepted an invalid schema id.");
        AssertThrows<NotSupportedException>(
            () => serializer.Deserialize(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99", StringComparison.Ordinal)),
            "Snapshot importer accepted an unsupported future schema version.");
        AssertThrows<InvalidDataException>(
            () => serializer.Deserialize(json.Replace("0100000000000000", "0G", StringComparison.Ordinal)),
            "Snapshot importer accepted invalid raw register hex data.");
        AssertThrows<InvalidDataException>(
            () => serializer.Deserialize(json.Replace("\"addressWidth\": 64", "\"addressWidth\": 28", StringComparison.Ordinal)),
            "Snapshot importer accepted addresses that exceed the declared address width.");

        string additiveJson = json.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"futureOptionalField\": { \"enabled\": true },",
            StringComparison.Ordinal);
        DebuggerSnapshot additive = serializer.Deserialize(additiveJson);
        AssertEqual(snapshot.SnapshotId, additive.SnapshotId,
            "Snapshot importer rejected or changed a schema-v1 document containing an unknown additive field.");
        return Task.CompletedTask;
    }

    private static async Task VerifyDebuggerSnapshotExportCancellationAsync()
    {
        DebuggerSnapshot snapshot = new DebuggerSnapshotCaptureService().Capture(CreateSnapshotRequest(
            "Transactional export", "Player", 0x10000000, 1, 0x300));
        DebuggerSnapshotJsonSerializer serializer = new();
        string directory = Path.Combine(Path.GetTempPath(), $"tk87me-snapshot-{Guid.NewGuid():N}");
        string destination = Path.Combine(directory, "snapshot.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(destination, "existing-complete-file").ConfigureAwait(false);
        try
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            await AssertThrowsAsync<OperationCanceledException>(
                () => serializer.ExportAsync(destination, snapshot, cancellation.Token),
                "Cancelled debugger snapshot export did not surface cancellation.").ConfigureAwait(false);
            AssertEqual("existing-complete-file", await File.ReadAllTextAsync(destination).ConfigureAwait(false),
                "Cancelled debugger snapshot export replaced an existing completed destination.");
            AssertEqual(0, Directory.GetFiles(directory, "*.tmp").Length,
                "Cancelled debugger snapshot export left a temporary publication file behind.");
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    private static Task VerifyDebuggerSnapshotLiveCaptureSourceAsync()
    {
        string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DebuggerViewModel.cs"));
        AssertContains(source, "DebuggerSessionState.Paused",
            "Live snapshot capture is not explicitly gated to a paused debugger session.");
        AssertContains(source, "_latestStopContext?.Sequence != stopSequence",
            "Live snapshot capture does not revalidate the stop sequence before publishing a snapshot.");
        AssertContains(source, "!IsTargetCurrent()",
            "Live snapshot capture does not revalidate Active Target/session identity before publication.");
        AssertContains(source, "CancellationTokenSource.CreateLinkedTokenSource",
            "Live snapshot capture does not accept cancellation through the debugger lifetime/caller token.");
        AssertContains(source, "ReadDisassemblyContextAsync",
            "Live snapshot capture does not preserve bounded logical disassembly context.");
        AssertContains(source, "ReadMemoryViewerWindowAsync",
            "Live snapshot capture does not preserve the bounded standard stack-memory context.");
        AssertContains(source, "DebuggerSnapshotSectionStatus.Failed",
            "Live snapshot capture does not preserve optional-section failures honestly.");
        AssertContains(source, "DebuggerSnapshotCaptureService",
            "Live snapshot capture bypasses the shared snapshot composition service.");
        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerSnapshotComparerAsync()
    {
        DebuggerSnapshotCaptureService capture = new();
        DebuggerSnapshot player1 = capture.Capture(CreateSnapshotRequest("Player 1", "Player", 0x10000000, 1, 0x300));
        DebuggerSnapshot player2 = capture.Capture(CreateSnapshotRequest("Player 2", "Player", 0x20000000, 1, 0x300));
        DebuggerSnapshot enemy1 = capture.Capture(CreateSnapshotRequest("Enemy 1", "Enemy", 0x30000000, 0, 0x400));
        DebuggerSnapshot enemy2 = capture.Capture(CreateSnapshotRequest("Enemy 2", "Enemy", 0x40000000, 0, 0x400));

        DebuggerSnapshotComparer comparer = new();
        DebuggerSnapshotComparison pair = comparer.Compare(player1, player2);
        DebuggerComparisonItem context = pair.Items.Single(item => item.Category == "Context" && item.Name == "Stop/current instruction");
        AssertEqual(DebuggerComparisonClassification.Identical, context.Classification,
            "Module-relative stop addresses with different raw ASLR bases were not treated as the same code location.");

        DebuggerSnapshotComparison groups = comparer.CompareGroups([player1, player2], [enemy1, enemy2]);
        DebuggerComparisonItem register = groups.Items.Single(item => item.Category == "Registers" && string.Equals(item.Name, "R12", StringComparison.OrdinalIgnoreCase));
        AssertEqual(DebuggerComparisonClassification.StableInBothGroupsDifferentBetweenGroups, register.Classification,
            "Comparer did not identify a register that is stable inside both groups and different between them.");
        DebuggerComparisonItem divergence = groups.Items.Single(item => item.Category == "Call Stack" && item.Name == "First divergence");
        AssertContains(divergence.GroupA, "+0x300", "Comparer did not expose the first Player call-stack divergence.");
        AssertContains(divergence.GroupB, "+0x400", "Comparer did not expose the first Enemy call-stack divergence.");
        DebuggerComparisonItem triggerCode = groups.Items.Single(item => item.Category == "Instructions" && item.Name == "Trigger instruction code");
        AssertEqual(DebuggerComparisonClassification.Identical, triggerCode.Classification,
            "Logical trigger instruction comparison did not normalize module-relative locations across ASLR bases.");
        DebuggerComparisonItem summaryRegister = groups.Items.Single(item =>
            item.Category == "Summary" && item.Name.Contains("Registers / R12", StringComparison.OrdinalIgnoreCase));
        AssertEqual(DebuggerComparisonClassification.StableInBothGroupsDifferentBetweenGroups, summaryRegister.Classification,
            "Stable register discriminator was not promoted into the explainable comparison summary.");

        DebuggerSnapshot withWrapper = enemy1 with
        {
            CallStack = Array.AsReadOnly(new[]
            {
                enemy1.CallStack[0],
                enemy1.CallStack[0] with { Index = 1, ModuleOffset = 0x250, InstructionAddress = 0x30000250 },
                enemy1.CallStack[1] with { Index = 2 },
                enemy1.CallStack[2] with { Index = 3 }
            })
        };
        DebuggerSnapshotComparison aligned = comparer.Compare(enemy1, withWrapper);
        DebuggerComparisonItem alignedPath = aligned.Items.Single(item => item.Category == "Call Stack" && item.Name == "Execution path");
        AssertContains(alignedPath.Evidence, "3 ordered frame(s) align overall",
            "Pairwise call-stack comparison did not retain ordered alignment evidence when one stack contains an extra wrapper frame.");
        return Task.CompletedTask;
    }

    private static Task VerifyDisassemblerBreakpointWatchpointActionSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string xaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DisassemblerWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DisassemblerWindow.xaml.cs"));
        string main = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml.cs"));
        AssertContains(xaml, "Header=\"Add Breakpoint\"", "Disassembler context menu does not expose the direct Add Breakpoint action.");
        AssertContains(xaml, "Header=\"Add Watchpoint\"", "Disassembler context menu does not expose the direct Add Watchpoint action.");
        AssertContains(code, "dataGrid.SelectedItems.Count == 1", "Disassembler debugger actions are not explicitly gated to exactly one selected row.");
        AssertContains(code, "AddBreakpointMenuItem_Click", "Disassembler Add Breakpoint action has no code-behind handler.");
        AssertContains(code, "AddWatchpointMenuItem_Click", "Disassembler Add Watchpoint action has no code-behind handler.");
        AssertContains(main, "FindAttachedDebugger(plugin, targetProcess)", "Disassembler debugger actions do not reuse the existing attached-debugger lookup path.");
        AssertContains(main, "CanResolveDisassemblyWatchpoint()", "Disassembler Add Watchpoint is still restricted to only the current stop/trigger instruction instead of any safely resolvable selected row while paused.");
        AssertContains(main, "ResolveDisassemblyWatchpointTarget", "Disassembler Add Watchpoint does not use the plugin-owned memory-access resolver.");
        AssertContains(main, "target.Size", "Disassembler Add Watchpoint does not use the derived memory-access size.");
        AssertContains(main, "target.Access", "Disassembler Add Watchpoint does not use the derived memory-access mode.");
        AssertContains(main, "ValidateAddressActionRequest(request)", "Disassembler debugger actions bypass existing backend request validation.");
        return Task.CompletedTask;
    }

    private static Task VerifyCallStackComparerWorkspaceSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string debuggerXaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string comparerXaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "CallStackComparerWindow.xaml"));
        string comparerCode = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "CallStackComparerWindow.xaml.cs"));
        string comparerViewModel = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "CallStackComparerViewModel.cs"));
        string main = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "MainWindow.xaml.cs"));
        AssertContains(debuggerXaml, "Content=\"Compare...\"", "Call Stack workspace does not expose the comparer entry point.");
        AssertContains(comparerXaml, "Capture Current", "Comparer does not expose live snapshot capture.");
        AssertContains(comparerXaml, "IsEnabled=\"{Binding CanCaptureCurrent}\"",
            "Comparer live capture is not disabled while the debugger cannot safely capture the current paused context.");
        AssertContains(comparerXaml, "Compare Groups", "Comparer does not expose grouped comparison.");
        AssertContains(comparerXaml, "<DataGridTemplateColumn Header=\"Group\"", "Comparer snapshot rows do not use a template-based Group editor consistent with Saved Addresses rows.");
        AssertContains(comparerXaml, "ItemsSource=\"{Binding AvailableGroups}\"", "Comparer snapshot Group editor is not populated from session-local group names.");
        AssertContains(comparerXaml, "x:Name=\"NewSnapshotGroupTextBox\"", "Comparer snapshot Group dropdown does not expose the dedicated text input for new session groups.");
        AssertContains(comparerXaml, "PreviewKeyDown=\"SnapshotNewGroupTextBox_PreviewKeyDown\"", "Comparer new-group input is not wired to explicit creation on Enter.");
        AssertContains(comparerXaml, "DropDownOpened=\"SnapshotGroupComboBox_DropDownOpened\"", "Comparer Group dropdown does not focus the dedicated new-group input when opened.");
        AssertContains(comparerXaml, "SelectedItem=\"{Binding Group, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", "Comparer snapshot Group dropdown does not assign an existing session group directly to the row.");
        AssertContains(comparerXaml, "Style=\"{StaticResource SnapshotGroupComboBoxStyle}\"", "Comparer snapshot Group row does not use the dedicated group-creation dropdown presentation.");
        AssertContains(comparerXaml, "x:Name=\"GroupAComboBox\"", "Comparer Group A selector is not a dropdown.");
        AssertContains(comparerXaml, "x:Name=\"GroupBComboBox\"", "Comparer Group B selector is not a dropdown.");
        AssertContains(comparerXaml, "ItemsSource=\"{Binding GroupNames}\" SelectedItem=\"{Binding SelectedGroupA, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\" IsEditable=\"False\"", "Comparer Group A selector is not restricted to registered session names with live view-model selection state.");
        AssertContains(comparerXaml, "ItemsSource=\"{Binding GroupNames}\" SelectedItem=\"{Binding SelectedGroupB, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\" IsEditable=\"False\"", "Comparer Group B selector is not restricted to registered session names with live view-model selection state.");
        AssertFalse(comparerXaml.Contains("x:Name=\"GroupATextBox\"", StringComparison.Ordinal), "Comparer Group A still exposes free-form text entry.");
        AssertFalse(comparerXaml.Contains("x:Name=\"GroupBTextBox\"", StringComparison.Ordinal), "Comparer Group B still exposes free-form text entry.");
        AssertContains(comparerXaml, "Click=\"RemoveSnapshotRowButton_Click\"", "Comparer snapshot rows do not expose the Saved-Addresses-style row Remove action.");
        AssertContains(comparerViewModel, "raw.Length == 0 ? string.Empty : _registerGroupName(raw)", "Comparer No group does not bypass group-name registration for empty membership.");
        AssertContains(comparerXaml, "Content=\"No group\"", "Comparer Group dropdown does not expose an explicit ungroup action.");
        AssertContains(comparerCode, "comboBox.SelectedIndex = -1;", "Comparer No group action does not clear the live row ComboBox selection immediately.");
        AssertFalse(comparerCode.Contains("comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateTarget();", StringComparison.Ordinal), "Comparer No group action still relies on the ineffective rev47 binding-target refresh.");
        AssertContains(comparerXaml, "IsEnabled=\"{Binding CanComparePair}\"", "Comparer Compare 2 action is not gated to exactly two selected snapshots.");
        AssertContains(comparerXaml, "IsEnabled=\"{Binding CanCompareGroups}\"", "Comparer Compare Groups action is not gated to two distinct non-empty groups.");
        AssertContains(comparerXaml, "IsEnabled=\"{Binding CanExportSnapshot}\"", "Comparer snapshot Export action is not gated to exactly one selected snapshot.");
        AssertContains(comparerXaml, "IsEnabled=\"{Binding HasResults}\"", "Comparer Export Results action is not gated to a current comparison result.");
        AssertContains(comparerXaml, "IsEnabled=\"{Binding HasSnapshots}\"", "Comparer Remove All action is not disabled when the snapshot list is empty.");
        AssertFalse(comparerXaml.Contains("Click=\"RemoveButton_Click\"", StringComparison.Ordinal), "Comparer still exposes the redundant panel-level Remove action.");
        AssertContains(comparerCode, "SnapshotNewGroupTextBox_PreviewKeyDown", "Comparer does not create a new snapshot group from the dropdown input.");
        AssertContains(comparerCode, "TryCreateAndAssignGroup", "Comparer new-group input does not assign the created group to the originating snapshot row.");
        AssertContains(comparerViewModel, "TryCreateAndAssignGroup", "Comparer row view model does not expose explicit create-and-assign group behavior.");
        AssertContains(comparerCode, "ViewModel.SelectedGroupA", "Comparer group comparison does not use the restricted Group A view-model selection.");
        AssertContains(comparerViewModel, "ObservableCollection<string> GroupNames", "Comparer does not retain session-local group names.");
        AssertContains(comparerViewModel, "RegisterGroupName", "Comparer does not normalize and register snapshot group assignments.");
        AssertContains(comparerViewModel, "StringComparison.OrdinalIgnoreCase", "Comparer group-name reuse is not case-insensitive.");
        AssertContains(comparerCode, "ImportAsync", "Comparer does not import offline snapshot JSON.");
        AssertContains(comparerCode, "ExportResultsButton_Click", "Comparer does not export derived comparison results.");
        AssertContains(comparerViewModel, "CompareGroups", "Comparer view model does not use the shared grouped comparison engine.");
        AssertContains(comparerCode, "ConfirmationDialogService", "Comparer destructive snapshot removal does not reuse the shared confirmation dialog service.");
        AssertContains(comparerViewModel, "ComparisonUsesSnapshot", "Comparer does not track whether a removed snapshot belongs to the current comparison result.");
        AssertContains(comparerViewModel, "SnapshotGroupChanged", "Comparer does not invalidate group results when compared-group membership changes.");
        AssertContains(comparerViewModel, "InvalidateGroupComparisonForSelectionChange", "Comparer does not invalidate group results when Group A/B inputs change.");
        AssertContains(comparerViewModel, "public void ClearGroup() => Group = string.Empty;", "Comparer row model does not support explicit ungrouping while retaining the session group catalog.");
        AssertContains(main, "CallStackComparerViewModel? comparerWorkspace = null", "Comparer snapshot state is not retained by the debugger-session workspace after the window closes.");
        AssertContains(main, "candidate => ReferenceEquals(candidate, comparerWorkspace)", "Main window does not reuse/activate the existing comparer window for one debugger-session workspace.");
        AssertFalse(main.Contains("DebuggerSnapshot? snapshot = viewModel.CanCaptureSnapshot", StringComparison.Ordinal), "Opening the comparer still performs an implicit snapshot capture.");
        AssertContains(main, "window.Closed += (_, _) => comparerWorkspace?.DetachLiveDebugger()", "Debugger-window closure does not detach the persistent comparer workspace from the disposed live debugger.");
        AssertFalse(comparerCode.Contains("CallStackComparerWindow_Closed", StringComparison.Ordinal), "Closing the comparer still discards its live-debugger binding instead of preserving the same-session workspace state.");

        string debuggerViewModel = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerViewModel.cs"));
        AssertContains(debuggerViewModel, "_latestStopContext = context;\n                OnPropertyChanged(nameof(CanCaptureSnapshot));",
            "Comparer capture availability is not re-evaluated when a new paused stop context arrives.");
        return Task.CompletedTask;
    }

    private static Task VerifyDebuggerListExportSourceAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string xaml = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(baseDirectory, "Fixtures", "DebuggerWindow.xaml.cs"));
        AssertContains(xaml, "Click=\"ExportDebuggerButton_Click\"", "Debugger does not expose a Universal Export entry point.");
        AssertContains(code, "debugger-threads", "Debugger export does not include Threads.");
        AssertContains(code, "debugger-registers", "Debugger export does not include Registers.");
        AssertContains(code, "debugger-breakpoints", "Debugger export does not include Breakpoints/Watchpoints.");
        AssertContains(code, "debugger-call-stack", "Debugger export does not include Call Stack rows.");
        AssertContains(code, "debugger-events", "Debugger export does not include Events.");
        AssertContains(code, "Trigger Instruction", "Debugger event export does not expose trigger instruction separately from stop/current IP.");
        AssertContains(code, "TabularExportService", "Debugger lists do not use the shared Universal Export service.");
        return Task.CompletedTask;
    }

    private static DebuggerSnapshotCaptureRequest CreateSnapshotRequest(
        string label,
        string group,
        ulong moduleBase,
        ulong registerValue,
        ulong uniqueFrameOffset)
    {
        byte[] registerBytes = BitConverter.GetBytes(registerValue);
        DebuggerSnapshotSource source = new(
            "0.1.7", 35, "mock", "Mock", "1.0.1", 17, "2.18.0", "Mock",
            0x1234, "mock-process", CpuArchitecture.X64, 64, 64, Endianness.Little, 7,
            DebuggerSnapshotCaptureSource.Live);
        DebuggerSnapshotEventContext context = new(
            42, DateTimeOffset.Parse("2026-09-12T12:00:00Z", CultureInfo.InvariantCulture),
            DebuggerEventKind.Watchpoint, DebuggerExecutionState.Paused, DebuggerStopReason.Watchpoint,
            0x99, "Mock Main", moduleBase + 0x20, moduleBase + 0x10, DebuggerTriggerResolution.DisassemblyDerived,
            0x50000000, DebuggerBreakpointAccess.Write, 4, "wp0", DebuggerSnapshotBreakpointType.Watchpoint,
            DebuggerBreakpointKind.Hardware, DebuggerSnapshotBreakpointLifetime.Persistent, "Watchpoint hit");
        DebuggerSnapshotRegister[] registers =
        [
            new("r12", "R12", "General", DebuggerRegisterRole.None, 64, DebuggerRegisterValueEncoding.Bytes,
                Convert.ToHexString(registerBytes), $"0x{registerValue:X16}", false)
        ];
        DebuggerSnapshotCallFrame[] frames =
        [
            new(0, moduleBase + 0x20, moduleBase + 0x80, 0x70000000, 0x70000020, "eboot.bin", moduleBase, 0x20, "shared_health_write"),
            new(1, moduleBase + 0x100, moduleBase + 0x180, 0x70000040, 0x70000060, "eboot.bin", moduleBase, 0x100, "apply_damage"),
            new(2, moduleBase + uniqueFrameOffset, null, 0x70000080, 0x700000A0, "eboot.bin", moduleBase, uniqueFrameOffset, null)
        ];
        DebuggerSnapshotInstruction[] instructions =
        [
            new(moduleBase + 0x10, "eboot.bin", 0x10, "017040", "add [rax+40h],esi", DisassemblyFlowControl.None, null, ["Watchpoint hit"]),
            new(moduleBase + 0x20, "eboot.bin", 0x20, "488B4F30", "mov rcx,[rdi+30h]", DisassemblyFlowControl.None, null, Array.Empty<string>())
        ];
        DebuggerSnapshotBreakpoint[] breakpoints =
        [
            new("wp0", 0x50000000, true, DebuggerSnapshotBreakpointType.Watchpoint, DebuggerBreakpointKind.Hardware,
                DebuggerBreakpointAccess.Write, 4, DebuggerSnapshotBreakpointLifetime.Persistent, true)
        ];
        DebuggerSnapshotMemoryBlock[] memory =
        [
            new("Stack", 0x70000000, "00112233445566778899AABBCCDDEEFF")
        ];
        DebuggerSnapshotSections sections = new(
            DebuggerSnapshotSectionStatus.Complete,
            DebuggerSnapshotSectionStatus.Complete,
            DebuggerSnapshotSectionStatus.Complete,
            DebuggerSnapshotSectionStatus.Complete,
            DebuggerSnapshotSectionStatus.Complete);
        return new DebuggerSnapshotCaptureRequest(source, context, registers, frames, instructions, breakpoints, memory, sections, label, group);
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

    private static void AssertContains(string source, string expected, string message)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expected);
        AssertTrue(source.Contains(expected, StringComparison.Ordinal), message);
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
