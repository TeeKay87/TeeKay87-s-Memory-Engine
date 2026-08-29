# TeeKay87's Memory Engine

TeeKay87's Memory Engine is a modular Windows desktop application intended to become a shared environment for memory scanning, debugging, memory inspection, and cheat development across multiple target platforms.

The application is being built in C# and WPF around a platform-neutral Core and a public Plugin SDK. Platform-specific behavior belongs in plugins so the same application and user interface can eventually support targets such as PlayStation 5, Windows PC, Xbox 360, emulators, and offline memory dumps without duplicating the main application.

## Current Status

The current build is **0.1.0.rev2** and retains the initial architecture and plugin foundation with the WPF application startup namespace conflict corrected.

Implemented functionality currently includes:

- a multi-project solution separating the WPF application, Core, Plugin SDK, platform plugins, and verification tests;
- centralized application title, version, revision, and active feature information through `AppInfo`;
- a capability-based Plugin SDK with neutral target architecture, process, memory-region, connection, and memory-access contracts;
- runtime discovery of plugin assemblies from the application's `Plugins` directory;
- isolated plugin loading through collectible `AssemblyLoadContext` instances;
- plugin metadata validation and duplicate plugin-id detection;
- a capability-driven WPF shell that lists discovered plugins and displays their metadata and supported operations;
- a deterministic in-memory development plugin used to exercise the same contracts that future live-target plugins will implement;
- dependency-free foundation verification tests covering plugin metadata, capabilities, process discovery, memory maps, memory reads, memory writes, and runtime plugin discovery.

This revision does **not** yet connect to PlayStation 5 or any other live target. Live PS5 support through ps5debug-NG is the next planned platform implementation.

## Architecture

The solution is organized as follows:

```text
TeeKay87.MemoryEngine.sln
│
├── src/
│   ├── TeeKay87.MemoryEngine.App/
│   │   └── WPF application and presentation layer
│   │
│   ├── TeeKay87.MemoryEngine.Core/
│   │   └── Shared application infrastructure and plugin hosting
│   │
│   ├── TeeKay87.MemoryEngine.PluginSdk/
│   │   ├── Capabilities/
│   │   ├── Contracts/
│   │   └── Models/
│   │
│   └── Plugins/
│       └── TeeKay87.MemoryEngine.Platform.Mock/
│           └── Deterministic in-memory development target
│
├── tests/
│   └── TeeKay87.MemoryEngine.Tests/
│
├── docs/
│   ├── architecture/
│   └── testing/
│
├── Directory.Build.props
├── README.md
└── CHANGELOG.md
```

The architectural rule is intentionally simple:

> If a feature describes what a memory-development tool does, it should normally be shared. If it describes how a particular target performs that operation, it should normally belong to that target's plugin.

The detailed early-development design is documented in [`docs/architecture/EARLY_DEVELOPMENT_ARCHITECTURE.md`](docs/architecture/EARLY_DEVELOPMENT_ARCHITECTURE.md). The implemented Plugin SDK foundation is documented in [`docs/architecture/PLUGIN_SDK_FOUNDATION.md`](docs/architecture/PLUGIN_SDK_FOUNDATION.md).

## Plugin Model

A platform plugin implements `ITargetPlugin` from `TeeKay87.MemoryEngine.PluginSdk`.

Each plugin declares:

- a stable plugin id;
- display name;
- platform;
- backend or transport;
- plugin version;
- target architecture;
- supported capabilities.

The initial capability model includes connection, process enumeration, foreground-process discovery, memory map access, memory read/write, allocation/protection, suspend/resume, native scanning, AOB scanning, debugger functions, assembly/disassembly, and cheat-related operations.

Capabilities are flags rather than platform checks. The application therefore asks whether the active plugin supports an operation instead of checking whether the target is specifically a PS5, Windows PC, Xbox 360, or another platform.

### Session Services

Connected targets expose functionality through `ITargetSession.GetService<TService>()`.

The current Plugin SDK defines services for:

- `IProcessProvider`;
- `IForegroundProcessProvider`;
- `IMemoryMapProvider`;
- `IMemoryReader`;
- `IMemoryWriter`.

