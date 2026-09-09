# Application 0.1.7.rev6 Source Review — Threads and Thread Control

## Status

**Static/source review complete before packaging. Windows compilation and runtime verification remain external gates.**

This document records the source-level review for `0.1.7.rev6 - Threads and Thread Control`. It must not be interpreted as a substitute for the Windows `101/101` run or the focused Mock/live-PS5 thread acceptance in `APP_0.1.7_REV6_VERIFICATION.md`.

## Baseline

Rev6 was produced directly from the user-supplied, fully verified `0.1.7.rev5 - Responsive Target Header Input Sizing` package.

The rev5 verification baseline is:

- **97/97** Windows automated checks passed;
- responsive two-row header/button runtime acceptance passed;
- Mock debugger regression passed;
- live PS5 Attach/TCP755/Pause/Continue/transport-isolation/Detach-Reattach/window-close/disconnect-generation/multiple-window/regression gates passed;
- the optional natural async-interrupt mapping scenario was not safely triggerable and was explicitly deferred rather than failed.

## Mandatory Pre-Change Review

Before rev6 implementation, the project documentation and complete source/project text inventory were reviewed again. The review included `README.md`, `CHANGELOG.md`, every Markdown file under `docs/`, application/Core/Plugin SDK/plugin/test sources, XAML, and project/build files. The implementation therefore reuses the thread contracts already introduced in API `2.12.0` instead of adding a second debugger-thread abstraction.

The development action plan identifies thread enumeration/control as the next debugger dependency after the verified attach/run/pause/continue transport. Registers/stop context remain the next milestone after rev6.

## Intended Source Boundary

Rev6 intentionally changes only the layers required by its scope:

- App: Debugger thread presentation/commands and passive memory-map header presentation;
- Mock plugin: deterministic attached-session thread services and independent plugin revision;
- PS5 plugin: plugin-private ps5debug-NG thread command transport/mapping and independent plugin revision;
- tests: four new rev6 registrations plus updated capability/version expectations and PS5 protocol fixture;
- metadata/documentation.

The public Plugin SDK and shared Core debugger foundation remain byte-identical to rev5 because `IDebuggerThreadService`, `IDebuggerThreadControlService`, `DebuggerThreadInfo`, `DebuggerThreadState`, `ThreadEnumeration`, and `ThreadControl` already exist in API `2.12.0`.

## Shared Debugger UI Review

The Debugger workspace adds a capability-driven **Threads** pane. Source review confirms:

- the pane is visible only for `ThreadEnumeration`;
- thread rows bind only to neutral id/name/state data;
- Refresh obtains `IDebuggerThreadService` from the attached debugger session;
- Suspend/Resume require `ThreadControl` plus `IDebuggerThreadControlService`;
- per-thread control requires whole-debugger `Running` state;
- selection is preserved by neutral id after refresh where possible;
- Attach/Pause/Continue refresh neutral thread state;
- Detach/stale-target invalidation clears thread presentation;
- WPF/App source does not contain PS5 LWP ids, ptrace types, Windows thread handles, register structs, or ps5debug-NG opcodes for the thread feature.

`DebuggerThreadViewModel` is presentation-only and wraps the existing neutral SDK model rather than creating a second thread domain model.

## Mock Backend Review

Mock `1.0.0.rev9` targets unchanged Plugin API `2.12.0` and now advertises `Debugger + ThreadEnumeration + ThreadControl`.

The attached session exposes deterministic ids/names:

- `0x1 Main`;
- `0x2 Worker`;
- `0x3 Render`.

Source review confirms explicit suspended-id tracking, validation of unknown/duplicate transitions, Running-only per-thread control, whole-target Stopped mapping, persistence of individual Suspended state across whole-target Pause/Continue, and cleanup on session disposal.

## PS5 Backend Review

PS5 `0.1.0.rev26` targets unchanged Plugin API `2.12.0` and advertises `Debugger + ThreadEnumeration + ThreadControl`; more advanced debugger capability flags remain off.

The plugin-private protocol mapping was rechecked against current ps5debug-NG documentation/source before implementation:

