# TeeKay87's Memory Engine --- Early Development Architecture Guide

## 1. Purpose

This document defines the initial architectural direction, design
principles, platform abstraction strategy, plugin model, shared
subsystems, and early development requirements for **TeeKay87's Memory
Engine**.

It is intended to serve as a technical guide during the early stages of
development. The project must not be designed as a PlayStation 5-only
memory tool. PlayStation 5 support through **ps5debug-NG** is the first
target implementation, but the application architecture must remain
platform-neutral so that additional targets such as Windows PC, Xbox
360, PlayStation 3, PlayStation 4, emulators, and offline memory dumps
can be added later without replacing the main application or duplicating
its user interface.

The primary design goal is to create a modern, extensible memory
scanning, debugging, and cheat-development environment in C# and WPF.

------------------------------------------------------------------------

## 2. Core Development Principles

The following principles should guide the implementation from the
beginning.

### 2.1 Platform-neutral core

The main application must not contain PlayStation 5-specific
assumptions.

Core concepts should use neutral names such as:

-   `TargetProcess`
-   `TargetModule`
-   `MemoryRegion`
-   `MemoryAddress`
-   `ScanResult`
-   `Breakpoint`
-   `Watchpoint`
-   `RegisterSet`
-   `CheatDefinition`

Names such as `Ps5Process`, `Ps5Address`, or `Ps5ScanResult` should only
exist inside a PlayStation-specific plugin when genuinely necessary.

### 2.2 Plugin-owned platform behavior

Each platform plugin must describe:

-   what platform it supports;
-   how the target is discovered and connected;
-   how processes are enumerated;
-   how memory is read and written;
-   how memory maps are obtained;
-   which scanner operations are supported;
-   whether native/server-side scanning is available;
-   which debugger operations are supported;
-   CPU architecture and endianness;
-   register definitions;
-   assembler and disassembler support;
-   platform-specific cheat operations;
-   required game or executable metadata;
-   cheat validation rules;
-   supported cheat export formats;
-   how cheats are applied and disabled.

Core must consume these capabilities rather than contain platform
checks.

### 2.3 Capability-driven UI

The UI must adapt to the active plugin's reported capabilities.

Core should not contain logic such as:

``` csharp
if (platform == Platform.PlayStation5)
{
    ShowWatchpoints();
}
```

The preferred model is capability-based:

``` csharp
if (plugin.Capabilities.HasFlag(TargetCapabilities.Watchpoints))
{
    ShowWatchpoints();
}
```

Unsupported functionality should be hidden or disabled consistently.

### 2.4 Shared functionality first

Functionality that can be implemented independently of a specific
platform should be implemented once in Core.

Platform plugins should provide low-level target behavior and
platform-specific rules, not duplicate generic application
functionality.

### 2.5 Preserve verified functionality

Once functionality has been implemented and verified, unrelated future
work must not modify, remove, or regress it.

If previously verified behavior must be changed to support new
functionality or correct an identified defect, the required change and
its consequences must be identified before implementation.

------------------------------------------------------------------------

## 3. Technology Stack

The initial application should use:

-   **C#**
-   **WPF (Windows Presentation Foundation)**
-   **MVVM**
-   a modern supported .NET version appropriate at implementation time
-   asynchronous I/O through `async` / `await`
-   separate projects for Core, plugin contracts, WPF UI, platform
    plugins, and tests

A preliminary solution structure may resemble:

``` text
TeeKay87.MemoryEngine.sln
│
├── TeeKay87.MemoryEngine.App
│   └── WPF application and presentation layer
│
├── TeeKay87.MemoryEngine.Core
│   ├── Scanner
│   ├── ScanResults
│   ├── AddressTable
│   ├── FreezeEngine
│   ├── PointerScanner
│   ├── MemoryViewer
│   ├── Debugger
│   ├── Modules
│   ├── Symbols
│   ├── CheatProjects
│   ├── CheatOperations
│   ├── Export
│   ├── Scripting
│   └── PluginHost
│
├── TeeKay87.MemoryEngine.PluginSdk
│   ├── Contracts
│   ├── Capabilities
│   ├── Models
│   └── Plugin metadata
│
├── TeeKay87.MemoryEngine.Platform.PS5
│   └── ps5debug-NG implementation
│
└── TeeKay87.MemoryEngine.Tests
```

Future plugins should be addable without rebuilding the conceptual
architecture of the application.

------------------------------------------------------------------------

## 4. Plugin Architecture

Platform support should be distributed as separate plugin assemblies.

Example:

``` text
Plugins/
├── Platform.PS5.ps5debugNG.dll
├── Platform.Windows.dll
├── Platform.Xbox360.dll
└── Platform.MemoryDump.dll
```

The exact naming convention can be finalized later.

### 4.1 Main plugin contract

A platform plugin should expose a top-level contract similar in purpose
to:

``` csharp
public interface ITargetPlugin
{
    string Id { get; }
    string Name { get; }
    string Platform { get; }

    TargetCapabilities Capabilities { get; }

    IMemoryProvider Memory { get; }
    IScannerProvider? Scanner { get; }
    IDebuggerProvider? Debugger { get; }
    IAssemblerProvider? Assembler { get; }
    IDisassemblerProvider? Disassembler { get; }
    ICheatProvider? Cheats { get; }

    Task<ITargetSession> ConnectAsync(
        TargetConnectionOptions options,
        CancellationToken cancellationToken);
}
```

This is an architectural example rather than a frozen API. The final
interfaces should be designed carefully before implementation.

### 4.2 Plugin manifest

Each plugin should expose metadata describing itself and its
requirements.

A conceptual manifest could contain:

