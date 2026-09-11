# Application 0.1.7.rev15 Source Review — Breakpoint Runtime Validation Fixes

## Scope

Rev15 is built from the supplied `0.1.7.rev14` package after rev14 passed **114/114** Windows verification and comprehensive Mock/live-PS5 breakpoint runtime acceptance. The runtime cycle found two remaining implementation defects: Software/Execute breakpoint addresses could reach breakpoint state/backend handling without target execute-range validation, and a very fast breakpoint re-hit could arrive while Continue was still completing and leave the Registers pane empty. The revision also removes the requested long visible-range explanatory paragraph from the Disassembler.

No public debugger contract, ps5debug-NG breakpoint packet, persistent/temporary lifetime rule, paused Disable/Remove staging rule, Breakpoints/Events splitter behavior, scanner, Saved Addresses, Memory Viewer, export path, or theme behavior is redesigned.

## Mandatory Pre-change Review

Before production edits, the supplied rev14 tree was reread in full:

- root `README.md` and `CHANGELOG.md`;
- every Markdown file under `docs/`;
- all C#, XAML, project, configuration, and supporting source files;
- Mock target/debugger layout and breakpoint services;
- PS5 target-session memory-map path, debugger provider/session, breakpoint transport, and protocol test server;
- host debugger coordinator/ViewModel state/event ordering;
- Disassembler XAML and existing source-contract tests.

The review covered **152 Markdown files / 34,558 lines** and **254 source/project/config files / 44,387 lines** in the rev14 baseline. It confirmed that the required fixes can reuse existing memory-map, debugger-event, and stop-context paths rather than introducing parallel abstractions.

## Root Cause — Breakpoint Address Validation

### Mock

The Mock debugger validated breakpoint kind/access/size and duplicate state, but did not check address ownership. The deterministic target has one broad Read/Write memory map while its actual synthetic executable fixture is separately defined by `MockTargetLayout.CodeAddress` and `CodeBytes`. Therefore a mapped data address such as Ammo at `0x10000104`, or an address outside the target map such as `0x80000104`, could be accepted as a Software/Execute breakpoint.

Rev15 keeps the policy inside the Mock plugin. `ValidateSoftwareExecuteBreakpoint` now requires both:

- membership in the deterministic target memory range; and
- membership in the explicit synthetic code fixture `[CodeAddress, CodeAddress + CodeBytes.Length)`.

### PS5

The PS5 debugger similarly validated the supported one-byte Software/Execute shape and duplicate/slot state before calling ps5debug-NG `SET_BREAKPOINT`, but did not validate target mapping/protection first.

The ordinary active-target workflow already obtains the PS5 memory map through `IMemoryMapProvider`. `Ps5TargetSession` now stores the latest immutable region list for that same process. `Ps5DebuggerProvider` passes a plugin-private read-only snapshot callback into `Ps5DebuggerSession`. `AddBreakpointAsync` requires that snapshot before slot allocation and rejects an unavailable map, unmapped address, non-executable region, or guarded region.

This design deliberately avoids issuing a new memory-map request from `Ps5DebuggerSession`: the general PS5 target client is not treated as a concurrently multiplexed command stream, so synchronous use of the already-enumerated snapshot avoids a new transport race. No memory-map rule or PS5 page-protection policy is moved into Core/WPF.

## Root Cause — Immediate Breakpoint Re-hit Register Refresh

The host clears Registers when debugger state changes to Running. During Continue, `DebuggerViewModel` also holds `IsBusy=true`. The coordinator can receive an immediate Breakpoint/Paused event before the Continue command has finished unwinding. The existing event handler updated the instruction pointer but skipped `RefreshStopContextFromEventAsync` whenever the ViewModel was busy, with no later replay. Registers could therefore remain empty after the new Paused state.

Rev15 tracks Continue separately from generic busy operations. A Paused event arriving while Continue is in progress is stored as the newest deferred stop context. In Continue's `finally` path, after the busy state is released, the ViewModel replays that event through the existing Breakpoints/Threads/Registers refresh routine if the target is still current and Paused. Running and stale-state transitions discard deferred state. Manual Pause retains its explicit refresh path so it does not gain a duplicate event refresh.

The redundant unconditional `ClearRegisters()` after Continue was also removed because `ApplyCoordinatorState` already owns non-Paused cleanup and must not erase a new fast Paused stop.

## Disassembler Presentation Cleanup

