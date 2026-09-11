# Plugin SDK Foundation

## Purpose

This document describes the current platform-plugin boundary used by TeeKay87's Memory Engine.

The Plugin SDK is platform-neutral. It defines the contracts that allow the host to discover a target plugin, render its connection fields, connect to a target, invoke neutral target services, and consume scanner definitions without exposing ps5debug-NG, Windows APIs, Xbox 360 protocols, or other backend-specific implementation types to Core or WPF.

Plugin-specific design, protocol, implementation, and verification documentation belongs under that plugin's dedicated directory in `docs/plugins/`.

## Project Boundary

| Project | Responsibility |
| --- | --- |
| `TeeKay87.MemoryEngine.App` | WPF presentation, application composition, and generic UI generated from plugin declarations |
| `TeeKay87.MemoryEngine.Core` | Shared host infrastructure, plugin discovery, scan orchestration, and platform-neutral application logic |
| `TeeKay87.MemoryEngine.PluginSdk` | Public contracts, capability flags, compatibility metadata, neutral target models, reusable standard Value Types, and stable cross-boundary Scan Type ids |
| platform plugin projects | Target-specific connection, services, concrete Value Types/options, and native/backend mappings |
| `TeeKay87.MemoryEngine.Tests` | Dependency-free verification executable |

The Plugin SDK does not reference Core or WPF. A platform plugin therefore depends only on the SDK and its own backend dependencies.

## Independent Version Domains

Three version domains are intentionally separate:

1. the host application version;
2. each plugin's own version/revision;
3. the Plugin API compatibility version.

The current Plugin API is:

```text
2.16.0
```

Plugin API `1.1.0` introduced optional native value scanning and process control. Plugin API `1.2.0` introduced optional native refinement/session reset. Plugin API `1.3.0` introduced the first capability-driven Value Type and Scan Type declarations. Host `0.1.3.rev3` replaced that enum/Core-catalog model with plugin-owned concrete scan-definition objects, advancing the public contract to major version `2.0.0`. Host `0.1.3.rev7` added optional `IMemoryScanOption` declarations and `MemoryScanOptions`, producing API `2.1.0`. Host `0.1.3.rev13` added optional batched native-result streaming and candidate-source contracts, producing API `2.2.0`. Host `0.1.3.rev16` added optional host-managed plugin settings through `IPluginSettings`/`IPluginSettingsConsumer`, advancing the API to `2.3.0`. Host `0.1.3.rev18` added optional `IConcurrentMemoryWriter`, producing API `2.4.0`. Host `0.1.3.rev24` added optional `IMemoryValueInputPolicy`, producing API `2.5.0`. Host `0.1.3.rev25` added optional `IMemoryValueComparer`, producing API `2.6.0`; this lets Core-owned ordered/delta Scan Types operate on plugin Value Types without moving value representation into Core. Host `0.1.3.rev26` adds optional `INativeScanTypeMappingProvider` plus the neutral `NativeScanTypeMapping` model, producing API `2.7.0`; plugins can now declare semantic equivalence between Core-owned Scan Types and plugin-native operations without leaking backend ids into Core or WPF. Host `0.1.3.rev28` adds optional `IMemoryScanOptionPresentation` and `IMemoryScanOptionApplicability`, producing API `2.8.0`; plugins can now request generic choice/toggle presentation and restrict an option to the Core Scan Types/stages where it is meaningful without adding platform checks to WPF. Host `0.1.3.rev30` adds optional `INativeValueScanResidentResultSet` and optional Previous-value payloads on `NativeValueScanResultBatch`, producing API `2.9.0`; a backend can now keep a complete authoritative native survivor set resident, expose bounded windows to the host, and declare whether the current set can be refined natively without forcing immediate full materialization. Host `0.1.6.rev1` adds `IDisassemblerProvider`, `DisassembledInstruction`, and `DisassemblyFlowControl`, producing API `2.10.0`; Core can now request architecture-neutral instruction records from a plugin without moving target-specific decoding or transport logic into the host. Host `0.1.6.rev6` extends `DisassembledInstruction` with optional architecture-neutral syntax-presentation tokens, producing API `2.11.0`; the original constructor remains available, so compatible 2.x providers that do not supply tokens continue to work with plain instruction text. Host `0.1.7.rev1` adds the first architecture-neutral debugger surface through `IDebuggerProvider`, `IDebuggerSession`, optional attached-session services, neutral debugger models, and `TargetCapabilities.ThreadControl`, producing API `2.12.0`. The contracts are additive: a compatible 2.11 plugin remains loadable and does not gain debugger behavior unless it explicitly advertises the appropriate capability and exposes the corresponding services. Host `0.1.7.rev7` extends `DebuggerRegister` with neutral `DebuggerRegisterValueEncoding`, producing API `2.13.0`; register presentation/editing can now interpret fixed-width values without architecture-specific parsing in WPF. Host `0.1.7.rev11` adds the optional `IDebuggerBreakpointStateService`, producing API `2.14.0` for generic Enable/Disable of neutral breakpoint records. Host `0.1.7.rev16` adds optional `DebuggerEvent.TriggeredBreakpoint` context while retaining the original event constructor, producing API `2.15.0`; a watchpoint event can now identify the watched neutral record without replacing the accessing instruction stored in `InstructionPointer`. Host `0.1.7.rev17` consumes the already-defined call-stack/step contracts without another bump. Host `0.1.7.rev30` adds optional `IDebuggerBreakpointValidationService` plus `DebuggerBreakpointValidationResult`, producing API `2.16.0`; generic UI can preflight a complete neutral breakpoint/watchpoint request without mutating debugger state while final add validation remains plugin-owned.

