# Application 0.1.7.rev7 Source Review — Registers and Stop Context

## Status

**Static/source review is the package-lock gate. Windows compilation and runtime verification remain external gates.**

This document records the source-level review for `0.1.7.rev7 - Registers and Stop Context`. It does not substitute for the Windows **107/107** run or the focused Mock/live-PS5 acceptance in `APP_0.1.7_REV7_VERIFICATION.md`.

## Baseline

Rev7 is produced from the user-supplied `0.1.7.rev6 - Threads and Thread Control` package after rev6 was accepted as the working baseline:

- **101/101** Windows automated checks passed;
- main-workspace cleanup and all Mock thread gates passed;
- live PS5 thread enumeration, whole-target Pause/Continue, debugger transport/cleanup/reconnect, and final regression smoke passed;
- individual PS5 thread Suspend returned ps5debug-NG on-wire `CMD_ERROR` (`0xF0000001`) for an enumerated worker thread, without crashing the game, console, or debugger session;
- that external backend rejection is documented in `docs/bug-reports/ps5debug-ng-thread-suspend-returns-cmd-error.md` rather than being hidden or reclassified as a successful live ThreadControl test.

## Mandatory Pre-Change Review

Before rev7 changes were locked, `README.md`, `CHANGELOG.md`, every Markdown document under `docs/`, and the complete source/project text inventory were reviewed. The existing debugger contracts introduced in API 2.12 were reused. Rev7 adds only the neutral register-value interpretation metadata needed by the shared UI rather than introducing a second register model.

The development action plan places Registers and Stop Context directly after the verified thread layer. Extended floating-point/SIMD/debug-register state remains the next milestone.

## Public Contract Review

Plugin API advances to `2.13.0` through additive `DebuggerRegisterValueEncoding` metadata on `DebuggerRegister`:

- `Bytes` for opaque values;
- `UnsignedLittleEndian` for fixed-width little-endian integers;
- `UnsignedBigEndian` for fixed-width big-endian integers.

The existing semantic `DebuggerRegisterRole` remains responsible for InstructionPointer, StackPointer, and FramePointer behavior. Shared code must not infer those roles from literal architecture register names.

## Shared App Register Review

Source review confirms the Debugger register UI is architecture-neutral:

- visibility requires `RegisterAccess` plus `IDebuggerRegisterService`;
- snapshots require a valid Paused session and selected thread;
- Continue/Detach/stale-target transitions clear register data;
- selected-thread changes while Paused refresh the snapshot;
- formatting/parsing uses `DebuggerRegisterValueEncoding` and declared byte width;
- writable rows alone can open the edit dialog;
- writes refresh and compare exact returned bytes before success is reported;
- the current instruction is resolved through `DebuggerRegisterRole.InstructionPointer`;
- Disassembler navigation reuses the host's existing target-bound Disassembler launcher.

No PS5 opcode, LWP type, ptrace call, FreeBSD register structure, or hard-coded x86 register-name dependency belongs in these shared files.

## Mock Backend Review

Mock `1.0.0.rev10` targets Plugin API `2.13.0` and advertises `Debugger + ThreadEnumeration + ThreadControl + RegisterAccess`.

The attached debugger session supplies deterministic paused-thread 64-bit general-register snapshots for Main/Worker/Render. The fixture marks frame pointer, stack pointer, and instruction pointer semantically, uses unsigned little-endian encoding, and exposes writable rows so the host's edit/write/read-back flow has a safe deterministic acceptance backend.

Register operations reject Running/detached/disposed use and unknown or width-invalid writes rather than mutating presentation optimistically.

## PS5 Backend Review

PS5 `0.1.0.rev27` targets Plugin API `2.13.0` and advertises `Debugger + ThreadEnumeration + ThreadControl + RegisterAccess`.

The GETREGS mapping was checked against the current ps5debug-NG source/protocol reference used during development:

| Item | Rev7 mapping |
| --- | --- |
| Command | `0xBDBB0008` |
| Request | 4-byte selected LWP/thread id |
| Success payload | 176-byte amd64 general-register block |
| Backend operation | `PT_GETREGS` |
| Host exposure | neutral read-only `DebuggerRegister` rows |

