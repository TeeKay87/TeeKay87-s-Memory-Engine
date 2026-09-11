# Application 0.1.7.rev10 Verification — PS5 Extended Register Transport Guard

## Status

**VERIFIED.** The packaged Windows suite passed **110/110**. Focused live-PS5 runtime acceptance also passed: Attach -> Pause now completes with the guarded register snapshot, repeated Refresh/Pause/Continue remains stable, Current Instruction -> Disassembler remains functional, and Detach/Reattach, window-close cleanup, disconnect/reconnect, and stale-session transport regression all passed. Rev10 is the verified baseline for rev11.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev10` |
| Feature | `PS5 Extended Register Transport Guard` |
| Plugin API | `2.13.0` |
| Mock plugin | `1.0.0.rev11` |
| PS5 plugin | `0.1.0.rev29` |
| Automated checks | `110` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev10 ZIP to a clean directory.
2. Build the solution in the normal Windows Release environment.
3. Run the verification executable.
4. Confirm the two new registrations pass:

```text
PASS  PS5 debugger extended register capability gating
PASS  PS5 debugger optional register timeout isolation
```

5. Confirm the final line is:

```text
All 110 checks passed.
```

All 108 rev9 registrations must remain present.

## Gate B — Quick Rev9 UI/Mock Regression

Rev9 Gates B-D already passed. Run a compact regression only:

1. Connect to Mock, set `TestGame.exe` Active, open Debugger, Attach, and Pause.
2. Confirm Threads/Registers still starts approximately 50/50 and the splitter still respects the existing 20/80 limits.
3. Confirm the wide Mock rows (80/128/256-bit) still display correctly and remain read-only.
4. Edit one ordinary writable Mock general register and verify write/read-back still succeeds.
5. Continue, Detach, and close/reopen the Debugger once.

Expected: no rev10 PS5 transport change altered the accepted rev9 Mock/UI behavior.

## Gate C — Live PS5 Register Refresh No Longer Stalls

This is the direct regression for rev9 Gate E.

1. Connect to the same real PS5/ps5debug-NG setup used for the rev9 failure.
2. Set `eboot.bin` as Active Target, open Debugger, and Attach.
3. Run global **Pause**.
4. Wait for the automatic thread/register refresh.

Required result:

- the target pauses normally;
- the UI remains responsive;
- the Registers refresh **completes** instead of remaining at `Registers 0` indefinitely;
- at minimum, the verified 26-row general-register snapshot is available with a valid semantic instruction pointer;
- if both safe optional backend reads complete, the current full paused PS5 surface contains **76 rows**: 26 general + 48 FPU/SIMD + 2 FS/GS-base;
- **no DR0-DR3/DR6/DR7 rows are expected in rev10**, because paused GETDBREGS is deliberately suppressed for backend safety;
- if GETFPREGS or GETFSGSBASE is unavailable/times out, only that safe optional group may be absent; general rows and any healthy safe optional group must remain usable.

A bounded fallback is acceptable. An indefinite pending state is not.

Record which safe optional groups are present after the first Pause: FPU/SIMD and FS/GS. Also confirm that the UI never exposes a Debug-register group on this paused path.

## Gate D — Optional Failure Cache and Refresh Stability

While still Paused:

1. Select a register and press **Refresh** several times.
2. Confirm selection remains stable where the selected row still exists.
3. Confirm no duplicates appear.
4. If Gate C omitted one safe optional group, repeated Refresh must not repeatedly incur the same visible timeout delay for that failed group during the same attachment.
5. Any safe optional group that did succeed must continue to update normally.
6. The general rows must remain available on every refresh.
7. No Refresh while Paused may cause a new Debug-register group to appear; GETDBREGS remains intentionally disabled in this workflow.

## Gate E — Owner Transport After Fallback

This gate is mandatory even when Gate C displayed only general registers.

1. Press **Continue** and confirm the target resumes normally.
2. Pause again and confirm a fresh general stop context loads.
3. Press **Detach** and confirm it completes normally.
4. Reattach once and Pause again.

Required result: an optional probe timeout/failure must not make the attached debugger-owner connection unusable. No framing error, permanent busy state, forced app restart, or stuck Detach is acceptable.

## Gate F — Current Instruction -> Disassembler

With a valid paused RIP/IP:

1. Use **Disassembler...** from the Registers pane.
2. Confirm the normal Disassembler opens at the current/containing instruction.
3. Confirm syntax highlighting, origin marking, and Back/Forward navigation still behave normally.
4. Return to Debugger and Continue.

## Gate G — Cleanup / Reconnect

1. Attach/Pause, then close the Debugger with **X**.
2. Open a new Debugger and reattach successfully.
3. Disconnect the PS5 connection while a Debugger window exists.
4. Confirm the old window becomes stale/inactive and cannot reuse register data against a new connection generation.
5. Reconnect, set `eboot.bin` Active again, open a fresh Debugger, Attach/Pause, and confirm a new snapshot loads.

## Gate H — Final Transport Regression

With the debugger attached, exercise the already verified parallel workflows:

- process Refresh;
- Memory Viewer read;
- Register Refresh;
- Current Instruction -> Disassembler;
- Continue -> Pause;
- Detach/Reattach.

No target-memory, scanner/disassembler, debugger-event, or transport framing regression may appear.

PS5 individual Thread Suspend/Resume is **not** part of this rev10 gate. It remains documented as **IMPLEMENTED / BACKEND BLOCKED** on the currently tested ps5debug-NG backend.

## Acceptance Criteria

Rev10 is accepted only when:

- Gate A reports **110/110 PASS**;
- the quick Mock/splitter regression passes;
- the exact rev9 live PS5 `Registers 0` indefinite wait no longer occurs;
- mandatory general registers remain available after safe optional failure/fallback;
- the paused PS5 register path never issues GETDBREGS and therefore never exposes DR rows in rev10;
- Continue/Detach/Reattach remain usable after the optional-register path;
- Current Instruction -> Disassembler and stale-session cleanup remain intact;
- no unrelated verified feature regresses.

After rev10 is accepted, the next planned debugger revision is **0.1.7.rev11 — Breakpoint Manager and Software Breakpoints**.

## Final Acceptance Result — 2026-09-09

- **Automated Windows verification:** `110/110 PASS`.
- **Live PS5 register transport regression:** PASS. The rev9 `Registers 0` indefinite pending state no longer occurs.
- **Expected live safe snapshot:** 76 rows when both safe optional groups are available; paused Debug-register rows remain intentionally omitted.
- **Repeated Refresh / Pause / Continue:** PASS.
- **Current Instruction -> Disassembler:** PASS.
- **Detach / Reattach / Debugger X-close cleanup:** PASS.
- **Disconnect / reconnect / stale-session isolation:** PASS.
- **Result:** `0.1.7.rev10 - PS5 Extended Register Transport Guard` is fully verified and is the baseline for rev11.
