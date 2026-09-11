# Application 0.1.7.rev16 Verification — Hardware Watchpoints

## Status

**VERIFIED.** The clean Windows automated suite passed **118/118**, and the complete focused Mock/live-PS5 Hardware Watchpoints acceptance passed. Rev16 is the verified baseline for rev17 Call Stack, Call Frames and Stepping.

## Verified Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev16` |
| Feature | `Hardware Watchpoints` |
| Plugin API | `2.15.0` |
| Mock plugin | `1.0.0.rev14` |
| PS5 plugin | `0.1.0.rev33` |
| Automated checks | `118` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev16 ZIP into a clean directory.
2. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

3. Confirm the final line is exactly:

```text
All 118 checks passed.
```

Do not skip or remove a failing registration to reach the expected count.

## Gate B — Mock Hardware Watchpoint Lifecycle

Use the Mock target and attach Debugger.

1. Confirm the right pane heading is **Breakpoints / Watchpoints**.
2. Add a **Hardware Watchpoint** at Mock Health `0x10000100`, Access `Write`, Size `4`, Persistent.
3. Confirm the row is Enabled and identifies a Hardware / Write / Persistent definition.
4. From Paused, click Continue.
5. Expected stop:
   - State returns to Paused;
   - Event kind/stop reason is Watchpoint;
   - Instruction Point is the synthetic accessing code instruction (`0x10000404` in the current fixture);
   - the triggered breakpoint/watchpoint context identifies `0x10000100` as the watched address;
   - the persistent row remains Enabled.
6. Continue again and confirm the persistent watchpoint can hit again.
7. Disable it and verify it does not produce a deterministic watchpoint hit while disabled; enable it again and confirm it can hit.
8. Remove it and confirm no stale state remains.

## Gate C — Mock Validation and Temporary Lifetime

1. Add a temporary Hardware Watchpoint at a naturally aligned mapped data address using a supported width.
2. Trigger it once.
   - Expected: Watchpoint event and the row disappears after the first hit.
3. Verify legal Mock widths: 1, 2, 4, 8.
4. Verify an unaligned request for a multi-byte width is rejected.
5. Verify an out-of-map watched range is rejected.
6. Verify Read, Write, and ReadWrite can be represented by the Mock backend.

The automated suite owns the deterministic slot-limit/duplicate edge coverage; exhaustive manual four-slot boundary testing is optional if Gate A passed.

## Gate D — Debugger UI Contract

Confirm:

- Add offers **Software Execute Breakpoint** and **Hardware Watchpoint**;
- hardware mode shows Access and Size inputs;
- Address uses the shared hex-address live input filter and rejects non-hex input/paste; Size uses the shared unsigned-integer live input filter, while final supported-width/alignment validation remains plugin-owned;
- the existing State column remains present and a Size column shows the configured byte width;
- the existing Enable and Disable buttons remain present and functional;
- selecting a data watchpoint does **not** enable Disassembler navigation for the watched data address;
- selecting a software execute breakpoint still allows Disassembler navigation;
- the existing Breakpoints/Events proportional splitter and approximately 65/35 starting layout remain unchanged.

## Gate E — Live PS5 Single Hardware Watchpoint

Use one active hardware watchpoint first because current ps5debug-NG can clear DR6 trigger-slot status in the outgoing event packet.

1. Connect to PS5, set the intended `eboot.bin` Active Target, open Debugger, and Attach.
2. Pause and choose a known valid writable data address whose value can be changed predictably.
3. Add a **Hardware Watchpoint**, Access `Write`, with the correct naturally aligned width (normally 4 bytes for a known 32-bit/Float value).
4. Continue.
5. Cause the game or an external normal write path to modify the watched value.
6. Expected:
   - Debugger returns to Paused;
   - stop is reported as Watchpoint;
   - Instruction Point is a code instruction that accessed the data, not the watched data address;
   - the watchpoint row still identifies the original watched address;
   - Threads and Registers populate normally after the stop.

If the event message says that DR6 trigger-slot status was not preserved but exactly one managed watchpoint was active, that is the intended safe single-watchpoint fallback and is acceptable for the current backend.

## Gate F — PS5 Persistent, Temporary, Enable/Disable, and Cleanup

