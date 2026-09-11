# Application 0.1.7.rev27 Verification — Interrupted Operation and Safe Detach Breakpoint Cleanup

## Status

**SUPERSEDED BY REV28 AFTER WINDOWS SOURCE-CONTRACT FAILURE.** Rev27 preserves the accepted rev25 logical-stop work and the rev26 breakpoint-aware Step Over correction, then fixes the operation-owned temporary breakpoint that remained after an unrelated live PS5 stop and adds defensive software-breakpoint restoration before PS5 detach/disposal.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev27` |
| Feature | `Interrupted Operation and Safe Detach Breakpoint Cleanup` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev36` |
| Automated checks | `135` |

## Carried-Forward Acceptance

Do not repeat the full earlier debugger matrix before reaching the correction-sensitive gates. The following evidence remains accepted:

- rev25 MainWindow status-bar alignment in Dimmed/Dark/Light;
- rev25 focused Mock debugger regression;
- rev25 live PS5 logical software-breakpoint event/IP/Register/frame-zero consistency;
- rev25 Step Into from a logical software-breakpoint stop without a second native step; and
- rev26 live Step Over at `0xE9F74E`, which correctly finished at `0xE9F753` and removed its temporary fall-through breakpoint.

Rev27 changes the shared interruption/cleanup path and PS5 detach behavior, so focused Step Over/Step Out cleanup regression is still required below.

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev27 ZIP into a clean directory.
2. Build the solution/application normally in Visual Studio.
3. Confirm there are no C# or XAML compile failures and no warnings promoted to errors.
4. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

5. Confirm the final line is exactly:

```text
All 135 checks passed.
```

Do not continue live acceptance if any automated check fails.

### Actual rev27 Windows result

The supplied rev27 candidate completed **134/135** checks. The only failure was `Debugger breakpoint manager source contract`, whose older assertion still required the literal `else if (_continueInProgress)` source shape. Rev27 production had intentionally widened that branch to `else if (_continueInProgress || _pendingInterruptedComposedExecutionOperation is not null)`; the dedicated new interrupted-operation cleanup contract required that widened condition and passed. No live rev27 gate was started. Rev28 corrects the stale assertion without changing production behavior.

## Gate B — Quick Composed-Stepping Regression

### Step Over

Use the already verified call sequence if the same target build is active:

```text
0xE9F74E  call 0x1A3BC30
0xE9F753  jmp short 0xE9F731
```

1. Start a clean debugger session and place a persistent Software/Execute breakpoint at `0xE9F74E`.
2. Continue to the logical breakpoint stop.
3. Press **Step Over** once.
4. Confirm final IP is `0xE9F753`, the state is Paused, and no temporary `0xE9F753` breakpoint remains.

### Step Out

1. Establish a natural manual-Pause context with a valid selected Call Stack return address and no unrelated persistent breakpoint active.
2. Note the selected frame's Return value.
3. Press **Step Out**.
4. Confirm final IP equals that Return value and no temporary Step Out breakpoint remains visible.

Any regression in either already-working operation blocks rev27.

## Gate C — Interrupted Run to Address Cleanup

This gate deliberately creates a controlled interruption without depending on the earlier signal-10 observation.

1. From a clean Paused PS5 debugger session with no persistent software breakpoint active, choose a valid executable Run-to address that will not be reached immediately.
2. Start **Run to Address** and confirm the target enters Running state.
3. Before the target reaches the Run-to address, press **Pause** manually.
4. Confirm the resulting stop is Paused/PauseRequested rather than a hit on the Run-to target.
5. Open **Breakpoints / Watchpoints** after the paused context refresh finishes.
6. Confirm the operation-owned temporary Run-to breakpoint is no longer present in the visible manager.
7. Confirm the Debugger status describes the interrupted operation/paused result and does not remain at `Run to Address is running toward ...`.

This directly verifies the host interruption-cleanup correction.

## Gate D — Safe Detach After Interrupted Run-to Cleanup

Continue directly from Gate C without resuming the target first.

1. With the Run-to interruption already cleaned from the visible manager, press **Detach**.
2. Confirm Debugger reaches Detached state normally.
3. Confirm the game remains running/usable and does not terminate as it did after the rev26 interrupted Run-to state.
4. Reattach and manually Pause.
5. Confirm fresh Threads, Registers, and Call Stack load normally and no old temporary Run-to breakpoint reappears.

A target crash, transport/framing failure, stuck detach, or resurrected temporary breakpoint blocks rev27.

## Gate E — Normal Successful Run to Address

After the interrupted-cleanup/detach path passes, verify that ordinary successful Run-to behavior was not changed.

1. From a clean Paused context, choose a known executable address that is expected to execute. Reuse a previously proven game-code address when practical.
2. Start **Run to Address** and allow it to reach the target normally.
3. Confirm the breakpoint event, visible IP, Registers `RIP`, and Call Stack frame zero report the requested address.
4. Confirm state is Paused and the status describes the resulting stop rather than stale running text.
5. Confirm the temporary Run-to breakpoint is absent afterward.

## Gate F — Final Cleanup / Reconnect Regression

1. Continue, then manually Pause again and confirm no previous logical/operation stop context survives incorrectly.
2. Detach with no operation active and confirm the target remains usable.
3. Reattach and verify one ordinary persistent software breakpoint can still be added, hit, removed/disabled according to the existing paused semantics, and cleaned safely on final detach.
4. Close Debugger, then close MainWindow with modeless tools open, confirming the previously accepted asynchronous tool cleanup still completes normally.
5. Confirm no orphan temporary breakpoint, stale operation status, unexpected detach, or transport/framing error remains.

## Completion Rule

Rev27 may be accepted only after:

1. clean Windows WPF build;
2. **135/135 PASS**;
3. quick live Step Over and Step Out regression PASS;
4. controlled interrupted Run-to cleanup PASS;
5. detach immediately after that interrupted operation leaves the game alive and reconnectable;
6. a normal successful Run to Address PASS; and
7. final cleanup/reconnect/tool-window regression PASS.

Rev27 did require a verifier correction after the **134/135** Windows result and is therefore superseded. Rev28 corrects the stale deferred-stop source assertion; Debugger **Integration, Export and Finalization** moves to `0.1.7.rev29`.