``` json
{
  "id": "platform.ps5.ps5debug-ng",
  "name": "PlayStation 5",
  "version": "1.0.0",
  "revision": 1,
  "pluginApiVersion": "1.0.0",
  "backend": "ps5debug-NG",
  "architecture": ["x86_64"],
  "capabilities": [
    "memory.read",
    "memory.write",
    "memory.maps",
    "scanner.native",
    "debugger.breakpoints",
    "debugger.watchpoints",
    "debugger.registers",
    "debugger.stack",
    "assembler.x86_64",
    "disassembler.x86_64",
    "cheats.create"
  ]
}
```

The manifest format and whether it is embedded or external should be
decided during implementation.

### 4.3 Platform versus backend

The architecture should distinguish the platform from the
transport/backend whenever practical.

For example:

``` text
Platform:
PlayStation 5

Backend:
ps5debug-NG
```

This prevents the PlayStation 5 implementation from becoming permanently
tied to one backend if another compatible debugging or memory-access
system appears later.

### 4.4 Host-managed plugin settings

Plugins may need small persistent preferences such as the most recently successful connection endpoint or platform-specific defaults. Persistence should remain a host responsibility rather than allowing each plugin to invent its own files and storage rules.

The implemented direction is a plugin-id-scoped settings service supplied through the Plugin SDK. A plugin uses stable keys through a neutral settings contract; Core owns namespace isolation and the persistence implementation. The plugin does not receive the physical settings path and does not parse the host settings document. This keeps storage-format changes independent from plugin code and prevents one plugin from accidentally overwriting another plugin's values.

Sensitive credentials should not be stored through a plain-text general settings service unless a separate secure-storage design is introduced deliberately.

------------------------------------------------------------------------

## 5. Initial PlayStation 5 Plugin

The first platform implementation will target PlayStation 5 using
**ps5debug-NG**.

ps5debug-NG runs as a payload on the PlayStation 5 and exposes remote
process, memory, scanning, and debugging functionality to a client on
another machine.

The Windows application therefore acts as a client. It should not
contain console exploit or payload-loading logic unless such
functionality is deliberately added as a separate feature later.

The PS5 plugin is expected to handle functionality such as:

-   PS5 discovery where supported;
-   manual IP connection;
-   connection state;
-   foreground application detection;
-   process enumeration;
-   game/process metadata;
-   memory read;
-   memory write;
-   memory maps;
-   native scanning when advantageous;
-   AOB scanning;
-   memory allocation when supported;
-   debugger connection;
-   breakpoints;
-   hardware watchpoints;
-   register access;
-   threads;
-   stack walking;
-   target-side assembler/disassembler functionality where appropriate;
-   PS5-specific cheat metadata;
-   PS5 cheat application;
-   PS5 cheat validation;
-   PS5 cheat export formats.

All ps5debug-NG protocol details must remain inside the PS5/backend
implementation and must not leak into Core.

------------------------------------------------------------------------

## 6. Shared Memory Scanner

The scanner should primarily be a Core subsystem.

The same standard comparison workflow should be available for every compatible
platform. As of `0.1.3.rev26`, Core owns the following production Scan Types:

-   Exact Value
-   Fuzzy Value
-   Bigger Than
-   Smaller Than
-   Between
-   Unknown Initial Value
-   Unknown Initial Low Value
-   Increased Value
-   Decreased Value
-   Changed Value
-   Unchanged Value
-   Increased By
-   Decreased By

AOB / byte-pattern scanning remains a separate representation/search concern rather
than another ordered comparison predicate. Additional general predicates can be
introduced in Core later when their semantics are useful across platforms.

### 6.1 Generic scanner

Core should provide a generic scanner that can operate through a
sufficiently capable memory provider.

At the lowest level, it should only require target memory and
memory-region information.

Conceptually:

``` csharp
public interface IMemoryReader
{
    Task<int> ReadAsync(
        ulong address,
        Memory<byte> destination,
        CancellationToken cancellationToken);
}
```

This allows the same scanner to operate against:

-   ps5debug-NG;
-   Windows process APIs;
-   Xbox 360 remote debugging;
-   other consoles;
-   emulators;
-   offline memory dumps.

### 6.2 Native scanner acceleration

Plugins may optionally provide native scanning.

This is especially important for remote targets because transmitting
large memory regions to the PC may be substantially slower than scanning
on the target.

The scanner architecture should therefore support:

``` text
Core Scan Type request
     │
     ├── Plugin declares a semantically equivalent native mapping
     │       ├── Runtime shape supported -> plugin performs native scan
     │       └── Runtime shape rejected  -> Core fallback
     │
     └── No semantic native mapping
             └── Core generic scanner
```

The mapping contract is generic and backend-neutral. Core owns the predicate identity
and meaning; a plugin owns its native id/opcode and publishes only the mappings whose
semantics match Core for the declared First/Next stage and Value Types. A future PC,
Xbox 360, console, emulator, or dump plugin can therefore reuse the same Core Scan Type
catalog while accelerating only the operations its backend supports.

The user-facing scan workflow must remain identical regardless of which
implementation is selected.

### 6.3 Scan sessions

Core should own scan-session state including:

-   initial scan criteria;
-   previous values;
-   current values;
-   result count;
-   filtering;
-   next-scan state;
-   cancellation;
-   progress;
-   result paging;
-   result persistence where appropriate.

Large result sets must be designed for efficient storage and must not
require every result to exist as a permanently allocated WPF object.

------------------------------------------------------------------------

## 7. Shared Data Types and Target Architecture

Core should provide standard memory data types such as:

-   signed and unsigned 8-bit integers;
-   signed and unsigned 16-bit integers;
-   signed and unsigned 32-bit integers;
-   signed and unsigned 64-bit integers;
-   32-bit float;
-   64-bit double;
-   byte arrays;
-   ASCII;
-   UTF-8;
-   UTF-16.

Plugins may register additional platform-specific types if required.

Target architecture metadata should include at least:

-   CPU architecture;
-   pointer width;
-   address width;
-   endianness.

This is necessary because platforms differ significantly. For example,
Xbox 360 and PS5 cannot be treated as having identical pointer or endian
behavior.

