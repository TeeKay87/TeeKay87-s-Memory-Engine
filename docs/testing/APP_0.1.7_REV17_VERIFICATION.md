# Application 0.1.7.rev17 Verification — Call Stack, Call Frames and Stepping

## Status

**SUPERSEDED AFTER AUTOMATED GATE.** The clean Windows automated suite completed successfully at **124/124 PASS**. Before the focused Mock Call Stack/Stepping runtime gates began, the first Debugger UI inspection exposed the new upper-right WPF tab surface using the operating-system default white `TabControl`/`TabItem` presentation under Dimmed theme. Runtime acceptance stopped at that point. The corrective presentation change is carried by `0.1.7.rev18`; rev17 is not marked runtime-verified.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev17` |
| Feature | `Call Stack, Call Frames and Stepping` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev34` |
| Automated checks | `124` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev17 ZIP into a clean directory.
2. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

3. Confirm the final line is exactly:

```text
All 124 checks passed.
```

Do not skip, rename away, or remove a failing registration to reach the expected count. The registry must contain 124 unique names and 124 unique method targets.

**Result:** PASS — Windows reported `All 124 checks passed.` before runtime/UI verification began.

## Runtime Blocker Found Before Gate B

The first Debugger screenshot taken before executing the focused Gate B steps showed the Breakpoints / Watchpoints + Call Stack `TabControl` using the system-default white tab strip/content chrome in the Dimmed theme. This violates the project theme system and makes header text difficult to read. No further rev17 runtime gates were accepted after this observation. The fix is intentionally isolated to shared WPF tab presentation in rev18.

## Gate B — Debugger Workspace Layout and Capability Gating

Use the Mock target first.

1. Open Debugger and confirm the execution controls are arranged on two rows:
   - row 1: State, Attach, Pause, Continue, Detach;
   - row 2: Step Into, Step Over, Step Out, Run to....
2. Confirm Threads and Registers remain simultaneously visible on the left after Attach -> Pause.
3. Confirm the upper-right area contains tabs for **Breakpoints / Watchpoints** and **Call Stack**.
4. Confirm Events remains visible below and owns its own Events/count/Clear Events header.
5. Drag the new vertical splitter between the left and right workspaces in both directions. It must preserve practical minimum widths and the existing proportional limits.
6. Drag the existing Threads/Registers and upper-right/Events splitters and confirm their established behavior is unchanged.
7. Resize the Debugger down toward its minimum size and back up. Controls must remain usable without introducing an always-visible extra pane or broken layout.
8. Confirm Step controls are enabled only while the current debugger is Paused with a selected thread and the active plugin advertises `StepExecution`.
9. Confirm Call Stack is shown only when the active plugin advertises `CallStack`.

## Gate C — Mock Call Stack and Frame Navigation

1. Connect Mock, set `TestGame.exe` as Active Target, open Debugger, Attach, and Pause.
2. Select thread **Main** and open the Call Stack tab.
3. Confirm three deterministic frames are shown in increasing frame-index order.
4. Confirm the grid exposes the compact primary columns:
   - `#`;
   - Instruction Address;
   - Module;
   - Symbol.
5. Select each frame and confirm the detail area updates Stack Pointer, Frame Pointer, and Return Address appropriately.
6. Select another debugger thread while still Paused. Confirm Registers and Call Stack both refresh for the new thread rather than retaining Main-thread stop context.
7. Click **Refresh** in Call Stack and confirm the selected frame index is preserved when possible.
8. From a selected frame, open **Disassembler** and confirm the existing Disassembler opens at that frame's Instruction Address.
9. From a selected frame, open **Memory Viewer** and confirm the existing Memory Viewer opens at that same Instruction Address.
10. Continue. Call Stack and Registers must clear while the target is Running.
11. Pause again. Frames must repopulate from the new current stop context.

No debugger-local memory viewer or disassembler should appear; both actions must reuse the existing modeless tools and their current target-generation safety checks.

## Gate D — Mock Step Into

1. With Mock attached and Paused, select a thread and note its current instruction address.
2. Click **Step Into**.
3. Confirm the debugger transitions through Running and returns to Paused automatically.
4. Confirm one `StepCompleted` event is emitted for the selected thread.
5. Confirm the selected thread's Instruction Pointer advances through the deterministic Mock instruction fixture.
6. Confirm Registers and Call Stack repopulate after the completed step.
7. Repeat several times to verify event ordering and stop-context refresh remain stable.
8. Confirm Step Into is unavailable while Running, Detached, or without a selected thread.