The compatibility rule is:

- major version must match the host Plugin API major version;
- a plugin may target the same or an older minor version within that major version;
- a plugin requiring a newer minor version than the host provides is rejected during discovery.

A Plugin API `1.x` binary is therefore not considered compatible with the 2.x host contract. Within major version 2, the host accepts plugins that target the same or an older minor version; `ITargetPlugin.SupportedScanOptions` has an empty default implementation so an existing 2.0 plugin does not have to declare scan options.

## Plugin Entry Point

A platform plugin exposes one or more public, non-abstract classes implementing `ITargetPlugin`.

The current scanner-related part of the contract is conceptually:

```csharp
PluginMetadata Metadata { get; }
TargetCapabilities Capabilities { get; }
IReadOnlyList<TargetConnectionSettingDefinition> ConnectionSettings { get; }
IReadOnlyList<IMemoryValueType> SupportedValueTypes { get; }
IReadOnlyList<IMemoryScanOption> SupportedScanOptions { get; }
string? DefaultValueTypeId { get; }

Task<ITargetSession> ConnectAsync(
    TargetConnectionOptions options,
    CancellationToken cancellationToken);
```

A plugin entry type currently requires a public parameterless constructor because `PluginHost` creates it through reflection.

`ITargetPlugin.SupportedScanTypes` and `DefaultScanTypeId` remain only as obsolete compatibility members in Plugin API 2.x. The host no longer reads or validates them. New plugins should not implement them. Scan Type definitions and default selection are owned by `TeeKay87.MemoryEngine.Core.Scanning.MemoryScanTypeCatalog`.

## Plugin Metadata

`PluginMetadata` contains:

- stable plugin id;
- user-facing plugin name;
- target platform;
- backend/transport name;
- plugin semantic version;
- plugin revision;
- targeted Plugin API version;
- description;
- target architecture.

`PluginMetadata.DisplayVersion` uses:

```text
<version>.rev<revision>
```

Platform and backend remain separate concepts. A target platform may be PlayStation 5 while the active transport/backend is ps5debug-NG.

## Connection Settings

Connection requirements are plugin-owned.

`ITargetPlugin.ConnectionSettings` exposes `TargetConnectionSettingDefinition` objects containing the stable option key, label, optional description, optional default value, and required state. The WPF application renders these fields generically and passes the resulting `TargetConnectionOptions` to the plugin.

From host `0.1.7.rev4`, generic connection fields are rendered on the permanent first target row between **Platform** and **Connect**. Host `0.1.7.rev5` makes their sizing responsive: every ordinary row-1 connection TextBox/ComboBox participates in one shared host width capped by `UiMetrics.TopTargetInputMaxWidth = 180` and carrying no minimum width. The host reduces that common width as the available first-row content width contracts, after reserving fixed action buttons plus existing margins/spacings. Plugins must not encode their own WPF widths or add platform-specific layout branches. The permanent target header is limited to two normal control rows, so plugins that require extensive configuration should keep additional settings in a dedicated settings/details workflow rather than requiring extra permanent rows.

The plugin remains responsible for backend-specific validation such as legal port ranges, host syntax, credentials, or target-specific requirements.
The generic host therefore does not apply memory-value or numeric textbox filters to plugin-defined connection fields unless a future public connection-setting contract explicitly declares such an input policy.

## Host-Managed Plugin Settings

Plugin API `2.3.0` added optional `IPluginSettings` and `IPluginSettingsConsumer` contracts. A plugin implementing `IPluginSettingsConsumer` receives an `IPluginSettings` object scoped by Core to that plugin's stable `PluginMetadata.Id` before the host reads the plugin's connection declarations. The plugin can then use stable keys through `TryGetString`, `TrySetString`, and `TryRemove` without receiving a settings file path or access to another plugin's namespace.

Core persists those namespaces inside the existing shared application settings document. The WPF layer continues to own presentation only; it does not contain platform-specific persistence rules. Plugins that do not require persisted state simply omit `IPluginSettingsConsumer`, so older compatible 2.x plugins need no change.

