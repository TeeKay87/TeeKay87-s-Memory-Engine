# Application 0.1.7.rev2 Verification — Debugger Workspace and Mock Backend

## Scope

Application `0.1.7.rev2 - Debugger Workspace and Mock Backend` is the first user-facing consumer of the debugger contracts/Core coordinator verified in `0.1.7.rev1`.

Rev2 adds:

- the capability-driven modeless Debugger workspace;
- explicit Attach / Pause / Continue / Detach controls;
- bounded neutral debugger-event presentation;
- host-owned debugger coordinator lifetime tied to the Active Target/session;
- deterministic Mock `IDebuggerProvider` / `IDebuggerSession` behavior.

Rev2 does **not** add a PS5 debugger provider/transport, thread/register services, breakpoints/watchpoints, call stacks, stepping, Find What Writes, Break and Trace, or debugger export.

## Expected Versions

| Component | Expected version |
| --- | --- |
| Application | `0.1.7.rev2` |
| Feature | `Debugger Workspace and Mock Backend` |
| Plugin API | `2.12.0` |
| In-Memory Test Target | `1.0.0.rev8`, targets API `2.12.0` |
| PlayStation 5 plugin | `0.1.0.rev24`, targets API `2.11.0` |

PS5 remains compatible through the same-major/older-minor Plugin API rule and must not advertise debugger support in rev2.

## Windows Build and Automated Verification

On Windows, from the repository root:

1. perform a clean Release build of the complete solution;
2. confirm no new compiler warning/error is introduced under the project's warnings-as-errors configuration;
3. run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

4. confirm the final line is:

```text
All 89 checks passed.
```

The verified rev1 registry contained 84 checks. Rev2 must retain all 84 and add exactly five:

1. `Mock debugger provider lifecycle`;
2. `Mock debugger deterministic pause and continue events`;
3. `Mock debugger attachment exclusivity and target cleanup`;
4. `Debugger workspace command and event source contract`;
5. `Debugger target lifetime and stale-session source contract`.

Any build failure or failed check blocks rev2 verification.

## Mock Debugger Runtime / UI Acceptance

Use the **In-Memory Test Target** so no physical target is required.

### 1. Entry-point capability gating

1. Select the Mock plugin.
2. Connect.
3. Promote `TestGame.exe` to **Active Target**.
4. Confirm **Debugger...** is visible and enabled in the target strip.
5. Confirm existing **Memory Viewer**, **Disassembler**, scan, and Saved Addresses controls retain their established layout/behavior.

Expected: Debugger is available only after a valid Active Target exists. The PS5 plugin must not expose a Debugger entry in rev2 because it advertises no `Debugger` capability.

### 2. Open without implicit attach

1. Click **Debugger...**.
2. Confirm the window opens modelessly.
3. Confirm the target text identifies `TestGame.exe` and process id `0x3E9` (1001).
4. Confirm initial state is **Detached**.
5. Confirm Attach is enabled; Pause/Continue/Detach are disabled.

Expected: opening the workspace itself performs no debugger attachment.

### 3. Attach

1. Click **Attach**.

Expected:

- state becomes **Running**;
- status reports that the debugger is attached and target is running;
- Attach becomes disabled;
- Pause and Detach become enabled;
- Continue remains disabled;
- no synthetic event is required merely for attach in rev2.

### 4. Deterministic Pause event

1. Click **Pause**.

Expected:

- state becomes **Paused**;
- exactly one new event appears;
- Event = `Paused`;
- State = `Paused`;
- Stop Reason = `PauseRequested`;
- Thread = `0x1`;
- Instruction Pointer = `0x10000400`;
- Message = `Mock target paused.`;
- Continue and Detach are enabled; Pause is disabled.

### 5. Deterministic Continue event

1. Click **Continue**.

Expected:

- state returns to **Running**;
- one additional event appears;
- Event = `Resumed`;
- State = `Running`;
- Stop Reason is blank/None;
- Thread = `0x1`;
- Instruction Pointer = `0x10000400`;
- Message = `Mock target resumed.`;
- Pause and Detach are enabled; Continue is disabled.

