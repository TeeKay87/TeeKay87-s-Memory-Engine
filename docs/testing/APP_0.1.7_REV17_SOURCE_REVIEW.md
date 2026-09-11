# Application 0.1.7.rev17 Source Review — Call Stack, Call Frames and Stepping

## Scope

Rev17 is built directly from the supplied and fully verified `0.1.7.rev16` Hardware Watchpoints package. Rev16 completed the clean Windows automated suite at **118/118 PASS** and the complete focused Mock/live-PS5 acceptance cycle before rev17 work began.

The revision combines the previously separate Call Stack/Call Frames and Stepping milestones because the stepping workflows depend directly on selected-thread stop context, neutral call frames, the verified Disassembler, and the existing temporary software-breakpoint lifecycle. Debugger export and final block-wide integration remain intentionally deferred to rev18.

## Mandatory Pre-change Review

Before rev17 production edits, the supplied rev16 tree was reviewed across:

- root `README.md` and `CHANGELOG.md`;
- every Markdown document under `docs/`;
- the complete application, Core, Plugin SDK, Mock plugin, PS5 plugin, tests, XAML, project files, and supporting tools;
- existing `IDebuggerCallStackService`, `DebuggerStackFrame`, `IDebuggerStepService`, `DebuggerStepKind`, `TargetCapabilities.CallStack`, and `TargetCapabilities.StepExecution` contracts;
- the Debugger window/ViewModel, existing Threads/Registers splitters, Breakpoints/Watchpoints manager, event presentation, stop-context refresh, and stale-session rules;
- the existing Core Disassembler and modeless Memory Viewer navigation paths;
- Mock debugger thread/register/breakpoint/watchpoint fixtures and target lifetime;
- PS5 debugger command transport, asynchronous interrupt parser, memory-map cache, register service, software-breakpoint/watchpoint cleanup rules, and protocol test server;
- current ps5debug-NG `PROTOCOL.md`, `common/include/protocol.h`, `debugger/source/proc.c`, and `debugger/source/debug.c` for the stack-read and native-step commands.

The review confirmed that no new public debugger contract was required. Rev17 therefore remains on Plugin API `2.15.0` and consumes the neutral contracts already established by the debugger foundation.

## Host Call Stack Integration

`DebuggerViewModel` now owns a neutral `CallFrames` collection and selected-frame state. The host requires both `TargetCapabilities.CallStack` and an attached `IDebuggerCallStackService` before issuing a request.

Call-frame lifetime follows the same stop-context safety model as Registers:

- the debugger must be Paused;
- a selected thread must still match the request;
- the plugin/process/connection generation must still be current;
- changing selected thread while Paused refreshes Registers and Call Stack through the same stop-context path;
- Continue/Running, Detach, target invalidation, disconnect/reconnect, and cleanup clear the frame snapshot.

Frames are sorted by neutral frame index for presentation. Selection is restored by frame index when possible after refresh.

## Debugger Workspace Layout

The revision does not add another permanently visible full-size data pane.

- Threads and Registers remain on the left and retain their established proportional row splitter.
- Breakpoints / Watchpoints and Call Stack share the upper-right workspace through a tab control.
- Events remains in the lower-right workspace and now owns its Events/count/Clear Events header.
- A new vertical `ProportionalGridSplitter` separates the left and right workspaces, starting at approximately 36/64 and reusing the established relative movement limits and shared theme style.
- The right-side upper workspace and Events retain the established 13/7 starting split.
- Attach/Pause/Continue/Detach remain on the first execution row; Step Into/Step Over/Step Out/Run to... use a second execution row.

The Call Stack grid exposes only frame index, instruction address, module, and symbol. Stack Pointer, Frame Pointer, and Return Address appear in a selected-frame detail area. The selected frame can open its instruction address in the existing Disassembler or Memory Viewer rather than introducing debugger-local duplicates.

## Host Stepping Composition

The host deliberately treats Step Into as the backend-native primitive.

### Step Into

`IDebuggerStepService.StepAsync(DebuggerStepKind.Into, selectedThreadId, ...)` is invoked only for a current Paused session with a selected thread. The existing rapid Continue-to-Paused stop-context deferral is reused so a fast native completion event is not lost while the initiating command is still leaving its busy state.

### Step Over

