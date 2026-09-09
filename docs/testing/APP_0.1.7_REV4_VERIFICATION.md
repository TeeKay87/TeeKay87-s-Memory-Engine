# Application 0.1.7.rev4 Verification — Two-Row Target Header and Button Alignment

> **Status:** Superseded by `0.1.7.rev5` before rev4 completed its automated/live-PS5 acceptance. The first Windows UI review confirmed the two-row order and shared button text alignment but exposed that fixed 240-unit row-1 inputs could extend beyond the right edge when the window was narrowed. Rev5 retains the rev3 debugger backend plus rev4 row/button work and replaces only the row-1 input sizing. The authoritative current gate is `APP_0.1.7_REV5_VERIFICATION.md`.


## Scope

Application `0.1.7.rev4 - Two-Row Target Header and Button Alignment` supersedes the unverified rev3 candidate. Rev4 preserves the complete rev3 PS5 debugger implementation and changes only shared host presentation, button-content layout, verification coverage, version metadata, and documentation.

The last fully verified baseline remains `0.1.7.rev2`: **89/89** Windows checks plus the complete focused Mock Debugger runtime/UI acceptance passed. Rev3 added the first real PS5 debugger backend but was superseded before its Windows/live acceptance was completed.

Rev4 must therefore pass both the new UI acceptance below and the full live PS5 debugger gate originally prepared for rev3.

## Expected Versions

