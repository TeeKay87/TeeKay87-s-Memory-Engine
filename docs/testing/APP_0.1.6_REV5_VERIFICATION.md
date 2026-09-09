# TeeKay87's Memory Engine 0.1.6.rev5 Verification

## Revision

```text
Application: 0.1.6.rev5
Feature: Continuous Disassembly Stream and Origin Resolution
Plugin API: 2.10.0
Mock plugin: 1.0.0.rev5
PS5 plugin: 0.1.0.rev23
```

## Purpose

This revision corrects the instruction-boundary behavior discovered during live PS5 verification of `0.1.6.rev4`.

Rev4 successfully added a bounded around-origin page containing up to 512 bytes before and 512 bytes from the requested address, but it deliberately decoded those two parts as separate streams. That protected the exact requested byte from uncertain pre-context alignment, but it also created an artificial boundary at the origin. If the requested address was inside a legitimate multi-byte instruction, the instruction before the origin was truncated and the bytes beginning at the requested address were decoded as a new, false instruction stream.

Rev5 keeps the same bounded target-memory acquisition and changes only the decode orchestration:

```text
one bounded around-origin memory read
        |
        v
one continuous provider decode over the complete returned range
        |
        v
origin row = decoded instruction containing RequestedAddress
```

The requested address remains the user's origin and is not silently rewritten to the instruction start. On variable-length architectures, the first byte of the complete context window may itself still be in the middle of an instruction, so the first rows remain explicitly best-effort.

The verified `0.1.6.rev4` base passed all **65 automated checks** on Windows before this revision was created.

## 1. Clean Windows Build

From a clean checkout/extraction of this revision, remove any old `bin` and `obj` folders and build the solution normally on Windows.

Expected:

- the solution builds without errors or warnings;
- the WPF application builds as `0.1.6.rev5`;
- Plugin API remains `2.10.0`;
- Mock plugin remains `1.0.0.rev5`;
- PS5 plugin remains `0.1.0.rev23`;
- the PS5 plugin output still includes its existing private Iced dependency/dependency metadata;
- no additional runtime dependency is introduced by rev5.

Rev5 does not change any Plugin SDK contract, plugin metadata, package reference, or ps5debug-NG transport implementation.

## 2. Automated Verification Suite

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 66 checks passed.
```

The 65 rev4 checks must remain green.

The new check is:

```text
Core disassembly continuous origin resolution
```

It deliberately requests an origin at the second byte of the Mock Target's two-byte `load` instruction and verifies that:

- the returned instruction still begins at the original instruction start;
- the instruction retains its two-byte length;
- the instruction still decodes as `load`;
- the instruction address range contains the requested origin;
- Core does not fabricate a new instruction beginning at the interior origin byte.

## 3. Application Version and Title Presentation

Launch the application.

Expected:

- native Windows title: `TeeKay87's Memory Engine`;
- compact top application row: `TeeKay87's Memory Engine`;
- no version/revision in either of those title surfaces;
- permanent bottom status bar: `0.1.6.rev5` at the right edge;
- the feature title is not rendered as permanent UI chrome.

This preserves the title/version behavior verified at the end of `0.1.5` and retained through the current Disassembly block.

## 4. Mock Continuous-Origin Runtime Test

Use the In-Memory Test Target.

1. Connect.
2. Select the Mock process.
3. Set it as Active Target.
4. Open **Disassembler...**.
5. Enter:

```text
0x10000401
```

6. Choose **Go To**.

The deterministic fixture begins at:

```text
0x10000400  10 2A  load r0, 0x2A
```

The requested origin `0x10000401` is intentionally the second byte of that instruction.

Expected rev5 behavior:

- the row beginning at `0x10000400` remains a complete two-byte `load` instruction;
- that `0x10000400` row receives the green origin marker because it contains `0x10000401`;
- there is no fabricated instruction beginning at `0x10000401`;
- Address remains `0x10000401` in the input field;
- surrounding rows before and after remain visible according to the bounded context policy;
- selecting another row does not remove the origin marker or change row height/width.

This is the deterministic acceptance test for the rev5 correction.

## 5. Canonical-Boundary Regression

Still on Mock Target, navigate to:

```text
0x10000400
```

