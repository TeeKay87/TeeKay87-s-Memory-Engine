# Application 0.1.7.rev9 Source Review — Extended Register State and Debugger Pane Splitter

## Status

**Static/source review is the package-lock gate. Windows compilation and runtime verification remain external gates.**

This document records the final source-level review for `0.1.7.rev9 - Extended Register State and Debugger Pane Splitter`, built directly from the fully accepted `0.1.7.rev8 - Debugger Workspace Source Contract Fix` package. Rev8 passed **107/107** Windows checks and all **11/11** focused Registers and Stop Context Mock/live-PS5 acceptance steps.

Rev9 must still pass the packaged **108/108** Windows gate and the focused runtime/hardware checklist in [`APP_0.1.7_REV9_VERIFICATION.md`](APP_0.1.7_REV9_VERIFICATION.md) before it is called verified.

## Post-Review Hardware Result

The static package review below predates the final rev9 runtime result and must be read as a historical source review, not as the final transport conclusion.

After packaging, the Windows suite passed **108/108** and focused Mock Gates B-D passed. Live PS5 Gate E then failed: Attach/Pause and thread enumeration completed, but the register refresh remained pending at `Registers 0` with no instruction pointer. The review's earlier conclusion that optional extended reads were safe merely because they were serialized on the dedicated debugger-owner socket was incomplete. A reusable framed stream cannot safely abandon a partially received response, so rev9's post-dispatch uncancelled read could wait indefinitely if an optional backend operation did not complete.

The live result is corrected in `0.1.7.rev10`. The mandatory GETREGS owner-stream path remains unchanged; optional extended reads are capability-gated and moved to bounded disposable probe connections. No upstream defect is claimed solely from the rev9 failure because current ps5debug-NG source contains the relevant handlers.

## Revision Boundary

Rev9 is intentionally limited to the next ordered Debugger milestone and the requested Debugger layout correction:

- reuse the existing neutral `DebuggerRegister` model for backend-provided wide/extended register state;
- add deterministic wide-register fixtures to Mock;
- add read-only PS5 FPU/SIMD, debug-register, and FS/GS-base reads through the dedicated debugger command transport;
- replace the fixed Debugger Registers height with the existing proportional row splitter;
- synchronize current debugger/PS5/Mock documentation and rev9 verification guidance.

No Core source or Plugin SDK source is changed. Plugin API therefore remains `2.13.0`. Breakpoints, watchpoints, call stacks, and stepping remain later `0.1.7` revisions.

## Baseline Acceptance Review

The rev8 verification record was synchronized with the final user-reported result before packaging rev9:

- Windows automated gate: **107/107 PASS**;
- focused Registers and Stop Context acceptance: **11/11 PASS**;
- live PS5 general-register snapshots, Current Instruction -> Disassembler, read-only gating, cleanup, and reconnect paths were accepted;
- individual PS5 Thread Suspend/Resume remains **IMPLEMENTED / BACKEND BLOCKED** on the tested ps5debug-NG backend and is not reopened by rev9.

## Extended Register Architecture Review

### Shared contract reuse

The existing `DebuggerRegister` contract already carries everything rev9 needs: stable id/display name, arbitrary bit width, raw bytes, neutral group, semantic role, writability, and `DebuggerRegisterValueEncoding`. The existing host formatter uses `BigInteger` for unsigned values, so 80-, 128-, and 256-bit rows do not require a second register hierarchy or an architecture-specific host type.

No Plugin API change was introduced merely to represent x87/XMM/YMM state.

### Mock backend

Mock `1.0.0.rev11` retains its existing writable 64-bit General/Control rows and adds deterministic read-only fixtures:

| Id | Display | Width | Group |
| --- | --- | ---: | --- |
| `fp0` | `FP0` | 80-bit | `Floating Point` |
| `vector0` | `V0` | 128-bit | `SIMD` |
| `vector1` | `V1` | 256-bit | `SIMD` |
| `debug0` | `D0` | 64-bit | `Debug` |