Value encoding and decoding should remain centralized wherever possible.

------------------------------------------------------------------------

## 8. Scan Results

Scan results should be a Core concept.

A generic result may include:

``` csharp
public record ScanResult(
    ulong Address,
    MemoryValue CurrentValue,
    MemoryValue? PreviousValue);
```

Additional metadata may include:

-   module;
-   module-relative offset;
-   memory region;
-   protection;
-   resolved symbol;
-   plugin-provided metadata.

Core should own:

-   display;
-   paging;
-   filtering;
-   sorting;
-   selection;
-   current/previous value presentation;
-   adding results to the saved address list;
-   export.

------------------------------------------------------------------------

## 9. Saved Address List

The saved address system is shared by all targets. The initial functional baseline was implemented in application `0.1.3.rev17` without adding platform-name checks or a host-owned concrete Value Type list.

The rev17 row model includes:

-   Frozen state and captured freeze bytes;
-   user-editable description;
-   absolute resolved address;
-   plugin-declared data type;
-   current value bytes and display text;
-   target process identity used to prevent automatic writes to a different Active Target.

Rows are created from Scan Results by double-click or **Save Address**, survive First/Next/New Scan for the current application/plugin workspace, refresh through neutral `IMemoryReader`, and edit/freeze through neutral `IMemoryWriter`. The **Type** selector reuses the plugin's `IMemoryValueType` definitions for size, parsing, formatting, alignment, and target-endianness interpretation. Rev17 does not persist address rows across application launches or plugin reloads; that belongs with the later project/cheat persistence design rather than application settings.

Application `0.1.3.rev24` keeps live Value-entry syntax on the same plugin-owned boundary. Plugin API `2.5.0` adds optional `IMemoryValueInputPolicy`, which a concrete `IMemoryValueType` may implement when it can describe potentially valid intermediate editor text. The host consumes that policy generically and still uses `TryParse(...)` for final validity; it must not infer syntax from platform names, type ids, or display labels.

The model should later expand with:

-   address expressions;
-   display-format overrides;
-   groups;
-   notes;
-   hotkeys;
-   pointer information;
-   project/cheat persistence;
-   export.

Addresses should not remain restricted to absolute addresses long-term.

The address-expression system should eventually support concepts such
as:

``` text
0x20752D3A0
eboot.bin + 0x113996C
game.exe + 0x14AB20
[[module+0x1234]+0x20]+0x18
```

Resolution of platform-specific module and pointer semantics should be
delegated appropriately.

------------------------------------------------------------------------

## 10. Freeze and Repeated Memory Operations

Freeze behavior is platform-neutral at the workflow level.

Application `0.1.3.rev17` introduced the first bounded freeze loop for Saved Addresses. Application `0.1.3.rev18` separated ordinary value refresh from repeated Frozen writes. Application `0.1.3.rev19` aligns repeated Frozen behavior more closely with Cheat Engine: value refresh defaults to 500 ms, Frozen writes default to 100 ms, the captured Frozen bytes remain independent from refresh display state, and transient Frozen write failures remain Frozen for automatic retry. Rev20 added explicit Value-write/background-I/O coordination and stale-read protection, rev21 added queued individual removal, and rev22 generalizes the coordination rule so transient Saved Address background I/O cannot consume target-level user actions or Saved Address Freeze/Address/Type/Remove All intent. Both settings retain independent 50-10,000 ms ranges.

Value refresh pauses during First/Next Scan. Frozen writes continue during a scan when Pause target while scanning is Off and are suppressed when that option is On. To avoid corrupting a plugin's primary command stream, in-scan Frozen writes require Plugin API `2.4.0` `IConcurrentMemoryWriter`; the PS5 implementation uses a separate lazily connected ps5debug-NG client. Rev19 also prefers the concurrent writer for repeated Frozen writes outside scanning when available, so an ordinary refresh read does not delay the freeze cadence. A row remains inactive when another process is the Active Target, and active freezes are disabled when the target connection ends so a later connection cannot silently resume old repeated writes.

The subsystem should later expand with:

-   grouped operations;
-   per-entry or per-group intervals if a real use case requires them;
-   hotkey-controlled state;
-   generalized scheduled/task actions.

------------------------------------------------------------------------

## 11. Pointer Support

Pointer chains and pointer scanning should be implemented as shared Core
functionality wherever possible.

The plugin should provide the information required to interpret the
target:

-   pointer width;
-   endianness;
-   readable regions;
-   modules/static ranges;
-   alignment information where relevant.

Core can then implement:

-   pointer-chain evaluation;
-   reverse pointer search;
-   offset constraints;
-   depth constraints;
-   result storage;
-   filtering;
-   persistence;
-   comparison between sessions/dumps where later supported.

Offline memory-dump support should be considered during design because
pointer scanning against dumps can be useful even without a live target.

------------------------------------------------------------------------

## 12. Memory Viewer

The Memory Viewer should be primarily a shared Core/WPF subsystem.

Shared functionality should include:

-   virtualized hex view;
-   ASCII/text representation;
-   address navigation;
-   selection;
-   copy;
-   editing;
-   refresh;
-   go-to-address;
-   bookmarks;
-   region/module awareness;
-   navigation history.

The plugin supplies memory read/write functionality and target metadata.

