# TeeKay87's Memory Engine — Full Development Action Plan

**This document should be updated to reflect what is implemented and what is not.**

## Purpose

This document defines the long-term functional target for **TeeKay87's Memory Engine** and is intended to be used as a standing reference throughout development.

It combines:

- the functionality already implemented in TeeKay87's Memory Engine;
- the architecture and workflows already documented for the project;
- useful concepts and workflows from **Cheat Engine**;
- useful concepts and workflows from **MemoryEngine360**;
- platform-specific capabilities required for PlayStation 5, PC, Xbox 360, and future targets;
- project-specific functionality that goes beyond either reference application.

This document is **not a development-order roadmap**. The sections are grouped by subsystem and responsibility rather than by the order in which they must be implemented.

The goal is not to clone Cheat Engine or MemoryEngine360. TeeKay87's Memory Engine should adopt useful ideas and proven workflows while implementing them through its own C#/WPF architecture, neutral Core contracts, public Plugin SDK, platform plugins, shared safety rules, and project-specific workflows.

---

## Origin Legend

Every major feature in this document is tagged with its primary conceptual origin.

| Tag | Meaning |
| --- | --- |
| **TK87ME** | Designed specifically for TeeKay87's Memory Engine or significantly reworked into a project-specific design. |
| **CE** | Primarily inspired by a useful Cheat Engine feature or workflow. |
| **ME360** | Primarily inspired by a useful MemoryEngine360 feature or workflow. |
| **CE + ME360** | Both reference applications contain a substantially similar useful concept. |
| **Hybrid** | A CE/ME360 concept is being combined with project-specific architecture or functionality into a substantially broader TK87ME implementation. |
| **Platform-specific** | Behavior belongs to a platform plugin or backend rather than Core. |

The origin tags describe **feature inspiration and workflow provenance**, not source-code ownership. Unless explicitly documented otherwise, TeeKay87's Memory Engine should implement functionality independently rather than copying implementation code from external projects.

---

## Status Legend

| Status | Meaning |
| --- | --- |
| **Implemented** | Present in the currently verified or current candidate codebase. |
| **Current candidate** | Present in the current source revision but still awaiting its complete verification cycle. |
| **Planned** | Intended part of the final program and should be implemented. |
| **Future / optional** | Useful advanced functionality that should remain in scope if practical, but is not required before the primary toolchain is complete. |

The current development candidate is **TeeKay87's Memory Engine 0.1.7.rev31 — Safe PS5 Watchpoint Detach Cleanup**. The complete `0.1.6.rev14` Disassembler block remains fully tested and hardware-verified, and rev29 is accepted after **138/138** automated checks plus focused live-PS5 logical-disassembly/marker verification. Rev30 added the Breakpoint/Watchpoint **Type** versus Software/Hardware **Mechanism** split plus validated **Add Breakpoint** / **Add Watchpoint** actions for Scan Results and Saved Addresses, but it was superseded before formal verification when live shutdown testing exposed a target-safety problem: a hardware watchpoint could remain armed after the client exited and later terminate the game when the watched access occurred. Rev31 advances the PS5 plugin to `0.1.0.rev38` and explicitly disables every backend-active or staged client-owned hardware-watchpoint slot before backend detach/disposal, while preserving rev36's equivalent software-breakpoint restore pass. Plugin API remains `2.16.0`, Mock remains `1.0.0.rev16`, and the registry target is **142 checks**. The external ps5debug-NG watchpoint-teardown defect and the previously documented same-instruction breakpoint/watchpoint event-consumption behavior are both recorded under `docs/bug-reports/`. Individual PS5 Thread Suspend/Resume remains implemented but externally blocked by the tested backend returning `CMD_ERROR`.

---

# 1. Platform Architecture, Core, Plugin SDK, and Plugin System

## Objective

The final application must remain a **single reusable memory-engine host** rather than evolving into several platform-specific applications. Shared behavior belongs in Core or the Plugin SDK. Platform-specific behavior belongs in the corresponding plugin.

## Required functionality

- **[TK87ME — Implemented] Modular solution architecture** separating:
  - WPF host application;
  - shared Core;
  - public Plugin SDK;
  - platform plugins;
  - verification/test projects.
- **[TK87ME — Implemented] Capability-driven plugins.** A plugin declares what it supports, such as process enumeration, memory read/write, scanning, target pause/resume, disassembly, debugging, pointer scanning, assembly, remote file operations, or future platform-specific capabilities.
- **[TK87ME — Implemented] Capability-driven UI.** Host controls must be enabled, disabled, hidden, or populated according to current plugin capabilities instead of hardcoding assumptions about PS5, PC, or Xbox 360.
- **[TK87ME — Implemented] Independent host and plugin versions.** Application version/revision, Plugin API version, and each plugin's own version/revision remain independent.
- **[TK87ME — Implemented] Runtime plugin discovery** from the application's plugin directory.
- **[TK87ME — Implemented] Isolated plugin loading** using collectible load contexts where applicable.
- **[TK87ME — Implemented] Plugin API compatibility validation.** Unsupported/incompatible plugins must fail clearly without destabilizing the host.
- **[TK87ME — Implemented] Duplicate plugin-ID protection.**
- **[TK87ME — Implemented] Plugin-owned Value Types.** The host must never assume every platform supports exactly the same Value Types.
- **[TK87ME — Implemented] Core-owned generic Scan Types.** Scan semantics are shared across platforms and only mapped to backend-native operations when a plugin can guarantee equivalent behavior.
- **[TK87ME — Implemented] Plugin-owned Scan Options.** Endianness, alignment, floating-point rounding, pause-target behavior, and future backend-specific controls are exposed through neutral plugin contracts.
- **[TK87ME — Implemented] Host-managed plugin settings.** Plugin settings are persisted under isolated plugin namespaces inside host-managed application configuration.
- **[TK87ME — Implemented] Neutral Target Architecture metadata** including CPU architecture, address width, pointer width, and endianness.
- **[TK87ME — Implemented] Active Target state separate from process selection.** Browsing a process must never silently retarget all memory tools.
- **[TK87ME — Implemented] Connection/session generation identity.** Modeless tools must remain bound to the target/process/session they were opened against and must reject stale access after reconnect or target changes.
- **[TK87ME — Implemented] In-Memory Mock Target** for deterministic regression tests without physical hardware.
- **[ME360 + TK87ME — Planned] Multiple backend/connection implementations for the same platform.** For example, a future Xbox plugin could support more than one transport while the host remains unchanged.
- **[TK87ME — Planned] Optional plugin-defined advanced services** such as remote files, debugger transport, assembler provider, symbol provider, script extensions, target allocation, memory protection changes, and hardware tracing.

## Architectural requirements

- No PS5, Xbox 360, x86, PowerPC, XBDM, JRPC, ps5debug, or other platform-specific semantics may leak into neutral Core or generic WPF presentation code.
- Core can define shared concepts and contracts, but platform-specific implementation belongs to plugins.
- The host must remain usable with future plugins without redesigning the main application.
- Optional functionality must fail by capability, not by assumptions about platform names.

---

# 2. Connections, Targets, Processes, Modules, and System Operations

## Objective

Provide a consistent target lifecycle that works for local PC processes, network-connected consoles, offline dumps, and future targets.

## Required functionality

- **[CE + ME360 + TK87ME — Implemented] Connect and Disconnect.**
- **[CE + ME360 + TK87ME — Implemented] Process enumeration.**
- **[TK87ME — Implemented] Preferred foreground process selection** without automatically changing Active Target.
- **[TK87ME — Implemented] Explicit Set Active Target workflow.**
- **[CE + ME360 + TK87ME — Implemented] Memory-map / region enumeration.**
- **[CE + ME360 + TK87ME — Implemented] Raw memory read/write.**
- **[TK87ME — Implemented] Safe writes with read-back verification.**
- **[TK87ME — Implemented] Memory protection metadata** where the backend provides it.
- **[TK87ME — Implemented] Target/process suspend and resume** where supported.
- **[CE + TK87ME — Planned] Allocate target memory** through a capability-driven API.
- **[CE + TK87ME — Planned] Free previously allocated target memory.**
- **[CE + TK87ME — Planned] Change memory protection** where the backend safely supports it.
- **[ME360 — Planned] Remote File Browser** for targets exposing filesystem capabilities.
- **[ME360 + TK87ME — Planned] Optional dedicated debugger connection/transport** when a backend benefits from separation between general memory traffic and debugger traffic.
- **[ME360 + TK87ME — Planned] Offline memory-file / dump target.** A dump must be able to expose read-only regions to the scanner, Memory Viewer, Disassembler, Pointer Scanner, structure tools, snapshot comparison, and related read-only workflows.
- **[CE — Planned] Save memory range/region to file.**
- **[CE — Planned] Load memory data from file into a writable range** with explicit bounds and safety checks.

## Safety requirements

- No tool opened against an old session may silently switch to a new target after reconnect.
- Explicit foreground operations must coordinate safely with background refresh/freeze operations.
- Platform-specific destructive operations must require appropriate capability checks and warnings.

---

# 3. Memory Scanner

## Objective

Deliver a scanner at least as useful as the common CE workflow, while preserving TK87ME's scalable disk-backed and backend-resident result architecture.

## Scan workflow

- **[CE + ME360 + TK87ME — Implemented] First Scan.**
- **[CE + ME360 + TK87ME — Implemented] Next Scan.**
- **[CE + ME360 + TK87ME — Implemented] New Scan.**
- **[TK87ME — Implemented] Explicit scan-session lifecycle.**
- **[TK87ME — Implemented] Scan cancellation and progress.**
- **[TK87ME — Implemented] Pause target while scanning** when supported.

## Core Scan Types

Currently implemented Core-owned Scan Types:

- **[TK87ME / CE-style workflow — Implemented] Exact Value**
- **[TK87ME — Implemented] Fuzzy Value**
- **[TK87ME — Implemented] Bigger Than**
- **[TK87ME — Implemented] Smaller Than**
- **[TK87ME — Implemented] Between**
- **[TK87ME — Implemented] Unknown Initial Value**
- **[TK87ME — Implemented] Unknown Initial Low Value**
- **[TK87ME — Implemented] Increased Value**
- **[TK87ME — Implemented] Decreased Value**
- **[TK87ME — Implemented] Changed Value**
- **[TK87ME — Implemented] Unchanged Value**
- **[TK87ME — Implemented] Increased By**
- **[TK87ME — Implemented] Decreased By**

Additional useful Scan Types to evaluate and add where semantics are sufficiently clear:

- **[CE — Planned] Not Equal.**
- **[CE — Planned] Greater Than or Equal.**
- **[CE — Planned] Less Than or Equal.**
- **[CE — Planned] Outside Range.**
- **[CE — Planned] Compare against saved/first-scan baseline.**
- **[CE — Future / optional] Percentage-based increase/decrease/tolerance.**
- **[CE — Future / optional] Grouped value search.**
- **[CE — Future / optional] Advanced signature/address-mask search modes.**

## Value Types

- **[CE + ME360 + TK87ME — Implemented] Signed and unsigned integer types** where declared by plugin.
- **[CE + ME360 + TK87ME — Implemented] Float.**
- **[CE + ME360 + TK87ME — Implemented] Double.**
- **[CE + TK87ME — Implemented] Array of Bytes.**
- **[CE — Planned] Wildcard/masked AOB**, for example `48 8B ?? ?? 89`.
- **[CE — Planned] ASCII/UTF-8 strings.**
- **[CE — Planned] UTF-16 strings.**
- **[CE — Future / optional] UTF-32 strings.**
- **[CE — Planned] Binary / bit-field values.**
- **[CE — Planned] Boolean.**
- **[Hybrid — Planned] Pointer Value Type** using the current target's pointer width and endianness.
- **[CE — Future / optional] Custom Value Types** through a controlled extension model.

## Scan options and filtering

- **[TK87ME — Implemented] Endianness option** where plugin-declared.
- **[TK87ME — Implemented] Alignment option** where plugin-declared.
- **[TK87ME — Implemented] Floating-point rounding option** where plugin-declared.
- **[CE + TK87ME — Planned] Read/write/execute protection filtering.**
- **[CE + TK87ME — Planned] Explicit start/end address range.**
- **[CE + TK87ME — Planned] Module-only scan scope.**
- **[TK87ME — Planned] Saved scan presets** for frequently reused scan configurations.

