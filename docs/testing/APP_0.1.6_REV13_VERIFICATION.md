# TeeKay87's Memory Engine 0.1.6.rev13 Verification

## Purpose

Application `0.1.6.rev13 - Disassembler Context Selection Fix` is a narrow corrective revision built from the rev12 finalization candidate. Rev12 passed all 77 automated checks and its Mock runtime pass confirmed the combined Disassembler feature set except for one Extended-selection context-menu defect: right-clicking a selected row other than the primary `SelectedItem` could collapse the multi-selection to the clicked row.

Rev13 corrects only that host selection path. It does not add another Disassembler feature milestone and does not change Core disassembly, universal export writers, Plugin SDK contracts, Mock decoding, PS5 decoding/transport, scanner behavior, Memory Viewer, or Saved Addresses.

Version `0.1.6` remains open until this focused correction and the remaining live-PS5 final acceptance are verified.

## Expected Version State

```text
Application:     0.1.6.rev13
Feature:         Disassembler Context Selection Fix
Plugin API:      2.11.0
Mock plugin:     1.0.0.rev7
PS5 plugin:      0.1.0.rev24
```

The native Windows title and compact top application row must continue to show only `TeeKay87's Memory Engine`. `0.1.6.rev13` belongs only in the permanent bottom status bar.

## 1. Clean Windows Build

1. Extract the rev13 package to a clean source directory.
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
All 78 checks passed.
```

Rev13 adds exactly one check to rev12's 77-check registry:

- `Disassembler right-click preserves extended selection`.

The new check verifies that the production Disassembler XAML/code-behind intercepts right-click on an already-selected row before the DataGrid can replace the selected set, retains the explicit collapse path for an unselected context row, does not rewrite `SelectedInstruction` merely while opening the context menu, and can evaluate Follow Target capability for the context row directly.

No previous check may be removed or weakened.

## 3. Focused Mock Right-Click Regression

Connect **In-Memory Test Target**, set the Mock process as Active Target, open **Disassembler...**, and navigate to:

```text
0x10000400
```

Create a multi-selection spanning several rows, preferably including the deterministic fixture:

```text
0x10000400  load r0, 0x2A
0x10000402  call 0x10000408
0x10000404  add r0, 0x01
0x10000406  jump-if 0x1000040A
0x10000408  return
```

Verify all of the following independently:

1. Right-click the **first** row in the selection. The complete multi-selection remains selected.
2. Recreate/retain the selection and right-click a **middle** selected row that is not the primary selected row. The complete multi-selection remains selected.
3. Right-click the **last** selected row. The complete multi-selection remains selected.
4. From each of those context positions, choose **Copy Selected**. The clipboard must contain the entire selected set in displayed order, not only the row that opened the menu.
5. Confirm `Ctrl+C` still copies the complete selected set.
6. Right-click a row **outside** the current selection. The old multi-selection must collapse and only the new context row must be selected.
7. On that unselected-row case, verify **Copy Address**, **Copy Bytes**, **Copy Instruction**, and **Copy Address + Instruction** use that new context row.

**Pass condition:** context-clicking any already-selected row preserves the full selected set; context-clicking outside the set still produces the intentional single-row context.

## 4. Follow Target Context Regression

The selection fix must not break rev11 Follow Target semantics.

1. Multi-select rows including `call 0x10000408`.
2. Right-click the selected `call` row even when it is not the primary selected row.
3. Confirm **Follow Target** is enabled for that context row.
4. Invoke it and confirm navigation reaches `0x10000408`.
5. Use Back and confirm the previous origin is restored.
6. Right-click a selected non-flow row such as `add r0, 0x01`; Follow Target must remain disabled for that row.

The context menu must determine Follow Target availability from the clicked instruction without first changing the primary grid selection.

## 5. Rev12 Feature Smoke Regression

A complete repeat of every Mock rev12 test is not required after this host-only correction, but perform a short smoke check:

- Architecture still shows `Custom / Unknown`;
- Region Start/End still work and Previous/Next Region remain disabled on the one-region Mock map;
- Back/Forward still traverses successful navigation;
- syntax/origin presentation is unchanged;
- Displayed/Selected export still opens and one export completes;
- no selection action changes row dimensions.

## 6. Remaining Live PS5 Finalization

After sections 1-5 pass, continue the live-target portions of the rev12 final checklist. At minimum verify:

- Previous/Next readable-region navigation on a real multi-region process;
- Region Start/End and coherent Back/Forward history;
- Refresh remains history-neutral;
- module-relative presentation is correct when real module metadata exists and blank when it does not;
- real executable-memory disassembly still matches Memory Viewer bytes;
- continuous mid-instruction origin resolution remains correct;
- direct Call/Jump/ConditionalJump Follow Target works;
- indirect flows without a provider-supplied target remain non-followable;
- Scan Results, Saved Addresses, and Memory Viewer still open the expected address in Disassembler;
- stale Disassembler sessions never redirect to a newly connected/selected target;
- Light, Dimmed, and Dark remain layout/theme correct.

Use `APP_0.1.6_REV12_VERIFICATION.md` sections 6-10 for the detailed live-PS5 steps where needed.

## 7. Final 0.1.6 Acceptance

Version `0.1.6` can be marked complete only when:

1. the rev13 clean Windows build succeeds;
2. all **78/78** automated checks pass;
3. the focused Mock right-click multi-selection regression passes;
4. Follow Target context behavior remains correct;
5. the short rev12 smoke regression passes;
6. the remaining live-PS5 Disassembler finalization passes;
7. no unrelated verified subsystem regression is found.

If all seven gates pass, no further `0.1.6` revision is required for the planned Disassembler feature block and development may advance to the next version/feature block.