Application `0.1.5.rev1` begins this direction with a deliberately bounded **read-only foundation** rather than attempting to freeze the entire list above into one first implementation. Core owns a neutral `MemoryViewerReader` that reads a 512-byte window through `IMemoryReader`, clamps the window to one readable non-guarded `MemoryRegion`, and returns a `MemoryViewSnapshot`. The host WPF window supplies Go To, Refresh, Region / Module, Protection, visible range, and 16-byte Address/Hex/ASCII rows. Scan Results and Saved Addresses expose **Browse Memory** entry points. Corrective `0.1.5.rev2` and `0.1.5.rev3` repair the WPF/C# compile issues discovered by the first Windows builds. Application `0.1.5.rev4` resumes functional work with extended row selection, copy actions, Back/Forward successful-address history, and a persistent green origin-row marker that remains separate from ordinary selection. Runtime verification exposed a row-size change when that origin row was selected; `0.1.5.rev5` removes the selection-only border so the marker is layout-neutral and was fully verified. Application `0.1.5.rev6` adds the first safe raw-byte write path through neutral `IMemoryReader`/`IMemoryWriter`: an explicit row editor performs stale-source validation, requires existing Read+Write/non-Guard protection, and reads every issued write back for verification without changing page protection. Application `0.1.5.rev7` adds viewer-local exact-address bookmarks and Core-owned previous/start/end/next navigation across readable non-guarded memory regions. Application `0.1.5.rev8` keeps those Core behaviors unchanged while correcting bookmark selection presentation and host UI/state consistency discovered during runtime verification. The detailed implemented behavior lives in `MEMORY_VIEWER.md`; this early list remains guidance rather than a frozen contract.

------------------------------------------------------------------------

## 13. Disassembly and Assembly

The application must support multiple CPU architectures.

The disassembly model therefore must not assume x86-64.

A neutral instruction model should include information such as:

-   address;
-   raw bytes;
-   mnemonic;
-   operands;
-   instruction length;
-   branch target;
-   memory operands;
-   symbol/module information.

Conceptually:

``` csharp
public interface IDisassembler
{
    IEnumerable<Instruction> Disassemble(
        ulong address,
        ReadOnlySpan<byte> bytes);
}
```

Plugins may use:

-   target-side disassembly;
-   a client-side library;
-   another architecture-specific implementation.

Likewise, assembly support should be exposed through a provider rather
than hard-coded into Core.

PS5 uses x86-64, while Xbox 360 uses PowerPC, so this abstraction is
mandatory.

------------------------------------------------------------------------

## 14. Debugger Architecture

The low-level debugger implementation is platform-specific, but the
debugger workflow and data models can be shared.

Core should define neutral concepts for:

-   debugger session;
-   breakpoint;
-   watchpoint;
-   debug event;
-   thread;
-   register;
-   register set;
-   instruction pointer;
-   stack frame;
-   call stack;
-   continue;
-   pause;
-   step operations where supported.

The plugin should implement the actual target operations.

### 14.1 Find What Writes / Accesses

The user workflow should be shared:

``` text
Select address
      ↓
Find what writes/accesses
      ↓
Create watchpoint
      ↓
Continue target
      ↓
Receive debug event
      ↓
Record instruction and context
```

Core should manage:

-   hit list;
-   hit counts;
-   duplicate grouping;
-   instruction history;
-   register snapshots;
-   optional stack snapshots;
-   timestamps;
-   navigation to disassembly.

The plugin provides watchpoint creation/removal and debug events.

### 14.2 Registers

Register presentation must be architecture-neutral.

A generic model should describe:

-   register identifier;
-   display name;
-   bit width;
-   value;
-   group/category.

A PS5 plugin can therefore expose x86-64 registers while an Xbox 360
plugin exposes PowerPC registers through the same UI framework.

### 14.3 Call stack

Core should define and display stack frames.

The plugin or backend determines how stack unwinding is performed.

------------------------------------------------------------------------

## 15. Modules and Memory Maps

Core should define neutral representations for:

-   modules;
-   sections;
-   memory regions;
-   base addresses;
-   sizes;
-   permissions;
-   mapped names;
-   module-relative offsets.

Examples differ by platform:

``` text
PlayStation 5:
eboot.bin

Windows:
game.exe
engine.dll

Xbox 360:
default.xex
```

The presentation and navigation logic should remain shared.

------------------------------------------------------------------------

## 16. Cheat Project System

The application should have its own neutral internal cheat/project
representation.

It should not use PS5 JSON, SHN, MC4, Cheat Engine CT, or an Xbox
trainer format as its internal source of truth.

Core should own concepts such as:

-   project;
-   target metadata;
-   game/application metadata;
-   cheat groups;
-   cheat entries;
-   descriptions;
-   notes;
-   operation ordering;
-   enable/disable state;
-   validation framework;
-   undo/redo;
-   copy/paste.

The exact project file extension and schema will be defined later.

------------------------------------------------------------------------

## 17. Cheat Operations

A useful distinction should be made between generic and
platform-specific cheat operations.

Generic operations may include:

-   memory write;
-   freeze value;
-   patch bytes;
-   restore bytes;
-   NOP instruction;
-   pointer write;
-   conditional write.

Plugins may register additional operations.

Examples could include:

### PlayStation 5

-   platform-specific code cave;
-   master-code concept where required by an export format;
-   dependency handling;
-   PS5 process/module-specific operations.

### Windows

-   AOB injection;
-   Windows-specific code injection;
-   DLL-related operations where appropriate.

### Xbox 360

-   PowerPC-specific branch patching;
-   trainer-specific operations.

Core should render operation editors through plugin-provided
descriptions or editors without embedding platform rules.

------------------------------------------------------------------------

## 18. Plugin-defined Cheat Metadata

Plugins must be able to define the metadata required for their platform.

For example, a PS5 plugin may require:

-   Title ID;
-   game version;
-   process;
-   module;
-   optional firmware/backend compatibility information.

An Xbox 360 plugin may require different identifiers.

A Windows plugin may instead require:

-   executable;
-   process name;
-   architecture.

The Core cheat editor should construct appropriate fields from plugin
metadata definitions rather than hard-code every platform.

------------------------------------------------------------------------

## 19. Cheat Validation

Plugins should own platform-specific validation.

Before applying or exporting a cheat, the plugin should be able to
validate:

-   required metadata;
-   supported operations;
-   address constraints;
-   architecture-specific instructions;
-   export-format restrictions;
-   dependency requirements;
-   unsupported combinations.

