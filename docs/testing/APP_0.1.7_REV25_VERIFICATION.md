# Application 0.1.7.rev25 Verification — PS5 Breakpoint Stop Context and Status Alignment Fix

## Status

**SUPERSEDED BY REV26 AFTER GATE E STEP OVER FAILURE.** Rev25 passed its Windows automated gate, MainWindow status-bar alignment checks, focused Mock regression, and the complete live-PS5 logical software-breakpoint stop-context gate. The first live Step Over check then exposed a host composition defect: when stopped logically on a software breakpoint placed on a `call`, the host re-read breakpoint-patched target memory, decoded the rearmed `INT3` instead of the original `call`, and incorrectly selected the native Step Into path. Rev26 preserves all accepted rev25 work and corrects that breakpoint-aware instruction-resolution problem.

## Candidate Metadata

| Component | rev25 value |
| --- | --- |
| Application | `0.1.7.rev25` |
| Feature | `PS5 Breakpoint Stop Context and Status Alignment Fix` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev35` |
| Automated checks | `132` |

## Gate A — Clean Windows Build and Automated Verification — PASS

The supplied rev25 package built successfully on Windows and the complete automated registry passed:

```text
All 132 checks passed.
```

No C# or XAML build blocker was reported during this gate.

## Gate B — MainWindow Status-Bar Visual Alignment — PASS

Runtime inspection accepted the rev25 status-bar alignment correction:

- Connected/Not connected, normal status text, scan status, progress/elapsed content, and the right-side application version were visually centered on the same horizontal status-row centerline;
- resizing MainWindow through practical widths did not disturb that alignment or the existing left-to-right order; and
- Dimmed, Dark, and Light all retained the same centered layout without changing the established text, progress, color, binding, or version-placement behavior.

## Gate C — Quick Mock Regression — PASS

The correction-sensitive Mock regression passed:

- Active Target -> Debugger -> Attach -> Pause populated Threads, Registers, and Call Stack normally;
- Breakpoints / Watchpoints and Call Stack workspace switching remained correct;
- one native Step Into and one composed stepping/Run-to path returned to a normal Paused context with the expected event sequencing;
- Detach/Reattach cleared the old register/call-frame state and loaded a fresh session; and
- ordinary software breakpoints and hardware watchpoints still worked after stepping.

The complete Mock Call Stack/stepping cycle already accepted in rev24 therefore remained intact in rev25.

## Gate D — Live PS5 Logical Software-Breakpoint Stop Context — PASS

The rev25 PS5 logical-stop correction passed on hardware.

### Ordinary instruction breakpoint

A persistent Software/Execute breakpoint was placed on:

```text
0xE9F747  mov esi,1
0xE9F74C  xor edx,edx
```

After Continue, all logical stop surfaces agreed on the breakpoint instruction itself:

- breakpoint event: `0xE9F747`;
- displayed Debugger IP: `0xE9F747`;
- Registers `RIP`: `0xE9F747`; and
- Call Stack frame zero: `0xE9F747`.

Pressing Step Into once then produced one `Resumed -> StepCompleted` transition and finished at `0xE9F74C`. The operation did not send/observe an apparent second-instruction step. This confirms that rev25 correctly consumed ps5debug-NG's already-completed transparent software-breakpoint step.

### `call` breakpoint logical position

A clean game/debugger session was then used with a persistent Software/Execute breakpoint on the known five-byte call:

```text
0xE9F74E  call 0x1A3BC30
0xE9F753  jmp short 0xE9F731
```

The breakpoint hit correctly reported `0xE9F74E` in the event, displayed IP, Registers `RIP`, and Call Stack frame zero rather than immediately exposing the backend's post-step callee address.

One earlier reused-session attempt produced an unrelated signal-11 stop after the preceding test sequence. Restarting the game and repeating the `0xE9F74E` breakpoint from a clean session produced the expected breakpoint event and completed this gate. No separate reproducible rev25 application defect was established from that single reused-session signal.

## Gate E — Live PS5 Step Over, Step Out, and Run to Address — BLOCKED AT STEP OVER

### Step Over — FAIL

Testing continued directly from the accepted logical stop at:

```text
0xE9F74E  call 0x1A3BC30
```

The expected Step Over result was the fall-through address:

```text
0xE9F753
```

Instead, pressing **Step Over** produced a `Resumed -> StepCompleted` sequence identified as **Step Into** and finished at:

```text
0x1A3BC30
```

That is the callee, so this was a real Step Over functional failure rather than a status-text-only problem.

### Root cause established from runtime and source review

The live disassembly exported while the persistent software breakpoint was armed showed:

```text
0xE9F74E  CC  int3
```

This is expected for the physical target memory because ps5debug-NG rearms the software breakpoint by restoring `INT3` after transparently executing the original instruction. Rev25's host `StepOverAsync()` still resolved the current instruction by reading live disassembly at the logical RIP. It therefore saw `int3` rather than the original five-byte `call`, treated the instruction as a non-call, and deliberately fell back to the native Step Into path. The new rev25 PS5 logical-stop handling then correctly consumed the backend's already-completed transparent step, whose live post-step RIP was inside the callee at `0x1A3BC30`.

The stop-context fix itself was therefore working; the remaining defect was in the host's Step Over instruction classification at a logical software-breakpoint stop.

### Step Out / Run to Address

Step Out and the remaining Run to Address acceptance checks were not used to accept rev25 after the Step Over blocker was confirmed. They carry forward to the corrective rev26 runtime cycle after Step Over is repaired.

## Gate F — Live Cleanup / Stale-Session Regression

The final Gate F acceptance sequence was not completed after Gate E became blocking. The already accepted rev24/rev25 lifecycle behavior remains the baseline and must be rechecked after the remaining rev26 live stepping tests.

## Rev25 Completion Decision

Rev25 is **not accepted as the final corrective stepping revision** because live Step Over from a software-breakpoint logical stop failed. The following rev25 results are carried forward as accepted evidence because rev26 does not redesign those paths:

1. clean Windows build and **132/132 PASS**;
2. MainWindow status-bar centerline alignment PASS;
3. quick Mock regression PASS; and
4. live PS5 logical software-breakpoint event/register/frame consistency plus Step Into-from-breakpoint PASS.

Rev26 corrects host-side breakpoint-aware Step Over instruction resolution. After its clean automated gate, runtime verification resumes at the failed live Step Over step, followed by Step Out, Run to Address, and cleanup/reconnect regression. The Debugger **Integration, Export and Finalization** milestone therefore moves to rev27.
