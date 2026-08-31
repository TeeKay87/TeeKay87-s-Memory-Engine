# PlayStation 5 Process Enumeration Verification

## Purpose

This document defines verification for the PS5 plugin's first process-enumeration implementation introduced with host application `0.1.1.rev3` and PS5 plugin `0.1.0.rev2`.

The feature must prove that the plugin can parse ps5debug-NG process-list responses and expose them through the existing platform-neutral `IProcessProvider` contract without introducing PlayStation-specific process types into Core or WPF.

## Automated Loopback Coverage

`Ps5ProtocolTestServer` can now continue beyond the normal connection handshake and serve a deterministic `CMD_PROC_LIST` response.

The fixture sends:

```text
Status: 0x80000000
Count:  3
```

with representative entries:

| PID | Name |
| ---: | --- |
| `101` | `SceShellCore` |
| `2222` | `eboot.bin` |
| `3333` | `WebProcess` |

The verification executable checks that:

1. the client sends `CMD_PROC_LIST` with standard packet magic and zero request-body length;
2. the plugin accepts the documented wire success status;
3. the `uint32` process count is parsed correctly;
4. each 36-byte process entry is parsed at the documented offsets;
5. NUL-padded process names are decoded correctly;
6. signed PS5 PIDs are validated and mapped to `TargetProcess.Id`;
7. the connected `Ps5TargetSession` exposes `IProcessProvider`;
8. the resulting neutral process list contains the expected names and PIDs;
9. `Ps5TargetSession` exposes `IForegroundProcessProvider`;
10. when `eboot.bin` exists, the preferred-process service returns that neutral `TargetProcess`.

## Rev5 Preferred Target Process Selection

From PS5 plugin `0.1.0.rev8`, the plugin advertises `ForegroundProcess` and uses the existing neutral `IForegroundProcessProvider` contract to nominate `eboot.bin` when it exists.

The host applies this only when the process list has been refreshed and no previous Target Process selection could be restored. The behavior is deliberately limited to selection:

- `eboot.bin` becomes the selected **Target Process** row automatically;
- the user may still choose another process manually;
- a manual selection is preserved on later Refresh operations when the same process still exists;
- the preferred-process result does **not** automatically call **Set Active Target**;
- the WPF host does not compare the platform name or hard-code `eboot.bin`; the PS5-specific choice remains inside the plugin service.

## Required Live-Target Verification

After building `0.1.1.rev3` on Windows:

1. start ps5debug-NG on the PS5;
2. launch TeeKay87's Memory Engine;
3. select **PlayStation 5** and connect;
4. confirm that the **Target Processes** panel populates automatically;
5. verify that the list contains multiple real processes and includes expected names such as `SceShellCore` and, while a game is running, `eboot.bin`;
6. confirm `eboot.bin` is automatically selected in **Target Process** when it exists;
7. choose a different process manually, press **Refresh**, and confirm that manual selection is preserved while that process still exists;
8. select `eboot.bin` again and confirm the Active Target field has still not changed automatically;
9. choose **Set Active Target**;
10. verify that Active Target shows the chosen process name and PID;
11. choose **Refresh** and verify that the active target remains selected when the same PID/name pair still exists;
12. start or stop a process if practical, refresh, and verify that the list changes without application failure;
13. disconnect and confirm that the process list, selected process, and active target are cleared;
14. reconnect and confirm that a new process list can be retrieved normally and `eboot.bin` is preferred again when no manual selection is being restored.


## Recorded Live Result

On 2026-08-30, live testing against a physical PS5 confirmed that TeeKay87's Memory Engine `0.1.1.rev3` can connect through the PS5 plugin and retrieve/display the console's real process list.

This provides a live pass for the fundamental `CMD_PROC_LIST` path and its translation into the host's generic process presentation. The user did not separately report results for every detailed selection, Refresh, Active Target persistence, process-start/stop, disconnect, reconnect, or error-state item in the full procedure below. Those checks therefore remain explicit regression requirements.

## Error and State Checks

Also verify:

- Refresh is disabled before connection;
- Set Active Target is disabled when no process row is selected;
- Set Active Target is disabled when the selected row is already the active target;
- a process-list error is shown in the process panel without converting a successful connection into a false connection failure;
- the shared disabled-button styling remains readable for the new process buttons;
- changing to the In-Memory Test Target uses the same generic process panel and `IProcessProvider` path.

## Preparation-Environment Status

The source-preparation environment does not contain the .NET SDK or Windows WPF runtime, so native compilation and live PS5 execution cannot be performed there.

Static source checks and the deterministic loopback fixture are prepared in the source tree. Final live verification must be recorded after the Windows build is tested against a physical PS5.