## Scalable result architecture

These are important TK87ME-specific requirements and must be preserved throughout future scanner work:

- **[TK87ME — Implemented] Complete result sets independent from displayed preview rows.**
- **[TK87ME — Implemented] Disk-backed result generations.**
- **[TK87ME — Implemented] Limited WPF presentation preview rather than one UI row per result.**
- **[TK87ME — Implemented] Backend-resident full result sets.**
- **[TK87ME — Implemented] Automatic Core/native fallback based on semantic compatibility.**
- **[TK87ME — Implemented] Deferred materialization of backend-resident sets when Core requires them.**
- **[TK87ME — Implemented] Transactional result publication and stale-session cleanup.**
- **[TK87ME — Implemented] Scanning remains correct beyond the displayed result subset.**
- **[TK87ME — Planned] Saved scan snapshots/baselines** that can be reopened and compared later.

---

# 4. Scan Results Workspace

## Objective

Keep Scan Results optimized for temporary discovery while allowing any useful result to flow into persistent tools.

## Required functionality

- **[CE + TK87ME — Implemented] Temporary scan result list.**
- **[TK87ME — Implemented] Address, current value, previous value, type, region/module, protection.**
- **[TK87ME — Implemented] Bounded live refresh of visible results.**
- **[CE + TK87ME — Implemented] Ctrl/Shift multi-selection.**
- **[CE + TK87ME — Implemented] Save Address for all selected results.**
- **[TK87ME — Implemented] Open in Memory Viewer.**
- **[TK87ME — Implemented] Open in Disassembler.**
- **[TK87ME — Implemented] Universal export with All, Displayed, and Selected scopes where applicable.**
- **[TK87ME — Implemented] JSON, CSV, TSV, and Markdown export.**
- **[TK87ME — Implemented] Full-set export even when only a bounded result preview is displayed.**
- **[TK87ME — Planned] Open in Pointer Scanner.**
- **[CE — Planned] Find What Writes / Reads / Accesses from a result address.**
- **[TK87ME — Planned] Add result directly to Cheat Project.**
- **[TK87ME — Planned] Send result to Structure Viewer.**

---

# 5. Saved Addresses / Persistent Address Table

## Objective

Create a persistent address table at least as useful as CE's memory-record workflow while integrating it with TK87ME's target/session safety, projects, and cross-workspace navigation.

## Current functionality

- **[CE + TK87ME — Implemented] Separate Saved Addresses table.**
- **[CE + TK87ME — Implemented] Description.**
- **[CE + TK87ME — Implemented] Address.**
- **[CE + TK87ME — Implemented] Value.**
- **[CE + TK87ME — Implemented] Value Type.**
- **[TK87ME — Implemented] Protection column.**
- **[CE + TK87ME — Implemented] Edit value.**
- **[CE + TK87ME — Implemented] Freeze.**
- **[TK87ME — Implemented] Independent read-refresh and freeze cadences.**
- **[TK87ME — Implemented] Dedicated/concurrent frozen-write transport on PS5.**
- **[TK87ME — Implemented] Open in Memory Viewer.**
- **[TK87ME — Implemented] Open in Disassembler.**
- **[CE + TK87ME — Implemented] Add Manually.**
- **[TK87ME — Implemented] Universal export.**

## Add Manually expansion

The current dialog intentionally exposes placeholders for unsupported CE-style fields. These should become functional over time:

- **[CE — Planned] Pointer mode with base address and arbitrary offset chain.**
- **[CE — Planned] Hexadecimal display mode.**
- **[CE — Planned] Binary start bit.**
- **[CE — Planned] Unicode/string-specific options.**
- **[CE — Planned] Code page.**
- **[TK87ME — Planned] Live resolved pointer preview.**
- **[TK87ME — Planned] Validation against current target architecture and region map.**

## Freeze behavior and automation

- **[CE — Planned] Freeze Always.**
- **[CE — Planned] Freeze allowing increases.**
- **[CE — Planned] Freeze allowing decreases.**
- **[CE — Planned] Hotkeys per Saved Address.**
- **[CE — Planned] Hotkey actions for toggle, activate, deactivate, set value, increase, and decrease.**
- **[TK87ME — Planned] Optional per-entry update/freeze interval.**
- **[TK87ME — Planned] Optional per-group interval or grouped action.**

## Organization

- **[CE + TK87ME — Planned] Groups/folders.**
- **[CE — Planned] Parent/child memory records.**
- **[CE — Planned] Activate/deactivate child entries with parent.**
- **[TK87ME — Planned] Group freeze/unfreeze.**
- **[CE + TK87ME — Planned] Drag-and-drop/reorder.**
- **[TK87ME — Planned] Persistent storage through Cheat Projects instead of application settings.**

## Cross-tool actions

- **[CE — Planned] Find What Writes / Reads / Accesses.**
- **[TK87ME — Planned] Open in Structure Viewer.**
- **[TK87ME — Planned] Open in Pointer Scanner.**
- **[TK87ME — Planned] Convert to Cheat Project operation.**

---

# 6. Memory Viewer / Hex Editor

## Objective

Provide a safe, modeless, architecture-neutral hex viewer/editor suitable for both quick inspection and debugger/reverse-engineering workflows.

## Required functionality

- **[CE + ME360 + TK87ME — Implemented] Address / Hex / ASCII presentation.**
- **[TK87ME — Implemented] Bounded memory reads within readable regions.**
- **[TK87ME — Implemented] Region/module/protection information.**
- **[CE + TK87ME — Implemented] Go To address.**
- **[TK87ME — Implemented] Back / Forward history.**
- **[TK87ME — Implemented] Bookmarks.**
- **[TK87ME — Implemented] Previous Region / Region Start / Region End / Next Region.**
- **[CE + TK87ME — Implemented] Selection and copy.**
- **[CE + ME360 + TK87ME — Implemented] Safe memory editing.**
- **[TK87ME — Implemented] Stale-data check before edit/write.**
- **[TK87ME — Implemented] Writable-region enforcement.**
- **[TK87ME — Implemented] Read-back write verification.**
- **[TK87ME — Implemented] Full value-span highlighting** when the caller supplies the value size.
- **[TK87ME — Implemented] Cross-row span highlighting.**
- **[TK87ME — Implemented] Open in Disassembler.**

## Planned extensions

- **[CE — Planned] Find/search inside current memory region.**
- **[CE — Planned] Search for bytes, integers, floating point values, text, and AOB patterns inside the current region.**
- **[CE — Planned] Fill selected range.**
- **[CE — Planned] Save selected/ranged memory to file.**
- **[CE — Planned] Load data from file into selected/ranged memory.**
- **[TK87ME — Planned] Interpret Selection panel**, showing the selected bytes as multiple primitive types, pointer, float/double, and strings.
- **[TK87ME — Planned] Open selected range in Structure Viewer.**
- **[CE + TK87ME — Planned] Find What Writes / Reads / Accesses for selected byte span.**
- **[CE + TK87ME — Future / optional] Snapshot/diff overlay.**

---

# 7. Disassembler

## Objective

Provide a shared architecture-neutral Disassembler workspace whose decoding details remain plugin-owned.

## Existing/candidate functionality

- **[TK87ME — Implemented] Neutral `IDisassemblerProvider` contract.**
- **[TK87ME — Implemented] PS5 x86-64 provider.**
- **[TK87ME — Implemented] Synthetic Mock instruction provider.**
- **[TK87ME — Implemented] Bounded context around origin.**
- **[TK87ME — Implemented] Approximately 512 bytes before and 512 bytes after/requested from origin where region bounds allow.**
- **[TK87ME — Implemented] Continuous decode stream across origin.**
- **[TK87ME — Implemented] Mid-instruction origin resolution.**
- **[TK87ME — Implemented / verified rev29] Address / Bytes / Markers / Instruction columns.** `Markers` carries debugger-owned presentation metadata without replacing the code bytes or instruction text.
- **[TK87ME — Implemented / verified rev29] Logical debugger-aware code presentation.** A matching debugger session can overlay captured original Software/Execute breakpoint bytes into a local disassembly buffer before decode so backend `INT3` instrumentation never appears as user-visible game code. Hardware watchpoint hits contribute marker metadata only and never replace instruction bytes.
- **[TK87ME — Implemented] Neutral syntax-token highlighting.**
- **[TK87ME — Implemented] Back / Forward.**
- **[TK87ME — Implemented] Go To / Refresh.**
- **[TK87ME — Implemented] Follow Target for known direct calls, jumps, and conditional branches.**
- **[TK87ME — Implemented] Indirect branches remain non-navigable unless a provider/debugger can prove the target.**
- **[TK87ME — Implemented] Previous Region / Region Start / Region End / Next Region.**
- **[TK87ME — Implemented] Module-relative origin presentation.**
- **[TK87ME — Implemented] Ctrl/Shift multi-selection with context-menu preservation when any already-selected row is right-clicked.**
- **[CE + TK87ME — Implemented] Copy Address / Bytes / Instruction / Address + Instruction / Selected with selection-consistent multi-row behavior and display-order normalization.**
- **[TK87ME — Implemented] Structured JSON/CSV/TSV/Markdown export.** Rev29 extends Disassembler export with the same `Markers` metadata shown in the table.

## Planned extensions

- **[CE — Planned] Symbol-aware disassembly.**
- **[CE — Planned] User-defined labels/symbols.**
- **[CE — Planned] Cross references: callers, jump references, data references.**
- **[CE — Planned] Code bookmarks / code list.**
- **[TK87ME — Planned] Highlight debugger current instruction.**
- **[TK87ME — Planned] Dynamic resolution of indirect branch target when debugger register context is available.**
- **[TK87ME — Planned] Open instruction in assembler/patch editor.**
- **[CE — Planned] Find What Addresses This Instruction Reads/Writes.**
- **[CE — Planned] Break and Trace from selected instruction.**

---

# 8. Assembler, Instruction Editing, Patching, and Code Injection

## Objective

Turn the read-only Disassembler into a safe code-editing environment without putting architecture-specific assembly semantics in Core.

## Required functionality

- **[Hybrid — Planned] Architecture-neutral assembler-provider contract.**
- **[CE — Planned] Assemble one or more instructions at an address.**
- **[CE — Planned] Replace selected instruction.**
- **[CE — Planned] Replace instruction/range with NOPs.**
- **[CE — Planned] Restore original bytes.**
- **[TK87ME — Planned] Capture original bytes automatically before patching.**
- **[TK87ME — Planned] Verify patched bytes with read-back.**
- **[TK87ME — Planned] Reject patch if target/session has become stale.**
- **[CE — Planned] Allocate executable memory/code cave where backend supports it.**
- **[CE — Planned] Free allocated memory.**
- **[CE — Planned] Code injection templates.**
- **[CE — Planned] Full injection template.**
- **[CE — Planned] AOB/signature injection template.**
- **[Hybrid — Planned] Generate AOB/signature from selected instruction range.**
- **[Platform-specific — Planned] Correct branch encoding for each architecture.**
- **[TK87ME — Planned] Enable/disable patch with automatic original-byte restoration.**
- **[TK87ME — Future / optional] Code-cave scanner.**
- **[TK87ME — Planned] Convert patch/injection directly into Cheat Project operation.**

---

# 9. Debugger

## Objective

Build a capability-driven debugger UI shared across supported plugins while allowing each backend to expose only what it can reliably implement.

## Current `0.1.7` Debugger Block

### Verified rev1 foundation