The first consumer is PS5 plugin `0.1.0.rev15`, which restores `connection.host` and `connection.port` as connection-field defaults and updates them only after a successful ps5debug-NG connection. See [`PLUGIN_SETTINGS_PERSISTENCE.md`](PLUGIN_SETTINGS_PERSISTENCE.md) for the storage and lifecycle details.

Plugin API `2.4.0` adds optional `IConcurrentMemoryWriter`. It extends `IMemoryWriter` as a marker contract that explicitly promises the returned writer can be used while another long-running operation owns the session's primary target transport. The host must never assume that ordinary `IMemoryWriter` is safe for concurrent protocol traffic. Saved Addresses uses this service only for Frozen writes that continue during scanning. PS5 plugin `0.1.0.rev16` provides it through a separate lazily connected ps5debug-NG client, keeping those writes isolated from TurboScan's primary command stream.

Plugin API `2.5.0` adds optional `IMemoryValueInputPolicy`. A concrete `IMemoryValueType` may implement this companion contract when it can tell the host whether the current textbox contents are still a potentially valid edit state. The contract is deliberately optional and does not replace `IMemoryValueType.TryParse(...)`: live filtering is a presentation aid, while final parse/range/architecture validation remains authoritative when a scan or write is committed. Standard SDK Value Types implement the policy. Custom plugin Value Types that omit it remain fully compatible and continue to receive unrestricted text followed by their existing `TryParse(...)` validation. The host must not infer custom syntax from display names, stable ids, or platform names.

## Capability Model

`TargetCapabilities` is a flags enum used for coarse-grained subsystem availability. It includes connection/process/memory capabilities plus allocation/protection, native scanning, pointer scanning, disassembly/assembly, debugging, breakpoints/watchpoints, register access, thread enumeration/control, call-stack access, stepping, and cheat operations. `ThreadControl` was added in Plugin API `2.12.0` so a backend can advertise thread enumeration without implying individual-thread suspend/resume.

A backend may technically support an operation before its Memory Engine plugin implements it. The plugin must advertise only capabilities that its current implementation can actually provide.

Capability flags describe subsystem availability. Value Types remain plugin-owned because representation/parsing can be platform-specific. Standard Scan Type behavior is Core-owned; ordered/delta Core predicates use the optional `IMemoryValueComparer` companion contract when the selected Value Type supports numeric ordering.

From Plugin API `2.10.0`, a plugin advertising `TargetCapabilities.Disassembly` supplies `IDisassemblerProvider` from its connected session. The provider receives caller-supplied bytes plus the existing `TargetArchitecture` and returns neutral instruction records; it does not own target-memory I/O. Architecture-specific decoding remains in the plugin. Plugin API `2.11.0` optionally adds `DisassemblyTextToken` metadata to each instruction so a provider can classify already-formatted text as neutral Mnemonic/FlowControlMnemonic/Register/Number/Keyword/Text segments without leaking architecture-specific register or opcode knowledge into Core/WPF. Providers that omit tokens retain the same plain-text presentation. See [`DISASSEMBLY_ARCHITECTURE.md`](DISASSEMBLY_ARCHITECTURE.md).

## Target Sessions and Services

A successful connection returns `ITargetSession`.

A session exposes plugin metadata, target architecture, connection state, asynchronous disposal, and typed service lookup through `GetService<TService>()`.

Current neutral service contracts include:

```text
IProcessProvider
IForegroundProcessProvider
IMemoryMapProvider
IMemoryReader
IMemoryWriter
INativeValueScanner
INativeValueScanRefiner
INativeValueScanStreamProvider
INativeValueScanResultStream
INativeValueScanStreamRefiner
INativeValueScanCandidateSource
INativeScanTypeMappingProvider
IProcessControl
IDisassemblerProvider
IDebuggerProvider
```

`TargetSessionExtensions.GetRequiredService<TService>()` is available for paths where capability checks already establish that a service must exist.

## Plugin-Owned Value Types

### Core rule

Core knows **what a Value Type definition can do**, but it does not know **which concrete Value Types exist**.

There is no Core master Value Type list and no `MemoryValueType` enum registry. Every Value Type displayed or executed in a scan comes from the active plugin's `SupportedValueTypes` collection.

`IMemoryValueType` currently provides:

- stable `Id`;
- `DisplayName`;
- category and description metadata;
- input help text;
- optional fixed byte width;
- default alignment;
- input parsing into a neutral `MemoryScanValue`;
- scan-width/alignment resolution for fixed or dynamic representations;
- conversion of target bytes into a displayed `MemoryScanValue`;
- equality semantics used by reusable scan predicates.

This lets a plugin provide a Value Type unknown to the host. Core does not switch on `Int32`, `Float`, `String`, or another concrete identifier.

### Reusable standard definitions

The Plugin SDK contains optional reusable helpers in:

```text
TeeKay87.MemoryEngine.PluginSdk.Scanning
```

