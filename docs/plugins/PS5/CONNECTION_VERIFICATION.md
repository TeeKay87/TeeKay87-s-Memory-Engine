# PlayStation 5 Plugin Connection Verification

## Automated Protocol Test

`tests/TeeKay87.MemoryEngine.Tests` contains a loopback ps5debug-NG protocol test server used to verify the connection handshake without requiring a physical console.

The connection-handshake test independently expects the published connection constants and command sequence. It verifies that the PS5 plugin sends:

1. packet magic `0xFFAABBCC`;
2. `CMD_VERSION`;
3. `CMD_PLATFORM_ID`;
4. `CMD_BRANDING`;
5. `CMD_FW_VERSION`;
6. `CMD_PROC_NOP`.

It returns representative ps5debug-NG responses including protocol `1.3`, platform id `5`, branding with a NUL-separated capability level, firmware `12.40`, and the wire success status.

The test then verifies that:

- `ConnectAsync` returns a connected session;
- the connected session exposes the currently implemented `IProcessProvider` service;
- disposing the session transitions it to a disconnected state.

Process-list wire parsing has a separate PS5 plugin verification procedure in `PROCESS_ENUMERATION_VERIFICATION.md`.

## Live-Target Verification Procedure

On a Windows development machine with ps5debug-NG running on a reachable PS5:

1. build and launch TeeKay87's Memory Engine;
2. select **PlayStation 5**;
3. enter the PS5 IP address;
4. keep port `744` unless the target is intentionally configured differently;
5. choose **Connect**;
6. verify that the UI reports `Connected.`;
7. choose **Disconnect**;
8. verify that the UI returns to `Not connected.`;
9. repeat once to verify reconnect behavior.

Also test at least one unavailable IP or stopped ps5debug-NG instance and confirm that the application remains running and presents the connection error rather than terminating.

## Runtime Result Recorded on 2026-08-30

The Windows application was successfully built/launched from the project and a real PlayStation 5 connection to ps5debug-NG was reported working. This confirms the live Connect path remained functional after the reusable button-style revision.

This result verifies live connection establishment. It does not by itself verify disconnect/reconnect repetition, unavailable-target error handling, or the newly added process-list operation.

## Verification Status

| Check | Status |
| --- | --- |
| Protocol constants reviewed against ps5debug-NG source/reference | PASS |
| Loopback command framing and response parsing represented in tests | IMPLEMENTED |
| Live Windows build/application launch | PASS - user verified 2026-08-30 |
| Live PS5 connection | PASS - user verified 2026-08-30 |
| Disconnect/reconnect against real PS5 | PENDING USER TEST |
| Unavailable-target error handling | PENDING USER TEST |
