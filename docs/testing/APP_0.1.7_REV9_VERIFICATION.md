# Application 0.1.7.rev9 Verification — Extended Register State and Debugger Pane Splitter

## Status

**Superseded after live PS5 Gate E failure.** Rev9 is not the accepted PS5 extended-register transport revision.

Recorded acceptance result:

| Gate | Result |
| --- | --- |
| A — Windows build/automated verification | **PASS — 108/108** |
| B — Threads/Registers splitter | **PASS** |
| C — Mock extended register presentation | **PASS** |
| D — Mock splitter/lifecycle regression | **PASS** |
| E — Live PS5 extended register enumeration | **FAIL** |
| F-I | **Not run after Gate E failure** |

During Gate E, Attach and global Pause succeeded on real PS5 hardware. The target was paused, the Debugger UI remained responsive, and the Threads pane loaded **82** stopped threads. The register refresh did not complete: the Registers pane remained at `0`, the instruction pointer stayed unavailable, and the workflow never reached the extended-register enumeration checks.

Source review after the failure identified a client-side robustness gap in rev9. The mandatory GETREGS call was followed by optional GETFPREGS/GETDBREGS/GETFSGSBASE calls on the reusable debugger-owner command connection. Once an optional command had been sent, rev9 deliberately finished that framed response with `CancellationToken.None`; if the backend did not complete the response, the entire register refresh could therefore wait indefinitely. This observation does **not** by itself prove a ps5debug-NG defect. Current upstream source contains handlers for those commands and an error response for unknown debugger opcodes.