`StandardMemoryValueTypes` currently provides the eleven representations already needed by the built-in PS5 and Mock plugins:

```text
UInt8
Int8
UInt16
Int16
UInt32
Int32
UInt64
Int64
Float32
Float64
ByteArray
```

These helpers implement the established decimal/hex parsing, target-endian byte conversion, display formatting, standard alignments, and exact equality behavior.

For the standard integer helpers, signed representations are the primary compact presentation and use the display names `1 Byte`, `2 Bytes`, `4 Bytes`, and `8 Bytes`. Unsigned representations remain explicit as `1 Byte (Unsigned)`, `2 Bytes (Unsigned)`, `4 Bytes (Unsigned)`, and `8 Bytes (Unsigned)`. This is display metadata only: the stable `standard.int*` / `standard.uint*` ids, numeric ranges, parsing behavior, byte widths, and target-endian conversion remain distinct and unchanged. The built-in PS5 and Mock plugins currently choose `standard.int32` as `DefaultValueTypeId`, which renders as `4 Bytes`. A future plugin remains free to choose another default or provide different display names because the host still renders the plugin's definitions directly.

They are **not a master registry**. A plugin may:

- reuse any of them;
- omit any of them;
- expose them in its own order;
- wrap or replace them;
- provide completely custom `IMemoryValueType` implementations and ids.

A future PC or Xbox 360 plugin therefore does not require a Core change to introduce a target-specific representation.

## Core-Owned Scan Types

From host `0.1.3.rev25`, the production Scan Type catalog is owned by Core rather than by individual platform plugins. `TeeKay87.MemoryEngine.Core.Scanning.MemoryScanTypeCatalog` is the single host-side source for the standard predicates shown in the Scan panel.

`IMemoryScanType` remains a public Plugin SDK contract because Core Scan Types operate against plugin-owned `IMemoryValueType` implementations and the low-level scanner/test surfaces accept the neutral interface. The contract provides:

- stable `Id`;
- `DisplayName` and description;
- First Scan / Next Scan availability;
- required operand count;
- Value Type compatibility;
- input validation;
- comparison behavior.

The current Core catalog contains 13 standard predicates:

- Exact Value;
- Fuzzy Value;
- Bigger Than;
- Smaller Than;
- Between;
- Unknown Initial Value;
- Unknown Initial Low Value;
- Increased Value;
- Decreased Value;
- Changed Value;
- Unchanged Value;
- Increased By;
- Decreased By.

Plugins do **not** redeclare these standard predicates. `ITargetPlugin.SupportedScanTypes` and `DefaultScanTypeId` remain obsolete compatibility members for older Plugin API 2.x binaries and are ignored by the production host.

Value Type capability determines which Core predicates are offered. Equality predicates can use the base `IMemoryValueType` equality contract, while ordered/delta predicates require the optional `IMemoryValueComparer` companion interface and a fixed value width. Fuzzy Value is currently defined for the standard Float/Double ids. Unknown Initial Value requires a fixed-width Value Type because no operand exists from which a variable width can be inferred.

A new **general** comparison predicate therefore belongs in Core. A new target-specific representation remains plugin-owned as an `IMemoryValueType`.

## Plugin-Owned Scan Options

Plugin API `2.1.0` added optional `IMemoryScanOption` definitions. Core does not assume that every target supports Endianness, Alignment, floating-point matching controls, or any other scan option. The active plugin decides which option definitions exist and the WPF Scan panel renders those definitions generically.

Each option declares:

- a stable id and display metadata;
- its list of choices and default choice;
- whether it applies to the currently selected `IMemoryValueType`;
- whether it must lock after a successful First Scan.

Plugin API `2.8.0` adds two optional companion contracts without changing the base `IMemoryScanOption` ABI for older 2.x plugins:

- `IMemoryScanOptionPresentation` describes whether the host should render the option as the normal choice list or as a generic two-state toggle. Toggle metadata maps checked and unchecked states back to the option's existing stable choice ids, so no WPF control state leaks into scan execution.
- `IMemoryScanOptionApplicability` lets the plugin state whether the option is meaningful for a selected Core-owned `IMemoryScanType` and First/Next stage. The host combines this with the existing Value Type applicability and hides an option when either dimension says it is irrelevant.

Plugin discovery validates scan-option ids, choices, defaults, Value Type applicability, optional presentation metadata, toggle choice mappings, and optional Core Scan Type applicability before the plugin reaches the workspace. A malformed option declaration is reported as a plugin discovery error rather than being allowed to fail later during UI construction.

The selected choices are captured in `MemoryScanOptions` and flow through shared and native scan requests exactly as before. Presentation and visibility metadata affect only how the choice is offered to the user; they do not alter its stored id/value semantics. Reusable standard option ids exist for Endianness, Alignment, and Floating-point rounding, but a plugin may define additional option ids without adding platform-specific UI code. `NativeValueScanRequest` carries the same option state to a native backend.

