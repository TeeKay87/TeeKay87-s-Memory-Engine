# TeeKay87's Memory Engine 0.1.6.rev14 Verification

## Purpose

Application `0.1.6.rev14 - Disassembler Multi-Selection Copy Consistency` is a narrow corrective revision built from the rev13 finalization candidate.

Rev13 passed all 78 automated checks and the focused Mock right-click selection regression. Runtime testing then confirmed that the complete Extended selection remained intact regardless of which selected row was right-clicked, while right-clicking an unselected row correctly collapsed the selection to that row. The same test exposed one remaining inconsistency: **Copy Address**, **Copy Bytes**, **Copy Instruction**, and **Copy Address + Instruction** still copied only the context-clicked row, while **Copy Selected** and `Ctrl+C` correctly copied the complete selected set.

Rev14 corrects only that clipboard projection path. It does not change Core disassembly, universal export writers, Plugin SDK contracts, Mock decoding, PS5 decoding/transport, scanner behavior, Memory Viewer, Saved Addresses, target I/O, or theme behavior.

Version `0.1.6` remains open until this focused correction and the remaining live-PS5 final acceptance are verified.

## Expected Version State

```text
Application:     0.1.6.rev14
Feature:         Disassembler Multi-Selection Copy Consistency
Plugin API:      2.11.0
Mock plugin:     1.0.0.rev7
PS5 plugin:      0.1.0.rev24
```

The native Windows title and compact top application row must continue to show only `TeeKay87's Memory Engine`. `0.1.6.rev14` belongs only in the permanent bottom status bar.

## 1. Clean Windows Build

1. Extract the rev14 package to a clean source directory.
2. Build the complete solution in Release using the normal Windows/Visual Studio workflow.
3. Confirm there are no compiler errors or warnings.
4. Confirm both built-in plugins load normally.

**Pass condition:** clean build succeeds with zero warnings/errors.