- **[TK87ME — Implemented / verified] Public `IDebuggerProvider` / `IDebuggerSession` boundary.** Connected target sessions can expose a debugger provider only when the plugin advertises the neutral `Debugger` capability; attached debugger sessions own run/pause/continue/detach and debugger-event delivery.
- **[TK87ME — Implemented / verified] Optional attached-session services** for thread enumeration, separate thread control, register access/editing, breakpoints/watchpoints, call stacks, and step execution.
- **[TK87ME — Implemented / verified] Neutral debugger data models** for execution state, stop reasons/events, threads, register bit-vectors and semantic roles, stack frames, breakpoint/watchpoint requests, and step kinds.
- **[TK87ME — Implemented / verified] Core debugger identity/event envelope and session coordinator** bound to plugin id, process id/name, and connection generation with monotonic session-local event sequencing and serialized lifecycle/control operations.
- **[TK87ME — Implemented / verified] Plugin API `2.12.0`.** Rev1 passed the complete Windows verification suite with **84/84** checks.

### Verified rev2 host workspace and Mock backend

- **[TK87ME — Implemented / verified] Capability-driven modeless Debugger workspace.** The main target strip exposes **Debugger...** only for plugins advertising `Debugger`; each window captures one Active Target and connection generation and uses the shared Core coordinator instead of calling a backend directly.
- **[TK87ME — Implemented / verified] Explicit Attach / Pause / Continue / Detach workflow.** Command availability follows current target identity and debugger lifecycle state. Starting a new attachment additionally requires the host's normal target-operation availability gate.
- **[ME360 + TK87ME — Implemented / verified] Debug event viewer/log.** The workspace shows neutral sequence/time/event/state/stop-reason/thread/instruction-pointer/message data in a bounded virtualized table and can clear the current presentation history.
- **[TK87ME — Implemented / verified] Host-owned debugger-session lifetime and stale-window rejection.** Active coordinators are disposed before target-session replacement/disconnect, Active Target changes, or plugin shutdown. Operations/events are revalidated against plugin/process/connection generation; transient foreground operations do not redefine target identity.
- **[TK87ME — Implemented / verified] Deterministic Mock debugger backend.** Mock `1.0.0.rev8` targets API `2.12.0`, advertises only the coarse `Debugger` capability, starts attachments in Running state, produces deterministic pause/resume events, enforces one attachment, and cleans up on detach/window close/disconnect.
- **[TK87ME — Verified] Rev2 acceptance.** The Windows verification suite passed **89/89** checks and the complete focused Mock runtime/UI acceptance passed, including capability gating, modeless open, attach, repeated pause/continue events, event-history clearing, detach/reattach, window-close cleanup, disconnect/reconnect generation invalidation, multiple-window exclusivity/recovery, and regression smoke testing.

### Verified through rev5 — PS5 Debug Transport and Attach/Detach

- **[TK87ME / ps5debug-NG — Implemented / verified] PS5 debugger provider.** PS5 plugin `0.1.0.rev25` targets API `2.12.0`, advertises the coarse `Debugger` capability, and exposes `IDebuggerProvider` from a connected `Ps5TargetSession`. Advanced debugger capability flags remain off.
- **[TK87ME / ps5debug-NG — Implemented / verified] Dedicated debugger command transport.** A debugger attachment opens its own TCP command connection to the configured ps5debug-NG server instead of sharing the established memory/scan command stream or Frozen-write connection.
- **[TK87ME / ps5debug-NG — Implemented / verified] Required outbound event channel.** The plugin listens on TCP `755` before sending `CMD_DEBUG_ATTACH` (`0xBDBB0001`), allowing ps5debug-NG to connect back to the host for asynchronous debugger interrupts.
- **[TK87ME / ps5debug-NG — Implemented / verified] Real Attach / Detach / Pause / Continue.** Attach and detach use `0xBDBB0001` / `0xBDBB0002`. Attached execution control uses `CMD_DEBUG_CONTINUE` / stop-go (`0xBDBB0010`) with action `1` for pause and `0` for resume. These debugger commands are separate from the already verified non-debugger `IProcessControl` path.
- **[TK87ME / ps5debug-NG — Implemented; natural-hit runtime gate deferred] Async interrupt translation.** The plugin consumes the fixed 1184-byte interrupt packet and maps only neutral thread id, wait-status-derived signal/stop reason, thread name, and instruction pointer into Core events. Register/FPU/debug-register wire blocks stay plugin-private.
- **[TK87ME / ps5debug-NG — Implemented / hardware-verified in rev25] Correct stop-state semantics.** Ordinary debugger interrupts still map to neutral `Paused` state, but current ps5debug-NG handles a matched software breakpoint specially: it restores the original byte, rewinds the packet RIP, single-steps that instruction, rearms `INT3`, and only then sends the original logical pre-instruction packet to the client. Rev25 treats the packet register snapshot as the authoritative logical stop for the matching managed software breakpoint while keeping ordinary pause/step contexts on the existing live-register path. Live rev25 verification confirmed event/IP/Registers/Call Stack agreement on both a normal instruction and the `0xE9F74E` call, plus correct one-step reconciliation. The external divergence and exact backend sequence are documented in `docs/bug-reports/ps5debug-ng-software-breakpoint-event-and-live-register-state-diverge.md`.
- **[TK87ME — Implemented / verified] PS5 debugger ownership/cleanup.** One active PS5 debugger attachment is allowed per target session. Explicit detach, debugger-window disposal, target-session disposal, and attach failure close the event listener/socket and dedicated command connection without reusing the primary target transport.
- **[TK87ME — Implemented / verified] Permanent two-row target header.** Rev4 established Row 1 as Platform -> plugin-declared connection inputs -> Connect -> Disconnect -> Target Process -> Refresh -> Set Active Target, and Row 2 as Reload Plugins -> Disassembler -> Debugger. Rev5 retains that exact order. Future permanent top-level action/tool buttons must be appended to row 2. Memory-map status and Plugin details may share the second-row surface without changing the action order. The redundant `Active <process>` and permanent process-count/status prose remain removed, and connection state remains the leftmost bottom-status item with theme-aware red/green status-box presentation.
- **[TK87ME — Implemented / verified] Rev5 responsive top-input metric.** `UiMetrics.TopTargetInputMaxWidth = 180` is the host-owned maximum for Platform, Target Process, and every ordinary plugin-declared TextBox/ComboBox rendered in row 1, including PS5 Port. All ordinary row-1 inputs share one computed width. The host reserves fixed Connect/Disconnect/Refresh/Set Active Target widths plus the existing inter-control spacing and uses the target bar's actual width after left/right padding is removed to reduce the shared input width as necessary. The row-1 input rule has no minimum width; fields may shrink below 180 rather than push controls beyond the right edge. Because the calculation explicitly subtracts the target-bar Border left/right padding from the measured width, the permanent left/right target margins remain protected. Future plugins inherit this host behavior and must not introduce platform-specific top-field widths or additional permanent target rows.
- **[TK87ME — Implemented / verified] Shared button text correction retained.** Normal buttons retain the existing `UiMetrics.StandardControlHeight = 34`, semantic styles, colors, border geometry, and interaction behavior. Only internal vertical content padding is reduced and generated string content is explicitly centered so descenders are not clipped. Rev5 does not alter that rev4 button fix.
- **[TK87ME — Implemented / verified] Rev5 verification.** The Windows suite passed **97/97**, focused UI/Mock acceptance passed, and the complete safe live-PS5 gate passed. The optional natural async-interrupt mapping scenario was not safely triggerable and remains deferred rather than failed.

Rev5 intentionally stopped before thread enumeration/control. Rev6 implemented that next layer, rev8 completed Registers and Stop Context, rev10 completed the guarded PS5 extended-register transport, rev15 completed verification of the Breakpoint Manager plus persistent/temporary software execute breakpoints, and rev16 completed Hardware Watchpoints. Rev17 implemented Call Stack, Call Frames and Stepping and passed 124/124 before runtime UI corrections became necessary. Rev18-rev23 corrected the WPF presentation/window-lifecycle issues, and rev24 replaced the problematic tab chrome with the accepted selector-button workspace. Rev25 corrected ps5debug-NG logical software-breakpoint stop context, rev26 corrected breakpoint-aware Step Over, and rev27/rev28 completed interrupted-operation temporary-breakpoint cleanup plus safe Detach behavior. Rev28 ultimately passed 135/135 and the carried live PS5 stepping/Run-to/cleanup gates. Rev29 completed and verified the remaining debugger/disassembler presentation integration so software breakpoint trap bytes never replace the original code shown to the user and debugger state is surfaced through neutral Markers. Rev30 added semantic Type/Mechanism classification plus validated Scan Results/Saved Addresses breakpoint/watchpoint shortcuts but was superseded before formal verification when live shutdown testing exposed stale hardware-watchpoint teardown state. Rev31 is the focused PS5 teardown-safety correction. Debugger export, final integration/cleanup stress coverage, and complete block finalization remain for rev32.

### Verified rev6 — Threads and Thread Control

- **[TK87ME — Implemented / verified] Shared Threads pane.** The modeless Debugger adds a neutral selectable Thread/Name/State list only when `ThreadEnumeration` is advertised, with explicit Refresh and selection preservation by neutral thread id.
- **[TK87ME — Implemented / verified] Separate thread-control capability/service.** Suspend/Resume appears only when `ThreadControl` is advertised and `IDebuggerThreadControlService` is present. Control is disabled unless the whole debugger target is Running.
- **[TK87ME — Implemented / verified] Deterministic Mock consumer.** Mock `1.0.0.rev9` exposes Main/Worker/Render thread rows and deterministic Suspended/Stopped/Running transitions for host regression.
- **[TK87ME / ps5debug-NG — Implemented; individual control externally blocked] Real PS5 consumer.** PS5 `0.1.0.rev26` maps `0xBDBB0005` thread list, `0xBDBB0011` thread info, `0xBDBB0006` suspend, and `0xBDBB0007` resume through the dedicated debugger command connection.
- **[TK87ME — Implemented / verified] Neutral state limitation.** Current ps5debug-NG list/info replies do not expose an individual scheduler/execution state, so PS5 rows inherit whole-target Running/Stopped state except ids successfully suspended by Memory Engine, which remain neutral `Suspended` until resumed or disappear from a later enumeration.
- **[TK87ME — Implemented / verified] Header cleanup.** Passive successful `Memory map: <n> region(s) loaded.` text is removed from the permanent row-2 surface. Actual memory-map enumeration and error presentation remain unchanged.
- **[TK87ME — Verified] Rev6 verification.** The Windows suite passed **101/101**. Mock thread enumeration/control, live PS5 enumeration, whole-target interaction, transport/cleanup, and final regression all passed. The tested ps5debug-NG backend rejected individual Suspend with on-wire `CMD_ERROR` `0xF0000001`; no crash or connection loss occurred. The external reproduction is documented in `docs/bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md`.

### Verified through rev8 — Registers and Stop Context

- **[TK87ME — Implemented / verified] Plugin API `2.13.0`.** `DebuggerRegisterValueEncoding` adds neutral byte/unsigned-little-endian/unsigned-big-endian interpretation metadata without introducing architecture-specific register types into the public surface.
- **[TK87ME — Implemented / verified] Capability-driven Registers pane.** The Debugger shows Register/Value/Group only when `RegisterAccess` is advertised and `IDebuggerRegisterService` is available. Snapshots exist only for the selected thread while Paused and are cleared on Continue, Detach, stale-target invalidation, or loss of valid stop context.
- **[TK87ME — Implemented / verified] Semantic stop context.** InstructionPointer, StackPointer, and FramePointer behavior uses `DebuggerRegisterRole`; the host never keys functionality from architecture names such as RIP/RSP/RBP.
- **[TK87ME — Implemented / verified] Safe writable-register workflow.** Writable backends use a shared fixed-width parser, Edit dialog, backend write, refresh, and exact read-back verification. Mock `1.0.0.rev10` supplied the deterministic rev8 acceptance path.
- **[TK87ME / ps5debug-NG — Implemented / verified] Read-only PS5 general registers.** PS5 `0.1.0.rev27` maps `CMD_DEBUG_GET_REGISTERS` (`0xBDBB0008`) and the 176-byte FreeBSD amd64 general-register block into neutral rows. Native offsets and LWP terminology remain plugin-private. SETREGS is intentionally not exposed because that backend path is not hardware-verified.
- **[TK87ME — Implemented / verified] Existing Disassembler integration.** The current semantic instruction-pointer address opens the normal Disassembler; no debugger-local decoder/viewer is added.
- **[TK87ME — Verified] Rev8 acceptance.** The corrected Windows gate passed **107/107**, and all **11/11** focused Mock/live-PS5 Registers and Stop Context steps passed, including selection/refresh stability, Mock writes, live PS5 general-register snapshots, stop-context refresh, Current Instruction -> Disassembler, read-only gating, and transport/cleanup regression.

