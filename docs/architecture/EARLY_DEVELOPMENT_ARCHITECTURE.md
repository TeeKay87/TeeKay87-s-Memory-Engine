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

The same scan workflow should be available for every compatible
platform:

-   Exact Value
-   Unknown Initial Value
-   Changed Value
-   Unchanged Value
-   Increased Value
-   Decreased Value
-   Greater Than
-   Less Than
-   Between
-   AOB / byte pattern

Additional scan modes can be introduced later.

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
Scan request
     │
     ├── Native scanner supported for this operation
     │       └── Plugin performs scan
     │
     └── No native implementation
             └── Core generic scanner
```

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

The saved address system should be shared by all targets.

An entry may include:

-   enabled state;
-   description;
-   address expression;
-   resolved address;
-   data type;
-   current value;
-   display format;
-   freeze state;
-   freeze value;
-   group;
-   notes;
-   hotkeys;
-   pointer information.

Addresses should not be restricted to absolute addresses.

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

Core can maintain scheduled or repeated writes while the plugin performs
the actual memory operation.

This subsystem should be designed to support:

-   fixed-value freeze;
-   periodic write;
-   enable/disable;
-   configurable interval where appropriate;
-   safe cancellation when disconnecting;
-   grouped operations.

A future generalized task/action sequencer may build on the same
infrastructure.

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
tables should be exportable**.

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

Although the architecture must not be driven by UI alone, the intended
application is a modern desktop tool rather than a visual clone of Cheat
Engine.

The WPF shell should eventually provide common workspaces such as:

``` text
Target / Connection
Scanner
Scan Results
Saved Addresses
Memory Viewer
Disassembler
Debugger
Pointer Scanner
Cheat Editor
Project
Logs
Settings
```

The same primary UI should be used regardless of active platform.

Platform plugins may add metadata, commands, editors, or optional panels
through controlled extension points, but should not replace the entire
application UI.

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
10. Implement memory read/write.
11. Implement memory maps.
12. Implement generic value encoding/decoding.
13. Implement the generic scanner architecture.
14. Integrate native PS5 scanning as an optional acceleration path.
15. Implement scan result storage and virtualization.
16. Implement universal export infrastructure.
17. Implement saved addresses.
18. Implement freeze/repeated writes.
19. Implement Memory Viewer.
20. Implement architecture-neutral disassembly contracts.
21. Implement PS5 disassembly.
22. Implement debugger contracts.
23. Implement PS5 breakpoints/watchpoints.
24. Implement Find What Writes/Accesses.
25. Implement registers, threads, and call stack.
26. Implement pointer support.
27. Implement the neutral cheat-project model.
28. Implement plugin-defined cheat operations and metadata.
29. Implement PS5 cheat building and validation.
30. Implement PS5-specific exporters.
31. Add scripting only after the underlying APIs are stable enough to
    expose safely.

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
-   when a new version begins, revision numbering begins again at `1`.

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
