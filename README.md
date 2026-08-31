# TeeKay87's Memory Engine

TeeKay87's Memory Engine is a modular Windows desktop application intended to become a shared environment for memory scanning, memory inspection, debugging, and cheat development across multiple target platforms.

The application is written in C# and WPF around a platform-neutral Core and public Plugin SDK. Platform-specific communication and target behavior belong in plugins so the same host, scanner workflow, saved-address model, memory tools, and presentation layer can be reused across PlayStation 5, PC, Xbox 360, and future platforms.

## Current Status

The current application build is **0.1.3.rev1 - PS5 Value Type Expansion**.

The permanent main workspace is now based on the familiar workflow used by Cheat Engine while using the project's own modernized presentation and platform-plugin architecture. The application is not intended to be a visual clone. The layout keeps the parts of the Cheat Engine workflow that are useful and immediately recognizable: a persistent target selector, temporary scan results, scan controls, and a separate persistent saved-address table.

The current built-in plugins are:

| Plugin | Plugin version | Plugin API | Current purpose |
| --- | --- | --- | --- |
| In-Memory Test Target | `1.0.0.rev1` | `1.0.0` | Deterministic development and regression target |
| PlayStation 5 | `0.1.0.rev9` | `1.2.0` | Connection, preferred `eboot.bin` process selection, memory access, all ps5debug-NG scan value types for Exact Value scans, native First/Next Scan acceleration, and process suspend/resume |

Plugin versions are independent from the TeeKay87's Memory Engine application version. The Plugin API also has its own compatibility version.

### Implemented application functionality

