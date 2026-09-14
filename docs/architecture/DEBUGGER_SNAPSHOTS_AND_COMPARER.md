# Debugger Snapshots and Call Stack Comparer

## Scope

Application `0.1.7.rev33` introduces the shared persistence and comparison layer used to move debugger evidence from a live paused stop into repeatable offline analysis. The complete context is represented by the Core-owned `DebuggerSnapshot`; platform plugins continue to provide the neutral debugger/disassembly/memory services already exposed through the Plugin SDK and do not own snapshot composition.

Rev33 is a verification candidate. Rev31 remains the latest verified PS5 cleanup baseline, and the rev32 trigger/current-IP semantics carried into rev33 must be verified before this architecture is accepted.

## Data flow

```text
Paused Debugger
      |
      v
DebuggerSnapshotCaptureService
      |
      v
Immutable DebuggerSnapshot
   /       |        \
  v        v         v
JSON    Pairwise    Group
Export  Compare     Compare
  |
  v
JSON Import -> offline DebuggerSnapshot -> same comparer
```

Flat debugger lists use Universal Export separately. A table export is useful for Threads, Registers, Breakpoints/Watchpoints, Call Stack, Events, and derived comparison rows, but it is not the persistence format for complete debugger context.

## Standard snapshot

A Standard Snapshot records:

- unique snapshot id and UTC capture time;
- editable Label, Notes, and user-defined Group metadata;
- application/plugin/API/platform/process/architecture metadata from capture time;
- historical connection generation, never reused as a live identity after import;
- event sequence and timestamp;
- execution state and stop reason;
- stopped thread id/name;
- real stop/current instruction pointer;
- trigger instruction and `BackendExact` / `DisassemblyDerived` / `Unresolved` provenance;
- watched address, access, width, and triggered breakpoint/watchpoint metadata when applicable;
- registers with exact raw bytes plus presentation metadata;
- neutral call frames with raw and module-relative locations when resolvable;
- bounded logical/original disassembly around stop and trigger;
- managed breakpoint/watchpoint state with Type, Mechanism, Access, Size, Lifetime, Enabled, and triggered-item state;
- a bounded stack-memory block around the semantic stack pointer when the existing safe memory path can provide it;
- per-section state: `Complete`, `Unavailable`, `Skipped`, or `Failed`.

The snapshot does not contain backend breakpoint instrumentation as original game code. Disassembly is read through the existing logical overlay pipeline, so a Software/Execute `INT3` remains debugger state rather than persisted instruction bytes.

## Capture safety

`DebuggerViewModel.CaptureSnapshotAsync` is the single live composition path used by the comparer. Capture is allowed only while the associated debugger is Paused on its current Active Target. The operation freezes the current event sequence, captures already-materialized register/call-stack/breakpoint state, performs bounded logical disassembly and stack-memory reads when available, then rechecks:

- debugger still Paused;
- same stop sequence;
- same plugin/process/connection generation/current target.

If execution resumes, another stop replaces the context, the target becomes stale, or cancellation is requested, no snapshot is published. Optional section failure is retained as section status instead of fabricating zero/default data.

Capture is read-only and does not implicitly pause a Running target.

## Immutability

Published snapshot collections are copied into read-only collections. Instruction marker collections are copied as well. Subsequent debugger refreshes, execution, breakpoint changes, or UI selection changes therefore do not mutate already captured evidence. Only Label, Notes, and Group may be changed by replacing the immutable snapshot record with a metadata-updated copy.

## JSON snapshot schema

Complete snapshots use:

```json
{
  "schema": "teekay87-memory-engine-debugger-snapshot",
  "schemaVersion": 1,
  "snapshot": { }
}
```

Rules for schema version 1:

- 64-bit/address values are canonical `0x...` strings rather than JSON numbers;
- public enum values are names rather than numeric ordinals;
- raw register/instruction/memory bytes are deterministic hexadecimal strings;
- UTC timestamps use the serializer's ISO-8601 representation;
- address width, raw-byte syntax/length, instruction markers, and memory-block range are validated on import;
- unknown additive fields are tolerated by the version-1 reader;
- a different schema identity or unsupported schema version is rejected;
- export writes a temporary file and publishes the destination only after serialization completes successfully.

Import creates data for offline inspection/comparison. It never connects to the source process, never treats historical connection generation as a current session, and never recreates breakpoint/watchpoint manager state.

## Call Stack Comparer

The user-facing window is named **Call Stack Comparer**, but internally it compares complete `DebuggerSnapshot` objects. It is modeless and can remain useful independently of target traffic once snapshots exist.

The snapshot collection displays Label, Group, Notes, capture time, source, event, trigger, stop IP, and process. Rev41 introduced always-visible controls patterned after Saved Addresses, rev42 made Group creation explicit, and rev43 completes the interaction/state rules: Label and Notes use in-row text editors, Group is a normal selection dropdown whose popup begins with an explicit **No group** action followed by the dedicated new-group text input and existing groups, read-only evidence remains read-only, and each snapshot row has a confirmed dedicated Remove action. From host rev39, opening or reactivating the comparer never captures automatically. One `CallStackComparerViewModel` workspace is retained for the lifetime of the same Debugger window/session, so closing/reopening the modeless presentation window does not discard snapshots or results. New live evidence is added only through **Capture Current**. Its enabled state follows the attached Debugger's `CanCaptureSnapshot` state, including Running -> Paused transitions when a new stop context arrives. JSON snapshots can be imported completely offline. Closing the Debugger detaches an already-open comparer from live capture authority while preserving its existing evidence for offline analysis.