### Rev9 — Extended Register State and pane splitter — partially accepted / superseded

- **[TK87ME — Implemented / automated verified] Existing neutral model reused.** Plugin API remained `2.13.0`; `DebuggerRegister` already represented arbitrary widths/groups without an architecture-specific API addition.
- **[TK87ME — Implemented / runtime verified] Proportional Threads/Registers layout.** The 50/50 startup split, 20/80 relative limits, resizing behavior, themes, and lifecycle behavior passed focused Gate B and Gate D.
- **[TK87ME — Implemented / runtime verified on Mock] Deterministic Mock extended state.** Mock `1.0.0.rev11` wide 80/128/256-bit presentation, selection/refresh stability, read-only gating, and the existing writable general-register path passed focused Gate C.
- **[TK87ME — Verified] Rev9 Windows gate.** All **108/108** checks passed.
- **[TK87ME / ps5debug-NG — Live gate failed] PS5 optional extended-register transport.** Attach, Pause, and thread enumeration succeeded on real hardware, but the Registers refresh remained pending at `Registers 0` / unavailable IP after the rev9 owner-stream sequence entered an optional extended-register read. The UI remained responsive and the target was paused. Rev9 is therefore not the accepted live-PS5 extended-register implementation.

### Rev10 — PS5 Extended Register Transport Guard — verified

- **[TK87ME / ps5debug-NG — Implemented / verified] Mandatory general state remains isolated from optional state.** Verified `GETREGS` stays on the debugger-owner command connection and is mapped before optional work.
- **[TK87ME / ps5debug-NG — Implemented / verified] Negotiated eligibility.** Automatic optional register probes require ps5debug-NG protocol `1.3` or newer and capability level `1.0` or newer from the connection metadata already collected during target identification. Missing/older metadata produces a general-only snapshot with no optional command traffic.
- **[TK87ME / ps5debug-NG — Implemented / verified] Disposable bounded probes.** Safe paused-state optional reads currently comprise GETFPREGS and GETFSGSBASE. Each uses a fresh short-lived command connection with a two-second linked timeout. A timeout/rejection/socket/probe-local framing failure can be discarded with that connection rather than leaving the debugger-owner stream mid-response.
- **[ps5debug-NG — External backend bug identified / workaround implemented] GETDBREGS after Pause.** Current upstream `debug_continue_handle` pause flow consumes the stop event, while `debug_getdbregs_handle` later relies on `is_process_stopped()` calling `wait4()` again. If that second status query reports no unread stop event, GETDBREGS sends another SIGSTOP and performs a blocking wait even though the process is already stopped. Because debug commands hold shared backend locks, this can stall the complete debugger control path. Rev10 therefore sends **no paused GETDBREGS** and omits DR0-DR3/DR6/DR7 from the live paused snapshot until the backend is corrected and hardware-verified. The external report is `docs/bug-reports/ps5debug-ng-getdbregs-can-hang-after-pause.md`.
- **[TK87ME / ps5debug-NG — Implemented / verified] Per-group degradation.** A failed safe optional command is cached unavailable for the current attachment and is not repeatedly retried. Healthy FPU/SIMD and FS/GS groups continue refreshing. A fully successful current PS5 snapshot is 76 rows: 26 general + 48 FPU/SIMD + 2 FS/GS.
- **[TK87ME / ps5debug-NG — Implemented / verified] Read-only boundary retained.** No PS5 SET-register transport is exposed.
- **[TK87ME — Verified] Rev10 acceptance.** The complete Windows registry passed **110/110**, and focused live-PS5 acceptance passed for the guarded 76-row snapshot path, repeated Refresh/Pause/Continue, Current Instruction -> Disassembler, Detach/Reattach, window-close cleanup, and disconnect/reconnect stale-session handling.

### Verified rev16 — Hardware Watchpoints

- **[TK87ME — Implemented / candidate] Shared manager reuse.** The existing `IDebuggerBreakpointService`, `IDebuggerBreakpointStateService`, `DebuggerBreakpointRequest`, and `DebuggerBreakpoint` contracts now drive both verified software execute breakpoints and capability-gated hardware data watchpoints. No platform slot or DR-register detail is introduced into Core/WPF.
- **[TK87ME — Implemented / current rev31 candidate] Plugin API `2.16.0`.** API `2.15.0` added optional `DebuggerEvent.TriggeredBreakpoint`; API `2.16.0` adds optional `IDebuggerBreakpointValidationService` so generic UI can preflight a complete neutral request without mutating target/debugger state. Older compatible 2.x plugins remain loadable and simply do not enable the new quick-address actions if they omit the optional service.
- **[TK87ME — Implemented / verified baseline; rev30 classification retained by current rev31 candidate] Combined Breakpoints / Watchpoints workspace.** Add selects Software Execute Breakpoint or Hardware Watchpoint according to advertised capabilities. Hardware requests expose neutral Access and Size inputs; plugins remain authoritative for supported access modes, widths, alignment, mapped-range rules, and slot limits. The table classifies each row by semantic Type (`Breakpoint`/`Watchpoint`) and separately exposes implementation Mechanism (`Software`/`Hardware`). Refresh/Enable/Disable/Remove/Remove All work across both kinds. Disassembler navigation remains Execute-only.
- **[TK87ME — Implemented in rev30 / retained by current rev31 candidate] Main-workspace debugger address actions.** Scan Results and Saved Addresses expose Add Breakpoint/Add Watchpoint only when an already-open Debugger is attached to the same plugin/process/connection generation and the plugin validator accepts the exact request. Add Breakpoint defaults to persistent Software/Execute; Add Watchpoint defaults to persistent Hardware/Write using the row value width. Invalid or unavailable actions are disabled rather than attempted.
- **[TK87ME — Implemented / verified] Deterministic Mock watchpoints.** Mock `1.0.0.rev14` advertises `Watchpoints`, supports Read, Write, and Read/Write data conditions with aligned 1/2/4/8-byte ranges, enforces four hardware slots separately from its 30 software-breakpoint slots, and emits deterministic persistent/temporary watchpoint events for host regression.
- **[TK87ME / ps5debug-NG — Implemented / verified] PS5 hardware data watchpoints.** PS5 `0.1.0.rev33` advertises `Watchpoints` and maps Write / ReadWrite requests to `CMD_DEBUG_SET_WATCHPOINT` (`0xBDBB0004`) with four backend slots. Native DR7 access/length encodings, per-thread backend programming, and slot ownership remain plugin-private. Read-only is rejected because amd64 DR7 has no distinct read-only data-watchpoint encoding; callers must explicitly choose Read/Write when reads must be observed.
- **[TK87ME / ps5debug-NG — Implemented / candidate] Safe address validation.** PS5 requires an aligned 1/2/4/8-byte range that remains inside one current mapped non-guarded memory region before any watchpoint command is sent. Rejected requests do not allocate or mutate a backend slot.
- **[TK87ME / ps5debug-NG — Implemented / verified] Event attribution without GETDBREGS.** The PS5 event translator reads DR6 only from the already-delivered 1184-byte interrupt packet; the rev10 paused `GETDBREGS` suppression remains unchanged. When B0-B3 bits are present they map exact active slots. If the current backend strips DR6 and exactly one hardware watchpoint is active, the event is attributed through an explicit single-watchpoint fallback. With multiple active watchpoints and no DR6 bits, the stop remains an unattributed signal event and temporary records are preserved rather than guessing.
- **[ps5debug-NG — External source defect documented] Packet-side DR6 clearing.** Current upstream `dispatch_debug_events()` clears `pkt_dr[6]` before later preserving `_saved_dr6`, which can remove B0-B3 trigger-slot status from the client interrupt. See `docs/bug-reports/ps5debug-ng-watchpoint-interrupt-clears-dr6-trigger-status.md`.
- **[TK87ME — Verified] Rev16 acceptance.** The automated registry expanded from 114 to **118** unique checks for triggered breakpoint context, deterministic Mock hardware-watchpoint lifecycle, host manager/source contracts, and PS5 protocol/hit mapping. The clean Windows gate passed **118/118**, and the complete focused Mock/live-PS5 acceptance passed across Write/ReadWrite watchpoints, 1/2/4/8-byte widths, all four hardware slots and reuse, persistent/temporary lifecycle, manager actions, cleanup/reconnect scenarios, software-breakpoint coexistence, and Threads/Registers/transport regression. The upstream DR6 limitation remained safely contained by the conservative attribution rules.

## Required debugger foundation

- **[CE + ME360 + TK87ME — Implemented / verified] User-facing debugger session lifecycle built on the verified rev1 neutral foundation.**
- **[CE + ME360 — Implemented / verified on Mock and PS5] Run / Pause / Continue through the shared coordinator.**
- **[CE + ME360 + TK87ME — Implemented / verified] Thread list.** Rev6 adds capability-driven enumeration/selection/refresh through `IDebuggerThreadService` in the shared workspace, deterministic Mock, and real PS5 backend.
- **[ME360 + TK87ME — Implemented; PS5 backend blocked] Suspend/resume individual thread.** Rev6 uses the separate `IDebuggerThreadControlService`, permits control only while the whole target is Running, and keeps backend thread handles/protocol ids plugin-private.
- **[CE + ME360 + TK87ME — Implemented / verified through rev10] Register viewer.**
- **[CE + ME360 — Implemented / verified where supported] Edit registers where supported.**
- **[CE + ME360 — Implemented / current candidate] Call stack / call frame view.**
- **[ME360 + TK87ME — Implemented / verified] Debug event viewer/log.**
- **[TK87ME — Implemented / verified] Debugger actions integrate with the existing Disassembler rather than implementing a duplicate current-instruction viewer; broader Memory Viewer integration remains later.**

### Rev17 — Call Stack, Call Frames and Stepping