| Operation | Opcode | Shape used by rev6 |
| --- | --- | --- |
| Get thread list | `0xBDBB0005` | no body; success + `uint32 count` + ids |
| Suspend thread | `0xBDBB0006` | `uint32 thread id`; status |
| Resume thread | `0xBDBB0007` | `uint32 thread id`; status |
| Thread info | `0xBDBB0011` | `uint32 thread id`; success + 40-byte id/priority/name record |

Source review confirms:

- the complete list/info sequence is serialized through the existing dedicated debugger command gate;
- count is bounded defensively before allocating/reading the list;
- optional per-thread info failure retains the enumerated id rather than dropping the thread;
- only id/name/neutral state leave the PS5 plugin;
- priority, LWP terminology, 32-bit wire layout, opcodes, and protocol framing remain plugin-private;
- successful individual Suspend/Resume is tracked locally because current list/info responses do not provide a reliable per-thread execution-state field;
- suspended ids are reconciled against each new backend enumeration;
- thread control requires the whole debugger target to be Running;
- existing rev5 Attach/Detach/Pause/Continue/TCP755/cleanup behavior is extended rather than duplicated.

## Main Workspace UI Cleanup Review

The rev5 two-row header, responsive 180-unit maximum ordinary inputs, protected side padding, and shared button metrics are preserved.

Rev6 removes only the `MemoryRegionStatusText` binding that displayed passive successful memory-map count prose in the permanent header. Source review confirms the underlying `GetMemoryRegionsAsync` flow and `ActiveMemoryRegions` assignment remain in `PluginViewModel`, and `MemoryRegionErrorText` remains available for real failures.

## Automated Verification Registry Review

The rev5 registry contains 97 unique test names. Rev6 retains all of them and adds exactly four:

1. `Mock debugger thread enumeration and control`;
2. `Debugger workspace thread panel source contract`;
3. `PS5 debugger thread enumeration and control protocol`;
4. `Main workspace passive memory-map status removal`.

Expected rev6 registry size: **101 unique checks**.

The PS5 protocol fixture is extended with the thread-list/thread-info/suspend/resume command shapes and records per-thread control actions for deterministic verification.

## Static Checks Required Before ZIP Lock

The final package review must confirm all of the following and record the results below:

- all intended XAML/XML/project files are structurally parseable;
- all Markdown relative links resolve;
- no `bin/` or `obj/` build output is included;
- all 97 rev5 test registrations remain present and four new registrations produce 101 unique tests;
- Plugin SDK and Core production trees are byte-identical to rev5;
- App thread presentation does not contain platform-specific debugger terms/opcodes;
- application/plugin version/revision metadata is consistent;
- ZIP integrity passes.

## Environment Limitation

The assistant environment does not provide the project's authoritative Windows/.NET/WPF runtime gate. No claim that `101/101` passed is made by this source review. The user must run the clean Windows Release build and verification executable, followed by the focused runtime/hardware checklist.

## Final Static Review Results

Final package-lock review was completed against the exact user-supplied `0.1.7.rev5` baseline after all rev6 source and documentation changes were finished. The review produced the following results:

### Diff boundary

- **30 existing files changed** and **4 files added** relative to rev5; **0 files were removed**.
- The four added files are the two rev6 testing documents, `Ps5DebuggerThreadInfo.cs`, and `DebuggerThreadViewModel.cs`.
- Production changes remain limited to App debugger/header presentation, Mock debugger thread services/version metadata, and PS5 debugger thread protocol/session services/version metadata. Test changes remain limited to the verification registry and PS5 protocol fixture.
- No unrelated Core scanner, export, Memory Viewer, Disassembler, settings, or primary PS5 process/memory/scan implementation was rewritten for rev6.

### Structural and documentation checks

- **PASS — XML/XAML/project structure:** all **21** `.xaml`, `.csproj`, `.props`, and `.targets` files parsed successfully.
- **PASS — Markdown relative links:** all **133** Markdown files were scanned; **40** relative links were resolved and none were broken.
- **PASS — build-output hygiene:** no `bin/` or `obj/` directory is present in the package tree.
- **PASS — current documentation consistency:** `README.md`, `CHANGELOG.md`, the development action plan, debugger architecture, Plugin SDK documentation, Mock documentation, PS5 documentation, and the rev6 verification documents consistently identify rev6 as the current **Threads and Thread Control** candidate built on fully verified rev5.
- During final documentation review, one stale paragraph in the living PS5 README was found still describing rev26 as advertising only the coarse Debugger capability. It was corrected to record the historical rev25 debugger consumer and the current rev26 `Debugger + ThreadEnumeration + ThreadControl` capability/service set. No production code changed as a result of this documentation correction.

