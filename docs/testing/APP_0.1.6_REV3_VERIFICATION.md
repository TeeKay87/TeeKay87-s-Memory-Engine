# TeeKay87's Memory Engine 0.1.6.rev3 Verification

## Revision

```text
Application: 0.1.6.rev3
Feature:     Disassembler Workspace
Plugin API:  2.10.0
Mock plugin: 1.0.0.rev5
PS5 plugin:  0.1.0.rev23
```

## Purpose

This checklist verifies the first user-facing Disassembler workspace built on the neutral Disassembly contracts/Core reader from `0.1.6.rev1` and the PS5 x86-64 provider from `0.1.6.rev2`.

The verified `0.1.6.rev2` base passed all **64 automated checks** on Windows. Rev3 does not change the Plugin API, Mock decoder, PS5 decoder, scanner, Saved Addresses, Memory Viewer, or target transport. Its new acceptance surface is primarily WPF host integration and therefore requires both the unchanged automated suite and direct runtime/UI verification.

Rev3 intentionally does **not** yet add cross-workspace `Disassemble Here` commands, navigation history, branch/call following, disassembly copy/export, assembly, instruction editing, or debugger behavior.

## 1. Clean Windows Build

From a clean source tree:

```powershell
dotnet clean .\TeeKay87.MemoryEngine.sln
dotnet build .\TeeKay87.MemoryEngine.sln -c Debug
```

Expected:

- the complete solution builds without errors;
- the WPF application output contains both platform plugins;
- the PS5 plugin deployment still contains its private Iced dependency and `.deps.json`;
- no new runtime dependency is required by Core or the WPF application for instruction decoding.

## 2. Automated Verification Suite

Run the existing verification executable exactly as for rev2.

Expected result:

```text
64 checks passed
```

All 64 existing checks must remain green. Rev3 does not replace or weaken any of the already-verified neutral/Core/PS5-provider coverage.

## 3. Application Version and Title Presentation

Launch the application and confirm:

- the native Windows title bar shows `TeeKay87's Memory Engine` without version/revision;
- the compact top application row also shows only the application title;
- the permanent bottom status bar shows `0.1.6.rev3`;
- the revision feature title is not displayed as persistent application chrome.

This confirms the title-presentation correction retained from the end of `0.1.5` and `0.1.6.rev2` has not regressed.

## 4. Capability-Driven Disassembler Entry Point

### Mock Target

1. Select **Mock Target**.
2. Connect.
3. Select the mock process and set it as **Active Target**.
4. Wait for the memory map to be available.

Expected:

- a **Disassembler...** button is visible because Mock Target advertises `TargetCapabilities.Disassembly`;
- the button is disabled before a usable Active Target/memory map exists;
- the button becomes enabled when the current target is connected, readable, idle, and has at least one readable non-guarded region;
- no platform name is used to decide whether the button appears.

### PS5

Repeat the same state checks with the PS5 plugin.

Expected:

- the same generic button appears because the PS5 plugin advertises the neutral Disassembly capability;
- its enabled/disabled state follows the same host conditions as Mock Target.

## 5. Opening the Workspace

With Mock Target active, click **Disassembler...**.

Expected:

- a separate modeless **Disassembler** window opens;
- the window remains associated with the plugin/process/connection generation that opened it;
- the main application remains usable while the Disassembler window is open;
- opening another Disassembler window does not close the first one;
- the initial address is chosen generically from a readable region, preferring an executable region when one is available;
- no PS5, `eboot.bin`, or x86-specific name is hardcoded into the host workspace.

## 6. Workspace Layout

Confirm the window contains:

- target/process identity;
- **Back** and **Forward** controls;
- hexadecimal **Address** input;
- **Go To**;
- **Refresh**;
- Region / Module information;
- Visible range;
- Protection;
- Architecture;
- a disassembly table with **Address**, **Bytes**, and **Instruction** columns;
- status/error presentation;
- the instruction-boundary warning.

For rev3 specifically:

- **Back** and **Forward** are visible but disabled because history is a rev4 milestone;
- the default Instruction column renders mnemonic + operands together while the underlying model remains structured.

## 7. Deterministic Mock Visual Decode

In the Mock Disassembler, enter:

```text
0x10000400
```

and choose **Go To**.

The deterministic fixture should begin with the following records:

| Address | Bytes | Instruction |
| --- | --- | --- |
| `0x10000400` | `10 2A` | `load r0, 0x2A` |
| `0x10000402` | `20 04` | `call 0x10000408` |
| `0x10000404` | `11 01` | `add r0, 0x01` |
| `0x10000406` | `31 02` | `jump-if 0x1000040A` |
| `0x10000408` | `40` | `return` |
| `0x10000409` | `00` | `nop` |
| `0x1000040A` | `40` | `return` |

Additional zero-filled Mock bytes may decode as deterministic Mock `nop` records after the fixture.

Also confirm:

- the first requested-address row is marked with the theme-aware origin background;
- selecting another row does not remove the origin marker;
- selecting the origin row does not change its height, width, borders, padding, or column geometry;
- normal selection never changes row dimensions or creates a layout jump.

## 8. Address Navigation and Refresh

