# Application 0.1.7.rev30 Verification — Debugger Address Actions and Breakpoint Classification

## Status

**SUPERSEDED BEFORE VERIFICATION.** Rev30 built on the verified rev29 logical Disassembler/Markers baseline and introduced the classification/address-action work described below, but its formal Windows/runtime gate was not completed. Live shutdown testing exposed a PS5 hardware-watchpoint teardown safety issue, so rev30 is superseded by `0.1.7.rev31 - Safe PS5 Watchpoint Detach Cleanup`. The gates below are retained as the historical rev30 plan and their relevant regressions are carried into rev31.

## Candidate Metadata

| Component | Expected value |
| --- | --- |
| Application | `0.1.7.rev30` |
| Feature | `Debugger Address Actions and Breakpoint Classification` |
| Plugin API | `2.16.0` |
| Mock plugin | `1.0.0.rev16` |
| PS5 plugin | `0.1.0.rev37` |
| Automated checks | `141` |

## Gate A — Clean Windows Build and Automated Verification

1. Extract the rev30 ZIP to a clean directory and build the full solution in Visual Studio.
2. Confirm no C#/XAML compile failure and no warning promoted to an error.
3. Run:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

4. The final line must be exactly `All 141 checks passed.`

## Gate B — Breakpoints / Watchpoints Table Classification

1. Open Debugger against Mock or PS5, attach, and create one Software/Execute breakpoint plus one Hardware data watchpoint.
2. Confirm the table order is **Address | State | Type | Mechanism | Access | Size | Lifetime**.
3. Confirm the Software/Execute row reports **Type = Breakpoint**, **Mechanism = Software**, **Access = Execute**.
4. Confirm the Hardware/Write row reports **Type = Watchpoint**, **Mechanism = Hardware**, **Access = Write**.
5. Refresh/Enable/Disable/Remove remain unchanged.

## Gate C — Context Menu Prerequisite / Disabled State

For both Scan Results and Saved Addresses:

1. Close the Debugger, right-click a row, and confirm **Add Breakpoint** / **Add Watchpoint** are disabled.
2. Open Debugger but leave it detached; both shortcuts remain disabled.
3. Attach Debugger to the current Active Target. Reopen the row menu and confirm each shortcut is enabled only if the complete request is legal for that address/value width.
4. If the Saved Address belongs to another target identity or the connection generation changes, both shortcuts must remain disabled for that stale/mismatched row.

## Gate D — Add Watchpoint from Scan Results

1. With Debugger already attached to the same target, right-click a suitable aligned Scan Result whose value width is supported by the backend.
2. Confirm **Add Watchpoint** is enabled. For an ordinary writable data address, **Add Breakpoint** should remain disabled when the address is not executable.
3. Click **Add Watchpoint**.
4. Confirm Debugger receives a persistent Hardware/Write watchpoint at exactly the selected address and with exactly the Scan Result's current value width.
5. Confirm a duplicate shortcut becomes disabled while the equivalent watchpoint exists.

## Gate E — Saved Addresses Shortcuts

1. Repeat the legal Hardware/Write shortcut from a Saved Address and confirm its saved `ValueSize` is preserved.
2. Use a Saved Address that refers to executable code (or another backend fixture known to allow an execute breakpoint) and confirm **Add Breakpoint** creates a persistent Software/Execute record at that exact address.
3. Spot-check an invalid/misaligned/unsupported-width address and confirm the corresponding action is greyed out rather than producing a backend request.

## Gate F — Rev29 Logical Disassembly Regression

1. Keep or create a Software/Execute breakpoint on a known multi-byte instruction.
2. Open Disassembler while the breakpoint is active.
3. Confirm original logical bytes/instruction remain visible, with `Breakpoint` in Markers rather than `CC / int3`.
4. Trigger a hardware watchpoint independently and confirm `Watchpoint hit` still appears as metadata only.

The external ps5debug-NG same-instruction Software-breakpoint/Hardware-watchpoint overlap behavior is not a rev30 acceptance requirement; it is documented in `docs/bug-reports/ps5debug-ng-software-breakpoint-step-consumes-overlapping-watchpoint-hit.md`.

## Gate G — Detach / Target Lifetime Regression

1. Detach and confirm both main-workspace shortcuts become disabled immediately.
2. Reattach to the same current target and confirm legal actions become available again.
3. Change Active Target or reconnect and confirm an old Debugger window cannot authorize shortcuts for the new session generation.
4. Confirm no target crash, implicit attach, unexpected Resume/Pause, orphan breakpoint/watchpoint, or stale action remains.

## Completion Rule

Rev30 would have been accepted only after a clean Windows build, **141/141**, table classification PASS, Scan Results and Saved Addresses prerequisite/gating PASS, legal shortcut creation PASS, invalid shortcut disabling PASS, rev29 logical Disassembler regression PASS, and target-lifetime/detach regression PASS. Rev30 was superseded before acceptance. Its applicable gates are carried into rev31; Debugger Integration, Export and Finalization moves to rev32.