Scan options are not concrete target capabilities in Core. A plugin with no `SupportedScanOptions` renders no option controls. This preserves the plugin-owned architecture while still allowing shared scan orchestration to understand reusable neutral concepts when an option deliberately uses a standard id.

## Scan Definition Ownership

The ownership rule is:

```text
Core
    owns the universal Scan Type catalog and shared scan orchestration

Plugin SDK
    owns public contracts, stable cross-boundary ids, and optional Value Type companion contracts

Platform plugin
    owns concrete Value Types, optional Scan Options, and target/native mappings

WPF host
    combines Core Scan Types with the active plugin's Value Types/options
```

For a new plugin-defined value representation, no Core Value Type enum/catalog update is required. The plugin supplies an `IMemoryValueType`, adds it to `SupportedValueTypes`, and implements any native mapping it needs. If that Value Type supports numeric ordering/delta operations it may also implement `IMemoryValueComparer`, which automatically makes the compatible Core Scan Types available. A new general comparison predicate belongs in Core rather than being duplicated by every plugin.

## Native Scan Boundary

`INativeValueScanner` is an optional acceleration boundary for a backend that can perform scanning on the target/server rather than requiring Core to read the entire range.

`NativeValueScanRequest` carries neutral data:

- the active plugin Value Type id;
- the selected Core Scan Type id;
- resolved value width;
- resolved alignment;
- encoded input values;
- immutable plugin-selected `MemoryScanOptions`.

The native scanner returns `NativeValueScanResult` entries containing:

- absolute address;
- current bytes at that address.

Core validates the returned address/width and asks the active `IMemoryValueType` to convert the current bytes to a neutral `MemoryScanValue`. This prevents Core from requiring a concrete value codec even when the scan itself is native.

A native plugin is responsible for understanding the Core Scan Type id and its own Value Type ids. The host never interprets backend compare ids or opcodes. Plugin API `2.7.0` adds an explicit mapping layer for this boundary, described below.

`INativeValueScanRefiner` is the optional legacy companion service for a backend that can retain/refine a target-side result set. `ResetAsync` releases any native resident state when New Scan, Active Target changes, disconnect, or another incompatible state transition requires it.

### Semantic native Scan Type mappings in API 2.7

Plugin API `2.7.0` adds the optional `INativeScanTypeMappingProvider` service and `NativeScanTypeMapping` model. A connected session can publish which Core-owned predicates have semantically equivalent native implementations. Each mapping declares:

- `CoreScanTypeId`;
- an opaque plugin-owned `NativeScanTypeId`;
- First Scan and/or Next Scan availability;
- optional supported Value Type ids.

The mapping table is a **semantic contract**, not merely an optimization hint. A plugin must publish a mapping only when the native operation preserves Core behavior for the declared stage and Value Types. Similar names are insufficient when edge semantics differ.

Core `NativeScanTypeResolver` is the shared delegation gate. For sessions that publish mappings, the host asks for a native scanner only when the selected Core Scan Type, stage, and Value Type match a declaration. A missing mapping routes directly to the shared Core scanner. The native scanner can still reject a concrete runtime shape with `NotSupportedException` for reasons such as missing server capabilities, endianness, alignment, resource refusal, or another backend-specific restriction; the host then uses Core fallback without changing the selected Scan Type.

`NativeScanTypeId` remains opaque to Core/WPF. A PS5 plugin can use `ps5debug-ng.compare.9`, a future Windows plugin can use a process-scanner enum, and an Xbox plugin can use a protocol opcode without changing the host. The plugin is responsible for translating the selected Core Scan Type into its own backend request.

Older compatible 2.x plugins that do not implement `INativeScanTypeMappingProvider` retain the legacy try-native-then-fallback behavior so API minor-version compatibility is preserved.

See [`NATIVE_SCAN_TYPE_MAPPING.md`](NATIVE_SCAN_TYPE_MAPPING.md) for the complete ownership, equivalence, fallback, and current PS5 mapping rules.

### Batched native results in API 2.2

API `2.2.0` adds an optional streaming surface for backends whose native scans can return result counts too large to materialize safely as one managed list:

```text
INativeValueScanStreamProvider
INativeValueScanResultStream
INativeValueScanStreamRefiner
INativeValueScanCandidateSource
NativeValueScanResultBatch
```

`INativeValueScanResultStream` advertises the backend's source-result count and yields bounded `NativeValueScanResultBatch` values containing absolute addresses plus packed current-value bytes. Core can validate and persist those batches incrementally while keeping only a bounded UI preview.

`INativeValueScanCandidateSource` is the neutral input boundary for streaming refinement. The Core disk-backed `IScanResultSet` implements this contract, exposing a 64-bit count and batched address enumeration without constructing a giant `IReadOnlyList<ulong>`.