### Rev43 action state and result validity

Comparer actions now advertise only valid operations. **Compare 2** requires exactly two selected snapshots, complete-snapshot **Export...** requires exactly one selected snapshot, **Compare Groups** requires two distinct selected groups that each currently contain at least one snapshot, **Remove All** requires at least one snapshot, and **Export Results...** requires a current comparison result. The defensive click-time validation remains in place as a fallback.

A retained comparer workspace may keep a still-valid result when its window is closed and reopened in the same Debugger session. Results are invalidated when their real inputs change: pairwise results clear if either compared snapshot is removed, while Label/Notes/Group metadata changes do not invalidate pairwise comparison; group results clear when an involved snapshot is removed, membership moves into/out of either compared group, a row is explicitly ungrouped from an involved group, or Group A/Group B selection changes away from the pair that produced the result. **Remove All** clears snapshots and results while preserving the session-local group-name catalog.

### Pairwise comparison

Exactly two snapshots can be compared directly. The result includes call-stack path/frame evidence, exact raw register values, trigger/current instruction context, captured logical disassembly, breakpoint/watchpoint context, and bounded stack-memory context when available.

### Group comparison

Groups are arbitrary user-provided names; Core has no Player/Enemy/Boss semantics. Group membership is assigned per snapshot row. The row Group dropdown lists every group already registered in the current comparer session. Rev43 places **No group** at the top so a snapshot can be explicitly unassigned without deleting the session group name. A dedicated new-group text input follows it; pressing Enter registers/reuses the name and assigns it to that snapshot. Names are trimmed and reused case-insensitively. Imported snapshot Group metadata is registered into the same session list. Group A and Group B are non-editable selectors bound to that registered list and cannot create names themselves. Assigning the same group to multiple snapshots makes those snapshots members of one input set for the existing `CompareGroups` engine; reassignment updates only that snapshot's immutable metadata copy. Group names are session-local and are not application settings. For each value the comparer determines whether it is:

- identical;
- different/stable in both groups;
- stable only in Group A;
- stable only in Group B;
- variable in both groups;
- unavailable.

A call-stack frame is reported as the first group divergence only if every available snapshot inside Group A agrees at that position, every snapshot inside Group B agrees, and the two stable locations differ. Pairwise ordered frame alignment also uses a longest-common-subsequence measurement so an extra/missing wrapper frame can be described without pretending the stacks are index-identical.

### Address normalization

Code locations prefer `Module + Offset` whenever captured module information is available. Raw addresses remain part of the original snapshot but are not the preferred cross-launch identity because ASLR/process restarts can move modules.

### Candidate summary

Only `StableInBothGroupsDifferentBetweenGroups` evidence is promoted to a **Potential discriminator** summary. This is intentionally an evidence label, not a semantic claim. The program does not automatically call a register value a player flag, team id, entity pointer, or similar concept.

Priority is generally:

1. stable call-stack path/divergence;
2. stable register differences;
3. bounded memory differences;
4. logical instruction differences;
5. trigger/current context;
6. breakpoint/watchpoint context.

Variable evidence remains available in detail rows but is not promoted.

## Universal debugger table export

The Debugger's **Export...** action exposes independently selectable scopes for:

- Threads;
- Registers;
- Breakpoints / Watchpoints;
- Call Stack;
- Events.

These use the existing shared JSON/CSV/TSV/Markdown pipeline. Registers expose raw bytes and formatted values. Breakpoint/watchpoint rows expose Type and Mechanism separately. Events expose real Instruction Pointer and Trigger Instruction separately together with watched-address/access/size and trigger-resolution metadata.

The comparer can likewise Universal Export the current derived result table. Exporting a derived comparison never modifies its source snapshots.

## Disassembler debugger action

Rev33 introduced a Disassembler debugger convenience action. Rev35 refines it into separate **Add Breakpoint** and **Add Watchpoint** entries. Both still route into the existing debugger manager; Add Watchpoint additionally requires safe platform-owned memory-operand resolution from the current paused register context.

The command is enabled only when:

- exactly one Disassembler row is selected;
- an already-open Debugger is attached to the same plugin/process/connection generation;
- the current attached debugger supports the relevant capability/path and request validation remains available.

With multiple selected rows the menu item remains visible but disabled. Selecting it opens the existing breakpoint/watchpoint dialog and uses the existing neutral validation/add path. It never implicitly attaches a debugger.

## Deferred work

The handover's Deep Snapshot concept remains optional. Rev33 deliberately does not recursively dereference arbitrary registers or build an independent structure-analysis subsystem. Future selected-register/pointer memory capture can extend the same bounded snapshot model after the Standard Snapshot pipeline is verified.

Future Cheat Maker assembly/patching must also remain a separate intentional-write subsystem. Snapshot capture, import, and comparison stay read-only.