`DisassemblerWindow.xaml` no longer contains the paragraph beginning `The visible range is decoded as one continuous stream...`. Its dedicated spacer/layout row was removed at the same time, leaving no empty gap. The existing Region / Module, Visible Range, Protection, Architecture, navigation, selection, syntax highlighting, and instruction table behavior is otherwise unchanged.

## Verification-Code Changes

The test registry remains **114 unique registrations**. Existing checks are strengthened instead of adding count-only registrations:

- Mock breakpoint lifecycle rejects a mapped data address and an out-of-map address before exercising valid code breakpoints;
- PS5 breakpoint protocol coverage first populates the test target's memory-map cache, then rejects mapped non-executable and unmapped addresses and confirms no backend breakpoint action was recorded;
- Debugger breakpoint source coverage protects the Continue-specific deferred-stop-context path;
- Disassembler source coverage asserts that the removed explanatory text is absent.

## Version Domains

| Component | rev15 value | Reason |
| --- | --- | --- |
| Application | `0.1.7.rev15` | Host behavior/UI/docs changed |
| Feature | `Breakpoint Runtime Validation Fixes` | Current revision scope |
| Plugin API | `2.14.0` | No public contract change |
| Mock plugin | `1.0.0.rev13` | Mock breakpoint validation changed |
| PS5 plugin | `0.1.0.rev32` | PS5 target-map/breakpoint validation changed |
| Automated checks | `114` | Registry unchanged; existing checks strengthened |

Hardware Watchpoints moves to rev16. Call Stack/Frames, Stepping/Run-to, and final debugger integration consequently move to rev17, rev18, and rev19.

## Static Package-Preparation Requirements

Before packaging, confirm:

- `AppInfo` reports `0.1.7.rev15` and `Breakpoint Runtime Validation Fixes`;
- Plugin API remains `2.14.0`;
- Mock reports `1.0.0.rev13`;
- PS5 reports `0.1.0.rev32`;
- Mock rejects mapped-data and unmapped Software/Execute addresses;
- PS5 validates against cached mapped executable/non-guarded memory before backend mutation;
- immediate Continue-time Paused events can be deferred/replayed for stop-context refresh;
- the removed Disassembler paragraph is absent from production XAML;
- the test registry still contains 114 unique registrations;
- XAML/XML/project and JSON files parse;
- documentation relative links resolve;
- no `bin` or `obj` output is packaged;
- the final ZIP matches the locked work tree byte-for-byte.

Native .NET build/WPF execution is intentionally left to the Windows verification gate and is not claimed by source/package preparation.

## Final Static Review Result

The locked rev15 work tree passed the non-.NET package-preparation checks before archive creation:

- **154** Markdown files were reread successfully;
- **254** source/project/configuration text files were reread successfully;
- **23** XAML/project/XML files parsed with **0** errors;
- **3** JSON files parsed with **0** errors;
- **51** relative Markdown links resolved with **0** broken links;
- **0** `bin`/`obj` directories are present;
- application metadata reports `0.1.7.rev15 - Breakpoint Runtime Validation Fixes`;
- Plugin API remains `2.14.0`;
- Mock metadata reports `1.0.0.rev13` and PS5 metadata reports `0.1.0.rev32`;
- the verification registry still contains exactly **114** registrations with **114** unique names;
- the strengthened Mock breakpoint test covers mapped-data and out-of-map rejection while retaining a valid in-code persistent/temporary lifecycle;
- the strengthened PS5 breakpoint test populates the current-process memory-map cache, rejects mapped non-executable and unmapped requests, and asserts that rejected requests create **0** backend breakpoint actions;
- all changed C# files have balanced source braces in the static package check and the newly required `System.Collections.Generic` import is present in `Ps5DebuggerProvider.cs`;
- the Disassembler XAML no longer contains the removed visible-range explanatory paragraph;
- the Continue-specific `_deferredStopContextEvent` / `_continueInProgress` replay path is present and covered by the existing source-contract registration;
- compared with the supplied rev14 package, rev15 contains **2 added**, **22 changed**, and **0 removed** files;
- no external bug-report document was added because the corrected defects are internal host/plugin behavior.

This review intentionally does not claim a .NET build, WPF runtime result, or live-PS5 result for rev15. Those remain the Windows and hardware gates in `APP_0.1.7_REV15_VERIFICATION.md`.

## Package Target

The archive is named `TK87ME_0.1.7.rev15___Breakpoint-Runtime-Validation-Fixes.zip`. The locked work tree contains **408 files** and the final archive must contain exactly those relative paths, pass ZIP CRC/integrity validation, contain no extra root wrapper directory, and match the work-tree bytes file-for-file.