The plugin-private mapper follows the FreeBSD amd64 `struct reg` order used by ps5debug-NG: R15..RAX, trap/segment/error fields, RIP, CS, RFLAGS, RSP, and SS. RIP/RSP/RBP receive the neutral semantic roles required by the host. Native offsets, LWP naming, and command framing remain inside the PS5 plugin.

PS5 register writing is intentionally disabled in rev7. The presence of a ps5debug-NG SETREGS command is not treated as proof that its target-side path is safe or correct. A separate hardware verification is required before any future PS5 plugin revision marks those rows writable.

## External Bug Report Review

The new `docs/bug-reports/` policy is applied to the rev6 ps5debug-NG thread-control failure. The report contains the tested process/thread, exact wire result, reproduction, expected/actual behavior, relevant server handler, observed safety behavior, and a cautious suspected-area note. It does not claim a root cause that the live test did not prove.

## Automated Verification Registry Review

The rev7 registry must contain **107 unique checks**. Six names specifically cover the new milestone:

1. `Mock debugger register snapshots and writes`;
2. `Debugger workspace register panel source contract`;
3. `Debugger register value codec source contract`;
4. `Debugger register edit dialog source contract`;
5. `Debugger current instruction Disassembler integration`;
6. `PS5 debugger general register snapshot protocol`.

The PS5 protocol fixture supplies a deterministic 176-byte register block and records the thread id used by GETREGS.

## Final Static Review Results

Final package-lock review was completed against the exact user-supplied `0.1.7.rev6` baseline after all rev7 source and documentation changes were finished. The review produced the following results.

### Diff boundary

- **28 existing files changed** and **9 files added** relative to rev6; **0 files were removed**.
- The nine added files are the rev7 source-review and verification documents, the external ps5debug-NG bug report, the PS5 general-register mapper, the shared register value codec, the register edit dialog XAML/code-behind pair, the register row ViewModel, and `DebuggerRegisterValueEncoding`.
- Production changes remain limited to App debugger/register presentation and Disassembler launch plumbing, Mock debugger register services/version metadata, PS5 debugger GETREGS mapping/version metadata, and the additive Plugin SDK register-encoding contract. Test changes remain limited to the verification registry, existing model-contract coverage, and the PS5 protocol fixture.
- No unrelated Core scanner, export, Memory Viewer, settings, or primary PS5 process/memory/scan implementation was rewritten for rev7.

### Structural and documentation checks

- **PASS — XML/XAML/project structure:** all **22** `.xaml`, `.csproj`, `.props`, and `.targets` files parsed successfully.
- **PASS — Markdown relative links:** all **136** Markdown files were scanned; **43** relative links were resolved and none were broken.
- **PASS — build-output hygiene:** no `bin/` or `obj/` directory is present in the package tree.
- **PASS — current documentation consistency:** `README.md`, `CHANGELOG.md`, the development action plan, debugger architecture, Plugin SDK documentation, Mock documentation, PS5 documentation, rev6 verification record, external bug report, and rev7 verification documents consistently describe rev7 as the current Registers and Stop Context candidate built on the accepted rev6 baseline.
- **PASS — external bug-report policy:** the new ps5debug-NG report contains the reproduced process/thread ids, exact command/status values, expected and observed behavior, relevant upstream handler code, safety observations, and a cautious suspected-area note. It does not name this application or its scripts and does not claim an unproven upstream root cause.

### Verification-registry preservation

- **PASS — rev6 preservation:** all **101** rev6 test names remain registered.
- **PASS — rev7 additions:** exactly the six planned checks were added:
  1. `Mock debugger register snapshots and writes`;
  2. `Debugger workspace register panel source contract`;
  3. `Debugger register value codec source contract`;
  4. `Debugger register edit dialog source contract`;
  5. `Debugger current instruction Disassembler integration`;
  6. `PS5 debugger general register snapshot protocol`.
- **PASS — uniqueness:** the resulting registry contains exactly **107 unique checks** with no duplicate names.
- **PASS — API compatibility regression coverage:** the existing debugger model-contract check now also verifies that the original seven-parameter `DebuggerRegister` constructor remains present after the encoding-aware overload was added.

### Shared architecture preservation

