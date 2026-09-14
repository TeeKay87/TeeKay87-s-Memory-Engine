# TeeKay87's Memory Engine

TeeKay87's Memory Engine is a modular Windows desktop application intended to become a shared environment for memory scanning, memory inspection, debugging, and cheat development across multiple target platforms.

The application is written in C# and WPF around a platform-neutral Core and public Plugin SDK. Platform-specific communication and target behavior belong in plugins so the same host, scanner workflow, saved-address model, memory tools, and presentation layer can be reused across PlayStation 5, PC, Xbox 360, and future platforms.

## Current Status

The current application build is **0.1.7.rev48 - Comparer No Group Selection Reset Fix**. The Call Stack Comparer now clears the live Group ComboBox selection itself when **No group** is chosen, matching the already-correct underlying snapshot membership immediately without requiring the window to be reopened. The ungrouped state remains intentionally absent from the registered session-group list, while the previous group name stays available for reuse. Rev46 passed the Windows automated gate at **152/152** and its focused runtime checks confirmed Debugger Export attach gating, Description-aware Saved Address removal confirmation, stale pairwise-result invalidation, and Universal Export Select None/live Continue state. Plugin API remains `2.18.0`, Mock remains `1.0.1.rev17`, PS5 remains `0.1.2.rev39`, snapshot schema remains version 1, and the automated registry remains **152 checks**. Verification is defined in `docs/testing/APP_0.1.7_REV48_VERIFICATION.md`.

The permanent main workspace is now based on the familiar workflow used by Cheat Engine while using the project's own modernized presentation and platform-plugin architecture. The application is not intended to be a visual clone. The layout keeps the parts of the Cheat Engine workflow that are useful and immediately recognizable: a persistent target selector, temporary scan results, scan controls, and a separate persistent saved-address table.

The current built-in plugins are:

| Plugin | Plugin version | Plugin API | Current purpose |
| --- | --- | --- | --- |
| In-Memory Test Target | `1.0.1.rev17` | `2.18.0` | Deterministic development/regression target using Core-owned Scan Types, plugin-owned Value Types, synthetic custom/unknown 64-bit disassembly, debugger attach/pause/continue/events, deterministic thread/register behavior, software breakpoints, hardware watchpoints, three-frame call stacks, and native Step Into fixtures |
| PlayStation 5 | `0.1.2.rev39` | `2.18.0` | Connection, memory/scanner services, x86-64 disassembly, dedicated ps5debug-NG debugger transport, real thread/register services, guarded extended-register reads, software breakpoints, hardware Write/Read-Write watchpoints, server-side call-stack walking, and native thread Step Into |

Plugin versions are independent from the TeeKay87's Memory Engine application version. The Plugin API also has its own compatibility version.

### Implemented application functionality

- multi-project solution separating the WPF application, shared Core, Plugin SDK, platform plugins, and verification tests;
- centralized application title, version, revision, and active feature information through `AppInfo`; the native Windows title and compact top application row show only the application title, while the current `version.revN` is shown only at the right edge of the permanent bottom status bar;
- the supplied TK artwork is the application icon: the project embeds a multi-size Windows `.ico` for the executable and assigns the same icon to all WPF windows so title bars, task switching, and shell presentation stay consistent;
- the complete Scan control surface is disabled until a process has been explicitly promoted to **Active Target**, preventing Value/Scan Type/Value Type/plugin Scan Options and scan workflow controls from appearing interactive while the process picker is only browsing a selection;
- semantic button roles are theme-driven application-wide: Primary is affirmative, Secondary is neutral/supporting, and Danger is used consistently for Remove/Delete/Cancel/Exit/Disconnect/Abort-style actions and other destructive or dismissive controls;
- existing repository source-preflight tooling remains under `tools/preflight/`; further development of that auxiliary tooling is paused during the current debugger work, and the real Windows/WPF build remains the authoritative compile gate;
- reusable theme-aware confirmation dialogs with configurable title, message, affirmative/cancel button text, semantic Information/Warning/Danger tone, and safe default-button behavior; Saved Addresses **Remove All** and Call Stack Comparer snapshot Remove and Remove All actions reuse this shared dialog instead of bespoke message boxes;
- reusable Core tabular export infrastructure with schema-aware JSON plus CSV, TSV, and Markdown table writers; data sources stream bounded batches directly to a temporary file, support column selection and progress/cancellation, validate the advertised row count, and publish the destination only after a complete successful export;
- active export integration for **Scan Results** and **Saved Addresses**: Scan Results supports All/Displayed/Selected scopes and preserves complete-set semantics across materialized, disk-backed, and backend-resident scan storage, while Saved Addresses supports All/Selected stable snapshots;
- capability-based Plugin SDK with platform-neutral target, process, connection, memory-region, memory-access, Value Type, optional Value Type live-input policy, Core-owned Scan Type integration, and optional Scan Option presentation/applicability contracts;
- architecture-neutral disassembly foundation in Plugin API `2.11.0`: `IDisassemblerProvider` converts bounded caller-supplied target bytes into neutral `DisassembledInstruction` records with raw bytes, length, mnemonic, operands, flow-control classification, optional direct target, validity state, and optional neutral syntax/presentation tokens; the original constructor remains available for older compatible 2.x plugins that do not provide syntax metadata;
- shared Core `DisassemblyReader`, `DisassemblySnapshot`, and `DisassemblySessionIdentity` primitives: reads are clamped to one readable non-guarded region through `IMemoryReader`, provider output is range/order/raw-byte validated, and workspace instances bind safely to plugin/process/connection-generation identity. Rev29 adds presentation-only `DisassemblyOverlay` support so debugger instrumentation can be replaced with captured original bytes in a local decode buffer without writing to the target;
- Mock plugin `1.0.1.rev17` provides the deterministic synthetic Disassembler/debugger fixtures and advertises `Debugger`, `ThreadEnumeration`, `ThreadControl`, `RegisterAccess`, `Breakpoints`, `Watchpoints`, `CallStack`, and `StepExecution`; its attached debugger session exposes neutral pause/resume events, deterministic Main/Worker/Render threads, per-thread Suspend/Resume while Running, writable 64-bit General/Control register rows while Paused, read-only 80-bit floating-point, 128/256-bit SIMD and debug-register fixtures, persistent/temporary software execute breakpoints, deterministic Read/Write/Read-Write hardware watchpoints with a four-slot limit, deterministic three-frame call stacks, and native Step Into while Paused;
- PS5 plugin `0.1.2.rev39` targets Plugin API `2.18.0`, retains the verified Iced `1.21.0` x86-64 Disassembler provider and dedicated debugger transport, and advertises `Debugger`, `ThreadEnumeration`, `ThreadControl`, `RegisterAccess`, `Breakpoints`, `Watchpoints`, `CallStack`, and `StepExecution`. The plugin keeps the matching software-breakpoint interrupt's general/FPU packet snapshot as the authoritative logical stop context and consumes ps5debug-NG's already-completed transparent breakpoint step when Step Into is requested from that stop. Rev36 preserves defensive software-breakpoint restoration before detach/disposal, while the rev31 hardware-watchpoint teardown path explicitly disables every backend-active watchpoint plus staged temporary-watchpoint cleanup before backend detach. The event channel remains open while teardown commands run so backend-generated interrupts can be drained and ignored while detaching. Ordinary paused register reads still use the debugger-owner GETREGS path, safe optional FPU/FS/GS reads remain capability-gated, paused GETDBREGS remains suppressed, and all PS5 register rows remain read-only;