These services are optional. A plugin targeting an older 2.x API remains valid, and the host can use legacy list/native or shared-reader fallbacks when streaming is unavailable. The public API still does not prescribe a concrete Value Type or Scan Type vocabulary.

### Backend-resident complete results in API 2.9

Plugin API `2.9.0` adds optional `INativeValueScanResidentResultSet`. The contract describes complete-result behavior without exposing a platform protocol:

- `Count` is the complete authoritative 64-bit survivor count;
- `ValueSize` and `Alignment` remain stable for the handle lifetime;
- `IsAuthoritative` tells Core whether native membership already matches the requested Core semantics without host-side post-filtering;
- `ReadResultBatchesAsync(...)` reads only a requested bounded window and may include Previous-value bytes;
- inherited `ReadAddressBatchesAsync(...)` preserves the candidate-source surface;
- `CanRefine(...)` is a non-mutating query for whether the current resident generation can accept a specific native Next Scan;
- `DisposeAsync()` releases handle/backend state according to the plugin implementation.

`NativeValueScanResultBatch` also gains an optional packed Previous-value payload with the same record count/value width as the current-value payload. The original current-only constructor remains available, so API 2.2 streaming producers do not need to opt into resident result behavior. Consumers check `HasPreviousValueData` before requesting Previous bytes.

Core validates resident width, alignment, readable-map membership, strict ascending address order, and requested-window completeness. A backend-specific session id, storage path, command opcode, or protocol structure never crosses the public contract. If native refinement is not semantically or operationally available, Core can materialize the complete resident set through bounded windows into its existing disk-backed result writer and continue with shared refinement.

## Debugger Contracts in API 2.12

Plugin API `2.12.0` introduces the first public debugger boundary. The connected `ITargetSession` may expose `IDebuggerProvider` when the plugin advertises `TargetCapabilities.Debugger`. `IDebuggerProvider.AttachAsync(...)` creates an attached `IDebuggerSession` bound to the requested neutral `TargetProcess`.

`IDebuggerSession` owns the attached execution lifecycle and exposes:

- neutral execution state;
- debugger events;
- Pause;
- Continue;
- explicit Detach;
- asynchronous disposal;
- optional attached-session service lookup.

The optional debugger services are separate by subsystem; thread enumeration and individual-thread control are deliberately separate contracts:

```text
IDebuggerThreadService
IDebuggerThreadControlService
IDebuggerRegisterService
IDebuggerBreakpointService
IDebuggerBreakpointStateService
IDebuggerBreakpointValidationService
IDebuggerCallStackService
IDebuggerStepService
```

These services live on the attached debugger session rather than on the general target session because their state belongs to a specific debugger attachment/event stream. The host must check both the coarse target capability and the runtime service before enabling a workflow.

The neutral debugger models cover execution state, stop reasons/events, thread state, fixed-width register snapshots, semantic InstructionPointer/StackPointer/FramePointer roles, register writes, stack frames, software/hardware breakpoint requests, Execute/Read/Write/ReadWrite trigger semantics, temporary breakpoint intent, and Step Into/Over/Out requests. Backend handles, register structs, watchpoint slot rules, exception codes, and transport details remain plugin-private.

From API `2.15.0`, `DebuggerEvent.TriggeredBreakpoint` is optional event context. It is populated when the backend can identify the specific neutral breakpoint/watchpoint record that caused a stop. For data watchpoints, `InstructionPointer` remains the accessing instruction address; the watched memory address remains in `TriggeredBreakpoint.Request.Address`. Event producers that do not have or need this context continue using the original constructor and leave the property null.
From API `2.16.0`, `IDebuggerBreakpointValidationService` is an optional attached-session service for non-mutating request preflight. It accepts a complete `DebuggerBreakpointRequest` and returns `DebuggerBreakpointValidationResult` with `IsValid` plus a diagnostic message. It exists so generic host surfaces can disable impossible address actions before changing debugger state without duplicating mapped-range, protection, access, width, alignment, slot, duplicate, or pending-cleanup rules in WPF/Core. A plugin that does not expose the optional validator remains compatible; the existing `IDebuggerBreakpointService.AddBreakpointAsync(...)` path remains authoritative and may still reject a request at execution time.


`DebuggerSessionExtensions.GetRequiredService<TService>()` mirrors the target-session helper for attached-session paths where capability checks have already established that a service must exist.

The full host/Core ownership and development sequence is documented in [`DEBUGGER_ARCHITECTURE.md`](DEBUGGER_ARCHITECTURE.md).

