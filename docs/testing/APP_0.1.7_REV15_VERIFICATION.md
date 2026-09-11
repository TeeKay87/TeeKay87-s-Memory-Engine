# Application 0.1.7.rev15 Verification — Breakpoint Runtime Validation Fixes

## Status

**VERIFIED.** The clean Windows verification suite completed at **114/114 PASS**, and the focused runtime correction gates for breakpoint address validation, immediate breakpoint re-hit register refresh, and Disassembler presentation cleanup all passed. The focused breakpoint regression smoke also passed. Rev15 is the accepted breakpoint/runtime-correction baseline for rev16.

## Verified Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev15` |
| Feature | `Breakpoint Runtime Validation Fixes` |
| Plugin API | `2.14.0` |
| Mock plugin | `1.0.0.rev13` |
| PS5 plugin | `0.1.0.rev32` |
| Automated checks | `114` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the packaged rev15 ZIP into a clean directory.
2. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

3. Confirm the final line is exactly:

```text
All 114 checks passed.
```

The strengthened breakpoint checks must pass without reducing/skipping any existing registration.

## Gate B — Mock Breakpoint Address Validation

Use the Mock target and attach Debugger.

1. Try a Software/Execute breakpoint at `0x80000104`.
   - Expected: rejected; no row is created.
2. Try a Software/Execute breakpoint at `0x10000104` (Ammo/data).
   - Expected: rejected; no row is created.
3. Add a breakpoint at the synthetic code fixture `0x10000400`.
   - Expected: accepted and Enabled.
4. Pause/Continue to trigger it.
   - Expected: normal Breakpoint event, Paused state, correct Instruction Point.

This verifies that address validation fixes the rev14 defect without breaking valid Mock breakpoint behavior.

## Gate C — PS5 Address Validation Safety

The automated PS5 protocol fixture verifies that mapped non-executable and unmapped breakpoint requests are rejected before any `CMD_DEBUG_SET_BREAKPOINT` action. **Do not intentionally submit an invalid/unmapped address to live PS5 hardware solely for this gate.**

For live smoke coverage, use the same known-good executable instruction address that passed rev14:

- attach and Pause;
- add the valid Software/Execute breakpoint;
- Continue and confirm a normal Breakpoint hit;
- remove it through the already verified paused-safe path.

## Gate D — Immediate Breakpoint Re-hit Registers

This is the primary live correction gate.

1. Attach to `eboot.bin` and add a known-good persistent breakpoint in executable code.
2. Trigger it and confirm Registers are populated while Paused.
3. With the breakpoint still Enabled, click **Continue**.
4. Let the same breakpoint re-hit immediately.
5. Confirm State returns to `Paused` and Registers repopulate rather than remaining empty.
6. Repeat the Continue -> same breakpoint hit cycle several times.
7. Confirm Threads, Instruction Point, stop reason, and event history remain correct each time.

No manual Refresh or Disable/Enable workaround should be required.

## Gate E — Disassembler UI Cleanup

Open the Disassembler and confirm:

- the full paragraph beginning `The visible range is decoded as one continuous stream...` is gone;
- there is no blank row/gap left where that paragraph used to be;
- Region / Module, Visible Range, Protection, Architecture, navigation controls, and Address / Bytes / Instruction table remain correctly aligned;
- syntax highlighting and origin selection still behave normally.

## Gate F — Focused Breakpoint Regression Smoke

A full rev14 replay is unnecessary if Gates A-E are clean, but perform this focused smoke on Mock or PS5 as appropriate:

- persistent breakpoint remains after a hit;
- temporary breakpoint disappears after its first hit;
- Enable/Disable still works;
- Remove while Paused on PS5 does not resume unexpectedly;
- Breakpoints/Events still starts around 65/35 and remains draggable;
- Detach/Reattach does not retain stale breakpoint state;
- ordinary manual Pause still populates Registers.

## Acceptance Result

Rev15 was accepted after confirming:

- Windows reports **114/114 PASS**;
- Mock invalid/mapped-data breakpoint validation passes and valid code breakpoints still hit;
- a valid live PS5 breakpoint still installs/hits without transport regression;
- repeated immediate Continue -> same breakpoint hits reliably repopulate Registers;
- the requested Disassembler explanatory text and its empty spacing are gone;
- focused breakpoint lifecycle/paused-cleanup regression smoke passes.

All listed acceptance criteria passed. The next debugger revision is **0.1.7.rev16 — Hardware Watchpoints**.