The host uses the existing Core Disassembler context reader at the current semantic instruction pointer. If the resolved current instruction is not a Call, Step Over delegates to native Step Into. If it is a Call, the host calculates `instruction.Address + instruction.Length` using checked arithmetic and invokes the shared Run-to path for that fall-through address.

### Step Out

Step Out requires a selected frame with a non-null neutral Return Address. That address is passed to the same Run-to path; no x86-specific stack-frame logic appears in WPF/Core.

### Run to Address

Run to Address requires current Paused state plus StepExecution and Breakpoints capability. The host searches the existing breakpoint presentation for a matching Software/Execute address:

- an enabled matching breakpoint is reused;
- a disabled matching breakpoint blocks the operation instead of silently changing user state;
- otherwise a one-byte temporary Software/Execute breakpoint is added through the existing breakpoint service and the normal Continue path is used.

The new `RunToAddressDialog` is host-generic and validates hexadecimal `ulong` input with optional `0x` prefix. Target-specific executable-range validation remains in the breakpoint backend.

## Mock Backend

Mock advances from `1.0.0.rev14` to `1.0.0.rev15`, remains on Plugin API `2.15.0`, and advertises `CallStack` plus `StepExecution`.

`MockDebuggerSession` now implements `IDebuggerCallStackService` and `IDebuggerStepService`:

- Call Stack is Paused-only and validates the neutral thread id;
- each known thread exposes three deterministic frames tied to that thread's current RIP/RSP/RBP-style neutral fixtures and the existing synthetic code region;
- module/symbol text is deterministic for host/UI verification;
- native Step Into is the only backend step kind accepted;
- Step Into transitions to Running, emits the normal resumed event, advances the chosen synthetic instruction pointer, and completes after a deterministic short delay with `StepCompleted` and Paused state;
- native Over/Out are rejected because rev17 verifies the shared host composition instead.

Existing Mock software breakpoints, hardware watchpoints, writable-register behavior, scanner, memory, Memory Viewer, and Disassembler fixtures are retained.

## PS5 Server-side Call Stack

PS5 advances from `0.1.0.rev33` to `0.1.0.rev34`, remains on Plugin API `2.15.0`, and advertises `CallStack` plus `StepExecution`.

The PS5 implementation keeps the stack walk entirely plugin-owned:

1. The selected Paused thread's existing 176-byte general-register snapshot is read to obtain RIP, RBP, and RSP.
2. The debugger command client sends `CMD_PROC_READ_STACK` (`0xBDAA0023`) with the packed 24-byte `{ uint32 pid; uint64 rbp; uint64 rsp; uint32 depth; }` request.
3. Depth is capped at the upstream maximum of 64.
4. A successful response supplies a `uint32` payload length followed by a payload whose first field is frame count.
5. Each frame has a 44-byte fixed header containing current RBP/RSP, saved RBP, return address, flags, locals length, and code length, followed by optional locals/code blocks.
6. The client validates outer length, frame count, header availability, maximum locals/code sizes, truncation, and final payload consumption before returning frames.
7. Variable locals/code bytes are not exposed because the current neutral frame model does not use them.
8. The top neutral frame uses the selected current RIP; later instruction addresses use the preceding backend return address. Module names are resolved through the existing current-process memory-map data where possible.
9. A safely empty backend walk falls back only to the selected thread's known current frame; it does not fabricate caller frames.

Current upstream definitions reviewed for rev17 match the implementation: request size 24, maximum depth 64, maximum locals length `0x1000`, and code window length 200.

## PS5 Native Step Into

The PS5 debugger command client maps:

```text
CMD_DEBUG_STEP        = 0xBDBB0012
CMD_DEBUG_STEP_THREAD = 0xBDBB0013
```

The thread command carries one 32-bit backend thread id; the process command has no body. The current host normally supplies the selected thread and therefore uses the thread variant.

Before stepping, the session flushes pending software-breakpoint and hardware-watchpoint disables through the existing verified cleanup paths. It records the pending step/thread, sends the native command, enters Running, and emits a neutral resumed event. The matching asynchronous signal-5 stop becomes `StepCompleted` for the pending step. Managed software-breakpoint/watchpoint attribution is evaluated before StepCompleted so a known managed hit retains its correct event kind. Manual Pause, Continue, Detach, disposal, and cleanup clear pending step state as appropriate.

No PS5 SET-register command, x86-specific step enum, or wire command is added to Core/WPF.

## Verification-code Changes

The automated registry increases from **118 to 124 unique registrations** with six focused additions:

1. Mock deterministic call-stack service;
2. Mock native Step Into;
3. Debugger Call Stack/stepping workspace source contract;
4. Debugger Run to Address source contract;
5. PS5 server-side call-stack protocol;
6. PS5 native Step Into protocol and asynchronous completion.

The PS5 protocol test server now supplies deterministic `CMD_PROC_READ_STACK` responses and captures the native step commands needed by those tests. Existing registrations are retained.

## Version Domains

| Component | rev17 value | Reason |
| --- | --- | --- |
| Application | `0.1.7.rev17` | Call Stack/frames, stepping controls/composition, workspace layout, and docs changed |
| Feature | `Call Stack, Call Frames and Stepping` | Current revision scope |
| Plugin API | `2.15.0` | Existing call-stack/step contracts are sufficient; no API bump |
| Mock plugin | `1.0.0.rev15` | Deterministic call-stack and native Step Into added |
| PS5 plugin | `0.1.0.rev34` | Server-side stack-read and native step transport/mapping added |
| Automated checks | `124` | Six focused registrations added |

## Static Package-preparation Requirements

Before packaging, confirm:

- `AppInfo` reports `0.1.7.rev17` and `Call Stack, Call Frames and Stepping`;
- Plugin API remains `2.15.0`;
- Mock reports `1.0.0.rev15` / API `2.15.0` and advertises CallStack/StepExecution with matching services;
- PS5 reports `0.1.0.rev34` / API `2.15.0` and advertises CallStack/StepExecution with matching services;
- platform-specific stack/wire/step logic remains inside the corresponding plugins;
- the host reuses Core Disassembler, existing Memory Viewer navigation, and the existing breakpoint manager rather than introducing duplicate services;
- the test registry contains 124 unique registrations and every target method exists;
- XAML/XML/project and JSON files parse;
- Markdown relative links resolve;
- no `bin`, `obj`, `.vs`, temporary/editor artifacts, or merge-conflict markers are packaged;
- no historical verified revision is rewritten as if it were rev17;
- the final ZIP passes CRC/integrity and a file-for-file hash comparison against the locked source tree.

Native .NET build/WPF execution is a Windows verification gate and is not claimed by static source/package preparation in an environment without a .NET SDK.

## Package Target

The archive is named `TK87ME_0.1.7.rev17___Call-Stack-Call-Frames-and-Stepping.zip`, contains the complete project tree without an extra wrapper directory, and must pass ZIP CRC/integrity plus file-for-file hash comparison against the locked work tree.

## Final Static Review Result

The rev17 source tree completed the final static package-preparation review before archiving.

- The complete tree contains **417 files**. Every file was readable during the review.
- The rev17 delta against the verified rev16 source tree contains **6 added files**, **27 changed files**, and **0 removed files**.
- All **24** XML-family files (`.xaml`, `.csproj`, `.props`, `.targets`, and `.xml`) parse successfully.
- All **3** JSON files parse successfully.
- All **159** Markdown files were included in the documentation review. The **53** relative Markdown links found by the package audit resolve to existing paths.
- The automated test registry contains exactly **124 unique test names** and **124 unique method targets**, with no missing registered method.
- Application metadata resolves to `0.1.7.rev17` with feature title `Call Stack, Call Frames and Stepping`.
- Mock metadata resolves to `1.0.0.rev15` / Plugin API `2.15.0`, and the target advertises `CallStack` plus `StepExecution`.
- PS5 metadata resolves to `0.1.0.rev34` / Plugin API `2.15.0`, and the target advertises `CallStack` plus `StepExecution`.
- The **18** added or changed C# files have balanced top-level raw brace counts. Files without `using` directives do not reference namespaces that require imports; all changed scripts that require imports include them.
- No `bin`, `obj`, `.vs`, `__pycache__`, temporary/editor backup files, rejected patch files, or unresolved merge-conflict markers are present in the package tree.
- No stale documentation wording was found that still describes rev16 as the current candidate or keeps Call Stack/stepping assigned to separate current rev17/rev18 milestones.

The package-preparation environment does not provide a .NET SDK or a Windows WPF runtime, so this static review does **not** replace Gate A or the focused runtime gates in `APP_0.1.7_REV17_VERIFICATION.md`. Rev17 remains a **CANDIDATE** until the clean Windows automated suite reports `All 124 checks passed.` and the documented Mock/live-PS5 acceptance gates pass.