- **[CE + ME360 + TK87ME — Implemented / candidate] Capability-driven Call Stack / Call Frames.** The Debugger exposes neutral frames only while the attached session is Paused. Frames are tied to the selected debugger thread, target identity, and connection generation, and are discarded on resume, detach, disconnect, or stale-target transitions.
- **[TK87ME — Implemented / candidate] Compact Call Stack workspace.** Breakpoints/Watchpoints and Call Stack share the upper-right debugger surface and are selected through the accepted compact selector buttons. Threads and Registers remain visible on the left, Events remains visible below, and the left/right workspace uses the existing proportional splitter behavior instead of adding another permanently visible panel.
- **[TK87ME — Implemented / candidate] Frame navigation.** A selected frame can open its instruction address in the existing Disassembler or Memory Viewer. The Call Stack grid keeps the primary columns compact (`#`, Instruction Address, Module, Symbol) and presents SP, FP, and Return in the selected-frame detail area.
- **[CE + TK87ME — Implemented / candidate] Step Into.** The host calls the neutral `IDebuggerStepService` for the selected debugger thread. Mock provides deterministic native Step Into behavior; PS5 maps it to ps5debug-NG's native thread-step command on the dedicated debugger transport.
- **[CE + TK87ME — Implemented / live-verified through rev28] Step Over.** The host reuses neutral Disassembler output to determine whether the current instruction is a call. Ordinary paused contexts resolve the live instruction as before. For a matching logical Software/Execute breakpoint stop, rev26 first uses the original instruction captured before the breakpoint was installed so a rearmed backend `INT3` cannot hide the original `Call` flow-control classification or length. Non-call instructions use native Step Into. Calls are stepped over by running to the original instruction's fall-through address through the verified temporary breakpoint manager.
- **[CE + TK87ME — Implemented / live-verified through rev28] Step Out.** The host uses the selected call frame's return address and the same run-to/breakpoint composition path.
- **[CE + TK87ME — Implemented / live-verified rev28] Run to Address.** A themed address dialog accepts a hexadecimal target. Existing enabled execute breakpoints are reused; otherwise the host creates a temporary software execute breakpoint and continues. An existing disabled breakpoint is not silently changed.
- **[TK87ME — Implemented / candidate] Deterministic Mock call-stack/step backend.** Mock `1.0.0.rev15` exposes a three-frame paused call stack and deterministic Step Into completion so host lifetime, navigation, event ordering, and composed stepping can be tested without hardware.
- **[TK87ME / ps5debug-NG — Implemented / candidate] PS5 server-side stack walk.** PS5 `0.1.0.rev34` maps `CMD_PROC_READ_STACK` (`0xBDAA0023`) into neutral frames while keeping RBP-chain walking, variable payload layout, locals/code blocks, x86-64 registers, and memory-map resolution plugin-private.
- **[TK87ME / ps5debug-NG — Implemented / candidate] PS5 native thread Step Into.** The plugin maps selected-thread stepping to `CMD_DEBUG_STEP_THREAD` (`0xBDBB0013`) and recognizes the matching asynchronous trap as a neutral `StepCompleted` event after managed breakpoint/watchpoint hit attribution has had priority.
- **[TK87ME — Superseded after automated gate] Rev17 gate.** The clean Windows automated registry passed **124/124**. Before the focused runtime gates began, UI inspection exposed an unthemed system-default white `TabControl`/`TabItem` surface in the new upper-right Debugger workspace under Dimmed theme. The functional Call Stack/stepping implementation remains the basis for rev18, but rev17 is not accepted as a runtime-verified revision.
- **[TK87ME — Superseded before complete acceptance] Rev18 tab-theme correction.** The system-default white tab surface was replaced with application-owned theme-aware templates. Runtime inspection of the corrected surface exposed a clipped right-hand header border before the carried-forward debugger gates were completed.
- **[TK87ME — Superseded after automated gate] Rev19 tab/lifecycle correction.** Rev19 passed **128/128** automated checks and established independent modeless tool-window activation, but runtime showed that the selected/last workspace-tab right edge was still clipped and MainWindow could re-enter `Close()` while its original WPF close request was active.
- **[TK87ME — Superseded after rev21 runtime gate] Rev20 overlay-based tab edge.** Rev20 introduced a dedicated `TabRightEdge`, but rev21 runtime proved that placing it at the right-aligned boundary of an unconstrained template grid still did not produce the visible final edge on Windows.
- **[TK87ME — Superseded after runtime UI failure] Rev22 reserved-column tab edge.** Windows runtime inspection showed that the dedicated column/edge structure hid both tab labels and rendered the selected header as a solid accent block. The approach is rejected and must not be reintroduced.
- **[TK87ME — Superseded after runtime UI failure] Rev23 single-border tab header correction.** The shared TabItem returned to one themed border/content path and passed 130/130 automated checks, but Windows runtime inspection still showed the right edge clipped.
- **[TK87ME — Implemented / runtime-accepted] Rev24 Debugger mode switcher.** The upper-right Debugger no longer uses TabControl/TabItem. Compact application-styled Breakpoints / Watchpoints and Call Stack buttons select exactly one workspace, the selected button uses the theme accent outline, duplicate inner title/count rows are removed, former title actions move to the shared footer rows, and the default Debugger size is `1240 x 780`.
- **[TK87ME — Implemented / current candidate] Independent modeless tool-window activation.** Debugger, Disassembler, and Memory Viewer retain centered startup placement but release WPF ownership after modeless Show, so MainWindow can be activated above them and tools can subsequently be activated above MainWindow without `Topmost`.
- **[TK87ME — Implemented / runtime-confirmed in rev21] Coordinated application shutdown cleanup.** MainWindow tracks every modeless tool it opens. The first MainWindow close request is held while `IAsyncDisposable` cleanup such as debugger detach/session release and existing `IDisposable` cleanup for viewer/disassembler workspaces run. Tracked tools close normally, then MainWindow posts its final `Close()` through the dispatcher so the original `OnClosing` callback has unwound before shutdown completes. Modal dialogs remain owned and modal.
- **[TK87ME — Implemented / automated and runtime-confirmed for tested shutdown path] Rev21 shutdown compile correction.** The dispatcher-posted final close is intentionally fire-and-forget and explicitly discards the awaitable `DispatcherOperation`, satisfying the project-wide warnings-as-errors build policy without changing the shutdown ordering. Rev21 passed 130/130 and the subsequent runtime check confirmed that MainWindow closes the open tool windows without the previous re-entry exception.

## Breakpoints and stepping

- **[CE + TK87ME — Implemented / verified through rev15] Breakpoint manager.**
- **[CE + TK87ME — Implemented / verified through rev15] Software execute breakpoints, including persistent and temporary records.**
- **[CE — Planned] Hardware execute breakpoints where available.**
- **[CE + TK87ME — Implemented / verified rev16] Memory data watchpoints where available. Mock supports Read/Write/Read-Write; PS5 supports Write/Read-Write through four hardware debug-register slots.**
- **[CE + TK87ME — Implemented / live-verified through rev25] Step Into.**
- **[CE + TK87ME — Implemented / live-verified through rev28] Step Over.**
- **[CE + TK87ME — Implemented / live-verified through rev28] Step Out.**
- **[CE + TK87ME — Implemented / live-verified rev28] Run to address/cursor.**
- **[TK87ME — Implemented / verified through rev29] Current instruction can be opened in the normal Disassembler workspace from the semantic instruction-pointer stop context.** Rev29 also carries matching debugger markers and original Software/Execute bytes into that normal Disassembler read path.
- **[TK87ME — Planned] Register-click navigation into Memory Viewer or Disassembler.**
- **[TK87ME — Planned] Universal Export for threads, registers, call frames, breakpoints, and debug events.**

## Session safety

- All debugger events must be associated with plugin/process/connection generation.
- A stale debugger window must never handle an event as if it belonged to a newly selected process.
- Debugger-owned transports must be shut down cleanly on disconnect and target invalidation.

## Active debugger development order

The `0.1.7` feature block should proceed in dependency order so later workflows are composed from verified lower layers rather than introducing temporary platform-specific shortcuts:

1. **rev1 — Debugger Contracts and Core Foundation — verified:** neutral SDK models/services, identity/event binding, shared lifecycle coordinator; Windows verification passed **84/84**.
2. **rev2 — Debugger Workspace and Mock Backend — verified:** generic modeless workspace, host-owned target lifetime, deterministic Mock attach/events/run/pause/continue; Windows verification passed **89/89** and focused Mock runtime/UI acceptance passed.
3. **rev3 — PS5 Debug Transport and Attach/Detach — superseded before verification:** dedicated PS5 debugger command/event transport, real attach/detach/pause/continue, and async interrupt translation were implemented, but the accompanying first target-header layout was corrected before the Windows/live gate.
4. **rev4 — Two-Row Target Header and Button Alignment — superseded before verification:** retained the rev3 PS5 debugger implementation, established the permanent two-row top-target layout, and corrected shared button text centering; its first Windows UI review exposed fixed-width overflow when the window was narrowed.
5. **rev5 — Responsive Target Header Input Sizing — verified:** retained the rev3 debugger backend plus rev4 row/button work, established responsive 180-max/no-minimum ordinary row-1 inputs, passed **97/97**, passed focused UI/Mock acceptance, and passed the complete safe live-PS5 debugger/regression gate; natural async-stop mapping remains deferred because no safe trigger was available.
6. **rev6 — Threads and Thread Control — verified:** capability-driven thread pane, enumeration/selection/refresh, deterministic Mock control, real PS5 enumeration, and header cleanup; **101/101** plus focused acceptance passed. Individual PS5 Suspend/Resume remains implemented but externally blocked by the tested ps5debug-NG backend returning `CMD_ERROR`.
7. **rev7 — Registers and Stop Context — superseded before runtime acceptance:** the register implementation was completed, but the packaged Windows gate repeatedly produced **106/107** because an existing source-contract test falsely required explicit `new DebuggerViewModel(...)` syntax after the production code moved to target-typed `new(...)`.
8. **rev8 — Debugger Workspace Source Contract Fix — verified:** preserved the rev7 production register implementation, corrected the false-negative constructor source contract, passed **107/107**, and passed all **11/11** focused Registers and Stop Context Mock/live-PS5 acceptance steps.
9. **rev9 — Extended Register State and Debugger Pane Splitter — superseded after live Gate E:** **108/108** and focused Mock Gates B-D passed; live PS5 Attach/Pause/thread enumeration succeeded but the optional extended-register refresh could wait indefinitely, so the PS5 extended transport was not accepted.
10. **rev10 — PS5 Extended Register Transport Guard — verified:** preserves the rev9 splitter/neutral mappings, keeps mandatory GETREGS on the verified owner stream, moves safe GETFPREGS/GETFSGSBASE reads to bounded disposable probe connections with per-session unavailable-group caching, suppresses paused GETDBREGS, and passed **110/110** plus focused live-PS5 acceptance.
11. **rev11 — Breakpoint Manager and Software Breakpoints — superseded before verification:** introduced the capability-driven manager, persistent/temporary software execute breakpoints, Mock deterministic hits, real PS5 `0xBDBB0003` integration, and safe staged disable/remove while Paused, but the first Windows Gate A build stopped on two PS5 `CS8600` nullable diagnostics before the 114-check suite could run.
12. **rev12 — Breakpoint Manager Compile Fix — superseded before verification:** fixed the rev11 PS5 nullable lookups and allowed the application itself to launch, but the packaged verification project then failed to compile on an undefined `AssertContains` helper before the **114-check** suite could execute.
13. **rev13 — Breakpoint Test Build and Layout Fix — superseded after automated Gate A:** repaired the test-project compile blocker and established the requested 65/35 Breakpoints/Events starting split. The full suite then ran and reported **113/114** because the PS5 metadata expectation omitted the already-implemented `Breakpoints` capability.
14. **rev14 — PS5 Breakpoint Capability Verification Fix — superseded after 114/114 and runtime acceptance:** corrected the final stale PS5 capability-set expectation. The full Windows suite passed and the complete Mock/live-PS5 breakpoint workflow was exercised, but two runtime defects were found and moved into rev15.
15. **rev15 — Breakpoint Runtime Validation Fixes — verified:** Windows passed **114/114** and the focused runtime fixes/regression gates passed. Software/Execute address validation, immediate breakpoint re-hit register refresh, and the requested Disassembler cleanup are accepted.
16. **rev16 — Hardware Watchpoints — verified:** reused the neutral breakpoint manager for hardware data watchpoints, added optional triggered-breakpoint event context, provided deterministic Mock Read/Write/Read-Write coverage, mapped PS5 Write/Read-Write conditions to four `0xBDBB0004` DR slots, preserved honest attribution when the backend omits DR6 trigger bits, and passed **118/118** plus complete focused Mock/live-PS5 acceptance.
17. **rev17 — Call Stack, Call Frames and Stepping — superseded after automated gate:** implementation complete and **124/124 PASS**, but the first runtime/UI inspection exposed the unthemed default WPF tab surface before focused runtime acceptance.
18. **rev18 — Debugger Tab Theme Fix — superseded before complete acceptance:** replaced the system-default white tab surface with shared theme-aware TabControl/TabItem templates; subsequent runtime inspection exposed the clipped right edge and modeless-window lifecycle requirement before the carried-forward gates completed.
19. **rev19 — Debugger UI and Window Lifecycle Fixes — superseded after automated gate:** passed **128/128**. Runtime confirmed that the selected/last workspace-tab right edge still clipped and MainWindow shutdown could throw WPF `InvalidOperationException` from close re-entry while Debugger was open.
20. **rev20 — Debugger Tab Border and Shutdown Re-entry Fixes — superseded before automated/runtime acceptance:** carried the explicit in-bounds tab edge and dispatcher-posted final MainWindow close, but the first Windows build failed with `CS4014` because the returned awaitable `DispatcherOperation` was not explicitly acknowledged under warnings-as-errors.
21. **rev21 — MainWindow Shutdown Compile Fix — superseded after automated/runtime host gate:** passed **130/130** and runtime confirmed that closing MainWindow closes the open tool windows correctly, but the carried tab-right-edge correction still failed visually.
22. **rev22 — Debugger Tab Right Edge Layout Fix — superseded after runtime UI failure:** the reserved-column approach hid both tab labels and produced a solid accent-selected header, so it was rejected.
23. **rev23 — Debugger Tab Header Rendering Fix — superseded after automated/runtime UI gate:** passed **130/130**, but runtime still showed the workspace-tab right edge clipped.
24. **rev24 — Debugger Mode Switcher Layout Refresh — superseded after partial live-PS5 acceptance:** the selector-button workspace, Debugger sizing, splitters, tool cleanup, and complete Mock Call Stack/stepping runtime cycle passed. The final Windows run was **129/130** only because one stale source contract still expected the removed TabItem attribute. Live PS5 Call Stack/thread/frame navigation/native Step Into passed before software-breakpoint event-vs-live-register divergence blocked composed-step/Run-to acceptance.
25. **rev25 — PS5 Breakpoint Stop Context and Status Alignment Fix — superseded after Gate E Step Over failure:** Windows passed **132/132**; MainWindow alignment, focused Mock regression, and the live PS5 logical software-breakpoint stop-context/Step-Into gate passed. Step Over from the persistent `0xE9F74E` call then decoded the backend-rearmed `INT3`, selected native Step Into, and finished at callee `0x1A3BC30` instead of fall-through `0xE9F753`.
26. **rev26 — Breakpoint-Aware Step Over Fix — superseded after partial live acceptance:** Windows passed **133/133**. Live Step Over at `0xE9F74E` correctly finished at `0xE9F753` and cleaned its temporary breakpoint; clean Step Out reached its selected return address. Run to Address was then interrupted by a signal-10 stop before its target, leaving the operation-owned temporary breakpoint active, and detach from that interrupted state was followed by the game terminating.
27. **rev27 — Interrupted Operation and Safe Detach Breakpoint Cleanup — superseded after automated gate:** introduced exact composed-operation breakpoint ownership/interruption cleanup and PS5 `0.1.0.rev36` defensive software-breakpoint restoration before detach/disposal. The Windows run completed **134/135**; the only failure was a stale older source assertion that still required the pre-rev27 Continue-only deferred-stop source shape. No live rev27 gate was started.
28. **rev28 — Debugger Deferred Stop Verification Contract Fix — verified:** aligned the stale breakpoint-manager assertion with rev27 production behavior. The Windows suite passed **135/135**, and focused live PS5 verification accepted breakpoint-aware Step Over, Step Out, successful/interrupted Run to Address, temporary-breakpoint retirement, safe staged-cleanup Detach without target crash, and clean Detach/Reattach state.
29. **rev29 — Logical Disassembly and Debugger Markers — verified:** Windows passed **138/138**. Focused live PS5 acceptance confirmed original bytes/instructions remain authoritative under an active Software breakpoint, `Watchpoint hit` is metadata-only, and Breakpoint plus Watchpoint-hit markers can coexist in one range without changing logical code.
30. **rev30 — Debugger Address Actions and Breakpoint Classification — superseded before verification:** split Breakpoint/Watchpoint Type from Software/Hardware Mechanism, added validated Add Breakpoint/Add Watchpoint shortcuts to Scan Results and Saved Addresses, advanced Plugin API to `2.16.0`, Mock to `1.0.0.rev16`, PS5 to `0.1.0.rev37`, and documented the external ps5debug-NG overlapping breakpoint/watchpoint event-consumption behavior. Its planned **141/141** gate was not completed because live shutdown testing exposed stale hardware-watchpoint teardown state.
31. **rev31 — Safe PS5 Watchpoint Detach Cleanup — current candidate:** advance PS5 to `0.1.0.rev38`; explicitly disable every active or staged client-owned hardware-watchpoint slot before detach/disposal; document the external ps5debug-NG teardown defect; add focused disposal cleanup coverage. Registry target: **142/142**.
32. **rev32 — Integration, Export and Finalization:** universal debugger list export, final integration checks, cleanup/stale-session stress coverage, complete Mock regression, final live-PS5 regression, documentation cleanup, and completion of the `0.1.7` Debugger block.