Application `0.1.7.rev1` established the API `2.12.0` debugger surface. Rev7 advanced to `2.13.0` for neutral register encoding, rev11 to `2.14.0` for optional breakpoint state changes, and rev16 to `2.15.0` for optional triggered-breakpoint event context. Rev30 advanced to API `2.16.0` with optional breakpoint-request validation. Current application `0.1.7.rev31` keeps API `2.16.0`; Mock remains `1.0.0.rev16` and PS5 advances to `0.1.0.rev38`. Both advertise `Breakpoints`, `Watchpoints`, `CallStack`, and `StepExecution` and expose the established neutral attached-session services plus `IDebuggerBreakpointValidationService` so generic address shortcuts can be gated by the same plugin-owned rules used by the real add paths. The PS5 rev35 software-breakpoint stop correction is deliberately plugin-private: it reconciles the current backend's interrupt-packet snapshot with later live register state without changing any public model or service signature. Rev26's breakpoint-aware Step Over correction is likewise host-private: it retains the existing neutral `DisassembledInstruction` before Software/Execute breakpoint installation and uses it only for the matching `TriggeredBreakpoint` stop. Rev27's composed-operation ownership/interruption cleanup is host-private, while PS5 rev36's defensive software-breakpoint restoration and rev38's explicit hardware-watchpoint clearing before detach/disposal remain plugin-private. Breakpoint/watchpoint legality remains plugin-owned. Call-stack unwinding is likewise plugin-owned, while the host consumes only neutral `DebuggerStackFrame` records. `IDebuggerStepService` supplies the native Step Into primitive; the host composes Step Over, Step Out, and Run to Address from the verified Disassembler, call-frame, and temporary software-breakpoint layers rather than forcing every backend to duplicate those workflows.

For future plugins, `ThreadEnumeration` means the attached debugger session must expose `IDebuggerThreadService`; `ThreadControl` means it must additionally expose `IDebuggerThreadControlService`. The host requires both the advertised capability and the runtime service before enabling either workflow. The standard thread-control UI assumes an enumerable thread set, so a built-in/plugin implementation intended for that UI should advertise `ThreadControl` together with `ThreadEnumeration`. Thread identifiers remain opaque neutral unsigned ids, and backend terms such as LWP ids, native thread handles, priorities, scheduler objects, or protocol records stay private to the plugin. A backend that can enumerate but cannot safely suspend/resume individual threads should advertise only `ThreadEnumeration`.

## Register Value Encoding in API 2.13

Plugin API `2.13.0` adds `DebuggerRegisterValueEncoding` to `DebuggerRegister`. `Bytes` leaves the payload opaque; `UnsignedLittleEndian` and `UnsignedBigEndian` allow a generic host to format and parse numeric values at the declared byte width. The existing seven-parameter `DebuggerRegister` constructor remains present with the same signature and defaults to `Bytes`; the encoding-aware overload is additive. `DebuggerRegisterRole` remains the semantic route for InstructionPointer, StackPointer, and FramePointer behavior; consumers must not infer those roles from register names. Writability is still declared per register row, and a plugin may advertise `RegisterAccess` while exposing a read-only snapshot backend.



## Process Control

`IProcessControl` provides neutral suspend/resume operations. The Scan panel exposes **Pause target while scanning** only when the active plugin advertises both `ProcessSuspend` and `ProcessResume` and the connected session supplies the service.

The host owns the orchestration and resumes the target in a `finally` path after scan success, cancellation, or failure.

## Plugin Discovery and Validation

`PluginHost` recursively discovers plugin assemblies, loads them through collectible `PluginLoadContext` instances, finds public `ITargetPlugin` entry types, instantiates them, and validates their declarations before exposing them to the application.

Validation includes:

- metadata and Plugin API compatibility;
- duplicate plugin ids;
- connection-setting structure and duplicate keys;
- non-null Value Type collections;
- non-null definition entries;
- non-empty Value Type ids and display names;
- unique Value Type ids within a plugin;
- positive Value Type fixed width when supplied;
- positive default alignment;
- a default Value Type id that belongs to the plugin's own declarations;
- non-null Scan Option collections and entries;
- unique non-empty Scan Option ids;
- non-empty, uniquely identified option choices;
- a declared default choice for every option;
- option applicability that can be evaluated against the plugin's declared Value Types;
- valid optional ChoiceList/Toggle presentation metadata and checked/unchecked toggle mappings;
- optional Core Scan Type/stage applicability that can be evaluated against at least one current Core predicate.

The host deliberately does **not** validate a plugin definition against a Core list of known concrete types. Structural validity is sufficient for discovery; behavioral correctness belongs to plugin and scanner verification.

## Built-In Plugin Declarations

### PlayStation 5

PS5 plugin `0.1.0.rev25` targets Plugin API `2.12.0` and exposes eleven reusable standard Value Type definitions and three PS5 scan options: Endianness, Alignment, and Floating-point rounding. It exposes `IDisassemblerProvider`/`Disassembly` for the unchanged plugin-local x86-64 decoder and now also exposes `IDebuggerProvider`/the coarse `Debugger` capability through a separate plugin-owned ps5debug-NG debugger transport. Endianness declares the generic Toggle presentation with checked=Little Endian and unchecked=Big Endian. Floating-point rounding declares Core Scan Type applicability so it appears only for Exact Value with a compatible Float/Double Value Type. It also implements `INativeScanTypeMappingProvider` and publishes only the Core predicates that are semantically equivalent to current ps5debug-NG native operations for the declared stage/value combinations. Its native ids and wire compare values remain PS5-plugin internals. Missing or runtime-incompatible mappings use the shared Core scanner. Large authoritative TurboScan sets additionally expose `INativeValueScanResidentResultSet`, allowing the host to keep complete survivor membership target-resident until shared/Core processing requires materialization.

