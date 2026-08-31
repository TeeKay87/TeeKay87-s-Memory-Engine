# Plugin SDK Foundation

## Purpose

This document describes the current platform-plugin boundary used by TeeKay87's Memory Engine.

The Plugin SDK is deliberately platform-neutral. It defines what the host needs to know about a target plugin without exposing ps5debug-NG, Windows APIs, Xbox 360 protocols, or other backend-specific implementation types to Core or WPF.

Plugin-specific implementation and test documentation belongs under that plugin's dedicated directory in `docs/plugins/`.

## Project Boundary

The solution currently separates responsibilities into:

| Project | Responsibility |
| --- | --- |
| `TeeKay87.MemoryEngine.App` | WPF presentation and application composition |
| `TeeKay87.MemoryEngine.Core` | Shared host infrastructure and plugin discovery |
| `TeeKay87.MemoryEngine.PluginSdk` | Public contracts, capability flags, compatibility metadata, and neutral target models |
| platform plugin projects | Target-specific connection and operation implementations |
| `TeeKay87.MemoryEngine.Tests` | Dependency-free verification executable |

The Plugin SDK does not reference Core or WPF. A platform plugin therefore depends only on the SDK and its own backend dependencies.

## Independent Version Domains

Three version domains are intentionally separate.

### Host Application Version

The main application uses its own `AppInfo` version and revision, for example:

```text
0.1.1.rev1
```

This version describes TeeKay87's Memory Engine as a whole.

### Plugin Version

Every plugin owns its own semantic version and revision. A plugin does not inherit the host application's version.

Examples can therefore coexist as:

```text
Host application:  0.1.1.rev1
Plugin A:          1.0.0.rev1
Plugin B:          0.1.0.rev1
```

A plugin changes its version/revision only when that plugin changes.

Each built-in plugin keeps its version values in a plugin-local information class rather than duplicating displayed values across its source.

### Plugin API Version

The Plugin SDK has a separate compatibility version exposed by `PluginApiInfo`.

The current Plugin API version is:

```text
1.2.0
```

Plugin API `1.1.0` was introduced by host `0.1.2.rev4` to add optional native value-scanning and process-control services. Plugin API `1.2.0` was introduced by host `0.1.2.rev5` to add optional native refinement/session-reset behavior through `INativeValueScanRefiner`. Host `0.1.3.rev1` keeps Plugin API `1.2.0`: the existing `MemoryValueType` identifiers and generic `NativeValueScanRequest` already cover the full ps5debug-NG value-type expansion, so no public SDK contract change is required.

Each plugin declares the Plugin API version it targets in `PluginMetadata.ApiVersion`.

The current compatibility rule is:

- major version must match the host Plugin API major version;
- a plugin may target the same or an older minor version within that major version;
- a plugin targeting a newer minor version than the host is rejected during discovery.

This allows the host application, individual plugins, and the public contract to evolve independently.

## Plugin Entry Point

A platform plugin exposes one or more public, non-abstract classes implementing `ITargetPlugin`.

The current contract provides:

```csharp
PluginMetadata Metadata { get; }
TargetCapabilities Capabilities { get; }
IReadOnlyList<TargetConnectionSettingDefinition> ConnectionSettings { get; }
Task<ITargetSession> ConnectAsync(
    TargetConnectionOptions options,
    CancellationToken cancellationToken);
```

A plugin entry type currently requires a public parameterless constructor because `PluginHost` creates it through reflection.

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

`PluginMetadata.DisplayVersion` combines the plugin's own semantic version and revision using:

```text
<version>.rev<revision>
```

Platform and backend are separate concepts. For example, a target platform may be PlayStation 5 while its current backend is ps5debug-NG.

## Connection Settings

Connection requirements are plugin-owned.

`ITargetPlugin.ConnectionSettings` exposes a collection of `TargetConnectionSettingDefinition` instances. A definition currently contains:

- `Key` — stable option key used to construct `TargetConnectionOptions`;
- `Label` — user-facing field label;
- `Description` — optional help text;
- `DefaultValue` — optional initial value;
- `IsRequired` — whether the generic host should reject an empty value before connecting.

The WPF application renders these fields without checking the target platform name.

A plugin that requires no input returns an empty settings collection.

The plugin remains responsible for backend-specific validation such as legal port ranges, address syntax, credentials, or target-specific requirements.

The connection-setting model can be extended later with typed controls or richer validation metadata when a real need appears. The current contract intentionally implements only what the first connection workflows require.

## Capability Model

`TargetCapabilities` is a flags enum. The host and UI use these flags to determine which operations a plugin implementation actually provides.

The current capability surface includes:

- connection;
- process enumeration;
- foreground-process discovery;
- memory-region enumeration;
- memory read/write;
- memory allocation and protection;
- process suspend/resume;
- native value scanning;
- AOB scanning;
- native pointer scanning;
- disassembly and assembly;
- debugger sessions;
- breakpoints and watchpoints;
- register access;
- thread enumeration;
- call stack;
- step execution;
- cheat application;
- cheat validation;
- cheat export.

A backend may support an operation that its Memory Engine plugin does not yet implement. In that case the plugin must not advertise the capability yet.

Capability flags describe availability. Full subsystem contracts are introduced only when Core needs to invoke the operation and the behavior is understood well enough to define a useful neutral interface.

## Target Sessions and Services

A successful connection returns `ITargetSession`.

A session exposes:

- plugin metadata;
- target architecture;
- connection state;
- asynchronous disposal;
- typed service lookup through `GetService<TService>()`.

The current neutral service contracts are:

```text
IProcessProvider
IForegroundProcessProvider
IMemoryMapProvider
IMemoryReader
IMemoryWriter
INativeValueScanner
INativeValueScanRefiner
IProcessControl
```

Using typed services avoids one large target-session interface where every plugin must implement operations it does not support.

`TargetSessionExtensions.GetRequiredService<TService>()` is available for code paths where capability checks have already established that a service must exist.

### INativeValueScanner

`INativeValueScanner` is an optional acceleration boundary for platforms/backends that can perform a value comparison on the target rather than requiring Core to transfer every scanned byte. The service accepts the `TargetProcess`, Core's neutral scannable `IReadOnlyList<MemoryRegion>`, and a neutral `NativeValueScanRequest`, then returns matching absolute addresses. Passing the neutral region set lets the backend preserve the same scan boundaries chosen by Core without moving platform-specific memory-map data into the SDK.

The current request model carries:

- `MemoryValueType`;
- `ValueScanComparison`;
- encoded comparison bytes;
- requested alignment.

Core remains responsible for selecting eligible neutral regions, normalizing returned addresses into shared `MemoryScanResult` objects, enforcing common result limits, and implementing fallback scanning when the session does not expose the service. A plugin may advertise `NativeValueScanning` as an implemented plugin capability while a connected session conditionally returns no service when runtime backend negotiation shows that the remote server lacks the required optional protocol support.

### INativeValueScanRefiner

`INativeValueScanRefiner` is an optional companion to `INativeValueScanner` for backends that can keep an initial survivor set on the target and narrow that set without uploading every candidate from the host. The service receives the current `TargetProcess`, the host's previous absolute address list, the same neutral `NativeValueScanRequest`, and a cancellation token. It returns the absolute addresses that survive the refinement.

The host still owns shared scan-session semantics. Core validates returned addresses against the previous host result set and constructs new `MemoryScanResult` objects with `CurrentValue` from the requested Exact Value and `PreviousValue` from the preceding host result. If native refinement is unavailable or the backend reports that its resident session can no longer be used, the host falls back to the existing shared Core reader-based Next Scan.

`ResetAsync` lets New Scan or Active Target replacement release target-side resident scan state without introducing backend-specific cleanup commands into WPF/Core.

### IProcessControl

`IProcessControl` provides neutral asynchronous `SuspendAsync` and `ResumeAsync` operations for a `TargetProcess`. The current Scan workflow uses it only when both `ProcessSuspend` and `ProcessResume` are advertised.

The contract deliberately contains no debugger/session or platform-specific signal semantics. A plugin owns how process suspension is performed.

## Initial Neutral Models

### TargetArchitecture

Contains:

- CPU architecture;
- pointer width;
- address width;
- endianness.

Core must not assume every target uses x86-64 or little-endian data.

### TargetProcess

Contains:

- numeric process id;
- process name;
- optional display name.

Backend-specific process fields should remain inside the plugin until a shared requirement justifies a neutral model extension.

### MemoryRegion

Contains:

- base address;
- size;
- memory protection flags;
- optional region name;
- optional module name.

### MemoryValueType

Defines the common value-type identifiers used by scanner/value infrastructure. Host `0.1.3.rev1` actively uses `Int8`, `UInt8`, `Int16`, `UInt16`, `Int32`, `UInt32`, `Int64`, `UInt64`, `Float32`, `Float64`, and `ByteArray` for Exact Value scanning. `Ascii`, `Utf8`, and `Utf16` remain reserved for later string-oriented tooling because they are not ps5debug-NG scan value types.

Parsing, target-endian encoding, display formatting, alignment, and exact matching live in shared Core. Platform transports receive only the neutral enum plus encoded value bytes through `NativeValueScanRequest`.

## Plugin Discovery

`PluginHost` is implemented in Core.

Discovery currently performs the following steps:

1. resolve the configured plugin directory;
2. enumerate top-level `.dll` candidates;
3. create a collectible `PluginLoadContext` per candidate;
4. load the assembly and inspect public loadable types;
5. find non-abstract `ITargetPlugin` implementations;
6. instantiate plugins through public parameterless constructors;
7. validate required metadata;
8. validate plugin revision and Plugin API compatibility;
9. validate connection-setting keys and labels;
10. reject duplicate plugin ids;
11. return accepted plugins and discovery errors separately;
12. retain load contexts only for assemblies that contributed accepted plugins.

Reloading first releases application references and target sessions, then unloads prior plugin contexts before discovery runs again.

## Assembly Identity and Dependency Resolution

Plugins share the host's `TeeKay87.MemoryEngine.PluginSdk` assembly identity.

`PluginLoadContext` returns control to the default load context when the Plugin SDK assembly is requested. Other managed and unmanaged dependencies are resolved relative to the plugin assembly with `AssemblyDependencyResolver`.

This prevents duplicate SDK type identities while allowing platform plugins to carry backend-specific dependencies later.

## WPF Integration

The application does not contain platform-name checks for plugin discovery, metadata, capabilities, or connection fields.

For the selected plugin, the current shell can display:

- platform and backend;
- independent plugin version/revision;
- targeted Plugin API version;
- architecture;
- assembly path;
- capability flags;
- plugin-defined connection fields;
- connection/disconnection state and errors;
- a target-process list when the plugin advertises `ProcessEnumeration` and the connected session exposes `IProcessProvider`;
- optional preferred-process selection through `IForegroundProcessProvider` when `ForegroundProcess` is advertised; this only chooses the Target Process row and does not implicitly replace the Active Target;
- a row selection that is kept separate from the explicit active target selected for later memory operations;
- active-target memory-map status when the plugin advertises `MemoryRegionEnumeration` and the connected session exposes `IMemoryMapProvider`;
- capability-driven raw-memory read/write inspectors when the plugin advertises `MemoryRead`/`MemoryWrite` and the connected session exposes `IMemoryReader`/`IMemoryWriter`;
- optional native First Scan acceleration when `NativeValueScanning` is advertised and `INativeValueScanner` is available;
- optional native Next Scan refinement when `INativeValueScanRefiner` is available for the current native scan session, with the shared Core refinement path retained as fallback;
- optional scan-time target pause when both `ProcessSuspend` and `ProcessResume` are advertised and `IProcessControl` is available.

Connection commands operate through `ITargetPlugin.ConnectAsync`. Process enumeration operates through the existing `IProcessProvider` session service and generic `TargetProcess` model. Memory-map enumeration operates through the existing `IMemoryMapProvider` service and generic `MemoryRegion` model. Raw reads and writes operate through `IMemoryReader`/`IMemoryWriter` and caller-supplied neutral byte buffers. Optional native value scans operate through `INativeValueScanner`, and process pause/resume operates through `IProcessControl`. The UI does not open sockets, parse backend process/map/read/write/scan packets, or call platform APIs directly.

The active target is presentation/session workflow state rather than a PS5-specific SDK type. When memory-region enumeration is available, activating a target loads its neutral memory map into host state; refreshing a process list refreshes the map when the same active target remains present. When memory-read or memory-write support is present, that map can be used by generic host tooling to reject clearly unmapped/non-readable reads or non-writable writes before invoking `IMemoryReader`/`IMemoryWriter`. Disconnecting clears the process selection, active target, cached active-target memory regions, and raw read/write state.

## Plugin Documentation Structure

Each plugin has its own directory under:

```text
docs/plugins/<PluginName>/
```

Only documentation belonging to that plugin should be placed in that directory.

Examples of appropriate plugin-local documentation include:

- backend protocol mappings;
- plugin implementation design;
- supported firmware/runtime information;
- plugin-specific test plans and results;
- plugin-specific capability notes;
- platform-specific compatibility information.

General Plugin SDK architecture, host behavior, shared scanner design, and cross-platform contracts remain outside plugin-specific directories.

## Extension Rules

When adding functionality:

1. determine whether the operation is generic or target-specific;
2. add neutral models to the SDK only when information must cross the plugin boundary;
3. keep high-level reusable algorithms in Core;
4. advertise capabilities only after the plugin implementation exists;
5. add service contracts only when Core needs to invoke the operation;
6. extend connection-setting metadata only when a real plugin requires richer input behavior;
7. preserve independent host, plugin, and Plugin API versioning;
8. keep plugin-specific documentation in the plugin's dedicated docs directory;
9. add deterministic mock behavior where practical for generic regression tests;
10. never expose backend protocol types through neutral public contracts.

The Plugin SDK should remain deliberately smaller than the complete application model. Not every Core type needs to cross the plugin boundary.
