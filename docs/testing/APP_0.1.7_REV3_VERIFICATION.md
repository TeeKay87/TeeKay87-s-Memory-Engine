# Application 0.1.7.rev3 Verification — PS5 Debug Transport and Target UI Cleanup

> **Status:** Superseded by `0.1.7.rev4` before rev3 completed its Windows/UI/live-PS5 acceptance. The PS5 debugger backend described here is retained unchanged by rev4 and rev5; the authoritative current gate is `APP_0.1.7_REV5_VERIFICATION.md`.

## Scope

Application `0.1.7.rev3 - PS5 Debug Transport and Target UI Cleanup` is the first revision that connects the generic Debugger workspace to a real platform debugger backend.

Rev3 adds:

- PS5 plugin `IDebuggerProvider` / `IDebuggerSession` support;
- a dedicated ps5debug-NG debugger command connection that remains separate from the established memory/scan transport;
- the required outbound debugger callback listener on TCP `755`;
- explicit PS5 Attach, Pause, Continue, and Detach;
- translation of fixed 1184-byte ps5debug-NG interrupt packets into neutral debugger events;
- the compact main-window target/control layout and permanent bottom connection-state indicator requested for this revision.

Rev3 does **not** add thread enumeration/control, register editing, breakpoints, watchpoints, call stacks, stepping, Find What Writes/Reads/Accesses, Break and Trace, or debugger-specific export. Those remain later `0.1.7`/post-debugger milestones.

The verified baseline is `0.1.7.rev2`: all **89/89** Windows checks and the complete focused Mock Debugger runtime/UI acceptance passed before rev3 development began.

## Expected Versions

| Component | Expected version |
| --- | --- |
| Application | `0.1.7.rev3` |
| Feature | `PS5 Debug Transport and Target UI Cleanup` |
| Plugin API | `2.12.0` |
| In-Memory Test Target | `1.0.0.rev8`, targets API `2.12.0` |
| PlayStation 5 plugin | `0.1.0.rev25`, targets API `2.12.0` |

Mock remains unchanged from the fully verified rev2 package. PS5 advances independently because rev25 is its first revision to consume the debugger contracts introduced in Plugin API `2.12.0`.

## Windows Build and Automated Verification

On Windows, from the repository root:

1. perform a clean Release build of the complete solution;
2. confirm no compiler warning/error is introduced under the project's warnings-as-errors configuration;
3. run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

4. confirm the final line is:

```text
All 94 checks passed.
```

The verified rev2 registry contained 89 checks. Rev3 must retain all 89 and add exactly five:

1. `PS5 debugger provider lifecycle and exclusivity`;
2. `PS5 debugger attach pause continue and detach protocol`;
3. `PS5 debugger asynchronous interrupt mapping`;
4. `PS5 debugger dedicated transport isolation`;
5. `Main workspace target controls and connection status layout`.

Any build failure or failed check blocks rev3 verification.

## Main Workspace Runtime / UI Acceptance

Perform this section first because it does not require debugger attachment and validates the host-only layout changes separately from live PS5 protocol behavior.

### 1. Top target row

With either built-in plugin selected, confirm the first target row contains, from left to right:

- **Platform** ComboBox;
- **Target Process** ComboBox when process enumeration is supported;
- **Refresh**;
- **Set Active Target**.

Expected:

- all four controls occupy the same row;
- normal 34-unit control sizing/alignment is preserved;
- process selection and Active Target remain separate states;
- selecting a process does not make it Active until **Set Active Target** is used.

### 2. Disassembler / Debugger action row

Confirm **Disassembler...** and **Debugger...** remain on the row below the top target row and are left-aligned.

Expected: capability gating remains unchanged. A capability that is not advertised must not become available merely because the control moved.

### 3. Removed passive target prose

Confirm the main workspace no longer renders:

- `Active <process>`;
- `Connect to enumerate processes`;
- `1 process loaded`;
- `<n> processes loaded`.