`Find What Writes / Reads / Accesses` remains the next feature block after the debugger because it depends on the debugger's verified watchpoint/event/register/thread infrastructure. `Break and Trace` follows after that because it additionally depends on robust stepping and event sequencing.

---

# 10. Find What Writes / Reads / Accesses

## Objective

Implement the CE-style code-finder workflow that connects value discovery to instruction discovery and eventually to cheat creation.

## Required functionality

- **[CE + TK87ME — Planned] Find What Writes This Address.**
- **[CE + TK87ME — Planned] Find What Accesses This Address.**
- **[CE — Planned] Find What Reads This Address** where backend can distinguish reads from general access.
- **[CE — Planned] Find What Addresses This Instruction Writes To.**
- **[CE — Planned] Find What Addresses This Instruction Reads From.**
- **[TK87ME — Planned] Hit list.**
- **[TK87ME — Planned] Hit count per instruction.**
- **[TK87ME — Planned] Duplicate instruction grouping.**
- **[TK87ME — Planned] Register snapshot per hit.**
- **[TK87ME — Planned] Optional stack snapshot per hit.**
- **[TK87ME — Planned] Timestamp per hit.**
- **[TK87ME — Planned] Open hit in Disassembler.**
- **[TK87ME — Planned] Open referenced address in Memory Viewer.**
- **[TK87ME — Planned] Add discovered instruction/address to Cheat Project.**
- **[TK87ME — Planned] Universal Export for hit data.**

## Intended workflow

The final program should support the full chain:

```text
Scan value
  -> Save/inspect address
  -> Find What Writes/Accesses
  -> Inspect responsible instruction
  -> Analyze registers/stack
  -> Patch, inject, pointer-resolve, or create cheat
  -> Save into Cheat Project
  -> Test
  -> Export to target-specific format
```

---

# 11. Break and Trace / Execution Tracing

## Objective

Provide a CE-style instruction trace that can be used for reverse engineering without requiring manual repeated stepping.

## Required functionality

- **[CE — Planned] Break and Trace.**
- **[CE — Planned] Maximum trace count.**
- **[CE — Planned] Optional stop condition.**
- **[CE — Planned] Step-over option during trace.**
- **[CE — Planned] Optional address dereferencing.**
- **[CE — Planned] Register state per trace step.**
- **[CE — Planned] Optional stack snapshot.**
- **[TK87ME — Planned] Click trace row to open the same instruction in Disassembler.**
- **[TK87ME — Planned] Universal Export of trace results.**
- **[TK87ME — Planned] Compare two traces.**
- **[Platform-specific — Future / optional] Hardware-assisted execution tracing** such as Intel PT where a PC backend can support it reliably.

---

# 12. Pointer Chains and Pointer Scanner

## Objective

Provide both manual pointer-chain addresses and scalable pointer discovery against live targets and offline memory data.

## Manual pointer support

- **[CE + ME360 + TK87ME — Planned] Base address plus arbitrary offset chain.**
- **[TK87ME — Planned] Pointer width and endianness derived from TargetArchitecture.**
- **[TK87ME — Planned] Live resolved final address/value preview.**
- **[TK87ME — Planned] Pointer Saved Addresses remain compatible with freeze/write/Memory Viewer/Disassembler actions.**

## Pointer Scanner

- **[CE + ME360 + TK87ME — Planned] Reverse pointer scan to a target address.**
- **[CE + ME360 — Planned] Maximum depth.**
- **[CE + ME360 — Planned] Minimum and maximum offset.**
- **[ME360 — Planned] Addressable base and length.**
- **[ME360 — Planned] Pointer alignment.**
- **[TK87ME — Planned] Module/static-range constraints.**
- **[CE — Planned] Pointer maps.**
- **[CE — Planned] Rescan/filter an existing pointer result set.**
- **[TK87ME — Planned] Compare pointer results between target sessions.**
- **[TK87ME — Planned] Compare pointer results between memory dumps/snapshots.**
- **[ME360 + TK87ME — Planned] Offline pointer scan against memory file/dump.**
- **[TK87ME — Planned] Disk-backed huge pointer result sets.**
- **[TK87ME — Planned] Universal Export for pointer results.**
- **[TK87ME — Planned] Evaluate pointer chain live from result.**
- **[TK87ME — Planned] Convert pointer result directly to Saved Address.**
- **[CE — Future / optional] Parallel/distributed pointer scanning.**

---

# 13. Structure Dissect / Data Structure Viewer

## Objective

Add a CE-style Structure Dissect workspace for understanding objects, classes, entities, and repeated memory layouts.

## Required functionality

- **[CE — Planned] Dedicated Structure Viewer/Dissect workspace.**
- **[CE — Planned] Base address and configurable structure size.**
- **[CE — Planned] Automatic primitive-type suggestions.**
- **[CE — Planned] Manual field definitions.**
- **[CE — Planned] Field description/name.**
- **[CE — Planned] Nested structures.**
- **[CE — Planned] Pointer fields.**
- **[CE — Planned] Arrays.**
- **[CE — Planned] String fields.**
- **[CE — Planned] Enums and bit fields.**
- **[CE — Future / optional] Vector/matrix field helpers.**
- **[CE — Planned] Compare multiple base addresses side-by-side.**
- **[CE — Planned] Highlight equal/different fields across objects.**
- **[TK87ME — Planned] Open field in Memory Viewer.**
- **[TK87ME — Planned] Open pointer/function field in Disassembler.**
- **[TK87ME — Planned] Add field to Saved Addresses.**
- **[TK87ME — Planned] Export structure definitions and current data.**
- **[TK87ME — Planned] Persist structure definitions inside Cheat Project.**

---

# 14. Memory Snapshots and Comparison

## Objective

Support persistent memory-state comparison outside the immediate First Scan / Next Scan lifecycle.

## Required functionality

- **[CE — Planned] Capture memory snapshot.**
- **[CE — Planned] Save/load snapshots.**
- **[CE — Planned] Compare two or more snapshots.**
- **[CE + TK87ME — Planned] Scan against a saved snapshot/baseline.**
- **[CE — Planned] Use snapshots in Structure Dissect comparison.**
- **[TK87ME — Planned] Snapshot metadata including plugin, target/game/process, module/range, timestamp, architecture, and source type.**
- **[TK87ME — Planned] Snapshot from live target or offline dump.**
- **[TK87ME — Planned] Disk-backed/streaming storage for large snapshots.**
- **[TK87ME — Planned] Universal Export for snapshot differences.**

---

# 15. Modules, Sections, Symbols, Labels, and Cross References

## Objective

Make addresses understandable and reusable beyond raw hexadecimal numbers.

## Required functionality

- **[CE + TK87ME — Implemented/partial] Module information where backend provides it.**
- **[TK87ME — Implemented] Module-relative origin in Disassembler.**
- **[CE + TK87ME — Planned] Module section enumeration.**
- **[CE — Planned] Symbol resolver.**
- **[CE — Planned] User-defined symbols/labels.**
- **[CE — Planned] Navigate by symbol or `module+offset`.**
- **[TK87ME — Planned] Shared neutral symbol model available to Disassembler, Debugger, Pointer Scanner, Saved Addresses, scripts, and projects.**
- **[Platform-specific — Planned] Load backend/platform-provided symbols when available.**
- **[CE / PC-specific — Future / optional] .NET/Mono metadata and symbols.**
- **[TK87ME — Planned] Symbol import/export.**
- **[CE — Planned] Cross-reference lists for code and data references.**

---

# 16. Task Sequencer / Memory Automation

## Objective

Adopt and expand the useful ME360 Task Sequencer concept into a general automation layer above ordinary Saved Address freezing.

## Required operations