This service-based design allows the SDK to grow without forcing every target session to implement unsupported operations.

## Development Plugin

`TeeKay87.MemoryEngine.Platform.Mock` is an in-memory development target. It exists so Core and UI behavior can be implemented and verified without requiring a physical console or attaching to a local process.

The mock target exposes one process:

```text
TestGame.exe
```

Its memory begins at:

```text
0x10000000
```

The initial deterministic values include:

| Value | Address | Initial value |
| --- | --- | ---: |
| Health | `0x10000100` | `100.0` (`Float32`) |
| Ammo | `0x10000104` | `30` (`Int32`) |
| Money | `0x10000108` | `5000` (`Int32`) |

These values are development fixtures, not user-facing game data. They allow future scanner, saved-address, freeze, memory-viewer, and regression tests to run against a stable target.

## Requirements

Development currently targets:

- Windows 10 or Windows 11;
- .NET 9 SDK;
- Visual Studio 2022 with the .NET desktop development workload, or another environment capable of building WPF projects targeting .NET 9.

The WPF application targets `net9.0-windows`. The non-UI Core, Plugin SDK, mock plugin, and verification project target `net9.0`.

## Build

From the repository root:

```powershell
dotnet build TeeKay87.MemoryEngine.sln
```

A successful application build also builds the mock platform plugin and copies its assembly into the application's output `Plugins` directory.

For a Release build:

```powershell
dotnet build TeeKay87.MemoryEngine.sln -c Release
```

## Run the Foundation Verification

The current tests deliberately avoid third-party test-framework dependencies. They are a small executable verification suite so the architectural foundation can be exercised with only the .NET SDK.

Run them with:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj
```

The verification suite checks:

1. mock plugin metadata and capability declarations;
2. target connection and process enumeration;
3. foreground-process discovery;
4. memory-region enumeration;
5. deterministic memory reads;
6. memory writes followed by read-back verification;
7. discovery of the mock plugin through the runtime `PluginHost`.

## Run the Application

From the repository root:

```powershell
dotnet run --project src/TeeKay87.MemoryEngine.App/TeeKay87.MemoryEngine.App.csproj
```

At this stage the application opens the plugin foundation workspace. It automatically scans the `Plugins` directory next to the built executable, selects the first discovered plugin, and displays its metadata and capabilities.

Use **Reload Plugins** to repeat discovery after changing plugin assemblies on disk.

## Plugin Deployment

Platform plugin assemblies are discovered from:

```text
<application-directory>/Plugins/
```

The current discovery implementation scans the top level of that directory for `.dll` files. Public, non-abstract classes implementing `ITargetPlugin` and providing a public parameterless constructor are instantiated as plugins.

Each plugin id must be unique. Invalid metadata, duplicate ids, incompatible assemblies, and plugin-construction failures are returned as discovery errors and displayed by the application shell.

The Plugin SDK assembly is shared with the application rather than loaded as a private plugin copy. Other plugin dependencies can be resolved from the plugin assembly location through the plugin load context.

## Current Limitations

The following features are intentionally not implemented yet:

- PlayStation 5 / ps5debug-NG connection;
- live process selection;
- live memory read/write;
- scanner workflows;
- scan-result storage and virtualization;
- list/table export;
- saved addresses and freeze operations;
- memory viewer;
- disassembly and assembly;
- debugger, breakpoints, watchpoints, registers, threads, and call stacks;
- pointer scanning;
- cheat-project editing and platform export formats;
- Windows PC or Xbox 360 plugins.

Those features should be added on top of the current contracts without introducing platform-specific assumptions into the shared application.

## Documentation

Project documentation is stored under `docs/`:

- `docs/architecture/EARLY_DEVELOPMENT_ARCHITECTURE.md` — long-term architecture and early development direction;
- `docs/architecture/PLUGIN_SDK_FOUNDATION.md` — contracts and decisions implemented by the current foundation;
- `docs/testing/REV1_FOUNDATION_VERIFICATION.md` — verification scope and recorded results for the initial foundation revision;
- `docs/testing/REV2_WPF_APPLICATION_COMPILE_FIX.md` — compile-fix verification for the current revision.

Revision history and implementation changes are recorded in `CHANGELOG.md`.
