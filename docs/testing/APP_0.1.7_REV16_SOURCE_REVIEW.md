# Application 0.1.7.rev16 Source Review — Hardware Watchpoints

## Scope

Rev16 is built from the supplied and fully verified `0.1.7.rev15` package. Rev15 completed the Windows automated suite at **114/114 PASS**, and the focused runtime gates for software-breakpoint address validation, immediate breakpoint re-hit register refresh, Disassembler presentation cleanup, and breakpoint regression all passed.

The rev16 scope is hardware data watchpoints inside the still-active `0.1.7` Debugger block. The implementation extends the existing neutral breakpoint manager and debugger-event pipeline instead of creating a parallel watchpoint subsystem. Platform-specific register encodings, slot limits, address policy, and wire commands remain in the corresponding plugins.

## Mandatory Pre-change Review

Before rev16 production edits, the supplied rev15 tree was reviewed across:

- root `README.md` and `CHANGELOG.md`;
- every Markdown document under `docs/`;
- the complete application, Core, Plugin API, Mock plugin, PS5 plugin, tests, XAML, project files, and supporting tools;
- existing neutral breakpoint/watchpoint enums and request/event contracts;
- Debugger manager ViewModel/XAML and Add dialog;
- Mock deterministic debugger layout/session behavior;
- PS5 target-memory-map cache, debugger provider/session, command client, protocol constants, async event parser, and test server;
- current ps5debug-NG `PROTOCOL.md`, `common/include/protocol.h`, and `debugger/source/debug.c` at commit `d32d2d001dbbfd4cd2c0b7d6335b9a49d8a1cb86`.

That review confirmed that `DebuggerBreakpointKind.Hardware`, `DebuggerBreakpointAccess`, `DebuggerBreakpointRequest`, `TargetCapabilities.Watchpoints`, `DebuggerEventKind.Watchpoint`, `DebuggerStopReason.Watchpoint`, and the existing `IDebuggerBreakpointService` / `IDebuggerBreakpointStateService` already provide the correct neutral foundation. Rev16 therefore extends those contracts only where hit attribution needs one additional optional piece of context.

## Shared Plugin API Design

Plugin API advances to `2.15.0` by adding optional `DebuggerEvent.TriggeredBreakpoint` context. The previous constructor remains available so existing compatible event producers do not need to manufacture breakpoint context.

The distinction is deliberate:

- `InstructionPointer` is the instruction address at which execution stopped;
- for a data watchpoint, `TriggeredBreakpoint.Request.Address` is the watched memory address;
- callers do not infer a data address from the instruction pointer.

No PS5 DR register name, slot index, packet offset, or backend opcode enters the neutral API.

## Breakpoints / Watchpoints Manager

The existing Debugger manager is extended in place:

- the pane heading is `Breakpoints / Watchpoints`;
- the pane is visible when the plugin advertises either Breakpoints or Watchpoints;
- the Add dialog can create a Software Execute Breakpoint or Hardware Watchpoint;
- hardware-watchpoint input exposes Address, Access, Size, and Temporary/Persistent lifetime;
- Address reuses the host `HexAddress` live input filter and Size reuses the host `UnsignedInteger` live input filter, while plugin validation remains authoritative for mapping, width, alignment, and platform rules;
- the existing State column and Add/Refresh/Enable/Disable/Remove/Remove All controls remain unchanged, while the list adds Size so watchpoint width remains visible;
- Disassembler navigation remains valid only for execute breakpoints, not data watchpoints.

Access, width, alignment, slot, and mapping rules are plugin-owned. This keeps the host usable for future PC/Xbox plugins whose hardware rules differ from PS5.

## Mock Hardware Watchpoints

Mock advances to `1.0.0.rev14` and advertises `TargetCapabilities.Watchpoints`.

Its deterministic hardware-watchpoint rules are:

- four hardware slots independent of the 30 software-breakpoint slots;
- Read, Write, and ReadWrite data access;
- widths 1, 2, 4, and 8 bytes;
- natural alignment to the selected width;
- the complete watched range must remain inside Mock target memory;
- duplicate equivalence includes kind, address, size, and access.

Continue can generate a deterministic Watchpoint stop. The synthetic accessing instruction is inside the Mock code fixture, while `TriggeredBreakpoint` identifies the watched data address. Temporary watchpoints are removed on their first deterministic hit; persistent watchpoints remain.

## PS5 Hardware Watchpoint Transport

PS5 advances to `0.1.0.rev33` and advertises `TargetCapabilities.Watchpoints`.

Current ps5debug-NG defines:

```text
CMD_DEBUG_SET_WATCHPOINT = 0xBDBB0004
request size = 24 bytes
{ uint32 index, uint32 enabled, uint32 length, uint32 breaktype, uint64 address }
```

The backend exposes four hardware slots (`0-3`) through DR0-DR3/DR7. Rev33 keeps all of that private to the PS5 plugin.

Width encoding:

| Width | Wire length |
| ---: | ---: |
| 1 | 0 |
| 2 | 1 |
| 4 | 3 |
| 8 | 2 |

Data-access encoding:

| Neutral access | Wire breaktype |
| --- | ---: |
| Write | 1 |
| ReadWrite | 3 |

True Read-only data watchpoints are rejected on PS5 because amd64 DR7 has no read-only data-watchpoint encoding. ReadWrite is the supported way to observe reads.

Before any backend mutation, rev33 validates:

- hardware/data kind and supported access mode;
- width 1/2/4/8;
- natural alignment;
- complete watched range inside one current mapped region;
- non-guarded region state;
- duplicate state and four-slot capacity.

Software execute breakpoints remain on the verified INT3 path and continue to use their separate 30-slot pool.

## PS5 Watchpoint Event Attribution