| Component | Expected version |
| --- | --- |
| Application | `0.1.7.rev4` |
| Feature | `Two-Row Target Header and Button Alignment` |
| Plugin API | `2.12.0` |
| In-Memory Test Target | `1.0.0.rev8`, targets API `2.12.0` |
| PlayStation 5 plugin | `0.1.0.rev25`, targets API `2.12.0` |

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
All 96 checks passed.
```

Rev4 preserves all 94 registrations from the rev3 candidate and adds exactly two:

1. `Main workspace two-row target header standard`;
2. `Shared button content alignment and vertical padding`.

Any build failure or failed check blocks verification.

## Main Workspace Runtime / UI Acceptance

### 1. Permanent two-row header — Mock

Select **In-Memory Test Target** while disconnected.

Confirm the target/connection area has only two normal control rows.

**Row 1**, left to right:

1. **Platform**;
2. **Connect**;
3. **Disconnect**;
4. **Target Process**;
5. **Refresh**;
6. **Set Active Target**.

**Row 2**, left to right:

1. **Reload Plugins**;
2. **Disassembler...**;
3. **Debugger...**.

Expected:

- no third permanent connection/action row exists;
- empty error surfaces do not reserve a third row;
- Selected Process and Active Target semantics remain separate;
- capability-driven enable/visibility behavior is unchanged.

### 2. Permanent two-row header — PS5

Select **PlayStation 5** while disconnected.

Confirm **Row 1** is, left to right:

1. **Platform**;
2. **PS5 IP address or host name**;
3. **Port**;
4. **Connect**;
5. **Disconnect**;
6. **Target Process**;
7. **Refresh**;
8. **Set Active Target**.

Confirm **Row 2** remains:

1. **Reload Plugins**;
2. **Disassembler...**;
3. **Debugger...**.

Expected:

- Platform, PS5 host/IP, Port, and Target Process ordinary ComboBox/TextBox controls are visibly the same **240-unit width**;
- Port is not a narrower special case;
- plugin-specific inputs are host-rendered between Platform and Connect;
- no platform-specific third row appears;
- the default main-window width provides enough room for the intended two-row PS5 composition.

### 3. Theme coverage

Repeat the disconnected Mock and PS5 layout review in **Light**, **Dimmed**, and **Dark**.

Expected:

- spacing/order remains stable;
- labels and controls remain readable;
- no control overlaps the following item;
- the bottom connection indicator remains readable in all themes.

### 4. Button text alignment

Inspect representative buttons across the application, including labels with descenders where available, for example **Settings**, **Debugger...**, and dialog buttons such as **Apply**.

Expected:

- every ordinary button keeps the same outer height as before (`34` units through `UiMetrics.StandardControlHeight`);
- colors, borders, corner radii, hover/pressed/disabled visuals and per-view widths remain unchanged;
- text is visually centered horizontally and vertically;
- top/bottom internal whitespace is balanced;
- descenders such as `g`, `j`, `p`, `q`, and `y` are not clipped.

Also verify the deliberately compact 28-unit Saved Address Remove button still retains its intended smaller height.

### 5. Removed passive target/process prose and bottom status

Confirm the main workspace still does not render:

- `Active <process>`;
- `Connect to enumerate processes`;
- `1 process loaded`;
- `<n> processes loaded`.

Confirm the far-left bottom status indicator reads only **Connected** or **Not connected**, with the appropriate success/green or danger/red treatment. The application version must remain at the far right.

## Mock Regression Acceptance

1. Connect **In-Memory Test Target** and set `TestGame.exe` Active.
2. Open Debugger, Attach, Pause, Continue, and Detach.
3. Confirm deterministic Mock events/state remain identical to verified rev2.
4. Briefly confirm Scan, Saved Addresses, Memory Viewer, Disassembler, themes, and Connect/Disconnect still work.

Expected: rev4's presentation changes do not alter Mock/backend behavior.

## Live PS5 Debugger Acceptance

### Prerequisites

- use the current supported ps5debug-NG build;
- allow inbound TCP `755` through the PC firewall;
- ensure no other local debugger/client owns TCP `755`;
- connect through the normal configured ps5debug-NG command port (default `744`);
- select the game process and use **Set Active Target**.

### 1. Capability-driven entry

Open **Debugger...** for the current PS5 Active Target.

Expected: initial state `Detached`; opening does not implicitly attach; Attach is available while Pause/Continue/Detach are not.

### 2. Attach and callback

Click **Attach**.

Expected:

- attach succeeds;
- state becomes `Running`;
- Pause and Detach enable;
- Continue remains disabled;
- successful attach establishes the separate TCP `755` event connection;
- game execution continues normally after attach.

### 3. Pause / Continue

Click **Pause**.

Expected:

- target stops advancing;
- state becomes `Paused`;
- one `Paused` / `PauseRequested` neutral event is added;
- Continue and Detach enable, Pause disables.

Click **Continue**.

Expected:

- target resumes;
- state becomes `Running`;
- one `Resumed` event is added;
- Pause and Detach enable, Continue disables.

Repeat Pause -> Continue at least three times.

### 4. Dedicated transport isolation

While attached and Running, return to the main window and use safe established target operations such as process Refresh and a Memory Viewer read.

Expected: primary target traffic remains usable, debugger remains attached, ordinary target traffic does not become debugger events, and no framing error occurs.

### 5. Explicit detach / reattach

Detach while Running, reattach, then repeat once by pausing and detaching from Paused state.

Expected: backend ownership and TCP `755` are released and the target is not left permanently stopped. Reattach must not require reconnecting the whole PS5 plugin.

### 6. Window-close cleanup

Attach, close the Debugger window with X, open a fresh window, and Attach again.

Expected: no stale attachment or port-in-use error remains.

### 7. Disconnect / connection-generation invalidation

Attach, use main-window **Disconnect**, then reconnect PS5 and restore the Active Target.

Expected: the old Debugger window becomes stale/unusable; a new window can attach normally to the new connection generation.

### 8. Multiple Debugger windows

Open two Debugger windows for the same PS5 target. Attach the first and attempt Attach in the second.

Expected: one-active-debugger ownership is enforced cleanly without affecting the first session. After releasing the first, the second can attach.

### 9. Opportunistic async interrupt mapping

If a naturally occurring safe debugger stop arrives, confirm it produces a neutral `Paused` event with thread id and instruction pointer and that Continue resumes execution.

Do not deliberately crash/corrupt the target solely to manufacture an interrupt. Breakpoint/watchpoint-driven interrupt testing belongs to later revisions.

## PS5 Regression Smoke Test

After debugger acceptance, detach and briefly confirm:

- process enumeration / Active Target;
- memory-map loading;
- safe memory read and optional known-safe write/read-back;
- simple scan;
- Saved Addresses;
- Memory Viewer;
- Disassembler;
- theme switching;
- Disconnect / Connect.

## Pass Criteria

`0.1.7.rev4` is fully verified only when all of the following are true:

- metadata reports `0.1.7.rev4 - Two-Row Target Header and Button Alignment`;
- clean Windows Release build succeeds;
- all **96/96** automated checks pass;
- two-row header/order/240-unit input standard passes for Mock and PS5 in Light, Dimmed, and Dark;
- ordinary 34-unit button heights remain unchanged and button labels are fully centered/unclipped;
- Mock focused regression passes;
- PS5 plugin reports `0.1.0.rev25` / API `2.12.0` and the full live debugger Attach/Pause/Continue/Detach/cleanup/transport-isolation acceptance passes;
- established PS5 workflows pass the regression smoke test.

Until every gate passes, rev4 remains the current implementation candidate. The next debugger milestone must start from the exact rev4 package that completes this verification.
