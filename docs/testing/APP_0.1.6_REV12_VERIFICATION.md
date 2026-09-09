# TeeKay87's Memory Engine 0.1.6.rev12 Verification

## Purpose

Application `0.1.6.rev12 - Disassembler Export, Region Navigation and Finalization` combines the remaining planned `0.1.6` Disassembler work into one final implementation candidate:

- extended instruction selection and copy;
- structured universal Disassembler export;
- readable-region navigation;
- module-relative origin presentation where real module metadata exists;
- Mock Target architecture correction from X64 to Custom / Unknown;
- final regression coverage for the complete Disassembler workflow built across rev1-rev12.

This checklist is intentionally broader than an ordinary revision smoke test. **Do not mark version `0.1.6` complete or advance to `0.1.7` until this document's build/automated/runtime acceptance is confirmed.**

## Recorded Runtime Outcome

The Windows automated verification for rev12 was reported as **77/77 passed**. The subsequent Mock Target acceptance confirmed the Custom / Unknown architecture presentation, deterministic disassembly fixture, Follow Target/history, region navigation, `Ctrl+C` multi-row copy, export behavior, and the other exercised rev12 paths. One defect was found in section 4: right-clicking a selected row that was not the primary selected row collapsed the Extended selection to that row before context-menu bulk-copy behavior could use the complete selection.

That defect is corrected in **0.1.6.rev13 - Disassembler Context Selection Fix**. Final `0.1.6` acceptance therefore continues with `docs/testing/APP_0.1.6_REV13_VERIFICATION.md`; rev12 itself is not the final verified baseline.

## Expected Version State

```text
Application:     0.1.6.rev12
Feature:         Disassembler Export, Region Navigation and Finalization
Plugin API:      2.11.0
Mock plugin:     1.0.0.rev7
PS5 plugin:      0.1.0.rev24
```

The native Windows title and compact top application row must show only `TeeKay87's Memory Engine`. `0.1.6.rev12` belongs only in the permanent bottom status bar.

## 1. Clean Windows Build

1. Extract the rev12 package to a clean source directory.
2. Build the complete solution in Visual Studio/Release or run the normal Windows build workflow.
3. Confirm there are no compiler warnings or errors. Warnings are treated as errors project-wide.
4. Confirm runtime plugin deployment contains the Mock and PS5 plugin assemblies and the PS5 plugin's private Iced dependency metadata/files as established in rev2.

**Pass condition:** clean build succeeds without warnings/errors and both built-in plugins load normally.