Core should provide a common validation result model so all plugins
produce consistent user-facing errors and warnings.

------------------------------------------------------------------------

## 20. Cheat Export

Cheat export formats should be provided by plugins.

A plugin can register one or more exporters.

Possible PS5 formats may include:

-   JSON;
-   MC4;
-   SHN;
-   SHNEXT;
-   raw patch representations.

Other platforms can provide their own formats.

The internal project representation must remain independent from these
exports.

------------------------------------------------------------------------

## 21. Universal List and Table Export

A major project-wide requirement is that **all meaningful lists and
tables should be exportable**. Application `0.1.4.rev1` introduces the first production implementation of this direction, with its initial Windows compile path corrected in `0.1.4.rev2`; `0.1.4.rev3` adds exportable memory-map Protection and directly streamed indented JSON without changing the generic schema version. Application `0.1.4.rev4` attempted a host-only Saved Addresses Protection alignment correction through the DataGrid cell container, but runtime verification showed that the generated text element remained misaligned; `0.1.4.rev5` corrects the presentation with an explicit vertically centered template. The export contract and data semantics remain unchanged. One neutral Core tabular writer/data-source model is consumed by Scan Results and Saved Addresses. The exact current schema and supported scopes are documented in `UNIVERSAL_EXPORT.md`; later list-based tools should reuse that infrastructure where it remains appropriate rather than treating the examples in this early guide as a frozen API.

This applies to, among other things:

-   scan results;
-   saved addresses;
-   pointer scan results;
-   disassembly;
-   memory regions;
-   modules;
-   threads;
-   breakpoints;
-   watchpoints;
-   debugger hit lists;
-   call stacks;
-   cheat entries;
-   logs;
-   symbols where applicable.

This should be implemented as shared Core infrastructure rather than
separately for every screen.

### 21.1 Standard export formats

The initial generic formats should be:

-   **JSON**
-   **CSV**
-   **TSV**
-   **Markdown table**

Plain-text copy should also be available where useful.

### 21.2 Export scopes

Where applicable, export should allow:

-   all rows;
-   selected rows;
-   currently filtered rows.

The user should also be able to choose which columns are included.

### 21.3 Common context-menu behavior

List-based views should use consistent actions such as:

``` text
Copy
Copy Selected
Copy As...
────────────────
Export...
Export Selected...
────────────────
Select All
```

Specialized views may add context-specific copy commands.

For example, disassembly may additionally provide:

-   Copy Address;
-   Copy Bytes;
-   Copy Instruction;
-   Copy Address + Instruction.

### 21.4 Structured JSON

JSON should preserve structured data and useful metadata instead of
simply serializing displayed strings.

A scan-results export may contain:

``` json
{
  "type": "scan-results",
  "schemaVersion": 1,
  "target": {
    "platform": "PlayStation 5",
    "plugin": "ps5debug-ng",
    "process": "eboot.bin"
  },
  "results": [
    {
      "address": "0x20752D3A0",
      "valueType": "float32",
      "currentValue": 100.0,
      "previousValue": 82.0,
      "module": "eboot.bin",
      "offset": "0x152D3A0"
    }
  ]
}
```

Where practical, structured JSON exports should be designed so they may
later support re-import.

### 21.5 Streaming large exports

Large datasets must be streamed directly to disk.

Exporting tens of millions of scan results must not require creating
tens of millions of additional WPF or serialization objects in memory
first.

Large exports should support:

-   progress;
-   cancellation;
-   asynchronous operation;
-   efficient buffered writing;
-   safe handling of partial/cancelled output.

------------------------------------------------------------------------

## 22. Scripting

A shared scripting subsystem is desirable for later development.

The scripting API should expose neutral Core concepts such as:

-   target memory read/write;
-   scanner operations;
-   saved addresses;
-   debugger operations;
-   cheat operations;
-   project data.

Platform-specific functionality may be exposed conditionally based on
plugin capabilities.

The scripting language and implementation should be evaluated later. No
language should be selected solely because an existing project uses it.

------------------------------------------------------------------------

## 23. Lessons From Existing Projects

Three existing projects are especially relevant during architectural
research.

### 23.1 Cheat Engine

Cheat Engine demonstrates mature implementations and workflows for:

-   first/next memory scans;
-   scan result management;
-   unknown-value scanning;
-   pointer scanning;
-   address tables;
-   freeze behavior;
-   memory viewing;
-   disassembly;
-   debugging;
-   find-what-writes/accesses workflows;
-   scripting;
-   cheat tables.

It should be used as a behavioral and architectural reference where
appropriate, not as a requirement to reproduce its UI or internal
implementation.

### 23.2 MemoryEngine360

MemoryEngine360 is particularly relevant because it demonstrates that a
substantial amount of memory-tool functionality can be shared across
different connection types and targets.

Relevant concepts include:

-   generic console connections;
-   memory scanning;
-   saved addresses;
-   pointer scanning;
-   memory viewer;
-   repeated/task-based operations;
-   scripting;
-   plugin/custom connection support;
-   support for more than one console family;
-   operation against memory dumps.

Its architecture provides practical evidence that the application's
high-level tools do not need to be tied to one console.

### 23.3 ps5debug-NG

ps5debug-NG provides the first intended live-target backend for this
project.

It supplies the PS5-side functionality required for remote memory and
debugging operations.

The application should use its documented protocol through a dedicated
PS5/backend implementation while keeping protocol-specific details out
of Core.

------------------------------------------------------------------------

## 24. Code Reuse and Licensing Direction

Existing open-source projects should initially be treated as references
for:

-   algorithms;
-   architecture;
-   behavior;
-   protocol understanding;
-   edge cases;
-   user workflows.

Direct source-code reuse must not occur casually.

Before incorporating code from another project, its license and the
consequences for the entire project must be evaluated.

Where practical, the preferred approach is to implement clean C# code
based on documented behavior, public protocol specifications, and
independently understood algorithms.