- multi-project solution separating the WPF application, shared Core, Plugin SDK, platform plugins, and verification tests;
- centralized application title, version, revision, and active feature information through `AppInfo`;
- capability-based Plugin SDK with platform-neutral target, process, connection, memory-region, and memory-access contracts;
- independent plugin version/revision metadata and Plugin API compatibility metadata;
- runtime discovery of plugin assemblies from the application's `Plugins` directory;
- isolated plugin loading through collectible `AssemblyLoadContext` instances;
- plugin metadata, API compatibility, connection-setting, and duplicate plugin-id validation;
- plugin-defined connection settings rendered generically by the WPF application;
- generic Connect and Disconnect handling through the Plugin SDK rather than platform-specific WPF code;
- generic process enumeration through `IProcessProvider` with separate selected-process and Active Target state, plus optional `IForegroundProcessProvider` preferred-process selection; the PS5 plugin uses this neutral service to select `eboot.bin` automatically when it is present without making it the Active Target;
- automatic process refresh after connection for plugins advertising `ProcessEnumeration`;
- automatic memory-map retrieval for the Active Target when a plugin advertises `MemoryRegionEnumeration`;
- active-target memory regions retained in the host as neutral `MemoryRegion` models for validation and shared scanner work;
- capability-driven raw memory reads through the existing neutral `IMemoryReader` contract;
- host-side validation that raw-read requests stay inside readable Active Target regions when a memory map is available;
- a compact Raw Memory Read inspector that accepts hexadecimal addresses, bounded byte counts, and renders returned bytes as a hexadecimal/ASCII dump;
- capability-driven raw memory writes through the existing neutral `IMemoryWriter` contract;
- host-side validation that raw-write requests stay inside writable Active Target regions when a memory map is available;
- a Raw Memory Write inspector for controlled 4-byte Int32 diagnostics: the address remains hexadecimal, the value is entered as signed decimal, the host encodes it using target endianness, captures original bytes when readable, performs the write, reads the same range back, and reports PASS/FAIL verification;
- an explicit **Safe Write Test** diagnostic action that automatically searches the Active Target memory map for a readable/writable, non-executable, non-guarded region, checks a four-byte candidate for short-term stability, writes back the exact bytes already present, and immediately verifies them without intentionally changing the target value;
- shared Core memory scanner foundation using neutral `IMemoryReader`, `MemoryRegion`, `TargetProcess`, and `TargetArchitecture` data;
- optional plugin-side exact-value scan acceleration through the neutral `INativeValueScanner` service; PS5 First Scan uses ps5debug-NG TurboScan's multi-segment server-resident scan path instead of transferring the target's readable memory to the PC, while Core keeps the existing shared reader-based scanner as the fallback for plugins such as Mock or servers without the required TurboScan capabilities;
- optional native refinement through `INativeValueScanRefiner`; PS5 keeps compatible TurboScan survivor sets resident on the target and performs integer/Array-of-Bytes Exact Value Next Scans through TurboScan COUNT/GET, while Float/Double and any incompatible resident state use the shared Core refinement path to preserve strict Exact Value semantics;
- neutral process suspend/resume through `IProcessControl`, with a capability-driven **Pause target while scanning** checkbox that is Off by default and automatically resumes the Active Target after completion, cancellation, or scan failure;
- Exact Value scanner support for every value type exposed by ps5debug-NG: UInt8, Int8, UInt16, Int16, UInt32, Int32, UInt64, Int64, Float, Double, and Array of Bytes, with First Scan, Next Scan refinement, New Scan, transaction-safe cancellation, status-bar progress and elapsed-time reporting, current/previous values, and temporary result presentation;
- scan-panel keyboard workflow where Enter in the Value field runs First Scan before a scan session exists and Next Scan afterward; the theme-primary button follows the next logical action and New Scan restores First Scan as the primary action;
- bounded 256 KiB shared-scanner reads with readable/Guard filtering, target-endianness-aware numeric encoding/decoding, value-type-specific alignment, a 2,000,000-result safety limit, and protection against runaway repeated read failures;
- scan-result virtualization plus a 50,000-row presentation cap while the complete candidate set remains in Core for refinement;
- compact hexadecimal result-address formatting without redundant leading zeroes, plus a row context menu for **Copy address**, **Copy value**, and **Change value**; because Raw Memory Write remains an Int32 diagnostic, **Change value** is currently enabled only for signed Int32 scan results;
- a permanent Cheat Engine-inspired main workspace now connected to the initial scanner implementation;
- separate Scan Results and Saved Addresses tables so temporary scan candidates are not confused with persistent addresses;
- reusable application-wide WPF styling for buttons, text boxes, combo boxes, data grids, and other shared controls;
- one central 34-unit height standard for normal single-line buttons, text inputs, and selectors so current and future controls align consistently;
- vertically centered single-line TextBox content with compact internal padding so text remains fully visible without changing the established 34-unit control height;
- runtime color themes loaded from external JSON files;
- theme-owned Primary/Secondary/Danger button surfaces and text colors, plus theme-owned tooltip background, border, and text colors; Disconnect and Cancel Scan use the Danger role so stop/cancel/exit-style actions can be recolored consistently by each theme;
- an application-owned tooltip template so hover hints remain readable in Light, Dimmed, and Dark instead of falling back to operating-system tooltip colors;
- application-owned ContextMenu/MenuItem/Separator templates so scan-result context menus use the active theme throughout and no longer expose the operating-system light icon/checkmark gutter in Dimmed or Dark;
- immediate theme switching without restarting the application;
- persistence of the selected theme between launches;
- migration of legacy bundled theme ids and protection against stale renamed theme files left by incremental builds;
- corrected object-backed ComboBox presentation so platform, target-process, and theme selectors show their intended labels instead of CLR type names;
- three included color themes: **Light**, **Dimmed**, and **Dark**;
- deterministic in-memory development target with process enumeration, foreground-process discovery, memory maps, memory reads, and memory writes;
- PlayStation 5 connection, process enumeration, memory-map enumeration, and raw target-memory reads/writes using a native C# ps5debug-NG command-channel implementation; in-flight read/write transactions are completed atomically before scan cancellation is observed so cancelling a scan cannot leave unread protocol bytes on the shared command stream;
- dependency-free verification executable covering the shared plugin foundation, mock memory behavior, shared scanner/refinement behavior, plugin versioning, PS5 connection metadata, the ps5debug-NG handshake, process-list parsing, memory-map parsing/protection conversion, raw read/write protocol behavior, cancellation-safe command-stream reuse, write/read-back preservation, and runtime plugin discovery.

The scanner currently implements **Exact Value** for all eleven ps5debug-NG value types: unsigned/signed 8-, 16-, 32-, and 64-bit integers, Float, Double, and Array of Bytes. Array of Bytes currently means an exact byte sequence; wildcard/masked AOB syntax is not yet exposed by the Scan panel. Saved Addresses actions remain intentionally disabled until the address-table milestone. The generic scanner remains implemented in shared Core. When a plugin advertises `NativeValueScanning` and supplies `INativeValueScanner`, First Scan may use that target-side implementation as an acceleration path while Core normalizes the returned addresses into the same `MemoryScanResult` model. When the same session also supplies `INativeValueScanRefiner`, Next Scan may refine the resident target-side result set and Core again normalizes the returned survivor addresses. Core reader-based First/Next Scan remains the compatibility fallback.