Expected: removal is presentation-only. Process enumeration, Selected Process, Active Target, target errors, and memory-map state continue to work normally.

### 4. Bottom connection-state indicator

Confirm the far-left item in the permanent bottom status bar is the connection indicator.

Expected:

- disconnected state reads **Not connected** and uses the danger/red border;
- connected state reads **Connected** and uses the success/green border plus muted success background;
- text remains readable in Light, Dimmed, and Dark themes;
- the application version remains at the far right of the bottom status bar;
- general/error/scan status fields remain available between those endpoints.

Repeat connect/disconnect in all three themes and confirm no stale color/state remains after a transition.

## Mock Regression Acceptance

Rev3 is not allowed to change the fully verified Mock backend. Perform a focused regression check rather than repeating the entire rev2 acceptance suite.

1. Select **In-Memory Test Target**.
2. Connect and set `TestGame.exe` as Active Target.
3. Open Debugger.
4. Attach; confirm Running.
5. Pause; confirm deterministic Paused event at thread `0x1`, instruction pointer `0x10000400`.
6. Continue; confirm Running/Resumed.
7. Detach and close the window.
8. Confirm Scan, Saved Addresses, Memory Viewer, and Disassembler still open/use their existing deterministic fixtures.

Expected: behavior is identical to the fully verified rev2 backend aside from the requested main-window layout.

## Live PS5 Debugger Acceptance

### Prerequisites

Use a PlayStation 5 running the current ps5debug-NG build supported by the existing plugin connection path.

Before testing:

- confirm normal PS5 connection/process enumeration still works;
- ensure the PC firewall permits **inbound TCP 755** for TeeKay87's Memory Engine;
- ensure no other local debugger/client is listening on TCP `755`;
- select the game process normally used for live testing (typically `eboot.bin`) and use **Set Active Target**;
- do not intentionally crash the game merely to manufacture an async signal event.

The normal ps5debug-NG command port remains the configured connection port (default `744`). The debugger opens a **separate** command connection to that port and listens on local TCP `755` before sending attach.

### 1. Capability-driven PS5 entry

After the PS5 process is Active Target, confirm **Debugger...** is visible and enabled.

Open the Debugger workspace.

Expected:

- initial state is **Detached**;
- opening the window does not implicitly attach;
- Attach is available; Pause/Continue/Detach are unavailable before attach.

### 2. Live Attach and TCP 755 callback

Click **Attach**.

Expected:

- attach completes without timeout/firewall error;
- state becomes **Running**;
- Attach disables;
- Pause and Detach enable;
- Continue remains disabled;
- the game continues running normally after attach;
- the successful attach proves that ps5debug-NG was able to establish its outbound TCP 755 event connection.

If attach reports that port 755 is unavailable, another local listener/session must be closed before continuing. If attach succeeds at the command level but the event callback never arrives, verify Windows firewall/network routing before treating it as a code defect.

### 3. Live Pause

Click **Pause** while Running.

Expected:

- the game/process visibly stops advancing;
- Debugger state becomes **Paused**;
- one new neutral event appears with Event `Paused`, State `Paused`, Stop Reason `PauseRequested`;
- Continue and Detach enable;
- Pause disables.

The explicit ps5debug-NG pause signal is filtered by the server's async dispatch path, so the host's PauseRequested event is generated from the successful command rather than requiring a second TCP 755 event.

### 4. Live Continue

Click **Continue**.

Expected:

- the game/process resumes;
- Debugger state returns to **Running**;
- one new `Resumed` event appears;
- Pause and Detach enable;
- Continue disables.

Repeat Pause -> Continue at least three times. No command-stream corruption, duplicate attachment, stale state, or target freeze may occur.

### 5. Dedicated transport isolation

While attached and **Running**:

1. return to the main window;
2. use **Refresh** on the process list;
3. briefly open/read Memory Viewer or perform another safe established target read;
4. return to the Debugger window.

Expected:

- normal target commands continue working;
- debugger remains attached/Running;
- ordinary target traffic does not appear as debugger events;
- no protocol framing error is reported.