- **[ME360 — Planned] Set Memory.**
- **[ME360 — Planned] Delay.**
- **[ME360 — Planned] Memory comparison condition.**
- **[ME360 — Planned] Label.**
- **[ME360 — Planned] Jump to Label.**
- **[ME360 — Planned] Stop Sequence.**
- **[ME360 — Planned] Freeze operation.**
- **[ME360 — Planned] Randomize operation.**
- **[TK87ME — Planned] Conditional branches.**
- **[TK87ME — Planned] Loops / repeat count.**
- **[TK87ME — Planned] Wait until memory condition.**
- **[TK87ME — Planned] Enable/disable Cheat Project entry.**
- **[TK87ME — Planned] Run scan or refinement.**
- **[TK87ME — Planned] Invoke script.**
- **[TK87ME — Planned] Run another sequence.**
- **[TK87ME — Planned] Hotkey-triggered sequence.**
- **[TK87ME — Future / optional] Periodic/scheduled sequence.**

## Persistence and safety

- **[ME360 + TK87ME — Planned] Save/load sequences.**
- **[TK87ME — Planned] Store sequences inside Cheat Project.**
- **[TK87ME — Planned] Capability validation before execution.**
- **[TK87ME — Planned] Session/target binding for every target-dependent operation.**
- **[TK87ME — Planned] Explicit stop/cancel and clear operation-state UI.**

---

# 17. Scripting

## Objective

Provide a powerful automation and extension environment only after the core APIs are stable enough to expose safely.

Both Cheat Engine and MemoryEngine360 demonstrate the usefulness of Lua. Lua should therefore be the primary language candidate, but the final decision should remain tied to API stability and maintainability.

## Required scripting capabilities

- **[CE + ME360 — Planned] Integrated scripting workspace.**
- **[CE + ME360 — Planned] Multiple script documents/tabs.**
- **[CE + ME360 — Planned] Run, stop, reload.**
- **[ME360 — Planned] Clear compile/runtime error presentation.**
- **[TK87ME — Planned] Neutral memory read/write API.**
- **[TK87ME — Planned] Scanner API.**
- **[TK87ME — Planned] Saved Addresses API.**
- **[TK87ME — Planned] Memory Viewer/Disassembler API.**
- **[TK87ME — Planned] Debugger API.**
- **[TK87ME — Planned] Pointer Scanner API.**
- **[TK87ME — Planned] Cheat Project API.**
- **[TK87ME — Planned] Task Sequencer API.**
- **[TK87ME — Planned] Universal Export API.**
- **[ME360 + Platform-specific — Planned] Plugin-defined scripting extensions.**
- **[ME360 / Xbox-specific — Planned] JRPC-oriented helpers in Xbox plugin.**
- **[TK87ME / PS5-specific — Planned] PS5 script helpers exposed by PS5 plugin without leaking ps5debug specifics into Core.**

## Security/reliability requirements

- Scripts must run through documented APIs rather than bypassing target/session safety.
- Errors must never crash the host.
- Potentially destructive operations should remain explicit and traceable.

---

# 18. Mod Tools / Trainer Builder

## Objective

Allow projects to be exposed as simple user-facing trainers or mod-control interfaces without requiring the operator to use the full scanner/debugger UI.

## Required functionality

- **[CE + ME360 — Planned] Mod/Trainer UI workspace.**
- **[CE — Planned] Buttons and toggles bound to cheats.**
- **[CE — Planned] Value editors bound to Saved Addresses/project variables.**
- **[CE — Planned] Hotkey configuration/display.**
- **[ME360 — Planned] Tabbed mod-tool layouts.**
- **[TK87ME — Planned] Bind controls to neutral Cheat Project operations rather than platform-specific code.**
- **[TK87ME — Planned] Theme-aware controls.**
- **[TK87ME — Planned] Script-driven custom behavior.**
- **[TK87ME — Future / optional] Standalone lightweight trainer package** where backend licensing/deployment makes it practical.
- **[TK87ME — Future / optional] Platform-focused trainer mode**, for example a PS5 trainer that uses the PS5 plugin while hiding development-oriented panels.

---

# 19. Cheat Project System

## Objective

Create a project-centric source-of-truth format that can describe cheats independently of the final target/export format.

This is one of the most important TK87ME-specific subsystems and should not be replaced by CE `.CT`, PS5 JSON, MC4, SHN, SHNEXT, Xbox trainer files, or any other external format.

## Required project content

- **[TK87ME — Planned] Project metadata.**
- **[TK87ME — Planned] Game/application metadata.**
- **[TK87ME — Planned] Target/platform metadata.**
- **[CE + TK87ME — Planned] Cheat groups/folders.**
- **[CE + TK87ME — Planned] Cheat entries.**
- **[TK87ME — Planned] Description and detailed notes.**
- **[TK87ME — Planned] Ordered cheat operations.**
- **[TK87ME — Planned] Enable/disable state.**
- **[TK87ME — Planned] Dependencies between entries.**
- **[TK87ME — Planned] Undo/Redo.**
- **[TK87ME — Planned] Copy/Paste.**
- **[TK87ME — Planned] Save/Open.**
- **[TK87ME — Planned] Autosave/recovery.**

## Integration with existing/future tools

- **[TK87ME — Planned] Saved Addresses -> project.**
- **[TK87ME — Planned] Disassembler patch -> project.**
- **[TK87ME — Planned] Find What Writes result -> project.**
- **[TK87ME — Planned] Pointer chain -> project.**
- **[TK87ME — Planned] Structure definition -> project.**
- **[TK87ME — Planned] Symbols/labels -> project.**
- **[TK87ME — Planned] Scripts -> project.**
- **[TK87ME — Planned] Task sequences -> project.**
- **[TK87ME — Planned] Trainer/mod UI -> project.**

## Validation

- **[TK87ME — Planned] Validate project against current target/plugin capabilities.**
- **[TK87ME — Planned] Validate before live test.**
- **[TK87ME — Planned] Validate before export.**
- **[TK87ME — Planned] Preserve unsupported operations instead of silently dropping them.**

---

# 20. Cheat Operations

## Objective

Represent cheat behavior using neutral Core operations while allowing plugins to provide platform-specific operation types where necessary.

## Core/general operations

- **[TK87ME — Planned] Memory Write.**
- **[CE + TK87ME — Planned] Freeze Value.**
- **[CE + TK87ME — Planned] Patch Bytes.**
- **[CE + TK87ME — Planned] Restore Bytes.**
- **[CE + TK87ME — Planned] NOP Instruction.**
- **[CE + TK87ME — Planned] Pointer Write.**
- **[TK87ME — Planned] Conditional Write.**
- **[TK87ME — Planned] Multi-step operation.**
- **[TK87ME — Planned] Dependency/master operation.**

## Platform-specific operations

### PlayStation 5

- **[Platform-specific / TK87ME — Planned] x86-64 instruction patches.**
- **[Platform-specific / TK87ME — Planned] PS5 code-cave operations.**
- **[Platform-specific / TK87ME — Planned] Module/process-relative operations.**
- **[Platform-specific / TK87ME — Planned] Master/dependency constructs required by output formats.**

### PC

- **[CE / Platform-specific — Planned] AOB injection.**
- **[CE / Platform-specific — Planned] x86/x64 code injection.**
- **[CE / Platform-specific — Future / optional] Module/DLL-related operations.**

### Xbox 360

- **[ME360 / Platform-specific — Planned] PowerPC instruction patches.**
- **[ME360 / Platform-specific — Planned] PowerPC branch patches.**
- **[ME360 / Platform-specific — Planned] JRPC/XBDM-related operations where useful.**

---

# 21. Cheat Validation, Import, Export, and Interoperability

## Objective

Use the neutral project as source of truth and let plugins/exporters transform it into platform-specific formats only after validation.

## Validation

- **[TK87ME — Planned] Required metadata validation.**
- **[TK87ME — Planned] Address/range validation.**
- **[TK87ME — Planned] Architecture/instruction validation.**
- **[TK87ME — Planned] Dependency validation.**
- **[TK87ME — Planned] Unsupported-operation detection.**
- **[TK87ME — Planned] Output-format-specific restriction validation.**
- **[TK87ME — Planned] Live test against connected target before export.**

## PS5 export targets

- **[TK87ME — Planned] JSON.**
- **[TK87ME — Planned] MC4.**
- **[TK87ME — Planned] SHN.**
- **[TK87ME — Planned] SHNEXT.**
- **[TK87ME — Planned] Raw patch formats where useful.**
- **[TK87ME — Planned] Additional HEN/cheat-runner formats as needed.**

## Interoperability

- **[CE — Planned] CE `.CT` import where the neutral model can represent the content safely.**
- **[CE — Future / optional] CE `.CT` export for compatible subsets.**
- **[TK87ME — Planned] Import adapters for other documented formats.**
- **[TK87ME — Planned] Preserve source-specific unsupported metadata when possible instead of silently discarding it.**
- **[TK87ME — Planned] Integration with HEN Cheats Collection workflows** without moving HENCC/PS5-specific responsibilities into Core.

---

# 22. Universal Export Everywhere

## Objective

Use one shared export pipeline for all list-like and tabular data in the application.

This is a project-wide TK87ME design requirement and should remain broader and more consistent than the ad-hoc export behavior found in many memory tools.

## Existing infrastructure

- **[TK87ME — Implemented] Streaming tabular export.**
- **[TK87ME — Implemented] JSON, CSV, TSV, Markdown Table.**
- **[TK87ME — Implemented] Transactional destination publication.**
- **[TK87ME — Implemented] Progress/cancellation.**
- **[TK87ME — Implemented] Structured JSON rather than UI-text-only export.**
- **[TK87ME — Implemented] Scan Results export.**
- **[TK87ME — Implemented] Saved Addresses export.**
- **[TK87ME — Implemented] Disassembly export.**

## Final export coverage

The same infrastructure should eventually support:

- **[TK87ME — Planned] Memory Regions.**
- **[TK87ME — Planned] Modules/Sections.**
- **[TK87ME — Planned] Symbols.**
- **[TK87ME — Planned] Pointer Scan Results.**
- **[TK87ME — Planned] Threads.**
- **[TK87ME — Planned] Registers.**
- **[TK87ME — Planned] Breakpoints/Watchpoints.**
- **[TK87ME — Planned] Find What Writes/Reads/Accesses hits.**
- **[TK87ME — Implemented / current candidate] Call Stack/Frames.**
- **[TK87ME — Planned] Break-and-Trace results.**
- **[TK87ME — Planned] Structures.**
- **[TK87ME — Planned] Snapshot differences.**
- **[TK87ME — Planned] Cheat entries/project tables.**
- **[TK87ME — Planned] Task sequences.**
- **[TK87ME — Planned] Logs/events.**

## Export rule

Structured exports must expose the underlying data model, not only the text currently rendered in the UI.

---

# 23. Event Viewer, Diagnostics, and Logs

## Objective

Provide a unified diagnostic surface for target activity, background operations, debugger activity, and failures.

## Required functionality

- **[ME360 + TK87ME — Planned] Target event viewer.**
- **[TK87ME — Planned] Connection events.**
- **[TK87ME — Planned] Debugger events.**
- **[TK87ME — Planned] Scan events.**
- **[TK87ME — Planned] Write/freeze failures.**
- **[TK87ME — Planned] Pointer scanner progress/events.**
- **[TK87ME — Planned] Script/task-sequencer events.**
- **[TK87ME — Planned] Severity/category filtering.**
- **[TK87ME — Planned] Timestamps.**
- **[TK87ME — Planned] Copy/export.**
- **[TK87ME — Planned] Optional persistent diagnostic log files.**
- **[TK87ME — Planned] Correlation/session identifiers where needed for troubleshooting stale target behavior.**

---

# 24. PC Plugin — Final Capability Target

## Objective

Provide CE-class local PC functionality through a plugin without making Windows/x86 behavior a Core assumption.

## Target functionality

- **[CE / Platform-specific — Planned] Local Windows process attach.**
- **[CE / Platform-specific — Planned] Process/module/region enumeration.**
- **[CE / Platform-specific — Planned] x86 and x86-64 disassembly.**
- **[CE / Platform-specific — Planned] x86 and x86-64 assembly.**
- **[CE / Platform-specific — Planned] Breakpoints/watchpoints/debugger.**
- **[CE / Platform-specific — Planned] AOB injection and code caves.**
- **[CE / Platform-specific — Planned] Native symbol resolution.**
- **[CE / Platform-specific — Planned] Speedhack** as an optional capability only where a backend can implement it correctly.
- **[CE / Platform-specific — Future / optional] Mono/.NET inspection and symbols.**
- **[CE / Platform-specific — Future / optional] DLL/module injection-related features where appropriate.**
- **[CE / Platform-specific — Future / optional] Hardware-assisted execution tracing.**