## 2. Automated Verification

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 77 checks passed.
```

Rev12 adds four checks on top of the 73-check rev11 registry:

- `Disassembly export source structured data`;
- `Disassembler selection, copy, and export contract`;
- `Disassembler readable region navigation contract`;
- `Mock custom disassembly architecture`.

No previous check should be removed or weakened.

## 3. Mock Target — Architecture and Baseline Disassembly

1. Connect **In-Memory Test Target** and set its process as Active Target.
2. Open **Disassembler...**.
3. Confirm the Architecture field shows:

```text
Custom / Unknown · 64-bit addresses · 64-bit pointers · Little endian
```

It must **not** show X64.

4. Go To:

```text
0x10000400
```

5. Confirm the deterministic fixture still begins with the expected synthetic instructions:

```text
0x10000400  10 2A  load r0, 0x2A
0x10000402  20 04  call 0x10000408
0x10000404  11 01  add r0, 0x01
0x10000406  31 02  jump-if 0x1000040A
0x10000408  40     return
0x10000409  00     nop
0x1000040A  40     return
```

6. Confirm syntax highlighting, origin highlighting, Follow Target, and Back/Forward still behave as previously verified.

**Pass condition:** Mock is clearly presented as a synthetic custom architecture and all established deterministic Disassembler behavior remains intact.

## 4. Extended Selection and Copy

Using either Mock or PS5:

1. Select several adjacent rows with Shift.
2. Add/remove non-contiguous rows with Ctrl.
3. Confirm ordinary selection does not alter row height, column width, origin geometry, or syntax highlighting.
4. Right-click a row already inside the multi-selection. The existing multi-selection must remain intact.
5. Right-click a row outside the current selection. That row should become the row-specific context target instead of applying row-specific commands to an unrelated old selection.
6. Verify:
   - **Copy Address**;
   - **Copy Bytes**;
   - **Copy Instruction**;
   - **Copy Address + Instruction**;
   - **Copy Selected**;
   - `Ctrl+C`.
7. Confirm Copy Selected emits rows in displayed address/order, not the order in which the rows happened to be clicked.
8. Confirm copy operations perform no target read/write and do not create Back/Forward history entries.

Expected Copy Selected shape:

```text
0xADDRESS: AA BB CC	mnemonic operands
0xADDRESS: DD EE	mnemonic operands
```

**Pass condition:** all copy actions return the expected text, multi-selection remains layout-neutral, and selection/copy is history- and target-I/O-neutral.

## 5. Disassembler Universal Export

Open a populated Disassembler view and verify the visible **Export** button and context-menu **Export...** action.

### 5.1 Scopes

With no meaningful multi-selection, verify **Displayed Instructions** is available.

With multiple rows selected, verify both:

- **Displayed Instructions**;
- **Selected Instructions**.

Selected export must preserve displayed order.

### 5.2 Formats

Export at least one scope to every supported format:

- JSON;
- CSV;
- TSV;
- Markdown table.

Confirm files are created and contain the selected columns/row count.

### 5.3 Structured JSON

For JSON, select all columns and confirm the row objects contain structured fields rather than only rendered UI text:

```text
address
bytes
instruction
mnemonic
operands
length
flowControl
branchTarget
valid
regionOrModule
protection
moduleRelativeAddress
```

Confirm:

- `length` is numeric;
- `valid` is boolean;
- a known direct call/jump target is exported as the expected hexadecimal target;
- an instruction with no static target exports `branchTarget` as null;
- Protection matches the current snapshot region;
- module-relative data is present only when real module metadata/base can be resolved.

### 5.4 Existing Export Regression

Run a quick Scan Results export and Saved Addresses export after rev12's shared destination-picker refactor. Their existing scopes, formats, and output behavior must remain unchanged.

**Pass condition:** Disassembler uses the same universal export UX/writers successfully in all four formats and existing export consumers regress cleanly.

## 6. Region Navigation

### Mock

The normal Mock map contains one readable region. Confirm:

- **Region Start** navigates to the first byte when not already there;
- **Region End** navigates to the final byte when not already there;
- Previous/Next Region are disabled when no qualifying neighboring region exists;
- Back/Forward traverses successful Region Start/End moves.

### Live PS5

Use an Active Target with multiple mapped regions.

1. Open Disassembler in a readable region.
2. Test **Previous Region** and **Next Region**.
3. Confirm each destination is a readable, non-guarded region from the current memory map.
4. Test **Region Start** and **Region End**.
5. Confirm Region End navigates to the last address in the region, not `EndAddressExclusive`.
6. Use Back/Forward across a mix of Go To, Follow Target, and region navigation and confirm one coherent history timeline.
7. Press Refresh and confirm history position/count does not change.
8. If a navigation/read fails, confirm the previous successful instruction list and history position remain intact.

**Pass condition:** region navigation is usable, bounded to real readable non-guarded mappings, and fully integrated with existing history semantics.

## 7. Module-Relative Presentation

On a target/mapping where `ModuleName` is actually supplied:

1. Navigate within that module.
2. Confirm the Region / Module area shows a line shaped like:

```text
Origin: eboot.bin + 0x123456
```

3. Cross-check the offset against:

```text
requested origin - lowest mapped base for that module
```

4. Navigate into an anonymous region and confirm no module-relative label is fabricated.

If the current PS5 memory-map response does not provide module names for the tested mapping, blank module-relative presentation is the correct result and should be recorded as such rather than treated as a failure.

## 8. Live PS5 — Complete Disassembler Regression

Use a real **Read, Execute** region and repeat a compact regression of the behavior already verified through rev11:

- Memory Viewer and Disassembler bytes match at the same address;
- around-origin context shows bytes/instructions before and after the requested origin;
- a requested byte inside a multi-byte instruction highlights the containing instruction rather than creating a false stream at the byte;
- syntax highlighting remains readable and layout-neutral;
- direct call/jump/conditional targets can be followed;
- indirect flows without a static `BranchTarget` remain non-followable;
- Back/Forward behaves correctly after Go To, Follow Target, and region moves;
- Refresh leaves origin/history intact;
- Scan Results, Saved Addresses, and Memory Viewer **Open in Disassembler** routes still open the expected address/session.

The previously verified executable-region style around `0x42D43C` is a useful regression target if the same executable/build is still available, but the test is not tied to that exact address.

## 9. Session / Target Safety

1. Open a Disassembler window on Target A.
2. Disconnect/reconnect or change Active Target so the captured connection generation/process identity becomes stale.
3. Attempt Go To, Refresh, Follow Target, or a region-navigation command in the old window.
4. Confirm the old window does not silently read from Target B/new session.
5. Confirm failure leaves the previous successful view in place.

**Pass condition:** rev12 additions do not bypass the existing captured-session safety boundary.

## 10. Theme and Layout Regression

At minimum inspect Light, Dimmed, and Dark:

- Address / Bytes / Instruction columns remain unchanged;
- syntax token colors remain readable;
- origin row remains visually distinct even when selected;
- multi-selection does not change row dimensions;
- new Previous Region / Region Start / Region End / Next Region / Export controls use existing theme styles;
- no hardcoded light-theme surfaces appear in context menus or dialogs;
- disabled navigation controls remain visibly disabled;
- version/revision remains only in the permanent bottom status bar.

## 11. Final 0.1.6 Acceptance

Version `0.1.6` can be marked **complete and verified** only when all of the following are true:

1. clean Windows build succeeds;
2. all **77/77** automated checks pass;
3. Mock custom-architecture/disassembly regression passes;
4. multi-selection and all copy actions pass;
5. Displayed/Selected Disassembler export passes in JSON/CSV/TSV/Markdown;
6. existing Scan Results/Saved Addresses export regression passes;
7. readable-region navigation/history passes;
8. module-relative behavior is correct for both named and anonymous mappings;
9. full live PS5 Disassembler regression passes;
10. stale-session/target safety remains intact;
11. Light/Dimmed/Dark layout/theme regression passes;
12. no unrelated verified subsystem regression is found.

If all acceptance points pass, no additional `0.1.6` revision is required solely for planned feature work. The next version may then begin the separate Debugger foundation block. If a defect is found, remain on `0.1.6` and create the next corrective revision instead.