The normal 1184-byte async interrupt packet already contains a 128-byte debug-register block at offset `0x420`; DR6 is at packet offset `0x450`. Rev16 uses that event payload and does **not** re-enable paused `GETDBREGS`.

When DR6 B0-B3 identify a managed hardware slot, the plugin maps the stop exactly and emits a Watchpoint event with `TriggeredBreakpoint`.

### Current ps5debug-NG DR6 Limitation

Current `dispatch_debug_events` copies the debug-register state into the outgoing packet and then clears `pkt_dr[6]` for the debug-exception path before later watchpoint cleanup. The later save/restore therefore operates on the already-cleared packet value. A client can consequently receive a real watchpoint stop with DR6 B0-B3 equal to zero.

Rev16 handles that external defect conservatively:

- zero DR6 + exactly one enabled managed hardware watchpoint: infer the only possible managed watchpoint and emit Watchpoint with an explanatory message;
- zero DR6 + multiple enabled managed hardware watchpoints: do not guess; emit a generic Signal/Other stop explaining that exact attribution is unavailable;
- ambiguous multi-watchpoint stops do not remove any temporary watchpoint arbitrarily.

The external issue is documented in `docs/bug-reports/ps5debug-ng-watchpoint-interrupt-clears-dr6-trigger-status.md`.

## Paused Register Safety

Rev16 does not alter the rev10 guarded optional-register strategy. Paused `GETDBREGS` remains suppressed because the current backend can block after a previously consumed stop transition. Hardware-watchpoint attribution uses the debug-register bytes already included in the asynchronous event instead.

When optional safe groups succeed, the established PS5 register surface remains 76 rows. The rev15 immediate re-hit stop-context replay remains in place.

## Verification-Code Changes

The automated registry increases from **114 to 118 unique registrations**:

1. triggered-breakpoint debugger-event context;
2. Mock hardware-watchpoint lifecycle and deterministic hits;
3. Debugger hardware-watchpoint manager/Add-dialog source contract;
4. PS5 hardware-watchpoint protocol and hit mapping.

The PS5 test additionally covers exact DR6 attribution, a zero-DR6 single-watchpoint fallback, an ambiguous zero-DR6 multi-watchpoint stop that is not misattributed, temporary-lifetime safety, packet framing, access/width encoding, local validation, slot lifecycle, and continued suppression of paused `GETDBREGS`.

## Version Domains

| Component | rev16 value | Reason |
| --- | --- | --- |
| Application | `0.1.7.rev16` | Debugger UI/behavior/docs changed |
| Feature | `Hardware Watchpoints` | Current revision scope |
| Plugin API | `2.15.0` | Optional triggered-breakpoint event context |
| Mock plugin | `1.0.0.rev14` | Mock hardware watchpoints added |
| PS5 plugin | `0.1.0.rev33` | PS5 hardware watchpoint transport/mapping added |
| Automated checks | `118` | Four focused registrations added |

The `0.1.7` Debugger block remains active after rev16. Call Stack/Frames, Stepping/Run-to, and final debugger integration remain later revisions in the same version until that full block is implemented and verified.

## Static Package-Preparation Requirements

Before packaging, confirm:

- `AppInfo` reports `0.1.7.rev16` and `Hardware Watchpoints`;
- Plugin API reports `2.15.0`;
- Mock reports `1.0.0.rev14` / API `2.15.0` and advertises Watchpoints;
- PS5 reports `0.1.0.rev33` / API `2.15.0` and advertises Watchpoints;
- the shared manager uses existing breakpoint services rather than a duplicate watchpoint service;
- Mock validation/hit/lifetime logic is present;
- PS5 request framing and DR7 mappings match current upstream source;
- PS5 event parsing does not issue paused `GETDBREGS`;
- exact, single-fallback, and ambiguous-multi attribution branches are covered;
- the test registry contains 118 unique registrations;
- XAML/XML/project and JSON files parse;
- Markdown relative links resolve;
- no `bin`, `obj`, or `.vs` output is packaged;
- the final ZIP matches the locked work tree byte-for-byte.

Native .NET build/WPF execution is intentionally left to the Windows verification gate and is not claimed by source/package preparation.

## Final Static Review Result

The final rev16 source tree was reviewed again after the last UI-input and documentation corrections. The static preparation gate completed with the following results:

- 411 project files were present in the complete source tree;
- the rev16 tree differs from the supplied verified rev15 baseline in 32 files, including 17 C# source files;
- all 17 changed C# files were re-read for affected control flow, existing-service reuse, required namespace imports, and stale/redundant rev16 paths;
- the test registry contains 118 registrations with 118 unique names and 118 unique method targets;
- 23 XAML/XML/project files parse successfully;
- all 3 JSON files parse successfully;
- all 53 checked relative Markdown links resolve;
- the Breakpoint/Watchpoint Add dialog uses the shared `HexAddress` and `UnsignedInteger` live input filters, with source-contract assertions protecting both bindings;
- application, Plugin API, Mock, and PS5 version/capability metadata match the rev16 version domains documented above;
- PS5 watchpoint framing/constants and the event-side DR6 attribution branches remain present without introducing a paused debug-register command read;
- no `bin`, `obj`, or `.vs` build directories, temporary/editor artifacts, merge-conflict markers, or prohibited authorship wording are present;
- the external DR6 bug report contains source-level reproduction details without naming this application.

No .NET build or WPF/runtime result is claimed by this source review. Those checks remain the Windows and focused Mock/live-PS5 verification gates in `APP_0.1.7_REV16_VERIFICATION.md`.

## Package Target

The archive is named `TK87ME_0.1.7.rev16___Hardware-Watchpoints.zip`, contains the complete project tree without an extra wrapper directory, and must pass ZIP CRC/integrity plus file-for-file hash comparison against the locked work tree.