Their byte patterns are deterministic and derived from the selected thread's existing register seed. They remain architecture-neutral test fixtures and do not cause Mock to claim x86-64.

### PS5 backend

PS5 `0.1.0.rev28` keeps the verified `GETREGS` path and adds three optional selected-thread reads on the already-isolated debugger command connection:

| Command | Wire id | Success payload |
| --- | --- | ---: |
| GETFPREGS | `0xBDBB000A` | 832 bytes |
| GETDBREGS | `0xBDBB000C` | 128 bytes |
| GETFSGSBASE | `0xBDBB000E` | 16 bytes |

The current ps5debug-NG source/protocol reference was rechecked during the rev9 review. The server handlers accept the same 4-byte LWP/thread id body used by GETREGS. Current upstream returns the fixed 832-byte FPU/YMM block, fixed 128-byte debug-register block, and fixed 16-byte FS/GS-base block after success status.

The FPU mapper keeps native offsets private to the PS5 plugin. It maps the amd64 environment, eight 80-bit x87 stack values, sixteen XMM values, and reconstructs each 256-bit YMM value from its lower XMM half plus the upper xstate half. The debug mapper publishes only DR0-DR3, DR6, and DR7; reserved slots remain private. FSBASE/GSBASE are mapped as two 64-bit neutral rows.

Ordinary backend error/data-null status on an optional extended command omits only that group. A successful response must still contain its complete fixed-size payload; framing/truncation is not silently treated as unsupported capability.

### Write boundary

PS5 rows remain read-only. Rev9 does not expose SETREGS, SETFPREGS, SETDBREGS, or FS/GS write commands. Existing Mock register writing/read-back remains the only writable debugger-register acceptance path in this revision.

## Debugger Pane Splitter Review

`DebuggerWindow.xaml` reuses `ProportionalGridSplitter` and the existing `RowWorkspaceSplitterStyle` instead of adding debugger-specific resize code.

The left Debugger column now uses:

- Threads row: star-sized, practical minimum height 120;
- splitter row: Auto;
- Registers row: star-sized, practical minimum height 120;
- initial equal star values, producing a 50/50 available-height split;
- proportional previous-row limits 0.2 to 0.8;
- `ResizeBehavior=PreviousAndNext` so the selected star ratio is retained as the window changes height;
- capability-driven collapse of both the splitter and Registers row when `RegisterAccess` is unavailable.

The former fixed `Height="210"` Registers layout is absent.

## Automated Verification Review

The test registry contains **108 unique registrations**. All **107** rev8 registration names are preserved, and rev9 adds exactly one new registration:

```text
Debugger thread/register pane splitter source contract
```

Existing tests were extended in place rather than duplicated:

- `Mock debugger register snapshots and writes` now verifies the deterministic 80/128/256-bit fixtures while retaining the writable 64-bit path;
- `PS5 debugger general register snapshot protocol` now verifies GETREGS plus the three rev9 optional read commands, fixed payload sizes, FPU/x87/XMM/YMM offsets, debug-slot filtering, FS/GS mapping, and continued read-only gating.

The PS5 protocol test server records the selected thread id independently for each register command family and supplies deterministic fixed-size payloads.

## Final Static Review Results

The final review was run after all source and documentation synchronization was complete.

### Diff boundary

- **19 existing files changed**, **3 files added**, and **0 files removed** relative to the exact rev8 baseline.
- Added production source: `src/Plugins/TeeKay87.MemoryEngine.Platform.PS5/Ps5ExtendedRegisterMapper.cs`.
- Added documentation: this source review and `APP_0.1.7_REV9_VERIFICATION.md`.
- Changed production source remains limited to App metadata/Debugger layout, Mock debugger register fixtures/version metadata, and PS5 debugger register protocol/session/version metadata.
- Test changes remain limited to the existing verification registry/body and PS5 protocol test fixture.
- No scanner, Saved Addresses, Memory Viewer, Disassembler implementation, export implementation, settings implementation, or theme resource is rewritten for rev9.

### Full-tree review