### Mock

Mock plugin `1.0.0.rev8` targets Plugin API `2.12.0`, deliberately exposes the same standard Value Type set so Core-owned scan behavior can be tested deterministically without a console, and provides the synthetic `IDisassemblerProvider` fixture used to verify both the neutral disassembly contract and optional syntax-token presentation independently of PS5/x86-64. Rev7 corrects its CPU metadata to `CpuArchitecture.Unknown` while retaining 64-bit address/pointer widths and little-endian byte order, because the synthetic fixture is not a real x86-64 ISA.

The verification executable still injects a test predicate directly into the low-level `MemoryScanner` API to verify contract extensibility, but the production WPF host obtains its Scan Type list only from Core `MemoryScanTypeCatalog`.

## Extension Rule

When adding scanner functionality:

1. if the feature is a general comparison predicate that should behave the same on every platform, add it to the Core Scan Type catalog and give it a stable SDK id;
2. if the feature is a new memory representation, implement an `IMemoryValueType` in the platform plugin (or reuse an SDK standard helper);
3. implement `IMemoryValueComparer` only when that Value Type genuinely supports ordered/delta semantics;
4. keep platform-specific Scan Options in the plugin; use `IMemoryScanOptionPresentation` only when a generic toggle/choice presentation improves the UI, and `IMemoryScanOptionApplicability` when an option is meaningful only for specific Core Scan Types/stages;
5. if the backend can accelerate a Core predicate with equivalent semantics, publish that relationship through `INativeScanTypeMappingProvider`;
6. keep backend compare ids/opcodes and request translation inside the plugin;
7. use `NotSupportedException` for runtime-native shapes that must fall back to Core;
8. add deterministic tests for Core semantics, mapping declarations, native wire behavior, and fallback boundaries before advertising the path as complete.

A new plugin does not need its own copy of Exact/Changed/Increased/etc. It receives the universal Core catalog automatically and only declares its Value Types, options, services, and optional native mappings.

## Core-Owned Scan Types (0.1.3.rev25-rev26)

Rev25 moved the standard Scan Type lifecycle back to Core. Rev26 completes the current standard catalog with **Fuzzy Value** and **Unknown Initial Low Value**, bringing the production set to 13 predicates.

Plugins continue to own Value Types and may optionally implement `IMemoryValueComparer` on fixed-width Value Types that support ordered and delta comparisons. Exact/Changed/Unchanged use equality semantics; Fuzzy Value is currently Float/Double-specific; Unknown Initial Value snapshots every fixed-width candidate including zero; Unknown Initial Low Value keeps positive nonzero values up to a positive upper limit.

Plugin API `2.7.0` separates native acceleration from predicate ownership. `INativeScanTypeMappingProvider` tells Core which of those 13 predicates a backend can execute natively without changing their meaning. Unsupported mappings and incompatible runtime shapes automatically use the shared Core scanner. This allows future PC, Xbox 360, console, emulator, or offline-dump plugins to reuse the same Scan Type catalog while independently accelerating whatever their backend supports.

## Host `0.1.7.rev30` Compatibility Note

Rev30 advances the public Plugin SDK to `2.16.0` only for optional `IDebuggerBreakpointValidationService` and `DebuggerBreakpointValidationResult`. Compatible plugins targeting older 2.x minors remain loadable. If an attached debugger session omits the optional validator, existing Debugger Add/Remove/Enable/Disable behavior remains available, but the new Scan Results/Saved Addresses quick-address actions stay disabled because the host cannot safely preflight platform-specific legality. Mock `1.0.0.rev16` and PS5 `0.1.0.rev37` introduced the service. Current PS5 `0.1.0.rev38` retains that validator unchanged. Rev31 does not change the contract; PS5 rev38 is a plugin-private teardown correction that explicitly clears client-owned hardware watchpoints before backend detach/disposal.


## Host `0.1.7.rev31` Compatibility Note

Rev31 keeps Plugin API `2.16.0` unchanged. No public model, capability, or service is added for the PS5 watchpoint teardown correction. PS5 plugin `0.1.0.rev38` performs the cleanup entirely behind the existing `IDebuggerSession` / `IDebuggerBreakpointService` boundary by disabling every client-owned backend-active or staged hardware-watchpoint slot before detach/disposal. Compatible plugins targeting older 2.x API minors are unaffected.