This is especially important because the project is intended to become a
public codebase.

------------------------------------------------------------------------

## 25. Early User Interface Direction

Although the architecture must not be driven by UI alone, the intended application is a modern desktop memory-development tool. Cheat Engine is an important workflow reference because its target-selection, scan-results, scan-controls, and saved-address separation is familiar and effective, but TeeKay87's Memory Engine must not become a pixel-for-pixel visual clone.

The primary scanner workspace should deliberately preserve the recognizable workflow:

``` text
Application / Theme bar
        ↓
Target / Connection bar
        ↓
┌────────────────────────────────────┬─────────────────────┐
│ Scan Results                       │ Scan Controls       │
│ temporary scan candidates          │ First / Next / New  │
└────────────────────────────────────┴─────────────────────┘
        ↓
Saved Addresses
persistent user-selected addresses
```

The distinction between Scan Results and Saved Addresses is architectural as well as visual. Scan Results are transient candidates belonging to the active scan session. Saved Addresses are persistent user selections that may later support value editing, freezing, navigation, pointer analysis, disassembly, project/cheat integration, and export. Starting or refining a scan must not implicitly replace the saved-address table.

The target/connection context should remain visible while the user works. The selected process and explicit Active Target should remain separate so merely browsing a process list cannot silently redirect future memory operations.

Additional common workspaces should eventually include:

``` text
Memory Viewer
Disassembler
Debugger
Pointer Scanner
Cheat Editor
Project
Logs
Settings
```

The same primary UI should be used regardless of active platform. Platform plugins may add metadata, commands, editors, or optional panels through controlled extension points, but should not replace the entire application UI. Generic feature availability should be driven by capabilities and session services instead of literal platform-name checks.

### 25.1 Shared WPF control styling

Reusable application controls should use central WPF resources rather than copy control templates, state triggers, colors, or interaction behavior into individual views.

For standard buttons, the application should maintain a small set of semantic styles such as Primary, Secondary, and Danger on top of one shared base template. The base template should own common interaction states including normal, hover, pressed, keyboard focus, default action, and disabled presentation. Disabled controls must remain legible in every supported color theme and must not fall back to operating-system chrome that conflicts with the application palette.

The same central styling principle applies to frequently reused input and list controls such as TextBox, ComboBox, CheckBox, and DataGrid. Views remain responsible for local layout requirements such as margins or minimum width, while ordinary control visuals and interaction states belong to shared presentation resources.

Informational badges, toggles, check boxes, menu items, and other control categories should retain their own semantics instead of being made to look or behave like ordinary buttons merely for visual consistency.

Shared UI styling documentation belongs under `docs/ui/`, outside platform-plugin documentation.

### 25.2 Color themes

The host application should support color themes without allowing themes to redefine the UI. Theme data must therefore be external presentation data rather than executable XAML or replacement view definitions.

A theme may define the shared semantic palette used by the application's WPF resources, including window/panel surfaces, text, borders, inputs, accent colors, selection, disabled states, danger states, and status colors. Layout, control templates, bindings, commands, and feature behavior remain application-owned.

Theme switching should update the active WPF brush resources so an already-open workspace changes immediately without requiring a restart. The user's selected theme should be persisted between launches.

The initial palette set is:

``` text
Light
Dimmed
Dark
```

`Dark` preserves the original early application palette, while `Dimmed` intentionally sits between Light and Dark rather than being a second near-black theme. External theme files should be validated as complete palettes so a partially defined or malformed theme cannot leave stale colors from a previously active theme. Theme-system details belong under `docs/ui/`.

------------------------------------------------------------------------

## 26. Initial PS5 User Flow

A typical early PS5 workflow should eventually resemble:

``` text
Start application
      ↓
Select PlayStation 5 plugin
      ↓
Discover or enter PS5 IP
      ↓
Connect through ps5debug-NG
      ↓
Detect foreground game / select process
      ↓
Scan memory
      ↓
Narrow results
      ↓
Add address
      ↓
Inspect memory/disassembly
      ↓
Find what writes/accesses
      ↓
Create patch or cheat
      ↓
Test
      ↓
Save project
      ↓
Export to supported PS5 format
```

This workflow should be achievable without introducing PS5-specific
assumptions into generic Core components.

------------------------------------------------------------------------

## 27. Recommended Initial Development Order

Before implementing advanced PS5 functionality, the foundational
architecture should be established.

A reasonable early sequence is:

