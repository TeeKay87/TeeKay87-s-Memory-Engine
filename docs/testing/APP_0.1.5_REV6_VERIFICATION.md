# TeeKay87's Memory Engine 0.1.5.rev6 Verification

## Revision Under Test

```text
Host application:             0.1.5.rev6
Feature:                      Memory Viewer Safe Editing and Write Support
Plugin API:                   2.9.0
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Expected automated checks:    53
```

## Purpose

Rev6 is the first Memory Viewer revision that may change target memory. The revision therefore has a narrower safety contract than general-purpose inline hex editing: the DataGrid remains a presentation-only surface, one displayed row is edited at a time through an explicit modal dialog, the complete range must still belong to one readable+writable non-guarded region, the displayed bytes are reread immediately before the write and must still match, and every issued write is read back immediately for verification. No page-protection override is introduced.

The existing `tools/preflight/` files remain present but are unchanged; further development of that helper remains paused. The Windows/Roslyn/WPF build is the authoritative compile gate.

## 1. Clean Windows Build

1. Delete stale `bin`/`obj` directories if necessary.
2. Open `TeeKay87.MemoryEngine.sln` in Visual Studio.
3. Choose **Rebuild Solution**.
4. Confirm there are no compiler/WPF markup errors and no warnings.

## 2. Automated Verification Runner

Run `TeeKay87.MemoryEngine.Tests`.

Expected ending:

```text
All 53 checks passed.
```

Rev6 adds two checks:

- **Memory Viewer safe write and stale-data protection** — verifies stale-source rejection without a write, read-back mismatch reporting after an issued write, a successful verified write, and unchanged-edit no-op behavior;
- **Memory Viewer write protection rejection** — verifies that a range exposed through a read-only memory-map snapshot is rejected without modifying target memory.

All previous 51 checks must continue to pass.

## 3. In-Memory Test Target: Basic Edit and Verification

1. Connect to **In-Memory Test Target** and make Mock Game the Active Target.
2. Scan for Ammo (`30`) and choose **Browse Memory** for `0x10000104`.
3. Confirm the viewer header reports **Read/write** and the containing region reports `Read, Write, Private`.
4. Select the row containing `0x10000104`.
5. Confirm **Edit...** is enabled.
6. Open **Edit...** and confirm the dialog shows the selected row address/byte count, the current row bytes, an editable replacement field, and the safety explanation.
7. Change only the four bytes covering the Ammo value from little-endian `1E 00 00 00` to `1F 00 00 00`, leaving every other byte in the row unchanged.
8. Choose **Write & Verify**.
9. Confirm the Memory Viewer refreshes and reports a verified write.
10. Confirm a new scan/read of Ammo observes `31`.
11. Restore the value to `30` through the same Memory Viewer edit flow and confirm verification succeeds again.

The test is accepted only if the dialog requires the complete displayed row byte count and the target reflects exactly the requested replacement bytes.

## 4. Stale Displayed-Byte Rejection

This test proves the viewer does not blindly overwrite a row that changed after it was displayed.

1. Open Memory Viewer on the Mock Ammo row while it displays Ammo `30`.
2. Open **Edit Hex Bytes...** but do not submit yet.
3. Change Ammo to `31` through another existing application path while the edit dialog still contains the older row bytes.
4. In the open edit dialog, prepare a different valid replacement and choose **Write & Verify**.
5. Confirm the Memory Viewer reports that the displayed bytes became stale/changed.
6. Confirm the attempted replacement was **not** written.
7. Confirm the viewer refreshes to the target's current bytes.

If the current modal ownership prevents changing the same mock target through the main window while the dialog is open, reproduce the stale case with a live target or another external target-side change. The automated Core check remains authoritative for the stale-no-write contract.

## 5. Read-Only / Guarded Protection Behavior

Using a target/region that does not expose Write permission, confirm:

- the viewer header reports **Read-only**;
- **Edit...** is disabled;
- **Edit Hex Bytes...** is disabled in the row context menu;
- no page-protection change or override action is offered.

The automated write-protection check must also pass.

## 6. Dialog Input Validation

For a normal 16-byte row, open **Edit Hex Bytes...** and confirm:

- hexadecimal input is case-insensitive;
- whitespace between bytes is optional;
- non-hexadecimal characters are rejected;
- too few or too many hexadecimal characters are rejected;
- submitting unchanged bytes is rejected as a no-op in the dialog;
- Cancel closes the dialog without target I/O;
- **Write & Verify** is not the implicit Enter-key default.

Repeat once on a shortened final row near a region boundary if practical and confirm the dialog requires that row's actual byte count rather than always assuming 16 bytes.

## 7. Target / Connection Safety

1. Open a Memory Viewer for a target.
2. Disconnect/reconnect or change the explicit Active Target so the open viewer no longer belongs to the current target context.
3. Attempt an edit.
4. Confirm no write is sent and the viewer reports the target/connection mismatch.
5. Open a new viewer for the current Active Target and confirm normal editing works again.

