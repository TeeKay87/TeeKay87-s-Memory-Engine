# Changelog

## TeeKay87's Memory Engine 0.1.0.rev2 - WPF Application Namespace Compile Fix

### Added

- Added `docs/testing/REV2_WPF_APPLICATION_COMPILE_FIX.md` documenting the Visual Studio CS0118 failure, its cause, the exact corrective change, static verification, and the remaining native-build verification step.

### Changed

- Changed `App.xaml.cs` to alias `System.Windows.Application` as `WpfApplication` and inherit from that alias. This removes the compiler ambiguity with the existing `TeeKay87.MemoryEngine.App.Application` namespace without moving or renaming the `AppInfo` namespace.
- Updated the centralized `AppInfo` revision from `1` to `2`.
- Updated the centralized `AppInfo` feature title to `WPF Application Namespace Compile Fix`.
- Updated `README.md` so the current-build description and documentation index reflect revision 2 while continuing to describe the complete current application rather than acting as revision history.

### Removed

- No application functionality, plugin contracts, project structure, or previously implemented behavior was removed in this revision.

### Cause and Compatibility

- Revision 1 contained `public partial class App : Application` inside the `TeeKay87.MemoryEngine.App` namespace while also defining the child namespace `TeeKay87.MemoryEngine.App.Application`. In that scope, the identifier `Application` could bind to the namespace rather than `System.Windows.Application`, producing compiler error CS0118.
- The fix is intentionally limited to explicit WPF type resolution. The existing namespace hierarchy, solution layout, Core, Plugin SDK, mock plugin, plugin host, and WPF UI remain otherwise unchanged.

### Verification

- Reviewed all existing Markdown documentation and every source, XAML, project, solution, and root build-configuration file from the rev1 baseline before applying the change.
- Confirmed statically that the ambiguous inheritance declaration is removed and that `App.xaml` continues to reference the same `TeeKay87.MemoryEngine.App.App` class.
- Revalidated XML/XAML well-formedness, project references, centralized revision declarations, documentation consistency, and archive contents after the change.
- The preparation environment still does not provide the .NET SDK or Windows WPF toolchain, so a native build could not be executed there. Final Visual Studio build verification remains required and is documented in `docs/testing/REV2_WPF_APPLICATION_COMPILE_FIX.md`.

## TeeKay87's Memory Engine 0.1.0.rev1 - Initial Architecture and Plugin Foundation

### Added

- Added the first structured solution, `TeeKay87.MemoryEngine.sln`, with separate projects for the WPF application, shared Core, public Plugin SDK, development platform plugin, and verification tests.
- Added `Directory.Build.props` so nullable reference types, explicit `using` requirements, current C# language support, deterministic builds, and warning-as-error behavior are applied consistently across projects.
- Added a centralized `AppInfo` component as the authoritative source for the application title, version, revision, current feature title, display version, and window title.
- Added the initial `TeeKay87.MemoryEngine.PluginSdk` project with platform-neutral contracts and models:
  - `ITargetPlugin` for platform plugin entry points;
  - `ITargetSession` for connected target sessions;
  - service-based session capability access through `GetService<TService>()`;
  - `IProcessProvider` and `IForegroundProcessProvider`;
  - `IMemoryReader`, `IMemoryWriter`, and `IMemoryMapProvider`;
  - `TargetSessionExtensions.GetRequiredService<TService>()` for explicit required-service access;
  - `PluginMetadata`, `TargetConnectionOptions`, `TargetArchitecture`, `TargetProcess`, and `MemoryRegion` models;
  - CPU architecture, endianness, memory protection, and standard memory-value type definitions.
- Added a `TargetCapabilities` flags model covering the planned cross-platform capability surface, including target connection, process discovery, memory operations, scanning, debugger features, assembly/disassembly, and cheat functionality.
- Added the initial Core plugin-hosting infrastructure:
  - top-level plugin-directory discovery;
  - isolated collectible `AssemblyLoadContext` loading;
  - dependency resolution through `AssemblyDependencyResolver`;
  - shared Plugin SDK assembly identity between the host and plugins;
  - public plugin type discovery;
  - plugin metadata validation;
  - duplicate plugin-id rejection;
  - structured discovery errors;
  - unload support when plugins are reloaded or the host is disposed.
