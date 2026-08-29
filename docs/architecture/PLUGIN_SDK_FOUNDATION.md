# Plugin SDK Foundation

## Purpose

This document records the plugin architecture implemented for the initial foundation of TeeKay87's Memory Engine.

It complements the broader `EARLY_DEVELOPMENT_ARCHITECTURE.md` guide by describing the concrete contracts that now exist in source code. These contracts are intentionally limited to the foundation required before live PS5 integration and should be extended carefully as new subsystems are implemented.

## Project Boundary

The implemented solution separates responsibilities into the following projects:

| Project | Responsibility |
| --- | --- |
| `TeeKay87.MemoryEngine.App` | WPF presentation layer and application composition |
| `TeeKay87.MemoryEngine.Core` | Shared application infrastructure and plugin hosting |
| `TeeKay87.MemoryEngine.PluginSdk` | Public contracts, capability flags, and platform-neutral target models |
| `TeeKay87.MemoryEngine.Platform.Mock` | Deterministic development platform implementation |
| `TeeKay87.MemoryEngine.Tests` | Dependency-free foundation verification |

The Plugin SDK does not reference Core or the WPF application. Platform plugins are therefore able to depend on the SDK without taking a dependency on the host application.

## Plugin Entry Point

A platform plugin exposes one or more public, non-abstract classes implementing `ITargetPlugin`.

The current entry-point contract provides:

```csharp
PluginMetadata Metadata { get; }
TargetCapabilities Capabilities { get; }
Task<ITargetSession> ConnectAsync(
    TargetConnectionOptions options,
    CancellationToken cancellationToken);
```

A plugin entry type currently requires a public parameterless constructor because plugin discovery instantiates it through reflection.

This requirement is deliberately simple for the foundation. A future manifest, factory, dependency-injection, or package system can replace construction behavior without moving target-specific code into Core.

## Plugin Metadata

`PluginMetadata` distinguishes platform information from backend information.

The fields are:

- `Id` — stable unique plugin identifier;
- `Name` — user-facing plugin name;
- `Platform` — target platform;
- `Backend` — transport, protocol, runtime, or backend implementation;
- `Version` — plugin implementation version;
- `Description` — user-facing description;
- `Architecture` — target CPU, pointer width, address width, and endianness.

This distinction is important for the first planned live implementation:

```text
Platform: PlayStation 5
Backend:  ps5debug-NG
```

Core must not use the backend name as a substitute for platform identity.

## Capability Model

`TargetCapabilities` is a flags enum. The host and UI use these flags to determine which operations a plugin claims to support.

The initial capability surface contains:

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
- debugger session support;
- breakpoints and watchpoints;
- register access;
- thread enumeration;
- call stack;
- step execution;
- cheat application;
- cheat validation;
- cheat export.

A capability flag describes availability. It does not define the full API for that subsystem. Subsystem contracts should be introduced when implementation begins and the required behavior is understood well enough to define a stable interface.

## Target Sessions and Services

A successful connection returns `ITargetSession`.

The session exposes:

- the plugin metadata associated with the connection;
- target architecture;
- connection state;
- asynchronous disposal;
- typed service lookup through `GetService<TService>()`.

The initial service contracts are:

```text
IProcessProvider
IForegroundProcessProvider
IMemoryMapProvider
IMemoryReader
IMemoryWriter
```

Using typed services avoids creating a large `ITargetSession` interface where every platform must implement every planned feature.

For example, a read-only memory dump could provide:

```text
IProcessProvider
IMemoryMapProvider
IMemoryReader
```

without exposing `IMemoryWriter`.

A live PS5 session may expose additional services as its implementation grows.

`TargetSessionExtensions.GetRequiredService<TService>()` is provided for code paths where a capability has already been checked and the service is mandatory. Missing required services produce an explicit `NotSupportedException`.

## Initial Neutral Models

The Plugin SDK currently defines the minimum models needed for the first live-target milestone.

### TargetArchitecture

Contains:

- CPU architecture;
- pointer width in bits;
- address width in bits;
- endianness.

This prevents Core from assuming that every target behaves like x86-64 little-endian memory.

### TargetProcess

Contains:

- numeric process id;
- process name;
- optional display name.

Platform-specific fields must not be added merely because the first live plugin needs them. If future features require richer process metadata, neutral fields or plugin-owned metadata should be evaluated first.

### MemoryRegion

Contains:

- base address;
- size;
- memory protection flags;
- optional region name;
- optional module name.