The corrective implementation is `0.1.7.rev10 - PS5 Extended Register Transport Guard`. Rev10 retains the rev9 splitter and Mock extended-register work, preserves mandatory GETREGS on the verified owner stream, and moves eligible optional reads to bounded disposable probe connections.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev9` |
| Feature | `Extended Register State and Debugger Pane Splitter` |
| Plugin API | `2.13.0` |
| Mock plugin | `1.0.0.rev11` |
| PS5 plugin | `0.1.0.rev28` |
| Automated checks | `108` |

## Revision Scope

Rev9 deliberately changes only two parts of the active Debugger feature block:

1. backend-provided extended register rows using the already-public neutral `DebuggerRegister` model;
2. the left Debugger layout so Threads and Registers share available height through the existing proportional splitter.

The established Attach/Pause/Continue/Detach lifecycle, debugger event model, current-instruction Disassembler integration, general-register mapping, Mock writable-register flow, scanner, Saved Addresses, Memory Viewer, Disassembler, export, settings, and theme behavior are regression-sensitive and must remain intact.

PS5 register writing is not part of this revision. Individual PS5 Thread Suspend/Resume also remains **IMPLEMENTED / BACKEND BLOCKED** on the currently tested ps5debug-NG backend and does not need to be forced again for rev9 acceptance.

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev9 ZIP to a clean directory.
2. Build the solution in the normal Windows Release environment.
3. Run the verification executable.
4. Confirm the new splitter registration reports:

```text
PASS  Debugger thread/register pane splitter source contract
```

5. Confirm the extended register registrations report:

```text
PASS  Mock debugger register snapshots and writes
PASS  PS5 debugger general register snapshot protocol
```

6. Confirm the final line is:

```text
All 108 checks passed.
```

No previous check may be missing. Rev9 adds one new registry entry and extends the existing Mock/PS5 register checks rather than replacing the previously verified coverage.

## Gate B — Debugger Threads/Registers Splitter

Use Mock first because it always advertises both Thread Enumeration and Register Access.

1. Connect to Mock, set `TestGame.exe` as Active Target, and open Debugger.
2. Resize the Debugger window vertically so there is clearly more space than the minimum window height.
3. Confirm **Threads** and **Registers** initially receive approximately equal available height: **50/50**.
4. Drag the horizontal splitter upward and downward.
5. Confirm both panes resize smoothly and the splitter cannot push either pane out of the workspace.
6. At a reasonably tall window size, confirm the splitter stops at approximately the intended **20/80** and **80/20** relative limits.
7. Make one pane larger, then increase and decrease the Debugger window height. Confirm both panes scale with the window while retaining the selected proportion rather than restoring a fixed Registers height.
8. Reduce the window toward its minimum height. Confirm the practical pane minimums keep both lists usable even if those fixed usability minimums become stricter than the nominal ratio limit.
9. Confirm the splitter uses the normal themed handle/margins in Light, Dimmed, and Dark themes and no old bright/default WPF surface appears.
10. Confirm Threads controls and Register controls remain fully visible at both splitter extremes.

Expected result: no fixed-height Registers pane remains; the two panes behave as one proportional resizable workspace.

## Gate C — Mock Extended Register Presentation

1. Attach to Mock and globally Pause.
2. Confirm the existing General/Control rows remain present and that RIP/RSP/RBP semantic behavior is unchanged.
3. Confirm the additional read-only rows are present:

| Row | Expected width | Expected group |
| --- | ---: | --- |
| `FP0` | 80-bit | `Floating Point` |
| `V0` | 128-bit | `SIMD` |
| `V1` | 256-bit | `SIMD` |
| `D0` | 64-bit | `Debug` |

4. Confirm the wide values display as complete unsigned hexadecimal values without truncation, negative signs, overflow text, or layout corruption.
5. Select each wide row and use **Refresh** repeatedly. Confirm selection remains stable and no duplicate rows accumulate.
6. Select a writable ordinary General register and repeat one known-good Edit -> Write & Verify operation. Confirm rev8 writable-register behavior still succeeds.
7. Confirm **Edit...** is unavailable for `FP0`, `V0`, `V1`, and `D0`.
8. Continue and confirm the register snapshot clears as before.
9. Pause again and confirm the complete snapshot is rebuilt rather than reusing stale row objects from the previous stop context.

## Gate D — Mock Splitter/Lifecycle Regression

1. While Paused, select a non-default thread and a register row.
2. Move the splitter to a non-50/50 position.
3. Refresh Threads and Registers several times.
4. Confirm thread/register selection preservation remains correct and the splitter position is not reset by data refresh.
5. Detach, reattach, Pause, and confirm a fresh snapshot loads.
6. Close the Debugger with **X**, reopen it, and confirm the new window starts from the defined 50/50 layout rather than inheriting stale session data.
7. Disconnect/reconnect Mock and confirm an old Debugger window remains stale/unusable exactly as in rev8.

## Gate E — Live PS5 Extended Register Enumeration

Use the same known-good live PS5/ps5debug-NG setup that passed rev8.

1. Connect to PS5, set `eboot.bin` as Active Target, open Debugger, and Attach.
2. Globally Pause the target.
3. Confirm the existing general-register rows are still correct, including RIP/RSP/RBP and the Current Instruction display.
4. Confirm the Registers table additionally contains the extended groups returned by the current backend:
   - FPU environment/control rows;
   - `ST0`-`ST7` as 80-bit rows;
   - `XMM0`-`XMM15` as 128-bit rows;
   - `YMM0`-`YMM15` as 256-bit rows;
   - `DR0`-`DR3`, `DR6`, `DR7`;
   - `FSBASE`, `GSBASE`.
5. With all three optional backend commands supported, the complete current snapshot contains **82 rows**. If a whole optional group is absent, record which group is missing and the debugger status; an ordinary backend unsupported/error response is allowed to omit only that group, but malformed partial data or framing errors are not acceptable.
6. Inspect several wide rows. Confirm values are complete fixed-width hexadecimal data and the UI remains responsive/usable horizontally and vertically.
7. Confirm no reserved debug-register rows such as DR4/DR5 appear.
8. Confirm every PS5 register remains read-only and **Edit...** is unavailable.

## Gate F — Live PS5 Refresh and Stop Context

While the target remains Paused:

1. Select a specific extended register and click **Refresh** several times.
2. Confirm row count and groups remain stable for the same backend capabilities and no duplicate rows appear.
3. Confirm the selected row remains selected when its id still exists.
4. Select another thread and confirm a fresh complete snapshot is requested for that thread.
5. Return to the original thread and confirm another fresh snapshot loads.
6. Continue the target and confirm all register rows/current-instruction state clear.
7. Pause again and confirm general plus extended rows are rebuilt from the new stopped context.
8. Confirm Refresh itself does not create debugger events, change execution state, or produce transport/framing errors.

## Gate G — Live PS5 Current Instruction / Existing Debugger Regression

1. From a valid paused snapshot, use **Disassembler...** for the current instruction.
2. Confirm the existing Disassembler opens at the semantic instruction pointer and containing-instruction/origin behavior remains correct.
3. Confirm syntax highlighting and Back/Forward navigation still work.
4. Return to Debugger and confirm the paused session plus extended register snapshot remain valid.
5. Perform ordinary process Refresh and a safe Memory Viewer read while the debugger session exists.
6. Confirm those established transports do not interfere with subsequent register Refresh.

## Gate H — Live PS5 Cleanup / Reconnect Regression

1. Detach and confirm Threads/Registers/stop-context presentation clears correctly.
2. Reattach, Pause, and confirm a fresh full register snapshot loads.
3. Close an attached Debugger window with **X**, reopen, Attach/Pause, and confirm the debugger-owned sockets and register commands recover normally.
4. Disconnect the main PS5 session while Debugger is open and confirm the old window becomes stale/inactive and register presentation is cleared.
5. Reconnect, set `eboot.bin` Active Target again, open a fresh Debugger, Attach/Pause, and confirm general plus extended register reads work on the new connection generation.
6. Confirm no TCP 755 ownership conflict, command-stream framing error, duplicate event, or stale register snapshot appears.

## Gate I — Final Regression Smoke

Perform a short smoke test after the new register traffic/layout has been exercised:

- process enumeration and Active Target;
- Memory Viewer read;
- Disassembler and current-instruction navigation;
- simple First Scan / Next Scan;
- Saved Addresses;
- theme switching;
- Debugger Attach/Pause/Continue/Detach;
- Threads Refresh;
- Mock ordinary register write/read-back;
- PS5 register read-only gating;
- Disconnect/Reconnect.

The known individual PS5 Thread Suspend/Resume backend blocker does not need to be reproduced again.

## Acceptance Criteria

`0.1.7.rev9` is verified only when:

- the Windows build succeeds and **108/108** checks pass;
- Threads/Registers start 50/50 and remain proportionally resizable with adaptive practical limits;
- Mock displays stable 80/128/256-bit read-only fixtures while preserving the verified writable ordinary-register path;
- live PS5 retains correct general-register behavior and exposes the backend-supported extended register groups without framing/state corruption;
- every PS5 register remains read-only;
- Current Instruction -> Disassembler remains correct;
- Detach/window close/disconnect/reconnect cleanup remains correct;
- the final regression smoke reveals no change to previously verified functionality.