## 2. Automated Verification

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 79 checks passed.
```

Rev14 adds exactly one check to rev13's 78-check registry:

- **Disassembler multi-selection copy consistency**

The new check verifies that all four granular copy handlers project from the shared selected-row path, that display-order normalization remains in use, and that the old single-context-row `CopyText(...)` calls are absent.

## 3. Focused Mock Runtime Regression

Connect to **In-Memory Test Target**, set the Mock process as Active Target, open the Disassembler, and navigate to:

```text
0x10000400
```

Use the deterministic Mock instruction fixture.

### 3.1 Prepare one multi-selection

Select these seven rows:

```text
0x10000400  10 2A  load r0, 0x2A
0x10000402  20 04  call 0x10000408
0x10000404  11 01  add r0, 0x01
0x10000406  31 02  jump-if 0x1000040A
0x10000408  40     return
0x10000409  00     nop
0x1000040A  40     return
```

Use Ctrl/Shift as desired.

Right-click any row already contained in the selection. The complete seven-row selection must remain intact.

### 3.2 Copy Address

Expected clipboard text:

```text
0x10000400
0x10000402
0x10000404
0x10000406
0x10000408
0x10000409
0x1000040A
```

### 3.3 Copy Bytes

Expected clipboard text:

```text
10 2A
20 04
11 01
31 02
40
00
40
```

### 3.4 Copy Instruction

Expected clipboard text:

```text
load r0, 0x2A
call 0x10000408
add r0, 0x01
jump-if 0x1000040A
return
nop
return
```

### 3.5 Copy Address + Instruction

Expected clipboard text:

```text
0x10000400: load r0, 0x2A
0x10000402: call 0x10000408
0x10000404: add r0, 0x01
0x10000406: jump-if 0x1000040A
0x10000408: return
0x10000409: nop
0x1000040A: return
```

### 3.6 Copy Selected and Ctrl+C regression

Both **Copy Selected** and `Ctrl+C` must continue to produce:

```text
0x10000400: 10 2A	load r0, 0x2A
0x10000402: 20 04	call 0x10000408
0x10000404: 11 01	add r0, 0x01
0x10000406: 31 02	jump-if 0x1000040A
0x10000408: 40	return
0x10000409: 00	nop
0x1000040A: 40	return
```

### 3.7 Context-row collapse regression

With the same multi-selection active:

1. Right-click a row that is **not** currently selected.
2. Confirm the previous selection is cleared and only the clicked row remains selected.
3. Run each copy command.

Every copy command must now produce only that one row's corresponding representation. This confirms rev14 did not break rev13's intentional outside-selection context behavior.

### 3.8 Display order

Create a non-contiguous selection using Ctrl in an order different from the visible row order.

Every copy command must still emit rows in the current displayed instruction order, not in Ctrl-click order.

**Pass condition:** all granular copy commands, Copy Selected, Ctrl+C, selection preservation, outside-selection collapse, and display-order normalization behave exactly as described.

## 4. No-Regression Checks Before Live PS5

The following rev12/rev13 Mock behavior was already runtime-confirmed and should remain unchanged. A brief smoke check is sufficient unless any unexpected behavior appears:

- Architecture shows `Custom / Unknown`.
- Follow Target works on the deterministic direct `call` and `jump-if`.
- Back/Forward works after Follow Target.
- Region Start / Region End work.
- Previous Region / Next Region remain disabled when the Mock target has only one readable region.
- module-relative origin is shown from real Mock module metadata;
- universal export remains available and does not change selection;
- syntax highlighting and origin highlighting remain layout-stable.

## 5. Remaining Live-PS5 Final Acceptance

After sections 1-4 pass, continue the outstanding live-PS5 final acceptance inherited from rev12/rev13.

Verify at minimum:

1. open Disassembler against a real readable executable PS5 region;
2. confirm real x86-64 decoding and byte agreement with Memory Viewer;
3. confirm continuous origin resolution when the requested address falls inside a multi-byte instruction;
4. test direct Follow Target on real direct `call`, `jmp`, and/or conditional branch instructions where available;
5. confirm indirect/unresolved branches do not receive fabricated targets;
6. test Previous Region / Region Start / Region End / Next Region on real mapped regions;
7. combine Go To, Follow Target, region navigation, Back, and Forward in one history sequence;
8. confirm Refresh does not create a history entry;
9. confirm module-relative origin appears only when real module metadata exists;
10. test multi-selection and all copy commands on PS5 instruction rows;
11. export Displayed and Selected Instructions and confirm no additional target traffic is required while writing;
12. verify stale-session protection by leaving an old Disassembler window open across disconnect/reconnect or target replacement;
13. verify Light, Dimmed, and Dark presentation remains readable and layout-stable.

## 6. Final Acceptance Gate for 0.1.6

Version `0.1.6` may be marked complete only when all of the following are true:

1. rev14 clean Windows build succeeds;
2. all **79/79** automated checks pass;
3. the focused rev14 Mock multi-selection copy regression passes;
4. the remaining live-PS5 Disassembler acceptance passes;
5. no regression is observed in previously verified Memory Viewer, Saved Addresses, scanner, export, target/session, or theme behavior affected by the exercised paths.

If all five conditions pass, `0.1.6` can be closed as the completed Disassembler feature block. The next version may then begin the next planned subsystem rather than adding another corrective Disassembler revision.

---

## Final Verification Result — 2026-09-08

The complete `0.1.6` Disassembler feature block has now passed its final verification cycle.

The final acceptance state is:

- the rev14 Windows build/verification cycle completed successfully;
- all **79/79** automated checks passed;
- the focused rev14 Mock multi-selection copy regression passed;
- the complete Disassembler workflow was exercised on the deterministic Mock target;
- the remaining live PlayStation 5 acceptance was completed successfully against real target memory;
- real PS5 x86-64 disassembly, continuous origin resolution, direct-target navigation, readable-region navigation, navigation history, selection/copy behavior, universal export, session safety, and theme/layout presentation were hardware-verified as part of the final `0.1.6` acceptance;
- no blocking regression was found in the previously verified Memory Viewer, Saved Addresses, scanner, export, target/session, or theme workflows exercised during the final pass.

**Final status:** `0.1.6.rev14` is the completed and hardware-verified final revision of the `0.1.6` Disassembler feature block. No further `0.1.6` corrective revision is required for the verified feature set. Development proceeds to the separate `0.1.7` Debugger feature block.
