# Application 0.1.7.rev26 Verification — Breakpoint-Aware Step Over Fix

## Status

**SUPERSEDED BY REV27 AFTER PARTIAL LIVE-PS5 ACCEPTANCE.** Rev26 passed **133/133** and corrected the breakpoint-aware Step Over failure. Its live Step Over target/cleanup passed, and a clean Step Out reached the selected return address with no visible temporary breakpoint remaining. Run to Address was then interrupted by a signal-10 stop before its requested target; the operation-owned temporary breakpoint remained Enabled/Temporary. Detaching from that interrupted paused state was followed by the game terminating. Rev27 preserves the accepted rev26 Step Over correction and focuses on interrupted-operation cleanup plus defensive PS5 software-breakpoint restoration before detach/disposal.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev26` |
| Feature | `Breakpoint-Aware Step Over Fix` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev35` |
| Automated checks | `133` |

## Carried-Forward Rev25 Acceptance

The following rev25 results do **not** need to be repeated before resuming the live failure point because rev26 does not redesign those paths:

- MainWindow status-bar alignment: PASS, including resize plus Dimmed/Dark/Light;
- focused Mock debugger regression: PASS;
- live PS5 ordinary software-breakpoint event/IP/Register/frame-zero consistency at `0xE9F747`: PASS;
- live PS5 Step Into from that logical breakpoint stop to `0xE9F74C` without a second instruction skip: PASS; and
- live PS5 logical breakpoint position on the `call` at `0xE9F74E`, including event/IP/Registers/Call Stack frame zero: PASS.

A clean build and the new automated registry are still mandatory for the new package.

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev26 ZIP into a clean directory.
2. Build the solution/application normally in Visual Studio.
3. Confirm there are no C# or XAML compile failures and no warnings promoted to errors.
4. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

5. Confirm the final line is exactly:

```text
All 133 checks passed.
```

Do not continue live acceptance if any automated check fails.

## Gate B — Resume Live PS5 Step Over at the Rev25 Failure Point

Use the same target build and the already established known call sequence:

```text
0xE9F74E  call 0x1A3BC30
0xE9F753  jmp short 0xE9F731
```

Start from a clean game/debugger session so the single signal-11 observation from the reused rev25 session cannot contaminate this regression.

1. Attach and Pause.
2. Add one **persistent Software / Execute** breakpoint at `0xE9F74E` while the original `call` bytes are still present. Rev26 should capture the original neutral instruction before the backend installs `INT3`.
3. Continue until the persistent breakpoint hits.
4. Confirm the already accepted logical stop still reports `0xE9F74E`.
5. Press **Step Over** exactly once.
6. Confirm the operation finishes **Paused at `0xE9F753`**, the five-byte call's fall-through address.
7. Confirm the operation did **not** finish in the callee at `0x1A3BC30` and is not reported as a native Step Into completion.
8. Open Breakpoints / Watchpoints and confirm no temporary Step Over breakpoint remains. The original persistent `0xE9F74E` breakpoint may remain as expected.
9. Confirm no signal, transport/framing error, unexpected detach, or stale running status was produced.

This gate is the direct acceptance criterion for the rev26 production correction. Failure here blocks the revision immediately.

## Gate C — Continue Rev25 Gate E: Step Out

After Step Over passes, establish a natural Paused context with a valid selected Call Stack frame and return address.

1. Note the return address shown for the selected frame that Step Out will use.
2. Press **Step Out**.
3. Confirm the final logical RIP equals that return address.
4. Confirm no temporary Step Out breakpoint remains in Breakpoints / Watchpoints.
5. Confirm the target remains Paused and the event/status text describes the resulting stop rather than a stale running operation.

## Gate D — Continue Rev25 Gate E: Run to Address

1. From a clean Paused state, choose a known executable address that is expected to execute.
2. Use **Run to Address** for that location.
3. Confirm the breakpoint event, visible IP, Registers `RIP`, and Call Stack frame zero all report the requested address at the logical stop.
4. Confirm the bottom Debugger status describes the stop/Paused result and does **not** remain at `Run to Address is running toward ...`.
5. Confirm the temporary Run-to breakpoint is cleaned up.

## Gate E — Live Cleanup / Stale-Session Regression

1. Continue and Pause again; confirm a previous logical software-breakpoint snapshot does not remain visible in the unrelated pause.
2. Detach with no operation active and confirm Debugger data is cleared while the target remains usable.
3. Reattach and Pause; confirm fresh Registers and Call Stack are loaded.
4. Repeat one ordinary software-breakpoint hit after reconnect.
5. Close Debugger, then close MainWindow with modeless tools open, and confirm the already accepted cleanup/z-order behavior still completes normally.
6. Confirm no transport/framing error, unexpected detach, orphan temporary breakpoint, stale execution status, or stale original-instruction cache affects the new session.


## Final Rev26 Runtime Result

The final rev26 Windows run completed **133/133 PASS**. Live verification then established:

- **Step Over PASS:** persistent breakpoint stop at `0xE9F74E` was classified from the captured original `call`, Step Over finished Paused at `0xE9F753`, and the temporary fall-through breakpoint was removed while the persistent breakpoint remained.
- **Step Out functional target/cleanup PASS:** from a clean manual-Pause context, Step Out reached return address `0x800005AEB` and left no visible temporary breakpoint.
- **Run to Address BLOCKED:** a run toward `0x81C15634` stopped first with signal 10 at `0x81C1563B`; the temporary Software/Execute breakpoint at `0x81C15634` remained Enabled/Temporary rather than being retired as an interrupted operation.
- **Detach safety BLOCKED:** detaching directly from that interrupted state was followed by the target game terminating. The runtime evidence does not establish whether the external backend caused the signal or termination; the client-side cleanup gaps are addressed in rev27.

## Completion Rule

Rev26 may be accepted only after:

1. clean Windows WPF build;
2. **133/133 PASS**;
3. resumed live PS5 Step Over at `0xE9F74E` finishes at `0xE9F753` with temporary-breakpoint cleanup;
4. live Step Out PASS;
5. live Run to Address PASS, including stop-context/status/cleanup; and
6. live cleanup/reconnect/tool-window regression PASS.

The already completed rev25 MainWindow alignment, Mock regression, and logical software-breakpoint stop-context/Step Into checks remain accepted evidence and are not repeated unless the resumed testing exposes a regression that points back to them.

If rev26 passes without another code correction, the next revision is `0.1.7.rev27 — Integration, Export and Finalization`.