- Added `TeeKay87.MemoryEngine.Platform.Mock`, a deterministic in-memory development plugin that implements the same SDK contracts intended for live targets.
- Added a mock target process and memory region with deterministic Health, Ammo, and Money values so future Core functionality can be developed and regression-tested without a physical console.
- Added functional mock implementations for process enumeration, foreground-process discovery, memory-region enumeration, memory reads, and memory writes.
- Added a WPF/MVVM application shell that:
  - loads plugins from the output `Plugins` directory;
  - lists discovered platform plugins;
  - displays platform, backend, plugin version, target architecture, assembly path, and advertised capabilities;
  - reloads plugins without restarting the application;
  - displays plugin-discovery failures;
  - obtains all displayed application version information from `AppInfo`.
- Added build integration that builds the mock platform plugin with the application and places its plugin assembly under the application's output `Plugins` directory.
- Added a dependency-free executable verification project covering plugin metadata, capability declarations, process discovery, foreground process discovery, memory maps, memory reads, memory writes with read-back verification, and runtime plugin discovery.
- Added `.gitignore` rules for common .NET, Visual Studio, JetBrains, test, coverage, and operating-system generated files.
- Added detailed current-state documentation to `README.md`.
- Added `docs/architecture/PLUGIN_SDK_FOUNDATION.md` documenting the implemented plugin boundary, service model, capability model, discovery behavior, and extension rules.
- Added `docs/testing/REV1_FOUNDATION_VERIFICATION.md` documenting verification scope and results for this revision.

### Changed

- Reorganized the original single-project WPF template into the multi-project structure defined by the early-development architecture guide.
- Replaced the original empty `MainWindow` with the first capability-driven application shell.
- Replaced the original `TeeKay87_s_Memory_Engine` namespace with the structured `TeeKay87.MemoryEngine.*` namespace hierarchy used by the new solution.
- Moved platform-neutral contracts out of the WPF application so future PS5, Windows, Xbox 360, emulator, and memory-dump implementations can target the same Plugin SDK.
- Established explicit separation between platform identity and backend identity in plugin metadata. A plugin can therefore identify a target as PlayStation 5 while separately identifying ps5debug-NG as the backend.
- Established session services as the extension mechanism for target operations so unsupported features are absent instead of requiring platform-specific conditionals or dummy implementations.

### Removed

- Removed the original root-level single WPF project and its empty template source files after their application role was migrated into `src/TeeKay87.MemoryEngine.App`.
- Removed template-generated unused namespace imports from the original WPF code as part of the project restructure.
- Removed the now-unnecessary `docs/.gitkeep` placeholder because the documentation tree contains real architecture and verification documents.

No previously verified application functionality was removed because the supplied baseline contained only the unimplemented WPF template.

### Compatibility and Development Impact

- The project now requires opening/building `TeeKay87.MemoryEngine.sln` rather than the original root-level `.csproj`.
- Future platform implementations must reference `TeeKay87.MemoryEngine.PluginSdk` and expose an `ITargetPlugin` implementation instead of adding platform-specific behavior directly to the WPF application or Core.
- The current plugin loader expects plugin entry assemblies to be placed directly in the application's `Plugins` directory. More advanced packaging or manifests may be added later without changing the Core/platform separation established here.
- The current revision intentionally does not include ps5debug-NG code. PS5 transport/protocol work remains isolated to the upcoming PS5 platform plugin milestone.

### Verification

- Reviewed the complete supplied baseline before implementation, including `README.md`, `CHANGELOG.md`, the complete early-development architecture document, and every source/project file.
- Verified the new repository structure, project references, XML project files, XAML XML structure, centralized version declarations, plugin contract separation, and documentation consistency with automated repository checks.
- Added executable runtime verification tests for the mock target and plugin host so the same checks can be run on a Windows development machine with the .NET 9 SDK.
- The execution environment used to prepare this revision does not contain a .NET SDK or Windows WPF toolchain, so a native `dotnet build` and execution of the WPF application could not be performed in that environment. This limitation is recorded in `docs/testing/REV1_FOUNDATION_VERIFICATION.md` rather than being represented as a successful runtime build.