This verifies at runtime that debugger commands are not sharing the established primary memory/scan command stream.

### 6. Explicit detach and reattach

While Running:

1. click **Detach**;
2. confirm state becomes Detached;
3. confirm normal game execution is unaffected;
4. click **Attach** again.

Expected: immediate reattach succeeds and returns to Running.

Repeat once from a Paused state:

1. Pause;
2. Detach while Paused;
3. confirm the backend attachment is released and the game is not left permanently stopped;
4. reattach and confirm Running.

Any requirement to reconnect the entire PS5 plugin before reattaching is a rev3 failure.

### 7. Window-close cleanup

1. Attach and leave the target Running.
2. Close the Debugger window with X without clicking Detach.
3. Open a fresh Debugger window.
4. Attach again.

Expected: the first window's disposal releases backend debugger ownership, TCP 755, and the dedicated command socket. The new attach succeeds without an “already attached” or port-in-use error.

### 8. Disconnect / connection-generation invalidation

1. Attach the live PS5 debugger.
2. Use main-window **Disconnect** while the Debugger window remains open.
3. Confirm the old Debugger window becomes detached/stale and cannot operate against the old connection.
4. Reconnect to PS5, restore the game as Active Target, and open a **new** Debugger window.
5. Attach again.

Expected: the new connection generation works normally and the old window cannot silently follow it.

### 9. Multiple Debugger windows

1. Open two Debugger windows for the same current PS5 target.
2. Attach the first.
3. Attempt Attach in the second.

Expected:

- the first remains attached;
- the second reports the provider/backend one-active-debugger error cleanly;
- no host crash occurs.

Detach/close the first and retry the second. It should then attach normally.

### 10. Async interrupt mapping — opportunistic live check

The automated protocol fixture authoritatively verifies the TCP 755 1184-byte packet parser, wait-status signal mapping, thread id, instruction-pointer offset, and transition to neutral Paused state.

If a **naturally occurring and safe** debugger stop is received during live testing, confirm:

- one event appears with State `Paused`;
- Stop Reason is `Signal` for a nonzero wait signal;
- thread id is populated;
- instruction pointer is populated;
- Continue resumes execution.

Do **not** deliberately crash or corrupt the target solely to force this event. Breakpoint/watchpoint-triggered live interrupt testing belongs to the later breakpoint/watchpoint revisions.

## PS5 Regression Smoke Test

After debugger acceptance, detach and briefly confirm existing verified PS5 workflows still work:

- process enumeration and Active Target selection;
- memory-map loading;
- safe memory read;
- one known safe memory write/read-back if desired;
- a simple scan path;
- Saved Addresses;
- Memory Viewer;
- Disassembler opening and normal navigation;
- theme switching;
- Disconnect / Connect.

The new debugger transport must not alter the primary PS5 scanner/memory/disassembly paths.

## Pass Criteria

`0.1.7.rev3` is fully verified when all of the following are true:

- metadata reports `0.1.7.rev3 - PS5 Debug Transport and Target UI Cleanup`;
- Plugin API remains `2.12.0`;
- Mock remains `1.0.0.rev8` / API `2.12.0` and passes the focused regression check;
- PS5 reports `0.1.0.rev25` / API `2.12.0` and advertises only the currently implemented coarse debugger capability from the debugger family;
- clean Windows Release build succeeds;
- all **94/94** automated checks pass;
- main target/status UI acceptance passes in Light, Dimmed, and Dark;
- live PS5 Attach/Pause/Continue/Detach and reattach pass;
- TCP 755 callback/cleanup behavior passes;
- primary PS5 command traffic remains usable while debugger is attached and Running;
- close/disconnect/stale-window cleanup passes;
- PS5 regression smoke test passes.

Rev3 did not complete these gates as a standalone revision. It was superseded by rev4 after the first Windows UI review; rev4 retains the debugger backend and carries the remaining Windows/live-PS5 acceptance forward together with the corrected permanent two-row header.