Repeat Pause/Continue several times and confirm sequence numbers increase monotonically and the table remains responsive.

### 6. Clear event history

1. With at least one event visible, click **Clear Events**.

Expected: the presentation list becomes empty without detaching or changing the current Running/Paused debugger state.

### 7. Explicit detach and reattach

1. Click **Detach**.
2. Confirm state becomes **Detached**.
3. Confirm Attach becomes available again for the same still-current target.
4. Attach again and verify the session begins in Running state.

Expected: an explicit detach fully releases the Mock attachment so a new attachment can be created.

### 8. Window-close cleanup

1. Attach the debugger.
2. Close the Debugger window without clicking Detach.
3. Open a new Debugger window for the same Active Target.
4. Attach again.

Expected: closing the first window disposes/releases its coordinator/backend session. The second attach succeeds; there is no leaked “already attached” Mock session.

### 9. Disconnect invalidation

1. Attach in a Debugger window.
2. Use the main workspace **Disconnect** action.
3. Confirm the target disconnect completes and the debugger transitions to Detached/stale state rather than keeping the backend alive.
4. Reconnect Mock and set `TestGame.exe` Active Target again.
5. Confirm the old Debugger window cannot attach to the new connection generation.
6. Open a fresh Debugger window and confirm Attach works normally.

Expected: an old modeless window never follows a reconnect implicitly, even though process id/name are deterministic and identical.

### 10. Active Target lifetime

Mock currently exposes a single process, so direct process-to-process switching cannot be exercised with this plugin. The automated source/lifetime test therefore remains the rev2 gate for ordering debugger cleanup before `ActiveProcess` replacement. Runtime disconnect/reconnect verifies the same connection-generation stale-window rule.

### 11. Multiple Debugger windows

1. Open two Debugger windows for the same current Mock target.
2. Attach the first.
3. Attempt Attach in the second.

Expected: the second attach reports the Mock provider's one-active-session error without affecting the first session or crashing the host.

4. Detach/close the first.
5. Retry Attach in the second.

Expected: the second window can now attach.

## Regression Smoke Test

After the Debugger runtime checks, verify briefly on Mock that previously verified target-independent workflows still function:

- process enumeration and Active Target selection;
- a simple scan;
- Saved Address capture/read behavior;
- Memory Viewer opening/navigation;
- Disassembler opening at `0x10000400` and normal synthetic decode/navigation.

Rev2 is not allowed to alter these verified workflows beyond adding the new capability-driven Debugger entry and target-lifetime cleanup integration.

## PS5 Regression Boundary

No new PS5 debugger hardware test applies to rev2.

Confirm instead that:

- PS5 plugin remains `0.1.0.rev24` / API `2.11.0`;
- no `Debugger` capability is advertised by PS5;
- no PS5 production source change is present relative to rev1;
- no debugger command/event channel is opened against ps5debug-NG;
- existing PS5 scanner, memory, Saved Addresses/freeze, and Disassembler automated regression checks remain green.

Real PS5 debugger hardware acceptance begins with `0.1.7.rev3 - PS5 Debug Transport and Attach/Detach`.

## Pass Criteria

`0.1.7.rev2` is verified when all of the following are true:

- metadata reports `0.1.7.rev2 - Debugger Workspace and Mock Backend`;
- Plugin API remains `2.12.0`;
- Mock reports `1.0.0.rev8` / API `2.12.0` and advertises only the implemented coarse debugger capability;
- PS5 remains `0.1.0.rev24` / API `2.11.0` with no debugger capability;
- clean Windows Release build succeeds;
- all **89/89** automated checks pass;
- Mock runtime/UI acceptance above passes;
- stale-window disconnect/reconnect behavior passes;
- existing Mock regression smoke tests remain intact.

## Final Verification Result — 2026-09-08

All gates above subsequently passed: the Windows suite completed **89/89**, the complete focused Mock Debugger runtime/UI acceptance passed, and the regression smoke test remained intact. Rev2 is therefore the fully verified host/Mock debugger baseline. Rev3 development started from that verified package.