- **PASS — Core byte identity:** all **59** files under `src/TeeKay87.MemoryEngine.Core` are byte-identical to rev6.
- **PASS — Plugin SDK scope:** only three Plugin SDK files changed: `PluginApiInfo.cs`, `DebuggerRegister.cs`, and the added `DebuggerRegisterValueEncoding.cs`. No second register service/model hierarchy was introduced.
- **PASS — constructor compatibility:** the previous seven-parameter `DebuggerRegister` constructor remains available with the same signature and forwards to the encoding-aware overload using `Bytes`. The new metadata is therefore additive rather than replacing the previous compiled constructor surface.
- **PASS — host neutrality:** `DebuggerWindow.xaml`, `DebuggerWindow.xaml.cs`, `DebuggerViewModel.cs`, `DebuggerRegisterViewModel.cs`, and `DebuggerRegisterValueCodec.cs` were checked for PS5/LWP/ptrace/FreeBSD/ps5debug-NG/backend-opcode leakage; none is present. Shared behavior uses capabilities, attached-session services, semantic register roles, and declared value encodings only.

### Production-source review

- **PASS — changed source review:** all **23** changed/added C# or XAML files were re-read after implementation. Required explicit `using` directives are present for introduced BCL/SDK types, and no `TODO`, `FIXME`, or `HACK` marker exists in the changed source.
- **PASS — register lifetime rules:** register snapshots require a current Paused debugger session and selected thread; Continue, Detach, stale-target invalidation, and non-paused state transitions clear snapshot data and current-instruction state. Selected-thread changes while Paused request a fresh snapshot.
- **PASS — write safety:** only rows declared writable by the backend can open the edit flow. Writes are exact-width, occur only while Paused, are followed by a fresh snapshot, and require exact byte-for-byte read-back before success is reported. PS5 rows remain read-only.
- **PASS — numeric codec:** opaque bytes and unsigned little-/big-endian fixed-width values are handled without architecture-specific parsing. Unsigned values with the high bit set remain non-negative, and width overflow is rejected before a backend write.
- **PASS — semantic current instruction:** the host resolves the current instruction exclusively through `DebuggerRegisterRole.InstructionPointer`; it does not test register names such as RIP/PC. A refreshed snapshot without that role clears the previous current-instruction value. Disassembler navigation is target/generation checked before launch.
- **PASS — PS5 GETREGS boundary:** `0xBDBB0008`, the 4-byte selected backend thread id, 176-byte general-register payload, native offsets, and FreeBSD register ordering remain inside the PS5 plugin. The mapper exposes exactly **26** neutral rows and marks RIP/RSP/RBP with the corresponding semantic roles.
- **PASS — command-stream serialization:** PS5 GETREGS uses the existing debugger-owned command gate and connection. Cancellation is checked before command dispatch; once framing begins, the fixed status/payload response is consumed without abandoning the stream mid-command.
- **PASS — rev6 ThreadControl behavior retained:** the PS5 Suspend/Resume client path was not removed or rewritten. The known upstream `CMD_ERROR` result remains documented separately and does not alter Mock ThreadControl or whole-target Pause/Continue behavior.

### Metadata consistency

- **PASS — application:** `0.1.7.rev7`, feature title `Registers and Stop Context`.
- **PASS — Mock plugin:** `1.0.0.rev10`, Plugin API `2.13.0`.
- **PASS — PS5 plugin:** `0.1.0.rev27`, Plugin API `2.13.0`.
- **PASS — public API:** host Plugin API is `2.13.0`; the new value-encoding metadata is additive and the previous `DebuggerRegister` constructor surface is retained.

### ZIP lock

All pre-archive static checks above pass. A complete archive was created once to validate the package operation, then this result was written into the source-review record before the final archive was rebuilt and retested.

- **Archive:** `TK87ME_0.1.7.rev7___Registers-and-Stop-Context.zip`
- **Archive contents:** **385 files**, package-root layout preserved, no `bin/` or `obj/` output
- **Archive integrity:** **PASS** — Python `ZipFile.testzip()` returned no corrupt entry on the pre-lock archive; the final rebuilt archive is retested after this document is included.

The authoritative Windows/.NET gate is intentionally **not** claimed here. Rev7 still requires the user's clean Windows Release build, **107/107** automated result, and the focused runtime/hardware checks in `APP_0.1.7_REV7_VERIFICATION.md`.

## Environment Limitation

The authoritative build/test gate remains the user's Windows/.NET/WPF environment. This source review must not state that **107/107** passed until the user actually runs the packaged revision and reports that result.