1.  Create the solution and project structure.
2.  Establish centralized application information/version handling.
3.  Define the initial Plugin SDK.
4.  Define target capability models.
5.  Implement plugin discovery/loading.
6.  Define target/session abstractions.
7.  Create the basic WPF/MVVM application shell.
8.  Implement the first PS5 ps5debug-NG plugin connection.
9.  Implement process/foreground-target discovery.
10. Establish the permanent Cheat Engine-inspired scanner workspace and shared color-theme infrastructure before continuing to add feature-specific temporary UI.
11. Implement memory maps. **Implemented for PS5 in 0.1.1.rev11 and live-verified together with rev12 on a real PS5.**
12. Implement raw memory read. **Implemented for PS5 in 0.1.1.rev12 and live-verified together with rev11 on a real PS5.**
13. Implement raw memory write with read-back verification. **Implemented for PS5 in 0.1.1.rev16 / PS5 plugin 0.1.0.rev5 and live-verified through the 0.1.1.rev17 Safe Write Test. The Safe Write Test was run twice against a real PS5 and returned PASS both times.**
14. Implement generic value encoding/decoding. **The first architecture-aware Int32 decode path is implemented inside the 0.1.2.rev1 shared scanner; broader reusable value codecs remain future work.**
15. Implement the generic scanner architecture. **Initial implementation added in 0.1.2.rev1 for 4 Bytes / Int32 + Exact Value + First Scan + Next Scan + New Scan + cancellation/progress. Live PS5 use proved the scanner can find/change a real game value; 0.1.2.rev2 hardens cancellation so an in-flight ps5debug-NG read is fully drained before Core observes cancellation, preserving same-session reuse.**
16. Integrate native PS5 scanning as an optional acceleration path. **First Scan acceleration was implemented in 0.1.2.rev4 / PS5 plugin 0.1.0.rev7 through Plugin API 1.1.0 `INativeValueScanner`, using negotiated ps5debug-NG TurboScan multi-segment/server-resident acceleration for Int32 Exact Value. Later revisions added resident refinement, all ps5debug-NG value widths, plugin Scan Options, and large-result streaming. Host 0.1.3.rev26 / PS5 plugin 0.1.0.rev18 / Plugin API 2.7.0 completes the generic semantic mapping boundary: Core owns the 13 standard predicates, each plugin may publish equivalent native mappings, and missing/incompatible mappings fall back to the shared Core scanner. PS5 now maps Exact/Fuzzy/ordered/previous-value predicates where semantics match and uses TurboScan snapshot-with-zero-inclusion for Core Unknown Initial Value; Increased By/Decreased By and floating Unknown Initial Low remain Core fallback because upstream semantics differ.**
17. Implement scan result storage and virtualization. **Completed through the rev9 storage lifecycle foundation and rev13 disk-backed massive-result implementation; live PS5 verification later proved a 10,953,954-result First Scan and complete-set Next Scan refinement.**
18. Complete the universal Core Scan Type set and native mapping model. **Implemented in 0.1.3.rev25-rev26 and refined through rev32: 13 Core-owned predicates, dynamic operand UI, previous-value/delta semantics, Plugin API semantic native mappings, backend-resident complete result sets, and PS5/Core fallback boundaries. The complete 0.1.3 scanner block was user-verified through rev32 before development advanced to 0.1.4.**
19. Implement saved addresses. **Implemented in 0.1.3.rev17 and refined through rev21-rev24 coordination/input work.**
20. Implement freeze/repeated writes. **Implemented for Saved Addresses in 0.1.3.rev17, split into independent refresh/Frozen schedules in rev18, hardened in rev19, and coordinated with foreground user intent in rev20-rev22.**
21. Implement universal export infrastructure. **Initial production foundation implemented in 0.1.4.rev1 and compile-corrected in 0.1.4.rev2 for Scan Results and Saved Addresses: shared JSON/CSV/TSV/Markdown writing, selectable scopes/columns, bounded streaming, progress/cancellation, transactional destination publication, and complete-set Scan Results support across materialized, disk-backed, and backend-resident sources. Rev3 adds neutral memory-map Protection columns/export and human-readable streamed JSON. Later list-based tools, filtered-view scope, copy variants, and any re-import contracts remain future extensions rather than requirements forced into the initial 0.1.4 work.**
22. Implement the full Memory Viewer. **Read-only foundation implemented in 0.1.5.rev1 with neutral bounded Core reads, a themed Address/Hex/ASCII WPF viewer, Region / Module + Protection context, Go To/Refresh, Scan Results/Saved Addresses Browse Memory entry points, target/connection-generation safety, and two new Core verification checks. Corrective 0.1.5.rev2-rev3 repaired the WPF/C# compile issues discovered during the first Windows builds. Application 0.1.5.rev4 added independent extended row selection, clipboard actions, Back/Forward address history, and persistent green origin-row highlighting. Application 0.1.5.rev5 corrected selected-origin row geometry and was fully user-verified. Application 0.1.5.rev6 added safe single-row raw-byte editing with stale-source rejection, writable-region enforcement, and immediate read-back verification. Application 0.1.5.rev7 added viewer-local bookmarks plus previous/start/end/next readable-region navigation. Application 0.1.5.rev8 completed and was fully verified for the feature block, preserving those workflows while correcting bookmark selection presentation, Active Target scan gating, semantic Danger-button styling, and centralized version/revision presentation.**
23. Implement architecture-neutral disassembly contracts and host workspace. **Initial production foundation implemented in 0.1.6.rev1 with Plugin API 2.10.0 `IDisassemblerProvider`, neutral instruction/flow-control models, bounded Core read/decode validation, target/connection identity, and deterministic Mock provider coverage. Host 0.1.6.rev3 added the first modeless Disassembler workspace, rev4 added bounded 512-byte pre-origin/512-byte post-origin context plus Scan Results/Saved Addresses entry points, and rev5 removed the artificial split-at-origin decode seam so a requested byte inside a multi-byte instruction can resolve to the containing instruction while the start of arbitrary variable-length context remains explicitly best-effort. Host 0.1.6.rev6 advances the API to 2.11.0 with optional neutral syntax-presentation tokens, activates successful-address Back/Forward history, and adds target-safe Memory Viewer **Open in Disassembler** navigation without changing the three-column list geometry.**
24. Implement PS5 disassembly. **Implemented in PS5 plugin 0.1.0.rev23 / host 0.1.6.rev2 through Plugin API 2.10.0 using plugin-private Iced 1.21.0 x86-64 decoding; live executable-memory decode was verified during the 0.1.6.rev3 runtime pass. Host rev4-rev5 context/origin changes remain Core/WPF orchestration. Host 0.1.6.rev6 / PS5 plugin 0.1.0.rev24 adopts Plugin API 2.11.0 and maps Iced formatter output to neutral syntax tokens while leaving decode semantics and ps5debug-NG transport unchanged.**
25. Implement debugger contracts.
26. Implement PS5 breakpoints/watchpoints.
27. Implement Find What Writes/Accesses.
28. Implement registers, threads, and call stack.
29. Implement pointer support.
30. Implement the neutral cheat-project model.
31. Implement plugin-defined cheat operations and metadata.
32. Implement PS5 cheat building and validation.
33. Implement PS5-specific exporters.
34. Add scripting only after the underlying APIs are stable enough to expose safely.