- debugger watchpoint events now keep the real stop/current instruction pointer separate from the instruction that triggered the watched memory access. Plugin API `2.17.0` adds neutral trigger-resolution metadata (`BackendExact`, `DisassemblyDerived`, `Unresolved`) plus explicit watched-address/access/size accessors. Core resolves PS5 watchpoint triggers from the existing logical Disassembler stream only when one valid instruction ends exactly at the stop IP; otherwise the event remains explicitly unresolved. The Disassembler shows `Watchpoint hit` only on a resolved trigger instruction and uses `Watchpoint stop (trigger unresolved)` at the real stop IP when resolution cannot be established safely;
- Disassembler row context menus expose separate **Add Breakpoint** and **Add Watchpoint** actions. Both require exactly one selected instruction and an already attached Debugger for the same target/session. Add Breakpoint is enabled only for a valid instruction in the current executable region and reuses the existing Software/Execute request validation/add path. Add Watchpoint is enabled for any single selected memory-access instruction while the matching debugger is Paused and an optional plugin-owned `IDisassemblyWatchpointResolver` can safely derive one effective data address, supported byte width, and platform-appropriate access mode from that instruction plus the debugger's current paused register snapshot; ambiguous, non-memory, `LEA`/NoMemAccess, unsupported-width, stale, or unresolvable cases remain disabled. The final request still passes the existing plugin validator and `DebuggerViewModel.AddBreakpointAsync`;
- the debugger can capture a paused stop into an immutable Core-owned **Debugger Snapshot** containing source/session metadata, the structured event context, raw register bytes, call frames, bounded logical disassembly around stop/trigger, current breakpoint/watchpoint state, section status, and a bounded stack-memory window when available. Capture requires the same target/generation and unchanged stop sequence from start to publication; running, stale, cancelled, or changed-stop captures publish no snapshot;
- complete debugger snapshots export/import through schema `teekay87-memory-engine-debugger-snapshot` version `1`. Addresses are canonical hexadecimal strings, enum values use stable names, raw values remain exact, unknown additive fields are tolerated, invalid/incompatible files are rejected, and file publication is temporary-file/replace based so cancellation or failure does not publish partial JSON. Imported snapshots are offline comparer data and never attach, reconnect, restore breakpoints/watchpoints, or control a target;
- the Debugger toolbar can Universal Export the currently available **Threads, Registers, Breakpoints / Watchpoints, Call Stack, and Events** as JSON/CSV/TSV/Markdown using structured source data. The shared export dialog renders Scope and Format choices by their user-facing names across every export consumer, provides **Select None** and **Select All**, disables **Continue** immediately when no columns are selected, and keeps validation text synchronized with live column selection. Event export keeps stop/current IP, trigger instruction, watched address/access/size, and trigger resolution in distinct columns;
- the Call Stack workspace opens a separate modeless **Call Stack Comparer**. Opening or reopening the comparer is inspection-only: it never captures implicitly. A single comparer workspace is retained for the lifetime of the same Debugger window/session, so captured/imported snapshots and still-valid comparison results survive comparer-window close/reopen. New live snapshots are added only through **Capture Current**, whose enabled state follows the current paused stop context and re-enables on each new valid pause. Closing the Debugger removes live capture authority from an already-open comparer without deleting its captured/imported offline evidence. Snapshot rows use always-visible editors consistent with Saved Addresses: Label and Notes are text fields, Group is a dropdown, and each row has its own confirmed Danger-styled Remove action. The Group popup exposes **No group** for explicit unassignment, then the dedicated new-group input, followed by existing session groups. Group names remain in the session catalog even when no snapshot currently belongs to them. The redundant panel-level Remove action is gone. **Compare 2** is enabled only for exactly two selected snapshots; complete-snapshot **Export...** only for exactly one; **Compare Groups** only for two distinct selected groups that both contain snapshots; **Remove All** only when snapshots exist; and **Export Results...** only while a valid current comparison result exists. Pairwise results survive Label/Notes/Group metadata changes but are invalidated when either compared snapshot is removed. Group results are invalidated when relevant membership, Group A/B selection, or involved snapshots change. The comparer normalizes code locations to Module + Offset when captured, compares raw register values and logical/original disassembly, keeps trigger/current instruction distinct, compares breakpoint/watchpoint and bounded memory context, and promotes only stable cross-group differences as explainable potential discriminators. Comparisons operate solely on captured PC-side data and generate no target traffic;
- the current Debugger **Breakpoints / Watchpoints** manager is capability-driven and uses the neutral `DebuggerBreakpointRequest` / `DebuggerBreakpoint` model. Add can create the verified one-byte Software/Execute breakpoint or, when `Watchpoints` is advertised, a Hardware data watchpoint with plugin-validated access/size/alignment. The list shows **Address, State, Type, Mechanism, Access, Size, and Lifetime**: Type describes what the record is (`Breakpoint` for Execute, `Watchpoint` for data access), while Mechanism describes how it is implemented (`Software` / `Hardware`). Refresh, Enable, Disable, Remove, and Remove All operate on both kinds. Disassembler navigation remains available only for Execute records because a data-watchpoint address is not an instruction address. Address and Size reuse the host hexadecimal/unsigned-integer live input filters while final plugin validation remains authoritative. Persistent/temporary lifetime semantics are shared. Mock supports deterministic Read, Write, and Read/Write watchpoints. PS5 maps Write and Read/Write requests to ps5debug-NG `CMD_DEBUG_SET_WATCHPOINT` (`0xBDBB0004`), keeps its four DR0-DR3 slots and DR7 encodings plugin-private, and rejects unsupported Read-only requests rather than silently broadening them to Read/Write. PS5 software breakpoints retain the verified 30-slot `0xBDBB0003` mapping and paused Disable/Remove staging;
- Scan Results and Saved Addresses now expose **Add Breakpoint** and **Add Watchpoint** in their row context menus. These shortcuts are intentionally usable only when an existing Debugger window is already attached to the same plugin/process/connection generation. Add Breakpoint builds a persistent Software/Execute request; Add Watchpoint builds a persistent Hardware/Write request using the row's current/saved value width. `IDebuggerBreakpointValidationService` preflights the complete request so impossible actions are greyed out rather than attempted. The normal Debugger Add dialog remains available for explicit access/size/lifetime choices and remains the fallback for older compatible plugins that do not implement the optional validator;
- hardware-watchpoint events now preserve all three relevant neutral locations independently: `InstructionPointer` is the real stop/current instruction pointer, `TriggerInstructionAddress` is the memory-accessing instruction when safely resolved, and `TriggeredBreakpoint.Request.Address` / `WatchedAddress` describe the watched data address. The PS5 translator first uses DR6 B0-B3 bits already carried in the asynchronous 1184-byte event packet and never re-enables the unsafe paused `GETDBREGS` command. Current ps5debug-NG source clears packet-side DR6 before delivery for data-watchpoint traps; with exactly one active hardware watchpoint the plugin can safely use a documented single-watchpoint inference fallback, while a zero-DR6 stop with multiple active watchpoints remains an unattributed signal stop rather than guessing which temporary record fired. The external source-level backend defect is documented under `docs/bug-reports/ps5debug-ng-watchpoint-interrupt-clears-dr6-trigger-status.md`;
- the Debugger workspace now uses shared proportional splitters in both directions: the left Threads/Registers column and right debugger workspace start at approximately **36/64**, while the right upper tabbed area and Events retain the established approximately **65/35** vertical split; every splitter retains the shared 20/80 to 80/20 movement limits and adaptive sizing;
- if a breakpoint re-hits while the Continue command is still completing, the host defers that Paused stop context until the busy transition ends and then refreshes Breakpoints, Threads, and Registers through the normal stop-context path. Running state still clears stale register rows immediately, while manual Pause retains its existing explicit refresh;
- capability-driven modeless Disassembler workspace introduced in host `0.1.6.rev3` and extended through the completed `0.1.6.rev14` block: the main target strip exposes **Disassembler...** only for plugins advertising `Disassembly`; the workspace binds to the Active Target/process/connection generation, routes reads through the existing foreground target reservation, accepts hexadecimal Go To/Refresh reads, supports successful-address Back/Forward history, and shows Region / Module, visible range, Protection, Architecture, and a virtualized **Address / Bytes / Markers / Instruction** table. The default view contains up to 512 bytes before plus 512 bytes from the requested origin, decodes that complete range continuously, marks the decoded instruction containing the requested address with the existing theme-aware green origin surface, applies theme-aware syntax coloring inside the Instruction cell, and in rev29 consumes debugger presentation metadata so Software/Execute `INT3` instrumentation is masked by the captured original instruction while breakpoint/watchpoint information appears separately in Markers;
- the rev12-rev14 Disassembler workflow provides extended row selection with right-click selection preservation, selection-consistent granular copy commands, Displayed/Selected universal export through the existing JSON/CSV/TSV/Markdown pipeline, Previous/Region Start/Region End/Next readable-region navigation through the shared Core navigator, real module-relative origin text when module metadata exists, and direct Follow Target navigation through the same successful-address history. Rev29 extends structured Disassembler export with the `markers` field while exporting the same logical bytes/instructions that are visible in the workspace;
- the completed `0.1.6.rev14` Disassembler feature block has passed its full automated, Mock-runtime, and live-PS5 hardware acceptance and is now the verified code-navigation base for debugger integration;
- Plugin API `2.12.0` introduced the neutral debugger provider/session boundary plus optional attached-session contracts for thread enumeration, separate thread control, registers, breakpoints/watchpoints, call stacks, and stepping; Plugin API `2.13.0` added neutral `DebuggerRegisterValueEncoding`; rev11 advanced the API to `2.14.0` with the optional `IDebuggerBreakpointStateService`; rev16 advanced to `2.15.0` by adding optional `DebuggerEvent.TriggeredBreakpoint` context; rev30 advances to `2.16.0` with optional `IDebuggerBreakpointValidationService`, allowing generic UI to preflight a complete breakpoint/watchpoint request without mutating debugger state;
- neutral debugger models describe execution state, stop reasons/events, threads, fixed-width register bit-vectors with architecture-neutral InstructionPointer/StackPointer/FramePointer roles, stack frames, breakpoint/watchpoint requests, and step kinds;
- shared Core `DebuggerSessionIdentity`, `DebuggerEventContext`, and `DebuggerSessionCoordinator` bind debugger activity to plugin/process/connection generation, serialize lifecycle/control operations, validate the process returned by attach, assign monotonic event sequence numbers, and dispose failed/detached backend sessions;
- the verified rev1 foundation adds `TargetCapabilities.ThreadControl` so plugins can distinguish thread enumeration from individual-thread suspend/resume; rev6 activated `ThreadEnumeration`/`ThreadControl`, rev7 activated `RegisterAccess`, rev11 activated `Breakpoints`, rev16 activated `Watchpoints`, and rev17 activates the already reserved `CallStack` and `StepExecution` capabilities in Mock and PS5; rev30 advanced the API to `2.16.0` for optional request validation, and rev32 advanced it to `2.17.0` for separate watchpoint trigger/current-IP context; rev33 adds no Plugin API change, and rev35 advances it to `2.18.0` for optional Disassembler watchpoint-target derivation;
- capability-driven modeless Debugger workspace in the current rev39 candidate: **Debugger...** appears only for plugins advertising `Debugger`; each window is bound to one Active Target and connection generation, uses the shared Core coordinator for explicit Attach/Pause/Continue/Detach, displays a bounded virtualized neutral event history, rejects stale target/event contexts, conditionally adds host-neutral Threads and Registers panes, and provides a capability-driven Call Stack workspace plus Step Into/Step Over/Step Out/Run-to controls; compact Breakpoints / Watchpoints and Call Stack selector buttons reuse the shared application button template, show the selected workspace with an accent outline, display exactly one upper-right workspace at a time, and remove the former duplicated title/count rows while register/call-frame snapshots are still cleared as soon as the target resumes or the bound session becomes stale;
- shared modeless tool-window lifecycle management for Debugger, Disassembler, and Memory Viewer: each tool is centered relative to MainWindow when first shown and then releases WPF ownership so normal desktop activation/z-order applies in both directions; closing MainWindow holds the first close request, awaits asynchronous tool cleanup such as debugger detach/session release, runs synchronous viewer/disassembler disposal, closes every tracked tool through its normal WPF close path, then posts MainWindow's final `Close()` through the dispatcher so the final close occurs only after the original WPF `OnClosing` stack has unwound; modal dialogs remain owned/modal and are not converted into modeless tools;
- rev7 register presentation is architecture-neutral: rows expose plugin-provided display names, bit width, group, immutable value bytes, write capability, and neutral semantic roles; numeric formatting/parsing uses the SDK-declared `DebuggerRegisterValueEncoding`, Mock register writes require paused state plus exact-width read-back verification, and the semantic InstructionPointer role can open the existing Disassembler at the current instruction without WPF testing names such as RIP/PC;
- host-owned debugger coordinator registration ensures debugger sessions are disposed before target-session replacement/disconnect, Active Target changes, or plugin shutdown; immutable target identity remains valid across temporary foreground-operation gating, while starting a new attach still requires the stricter `CanOpenDebugger` gate;
- PS5 rev26 debugger transport retains the verified rev25 TCP `755` callback/Attach/Pause/Continue/Detach behavior and additionally serializes `CMD_DEBUG_GET_THREAD_LIST`, `CMD_DEBUG_THREAD_INFO`, `CMD_DEBUG_SUSPEND_THREAD`, and `CMD_DEBUG_RESUME_THREAD` on the same dedicated debugger command connection without sharing the normal memory/scan stream;
- independent plugin version/revision metadata and Plugin API compatibility metadata;
- runtime discovery of plugin assemblies from the application's `Plugins` directory;
- isolated plugin loading through collectible `AssemblyLoadContext` instances;
- plugin metadata, API compatibility, connection-setting, duplicate plugin-id, plugin-owned Value Type/Scan Option validation, and Core-owned Scan Type selection;
- plugin-defined connection settings rendered generically by the WPF application;
- host-managed plugin settings introduced in Plugin API `2.3.0`, with Core-owned plugin-id namespace isolation inside the shared application settings document;
- PS5 connection convenience persistence: the most recent successfully connected host/IP and port are restored as the plugin connection-field defaults on later application launches;
- generic Connect and Disconnect handling through the Plugin SDK rather than platform-specific WPF code; explicit target-level actions are not discarded merely because a Saved Address refresh/Frozen operation is momentarily active, and instead reserve the foreground and continue at the next safe Saved Address idle boundary;
- generic process enumeration through `IProcessProvider` with separate selected-process and Active Target state, plus optional `IForegroundProcessProvider` preferred-process selection; the PS5 plugin uses this neutral service to select `eboot.bin` automatically when it is present without making it the Active Target;
- compact responsive target header: the upper target/connection surface contains exactly two permanent control rows. Row 1 is Platform -> plugin-declared connection inputs -> Connect -> Disconnect -> Target Process -> Refresh -> Set Active Target. Row 2 is Reload Plugins -> Disassembler -> Debugger, with **Disassembler...** and **Debugger...** using the theme-driven Primary action style and future top-level tool/action buttons appended to that same second row. Platform, Target Process, and every ordinary plugin-declared TextBox/ComboBox in row 1 share one host-computed responsive width capped by `UiMetrics.TopTargetInputMaxWidth = 180`; there is no minimum width, so the controls shrink together when the row is constrained after fixed actions and existing left/right/inter-control margins are reserved. The redundant `Active <process>` and process-count/enumeration hint text remain removed, and connection state remains at the far left of the permanent bottom status bar in a theme-aware red/green indicator;
- automatic process refresh after connection for plugins advertising `ProcessEnumeration`;
- automatic memory-map retrieval for the Active Target when a plugin advertises `MemoryRegionEnumeration`;
- active-target memory regions retained in the host as neutral `MemoryRegion` models for validation and shared scanner work;
- Memory Viewer using shared Core `MemoryViewerReader`, `MemoryViewerWriter`, and `MemoryViewerRegionNavigator` services: Scan Results and Saved Addresses can open a modeless themed viewer at an address, Core reads only a bounded page inside one readable non-guarded region, normal values retain the established 512-byte page, and larger known source spans may request a larger page up to the existing 65,536-byte Core limit so their forward value bytes remain visible where the region permits; the WPF table presents explicit 16-byte Address/Hex Bytes/ASCII rows plus Region / Module, region range, Protection, visible range, Go To, Refresh, Back, Forward, Previous/Next Region, Region Start/End, and viewer-local bookmarks; writable regions can be edited one displayed row at a time through explicit pre-read validation, `IMemoryWriter`, and immediate read-back verification without changing page protections;
- Memory Viewer selection/copy/navigation behavior: extended row selection is independent from the navigation origin, the row containing the current origin address remains highlighted in green when selection moves elsewhere, and rev7 additionally highlights the complete known source-value byte span in both Hex Bytes and ASCII without changing the underlying text or row geometry; Scan Results use `CurrentValue.Size`, Saved Addresses use the current `ValueSize`, cross-row spans continue onto the next row, while manual/bookmark/region navigation uses a one-byte span; Back/Forward preserve both address and span length, `Alt+Left`/`Alt+Right` navigate that history, and the row context menu still supports Copy Address/Hex Bytes/ASCII/Row/Selected with `Ctrl+C` mapped to Copy Selected;
- Memory Viewer target safety captures both process identity and connection generation and routes reads and explicit writes through the host foreground-target reservation, so a viewer cannot silently follow another Active Target/reconnect or overlap the existing Saved Address/scan command stream; stale displayed bytes are rejected before writing, read-only/guarded ranges remain non-editable, and issued writes are read back before being reported as verified;
- Scan Results context-menu **Save Address** now respects multi-row selection: when the context-clicked row is part of a multi-selection, every selected current result is processed, existing Saved Address identities are skipped, derived count/export-state notifications are refreshed once after additions, and one aggregate status is reported; double-click remains a single-row shortcut;
- capability-driven raw memory reads through the existing neutral `IMemoryReader` contract;
- host-side validation that raw-read requests stay inside readable Active Target regions when a memory map is available;
- retained raw-memory read diagnostic code and commands for development/regression use; the Raw Memory Read inspector is no longer exposed in the main UI;
- capability-driven raw memory writes through the existing neutral `IMemoryWriter` contract;
- host-side validation that raw-write requests stay inside writable Active Target regions when a memory map is available;
- retained raw-memory write diagnostic code and commands for controlled Int32/write-verification development work; the Raw Memory Write inspector is no longer exposed in the main UI;
- an explicit **Safe Write Test** diagnostic action that automatically searches the Active Target memory map for a readable/writable, non-executable, non-guarded region, checks a four-byte candidate for short-term stability, writes back the exact bytes already present, and immediately verifies them without intentionally changing the target value;
- shared Core memory scanner foundation using neutral `IMemoryReader`, `MemoryRegion`, `TargetProcess`, and `TargetArchitecture` data;
- optional plugin-side native scan acceleration through the neutral legacy `INativeValueScanner` service plus the API 2.2 streaming `INativeValueScanStreamProvider`; Plugin API `2.7.0` adds `INativeScanTypeMappingProvider`, allowing each plugin to declare which Core Scan Types are semantically equivalent to its native operations for specific stages/Value Types, while Core keeps the shared reader-based scanner as the universal fallback;
- optional native refinement through `INativeValueScanRefiner` and the API 2.2 `INativeValueScanStreamRefiner`; Plugin API `2.9.0` adds optional `INativeValueScanResidentResultSet`, allowing a plugin/backend to keep a complete authoritative result set resident, expose its full count, and retrieve bounded result windows on demand. PS5 uses this for large TurboScan survivor sets so mapped Next Scans run directly against target-resident state; semantically incompatible predicates trigger explicit on-demand materialization into the shared Core disk-backed path;
- neutral process suspend/resume through `IProcessControl`, with a capability-driven **Pause target while scanning** checkbox that is Off by default and automatically resumes the Active Target after completion, cancellation, or scan failure;
- plugin-driven Scan panel options through `IMemoryScanOption`; Plugin API `2.8.0` optionally lets an option declare generic ChoiceList/Toggle presentation plus Core Scan Type applicability. The current PS5 plugin exposes Endianness as a **Little-endian byte order** checkbox, Alignment as a choice list, and Floating-point rounding only for Exact Value Float/Double scans; plugins that do not expose scan options render no extra controls;
- automatic vertical overflow handling in the Scan panel so plugin-provided controls remain reachable at smaller window heights; the panel does not use horizontal scrolling and keeps deliberate spacing between controls and the scroll track;
- Core-owned Scan Types shared across all plugins: Exact Value, Fuzzy Value, Bigger Than, Smaller Than, Between, Unknown Initial Value, Unknown Initial Low Value, Increased Value, Decreased Value, Changed Value, Unchanged Value, Increased By, and Decreased By, with First/Next-stage filtering and Value Type compatibility filtering;
- shared scanner support for all standard fixed-width numeric Value Types plus Exact Value Array of Bytes; PS5 native acceleration is selected through the plugin's semantic mapping table, including mapped Exact/Fuzzy/ordered/previous-value predicates and snapshot-based Unknown Initial Value, with Core fallback whenever semantics, endianness, capability, or runtime state do not match;
- compact standard integer Value Type presentation: signed Int8/Int16/Int32/Int64 appear as **1 Byte**, **2 Bytes**, **4 Bytes**, and **8 Bytes**, unsigned alternatives remain explicit, and built-in plugins select signed **4 Bytes**/Int32 by default;
- reusable WPF textbox filtering for memory input: standard integers accept decimal and `0x`-prefixed hexadecimal syntax appropriate to signedness, Float/Double accept invariant decimal/scientific edit states, Array of Bytes accepts its supported hexadecimal/separator syntax, Saved Address addresses accept an optional `0x` prefix plus at most 16 hexadecimal digits, and invalid paste operations are blocked by the same rules, and rev25 also intercepts the WPF Space-key path so whitespace cannot bypass numeric/Address filters while Array of Bytes still accepts separators;
- scan-panel keyboard workflow where Enter in a Value field runs First Scan before a scan session exists and Next Scan afterward; the theme-primary button follows the next logical action, Scan Type remains selectable between scans, and New Scan restores First Scan as the primary action;
- bounded 256 KiB shared-scanner reads with readable/Guard filtering, target-endianness-aware numeric encoding/decoding, value-type-specific alignment, disk-backed result streaming, and protection against runaway repeated read failures; the historical `2,000,000` safety limit remains only on legacy in-memory/list materialization paths;
- a complete scan-result abstraction plus a 50,000-row WPF presentation cap; shared/Core results remain disk-backed, while sufficiently large authoritative native result sets may stay resident in the plugin/backend and expose only the bounded preview to WPF. Next Scan always operates on the complete candidate set rather than only the displayed rows;
- compact hexadecimal result-address formatting without redundant leading zeroes, plus a row context menu for **Save Address**, **Copy address**, and **Copy value**; the former diagnostic **Change value** action is no longer exposed because its Raw Memory Write surface is intentionally hidden;
- a permanent Cheat Engine-inspired main workspace now connected to the initial scanner implementation;
- separate Scan Results and Saved Addresses tables so temporary scan candidates are not confused with user-saved addresses; both tables expose the containing memory range's read-only **Protection** flags when the active memory map can resolve them;
- functional Saved Addresses rows created by double-clicking a Scan Result or choosing **Save Address** from its context menu; saved rows survive First/Next/New Scan transitions for the current application/plugin workspace;
- manual Saved Address creation through **Add Manually**: the dialog uses the existing hexadecimal input filter, plugin-declared Value Types, fixed/variable byte-length rules, target/session revalidation, duplicate identity handling, and immediate post-create refresh; unsupported hexadecimal-display, Binary/Text-specialization, and pointer-chain concepts are present only as visibly disabled planned placeholders;
- interactive Saved Addresses columns for **Frozen**, **Description**, **Address**, plugin-declared **Type**, and **Value**, plus a read-only **Protection** column; the table includes generic target reads, generic writes, inline edits, copy/remove context-menu actions, immediate requested-state feedback for queued Freeze activation, and target-identity safety checks;
- coordinated Saved Address user actions: Freeze enable, Address/Type commits, direct Value writes, individual Remove, and confirmed Remove All are protected from transient refresh/Frozen timing windows; pending removal immediately disables Frozen and prevents new scheduled work, while edits/writes wait for a safe background-I/O boundary instead of requiring a second attempt;
- independent live-value refresh and Frozen-write intervals in Settings, both persisted in the shared application settings document and applied immediately after Save; live refresh defaults to 500 ms and covers all Saved Addresses plus only currently realized Scan Results rows, Frozen writes default to 100 ms, and both support a 50-10,000 ms range;
- bounded Saved Addresses scheduling where value refresh pauses for First/Next Scan, while Frozen writes continue during scans when **Pause target while scanning** is Off; when a plugin exposes API 2.4 `IConcurrentMemoryWriter`, repeated Frozen writes and direct Saved Address Value writes prefer that independent writer so they do not collide with the primary scan/refresh transport; direct Value editing is protected from background display refresh while the cell is being edited, and manual writes take priority over future periodic ticks; transient Frozen write failures keep the row Frozen and retry on the next interval; the PS5 plugin backs the concurrent writer with a separate lazily opened ps5debug-NG connection;
- per-row Remove controls plus a confirmed **Remove All** toolbar action;
- reusable application-wide WPF styling for buttons, text boxes, combo boxes, data grids, and other shared controls;
- one central 34-unit height standard for normal single-line buttons, text inputs, and selectors so current and future controls align consistently;
- vertically centered single-line TextBox content with compact internal padding so text remains fully visible without changing the established 34-unit control height;
- runtime color themes loaded from external JSON files;
- theme-owned Primary/Secondary/Danger button surfaces and text colors, plus theme-owned tooltip background, border, and text colors; Disconnect and Cancel Scan use the Danger role so stop/cancel/exit-style actions can be recolored consistently by each theme;
- an application-owned tooltip template so hover hints remain readable in Light, Dimmed, and Dark instead of falling back to operating-system tooltip colors; long string hints wrap within the shared tooltip width instead of being clipped;
- application-owned ContextMenu/MenuItem/Separator templates so scan-result context menus use the active theme throughout and no longer expose the operating-system light icon/checkmark gutter in Dimmed or Dark;
- immediate theme switching without restarting the application;
- persistence of the selected theme between launches;
- an application-level **Settings** window that extends the existing `%LocalAppData%\TeeKay87\MemoryEngine\settings.json` persistence model instead of introducing a second configuration store;
- a configurable **Scan Results Storage Location**, defaulting to `%LocalAppData%\TeeKay87\MemoryEngine\ScanResults`, with browse/default actions, write validation, persisted selection, explicit next-application-session activation so in-use scan-session files are never migrated between roots, and content-driven Settings layout so wrapped storage information and validation messages expand the storage panel instead of being clipped;
- host-owned temporary scan-result storage with a unique application-session GUID for every launch, a unique scan-session GUID for every First Scan, versioned `session.json`/`scan.json` metadata, compact versioned binary result files, explicit Creating/Writing/Committed/Failed/Cancelled/Invalidated state, generation-based transactional result replacement, conservative stale-session cleanup, New Scan invalidation, and best-effort shutdown cleanup;
- deletion-independent scan-session isolation: failed physical cleanup is logged but can never make an old application/scan session eligible for reuse by a later session;
- a reusable host-level modal operation-progress component with a platform-neutral `OperationProgress` contract, determinate/indeterminate modes, live status/detail text, optional cancellation, theme-aware WPF presentation, owner-window blocking, and exception/cancellation propagation; large native result transfers use the same generic component while complete result generations are committed to disk;
- application scan-storage lifecycle logging under `%LocalAppData%\TeeKay87\MemoryEngine\Logs\MemoryEngine.log` without per-result log spam;
- migration of legacy bundled theme ids and protection against stale renamed theme files left by incremental builds;
- corrected object-backed ComboBox presentation so platform, target-process, and theme selectors show their intended labels instead of CLR type names; the Platform selector now shows only each plugin's declared `Name`, while Backend remains available in Plugin details;
- three included color themes: **Light**, **Dimmed**, and **Dark**;
- deterministic in-memory development target with process enumeration, foreground-process discovery, memory maps, memory reads, and memory writes;
- PlayStation 5 connection, process enumeration, memory-map enumeration, and raw target-memory reads/writes using a native C# ps5debug-NG command-channel implementation; in-flight read/write transactions are completed atomically before scan cancellation is observed so cancelling a scan cannot leave unread protocol bytes on the shared command stream;
- verification executable covering the shared plugin foundation, Mock memory/disassembly behavior, shared scanner/refinement behavior, plugin versioning, PS5 connection metadata, the ps5debug-NG handshake, process-list parsing, memory-map parsing/protection conversion, raw read/write protocol behavior, cancellation-safe command-stream reuse, write/read-back preservation, PS5 x86-64 decoding, and runtime plugin discovery. The PS5 provider tests restore the same Iced package used by the plugin.