The PlayStation 5 plugin currently advertises:

```text
Connect
ProcessEnumeration
ForegroundProcess
MemoryRegionEnumeration
MemoryRead
MemoryWrite
ProcessSuspend
ProcessResume
NativeValueScanning
```

Disassembly, debugging attachment, breakpoints, and other unimplemented capabilities remain unadvertised until their implementations are complete and verified.

## Scan Workflow

The Scan panel treats the current scan-session state as the guide for the next action:

```text
No scan session:
    First Scan = theme Primary
    Next Scan  = normal Secondary
    Enter in Value -> First Scan

After a successful First Scan:
    First Scan = normal Secondary
    Next Scan  = theme Primary
    Enter in Value -> Next Scan

After New Scan:
    returns to the initial First Scan state
```

A failed or cancelled scan does not advance the primary action. During PS5 TurboScan operations, progress is indeterminate because the current server-resident Exact Value list path does not provide a usable percentage stream. Core fallback scans continue to report real percentage progress. Cancel remains transaction-safe: when an in-flight target-side operation cannot be interrupted without corrupting protocol framing, the UI immediately reports that cancellation has been requested and waits for the current target operation to reach a safe boundary.

## Main Workspace

The primary window follows a target-first memory-tool workflow:

```text
Application / Theme bar
        ↓
Platform + Connection + Target Process
        ↓
┌─────────────────────────────────────┬──────────────────┐
│ Scan Results                        │                  │
│ temporary candidates                │ Scan Controls    │
├─────────────────────────────────────┤                  │
│ Saved Addresses                     │                  │
│ persistent user-selected addresses  │                  │
└─────────────────────────────────────┴──────────────────┘
        ↓
Status bar
```

### Target and connection area

The upper target strip keeps the active platform and process visible while the user works. It reuses the already implemented plugin-defined connection fields and process commands.

For a process-capable plugin, the following concepts remain separate:

- **Process list** — every process currently returned by the plugin;
- **Selected process** — the process currently selected in the process picker;
- **Active Target** — the process explicitly chosen for future memory operations.

Changing the selected row does not silently redirect memory operations. A process becomes the Active Target only when explicitly selected as such. When the active plugin supports memory-region enumeration, setting an Active Target immediately requests that process's current memory map. The neutral region list is retained by the host and its loaded region count is shown in the target status area. Refreshing the process list also refreshes the memory map when the same Active Target remains available. When `MemoryRead` is available, the Raw Memory Read inspector can read bytes from that Active Target. When `MemoryWrite` is available, the Raw Memory Write inspector can submit a signed decimal 4-byte value to a known hexadecimal address. For map-capable targets, reads must fit inside readable regions and writes must fit inside writable regions before the host invokes the plugin. When the write range is also readable and an `IMemoryReader` is available, the host captures the original bytes, performs the write, reads the range back, and compares the returned bytes with the requested data. The diagnostic **Safe Write Test** removes the need to manually discover an address for protocol verification: it selects only regions marked Read + Write while excluding Execute and Guard, samples a four-byte interior address repeatedly, aborts if the bytes are not stable, then writes the current bytes back unchanged and performs immediate read-back verification.

### Scan Results

The large upper-left table displays temporary scan candidates normalized by shared Core. First Scan searches readable non-guard regions using the alignment and width of the selected Value Type. Next Scan refines only existing candidates, preserves the prior comparison value in the **Previous** column, and retains addresses matching the new Exact Value. Results are not automatically added to the persistent address table.

Result addresses are rendered in compact hexadecimal form, for example `0x10000104` rather than a fixed-width `0x0000000010000104`. Right-clicking a result row provides **Copy address**, **Copy value**, and **Change value**. The Change value action is currently available only for **4 Bytes (Signed) / Int32** results because the temporary Raw Memory Write diagnostic still writes Int32 values only. For those results it loads the address and intentionally leaves the decimal value field empty so a stale value cannot be written accidentally.