The protection flags currently cover read, write, execute, guard, private, and shared characteristics.

### MemoryValueType

Defines the initial common memory value types planned for scanner/value infrastructure:

- signed/unsigned 8-, 16-, 32-, and 64-bit integers;
- `Float32` and `Float64`;
- byte arrays;
- ASCII;
- UTF-8;
- UTF-16.

Encoding/decoding logic is intentionally not implemented in this revision. It belongs to the later shared value-encoding milestone.

## Plugin Discovery

`PluginHost` is implemented in Core.

Discovery currently works as follows:

1. Resolve the configured plugin directory.
2. Enumerate top-level `.dll` files.
3. Create a collectible `PluginLoadContext` for each candidate assembly.
4. Load the assembly and inspect its loadable public types.
5. Identify non-abstract `ITargetPlugin` implementations.
6. Construct each plugin through its public parameterless constructor.
7. Validate required metadata.
8. Reject duplicate plugin ids.
9. Return successful plugins and structured discovery errors separately.
10. Retain load contexts only for assemblies that contributed accepted plugins.

Reloading plugins first releases the previous plugin references in the application, then unloads the old collectible load contexts and performs discovery again.

## Assembly Identity and Dependency Resolution

Plugins must share the host's `TeeKay87.MemoryEngine.PluginSdk` assembly identity. Loading a private SDK copy would cause interface type identity to differ between host and plugin even if the code is identical.

`PluginLoadContext` therefore returns control to the default load context when the Plugin SDK assembly is requested.

Other managed and unmanaged dependencies are resolved with `AssemblyDependencyResolver` relative to the plugin entry assembly.

This is the foundation for future platform plugins that require backend-specific client libraries while keeping those dependencies outside Core.

## Mock Target

The development plugin is intentionally implemented as a real target session rather than a metadata-only placeholder.

It provides:

```text
Process:      TestGame.exe
Process ID:   1001
Memory base:  0x10000000
Memory size:  0x00010000
Architecture: x64, 64-bit pointers, 64-bit addresses, little-endian
```

Known values are initialized at stable addresses:

```text
Health  0x10000100  Float32  100.0
Ammo    0x10000104  Int32    30
Money   0x10000108  Int32    5000
```

Reads and writes operate on the backing memory buffer with range validation. The session exposes process, foreground-process, memory-map, memory-reader, and memory-writer services.

The mock target should remain available throughout development. Future generic subsystems should use it where practical for deterministic tests before they are verified against live targets.

## WPF Integration

The application shell does not contain platform-name checks.

At startup it:

1. resolves `<application-directory>/Plugins`;
2. calls `PluginHost.Discover`;
3. creates presentation models for accepted plugins;
4. displays plugin metadata;
5. enumerates and displays the plugin's capability flags;
6. surfaces discovery errors without terminating the application.

The mock plugin is built as a separate platform assembly and copied into the application's plugin output directory by the application project build.

## Rules for the PS5 Plugin

The upcoming PlayStation 5 implementation must build on this foundation rather than bypass it.

The PS5 plugin should:

- reference `TeeKay87.MemoryEngine.PluginSdk`;
- expose a stable PS5/ps5debug-NG plugin id;
- report `Platform` as PlayStation 5 and `Backend` as ps5debug-NG;
- report x86-64, 64-bit pointer/address widths, and correct endianness;
- translate ps5debug-NG process information into `TargetProcess`;
- translate ps5debug-NG memory maps into `MemoryRegion`;
- implement memory operations through the shared memory service interfaces;
- keep packet definitions, command ids, socket behavior, backend errors, and PS5-specific metadata inside the platform project.

No ps5debug-NG protocol type should be referenced by `TeeKay87.MemoryEngine.Core` or the WPF presentation layer.

## Extension Rules

When adding new functionality:

1. Determine whether the operation is generic or target-specific.
2. Add neutral models to the Plugin SDK only when they are required across the plugin boundary.
3. Keep high-level algorithms in Core when they can operate through neutral services.
4. Add capability flags when support may differ between plugins.
5. Add a service contract when Core needs to invoke a target-specific low-level operation.
6. Add deterministic mock behavior when practical so the new Core path can be regression-tested without hardware.
7. Do not expose ps5debug-NG, Windows API, Xbox 360, or other backend-specific types through public neutral contracts.

The Plugin SDK should remain deliberately smaller than the complete application model. Not every Core type needs to cross the plugin boundary.