Core owns the shared **Scan Type catalog** while plugins continue to own concrete **Value Types**, platform-specific Scan Options, and native/backend implementations. The catalog provides 13 standard predicates: Exact Value, Fuzzy Value, Bigger Than, Smaller Than, Between, Unknown Initial Value, Unknown Initial Low Value, Increased Value, Decreased Value, Changed Value, Unchanged Value, Increased By, and Decreased By. Exact Value uses the Value Type equality contract. Changed Value and Unchanged Value compare the fixed-width bytes retained by the previous scan, which makes snapshot change detection representation-stable for floating-point NaN payloads. Ordered and delta scans use the optional `IMemoryValueComparer` companion contract introduced in Plugin API `2.6.0`. Plugin API `2.7.0` adds `INativeScanTypeMappingProvider` plus `NativeScanTypeMapping`: a plugin may declare that a Core Scan Type is semantically equivalent to a native operation for selected First/Next stages and Value Types. Core uses that mapping as the native-delegation gate and otherwise routes directly to shared scanning. A mapping's native id is opaque to Core/WPF, so a future PC or Xbox 360 plugin can use its own compare enum/opcode names without changing the host.

Plugin API `2.8.0` extends only the presentation/applicability side of plugin-owned Scan Options. `IMemoryScanOptionPresentation` can request a generic choice-list or toggle editor and map toggle checked/unchecked states to existing stable choice ids. `IMemoryScanOptionApplicability` can restrict an option to the Core Scan Types/stages where it is meaningful. Plugin API `2.9.0` adds the optional `INativeValueScanResidentResultSet` contract for complete backend-resident result sets with authoritative counts and bounded range reads. The host validates and consumes these contracts generically; plugin ids, platform names, backend-specific option names, and resident-storage implementations are not hardcoded into WPF.

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
Disassembly
Debugger
ThreadEnumeration
ThreadControl
RegisterAccess
Breakpoints
Watchpoints
CallStack
StepExecution
```

Mock also advertises `Debugger`, `ThreadEnumeration`, `ThreadControl`, `RegisterAccess`, `Breakpoints`, `Watchpoints`, `CallStack`, and `StepExecution` because its attached debugger session supplies working neutral thread, register, breakpoint/watchpoint, call-stack, and native Step Into services. Assembly/instruction editing, pointer scanning, and other unimplemented capabilities remain unadvertised until their implementations are complete and verified. `Disassembly` remains advertised only by plugins that expose a working `IDisassemblerProvider`. The host requires the appropriate capability and runtime service rather than inferring functionality from a platform name.

## Settings and Scan Result Storage Foundation

Application-level settings are available from the **Settings** button in the top application bar. The settings window exposes independent **Live value refresh** and **Frozen write** intervals plus **Scan Results Storage Location**, and reuses the same persisted settings document that stores the selected color theme:

```text
%LocalAppData%\TeeKay87\MemoryEngine\settings.json
```

The same document also contains plugin-scoped values under a `plugins` object. Plugins do not read or write this file directly. Plugin API `2.3.0` introduced an optional `IPluginSettings` scope that Core binds to the plugin's stable id, so each plugin can read/save its own stable keys without knowing the persistence format or the namespaces of other plugins. Application-level saves preserve the plugin section and plugin writes preserve the existing theme, Saved Addresses refresh interval, Frozen write interval, and scan-storage settings.

Live values and Frozen writes use two independent application-level intervals. **Live value refresh** controls how often all Saved Addresses and the currently realized Scan Results rows are reread and defaults to `500 ms`; **Frozen write** controls how often captured Frozen bytes are reapplied and defaults to `100 ms`, matching Cheat Engine's current default freeze cadence. Both accept `50-10,000 ms`, persist between application launches, and are applied to already-loaded plugin workspaces immediately after Settings is saved. Their Settings text boxes accept decimal digits only while editing; Save still performs the authoritative 50-10,000 range check. Live value refresh pauses during First/Next Scan. Frozen writes continue during a scan when **Pause target while scanning** is Off; when Pause is On, the process is suspended and Frozen writes are suppressed until the scan finishes and the target resumes. A transient Frozen write failure does not silently unfreeze the row; the next configured Frozen interval retries it.

Saved Address edits initiated by the user are treated separately from disposable timer work. While the Value cell owns keyboard focus, background refresh/Frozen completions may update internal current bytes but do not overwrite the text being edited. Freeze enable, Address changes, and Type changes suppress future timer ticks and wait for an already-running Saved Address background cycle instead of rejecting the edit. Direct Value commits use the same user-operation boundary but still prefer a plugin-provided `IConcurrentMemoryWriter` when available; without one, the host waits for background Saved Address I/O before using the primary writer. A successful direct write advances the same per-row target-write generation used by Frozen writes so an older in-flight refresh cannot overwrite the newly written display value. Freeze requests also carry a per-row request generation so a later explicit **Off** request remains authoritative if an earlier freeze-enable sequence is still finishing.

The default scan-results root is:

```text
%LocalAppData%\TeeKay87\MemoryEngine\ScanResults
```

The selected directory is validated by creating, flushing, and removing a small probe file before the setting is accepted. A changed location is persisted immediately but becomes the active managed root on the next application launch; an in-use application/scan session is never migrated between roots. If the configured root cannot be initialized at startup, the host reports the storage error and logs it instead of silently switching to an unrelated fallback path. The configured root stores the active application/scan-session metadata and every complete candidate generation that has been materialized into the shared disk-backed path. A large authoritative native result set may instead remain resident in its plugin/backend while the scan session is active; in that state the host keeps only the bounded WPF preview locally and does not create a complete binary result generation until a Core fallback or another host-side consumer requires materialization.

Each application launch creates a new GUID-scoped managed directory. Each First Scan creates a fresh scan-session GUID beneath it. Compatible Next Scan operations keep the same scan-session identity, while New Scan, target replacement, plugin reload, or ViewModel disposal invalidates the current scan-session identity before best-effort deletion. A simplified current layout is:

```text
<Scan Results Root>\
    .tk87me-scan-storage.json
    <application-session-guid>\
        session.json
        <scan-session-guid>\
            scan.json