Expected:

```text
0x10000400  10 2A  load r0, 0x2A
0x10000402  20 04  call 0x10000408
0x10000404  11 01  add r0, 0x01
0x10000406  31 02  jump-if 0x1000040A
0x10000408  40     return
0x10000409  00     nop
0x1000040A  40     return
```

The exact formatting may follow the existing Mock presentation rules, but addresses, raw bytes, mnemonics, instruction lengths, and direct targets must match the deterministic fixture.

Expected:

- a requested address that already is an instruction boundary still marks that instruction normally;
- rev5 does not shift or change a known canonical origin;
- Go To and Refresh retain the same target/session behavior as rev4.

## 6. Bidirectional Context Regression

At an address with sufficient bytes on both sides, verify the Disassembler Visible range.

Expected default policy:

```text
up to 512 bytes before RequestedAddress
+ up to 512 bytes starting at RequestedAddress
= up to 1,024 bytes total
```

Expected:

- one containing readable, non-guarded region only;
- no read into a previous/next mapping to fill the page;
- near region start, unavailable pre-context is omitted;
- near region end, unavailable forward context is omitted;
- the returned range always contains the requested origin;
- the status line reports the actual returned byte count;
- no additional target-memory read is introduced by the continuous decode change.

## 7. Live PS5 Boundary-Resolution Test

Use a real PS5 and select `eboot.bin` as the Active Target.

Prefer a `Read, Execute` region so decoded bytes represent executable code.

### Reproduce the rev4 observation when practical

The rev4 runtime test exposed a useful real example at:

```text
Requested origin: 0x42D43C
Containing executable region: 0x400000 - 0x1AC7FFF
Protection: Read, Execute
```

Memory Viewer showed the surrounding bytes containing:

```text
0x42D439  C7 40 28 14 00 00 00
```

The requested byte `0x42D43C` lies inside that seven-byte instruction.

Rev4 split the decode at `0x42D43C`, producing a truncated/invalid record before the origin and unrelated instructions beginning from the interior bytes.

Rev5 expected behavior when the same target build/address is still available:

- the instruction beginning at `0x42D439` remains one complete seven-byte instruction;
- the mnemonic should decode as `mov` with the existing Iced/NASM presentation;
- the `0x42D439` row is the green origin row because its byte range contains `0x42D43C`;
- there is no new instruction beginning at `0x42D43C` solely because it is the requested address;
- the Address field remains `0x42D43C`;
- bytes shown by Disassembler match the same target bytes in Memory Viewer.

If that exact executable/build/address is no longer available, use another known multi-byte x86-64 instruction and request an address one or more bytes inside it. The acceptance rule is the same.

## 8. Live PS5 Normal Executable Stream

Choose a known instruction boundary inside a `Read, Execute` mapping and compare Memory Viewer with Disassembler.

Expected:

- bytes match byte-for-byte;
- coherent x86-64 instructions are produced;
- direct calls/jumps retain their statically decoded targets;
- indirect calls/jumps do not fabricate targets;
- normal executable code does not regress because of the rev5 continuous window.

The PS5 provider itself is unchanged from `0.1.6.rev2`; this test verifies the new Core orchestration around the already-verified provider.

## 9. Start-of-Window Boundary Notice

The rev5 Disassembler notice should explain the new semantics rather than the removed rev4 split rule.

Expected meaning:

- the complete visible range is decoded as one continuous stream;
- the origin marks the decoded instruction containing the requested address;
- the beginning of an arbitrary variable-length context window remains best-effort because the first byte may itself be inside an instruction.

The UI must not claim that raw x86-64 bytes alone prove the canonical boundary of the first visible instruction.

## 10. Refresh

Navigate to an origin that lies inside a multi-byte instruction and choose **Refresh**.

Expected:

- the Address field retains the same requested byte address;
- the same 512-before/512-from-origin acquisition policy is applied again;
- the complete returned range is decoded continuously again;
- the containing instruction remains the origin row when target bytes have not changed;
- changed target bytes are reflected after Refresh;
- Refresh does not create Back/Forward history because that feature is still deferred.

## 11. Scan Results Context Menu Regression

Run a scan and right-click a Scan Result.

Expected menu labels remain:

```text
Save Address
Open in Memory Viewer
Open in Disassembler
Copy address
Copy value
```

Expected behavior:

- **Open in Memory Viewer** opens the existing Memory Viewer behavior;
- **Open in Disassembler** opens the clicked address as the requested Disassembler origin;
- the resulting Disassembler uses rev5 continuous around-origin decoding;
- capability/state gating remains unchanged;
- no `Browse Memory` label has returned.

## 12. Saved Addresses Context Menu Regression

Right-click a Saved Address belonging to the current Active Target.

Expected:

- **Open in Memory Viewer** remains available according to the existing Memory Viewer rules;
- **Open in Disassembler** remains enabled only when the saved target identity matches the current Active Target and required Disassembly/memory services are available;
- opening the item keeps the saved address as RequestedAddress and uses rev5 continuous decoding.

Then change the Active Target/process so the Saved Address identity no longer matches.

Expected:

- **Open in Disassembler** is disabled;
- the application does not silently apply the old address to the new target.

## 13. Target / Connection Generation Safety

Open a Disassembler window, then disconnect/reconnect or switch the Active Target.

Attempt **Refresh** or **Go To** in the old window.

Expected:

- stale workspace identity is rejected;
- the old window does not silently follow the new connection/process;
- the previous successful view remains visible with an error/status message;
- no PS5-specific exception leaks into the generic UI.

## 14. Foreground Target-I/O Coordination

Exercise the Disassembler while Saved Addresses refresh/Frozen work is active.

Expected:

- explicit Disassembler navigation uses the existing foreground target reservation;
- background Saved Address work reaches its established safe idle boundary;
- rev5 does not create a new independent memory-read channel;
- no overlapping host operation corrupts the target command stream.

## 15. Region Boundaries

Test an origin near the beginning of a readable region and another near the end.

Expected:

- context never crosses into another region;
- Core still rejects unreadable and Guard mappings;
- Execute remains presentation metadata, not a Core requirement;
- a readable non-executable region can still be decoded, with the normal understanding that data may produce meaningless but technically valid instructions.

## 16. Theme and Layout Regression

Check Light, Dimmed, and Dark themes.

Expected:

- Disassembler surfaces remain theme-aware;
- origin row uses the existing `SuccessMutedBrush`;
- invalid records use the existing error brush;
- no hardcoded rev5 color is visible;
- Back/Forward disabled state remains consistent with other disabled controls;
- selecting the origin row or another row does not change row height, width, border geometry, or create a selection-driven horizontal scrollbar.

## 17. No Plugin/API Regression

Verify plugin metadata/details.

Expected:

```text
Plugin API: 2.10.0
Mock: 1.0.0.rev5 / API 2.10.0
PS5: 0.1.0.rev23 / API 2.10.0
```

No plugin revision is expected because rev5 changes only shared Core decode orchestration and host presentation wording. The actual architecture-specific providers remain unchanged.

## 18. Preservation Checks

Smoke-test the previously verified application areas affected only indirectly by shared target state:

- Connect/Disconnect;
- process enumeration and Active Target selection;
- First Scan / Next Scan / New Scan;
- Scan Results selection/context menu;
- Saved Addresses add/edit/freeze/remove;
- Memory Viewer open/navigation/read/edit/bookmarks;
- universal export entry points;
- theme switching;
- application status/version presentation.

No rev5 change is intended to redesign those workflows.

## Acceptance

`0.1.6.rev5` is accepted when:

1. the clean Windows build succeeds;
2. all **66/66** automated checks pass;
3. Mock Target `0x10000401` resolves to the `0x10000400` two-byte instruction without a fabricated row at `0x10000401`;
4. a live PS5 interior-origin test keeps the real multi-byte instruction intact across the requested address;
5. Memory Viewer and Disassembler bytes continue to match on the live target;
6. the ~512-before/~512-from-origin bounded context and region clamps remain correct;
7. Scan Results/Saved Addresses workspace entry points remain functional and target-safe;
8. title/version, theme, origin geometry, stale-session, and foreground-I/O regressions are absent.

After those checks pass, the next `0.1.6` work can proceed to successful-address Back/Forward navigation and the remaining cross-workspace navigation milestones.