### Verification-registry preservation

- **PASS — rev5 preservation:** all **97** rev5 test names remain registered.
- **PASS — rev6 additions:** exactly the four planned checks were added:
  1. `Mock debugger thread enumeration and control`;
  2. `Debugger workspace thread panel source contract`;
  3. `PS5 debugger thread enumeration and control protocol`;
  4. `Main workspace passive memory-map status removal`.
- **PASS — uniqueness:** the resulting registry contains exactly **101 unique checks** with no duplicate names.

### Shared architecture preservation

- **PASS — Core byte identity:** all **59** files under `src/TeeKay87.MemoryEngine.Core` are byte-identical to rev5.
- **PASS — Plugin SDK byte identity:** all **76** files under `src/TeeKay87.MemoryEngine.PluginSdk` are byte-identical to rev5.
- **PASS — no duplicate thread abstraction:** rev6 consumes the existing API `2.12.0` thread contracts instead of adding a second model/service layer.
- **PASS — host neutrality:** `DebuggerWindow.xaml`, `DebuggerViewModel.cs`, and `DebuggerThreadViewModel.cs` were checked for PS5/LWP/ptrace/ps5debug-NG/backend-opcode leakage; no platform-specific debugger terms or opcodes are present in the host thread presentation.

### Production-source review

- **PASS — changed C# review:** every changed or added C# source file was re-read after implementation, including the complete Mock/PS5 thread service paths, host ViewModel flow, version metadata, test registrations, and protocol fixture. Required explicit `using` directives are present for the BCL/SDK types introduced by the changed files; the two namespace-only/simple-record files require no additional imports.
- **PASS — unfinished-marker scan:** no `TODO`, `FIXME`, or `HACK` marker exists in changed/added C# or XAML source.
- **PASS — PS5 protocol boundary:** thread-list/info/control command ids, 32-bit backend ids, priority data, and ps5debug-NG framing stay inside the PS5 plugin. The host receives only neutral thread ids/names/states.
- **PASS — command-stream serialization:** list enumeration plus per-thread info lookups remain under the existing dedicated debugger command gate; Suspend/Resume uses that same debugger-owned serialized connection rather than the primary PS5 memory/scan stream.
- **PASS — thread state rules:** individual Suspend/Resume requires whole-target Running state; Mock preserves deterministic individual Suspended state across whole-target Pause/Continue, and PS5 reconciles locally owned suspended ids against fresh backend enumeration.
- **PASS — memory-map UI cleanup:** only the passive `MemoryRegionStatusText` binding was removed from `MainWindow.xaml`; `GetMemoryRegionsAsync`, `ActiveMemoryRegions`, and `MemoryRegionErrorText` remain in the established flow.

### Metadata consistency

- **PASS — application:** `0.1.7.rev6`, feature title `Threads and Thread Control`.
- **PASS — Mock plugin:** `1.0.0.rev9`, Plugin API `2.12.0`.
- **PASS — PS5 plugin:** `0.1.0.rev26`, Plugin API `2.12.0`.
- **PASS — public API:** host Plugin API remains `2.12.0`; no contract bump is required.

### ZIP lock

All pre-archive static checks above pass. A complete archive was created once to validate the package operation, then this result was written into the source-review record before the final archive was rebuilt and retested.

- **Archive:** `TK87ME_0.1.7.rev6___Threads-and-Thread-Control.zip`
- **Archive contents:** **376 files**, package-root layout preserved, no `bin/` or `obj/` output
- **Archive integrity:** **PASS** — Python `ZipFile.testzip()` returned no corrupt entry; the final rebuilt archive is retested after this document is included.

The authoritative Windows/.NET gate is intentionally **not** claimed here. Rev6 still requires the user's clean Windows Release build, **101/101** automated result, and the focused runtime/hardware checks in `APP_0.1.7_REV6_VERIFICATION.md`.