### Valid address

Enter another valid readable hexadecimal address and choose **Go To**.

Expected:

- the read begins exactly at the requested address;
- Address text is normalized to the current address after success;
- visible rows, region/module, range, protection, architecture, and status refresh together;
- the requested row becomes the new origin.

### Invalid text

Enter text that is not a hexadecimal address.

Expected:

- navigation is rejected locally;
- a clear error is shown;
- the previous successful disassembly remains visible.

### Unreadable / unmapped address

Enter a syntactically valid address that is not in a readable, non-guarded region.

Expected:

- Core rejects the read cleanly;
- the error is shown without replacing the last successful instruction list with fabricated data.

### Refresh

Return to a valid address and choose **Refresh**.

Expected:

- the same current address is read and decoded again;
- Refresh does not create navigation history in rev3;
- the origin remains the current requested address.

## 9. Region Boundary Behavior

Navigate close enough to the end of a readable region that fewer than the default 512 bytes remain.

Expected:

- the read is clamped to the containing region;
- the Disassembler does not read into an adjacent region to fill the page;
- Visible range reflects the actual bytes read;
- no instruction record claims bytes outside the displayed snapshot.

## 10. Target / Connection Safety

Open a Disassembler for one active target, then invalidate its identity using one of the following:

- disconnect and reconnect;
- change the Active Target to another process;
- reconnect so the connection generation changes.

Then use **Refresh** or **Go To** in the old window.

Expected:

- the old window does not silently begin reading from the new target/session;
- the operation fails with a clear stale/current-target error;
- the last successful disassembly can remain visible for reference;
- no read is issued through an unrelated target connection.

## 11. Foreground I/O Coordination

While an explicit foreground target operation is active, verify the Disassembler entry/read state follows the same target-I/O ownership rules as Memory Viewer.

Expected:

- the host does not create an independent transport or PS5 command stream for Disassembler reads;
- Disassembler reads are serialized through the existing foreground-target reservation;
- conflicting explicit reads/writes/scans are not started through the same primary session at the same time merely because a Disassembler window is open.

## 12. Live PS5 x86-64 Visual Verification

Use a known executable address that can also be inspected in Memory Viewer.

1. Connect to PS5.
2. Set the intended process as Active Target.
3. Open **Disassembler...**.
4. Enter the known executable address.
5. Open the same address in Memory Viewer.

Expected:

- Address/Bytes rows are populated by the PS5 x86-64 provider;
- instruction bytes correspond exactly to the target bytes shown by Memory Viewer at the same addresses;
- mnemonics/operands are plausible x86-64 output for those bytes;
- direct calls/jumps may already carry structured branch targets internally, but rev3 does not yet provide Follow Target interaction;
- Region / Module is backend-reported rather than fabricated;
- Protection reflects the real memory-map entry;
- Architecture reports x64, 64-bit address/pointer widths, and little-endian target metadata;
- **Refresh** rereads the current target bytes and redisassembles them.

If the entered address is not a canonical instruction boundary, the resulting decode may legitimately differ from the intended surrounding program flow. The UI warning must remain visible; rev3 does not claim otherwise.

## 13. Theme Verification

Repeat representative Mock/PS5 checks in:

- Light;
- Dimmed;
- Dark.

Expected:

- window/card/table/input/button/status surfaces use existing theme resources;
- no new hardcoded light-theme strip appears;
- the origin marker uses the existing success-muted semantic brush;
- invalid instruction text uses the existing error semantic brush;
- disabled Back/Forward controls use the normal disabled theme presentation;
- row selection remains readable while preserving origin styling.

## 14. Regression Checks

Confirm the following existing workflows remain operational:

- plugin discovery/connect/disconnect;
- process refresh and Active Target selection;
- Scan / New Scan / First Scan / Next Scan behavior;
- Scan Results;
- Saved Addresses;
- Memory Viewer, including bookmarks, edit/write, region navigation, history, origin marker, and context menu;
- status-bar version presentation;
- theme switching;
- settings;
- universal export surfaces already implemented elsewhere.

No rev3 change is intended to alter those verified workflows.

## Acceptance Criteria

`0.1.6.rev3` is accepted when all of the following are true:

1. The solution clean-builds on Windows.
2. All **64 automated checks** pass.
3. Disassembler availability is capability-driven.
4. The workspace opens modelessly for Mock and PS5 targets.
5. Mock deterministic bytes produce the expected visible instruction rows.
6. Address / Go To / Refresh work without introducing history yet.
7. Region/module/range/protection/architecture information is correct.
8. Origin marking is persistent and layout-neutral.
9. Bounded region behavior is correct.
10. Stale target/connection generations cannot be followed silently.
11. Disassembler reads use existing foreground target-I/O coordination.
12. Live PS5 bytes match Memory Viewer and decode through the PS5 x86-64 provider.
13. Light, Dimmed, and Dark themes render correctly.
14. Existing verified application functionality remains intact.
15. Version/revision remains absent from the native/top title presentation and appears only in the permanent status bar.

Once these items are verified, development can proceed to `0.1.6.rev4`, where Disassembler navigation history and cross-workspace `Disassemble Here` integration are planned.