The complete candidate set is retained for refinement up to the current 2,000,000-result safety limit. To keep WPF presentation responsive, the DataGrid displays at most the first 50,000 candidates at one time; the header continues to show the full result count.

### Scan controls

The full-height right-side scanner panel now provides the first usable memory-search workflow:

```text
Value Type: UInt8 / Int8 / UInt16 / Int16 / UInt32 / Int32 / UInt64 / Int64 / Float / Double / Array of Bytes
Scan Type:  Exact Value
First Scan
Next Scan
New Scan
Cancel Scan
```

The Value Type selector exposes exactly the eleven value types supported by ps5debug-NG's scanner. Integer values accept decimal input and `0x`-prefixed hexadecimal input, Float and Double use invariant decimal notation, and Array of Bytes accepts hexadecimal sequences such as `DE AD BE EF` or `DEADBEEF` up to 4,096 bytes. Value Type is editable before First Scan and then locked for the active scan session; choose New Scan before changing it. Scan Type remains Exact Value in this milestone. Scan status and errors are shown in the application status bar instead of consuming space under the Scan buttons. While a scan is active, the status bar also shows a progress bar and elapsed time; the final elapsed time remains visible after completion/cancellation until the scan session is reset.

Cancel Scan is cooperative. Core requests cancellation immediately, while a platform transport may defer the cancellation boundary until its current framed scan/read transaction has been completely consumed. This prevents a protocol such as ps5debug-NG from leaving unread response bytes in the shared TCP stream.

For PS5 First Scan, plugin `0.1.0.rev9` probes ps5debug-NG TurboScan capabilities during connection and exposes `INativeValueScanner` only when the server advertises both server-resident result storage and multi-segment scanning. The plugin authenticates the scan channel, sends the current neutral readable regions as disjoint segments, performs the selected Exact Value comparison on the target, fetches the surviving address/value records, and keeps the successful resident survivor set available for compatible refinement. Integer and exact Array-of-Bytes Next Scans use TurboScan COUNT/GET against that resident set when `INativeValueScanRefiner` is available. Float and Double deliberately close the resident session and fall back to shared Core refinement because current ps5debug-NG resident refinement uses fuzzy floating-point equality, which would not preserve Memory Engine's strict Exact Value semantics. Any other incompatible native state also falls back to Core. Native list-resident operations use an indeterminate status-bar progress indicator; Core fallback operations retain real percentage progress.

When the plugin exposes both process-suspend and process-resume capabilities, the Scan panel also shows **Pause target while scanning**. It is Off by default. When enabled, the host suspends the Active Target before First/Next Scan and resumes it in a `finally` path after success, cancellation, or ordinary failure.

### Saved Addresses

Saved Addresses is a separate persistent workspace below Scan Results in the left side of the main work area. Future operations such as value editing, freezing, pointer work, memory browsing, disassembly, cheat construction, and export will operate from this table rather than directly from the temporary scan result set.

The horizontal splitter between Scan Results and Saved Addresses is resizable, and a second vertical splitter between the left-side lists and the full-height Scan panel allows the scanner width to be adjusted independently. Both splitters use centered 4-pixel visual handles inside 14-pixel interactive tracks with equal breathing room on each side. The left-side tables now start at an even 50/50 height split and the horizontal divider is proportionally limited to a 20/80–80/20 range, so the usable resize range scales naturally with both windowed and fullscreen heights. The Scan panel keeps its established 280–420 pixel width range.

Detailed layout rules are documented in [`docs/ui/MAIN_WORKSPACE.md`](docs/ui/MAIN_WORKSPACE.md).

## Color Themes

Color themes are deliberately presentation-only. A theme can change the application's palette, but it cannot replace the UI, inject XAML, change control templates, or alter feature behavior.

The application currently includes:

1. **Light** — a light neutral palette;
2. **Dimmed** — a deliberately intermediate palette between Light and Dark;
3. **Dark** — the original dark palette used by the application before theme support was introduced.

Theme files are external JSON files copied to the runtime `Themes` directory:

```text
Themes/
├── Light.json
├── Dimmed.json
└── Dark.json
```

At startup, the application discovers and validates `*.json` files in that directory. Valid themes appear in the Theme dropdown in the application bar. Selecting a theme replaces the application's shared WPF brush resources immediately, so all controls using those resources redraw without an application restart.

The selected theme id is stored in:

```text
%LocalAppData%\TeeKay87\MemoryEngine\settings.json
```

If the saved theme is unavailable, the application falls back to **Dark** when present and otherwise to the first valid discovered theme. `App.xaml` also contains an emergency Dark-compatible palette so the application retains readable colors even if every external theme file is missing or invalid. The canonical bundled ids are `light`, `dimmed`, and `dark`. Preferences written by rev4-rev6 with the legacy ids `darker` or `darkest` are migrated automatically to `dimmed` or `dark` at startup.

A theme file contains metadata plus a fixed set of color values. Colors must use `#RRGGBB` or `#AARRGGBB`. Invalid files are skipped rather than partially applied, and the loading error is surfaced by the host. Primary and Secondary buttons now have independent background, border, and text palette entries instead of borrowing general panel/accent colors. Danger buttons retain their dedicated palette. Tooltips/hints likewise use dedicated background, border, and text colors supplied by the active theme.

The schema, palette keys, loading rules, and instructions for adding another color theme are documented in [`docs/ui/THEMES.md`](docs/ui/THEMES.md).

## Shared WPF Styling

Reusable WPF presentation belongs in shared resource dictionaries instead of individual views.

Current style resources are located under:

```text
src/TeeKay87.MemoryEngine.App/Resources/Styles/
├── ButtonStyles.xaml
└── ControlStyles.xaml
```

`ButtonStyles.xaml` provides reusable **Primary**, **Secondary**, and **Danger** button semantics on top of one shared control template. Their normal background/border/text colors are supplied independently by the active theme, while hover, pressed, keyboard-focus, defaulted, and disabled states remain owned centrally.

`ControlStyles.xaml` provides shared theme-aware presentation for common controls used by the main workspace, including text, text boxes, combo boxes, check boxes, data grids, cards, tooltips/hints, and related states. Tooltips use an application-owned template so their foreground/background no longer fall back to Windows theme colors. Context menus, menu items, and separators likewise use application-owned templates, including removal of the default Windows icon/checkmark gutter.

Normal single-line `Button`, `TextBox`, and `ComboBox` styles all use `UiMetrics.StandardControlHeight`, currently **34 WPF device-independent units**. This is the height already established by the Platform selector and is the application-wide baseline for future single-line interactive controls. Views should not override that height merely to make neighboring controls line up. The shared TextBox template uses compact vertical padding and stretches its content host across the available interior before applying `VerticalContentAlignment`, preventing text from being clipped while preserving the same outer height.

Views remain responsible for layout-specific values such as position, width, and margin. They should not duplicate ordinary control colors, control templates, interaction-state definitions, or the standard single-line control height.

See [`docs/ui/BUTTON_STYLES.md`](docs/ui/BUTTON_STYLES.md) for the button rules and [`docs/ui/CONTROL_METRICS.md`](docs/ui/CONTROL_METRICS.md) for shared control sizing rules.

## Architecture

The current solution is organized as follows:

```text
TeeKay87.MemoryEngine.sln
│
├── src/
│   ├── TeeKay87.MemoryEngine.App/
│   │   ├── Application/
│   │   ├── Controls/
│   │   ├── Infrastructure/
│   │   ├── Resources/Styles/
│   │   ├── Themes/
│   │   ├── Theming/
│   │   ├── ViewModels/
│   │   └── WPF views
│   │
│   ├── TeeKay87.MemoryEngine.Core/
│   │   └── shared host infrastructure and plugin discovery
│   │
│   ├── TeeKay87.MemoryEngine.PluginSdk/
│   │   ├── Capabilities/
│   │   ├── Contracts/
│   │   └── Models/
│   │
│   └── Plugins/
│       ├── TeeKay87.MemoryEngine.Platform.Mock/
│       │   └── deterministic in-memory development target
│       │
│       └── TeeKay87.MemoryEngine.Platform.PS5/
│           └── PlayStation 5 / ps5debug-NG implementation
│
├── tests/
│   └── TeeKay87.MemoryEngine.Tests/
│
├── docs/
│   ├── architecture/
│   ├── plugins/
│   │   ├── Mock/
│   │   └── PS5/
│   ├── testing/
│   └── ui/
│
├── Directory.Build.props
├── README.md
└── CHANGELOG.md
```

The central architectural rule is:

> If a feature describes what a memory-development tool does, it should normally be shared. If it describes how a particular target performs that operation, it should normally belong to that target's plugin.

The long-term architecture is documented in [`docs/architecture/EARLY_DEVELOPMENT_ARCHITECTURE.md`](docs/architecture/EARLY_DEVELOPMENT_ARCHITECTURE.md). The implemented plugin boundary is documented in [`docs/architecture/PLUGIN_SDK_FOUNDATION.md`](docs/architecture/PLUGIN_SDK_FOUNDATION.md).

## Plugin Versioning and Compatibility

The project keeps three version domains separate:

1. **Application version** — TeeKay87's Memory Engine, currently `0.1.3.rev1`;
2. **Plugin version** — each plugin has its own semantic version and revision;
3. **Plugin API version** — compatibility version for the public host/plugin contract, currently `1.2.0`.

A plugin does not inherit the host application's version. Updating the host does not automatically change a plugin version, and changing one plugin does not require unrelated plugins to change version.

The current Plugin API compatibility rule requires the same major API version. A plugin may target the same or an older minor API version within that major version, but a plugin requiring a newer minor API version than the host provides is rejected during discovery.

## Plugin Model

A platform plugin implements `ITargetPlugin` from `TeeKay87.MemoryEngine.PluginSdk`.

Each plugin declares:

- stable plugin id;
- display name;
- target platform;
- backend or transport;
- independent plugin version and revision;
- targeted Plugin API version;
- target architecture;
- supported capabilities;
- connection-field definitions required by that plugin.

Connected targets expose implemented operations through `ITargetSession.GetService<TService>()`.

The current neutral service contracts include:

- `IProcessProvider`;
- `IForegroundProcessProvider`;
- `IMemoryMapProvider`;
- `IMemoryReader`;
- `IMemoryWriter`;
- `INativeValueScanner`;
- `IProcessControl`.

The host uses capabilities and services rather than platform names to decide which functionality is available.

## In-Memory Test Target

`TeeKay87.MemoryEngine.Platform.Mock` is a deterministic development target used to exercise shared host functionality without a physical console.

It exposes one process:

```text
TestGame.exe
```

Its deterministic memory begins at:

```text
0x10000000
```

Initial values include:

| Value | Address | Initial value |
| --- | --- | ---: |
| Health | `0x10000100` | `100.0` (`Float32`) |
| Ammo | `0x10000104` | `30` (`Int32`) |
| Money | `0x10000108` | `5000` (`Int32`) |

These fixtures are intended to support future scanner, saved-address, freeze, memory-viewer, export, and regression tests.

Mock-specific documentation is kept only under [`docs/plugins/Mock/`](docs/plugins/Mock/).

## PlayStation 5 Plugin

`TeeKay87.MemoryEngine.Platform.PS5` communicates with a PlayStation 5 running ps5debug-NG over its TCP command channel.

The plugin defines its own connection settings. The current defaults are:

| Field | Required | Default |
| --- | --- | --- |
| PS5 IP address or host name | Yes | none |
| Port | Yes | `744` |

The WPF host renders these settings from Plugin SDK metadata; it does not contain PS5-specific IP or port logic.

A connection is accepted only after the plugin has opened the command channel, read and validated ps5debug-NG identification information, read the target firmware, and completed a process NOP/liveness request.

After connection, process enumeration is performed through the same neutral `IProcessProvider` contract used by the host and mock target. The PS5 plugin parses ps5debug-NG `CMD_PROC_LIST` records internally and exposes only generic `TargetProcess` models to the rest of the application.

Once a process is made the Active Target, the PS5 session uses the existing neutral `IMemoryMapProvider` contract. It sends ps5debug-NG `CMD_PROC_MAPS`, parses the backend-specific map entries inside the PS5 plugin, translates read/write/execute protection bits, and returns only generic `MemoryRegion` models to the host. The host caches the current Active Target map and displays the loaded region count in the target status area. Process refresh also refreshes the map when the same Active Target survives the refresh.

The same session implements the pre-existing neutral `IMemoryReader` and `IMemoryWriter` contracts. `CMD_PROC_READ` remains isolated inside the PS5 plugin: the plugin serializes the process id, 64-bit target address, and requested length, validates the response status, receives the raw bytes, and returns only the neutral read result to the host. `CMD_PROC_WRITE` uses the same packed 16-byte request body, waits for the server's first success acknowledgement, streams the requested bytes, and then consumes the command's second success status before returning. Once either read/write command has begun, the PS5 client completes that protocol transaction before honoring caller cancellation; this preserves command-stream framing and prevents Cancel Scan from corrupting the next process-list, map, read, or write command. Neither protocol structure crosses the Plugin SDK boundary.

