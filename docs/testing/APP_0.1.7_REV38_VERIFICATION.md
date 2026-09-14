# Application 0.1.7.rev38 Verification — Disassembler Arbitrary-Row Watchpoint Resolution

Rev38 follows the fully green rev37 Windows automated gate (**152/152 PASS**) and the successful live verification of separate Watchpoint Hit/Trigger and Stop/Current-IP presentation. During Gate 3, the Disassembler **Add Watchpoint** action was found to be too restrictive because it only enabled on the current stop or resolved trigger row. Rev38 removes that row-identity restriction while preserving paused-session, resolver, and backend validation safety.

## Package identity

| Item | Expected |
| --- | --- |
| Application | `0.1.7.rev38` |
| Feature | `Disassembler Arbitrary-Row Watchpoint Resolution` |
| Plugin API | `2.18.0` |
| Mock | `1.0.1.rev17` |
| PS5 | `0.1.2.rev39` |
| Automated registry | `152` checks |
| Snapshot schema | `teekay87-memory-engine-debugger-snapshot`, version `1` |

## Gate 1 — Clean Windows build and automated suite

1. Extract rev38 into a clean folder.
2. Build the complete solution on Windows.
3. Treat any compile warning/error as a blocker.
4. Run the complete verification project.
5. Expected result: **152/152 PASS**.

Do not continue after a failed check.

## Gate 2 — Carried watchpoint trigger/stop regression

This was already proven in rev37 runtime testing, but perform a short smoke check after the rev38 host change:

- resolved Watchpoint Hit/Trigger instruction remains green;
- real Stop/Current IP remains yellow when different;
- Debugger Events retain separate trigger and stop addresses;
- Registers remain bound to the real stop context;
- Software/Execute breakpoint presentation remains unchanged.

## Gate 3 — Disassembler Add Breakpoint / Add Watchpoint

### A. Single-selection menu and Add Breakpoint

1. Keep Debugger and Disassembler open for the same target/session.
2. Select exactly one valid instruction.
3. Confirm the context menu contains separate **Add Breakpoint** and **Add Watchpoint** entries.
4. Confirm **Add Breakpoint** is enabled when the existing validator accepts the Software/Execute request.
5. Add and remove one breakpoint and confirm logical/original bytes remain visible.

### B. Arbitrary-row Add Watchpoint — positive PS5 cases

Pause the debugger and keep the current register snapshot visible. Select memory-access instructions away from the current Hit/Stop pair.

Recommended examples from the already observed live region include instructions such as:

```asm
sub [rax+9Ch],ecx
mov dword [rbx+64h],0
mov rax,[rbx+48h]
mov ecx,[rbx+64h]
cmp byte [rel ...],0
```

For each suitable case:

1. Select exactly one row.
2. Confirm **Add Watchpoint** is enabled when the current paused register snapshot plus instruction metadata can resolve one safe effective address.
3. Invoke it.
4. Confirm the manager receives the derived Hardware request with the expected effective address, access mode, and width.
5. Remove the watchpoint before trying the next case if hardware slots would otherwise be consumed.

Interpretation requirement: for base/index addressing, the derived address is the address implied by the **current paused register values**. It is not a claim that the unrelated instruction previously executed with those exact register values. RIP-relative operands may resolve directly from instruction metadata.

### C. Negative/safety cases

Confirm **Add Watchpoint** remains disabled for representative cases:

- non-memory instruction such as `test`, `jmp`, `call`, `nop`, `push`/`pop` where no eligible explicit data operand is exposed by the resolver;
- `lea`;
- multiple selected rows;
- missing/unavailable required register context;
- unsupported/ambiguous memory operand or width;
- request rejected by existing PS5 alignment/mapping/slot validation;
- debugger Running;
- debugger detached/closed or attached to a different target/session.

No action may implicitly attach a debugger or guess an address.

## Gate 4 — Standard Snapshot capture

1. Running -> **Capture Current** disabled.
2. Pause -> Capture Current enabled.
3. Capture and inspect source/event/register/call-stack/logical-disassembly/breakpoint-context/stack-memory data and section status.
4. Confirm trigger and stop/current IP remain distinct in a watchpoint snapshot.
5. Resume to a new stop and confirm the old snapshot is immutable.
6. Confirm stale connection/target/session state cannot capture against a new target accidentally.

## Gate 5 — Snapshot JSON export/import

1. Export a captured snapshot.
2. Confirm schema `teekay87-memory-engine-debugger-snapshot`, version `1`.
3. Confirm 64-bit addresses remain exact hex strings and raw bytes survive exactly.
4. Import the same file and confirm offline/imported status.
5. Compare original vs imported and expect equivalent captured data.
6. Detach and confirm imported data remains usable offline.
7. Confirm import never reconnects, attaches, recreates breakpoint/watchpoint state, or writes target memory.
8. Confirm invalid schema/version is rejected clearly.

## Gate 6 — Universal Export for debugger tables

Verify independent export scopes for Threads, Registers, Breakpoints/Watchpoints, Call Stack, and Events. Exercise JSON plus at least one tabular format and confirm event export keeps trigger and stop/current addresses separate.

## Gate 7 — Call Stack Comparer pairwise behavior

Capture/import two snapshots and verify modeless lifecycle, pairwise selection, Summary, Call Stack, Registers, Context, Instructions, Breakpoint Context, Memory, raw register equality, module-relative identity, ordered frame alignment, logical/original instruction bytes, and Export Results.

## Gate 8 — Group comparison

Create two user-defined groups with multiple samples and verify stable common frames, stable first divergence, stable cross-group register differences, variable-value handling, evidence-based Potential discriminator wording, and imported/live snapshot equivalence.

## Gate 9 — Modeless lifecycle and offline ownership

Detach/close Debugger with comparer snapshots present, continue offline comparison, verify Capture Current loses live authority, reconnect another target without rebinding old snapshots, exercise activation/close/reopen, and close the application with Debugger/Disassembler/Comparer open.

## Gate 10 — Permanent rev31 teardown safety regression

Mandatory live PS5 regression:

- application exit with an active hardware watchpoint, then trigger the watched access: game continues;
- explicit detach with an active hardware watchpoint, then trigger the watched access: game continues;
- active/staged Software/Execute breakpoint cleanup restores original bytes and leaves no stale trap.

## Gate 11 — Full debugger regression

Run the established Mock and live-PS5 cycle covering attach/detach/reattach, Pause/Continue, Threads, Registers, breakpoint/watchpoint manager actions, multiple-watchpoint conservative attribution, Call Stack, Current Instruction -> Disassembler, logical breakpoint-byte overlay, Step Into, breakpoint-aware Step Over, Step Out, Run to Address, temporary-breakpoint cleanup, disconnect/reconnect stale-session behavior, Light/Dimmed/Dark presentation, and smoke regression for Scan Results, Saved Addresses, Memory Viewer, and Disassembler.

## Acceptance

Rev38 can continue into the remaining debugger-finalization gates only when the clean automated suite is green and Gate 3 proves that arbitrary safely resolvable memory-access rows enable Add Watchpoint without weakening the existing safety gates.