- **PASS — documentation reread:** all **140 Markdown files** were read after the final rev9 edits.
- **PASS — source/project reread:** all **247 C#/XAML/project/JSON files** were read after the final rev9 edits.
- **PASS — XML/XAML/project structure:** all **22 `.xaml`, `.csproj`, `.props`, and `.targets` files** parsed successfully.
- **PASS — Markdown links:** **45 relative Markdown links** were resolved across the tree; none were broken.
- **PASS — build-output hygiene:** no `bin/` or `obj/` directory is present.
- **PASS — changed-source hygiene:** no `TODO`, `FIXME`, or `HACK` marker was introduced in changed/added C# or XAML source.

### Architecture preservation

- **PASS — Core byte identity:** every file under `src/TeeKay87.MemoryEngine.Core` is byte-identical to rev8.
- **PASS — Plugin SDK byte identity:** every file under `src/TeeKay87.MemoryEngine.PluginSdk` is byte-identical to rev8; public Plugin API remains `2.13.0`.
- **PASS — host neutrality:** the changed Debugger XAML contains no PS5/LWP/ptrace/FreeBSD/x86 register implementation logic; it consumes only capability-driven host ViewModel state and the shared splitter control.
- **PASS — plugin ownership:** all FreeBSD/amd64 register offsets and ps5debug-NG command ids remain under the PS5 plugin; Core/WPF receive neutral rows only.
- **PASS — existing register semantics:** RIP/RSP/RBP semantic behavior still comes from the verified `Ps5GeneralRegisterMapper`; rev9 only appends optional neutral rows.
- **PASS — existing Mock write path:** the ordinary writable General/Control rows and exact read-back flow remain unchanged; new wide rows are additive and read-only.

### Transport and safety review

- **PASS — debugger command serialization:** all three new PS5 reads use the existing debugger-owned `_commandGate` and dedicated debugger command socket.
- **PASS — fixed response draining:** after command dispatch, successful fixed-size register payloads are consumed fully before control returns, preserving framing.
- **PASS — optional fallback boundary:** only ordinary error/data-null statuses return a missing optional group; unexpected status and short successful payloads still fail.
- **PASS — no PS5 write exposure:** `WriteRegisterAsync` still throws `NotSupportedException`; no SET-register command was added to the client surface.
- **PASS — existing thread blocker preserved:** rev9 does not rewrite the already documented PS5 Suspend/Resume path or claim that the upstream blocker is fixed.

### Metadata and documentation consistency

- **PASS — application metadata:** `0.1.7.rev9 - Extended Register State and Debugger Pane Splitter` is centralized in `AppInfo`.
- **PASS — Mock metadata:** `1.0.0.rev11`, Plugin API `2.13.0`.
- **PASS — PS5 metadata:** `0.1.0.rev28`, Plugin API `2.13.0`.
- **PASS — current documentation:** README, CHANGELOG, development plan, Debugger architecture, Mock README, PS5 README, PS5 protocol mapping, and PS5 native-scan identity block agree on the current host/plugin/API versions and rev9 scope.
- **PASS — rev8 acceptance history:** the rev8 verification document now records the final 107/107 plus 11/11 acceptance rather than leaving that accepted baseline marked pending.
- **PASS — no external bug-report trigger:** no new external-tool defect was discovered while implementing rev9. The existing ps5debug-NG individual-thread Suspend report remains unchanged.

### Package lock

- **Archive name:** `TK87ME_0.1.7.rev9___Extended-Register-State-and-Debugger-Pane-Splitter.zip`.
- **Archive entries:** **390 files**.
- **Archive integrity:** **PASS** — the final ZIP is checked with `ZipFile.testzip()` after rebuilding from the locked tree.
- **Package layout:** repository contents are at the ZIP root; no extra wrapper directory and no `bin/`/`obj/` output are included.

## Environment Limitation

The authoritative build/runtime gate remains the user's Windows/.NET/WPF environment. This source review does not claim that **108/108** passed. Rev9 becomes verified only after the packaged tests and focused runtime/hardware checks in `APP_0.1.7_REV9_VERIFICATION.md` are completed successfully.
