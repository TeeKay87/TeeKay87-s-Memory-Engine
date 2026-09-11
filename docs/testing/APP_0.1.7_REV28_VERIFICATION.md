# Application 0.1.7.rev28 Verification — Debugger Deferred Stop Verification Contract Fix

## Status

**CANDIDATE.** Rev28 contains no production debugger change. It corrects the single stale rev27 source assertion that expected the old Continue-only deferred-stop branch even though rev27 intentionally extended that branch for interrupted composed-operation cleanup.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev28` |
| Feature | `Debugger Deferred Stop Verification Contract Fix` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev15` |
| PS5 plugin | `0.1.0.rev36` |
| Automated checks | `135` |

## Rev27 Automated Result Carried Forward

Rev27 completed **134/135** checks. The only failure was:

```text
FAIL  Debugger breakpoint manager source contract
      Debugger ViewModel does not defer a paused breakpoint event that arrives while Continue is still completing.
```

The production source contained the stricter rev27 condition:

```text
else if (_continueInProgress || _pendingInterruptedComposedExecutionOperation is not null)
```

The dedicated `Debugger interrupted composed-operation cleanup source contract` checked that same widened condition and passed. Rev28 changes only the stale older assertion so both contracts now validate the intended implementation consistently.

## Carried-Forward Runtime Acceptance

Do not repeat the full earlier debugger matrix before the correction-sensitive rev27 gates. These results remain accepted:

- rev25 MainWindow status-bar alignment in Dimmed/Dark/Light;
- rev25 focused Mock debugger regression;
- rev25 live PS5 logical software-breakpoint event/IP/Register/frame-zero consistency;
- rev25 Step Into from a logical software-breakpoint stop without a second native step;
- rev26 live breakpoint-aware Step Over from `0xE9F74E` to `0xE9F753` with temporary-breakpoint cleanup; and
- rev26 clean natural-pause Step Out to the selected return address with no temporary breakpoint left visible.

Rev27 changed common composed-operation interruption cleanup and PS5 detach behavior, so the short cleanup regression below is still required after the Windows gate.

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev28 ZIP into a clean directory.
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

## Gate B — Quick Composed-Stepping Cleanup Regression

### Step Over

Use the already verified sequence if the same target build is active:

```text
0xE9F74E  call 0x1A3BC30
0xE9F753  jmp short 0xE9F731
```

1. Start a clean debugger session and place a persistent Software/Execute breakpoint at `0xE9F74E`.
2. Continue to the logical breakpoint stop.
3. Press **Step Over** once.
4. Confirm final IP is `0xE9F753`, state is Paused, and no temporary `0xE9F753` breakpoint remains.

### Step Out

1. Establish a natural manual-Pause context with a valid selected Call Stack return address and no unrelated persistent breakpoint active.
2. Note the selected frame's Return value.
3. Press **Step Out**.
4. Confirm final IP equals that Return value and no temporary Step Out breakpoint remains visible.

## Gate C — Controlled Interrupted Run to Address Cleanup

This gate deliberately creates an interruption without relying on incidental PS5 signals.

1. From a clean Paused PS5 debugger session with no persistent software breakpoint active, choose a valid executable Run-to address that will not be reached immediately.
2. Start **Run to Address** and confirm the target enters Running state.
3. Before the target reaches the Run-to address, press **Pause** manually.
4. Confirm the resulting stop is Paused/PauseRequested rather than a hit on the Run-to target.
5. Wait for paused-context refresh to finish, then open **Breakpoints / Watchpoints**.
6. Confirm the operation-owned temporary Run-to breakpoint is no longer present.
7. Confirm status describes the interrupted/paused result and does not remain at `Run to Address is running toward ...`.

## Gate D — Safe Detach After Interrupted Run-to Cleanup

Continue directly from Gate C without resuming first.

1. Press **Detach** after the interrupted Run-to temporary breakpoint has disappeared from the manager.
2. Confirm Debugger reaches Detached normally.
3. Confirm the game remains running/usable and does not terminate.
4. Reattach and manually Pause.
5. Confirm fresh Threads, Registers, and Call Stack load normally and no old temporary breakpoint reappears.

A target crash, stuck detach, transport/framing error, or resurrected temporary breakpoint blocks acceptance.

## Gate E — Normal Successful Run to Address

1. From a clean Paused context, choose a known executable address expected to execute.
2. Start **Run to Address** and allow it to reach the target normally.
3. Confirm breakpoint event, visible IP, Registers `RIP`, and Call Stack frame zero all report the requested address.
4. Confirm state is Paused and status describes the resulting stop rather than stale running text.
5. Confirm the temporary Run-to breakpoint is absent afterward.

## Gate F — Final Cleanup / Reconnect Regression

1. Continue, then manually Pause again and confirm no previous logical/operation stop context survives incorrectly.
2. Detach with no operation active and confirm the target remains usable.
3. Reattach and verify one ordinary persistent software breakpoint can still be added, hit, removed/disabled according to the existing paused semantics, and cleaned safely on final detach.
4. Close Debugger, then close MainWindow with modeless tools open, confirming the previously accepted asynchronous tool cleanup still completes normally.
5. Confirm no orphan temporary breakpoint, stale operation status, unexpected detach, or transport/framing error remains.

## Completion Rule

Rev28 may be accepted only after:

1. clean Windows WPF build;
2. **135/135 PASS**;
3. quick live Step Over and Step Out cleanup regression PASS;
4. controlled interrupted Run-to cleanup PASS;
5. detach immediately after interrupted cleanup leaves the game alive and reconnectable;
6. normal successful Run to Address PASS; and
7. final cleanup/reconnect/tool-window regression PASS.

If rev28 passes without another code correction, the next revision is `0.1.7.rev29 — Integration, Export and Finalization`.