The Raw Memory Read inspector is intentionally bounded to 4 KiB per interactive request. The temporary Raw Memory Write inspector currently writes one signed 4-byte Int32 value entered in decimal form; the host encodes those four bytes according to the active target's declared endianness before calling the neutral `IMemoryWriter`. This is a diagnostic-UI choice, not a limit on `IMemoryWriter` or future Saved Addresses/Memory Viewer editing.

Live Windows/PS5 testing has verified connection, real process enumeration, memory-map enumeration, raw-memory reads, raw-memory writes/read-back, and the initial shared scanner. The rev17 Safe Write Test returned PASS twice against a real PS5. The 0.1.2.rev1 scanner found a real in-game money value and allowed that value to be changed. Rev3 subsequently verified cancellation/session reuse on a real console: Cancel Scan could be followed by Refresh and a new scan without reconnecting. A real Int32 First Scan for `10002` on a 10,105-region target returned 266 results but required approximately `07:04.3`, while Next Scan was effectively instant. Rev4 therefore moves supported PS5 First Scans to ps5debug-NG TurboScan's multi-segment server-resident path while retaining the shared Core scanner as a compatibility/resource fallback.

PS5-specific documentation is kept only under [`docs/plugins/PS5/`](docs/plugins/PS5/).

## Building

Requirements:

- Windows 10 or Windows 11;
- Visual Studio 2022 with .NET desktop development workload;
- .NET 9 SDK.

Open:

```text
TeeKay87.MemoryEngine.sln
```

Then build:

```text
Build > Build Solution
```

Warnings are treated as errors project-wide.

The application build copies built platform plugin assemblies to the runtime `Plugins` directory and copies external JSON theme files to the runtime `Themes` directory for normal build and publish output.

## Verification Executable

Run the dependency-free verification executable with:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

The current revision contains **19 checks**. A successful run ends with `All 19 checks passed.`. Coverage includes the established scanner/transport regressions plus shared-Core parsing/scanning across all ps5debug-NG value types, PS5 wire-value-type mapping including Array of Bytes mask framing, TurboScan capability negotiation/authentication, resident First/Next Scan behavior, cancellation/session reuse, preferred `eboot.bin` process selection, and process suspend/resume.

Project-wide verification documents are kept under [`docs/testing/`](docs/testing/). Plugin-specific runtime/protocol verification belongs under each plugin's dedicated `docs/plugins/<Plugin>/` directory.

## Current Development Boundary

The complete low-level target-access chain — connection, process enumeration, explicit Active Target selection, memory-map retrieval, raw memory reads, raw memory writes, and immediate read-back verification — has now been live-verified against a real PlayStation 5. The rev17 Safe Write Test was executed twice against `eboot.bin` and returned `Verification: PASS` both times.

`0.1.3.rev1` expands the verified `0.1.2` Exact Value scanner from signed Int32 to **every value type exposed by ps5debug-NG**: UInt8, Int8, UInt16, Int16, UInt32, Int32, UInt64, Int64, Float, Double, and Array of Bytes. The shared Core scanner and the PS5 TurboScan path use the same selected type, width, alignment, display model, and target endianness rules. PS5 First Scan retains multi-segment resident TurboScan acceleration. Integer and exact Array-of-Bytes Next Scans retain target-side refinement, while Float/Double use shared Core refinement to preserve strict Exact Value semantics; Core remains the general fallback. The rev5 keyboard/action workflow, theme Danger controls, preferred `eboot.bin` selection, pause/resume behavior, and safe deferred cancellation remain unchanged. Scan Type is still limited to Exact Value, and Array of Bytes currently uses an exact all-bytes mask rather than wildcard syntax. Saved-address operations, freezing, comparative/unknown-value scan modes, full Memory Viewer, disassembly, debugging, pointer scanning, and cheat construction remain later work. Raw Memory Read and Raw Memory Write remain development diagnostics; Raw Memory Write is still Int32-only, so Scan Results → Change value is enabled only for Int32 results.