## Gate E — Mock Step Over

Exercise both branches of the host-composed operation.

### Non-call instruction

1. Pause on a deterministic Mock instruction that is not a Call.
2. Click **Step Over**.
3. Expected: the host resolves the current instruction through the existing Disassembler and uses native Step Into.
4. Confirm the debugger returns to Paused with the next deterministic instruction context.

### Call instruction

1. Pause on a deterministic Mock Call instruction in the existing code fixture.
2. Click **Step Over**.
3. Expected: the host computes the call's fall-through address, creates or reuses a software execute breakpoint there, and continues.
4. Confirm execution returns to Paused at the fall-through address.
5. If the host created a temporary breakpoint, confirm it disappears after the attributed hit.
6. Confirm no duplicate platform-specific step-over implementation or stale temporary row remains.

## Gate F — Mock Step Out

1. Pause Mock and select a Call Stack frame that has a Return Address.
2. Click **Step Out**.
3. Confirm the operation uses that selected frame's Return Address as the run-to target.
4. Confirm execution resumes and returns to Paused through the existing software-breakpoint mechanism.
5. Confirm a temporary breakpoint created solely for the operation is removed after its hit.
6. Select a frame with no Return Address and confirm Step Out is disabled.

## Gate G — Mock Run to Address

1. Pause Mock and click **Run to...**.
2. Confirm the themed dialog uses hexadecimal input, accepts an optional `0x` prefix, and rejects non-hex/overflow input.
3. Run to a valid executable Mock code address.
4. Confirm the target continues and returns to Paused at the requested address.
5. Confirm a temporary software execute breakpoint is created when no matching breakpoint exists and is removed after the hit.
6. Add an enabled persistent Software/Execute breakpoint at a different valid destination and Run to that exact address. Confirm the existing enabled breakpoint is reused and remains persistent after the hit.
7. Disable an existing Software/Execute breakpoint and request Run to that same address. Confirm the operation is rejected with a clear message rather than silently enabling or replacing the user's breakpoint.
8. Confirm a data watchpoint at the same numeric address is not treated as the run-to execute breakpoint.

## Gate H — Mock Lifetime and Regression

1. With Call Stack populated, Detach and confirm frames/registers/threads/breakpoints are cleaned according to the established debugger lifecycle.
2. Reattach and confirm no call-frame or temporary run-to state leaked from the old attachment.
3. Close the Debugger while attached, reopen it, and attach again; the new window must start from a fresh session.
4. Disconnect/reconnect Mock and confirm the old window cannot operate against the new connection generation.
5. Change Active Target/lifetime state through the existing supported workflow and confirm stale frame navigation and step commands are rejected.
6. Smoke-test persistent/temporary software breakpoints and hardware watchpoints to confirm rev16 behavior remains intact.
7. Smoke-test Register edit/write on Mock to confirm the existing writable-register acceptance path still works.

## Gate I — Live PS5 Call Stack

Use a normal known-good `eboot.bin` target and begin with no unnecessary breakpoints/watchpoints enabled.

1. Connect to PS5, set the intended game process as Active Target, open Debugger, Attach, and Pause.
2. Select the stopped/current thread and open the Call Stack tab.
3. Confirm at least the current frame appears without transport hang or framing error.
4. When the backend provides caller frames, confirm frame indexes are ordered and Instruction Address / Stack Pointer / Frame Pointer / Return Address values are plausible for the selected stopped thread.
5. Confirm module text appears when the frame address falls inside a mapped module/region known to the plugin; missing module/symbol text is acceptable where the backend/map does not provide it.
6. Select another enumerated thread while Paused and refresh Call Stack. Confirm the request follows the selected thread and does not leave stale frames from the previous selection.
7. Open a frame in Disassembler and confirm the existing Disassembler loads that frame address.
8. Open a frame in Memory Viewer and confirm the existing Memory Viewer loads that frame address when the target supports the current Memory Viewer gate.
9. Continue and confirm the Call Stack clears while Running. Pause again and confirm it can be rebuilt.

The server-side stack walker is frame-pointer based. A short stack, one-frame result, or naturally terminated chain is not by itself a failure if the current code path does not maintain a longer usable RBP chain. The acceptance requirement is safe, correctly framed behavior without fabricated caller data.

## Gate J — Live PS5 Native Step Into