With a known working watched address:

1. Persistent watchpoint: trigger it at least twice and confirm the row remains.
2. Temporary watchpoint: trigger once and confirm it is removed after the first attributed hit.
3. Disable while Paused and confirm the target remains Paused; Continue and confirm the disabled watchpoint does not hit.
4. Enable again and confirm it can hit.
5. Remove while Paused and confirm no stale row/backend state remains.
6. Add again on the same address to confirm the slot was released.
7. Test Remove All while Paused with multiple managed definitions; confirm the target/session remains usable.
8. Detach/Reattach and confirm no watchpoint state leaks into the new debugger session.
9. Close the Debugger while attached, reopen, Attach, and confirm the old hardware slots/state are gone.
10. Disconnect/Reconnect the target and confirm connection-generation invalidation clears the old watchpoints.

## Gate G — Current ps5debug-NG Multi-watchpoint Attribution Limitation

Current upstream event dispatch can clear DR6 B0-B3 before the client receives the interrupt. With more than one active hardware watchpoint, exact attribution may therefore be unavailable on the tested backend.

If testing multiple simultaneous live PS5 watchpoints:

- when DR6 is preserved, the correct watchpoint should be attributed normally;
- when DR6 arrives as zero, rev16 must **not guess** between multiple active watchpoints;
- the stop should be represented as a generic Signal/Other event explaining that exact watchpoint attribution is unavailable;
- no temporary watchpoint may be removed arbitrarily in that ambiguous case.

Reproducing this upstream limitation is **not** a rev16 failure if the host behaves conservatively as described. The external backend issue is documented in `docs/bug-reports/ps5debug-ng-watchpoint-interrupt-clears-dr6-trigger-status.md`.

## Gate H — Software Breakpoint and Register Regression

Perform a focused regression smoke:

- a known-good persistent Software Execute Breakpoint still installs and hits;
- a temporary Software Execute Breakpoint still disappears after its first hit;
- paused Disable/Remove does not unexpectedly resume the PS5 target;
- immediate Continue -> same software breakpoint re-hit still repopulates Registers;
- manual Pause/Continue continues to populate the established PS5 register surface without transport hangs/framing errors;
- paused refresh does not issue `GETDBREGS`;
- Breakpoint -> Disassembler still opens the correct code address.

## Verification Result

The rev16 acceptance cycle completed successfully. The clean Windows suite reported **118/118 PASS**. Focused runtime verification then confirmed:

- Mock persistent and temporary Read/Write/ReadWrite hardware-watchpoint lifecycle and validation;
- live PS5 Write and ReadWrite data watchpoints;
- live PS5 widths of 1, 2, 4, and 8 bytes at valid naturally aligned addresses;
- all four hardware slots, exhaustion behavior, release, and slot reuse;
- persistent and temporary hit lifetime;
- Enable, Disable, Remove, and Remove All behavior;
- detach/reattach, Debugger close/reopen, and disconnect/reconnect cleanup;
- coexistence with the already verified software execute-breakpoint path;
- Threads, Registers, paused register transport, and general debugger transport regression.

The tested ps5debug-NG backend continued to exhibit the documented DR6 limitation. Rev16 handled it as designed: exact slot attribution was used when DR6 was available, the sole active managed watchpoint could be inferred when that was unambiguous, and the plugin did not guess among multiple active watchpoints when the backend omitted DR6 trigger bits. That external limitation did not invalidate rev16 because the conservative fallback and no-guess behavior passed acceptance.

## Acceptance Criteria

Rev16 acceptance required and completed:

- Windows reports **118/118 PASS**;
- Mock persistent/temporary hardware watchpoints, validation, and UI behavior pass;
- at least one live PS5 Write or ReadWrite hardware data watchpoint produces a correct Paused stop and retains the distinction between instruction pointer and watched address;
- persistent/temporary, Enable/Disable/Remove, and debugger/session cleanup pass live;
- the current backend's zero-DR6 behavior is handled by safe single-watchpoint inference or conservative multi-watchpoint ambiguity rather than incorrect attribution;
- software breakpoint and guarded register-transport regressions remain clean.

All criteria above passed. Rev16 is therefore the verified Hardware Watchpoints baseline and development may proceed to rev17.