## Non-goal

A CE-style kernel driver or hypervisor must **not** become a mandatory dependency of the whole application. If advanced kernel/hypervisor functionality is ever added, it should be an optional PC backend/plugin with explicit capability and safety boundaries.

---

# 25. Xbox 360 Plugin — Final Capability Target

## Objective

Use MemoryEngine360 as a feature reference while implementing Xbox 360 behavior through the common TK87ME Plugin SDK.

## Target functionality

- **[ME360 / Platform-specific — Planned] XBDM or other suitable connection backend.**
- **[ME360 / Platform-specific — Planned] Process/title selection.**
- **[ME360 + TK87ME — Planned] Big-endian-aware memory read/write.**
- **[ME360 / Platform-specific — Planned] PowerPC disassembler.**
- **[ME360 / Platform-specific — Planned] PowerPC assembler.**
- **[ME360 / Platform-specific — Planned] PowerPC branch patching.**
- **[ME360 / Platform-specific — Planned] Debugger.**
- **[ME360 / Platform-specific — Planned] Thread/register/call-stack support.**
- **[ME360 — Planned] Pointer scanning.**
- **[CE + ME360 + TK87ME — Planned] Shared scanner, Saved Addresses, Memory Viewer, and export workflows.**
- **[ME360 — Planned] JRPC support.**
- **[ME360 — Planned] Lua/JRPC scripting extensions.**
- **[ME360 — Planned] Remote file browser where backend supports it.**
- **[ME360 + TK87ME — Planned] Xbox trainer/project export.**

Shared subsystems must remain in Core; the Xbox plugin should only own Xbox-specific communication, architecture, debugger, and export behavior.

---

# 26. PlayStation 5 Plugin — Final Capability Target

## Objective

Continue expanding the already functional PS5 plugin while keeping all PS5-specific transport and protocol details inside the plugin.

## Existing functionality

- **[TK87ME / Platform-specific — Implemented] ps5debug-NG connection.**
- **[TK87ME / Platform-specific — Implemented] Remember successful host/port.**
- **[TK87ME / Platform-specific — Implemented] Process enumeration.**
- **[TK87ME / Platform-specific — Implemented] Preferred `eboot.bin`.**
- **[TK87ME / Platform-specific — Implemented] Memory maps.**
- **[TK87ME / Platform-specific — Implemented] Memory read/write and verification.**
- **[TK87ME / Platform-specific — Implemented] Suspend/resume.**
- **[TK87ME / Platform-specific — Implemented] Dedicated concurrent freeze connection.**
- **[TK87ME / Platform-specific — Implemented] Native TurboScan acceleration.**
- **[TK87ME / Platform-specific — Implemented] Backend-resident large result sets.**
- **[TK87ME / Platform-specific — Implemented] PS5 scan options.**
- **[TK87ME / Platform-specific — Implemented] x86-64 disassembly.**

## Planned functionality

- **[TK87ME / Platform-specific — Implemented / verified through rev28; logical Disassembler integration current rev29 candidate] Debugger.**
- **[TK87ME / Platform-specific — Software breakpoints verified through rev15; hardware data watchpoints verified rev16] Breakpoints/watchpoints.**
- **[TK87ME / Platform-specific — Implemented / verified through rev10] Registers.**
- **[TK87ME / Platform-specific — Implemented / verified enumeration; individual control backend-blocked] Threads.**
- **[TK87ME / Platform-specific — Implemented / live-verified through rev28] Call stack and stepping.**
- **[TK87ME / Platform-specific — Planned] x86-64 assembly and patching.**
- **[TK87ME / Platform-specific — Planned] PS5 cheat operation types.**
- **[TK87ME / Platform-specific — Planned] PS5 metadata validation.**
- **[TK87ME / Platform-specific — Planned] JSON/MC4/SHN/SHNEXT export.**
- **[ME360-inspired / Platform-specific — Future / optional] Remote file browser** if the selected PS5 backend can expose a reliable filesystem API.

---

# 27. Themes, UI, Workspaces, Input, and Usability

## Objective

Keep the application productive and recognizable without becoming a visual clone of CE or ME360.

## Existing requirements

- **[TK87ME — Implemented] Light / Dimmed / Dark themes.**
- **[TK87ME — Implemented] External theme definitions.**
- **[TK87ME — Implemented] Live theme switching.**
- **[TK87ME — Implemented] Theme changes affect color/presentation but not layout or functionality.**
- **[TK87ME — Implemented] Semantic Primary / Secondary / Danger button roles.**
- **[TK87ME — Implemented] Consistent Danger styling for destructive and dismissive actions.**
- **[TK87ME — Implemented] Central text-input validation.**
- **[TK87ME — Implemented] Capability-driven control state.**
- **[TK87ME — Implemented] Resizable main-workspace splitters.**
- **[TK87ME — Implemented] Modeless Memory Viewer and Disassembler.**
- **[TK87ME — Implemented] Bottom status-bar application version.**
- **[TK87ME — Implemented] Application title without version/revision in native title/top title row.**

## Planned usability work

- **[TK87ME — Planned] Consistent modeless workspaces for Debugger, Pointer Scanner, Structures, Project, Scripting, Task Sequencer, Logs, and Mod Tools.**
- **[CE + TK87ME — Planned] Global and per-feature hotkeys.**
- **[TK87ME — Planned] Configurable keymap.**
- **[TK87ME — Planned] Workspace/layout persistence.**
- **[TK87ME — Planned] Consistent context-menu command naming across all tables.**
- **[TK87ME — Planned] Consistent selection semantics across all list/table workspaces.**
- **[TK87ME — Planned] Reusable dialogs for confirmation, progress, errors, and long-running operations.**
- **[TK87ME — Planned] Keyboard-first accessibility for important reverse-engineering workflows.**

---

# 28. Safety, Reliability, and Data Integrity

## Objective

Treat safe target handling and reproducible data behavior as first-class functionality rather than optional polish.

## Existing safety model

- **[TK87ME — Implemented] Active Target required before target operations.**
- **[TK87ME — Implemented] Process selection does not imply target activation.**
- **[TK87ME — Implemented] Connection/session generation binding.**
- **[TK87ME — Implemented] Stale-data detection.**
- **[TK87ME — Implemented] Read-back verified writes.**
- **[TK87ME — Implemented] Writable-region enforcement where protection metadata is available.**
- **[TK87ME — Implemented] Bounded reads.**
- **[TK87ME — Implemented] Region-boundary enforcement.**
- **[TK87ME — Implemented] Foreground/background I/O coordination.**
- **[TK87ME — Implemented] Independent freeze I/O where plugin supports it.**
- **[TK87ME — Implemented] Transactional export publication.**
- **[TK87ME — Implemented] Cancellation without publishing incomplete exports/result generations.**
- **[TK87ME — Implemented] Streaming handling of large result sets.**

## Planned safety extensions

- **[TK87ME — Planned] Automatic original-byte capture for code patches.**
- **[TK87ME — Planned] Reversible patch state.**
- **[TK87ME — Planned] Project-wide validation before Apply/Test/Export.**
- **[TK87ME — Planned] Explicit warnings for dangerous target operations.**
- **[TK87ME — Planned] Clear distinction between live-target data, offline dump data, and stale cached data.**
- **[TK87ME — Planned] Structured error categories suitable for UI, logs, scripts, and automated tests.**
- **[TK87ME — Planned] No silent fallback when it would change operation semantics.**

---

# Cross-Subsystem Integration Requirements

The final application should not consist of isolated tools. The following workflows are required integration goals.

## Value-discovery workflow

```text
Connect
  -> Select process
  -> Set Active Target
  -> First Scan
  -> Next Scan
  -> Save Address
  -> Freeze/Edit/Open in Memory Viewer
```

## Reverse-engineering workflow

```text
Saved/Scan Address
  -> Open in Memory Viewer
  -> Open in Disassembler
  -> Find What Writes/Accesses
  -> Inspect registers/call stack
  -> Follow code flow
  -> Patch / inject / pointer-resolve
```

## Pointer workflow

```text
Dynamic address
  -> Pointer Scanner
  -> Rescan across sessions/dumps
  -> Validate stable chain
  -> Convert to Saved Address
  -> Convert to Cheat Project operation
```

## Structure workflow

```text
Object address
  -> Structure Dissect
  -> Compare multiple objects/snapshots
  -> Identify fields/pointers
  -> Save useful fields
  -> Create project operations
```

## Cheat-development workflow

```text
Discovery
  -> Saved Address / instruction / pointer / structure
  -> Cheat Project
  -> Build ordered operation(s)
  -> Validate
  -> Test live
  -> Export through platform plugin
```

## Automation workflow

```text
Saved Address / Project operation
  -> Task Sequencer and/or Script
  -> Conditions / delays / loops / memory actions
  -> Hotkey or trainer control
```

---

# Final Product Definition

When the action plan is substantially complete, TeeKay87's Memory Engine should function as a unified environment for:

- memory scanning;
- memory inspection and editing;
- Saved Address management and freezing;
- pointer resolution and pointer scanning;
- disassembly and code navigation;
- debugging and execution tracing;
- finding code that reads/writes/accesses data;
- assembling, patching, and code injection;
- structure/data analysis;
- memory snapshots and comparison;
- symbol/module analysis;
- automation and scripting;
- cheat-project authoring;
- platform-specific cheat validation/export;
- trainer/mod-tool creation;
- reusable universal data export;
- local PC and remote console targets through one shared host architecture.

The intended result is **not merely "Cheat Engine for PS5"**. The final application should combine the useful reverse-engineering workflows proven by Cheat Engine, the console-oriented pointer/debugger/scripting/automation ideas demonstrated by MemoryEngine360, and the architectural and reliability features developed specifically for TeeKay87's Memory Engine.

Its defining characteristics should remain:

1. **platform-neutral Core architecture;**
2. **capability-driven plugins;**
3. **PS5, PC, Xbox 360, offline dumps, and future-target extensibility;**
4. **safe session-bound target interaction;**
5. **scalable scan-result handling;**
6. **shared modeless reverse-engineering workspaces;**
7. **a neutral Cheat Project source-of-truth model;**
8. **platform-specific exporters rather than platform-specific host logic;**
9. **one universal export model across all list-like tools;**
10. **strong regression testing and live-target verification before functionality is considered complete.**

---

# Development Rules to Preserve While Implementing This Plan

The existing project rules remain mandatory throughout implementation:

- Read `README.md`, `CHANGELOG.md`, and all relevant Markdown documentation before every code change.
- Review the complete current codebase before implementing new functionality so existing services/contracts are reused instead of duplicated.
- Always work from the latest verified source package.
- Do not alter previously verified functionality unless the new feature genuinely requires it; explain such changes before making them.
- Keep all platform-specific functionality in the corresponding plugin.
- Keep shared functionality in Core or the Plugin SDK, whichever is the correct abstraction boundary.
- Ensure every changed/new C# file contains all required `using` directives.
- Update application/plugin/API version metadata consistently where a revision requires it.
- Update `CHANGELOG.md` for every version/revision change.
- Keep `README.md` as a current full-program description, not a historical changelog.
- Store subsystem/research/testing documentation in a well-organized `docs/` hierarchy.
- Continue using independent plugin versions and a separate Plugin API compatibility version.
- Treat the Windows build and runtime tests as the authoritative compile/runtime verification gate.
- Preserve public-project-quality code and documentation throughout the codebase.

---

# Reference Projects

The external projects below are feature and architecture references for this development plan:

- **Cheat Engine** — https://github.com/cheat-engine/cheat-engine
- **MemoryEngine360** — https://github.com/AngryCarrot789/MemoryEngine360
- **ps5debug-NG** — https://github.com/OpenSourcereR-dev/ps5debug-NG

They should be studied to understand useful functionality and platform behavior, while TeeKay87's Memory Engine continues to use its own architecture and implementation unless reuse is explicitly evaluated and documented separately.