The viewer must never silently redirect an old window to another process or later connection generation.

## 8. Foreground I/O Coordination Regression

Confirm an explicit Memory Viewer edit does not collide with the existing target command stream:

1. Keep at least one Saved Address active.
2. Perform an ordinary Saved Address refresh/freeze cycle.
3. Edit a writable Memory Viewer row.
4. Confirm the viewer waits for the existing safe I/O boundary rather than issuing an unsynchronized command.
5. After the edit, confirm Saved Address refresh/freeze resumes normally.
6. Run an ordinary scan/read/write action afterward and confirm the connection remains usable.

When live PS5 testing is available, repeat a verified edit on a known safe writable scratch/game value and then run another normal ps5debug-NG operation to confirm the command stream remains synchronized. Do not use an address whose mutation could crash the game or system.

## 9. Selection / Origin / Navigation Regression

Confirm rev4-rev5 functionality remains unchanged after a write and refresh:

- the green origin row stays layout-neutral when selected;
- ordinary selection/multi-selection still works;
- Copy Address / Hex Bytes / ASCII / Row / Selected and `Ctrl+C` still work;
- Back/Forward and `Alt+Left`/`Alt+Right` still follow successful address history;
- Refresh does not add history;
- an edit-triggered refresh does not create a new navigation history entry;
- successful writing does not move the origin address unless the user explicitly navigates.

## 10. Theme Regression

Repeat the edit-dialog open/cancel path in **Light**, **Dimmed**, and **Dark**. Confirm:

- dialog surfaces, labels, inputs, information panel, validation text, and buttons remain readable;
- no operating-system fallback chrome appears;
- the Memory Viewer Read-only/Read/write status remains readable;
- the green origin marker continues to use the active theme.

## 11. Existing Application Regression Boundary

Confirm no regressions in:

- Scan Results multi-selection **Save Address**;
- First/Next/New Scan;
- Saved Address value editing, refresh, Freeze, remove, and Protection;
- universal export and pretty JSON;
- Connect/Disconnect/process/Active Target/plugin reload;
- PS5 native/resident scan behavior when live testing is available.

## Static Preparation Review

Before packaging, verify:

- `AppInfo` reports `0.1.5.rev6` and feature title `Memory Viewer Safe Editing and Write Support`;
- `MemoryViewerWriter` lives in Core and depends only on neutral Plugin SDK memory contracts/models;
- no PS5-specific code or page-protection override was added;
- the viewer's grid remains `IsReadOnly=True` and writing is explicit through the edit dialog;
- write eligibility requires Read + Write and rejects Guard;
- the pre-write reread compares the complete exact range before `IMemoryWriter.WriteAsync(...)`;
- every issued write is followed by exact-length read-back verification;
- Plugin API remains `2.9.0`;
- PS5 plugin remains `0.1.0.rev22`;
- Mock plugin remains `1.0.0.rev4`;
- automated registry contains exactly 53 checks;
- `tools/preflight/` is byte-identical to rev5;
- XAML/project XML and bundled JSON parse successfully;
- all relative Markdown links resolve;
- no `bin`, `obj`, or `.vs` directories are packaged;
- ZIP extraction reproduces the release tree byte-for-byte.

## Final Verification Result

`0.1.5.rev6` was fully user-verified before rev7 development began.

- Windows automated verification: **53/53 PASS**.
- Mock Target raw-byte editing: PASS, including larger 4-byte little-endian values.
- Real PS5 verified write/read-back: PASS at `0x227601F24`, changing Float `1.0` (`00 00 80 3F`) to `100.0` (`00 00 C8 42`); the viewer reported a verified 16-byte row write and Saved Addresses refreshed to `100`.
- Read-only protection behavior: PASS; Edit is disabled for regions without `Write`.
- Live stale-data protection: PASS; changed bytes were detected, no write was attempted, and the viewer refreshed.
- Back/Forward navigation: PASS.

The revision is therefore considered fully verified.

## Acceptance

`0.1.5.rev6` is accepted when the Windows solution builds cleanly, all **53/53** automated checks pass, the Memory Viewer can safely edit and read-back-verify a writable row, stale displayed bytes block the write, non-writable/guarded ranges cannot be edited, target/connection safety and foreground-I/O coordination hold, rev4-rev5 selection/origin/history behavior remains intact, and no previously verified scanner/Saved Address/export/PS5 behavior regresses.

After rev6 is accepted, later `0.1.5` revisions can focus on bookmarks, richer region navigation/handling, finer byte-oriented editing ergonomics if real use justifies them, and other Memory Viewer additions discovered through runtime use. Architecture-neutral disassembly remains the expected next major feature block after the Memory Viewer feature is considered complete and verified.