1. Attach and Pause at a safe instruction location with a selected current thread.
2. Note the current Instruction Pointer.
3. Click **Step Into** once.
4. Confirm the command returns promptly and the target transitions to Running.
5. Confirm the next matching asynchronous stop is reported as `StepCompleted` for the stepped thread and the Debugger returns to Paused.
6. Confirm Instruction Pointer changes to the resulting instruction and Threads/Registers/Call Stack refresh normally.
7. Repeat several times at safe locations.
8. Confirm no debugger command-stream framing error, duplicate unexpected stop, or stale step state occurs.

If an already-managed software breakpoint or hardware watchpoint genuinely triggers during a pending step, that managed hit may take precedence over `StepCompleted`. This is expected and should not be relabeled as a step completion.

## Gate K — Live PS5 Step Over

Test both a non-call instruction and a known safe Call instruction.

1. At a non-call instruction, **Step Over** should behave as one native Step Into and return to Paused normally.
2. At a Call instruction, note the instruction length/fall-through address shown by the existing Disassembler.
3. Click **Step Over**.
4. Confirm the target resumes and stops at the call's fall-through address through the existing software-breakpoint path.
5. Confirm a temporary breakpoint created for the operation is cleaned after the hit.
6. Confirm an already-enabled execute breakpoint at that address can be reused without altering its persistent lifetime.

Choose a call site known to return normally. Do not use an unsafe/non-returning call solely to satisfy this gate.

## Gate L — Live PS5 Step Out

1. Pause in a context where the selected current Call Stack frame has a plausible non-zero Return Address.
2. Select that frame and note the Return Address.
3. Click **Step Out**.
4. Confirm execution resumes and stops at that return address through the existing run-to/software-breakpoint path.
5. Confirm temporary breakpoint cleanup and normal Paused stop-context refresh.

If the current backend stack walk produces no usable Return Address at the chosen stop, select another safe stop context rather than manufacturing an address manually for this specific gate.

## Gate M — Live PS5 Run to Address

1. Pause at a known safe executable location.
2. Choose a nearby executable address that the target will naturally reach.
3. Use **Run to...** and enter that address.
4. Confirm the target resumes and stops at the requested address.
5. Confirm the temporary Software/Execute breakpoint is removed after its attributed hit.
6. Repeat using an already-enabled persistent software breakpoint and confirm it is reused and remains persistent.
7. Confirm an existing disabled software breakpoint at the requested address blocks Run to Address instead of being changed silently.

## Gate N — PS5 Cleanup, Stale Session, and Regression

After the focused rev17 operations, perform the following regression smoke:

- manual Pause/Continue still works;
- Threads enumerate and selection remains stable;
- the established paused general/optional Register surface refreshes without transport hang;
- paused `GETDBREGS` remains suppressed;
- a known-good persistent Software/Execute breakpoint still installs and hits;
- a temporary Software/Execute breakpoint still cleans up after its hit;
- a known-good Hardware Write or ReadWrite watchpoint still hits and preserves instruction-vs-watched-address semantics;
- paused breakpoint/watchpoint Disable/Remove safety remains intact;
- Detach/Reattach clears Call Stack, pending step state, and temporary run-to state;
- Debugger close/reopen clears attachment-owned state;
- Disconnect/Reconnect invalidates the old connection generation and leaves the new debugger transport usable.

## Acceptance Criteria

Rev17 is accepted only after:

- Windows reports **124/124 PASS**;
- the approved two-row/tabbed/split Debugger layout works at normal and minimum practical window sizes;
- Mock Call Stack, frame navigation, native Step Into, host-composed Step Over/Step Out/Run to Address, cleanup, and regression gates pass;
- live PS5 server-side Call Stack is safely usable for the selected paused thread without command-stream corruption or hangs;
- live PS5 native Step Into produces correct asynchronous `StepCompleted` behavior;
- live PS5 Step Over, Step Out, and Run to Address operate through the verified Disassembler/call-frame/software-breakpoint composition and clean temporary state correctly;
- stale-session/connection-generation protection remains effective;
- software breakpoint, hardware watchpoint, Threads, Registers, and debugger transport regressions remain clean.

Rev17 is superseded and must not be relabeled VERIFIED. Its successful 124/124 automated result remains recorded here. The focused runtime gates carried forward through the corrective revisions. Rev24 later completed the redesigned workspace and full Mock Call Stack/stepping runtime cycle. Rev25 then passed its logical software-breakpoint stop-context/Step-Into correction but exposed breakpoint-patched instruction classification when Step Over was attempted on a persistent `call`. Rev26 is the current corrective candidate for that Step Over blocker; Integration, Export and Finalization is therefore scheduled for **0.1.7.rev27** after rev26 acceptance.