This sequence may change as implementation and testing reveal
dependencies.

------------------------------------------------------------------------

## 28. Version and Revision Discipline

The project must use centralized application information so title,
version, revision, and related metadata are not duplicated throughout
the codebase.

A dedicated `AppInfo`-style component should become the authoritative
source for values such as:

-   application title;
-   version;
-   revision;
-   other stable application metadata.

Whenever version or revision changes, every displayed occurrence must
obtain the updated value from this centralized source.

During development:

-   revision increases while work remains within the same version;
-   version changes only when the functionality associated with that
    version has been completed and verified;
-   when a new version begins, revision numbering begins again at `1`;
-   version increments should remain conservative and do not need to be
    planned around reaching `1.0.0` at a predetermined project milestone.

Platform plugins have independent version and revision numbers. A plugin
must not inherit the host application's version simply because it ships
with that host revision. Plugin compatibility with the host must be
tracked separately through a Plugin API/contract version.

Release ZIP files must use:

``` text
TK87ME_<version>.rev<revision>___<Feature_title>.zip
```

------------------------------------------------------------------------

## 29. Documentation Requirements

All project documentation must be written in **English** and
**Markdown**.

The project root should contain at least:

``` text
README.md
CHANGELOG.md
docs/
```

### 29.1 README.md

`README.md` must describe the application as it currently exists.

It should explain:

-   purpose;
-   current functionality;
-   supported platforms/plugins;
-   installation;
-   usage;
-   relevant configuration;
-   limitations where appropriate.

It must **not** be used as a revision history or changelog.

It must be updated whenever a revision changes the application's current
functionality or usage.

### 29.2 CHANGELOG.md

Every version or revision change must be documented.

The heading for a revision must follow:

``` text
TeeKay87's Memory Engine <version>.rev<revision> - <Feature_title>
```

The entry must describe in detail:

-   what was added;
-   what was changed;
-   what was removed;
-   relevant implementation changes;
-   behavior changes;
-   compatibility implications where relevant.

CHANGELOG entries must describe actual changes in that revision rather
than becoming general project documentation.

### 29.3 docs/

The `docs/` directory should contain structured technical documentation
that does not belong in README or CHANGELOG.

Examples include:

-   architecture documentation;
-   protocol research;
-   test plans;
-   test results;
-   verification reports;
-   plugin SDK documentation;
-   scanner design;
-   debugger design;
-   export schemas;
-   platform-specific implementation notes.

Subdirectories should be introduced as documentation grows.

Every platform plugin must have its own dedicated documentation directory
under `docs/plugins/`. Only documentation belonging to that plugin should
be placed inside its directory. Protocol mappings, plugin-specific tests,
platform compatibility notes, and backend implementation details belong
there instead of being mixed into general Core documentation.

This architecture guide should eventually live in an appropriate
location under `docs/`.

------------------------------------------------------------------------

## 30. Pre-change Review Requirements

Before making code changes to an existing project revision:

1.  Read `README.md`.
2.  Read `CHANGELOG.md`.
3.  Read every relevant Markdown document under `docs/`.
4.  Review the complete current codebase.
5.  Identify existing functionality that can be reused or extended.
6.  Avoid duplicate implementations.
7.  Confirm that the supplied source is the latest known codebase when
    there is uncertainty.
8.  Identify whether the requested change requires modifying previously
    verified functionality.

Application `0.1.5.rev2` introduced a repository source-preflight helper and rev3 added one narrow C# declaration-space rule. Further development of `tools/preflight/` is paused from `0.1.5.rev4`; the existing files may remain available, but current feature work should not expand that subsystem unless the decision is explicitly revisited. The real Windows/Roslyn/WPF build remains the authoritative compile gate.

Code changes should build on the latest verified codebase.

When a previously implemented feature must be changed or removed to
support new work, that fact and the intended change must be identified
before implementation.

All C# source files must include the required `using` directives for the
code they contain.

------------------------------------------------------------------------

## 31. Public Codebase Quality

The project is intended to be public.

Code and documentation must therefore be written as maintainable
production project material.

This includes:

-   meaningful naming;
-   consistent formatting;
-   appropriate comments;
-   clear documentation;
-   useful error messages;
-   sensible separation of responsibilities;
-   avoiding unnecessary abstractions;
-   avoiding duplicated code;
-   avoiding placeholder-quality implementations being presented as
    complete;
-   documenting important architectural decisions.

------------------------------------------------------------------------

## 32. Architectural Rule of Thumb

When deciding whether functionality belongs in Core or a plugin, use the
following rule:

> If the feature describes what a memory-development tool does, it
> probably belongs in Core. If it describes how a particular target
> makes that operation possible, it probably belongs in the plugin.

Examples:

``` text
"Scan memory for a Float32 value"
    → Core

"Send a ps5debug-NG native scan command"
    → PS5 plugin

"Display registers"
    → Core

"Decode the PS5 x86-64 register response"
    → PS5 plugin

"Create a cheat project"
    → Core

"Export that cheat as SHN"
    → PS5 plugin

"Export scan results as CSV"
    → Core

"Resolve eboot.bin metadata"
    → PS5 plugin
```

This distinction should be maintained throughout development.

------------------------------------------------------------------------

## 33. Current Architectural Decision

The project should proceed as a **general-purpose modular memory
scanning, debugging, and cheat-development application**.

PlayStation 5 support through ps5debug-NG will be the first fully
developed target, but it must be implemented as a plugin using the same
public Plugin SDK intended for future platforms.

The initial architecture should deliberately make future support
possible for:

-   Windows PC;
-   Xbox 360;
-   PlayStation 3;
-   PlayStation 4;
-   emulators;
-   offline memory dumps;
-   additional future targets.

The goal is not to produce multiple platform-specific applications with
similar interfaces.

The goal is to build **one shared application and one shared Core**,
with target plugins supplying the capabilities and platform knowledge
required to operate against each system.