```

The metadata is versioned and records ownership, application/scan identity, lifecycle state, plugin id, target process id, Value Type id, Scan Type id, record-format version, timestamps, committed result count, active result-file generation, stored value width, and alignment. Only a session explicitly marked `Committed` with a valid referenced result file can be opened as the active disk-backed result set. Creating/Writing/Failed/Cancelled/Invalidated sessions are never reusable. Each result generation is written to a temporary same-session file, flushed and validated, then published by updating metadata to reference the new generation; a failed or cancelled replacement therefore cannot displace the last valid committed generation.

Correctness does not depend on successful deletion. Startup cleanup only considers GUID directories containing matching Memory Engine ownership metadata; unrelated folders are ignored. If antivirus, indexing, permissions, locks, or another filesystem problem prevents deletion, the stale directory remains physically present but cannot be adopted by the new application session because the active application/scan GUIDs are explicit. Normal shutdown similarly attempts to remove only the current managed application-session directory and does not block shutdown indefinitely on cleanup failure.

The host includes a reusable modal operation-progress service for blocking operations. Its Core `OperationProgress` model carries optional status, optional 0–1 fraction, and optional detail text; a null fraction selects indeterminate mode. The WPF dialog supports optional cancellation and remains modal until the underlying operation completes, cancels, or fails. Rev13 introduced this component for large native transfers into the disk-backed store. Rev30 keeps it as the on-demand **Materializing Scan Results** surface: a large authoritative resident result set no longer opens the dialog merely to populate the WPF preview, but the same generic modal is used if the complete resident set must later be transferred into local storage for a Core fallback. The dialog itself remains scan-agnostic.

The host also includes a reusable confirmation-dialog service for binary user decisions. Callers supply the title, message, button text, semantic tone, and whether the affirmative action is the Enter-key default; the WPF dialog owns modality, owner centering, keyboard behavior, and theme-aware presentation. Rev23 uses this component for **Remove All Saved Addresses**, with a Danger tone and no destructive Enter default. See [`docs/ui/CONFIRMATION_DIALOG.md`](docs/ui/CONFIRMATION_DIALOG.md).

Memory-oriented TextBoxes use a layered validation model. Rev24 adds reusable live filtering for typing and paste while preserving the existing final parsers as the authoritative range/semantic checks. Standard Value Types expose type-aware editing syntax through optional `IMemoryValueInputPolicy`; custom plugin Value Types may omit the policy and continue to rely only on `TryParse(...)`. Saved Address Address uses the host hexadecimal-address filter, while the two Saved Addresses interval settings use a digits-only host filter. Rev25 also blocks the WPF Space-key path for numeric/Address editors while preserving valid Array of Bytes separators. See [`docs/ui/TEXT_INPUT_VALIDATION.md`](docs/ui/TEXT_INPUT_VALIDATION.md).

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

The upper target strip keeps the active platform and process visible while the user works. The Platform dropdown displays only the active plugin's declared `Name`; backend/transport information remains available in Plugin details instead of being appended to the selector label. The strip reuses the already implemented plugin-defined connection fields and process commands.

For a process-capable plugin, the following concepts remain separate:

- **Process list** — every process currently returned by the plugin;
- **Selected process** — the process currently selected in the process picker;
- **Active Target** — the process explicitly chosen for future memory operations.

Changing the selected row does not silently redirect memory operations. A process becomes the Active Target only when explicitly selected as such. When the active plugin supports memory-region enumeration, setting an Active Target immediately requests that process's current memory map. The neutral region list is retained by the host and its loaded region count is shown in the target status area. Refreshing the process list also refreshes the memory map when the same Active Target remains available. The underlying raw read/write diagnostics remain implemented for development and regression work, but their inspector controls are not currently shown in the main workspace. For map-capable targets, reads must fit inside readable regions and writes must fit inside writable regions before the host invokes the plugin. When the write range is also readable and an `IMemoryReader` is available, the host captures the original bytes, performs the write, reads the range back, and compares the returned bytes with the requested data. The diagnostic **Safe Write Test** removes the need to manually discover an address for protocol verification: it selects only regions marked Read + Write while excluding Execute and Guard, samples a four-byte interior address repeatedly, aborts if the bytes are not stable, then writes the current bytes back unchanged and performs immediate read-back verification.

### Disassembler workspace

Application `0.1.6.rev14` is the fully tested and hardware-verified final implementation of the architecture-neutral Disassembler block. The **Disassembler...** button appears in the target-process strip only when the selected plugin advertises `TargetCapabilities.Disassembly` and becomes usable only when the current Active Target has a loaded readable memory map and the host can reserve the target for a foreground read. Opening the window chooses a generic initial region by preferring readable executable mappings, then named/module mappings, then lower base addresses; no PS5 process name or module name is hardcoded.

The workspace decodes around the entered hexadecimal origin instead of showing only bytes after it. The default Core context is up to **512 bytes before** the origin plus **512 bytes starting at** the origin, with both sides independently clamped to the same readable, non-guarded region. Core performs one bounded memory read and sends the complete returned range through one continuous provider decode. If the requested address falls inside a multi-byte instruction, the instruction can begin before the origin and continue across it, while the requested address itself remains unchanged as the presentation origin.

The visible table remains exactly three columns — **Address**, **Bytes**, and **Instruction** — with row/column virtualization. Neutral `DisassemblyTextToken` metadata provides theme-aware syntax colors for mnemonic, flow-control mnemonic, register, number, and keyword segments without teaching WPF architecture-specific register/opcode names. Invalid rows keep the error presentation. Custom themes that predate the syntax-token keys remain compatible through semantic fallback colors.

Selection is **Extended**, so Ctrl/Shift can select multiple instruction rows without changing origin geometry. Right-clicking any row that already belongs to the current multi-selection preserves the complete selected set even when that row is not the primary `SelectedItem`; right-clicking an unselected row intentionally collapses the old selection to the clicked row. The row context menu contains **Follow Target**, **Copy Address**, **Copy Bytes**, **Copy Instruction**, **Copy Address + Instruction**, **Copy Selected**, and **Export...**. Every copy command consumes the complete current selection in displayed order: Copy Address emits one address per row, Copy Bytes emits one byte string per row, Copy Instruction emits one decoded instruction per row, Copy Address + Instruction emits one combined line per row, and Copy Selected emits the full Address/Bytes/Instruction form. If right-clicking first collapsed the selection to an unselected row, these commands naturally operate on that one row. `Ctrl+C` uses Copy Selected. Row selection/copy causes no target traffic.

Disassembler export reuses the same universal export pipeline already used by Scan Results and Saved Addresses. **Displayed Instructions** exports every instruction materialized in the current bounded view; **Selected Instructions** is offered when one or more rows are selected. JSON, CSV, TSV, and Markdown table are supported with column selection. The structured export source exposes Address, Bytes, Instruction, Mnemonic, Operands, Length, Flow Control, Branch Target, Valid, Region / Module, Protection, and Module Relative, so JSON is not limited to the rendered instruction string. Export works from a stable presentation snapshot and performs no additional memory read while writing; cancellation/failure retains the universal export engine's transactional destination-publication guarantee.

**Back** and **Forward** implement successful-address navigation history and support Alt+Left / Alt+Right. The initial successful address is recorded once; successful Go To, Follow Target, Previous Region, Region Start, Region End, and Next Region navigation enter the same history. Navigating after going Back discards the abandoned Forward branch. Refresh re-reads the current origin without creating a history entry, and failed reads leave the prior view/history position intact.

Region navigation reuses Core's existing `MemoryViewerRegionNavigator`. **Previous Region** and **Next Region** move only between readable non-guarded regions, while **Region Start** and **Region End** navigate to the current region's first and final byte. When the loaded map supplies a real module name, the host resolves the module's lowest mapped base and displays the origin as `<module> + 0x<offset>`; anonymous regions never receive a fabricated module identity.

Scan Results and Saved Addresses provide **Open in Disassembler** alongside **Open in Memory Viewer**, and Memory Viewer exposes **Open in Disassembler** from its context menu. These routes preserve the captured plugin/process/connection generation, so a stale workspace cannot silently open an old address against a new target session.

Valid direct `Call`, `Jump`, and `ConditionalJump` rows with a provider-supplied `BranchTarget` can be followed through the context menu, double-click, or Enter. Direct target navigation never parses rendered operand text. Indirect flow such as `call [rax]` or `jmp rbx` remains non-followable until future debugger/register context can provide a real dynamic destination.

Variable-length architectures still cannot prove that the first byte of an arbitrary raw context window is the program's canonical instruction boundary. The requested origin itself is resolved against the decoded instruction span, preserving the live-verified mid-instruction behavior from rev5. The former long explanatory paragraph about this limitation was removed from the Disassembler workspace in `0.1.7.rev15`; the decode behavior itself is unchanged.

### Debugger workspace

Application `0.1.7.rev2` introduced the first modeless Debugger workspace on top of the verified rev1 Plugin SDK/Core foundation, rev3 connected the same neutral workspace to the real PS5 backend, rev5 completed live hardware verification of that transport, rev6 added thread services, rev8 completed Registers and Stop Context acceptance, rev10 completed the guarded PS5 extended-register transport, rev15 completed the software-breakpoint baseline, and rev16 completed Hardware Watchpoints at **118/118 PASS** plus the focused Mock/live-PS5 acceptance cycle. Rev17 added Call Stack/Call Frames and stepping without changing the public Plugin API and passed 124/124 automated checks. Rev21 passed 130/130 and runtime confirmed the coordinated MainWindow/tool shutdown path. Rev18-rev23 attempted to present Breakpoints / Watchpoints and Call Stack with themed WPF tabs, but repeated Windows runtime checks still exposed clipped or otherwise unstable header rendering. Current rev24 removes that TabControl/TabItem chrome entirely and replaces it with two compact application-styled selector buttons. The selected button receives the normal theme accent outline, only its corresponding upper-right workspace is visible, duplicate inner title/count rows are removed, and view-specific actions share the footer row beneath the data area. The **Debugger...** button appears only when the selected plugin advertises `TargetCapabilities.Debugger`; opening a window requires a connected current Active Target and captures that process plus the current connection generation. Opening the window never attaches automatically.

The workspace provides explicit **Attach**, **Pause**, **Continue**, and **Detach** controls on the first execution row. When `StepExecution` is available, a second row exposes **Step Into**, **Step Over**, **Step Out**, and **Run to...**. Events now owns its own lower-right header with the event count and **Clear Events** action. Lifecycle/control calls are routed through `DebuggerSessionCoordinator`, not directly to a platform backend. The event table is read-only and virtualized and shows sequence, time, event kind, execution state, stop reason, thread id, instruction pointer, and neutral message. The displayed history is bounded to the newest 2,000 events.

When the plugin advertises `TargetCapabilities.ThreadEnumeration`, rev6 adds a **Threads** pane with a neutral hexadecimal Thread id, optional Name, and neutral State. **Refresh** calls `IDebuggerThreadService`. If the plugin also advertises `ThreadControl`, **Suspend** and **Resume** are shown and call `IDebuggerThreadControlService`; those per-thread actions are enabled only while the overall debugger target is Running. Selection is preserved by thread id across refreshes, the list refreshes automatically after Attach/Pause/Continue, and stale-target/Detach cleanup clears the rows. WPF contains no PS5 LWP, Windows handle, or architecture-specific thread logic.

Mock plugin `1.0.1.rev17` exposes the deterministic Main/Worker/Render thread fixture, writable paused-thread general/control registers, read-only wide extended-register fixtures, deterministic software execute breakpoints/hardware watchpoints, a three-frame paused call stack, and native Step Into. PS5 plugin `0.1.2.rev39` retains the hardware-verified dedicated command/TCP 755 event transport, thread enumeration, whole-target controls, guarded register transport, software execute breakpoints, hardware data watchpoints, server-side call-stack walking, and native selected-thread Step Into. Before PS5 detach/disposal, any software-breakpoint slots that are still backend-active or staged for paused cleanup are explicitly restored, and any hardware-watchpoint slots that are backend-active or staged for cleanup are explicitly disabled, before the backend detach request. Its paused register surface keeps the verified general snapshot and can add guarded FPU/SIMD plus FS/GS-base groups through bounded disposable probes. Paused `GETDBREGS` is intentionally suppressed because the current upstream handler can block after the target is already stopped; see [`docs/bug-reports/ps5debug-ng-getdbregs-can-hang-after-pause.md`](docs/bug-reports/ps5debug-ng-getdbregs-can-hang-after-pause.md). Individual PS5 thread Suspend remains client-implemented but backend-blocked on the tested payload, which returns `CMD_ERROR`; see [`docs/bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md`](docs/bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md). Current ps5debug-NG does not return a per-thread run-state field with list/info records, so ordinary Running/Stopped projection remains plugin-private.

Every Debugger window is permanently bound to the plugin/process/connection generation captured when it was opened. The host owns all coordinators created for the current target session and disposes them before Disconnect, target-session replacement, Active Target changes, or application/plugin shutdown. An old window therefore becomes stale instead of silently following a newly connected process. While Paused, `CallStack` loads neutral frames for the selected thread and shows index, instruction address, module, symbol, SP, FP, and return address; frame navigation reuses the existing Disassembler and Memory Viewer. Step Into uses `IDebuggerStepService`. Step Over remains host-composed from neutral disassembly: ordinary paused contexts use the current live Disassembler result, while a matching logical Software/Execute breakpoint stop first uses the original instruction captured before backend breakpoint installation. Non-call instructions use native Step Into; calls use a temporary software breakpoint at the original instruction's fall-through address. Step Out and Run to Address use the same temporary-breakpoint/Continue path. The host now retains the exact target breakpoint id and ownership for a composed operation until a Paused event arrives. A stop on a different neutral event interrupts the operation; only an operation-owned temporary breakpoint is then removed, and cleanup is deferred safely if Continue/Pause is still completing. Call-frame/register state, logical breakpoint state, original-instruction snapshots, and composed-operation state are cleared with the debugger/target lifetime. Debugger export and final integration/finalization remain after rev28 acceptance of the unchanged rev27 cleanup implementation.

### Scan Results

The large upper-left table displays temporary scan candidates normalized by shared Core. First Scan searches readable non-guard regions using the alignment and width of the selected Value Type. Next Scan refines only existing candidates, preserves the prior comparison value in the **Previous** column, and retains addresses matching the selected Core Next Scan predicate. Results are not automatically added to the persistent address table.

Result addresses are rendered in compact hexadecimal form, for example `0x10000104` rather than a fixed-width `0x0000000010000104`. Double-clicking a result row immediately adds that one candidate to Saved Addresses. Right-clicking a result row provides **Save Address**, **Open in Memory Viewer**, capability-driven **Open in Disassembler**, **Copy address**, and **Copy value**. When multiple rows are selected and the context-clicked row belongs to that selection, **Save Address** processes the complete selected set and skips identities that are already present in Saved Addresses; right-clicking outside the existing multi-selection does not bulk-save the unrelated old selection. **Open in Memory Viewer** opens the Memory Viewer at that result address and carries the result's current byte size so the complete known value span is highlighted in Hex Bytes and ASCII. **Open in Disassembler** is enabled only when the current Active Target can provide neutral Disassembly and opens the same result address as the Disassembler origin. The viewer remains presentation-only until the user explicitly chooses **Edit...** / **Edit Hex Bytes...** on a writable row; it never changes page protection automatically. The former diagnostic **Change value** action is no longer exposed because its Raw Memory Write surface is intentionally hidden; the underlying diagnostic code remains in the application for development and regression work. Saved Addresses provides the user-facing path for editing and writing retained values.

The complete candidate set is retained outside the WPF presentation layer. Shared/Core scans use the active disk-backed scan-result generation. A native provider implementing `INativeValueScanResidentResultSet` may instead retain a sufficiently large authoritative set in its backend; the PS5 plugin uses this to keep large TurboScan survivors on the console and reads only the first 50,000 candidates required by the current presentation cap. Compatible native Next Scans refine that complete resident set directly. If a selected predicate requires Core fallback, the host materializes the resident set into the existing disk-backed format at that point and shows a cancellable **Materializing Scan Results** progress dialog. The DataGrid still materializes at most the first 50,000 candidates, while the header/status tracks the true 64-bit total count. Next Scan therefore includes candidates that were never shown in the DataGrid. The 2,000,000 safety limit remains only on legacy in-memory/list fallback APIs.

The Scan Results **Export...** action uses the shared export pipeline. **All Results** exports the complete authoritative candidate set rather than the WPF preview, so disk-backed and backend-resident scans can be streamed in bounded batches even when millions of rows are not displayed. **Displayed Results** exports the currently materialized presentation rows and **Selected Results** exports the current selection. JSON, CSV, TSV, and Markdown table are supported with column selection. Every Scan Result also displays the containing memory region's **Protection** flags, such as `Read`, `Read, Write`, or `Read, Execute`. Complete stored/resident exports guarantee Address, Value, Type, and Protection: Address/current bytes still come from the complete-set storage/backend, while Protection is resolved against the loaded Active Target memory map during export. The verified disk format is therefore unchanged. Materialized Displayed/Selected rows can additionally expose Previous and Region / Module.

### Scan controls

The full-height right-side scanner panel now provides the first usable memory-search workflow:

```text
Value(s): depends on selected Core Scan Type; multi-value operands share one row
Scan Type: Exact / Fuzzy / Bigger / Smaller / Between / Unknown / Changed / delta modes
Value Type: UInt8 / Int8 / UInt16 / Int16 / UInt32 / Int32 / UInt64 / Int64 / Float / Double / Array of Bytes
Alignment: Default / 1 / 2 / 4 / 8 / 16 / 32 / 64 / 128 Bytes
Floating-point rounding: Strict / ps5debug-NG tolerance (1e-6) [Exact Float/Double only]
[x] Little-endian byte order
[ ] Pause target while scanning
First Scan
Next Scan
New Scan
Cancel Scan
```

The **Value Type** selector remains plugin-driven, while **Scan Type** is now Core-owned. `ITargetPlugin.SupportedValueTypes` supplies the representations the target can read/write; `MemoryScanTypeCatalog` supplies the common comparison modes for every plugin. The Scan Type list is filtered by First/Next Scan stage and by the selected Value Type's supported comparison contracts. Numeric standard types therefore expose all current Core comparison modes, while Array of Bytes exposes operand-based Exact Value only in the current UI because its variable width cannot be inferred for operand-free snapshot predicates. **4 Bytes** (signed Int32) remains the built-in default Value Type for PS5 and Mock. Scan Types with no user operand hide the Value field; `Between` renders **Value 1** and **Value 2 side-by-side**; one-operand predicates render one full-width Value field. Value Type remains locked after First Scan until New Scan resets the session, while Scan Type is intentionally re-enabled after every completed scan so the next refinement predicate can be changed without starting over. **New Scan always restores Exact Value**, Core's declared default Scan Type, and republishes the selection after the filtered First Scan list is rebuilt so the selector cannot remain visually blank after an `ItemsSource` transition.

Cancel Scan is cooperative. Core requests cancellation immediately, while a platform transport may defer the cancellation boundary until its current framed scan/read transaction has been completely consumed. This prevents a protocol such as ps5debug-NG from leaving unread response bytes in the shared TCP stream.

For PS5 scanning, plugin `0.1.0.rev34` retains the verified TurboScan implementation and probes ps5debug-NG TurboScan capabilities and exposes the legacy/API 2.2 streaming services plus API 2.9 resident-result handles only when the required runtime engines are available. Plugin API `2.7.0` mapping metadata tells Core exactly which predicates may be delegated. Exact, Fuzzy, Bigger/Smaller, Between, Increased/Decreased, Changed/Unchanged, integer Unknown Initial Low, and snapshot-based Unknown Initial Value can use native paths when their current Value Type/options are compatible. For Float/Double Changed/Unchanged, the PS5 plugin deliberately transmits the same-width unsigned integer Value Type on TurboScan COUNT so ps5debug-NG performs raw four-/eight-byte equality instead of IEEE-754 NaN equality; GET results are still decoded and displayed as the selected Float/Double type. Unknown Initial uses `TS_SNAPSHOT | TS_SNAPSHOT_INCLUDE_ZEROS` so Core's include-zero semantics are preserved. Its progress records are consumed until ps5debug-NG's sentinel with no arbitrary host record-count cap; the payload may legitimately emit more than 1,024 records when a large snapshot uses smaller fallback I/O windows. If the server reports `snapshot_ok == 0`, the complete response is consumed first and the host then uses the shared Core fallback on the same synchronized connection. Increased By/Decreased By and floating Unknown Initial Low intentionally use Core fallback because upstream semantics differ. Strict Float/Double Exact Value still applies host-side exact filtering on First Scan and shared refinement on Next Scan; the explicit ps5debug-NG `1e-6` tolerance option keeps the tolerant Exact path native. Big-endian numeric comparisons that require native magnitude interpretation fall back to Core, while endian-independent raw equality/inequality and snapshots may remain native. Core validates native batches and commits them to the same disk-backed result generations used by the shared scanner.

When the plugin exposes both process-suspend and process-resume capabilities, the Scan panel also shows **Pause target while scanning**. It is Off by default. When enabled, the host suspends the Active Target before First/Next Scan and resumes it in a `finally` path after success, cancellation, or ordinary failure.

The PS5 plugin also exposes three scan options through generic Plugin SDK metadata. **Little-endian byte order** is a checkbox: checked selects Little Endian (the default/native PS5 byte order), while unchecked selects Big Endian. The option is hidden for one-byte/AOB Value Types where byte order has no meaning and, like Alignment, locks after First Scan because changing the candidate interpretation/shape would invalidate the session. **Alignment** remains a choice list and defaults to the selected Value Type's natural alignment; explicit 1–128 byte steps are supported and are passed directly in ps5debug-NG TurboScan START's `u8 alignment` field. **Floating-point rounding** is shown only when the selected Value Type is Float/Double **and** the selected Core Scan Type is Exact Value. It defaults to Strict, can switch to `ps5debug-NG tolerance (1e-6)`, and remains configurable between scans because it changes only the current Exact Value comparison rather than the retained candidate address shape. **New Scan** unlocks Endianness and Alignment.

### Saved Addresses

Saved Addresses is the functional user-address workspace below Scan Results. A displayed Scan Result can be saved either by double-clicking its row or by using **Save Address** in the Scan Result context menu. The toolbar also provides **Add Manually**, allowing a new row to be created directly from a hexadecimal Address, optional Description, plugin-declared Value Type, and variable byte Length where that type requires one. Manual rows are attached to the current Active Target and are refreshed immediately after creation; from then on they use the same Saved Address read/write/freeze/export/viewer workflows as scan-derived rows. Saving or manually adding the same target/address/Value Type combination again selects the existing row instead of silently duplicating it. Saved rows are independent of the temporary scan session: First Scan, Next Scan, and New Scan do not remove them. Rev17 keeps the list in the current application/plugin workspace only; project-file or cross-launch address persistence is a later feature.

Each row exposes five directly interactive columns plus a read-only Protection column:

- **Frozen** — clicking the checkbox captures the current live value and reapplies its bytes at the configured Saved Addresses interval. Unchecking stops repeated writes. A disconnect automatically disables active freezes so they cannot silently resume against a later connection.
- **Description** — click and edit the user-facing label directly.
- **Address** — click and edit a hexadecimal address. The editor blocks non-hexadecimal typing/paste, supports the existing optional `0x` prefix, and limits the live hexadecimal payload to the target-neutral 64-bit address width. Commit still runs the authoritative hexadecimal `ulong` parser; invalid/incomplete input restores the previous address. Changing an address disables freeze before the new location is used.
- **Type** — choose from the Value Types declared by that plugin. The row uses the plugin definition for width, parsing, display formatting, and endianness-aware value conversion; there is no Saved Address-specific Core catalog. Changing type disables freeze and refreshes the new representation.
- **Value** — displays the current target value and accepts direct edits. When the selected plugin Value Type implements the optional `IMemoryValueInputPolicy`, typing and paste are filtered to syntax that can still become valid while allowing useful incomplete edit states such as `-`, `0x`, or `1e-`. The Value Type's existing `TryParse` remains the authoritative commit/range check before any neutral `IMemoryWriter` call; if the row is frozen, the successful edit also becomes the new frozen value.
- **Protection** — read-only memory-map protection for the row's complete value range. It is refreshed from the cached Active Target memory map when the target/map, address, or Value Type changes and is blank when the saved row is inactive or no containing map region is available.

Saved Addresses and currently realized Scan Results rows refresh sequentially through neutral `IMemoryReader` at the Settings-controlled live-value interval. Scan Result virtualization is used as the refresh boundary, so the application does not periodically reread all 50,000 presentation rows; the stored/current scan baseline remains unchanged and only the displayed `Value` property is refreshed. The live-value scheduler is stopped for the entire duration of First Scan and Next Scan, including target pause/resume handling, and resumes only after the scan operation returns to the idle state. Automatic refresh/freeze work also yields to process/memory-map operations and the existing diagnostic read/write tools so independent protocol operations are not intentionally overlapped. Each Saved Address is bound to the plugin workspace and the process id/name from which it was saved. When another process is Active Target, the row remains visible but is inactive and automatic writes are not sent to that target.

Right-clicking a Saved Address provides **Freeze/Unfreeze**, **Open in Memory Viewer**, capability-driven **Open in Disassembler**, **Copy address**, **Copy value**, and **Remove address**. **Open in Memory Viewer** opens the Memory Viewer at that Saved Address while preserving the row's saved target identity and current `ValueSize`, which becomes the viewer's highlighted byte span. **Open in Disassembler** uses the same saved target identity and is enabled only when that target is still the current Active Target and the plugin can provide Disassembly, preventing a retained row from being silently interpreted against another process. A row inside an already readable/writable/non-guarded region can be edited through the viewer's explicit Write & Verify dialog; the operation is blocked if the displayed bytes became stale before the write. `0.1.5.rev7` suppresses the row-level Saved Address tooltip when its status text is empty, so hovering unused cell space no longer produces an empty hint box; the explicit Frozen checkbox and Remove button tooltips remain unchanged. Every row also has its own **Remove** button. The toolbar is now **Add Manually / Remove All / Export**. Add Manually is enabled only when memory read, plugin Value Types, and a safe current Active Target are available; Remove All keeps the reusable themed confirmation dialog and safe Cancel default; Export writes all or selected Saved Addresses as JSON, CSV, TSV, or a Markdown table with selectable columns including Protection. The manual-entry dialog keeps future CE-style hexadecimal display, Binary start-bit, Text Unicode/code-page, and Pointer/base/offset controls visibly disabled until neutral backing models exist. Saved-address exports use a stable host snapshot and do not require target I/O; the action is briefly disabled while a direct Address/Type/Value commit is still completing so the snapshot cannot capture the pre-commit value. Pointer resolution, persistent project-owned bookmarks, and project/cheat integration remain later extensions.

The horizontal splitter between Scan Results and Saved Addresses is resizable, and a second vertical splitter between the left-side lists and the full-height Scan panel allows the scanner width to be adjusted independently. Both splitters use centered 4-pixel visual handles inside 14-pixel interactive tracks with equal breathing room on each side. The left-side tables now start at an even 50/50 height split and the horizontal divider is proportionally limited to a 20/80–80/20 range, so the usable resize range scales naturally with both windowed and fullscreen heights. The Scan panel keeps its established 280–420 pixel width range.

Detailed Saved Addresses behavior is documented in [`docs/architecture/SAVED_ADDRESSES.md`](docs/architecture/SAVED_ADDRESSES.md).

The Memory Viewer architecture, safe editing rules, and planned `0.1.5` progression are documented in [`docs/architecture/MEMORY_VIEWER.md`](docs/architecture/MEMORY_VIEWER.md).

Detailed layout rules are documented in [`docs/ui/MAIN_WORKSPACE.md`](docs/ui/MAIN_WORKSPACE.md).

The external scanner survey is documented in [`docs/research/MEMORY_EDITOR_SCAN_TYPE_SURVEY.md`](docs/research/MEMORY_EDITOR_SCAN_TYPE_SURVEY.md). It is retained as design reference for plugin authors rather than as a mandatory Core registry.

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

The schema, palette keys, loading rules, and instructions for adding another color theme are documented in [`docs/ui/THEMES.md`](docs/ui/THEMES.md). Application-owned confirmation and operation-progress dialogs consume the same shared theme resources; they do not define their own Light/Dimmed/Dark palettes.

## Shared WPF Styling

Reusable WPF presentation belongs in shared resource dictionaries instead of individual views.

Current style resources are located under:

```text
src/TeeKay87.MemoryEngine.App/Resources/Styles/
├── ButtonStyles.xaml
└── ControlStyles.xaml
```

`ButtonStyles.xaml` provides reusable **Primary**, **Secondary**, and **Danger** button semantics on top of one shared control template. Their normal background/border/text colors are supplied independently by the active theme, while hover, pressed, keyboard-focus, defaulted, and disabled states remain owned centrally. Disabled controls use deliberately dimmed text and lower-contrast surfaces so unavailable actions are visibly distinct in every bundled theme. For buttons, the shared control template directly enforces the disabled background, border, and text brushes on the rendered template parts, so Primary, Secondary, Danger, command-driven, and workflow-local derived button styles cannot accidentally retain enabled-state colors while `IsEnabled` is false.

`ControlStyles.xaml` provides shared theme-aware presentation for common controls used by the main workspace, including text, text boxes, combo boxes, check boxes, data grids, cards, tooltips/hints, and related states. Disabled ComboBoxes dim their text and arrow together, disabled CheckBoxes are reduced in emphasis, and disabled context-menu items dim the complete item instead of looking selectable. Tooltips use an application-owned template so their foreground/background no longer fall back to Windows theme colors. Context menus, menu items, and separators likewise use application-owned templates, including removal of the default Windows icon/checkmark gutter.

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
│   │   ├── Dialogs/
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
│   │   ├── Models/
│   │   └── Scanning/  (optional reusable standard scan definitions)
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
│   ├── development/
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

The current long-term development plan is maintained in [`docs/TeeKay87_Memory_Engine_Full_Development_Action_Plan.md`](docs/TeeKay87_Memory_Engine_Full_Development_Action_Plan.md). The implemented plugin boundary is documented in [`docs/architecture/PLUGIN_SDK_FOUNDATION.md`](docs/architecture/PLUGIN_SDK_FOUNDATION.md), debugger ownership is documented in [`docs/architecture/DEBUGGER_ARCHITECTURE.md`](docs/architecture/DEBUGGER_ARCHITECTURE.md), and host-managed plugin persistence is documented in [`docs/architecture/PLUGIN_SETTINGS_PERSISTENCE.md`](docs/architecture/PLUGIN_SETTINGS_PERSISTENCE.md). The superseded early architecture notes remain in `docs/architecture/EARLY_DEVELOPMENT_ARCHITECTURE_(obsolete).md` only as historical context.

## Plugin Versioning and Compatibility

The project keeps three version domains separate:

1. **Application version** — TeeKay87's Memory Engine, currently `0.1.7.rev31`;
2. **Plugin version** — each plugin has its own semantic version and revision;
3. **Plugin API version** — compatibility version for the public host/plugin contract, currently `2.16.0`.

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
- concrete Value Type definitions supported by that plugin;
- optional per-Value-Type live-input policy through `IMemoryValueInputPolicy` when the host can safely reject impossible typing/paste before final parsing;
- concrete Value Type definitions and any optional comparison support required by the Core Scan Types;
- optional concrete Scan Option definitions, including choices, defaults, value-type applicability, and whether the option locks after First Scan;
- connection-field definitions required by that plugin.

Connected targets expose implemented operations through `ITargetSession.GetService<TService>()`.

The current neutral service contracts include:

- `IProcessProvider`;
- `IForegroundProcessProvider`;
- `IMemoryMapProvider`;
- `IMemoryReader`;
- `IMemoryWriter`;
- optional `IConcurrentMemoryWriter` for a transport that can safely write while the session's primary transport is busy;
- `INativeValueScanner` and `INativeValueScanRefiner`;
- `INativeValueScanStreamProvider` and `INativeValueScanStreamRefiner` for disk-backed/streamed native result production;
- optional `INativeScanTypeMappingProvider` for semantic Core-to-native Scan Type mapping;
- `IProcessControl`;
- `IDisassemblerProvider`;
- `IDebuggerProvider`, whose attached `IDebuggerSession` may expose optional thread/register/breakpoint/call-stack/step services when the matching capability is implemented.

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

These fixtures support scanner, Saved Addresses, freeze, export, Memory Viewer, disassembly, debugger, and regression testing. The established health/ammo/money addresses remain unchanged. Mock plugin `1.0.0.rev15` keeps the deterministic synthetic instruction fixture at `0x10000400`, uses that location for its debugger pause/resume event flow, exposes Main/Worker/Render thread rows, provides both writable general/control register rows and read-only wide extended-register fixtures while Paused, and advertises deterministic persistent/temporary software execute breakpoints plus deterministic hardware data watchpoints for Breakpoints / Watchpoints acceptance. Rev17 additionally exposes deterministic three-frame call stacks and native Step Into for the selected paused thread.

Mock-specific documentation is kept only under [`docs/plugins/Mock/`](docs/plugins/Mock/).

## PlayStation 5 Plugin

`TeeKay87.MemoryEngine.Platform.PS5` communicates with a PlayStation 5 running ps5debug-NG over its TCP command channel.

The plugin defines its own connection settings. The current defaults are:

| Field | Required | Default |
| --- | --- | --- |
| PS5 IP address or host name | Yes | none |
| Port | Yes | `744` |

The WPF host renders these settings from Plugin SDK metadata; it does not contain PS5-specific IP or port logic. PS5 plugin `0.1.2.rev39` also consumes its Core-managed `IPluginSettings` scope. After a connection succeeds, the plugin stores the normalized host and port as `connection.host` and `connection.port`. On later discovery those remembered values become the connection-field defaults. Failed connection attempts and unfinished textbox edits do not replace the last successful values.

A connection is accepted only after the plugin has opened the command channel, read and validated ps5debug-NG identification information, read the target firmware, and completed a process NOP/liveness request.

After connection, process enumeration is performed through the same neutral `IProcessProvider` contract used by the host and mock target. The PS5 plugin parses ps5debug-NG `CMD_PROC_LIST` records internally and exposes only generic `TargetProcess` models to the rest of the application.

Once a process is made the Active Target, the PS5 session uses the existing neutral `IMemoryMapProvider` contract. It sends ps5debug-NG `CMD_PROC_MAPS`, parses the backend-specific map entries inside the PS5 plugin, translates read/write/execute protection bits, and returns only generic `MemoryRegion` models to the host. The host caches the current Active Target map and displays the loaded region count in the target status area. Process refresh also refreshes the map when the same Active Target survives the refresh.

The same session implements the pre-existing neutral `IMemoryReader` and `IMemoryWriter` contracts. `CMD_PROC_READ` remains isolated inside the PS5 plugin: the plugin serializes the process id, 64-bit target address, and requested length, validates the response status, receives the raw bytes, and returns only the neutral read result to the host. `CMD_PROC_WRITE` uses the same packed 16-byte request body, waits for the server's first success acknowledgement, streams the requested bytes, and then consumes the command's second success status before returning. Once either read/write command has begun, the PS5 client completes that protocol transaction before honoring caller cancellation; this preserves command-stream framing and prevents Cancel Scan from corrupting the next process-list, map, read, or write command. Neither protocol structure crosses the Plugin SDK boundary.

The retained raw-read diagnostic path remains bounded to 4 KiB per request, and the retained raw-write diagnostic path writes one signed 4-byte Int32 value while encoding bytes according to the active target's declared endianness. These are development/regression helpers rather than visible main-workspace controls and do not limit `IMemoryReader` or `IMemoryWriter`. Saved Addresses use plugin-declared Value Types for generic value editing. From `0.1.5.rev1`, Memory Viewer inspection reuses the same neutral `IMemoryReader` and `MemoryRegion` contracts. Rev6 adds safe Memory Viewer editing through the already-existing neutral `IMemoryWriter`; no new ps5debug-NG command or PS5 Plugin API surface is required. Rev7 adds bookmark and region navigation entirely in host/Core using the already-loaded neutral memory map. The host uses the primary session reader/writer under its foreground-target reservation and never changes page protection on behalf of the viewer.

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

The repository still contains the optional source-preflight helper under `tools/preflight/`, but further development of that helper is currently paused. It may still be run when useful, but it is not treated as a substitute for or stronger signal than the real C#/WPF compiler. A clean Windows build remains the authoritative compile gate. Existing behavior is documented in [`docs/development/SOURCE_PREFLIGHT.md`](docs/development/SOURCE_PREFLIGHT.md).

Build with:

```text
Build > Build Solution
```

Warnings are treated as errors project-wide.

Source packages are safe to extract over an existing project tree from the preceding scanner-architecture revisions. Paths that held concrete rev2 scanner catalogs, enums, and the Core scan-value class are retained as inert source tombstones so extraction overwrites those obsolete files instead of allowing stale types to remain in an SDK-style project build. The tombstones define no types and do not restore the removed Core catalog architecture.

The application build copies each platform plugin assembly plus its generated dependency metadata/private runtime dependencies to the runtime `Plugins` directory, while shared Plugin SDK contracts remain host-owned. External JSON theme files are copied to the runtime `Themes` directory for normal build and publish output.

## Verification Executable

Run the verification executable with:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

The current revision contains **152 checks**. A successful run ends with `All 152 checks passed.`. Rev29 added Core logical debugger-byte overlay coverage, debugger/disassembly overlay-lifecycle coverage, and a Disassembler debugger-marker/logical-instruction source contract. Rev30 added neutral breakpoint-request validation coverage, Breakpoint/Watchpoint Type-versus-Mechanism presentation coverage, and Scan Results/Saved Addresses debugger-address-action source coverage. Rev31 adds a PS5 teardown regression that verifies both a still-active hardware watchpoint and a staged temporary-watchpoint cleanup are explicitly disabled before disposal sends backend detach. All previously registered checks remain active; no existing check is removed, skipped, renamed, or converted into an unconditional pass.

Project-wide verification documents are kept under [`docs/testing/`](docs/testing/). The current revision checklist is [`docs/testing/APP_0.1.7_REV31_VERIFICATION.md`](docs/testing/APP_0.1.7_REV31_VERIFICATION.md), and the current static source review is [`docs/testing/APP_0.1.7_REV31_SOURCE_REVIEW.md`](docs/testing/APP_0.1.7_REV31_SOURCE_REVIEW.md). Plugin-specific runtime/protocol verification belongs under each plugin's dedicated `docs/plugins/<Plugin>/` directory.

## Current Development Boundary

The complete low-level target-access chain — connection, process enumeration, explicit Active Target selection, memory-map retrieval, raw memory reads, raw memory writes, and immediate read-back verification — has now been live-verified against a real PlayStation 5. The rev17 Safe Write Test was executed twice against `eboot.bin` and returned `Verification: PASS` both times.

Rev13 connected the verified scan-storage foundation to the scanner: shared scans and materialized native scans commit complete candidate sets as compact binary records while WPF remains bounded to the first **50,000** rows. Rev30 extends the complete-result abstraction so a sufficiently large authoritative native result set may remain backend-resident instead. The PS5 TurboScan path can therefore keep hundreds of millions of survivors on the target, fetch only the presentation preview, and run compatible Next Scans directly against that resident session. If a later predicate requires Core semantics, the complete remaining resident set is materialized into the existing disk-backed format before shared refinement. The historical two-million limit remains only on legacy in-memory/list APIs as a compatibility safety boundary.

Rev15 completed the physical-PS5 massive-result verification: a signed-byte Exact Value First Scan for `50` committed **10,953,954** results, a same-value Next Scan refined the complete set to **8,796,420**, and the survivor set included addresses beyond the first 50,000 displayed rows. First Scan and Next Scan were also repeated successfully with **Pause target while scanning** enabled.

The current scanner architecture separates **Core-owned comparison semantics** from **plugin-owned data representations and native acceleration**. Core owns the 13 standard `IMemoryScanType` definitions; plugins supply concrete `IMemoryValueType` definitions, optional Scan Options, and optional semantic native mappings. The PS5 plugin exposes UInt8, Int8, UInt16, Int16, UInt32, Int32, UInt64, Int64, Float, Double, and Array of Bytes, then maps only semantically equivalent Core predicates to ps5debug-NG native operations. Unknown Initial Value uses the TurboScan snapshot engine with zero inclusion rather than direct `compareType 11`; Increased By/Decreased By remain Core fallback because ps5debug-NG's width-wrapping arithmetic is not fully equivalent; floating Unknown Initial Low remains Core fallback because the upstream comparator uses absolute magnitude. Array of Bytes currently uses an exact all-bytes mask rather than wildcard syntax. Saved-address capture, visible Scan Result live refresh, Saved Address editing, and freezing remain generic reader/writer + plugin Value Type workflows. Universal list/table export is shared Core/host infrastructure and is active for Scan Results, Saved Addresses, and the Disassembler; later list-based tools are expected to consume the same export contracts rather than introduce bespoke exporters. `0.1.5.rev1` established the bounded read-only Memory Viewer on neutral memory services. Corrective `0.1.5.rev2` and `0.1.5.rev3` repaired the WPF/C# compile issues discovered by the first Windows builds. `0.1.5.rev4` added independent extended row selection, copy actions, successful-address Back/Forward history, and a persistent green origin-row marker that does not disappear when another row is selected. `0.1.5.rev5` made the combined selected-origin presentation layout-neutral and was fully verified with 51/51 automated checks plus runtime layout confirmation. `0.1.5.rev6` adds safe single-row raw-byte editing through neutral read/write services, stale-data rejection, existing memory-protection enforcement, and immediate read-back verification; it is fully verified with 53/53 automated checks plus Mock Target and live-PS5 runtime coverage. `0.1.5.rev7` added viewer-local exact-address bookmarks and Core-owned previous/start/end/next readable-region navigation while correcting empty Saved Addresses row tooltips; those functional paths passed runtime testing. `0.1.5.rev8` completed and verified the Memory Viewer feature block with the selected-bookmark label and application-wide UI/state consistency around Active Target scan gating, Danger button semantics, and version/revision presentation. `0.1.6.rev1` established the architecture-neutral disassembly contract/Core foundation plus the deterministic Mock provider, and its 62-check Windows suite passed in full. `0.1.6.rev2` added the first real platform implementation: PS5 plugin `0.1.0.rev23` decodes x86-64 bytes through Iced while Core keeps ownership of bounded target reads, and all 64 checks passed. `0.1.6.rev3` exposed those verified layers through the first capability-driven WPF Disassembler workspace and was subsequently live-verified against real PS5 executable memory. `0.1.6.rev4` expanded the workspace to bounded bidirectional context and added direct Scan Results/Saved Addresses entry points. Runtime testing then exposed the artificial seam created when an address fell inside a multi-byte x86-64 instruction. `0.1.6.rev5` removes that seam by decoding the complete around-origin range continuously and marking the decoded instruction that contains the requested address; all 66 automated checks passed and the fix was live-verified on PS5. `0.1.6.rev6` adds architecture-neutral syntax-token presentation, theme-aware highlighting without changing the three-column list layout, successful-address Back/Forward history, and Memory Viewer-to-Disassembler navigation. `0.1.6.rev7` adds layout-neutral full value-span highlighting to Memory Viewer when opened from Scan Results or Saved Addresses, while preserving one-byte highlighting for manual/bookmark/region navigation and restoring span length through Back/Forward history. `0.1.6.rev8` corrects the six inline Memory Viewer `Run.Text` bindings to explicit OneWay mode after Windows runtime testing showed that the rev7 renderer otherwise attempted to bind read-only presentation properties through a writable mode. `0.1.6.rev9` adds target-safe manual Saved Address creation using existing plugin Value Types and Saved Address read/write infrastructure, while leaving unsupported Binary/Text display details and pointer chains as disabled planned dialog controls. `0.1.6.rev10` fixes the rev9 `TryParseHexAddress` definite-assignment compile error by initializing its `out` address before the short-circuit length guard; the manual-entry workflow is otherwise unchanged. `0.1.6.rev11` adds platform-neutral **Follow Target** navigation for direct Call/Jump/ConditionalJump records with a known provider-supplied `BranchTarget`, integrating context menu, double-click, Enter, and existing Back/Forward history without fabricating indirect destinations. `0.1.6.rev12` combined the remaining selection/copy/export milestone with the former final-polish/region-navigation milestone. Its 77-check suite passed and Mock runtime verification confirmed the combined feature set except for a right-click multi-selection collapse. `0.1.6.rev13` corrected right-click selection preservation and passed all 78 automated checks plus the focused Mock selection regression. Runtime copy testing then showed that Copy Address, Copy Bytes, Copy Instruction, and Copy Address + Instruction still projected only the context-clicked row even though the complete selection remained intact. `0.1.6.rev14` completed that final correction and has now passed the full **79/79** automated suite, focused Mock acceptance, and remaining live-PS5 hardware acceptance; `0.1.6` is therefore closed as the verified Disassembler feature block. `0.1.7.rev1` established and verified the neutral Plugin SDK contracts/models and shared Core lifecycle/event-safety foundation with **84/84** checks. `0.1.7.rev2` added and fully verified the generic modeless Debugger workspace, host-owned target-lifetime integration, and deterministic Mock debugger attach/pause/continue/event backend with **89/89** checks plus runtime/UI acceptance. `0.1.7.rev3` introduced the dedicated PS5 debugger command/event transport and real attach/pause/continue/detach backend, but its first compact target-header layout was superseded before Windows/runtime verification. `0.1.7.rev4` preserved that PS5 debugger implementation while establishing the permanent two-row target header and correcting shared button text centering without changing button height. `0.1.7.rev5` retains that work and replaces the fixed 240-unit row-1 input sizing with one responsive shared width capped at 180 units and allowed to shrink without a minimum so the header remains inside the available window content width. `0.1.7.rev6` added and verified thread enumeration/control infrastructure, with PS5 individual thread Suspend/Resume remaining externally backend-blocked. `0.1.7.rev7` implemented Registers and Stop Context but its first Windows gate exposed a false-negative source-contract assertion after the Debugger constructor call moved to target-typed `new(...)`. `0.1.7.rev8` corrected that verifier contract and is fully verified with **107/107** plus all **11/11** focused Registers and Stop Context runtime/hardware steps. `0.1.7.rev9` passed 108/108 plus the focused Mock splitter/register/lifecycle gates, but live PS5 Gate E exposed the optional-register transport stall. `0.1.7.rev10` completed the guarded PS5 extended-register correction, rev15 completed and verified the Breakpoint Manager plus Software Execute Breakpoints, and rev16 completed Hardware Watchpoints with **118/118 PASS** plus focused Mock/live-PS5 acceptance. Rev17 added Call Stack/Call Frames and Step Into/Step Over/Step Out/Run-to operations using the existing neutral contracts, verified Disassembler, and temporary-breakpoint infrastructure and passed 124/124 automated checks. Rev24 established the accepted independent modeless z-order/coordinated MainWindow-tool cleanup and replaced the repeatedly problematic Debugger TabControl/TabItem chrome with compact selector buttons. Rev25 preserved that accepted workspace, made the ps5debug-NG software-breakpoint interrupt snapshot authoritative for the matching logical stop, prevented a second native Step Into after the backend had already transparently stepped the breakpointed instruction, reconciled fast stop events with execution-status text, and centered all MainWindow bottom-status items. Rev27 added interruption-aware temporary-breakpoint retirement plus defensive PS5 software-breakpoint restoration before detach/disposal. Rev28 completed the deferred-stop verification correction and the carried debugger cleanup paths were then accepted in live PS5 testing. Rev29 added the verified logical Disassembler view for debugger-owned Software/Execute instrumentation plus the Markers column. Rev30 added debugger-address shortcuts and Breakpoint/Watchpoint classification but was superseded before verification when live shutdown testing exposed stale PS5 hardware-watchpoint state. Current rev31 adds explicit PS5 hardware-watchpoint cleanup before detach/disposal. Debugger Integration, Export and Finalization therefore moves to rev32. Pointer scanning, richer Find What Writes/Reads/Accesses workflows, Break and Trace, cheat construction, assembler/instruction editing, and saved-address project persistence remain later work. Further development of the existing `tools/preflight/` helper is paused unless that decision is explicitly revisited.
