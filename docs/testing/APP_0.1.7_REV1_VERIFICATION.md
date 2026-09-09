# Application 0.1.7.rev1 Verification — Debugger Contracts and Core Foundation

## Scope

Application `0.1.7.rev1 - Debugger Contracts and Core Foundation` begins the Debugger feature block after the complete `0.1.6.rev14` Disassembler block was fully tested and hardware-verified.

Rev1 is deliberately a shared architecture revision. It introduces the neutral Plugin SDK debugger surface and Core-owned debugger session coordination, but it does **not** add a WPF Debugger workspace, a deterministic Mock debugger backend, a PS5 debugger transport/provider, or any debugger capability advertisement from the built-in plugins.

The purpose of this verification cycle is therefore to prove that the new public/Core foundation is structurally sound and that the previously verified scanner, export, Memory Viewer, Disassembler, Mock, and PS5 production paths remain unchanged.

## Expected Versions

| Component | Expected version |
| --- | --- |
| Application | `0.1.7.rev1` |
| Feature | `Debugger Contracts and Core Foundation` |
| Plugin API | `2.12.0` |
| In-Memory Test Target | `1.0.0.rev7`, targets API `2.11.0` |
| PlayStation 5 plugin | `0.1.0.rev24`, targets API `2.11.0` |

The two built-in plugins intentionally do not receive new revisions in rev1. The host's existing Plugin API compatibility rule accepts plugins that share major API version `2` and target the same or an older minor version, so both `2.11.0` plugins remain compatible with host API `2.12.0`.

## Rev1 Contract Checks

Verify that Plugin API `2.12.0` exposes the new architecture-neutral debugger contracts:

- `IDebuggerProvider`;
- `IDebuggerSession`;
- `IDebuggerThreadService`;
- `IDebuggerThreadControlService`;
- `IDebuggerRegisterService`;
- `IDebuggerBreakpointService`;
- `IDebuggerCallStackService`;
- `IDebuggerStepService`;
- `DebuggerSessionExtensions`.

Verify the neutral model surface covers:

- debugger execution state;
- debugger event kind and stop reason;
- debugger event payloads;
- thread identity/state;
- fixed-width register values and semantic register roles;
- register write requests;
- stack frames;
- software/hardware breakpoint and watchpoint requests;
- breakpoint access mode;
- step kind.

The contracts must contain no PS5, ps5debug-NG, x86-64, Windows-debugger, Xbox 360, or other backend-specific transport structure.

## Capability Checks

Verify that `TargetCapabilities.ThreadControl` exists as a separate capability from `ThreadEnumeration`.

Verify that neither built-in plugin advertises any newly implemented debugger capability in rev1. In particular, Mock `1.0.0.rev7` and PS5 `0.1.0.rev24` must not begin advertising `Debugger`, `ThreadEnumeration`, `ThreadControl`, `RegisterAccess`, `Breakpoints`, `Watchpoints`, `CallStack`, or `StepExecution` merely because API `2.12.0` defines the shared contracts.

## Core Coordinator Checks

Verify `DebuggerSessionCoordinator` and its supporting Core models enforce the following rules:

1. the coordinator is created for a specific plugin/process/connection-generation identity;
2. the identity must match the target process supplied to the coordinator;
3. attach operations are serialized and are legal only from the detached state;
4. the process returned by a backend debugger attachment must match the process requested by Core;
5. failed or mismatched attachments are cleaned up rather than retained as an active session;
6. Pause and Continue delegate only to the currently attached session;
7. debugger events are accepted only from the current attached session;
8. every accepted event is wrapped with the immutable debugger session identity;
9. accepted events receive a positive monotonic session-local sequence number;
10. detach unsubscribes events, invokes backend detach, disposes the attached session, and returns Core to the detached state;
11. stale events after detach are not forwarded;
12. optional debugger services are resolved from the attached debugger session, not inferred from a platform name.

## Automated Verification Registry

The verification executable should contain **84 checks**.

The complete 79-check `0.1.6.rev14` registry must remain present. Rev1 adds exactly these five debugger-foundation checks:

1. **Debugger neutral model contracts**;
2. **Debugger optional service contracts**;
3. **Debugger session target identity**;
4. **Core debugger session lifecycle and event binding**;
5. **Core debugger attach safety and cleanup**.

No previous verification check should be removed, bypassed, or weakened.

## Windows Build and Automated Test Procedure

On Windows, from the repository root:

1. perform a clean Release build of the solution;
2. confirm the build completes without compiler errors or warnings introduced by rev1;
3. run the verification executable;
4. confirm the run ends with:

```text
All 84 checks passed.
```

Any build failure or failed check blocks rev1 verification.

## Regression Boundary

Rev1 must preserve the production implementations of both built-in plugins. Source review should confirm no production file under either plugin project was changed as part of the debugger foundation.

The following already verified behavior must remain unaffected:

- target connection/disconnection and process discovery;
- Active Target handling and connection-generation safety;
- memory maps, reads, writes, and process suspend/resume;
- Core scanning and PS5 TurboScan paths;
- Saved Addresses and Frozen writes;
- universal export;
- Memory Viewer navigation/editing/bookmarks/value-span presentation;
- Disassembler decoding, syntax highlighting, origin resolution, navigation/history, selection/copy, Follow Target, region navigation, and export.

A short Mock smoke test of the existing scanner/Memory Viewer/Disassembler paths is sufficient for rev1 because no new user-facing debugger path exists yet.

## Hardware Requirement

There is no new PS5 debugger implementation to hardware-test in `0.1.7.rev1`. Live PS5 debugger attachment must **not** be treated as a rev1 requirement and no PS5 debugger command should be sent by this revision.

The already completed `0.1.6.rev14` live-PS5 acceptance remains the hardware baseline. New debugger hardware verification begins only when the PS5 plugin receives its actual debugger transport/provider in a later `0.1.7` revision.

## Pass Criteria

`0.1.7.rev1` is verified when all of the following are true:

- application metadata reports `0.1.7.rev1 - Debugger Contracts and Core Foundation`;
- Plugin API metadata reports `2.12.0`;
- Mock and PS5 plugin versions/API targets remain unchanged and load successfully;
- no built-in plugin advertises debugger support yet;
- clean Windows Release build succeeds;
- all **84/84** automated checks pass;
- source review confirms the rev1 contracts/Core foundation remains platform-neutral;
- existing Mock smoke-test behavior remains intact.

## Verification Result

`0.1.7.rev1` has completed its authoritative Windows verification gate. The verification executable passed **84/84** checks.

Rev1 contains no WPF Debugger workspace and no platform debugger backend, so no new live-hardware debugger acceptance was applicable. The revision is therefore the verified contracts/Core baseline for `0.1.7.rev2` and later debugger work.
