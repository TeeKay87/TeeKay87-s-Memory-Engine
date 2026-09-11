# TeeKay87's Memory Engine — Debugger Finalization Plan and Handover

## Watchpoint Trigger Resolution, Debugger Snapshots, Export/Import, and Call Stack Comparer

**Document purpose:** implementation plan and development handover for the remaining major debugger work before the `0.1.7` Debugger feature block is considered complete.

**Starting point:** the latest verified debugger baseline is `0.1.7.rev31`, including the verified PS5 hardware-watchpoint detach cleanup and the complete debugger functionality carried forward from earlier revisions.

**Important:** this document intentionally does not assign fixed revision numbers to the individual implementation stages. Each code change must continue to follow the project's normal revision rules, and intermediate fixes may shift the exact revision in which a stage is completed.

---

# 1. Purpose and Final Direction

The remaining debugger work should be completed in this order:

1. **Correct watchpoint-hit presentation and debugger event semantics.**
2. **Introduce a complete debugger snapshot model and build debugger export/import around that model.**
3. **Build the Call Stack Comparer as a separate modeless window using debugger snapshots as its source data.**
4. **Complete final debugger integration, cleanup, regression testing, and documentation.**

The central design decision is that debugger export must not be treated as a collection of unrelated UI-table exports.

The primary structured workflow should be:

```text
Live Debugger
        |
        v
Debugger Snapshot
   /             \
  v               v
JSON Export    Call Stack Comparer
  |
  v
JSON Import
  |
  v
Call Stack Comparer
```

The existing Universal Export system should still be used for flat debugger tables such as Threads, Registers, Breakpoints/Watchpoints, Call Stack rows, and Events. However, the **complete debugger context** must be represented by a dedicated hierarchical `DebuggerSnapshot` model and a lossless JSON representation.

This distinction is important:

- **Universal Export** remains the standard solution for list/table data.
- **Debugger Snapshot JSON** becomes the source-of-truth format for complete debugger context, persistence, import, and comparison.

The Call Stack Comparer should use the same `DebuggerSnapshot` model regardless of whether a snapshot came directly from a live debugger session or was imported from a JSON file.

---

# 2. Goals

The completed debugger should support all of the following:

- A software breakpoint is presented on the exact instruction it applies to.
- A hardware watchpoint identifies and marks the instruction that caused the watched memory access whenever that instruction can be resolved safely.
- The true stop/current instruction pointer remains available separately from the watchpoint trigger instruction.
- Debugger event data distinguishes:
  - current/stop instruction;
  - trigger instruction;
  - watched memory address;
  - watchpoint access mode;
  - watchpoint size;
  - trigger-resolution status.
- A paused debugger context can be captured into a complete neutral snapshot.
- A snapshot contains enough information to reproduce the useful debugging context offline.
- A complete snapshot can be exported to JSON without losing information.
- A JSON snapshot can be imported later without requiring a live target.
- Imported and live snapshots use the same comparison pipeline.
- The existing debugger lists can still be exported individually through Universal Export.
- The Call Stack Comparer opens in its own modeless window from the Call Stack panel.
- The comparer can compare two snapshots directly.
- The comparer can also compare groups of snapshots, for example:
  - Player;
  - Enemy;
  - Friendly NPC;
  - Boss;
  - Vehicle;
  - any other user-defined group.
- The comparer can identify:
  - common call-stack frames;
  - the first meaningful call-stack divergence;
  - register values that remain stable inside one group but differ from another;
  - current and trigger instruction differences;
  - logical disassembly differences;
  - breakpoint/watchpoint context differences;
  - stable candidate discriminators useful for cheat development.
- Comparison must remain explainable. The program must show why something was considered common, different, stable, or potentially useful.
- No snapshot capture or comparison feature may alter target memory.
- No imported snapshot may ever be mistaken for live target state.

---

# 3. Non-Goals

The first complete implementation does not need to become a general-purpose symbolic execution or decompiler system.

The following are outside the initial scope unless they become necessary during implementation:

- automatic reconstruction of complete C/C++ class definitions;
- arbitrary deep pointer crawling from every register;
- automatic semantic naming such as "player object" or "enemy object";
- automatic generation of final cheats without user review;
- full x64 unwind metadata support beyond the existing call-stack backend;
- instruction-level execution tracing;
- full Structure Dissect integration;
- unlimited memory dumps inside every debugger snapshot;
- automatic external symbol-server integration;
- cross-architecture instruction equivalence analysis.

The comparer may highlight useful evidence, but it must not claim that a value is a player flag, team identifier, entity pointer, or similar concept unless the user confirms that interpretation.

---

# 4. Architectural Principles

## 4.1 Keep platform-specific behavior in plugins

The existing project boundary remains mandatory:

- Core owns neutral debugger/snapshot/comparison concepts.
- Plugin SDK owns neutral contracts that plugins need to implement or populate.
- PS5-specific protocol behavior remains in the PS5 plugin.
- Mock-specific deterministic behavior remains in the Mock plugin.
- No ps5debug-NG packet layouts, DR-register details, x86-specific register names, or PS5-specific assumptions may leak into generic Core or WPF logic.

## 4.2 Preserve the existing debugger session safety model

Every live capture must remain bound to:

- plugin id;
- process id;
- connection generation;
- debugger attachment identity/state.

A stale debugger window must never capture a snapshot from a newly connected process by accident.

## 4.3 Logical disassembly remains authoritative

Debugger snapshots must store the **logical/original code view**, not backend instrumentation.

For example, if a software breakpoint physically changed:

```text
01 70 40
```

into:

```text
CC 70 40
```

the snapshot must preserve:

```asm
add [rax+40h],esi
```

with the original bytes:

```text
01 70 40
```

The physical `INT3` is debugger implementation state and must never become the saved representation of the game's original instruction.

## 4.4 Comparison must use underlying data

The comparer must operate on structured data, not UI-rendered strings.

Display text may be stored when useful for offline presentation, but comparison logic should prefer:

- raw register bytes;
- semantic register roles;
- instruction addresses;
- module-relative addresses;
- logical instruction bytes;
- structured breakpoint/watchpoint metadata;
- structured frame addresses;
- structured event information.

---

# 5. Stage One — Watchpoint Trigger Resolution and Event Model

## 5.1 Current behavior

For a software execute breakpoint, the debugger can present the breakpoint on the instruction that is about to execute.

For a hardware data watchpoint, x86-64 normally reports the debug trap after the memory-accessing instruction has executed.

The verified example was:

```asm
0x8033D981   add [rax+40h],esi
0x8033D984   mov rcx,[rdi+30h]
```

The watched memory was modified by:

```asm
0x8033D981   add [rax+40h],esi
```

but the stop context reported:

```text
RIP = 0x8033D984
```

The current debugger presentation therefore places `Watchpoint hit` on `0x8033D984`, even though that instruction did not cause the watched memory access.

That is technically a correct stop position, but it is not the most useful presentation for reverse engineering.

## 5.2 Required behavior

The debugger must distinguish the following concepts explicitly:

```text
InstructionPointer
TriggerInstructionAddress
WatchedAddress
WatchpointAccess
WatchpointSize
TriggerResolution
```

Recommended neutral meanings:

### `InstructionPointer`

The real debugger stop/current instruction pointer from the event/register context.

Example:

```text
0x8033D984
```

This remains authoritative for:

- register state;
- stepping;
- execution control;
- current debugger context;
- navigation that explicitly means "where execution is stopped."

### `TriggerInstructionAddress`

The instruction that caused the watched memory access.

Example:

```text
0x8033D981
```

This is what the Disassembler should use for the `Watchpoint hit` marker when resolved.

### `WatchedAddress`

The memory address being watched.

Example:

```text
0x239629040
```

### `WatchpointAccess`

Neutral access type:

```text
Read
Write
ReadWrite
Execute
```

Only values supported by the relevant watchpoint implementation are populated.

### `WatchpointSize`

The watched width in bytes.

Example:

```text
4
```

### `TriggerResolution`

Do not reduce this to a simple guessed address.

Recommended statuses:

```text
BackendExact
DisassemblyDerived
Unresolved
```

Possible future statuses may be added if needed, but the first public schema should remain small and explicit.

`BackendExact` means the backend itself supplied an authoritative triggering instruction.

`DisassemblyDerived` means TeeKay87's Memory Engine derived the triggering instruction from a trusted logical instruction stream and the current stop address.

`Unresolved` means the application cannot safely identify the trigger instruction.

## 5.3 Do not use `RIP - 1`

x86-64 instructions are variable length.

The previous instruction may begin 1 to 15 bytes before the current RIP, and subtracting a constant amount is incorrect.

The resolver must use the existing disassembly infrastructure.

## 5.4 Trigger-resolution strategy

The preferred resolution order is:

1. If the plugin/backend can provide an authoritative triggering instruction address, use it and mark the result `BackendExact`.
2. Otherwise use the existing logical disassembly pipeline to inspect code immediately before the current stop RIP.
3. Resolve the previous instruction only when the logical instruction stream contains one instruction whose end address is exactly the stop RIP.
4. If the instruction boundary cannot be established safely, leave the trigger unresolved.

The generic relationship is:

```text
TriggerInstruction.Address + TriggerInstruction.Length == InstructionPointer
```

The resolver must use logical/original disassembly so active software breakpoints do not corrupt instruction boundaries.

## 5.5 Where resolution should live

The event model belongs in the Plugin SDK because it is part of neutral debugger context.

The host/Core may provide a neutral trigger-resolution service that consumes:

- the neutral debugger event;
- target identity;
- logical `DisassemblyReader`;
- the plugin's `IDisassemblerProvider`.

The resolver must not contain x86-64 decoding logic itself. Architecture-specific decoding remains behind the plugin's disassembler provider.

If a plugin can provide the trigger directly, the Core resolver must not replace the backend result with a derived one.

## 5.6 Marker behavior

### Software breakpoint

Continue showing:

```text
Breakpoint
```

on the breakpoint instruction.

### Resolved watchpoint

Show:

```text
Watchpoint hit
```

on `TriggerInstructionAddress`.

Do not show `Watchpoint hit` on the stop/current instruction.

Example:

```text
Address       Markers          Instruction
0x8033D981    Watchpoint hit   add [rax+40h],esi
0x8033D984                     mov rcx,[rdi+30h]
```

### Unresolved watchpoint

Do not pretend the current RIP caused the watchpoint.

If trigger resolution fails, use wording that describes the actual information available, for example:

```text
Watchpoint stop (trigger unresolved)
```

on the current stop instruction.

This makes the failure visible and avoids presenting incorrect reverse-engineering information.

## 5.7 Preserve both addresses

Moving the visible marker must never discard the real stop RIP.

The debugger event, snapshot, event log, and comparer must retain both values independently.

Example snapshot context:

```text
Watched address:
0x239629040

Trigger instruction:
0x8033D981
add [rax+40h],esi

Stop/current instruction:
0x8033D984
mov rcx,[rdi+30h]
```

## 5.8 Event history

The Debugger Events table should be evaluated after the event-model change.

At minimum, structured export must expose both addresses.

The UI may continue showing the normal Instruction Pointer column, but consider adding one of the following if it remains readable:

```text
Trigger
```

or:

```text
Trigger Instruction
```

Do not overload the existing Instruction Pointer field.

## 5.9 Breakpoint/watchpoint overlap limitation

The already identified external ps5debug-NG behavior must remain documented:

- if a software breakpoint is placed on the same instruction that would trigger a hardware watchpoint;
- the backend's internal single-step used to execute/rearm the software breakpoint can consume the watchpoint event;
- the host may never receive a separate watchpoint event.

The new trigger resolver must not attempt to fabricate an event that the backend never delivered.

This limitation belongs in the existing external bug documentation and should remain a regression note during live PS5 verification.

---

# 6. Stage Two — Debugger Snapshot Model

The snapshot model is the foundation for export/import and the comparer.

It should be implemented before the JSON file format is finalized.

## 6.1 Snapshot ownership

The complete snapshot model should live in Core.

It is an application-level composition of already neutral debugger/disassembly data.

Plugins should not need to construct the complete snapshot themselves.

Plugins continue supplying:

- debugger events;
- registers;
- call frames;
- breakpoint/watchpoint state;
- architecture/provider services;
- memory/disassembly services.

Core/host composes that data into one immutable snapshot.

## 6.2 Snapshot identity

Each snapshot should have a unique identifier.

Recommended fields:

```text
SnapshotId
CapturedAtUtc
Label
Notes
Group
CaptureMode
```

`SnapshotId` should be generated locally and remain stable through export/import.

`Label` is user-editable, for example:

```text
Player hit by Enemy A
Enemy A hit by Player
Boss damage
Player poison tick
```

`Group` is optional and used by grouped comparisons:

```text
Player
Enemy
Boss
Friendly
```

The user must be able to rename groups.

## 6.3 Snapshot source metadata

The snapshot should record enough source metadata to understand where it came from without implying that an imported snapshot is still live.

Recommended metadata:

```text
ApplicationVersion
ApplicationRevision
PluginId
PluginName
PluginVersion
PluginRevision
PluginApiVersion
PlatformDisplayName
ProcessId
ProcessName
TargetArchitecture
AddressWidth
PointerWidth
Endianness
ConnectionGenerationAtCapture
CaptureSource
```

`ConnectionGenerationAtCapture` is historical metadata only.

On import, it must never be treated as a current/live session identity.

`CaptureSource` should distinguish at least:

```text
Live
Imported
```

The imported in-memory object may retain original capture metadata while also being marked as imported/offline in the comparer.

## 6.4 Section status

A snapshot should remain valid even if one optional section is unavailable.

Each major section should record status such as:

```text
Complete
Unavailable
Skipped
Failed
```

and optionally an error message.

Example:

```text
Registers: Complete
CallStack: Complete
Disassembly: Complete
StackMemory: Skipped
```

This avoids throwing away a useful snapshot because one optional data source failed.

## 6.5 Event context

A snapshot captured from a debugger stop should include:

```text
EventSequence
EventTimestamp
EventKind
ExecutionState
StopReason
ThreadId
ThreadName
InstructionPointer
TriggerInstructionAddress
TriggerResolution
WatchedAddress
WatchpointAccess
WatchpointSize
TriggeredBreakpointId
TriggeredBreakpointType
TriggeredBreakpointMechanism
TriggeredBreakpointLifetime
Message
```

Fields that do not apply remain null/absent.

## 6.6 Registers

Every captured register should preserve structured information.

Recommended data:

```text
Name
Group
Role
BitWidth
ValueEncoding
RawBytes
FormattedValue
IsWritable
```

`RawBytes` is the comparison source of truth.

`FormattedValue` is stored for offline readability and compatibility with the exact presentation used at capture time.

Comparison logic must not rely only on formatted text.

## 6.7 Call Stack

Capture all frames returned by the current backend, up to the backend's existing limit.

For PS5 the current configured maximum is 64 frames, but the actual result can be shorter because the current stack walk depends on the available frame-pointer chain.

Each frame should contain all neutral information currently available:

```text
Index
InstructionAddress
ReturnAddress
StackPointer
FramePointer
Module
ModuleBase
ModuleOffset
Symbol
```

If a value is unavailable, preserve that fact rather than synthesizing it.

## 6.8 Logical disassembly

A snapshot should contain bounded logical disassembly around:

- the current stop instruction;
- the trigger instruction when it differs and falls outside the current bounded window;
- optionally relevant call-frame addresses when requested by a deeper capture mode.

Recommended instruction fields:

```text
Address
Module
ModuleOffset
Bytes
InstructionText
FlowControl
DirectTarget
Markers
SyntaxTokens
```

Not every field needs to be mandatory in schema version 1, but addresses, bytes, instruction text, and markers should be retained.

The bytes must be logical/original bytes after debugger overlays.

## 6.9 Breakpoint/watchpoint state

Capture the manager state as it existed at snapshot time.

Recommended fields:

```text
Id
Address
Enabled
Type
Mechanism
Access
Size
Lifetime
IsTriggeredItem
CleanupState
```

Do not expose platform-native slot numbers in the neutral snapshot unless there is a strong diagnostic reason.

## 6.10 Memory context

A complete comparison system benefits from memory context, but memory capture must remain bounded.

Use two capture levels.

### Standard Snapshot

The default snapshot should remain fast.

Recommended initial contents:

- event context;
- registers;
- call stack;
- logical disassembly around stop/trigger;
- breakpoint/watchpoint state;
- small bounded stack-memory window around the current stack pointer if safe and inexpensive.

### Deep Snapshot

An explicit user action may capture additional context:

- larger stack-memory window;
- bounded memory around selected pointer-like registers;
- selected user-specified memory addresses/ranges;
- optional additional disassembly around selected call frames.

Do not automatically dereference every register recursively.

Deep capture must have hard limits and visible progress/cancellation if it requires multiple reads.

## 6.11 Snapshot immutability

Once captured, a snapshot should be immutable except for user metadata:

- Label;
- Notes;
- Group.

Debugger execution continuing afterward must not mutate existing snapshots.

This is essential for trustworthy comparisons.

---

# 7. Stage Three — Snapshot Capture Service

## 7.1 One capture pipeline

Create one shared capture service.

Conceptually:

```text
DebuggerSnapshotCaptureService
```

It should receive the current debugger coordinator/session identity and capture the sections in a deterministic order.

Do not duplicate snapshot capture logic in:

- export;
- comparer;
- debugger UI;
- Call Stack UI.

All consumers must use the same capture service.

## 7.2 Suggested capture order

A safe order is:

1. Validate current target/session identity.
2. Require an attached paused debugger context.
3. Freeze the event reference used for this capture.
4. Capture event context.
5. Capture selected-thread registers.
6. Capture call stack.
7. Resolve watchpoint trigger if needed.
8. Capture logical disassembly around trigger/current instruction.
9. Capture current breakpoint/watchpoint manager state.
10. Capture bounded standard memory context.
11. Revalidate that the session/stop context did not become stale during capture.
12. Publish the immutable snapshot only after capture completes.

If execution resumes during capture, the capture should fail or be clearly marked incomplete rather than combining data from two different stops.

## 7.3 No implicit pause

The default `Capture Snapshot` operation should require the debugger to already be Paused.

Do not silently pause a running game simply because the user opened the comparer or clicked export.

A future explicit "Pause and Capture" command could be considered separately.

## 7.4 Cancellation

Snapshot capture must support cancellation.

Cancellation must not publish a partially assembled snapshot as if it were complete.

If partial snapshots are ever supported intentionally, they must be explicitly labeled as partial and section statuses must reflect that.

---

# 8. Stage Four — Debugger Export and Import

## 8.1 Two export systems with different responsibilities

### Existing Universal Export

Continue using the existing shared tabular export pipeline for:

- Threads;
- Registers;
- Breakpoints/Watchpoints;
- Call Stack/Frames;
- Debug Events.

Supported existing formats remain:

```text
JSON
CSV
TSV
Markdown Table
```

The exports must use underlying structured data and not only UI text.

### Complete Debugger Snapshot Export

Use a dedicated hierarchical JSON serializer for `DebuggerSnapshot`.

Do not force a hierarchical snapshot into the tabular export abstraction if doing so damages the data model.

The implementation should reuse existing transactional/cancellation/file-publication helpers where appropriate, but the serialization model should remain purpose-built.

## 8.2 JSON is the lossless snapshot format

Complete snapshots should initially support one canonical format:

```text
JSON
```

CSV, TSV, and Markdown cannot represent the complete nested snapshot without flattening or losing structure.

They remain appropriate for individual tables only.

## 8.3 Snapshot schema identity

Every JSON file should identify itself clearly.

Recommended top-level fields:

```json
{
  "schema": "teekay87-memory-engine-debugger-snapshot",
  "schemaVersion": 1
}
```

The exact schema string can be finalized during implementation, but it must be stable and explicit.

## 8.4 Address representation

Do not serialize 64-bit addresses as ordinary JSON numbers if interoperability with JavaScript or other tools could lose precision.

Use canonical hexadecimal strings:

```json
"0x8033D981"
```

This applies to:

- instruction addresses;
- memory addresses;
- module bases;
- return addresses;
- stack/frame pointers;
- direct branch targets.

## 8.5 Raw binary values

Registers and memory bytes should retain exact raw content.

Recommended representation for small values:

```text
hexadecimal string
```

Example:

```json
"rawBytes": "0000803F"
```

For larger future memory blocks, Base64 may be preferable.

Whichever representation is chosen must be documented and deterministic.

## 8.6 Enum representation

Serialize public enums by stable names rather than numeric ordinals.

Example:

```json
"watchpointAccess": "Write"
```

This is easier to inspect manually and safer across implementation changes.

## 8.7 Timestamps

Use UTC ISO-8601 timestamps.

Example:

```text
2026-09-11T21:15:42.184Z
```

## 8.8 Snapshot import

Import must:

1. Parse JSON.
2. Validate schema identity.
3. Validate schema version.
4. Validate required fields.
5. Validate address syntax and widths.
6. Validate raw byte encodings.
7. Construct an immutable offline `DebuggerSnapshot`.
8. Never attempt to connect to the original target automatically.
9. Never apply any imported breakpoint/watchpoint state to a live debugger.
10. Never treat imported connection generation as a current target identity.

Imported snapshots are data only.

## 8.9 Compatibility rules

Schema version 1 should be designed to tolerate additive future fields.

Unknown additive fields should not make older readers fail unless they are required to interpret the snapshot safely.

A future incompatible structural change must increment `schemaVersion`.

Application version/revision and snapshot schema version are separate concepts.

## 8.10 Round-trip requirement

A core acceptance test must prove:

```text
Live/constructed DebuggerSnapshot
        ->
Serialize JSON
        ->
Deserialize JSON
        ->
Equivalent DebuggerSnapshot
```

The round trip must preserve all defined schema data.

## 8.11 Transactional publication

A failed or cancelled export must not leave a destination file that appears valid.

Use the same publication philosophy already established by Universal Export:

- write temporary output;
- flush/complete;
- publish destination atomically where possible;
- delete temporary data on failure/cancellation.

---

# 9. Stage Five — Debugger UI Integration for Snapshots

## 9.1 Avoid overloading the main Debugger window

The main Debugger window already contains:

- Threads;
- Registers;
- Breakpoints/Watchpoints;
- Call Stack;
- Events;
- execution controls.

The comparison system should not be embedded into that existing layout.

## 9.2 Call Stack panel entry point

The Call Stack panel should receive a clear action button such as:

```text
Compare...
```

or:

```text
Open Comparer...
```

Preferred behavior:

- available while a debugger session is attached;
- the comparer window can also remain open after detach because imported/captured snapshots are offline data;
- if the debugger is Paused, the user can capture the current context directly into the comparer;
- if the debugger is Running, live capture actions are disabled, but the comparer can still open to inspect existing/imported snapshots.

A separate button may be used for:

```text
Capture Snapshot
```

if that produces a cleaner workflow.

The final button layout should avoid crowding the Call Stack pane.

## 9.3 Recommended one-click workflow

A useful workflow is:

```text
Paused at Player hit
    ->
Call Stack > Compare...
    ->
Capture current snapshot and open/reuse comparer
```

Then later:

```text
Paused at Enemy hit
    ->
Call Stack > Compare...
    ->
Capture second snapshot into same comparer
```

The user should not need to export to disk before comparing live snapshots.

## 9.4 Export Snapshot

Provide a visible way to export the current captured snapshot as JSON.

This can live in:

- the comparer;
- the Call Stack panel;
- or a shared debugger export menu.

Avoid implementing separate capture logic in each place.

Every export route must call the same capture/serializer services.

---

# 10. Stage Six — Call Stack Comparer Window

## 10.1 Window role

The user-facing window should be called **Call Stack Comparer** because that is the workflow entry point and primary reverse-engineering purpose.

Internally, it should compare complete `DebuggerSnapshot` objects.

This gives the user a familiar name without limiting the architecture to call-stack-only data.

## 10.2 Window lifecycle

The Call Stack Comparer should be:

- modeless;
- independently activatable like Memory Viewer, Disassembler, and Debugger;
- tracked by MainWindow for orderly application shutdown;
- able to remain useful with no live debugger attached;
- able to compare imported files entirely offline.

Do not make it owned in a way that forces it to remain behind or above MainWindow.

Follow the already verified modeless-window activation pattern.

## 10.3 Snapshot collection pane

The comparer needs a snapshot collection.

Recommended columns:

```text
Label
Group
Captured
Source
Event
Trigger
Stop IP
Process
```

Each row represents one immutable snapshot.

Recommended commands:

```text
Capture Current
Import...
Export...
Rename
Set Group
Duplicate Metadata
Remove
Remove All
```

`Duplicate Metadata` is optional but may help when repeatedly collecting samples for one group.

## 10.4 Group assignment

Groups are essential.

Example:

```text
Player
  Player hit 1
  Player hit 2
  Player hit 3

Enemy
  Enemy hit 1
  Enemy hit 2
  Enemy hit 3
```

The comparer must support user-defined group names.

Do not hardcode Player/Enemy semantics into Core.

## 10.5 Comparison modes

Support at least two modes.

### Pairwise

Compare:

```text
Snapshot A
vs
Snapshot B
```

Useful for immediate inspection.

### Group comparison

Compare:

```text
Group A
vs
Group B
```

This is the important mode for discovering stable differences.

The engine should ask:

```text
What is stable inside Group A?
What is stable inside Group B?
What differs consistently between A and B?
```

## 10.6 Comparison result categories

Use clear, explainable categories:

```text
Identical
Different
Stable in both groups, different between groups
Stable only in Group A
Stable only in Group B
Variable in both groups
Unavailable
```

Avoid unexplained machine-generated confidence scores in the first implementation.

A future confidence system can be added if it is based on transparent evidence.

---

# 11. Call Stack Comparison

## 11.1 Primary purpose

The first call-stack question is:

> Where do the Player and Enemy execution paths first diverge before reaching the same shared instruction?

Example:

```text
PLAYER
#0 shared_health_write
#1 apply_damage
#2 player_damage_handler

ENEMY
#0 shared_health_write
#1 apply_damage
#2 enemy_damage_handler
```

The comparer should identify:

```text
Common frames:
#0
#1

First divergence:
Player: #2 player_damage_handler
Enemy:  #2 enemy_damage_handler
```

## 11.2 Address normalization

Raw addresses alone are insufficient across process restarts or ASLR changes.

Where module information exists, compare code locations primarily as:

```text
Module + Offset
```

Example:

```text
eboot.bin+0x123456
```

Raw address remains available for the original capture.

## 11.3 Frame alignment

Do not assume two stacks always have exactly the same number of frames.

Initial implementation may compare by frame index, but a robust implementation should also consider sequence alignment using normalized code locations.

A longest-common-subsequence or similar ordered matching strategy can handle:

- one missing frame;
- an extra wrapper frame;
- partial RBP-chain capture;
- slightly different stack depths.

Keep the result understandable.

## 11.4 Navigation

For live-compatible snapshots, a frame result may offer:

```text
Open in Disassembler
```

For imported snapshots, navigation must not silently jump to unrelated live memory.

Offline imported frames can still show saved logical disassembly if it was captured.

If the currently connected live target can be proven to match the snapshot target and the user explicitly requests live navigation, that may be considered later.

---

# 12. Register Comparison

## 12.1 Raw equality

Compare register raw bytes first.

Do not compare only formatted strings.

## 12.2 Semantic roles

Treat semantic roles specially where useful:

```text
InstructionPointer
StackPointer
FramePointer
```

This allows architecture-neutral presentation.

## 12.3 Group comparison

For each register:

```text
Group A values
Group B values
```

Classify whether it is:

- constant within A;
- constant within B;
- same constant in both;
- different stable constants;
- variable in A;
- variable in B;
- unavailable.

Example:

```text
Register: R12

Player:
0x0000000000000001
0x0000000000000001
0x0000000000000001

Enemy:
0x0000000000000000
0x0000000000000000
0x0000000000000000

Result:
Stable in both groups, different between groups
```

This is potentially useful evidence.

## 12.4 Pointer caution

A register containing a different address in every snapshot is not automatically useful.

Example:

```text
RDI
Player 1: 0x239812000
Player 2: 0x239918000
Enemy 1:  0x239A45000
Enemy 2:  0x239B12000
```

The comparer should classify it as variable rather than suggesting that raw pointer values themselves distinguish Player from Enemy.

A deeper memory comparison may later reveal useful fields behind those pointers.

---

# 13. Trigger and Current Instruction Comparison

The comparer should display both:

```text
Trigger Instruction
Stop/Current Instruction
```

for watchpoint snapshots.

For software breakpoint snapshots, these may naturally refer to the same logical instruction depending on the event semantics.

Example:

```text
Watched Address
0x239629040

Trigger
0x8033D981
01 70 40
add [rax+40h],esi

Stop
0x8033D984
48 8B 4F 30
mov rcx,[rdi+30h]
```

The comparison engine should not collapse these two concepts.

---

# 14. Logical Disassembly Comparison

## 14.1 Compare original code

Only compare logical/original bytes and decoded instructions.

Debugger instrumentation such as `INT3` must not create differences.

## 14.2 Useful comparisons

The comparer may show:

- identical current instruction;
- identical trigger instruction;
- differences in surrounding code;
- differences at caller addresses;
- common call target;
- different caller;
- different direct branch targets.

## 14.3 Module-relative comparison

Prefer module-relative addresses when available.

This allows snapshots from different launches to compare meaningfully.

## 14.4 Saved offline presentation

Imported snapshots should still be able to display their captured instruction context without a live target.

This is one of the main reasons disassembly must be part of the snapshot rather than only an address reference.

---

# 15. Breakpoint and Watchpoint Context Comparison

Include a dedicated comparison section for:

```text
Type
Mechanism
Address
Access
Size
Lifetime
Enabled state
Triggered item
Trigger resolution
Watched address
```

This helps distinguish snapshots captured under different debugger setups.

It also makes exported snapshots diagnostically useful when reproducing debugger issues later.

---

# 16. Memory Context Comparison

## 16.1 Standard stack memory

If a bounded stack-memory block is captured, compare it conservatively.

Do not attempt automatic symbolic interpretation in the first version.

Useful results include:

- identical bytes/ranges;
- changed ranges;
- pointer-sized words that are stable;
- module-relative code pointers where resolvable.

## 16.2 Deep snapshot pointer memory

Deep snapshots may capture bounded memory around selected registers.

Example future analysis:

```text
[RDI+0x18]

Player:
01
01
01

Enemy:
00
00
00

Result:
Stable in both groups, different between groups
```

This is exactly the type of candidate discriminator useful for player/enemy separation.

However, this feature should be carefully bounded.

Recommended first implementation:

- user selects which register(s) to inspect;
- fixed configurable maximum bytes around each selected pointer;
- no recursive pointer chasing;
- invalid/unmapped reads are recorded as unavailable rather than treated as zero.

## 16.3 Future Structure Viewer integration

If Structure Viewer is implemented later, comparer-discovered memory offsets should be easy to send there.

Do not create a second structure-analysis system inside the comparer.

---

# 17. Candidate Discriminator Analysis

The comparer should eventually include a Summary section that surfaces the most useful stable differences.

The engine should prioritize evidence in this general order:

1. Different stable call-stack path.
2. Different stable register value.
3. Different stable module-relative pointer/code address.
4. Different stable captured memory field.
5. Different trigger/current instruction context.
6. Other repeated structured differences.

Example result:

```text
Potential Discriminators

Call Stack
Player first unique frame:
eboot.bin+0x340810

Enemy first unique frame:
eboot.bin+0x351620

Registers
R12
Player: 1 in 3/3 snapshots
Enemy: 0 in 3/3 snapshots

Memory
[RDI+0x18]
Player: 01 in 3/3 snapshots
Enemy: 00 in 3/3 snapshots
```

Use wording such as:

```text
Potential discriminator
Stable difference
Candidate
```

Do not label something "Player Flag" automatically.

---

# 18. Comparer UI Layout

A recommended layout is:

```text
+--------------------------------------------------------------+
| Call Stack Comparer                                          |
+----------------------+---------------------------------------+
| Snapshot Collection  | Comparison Summary                    |
|                      |                                       |
| Player Hit 1         | Mode: Group A vs Group B              |
| Player Hit 2         |                                       |
| Player Hit 3         | Stable differences...                 |
| Enemy Hit 1          |                                       |
| Enemy Hit 2          |                                       |
| Enemy Hit 3          |                                       |
+----------------------+---------------------------------------+
| Comparison detail selector:                                  |
| Summary | Call Stack | Registers | Instructions | Context     |
+--------------------------------------------------------------+
| Detail table                                                   |
+--------------------------------------------------------------+
```

The exact layout can change during implementation, but the following principles should remain:

- snapshot collection stays visible;
- comparison mode and selected groups/snapshots are obvious;
- details do not overload the main Debugger window;
- tables use existing project selection/export patterns;
- splitters follow existing proportional layout rules;
- all themes work without layout changes.

---

# 19. Comparer Export

The comparer itself should support export.

Two different outputs are useful.

## 19.1 Export selected snapshots

Export original snapshot JSON.

## 19.2 Export comparison results

Comparison results are derived data and can use Universal Export where tabular.

Useful formats:

```text
JSON
CSV
TSV
Markdown Table
```

A complete comparison report may also use a dedicated structured JSON model if the result becomes hierarchical.

Do not modify original snapshot files when exporting comparison results.

---

# 20. Mock Backend Requirements

The Mock plugin should be extended so all major behaviors can be tested without PS5 hardware.

It should provide deterministic scenarios for:

- software breakpoint stop;
- hardware watchpoint stop;
- stop RIP after trigger instruction;
- known resolved watchpoint trigger;
- intentionally unresolved trigger case;
- deterministic registers;
- deterministic multi-frame call stack;
- two deliberately different execution paths;
- stable group-specific register difference;
- optional stable group-specific memory-field difference;
- logical disassembly around trigger/current instruction.

Recommended deterministic datasets:

```text
Player Snapshot
Enemy Snapshot
Player Group sample 1/2/3
Enemy Group sample 1/2/3
```

These should make the comparer tests repeatable.

---

# 21. Automated Verification Requirements

## 21.1 Watchpoint trigger tests

Verify:

- current RIP is preserved;
- trigger address is stored separately;
- resolved marker moves to trigger instruction;
- unresolved marker does not falsely identify current RIP as the trigger;
- software breakpoint marker behavior is unchanged;
- logical bytes remain unchanged;
- multiple markers can coexist;
- event export contains both trigger and stop information.

## 21.2 Snapshot capture tests

Verify:

- paused session required;
- stale target rejected;
- session generation mismatch rejected;
- capture cannot combine data from two stops;
- all required sections captured;
- unavailable optional section does not invalidate entire snapshot;
- capture is immutable after publication;
- continuing execution does not mutate old snapshot;
- cancellation publishes nothing.

## 21.3 JSON tests

Verify:

- canonical schema name/version;
- 64-bit addresses survive exactly;
- raw register bytes survive exactly;
- enum values survive;
- null/optional values survive;
- logical instruction bytes survive;
- trigger/current distinction survives;
- breakpoint/watchpoint context survives;
- serialize/deserialize round trip is equivalent;
- invalid schema rejected clearly;
- unsupported future schema rejected clearly;
- additive unknown fields do not break supported schema where safe.

## 21.4 Comparer tests

Verify:

- pairwise identical snapshot;
- pairwise different snapshot;
- first call-stack divergence;
- missing frame alignment;
- module-relative equality across different raw base addresses;
- stable register difference across groups;
- variable register values are not classified as stable;
- unavailable sections remain unavailable;
- imported snapshot compares identically to its pre-export source;
- logical disassembly ignores physical breakpoint instrumentation.

## 21.5 Window lifecycle tests

Verify:

- comparer opens modeless;
- MainWindow can activate above it;
- comparer can activate above MainWindow;
- Debugger can close while comparer remains open with offline snapshots;
- application shutdown closes comparer cleanly;
- no WPF close re-entry regression;
- reconnecting a target does not silently make old snapshots live.

---

# 22. Live PS5 Verification Requirements

After automated tests pass, focused live PS5 verification should cover the following.

## 22.1 Watchpoint trigger presentation

Use a known memory write.

Verify:

```text
Trigger instruction:
the actual memory-accessing instruction

Stop/current RIP:
the following instruction
```

The Disassembler marker must be on the trigger instruction.

Registers must still correspond to the real stop context.

## 22.2 Software breakpoint regression

Verify:

- original bytes remain visible;
- `Breakpoint` marker remains on the intended instruction;
- no `CC` leakage;
- stepping still works;
- Step Over remains breakpoint-aware.

## 22.3 Hardware watchpoint cleanup regression

Repeat the verified rev31 safety case:

- leave watchpoint active;
- close application without manually removing it;
- trigger the watched access afterward;
- game must continue normally.

This must remain a permanent regression gate.

## 22.4 Multiple watchpoints

Verify current conservative attribution behavior remains correct, especially when ps5debug-NG does not expose usable DR6 trigger bits.

Do not guess which watchpoint fired.

## 22.5 Overlapping software breakpoint/watchpoint external limitation

Reconfirm that the already documented backend limitation remains contained.

Do not treat the missing second event as a host trigger-resolution failure.

## 22.6 Snapshot capture

Capture at least:

- software breakpoint snapshot;
- hardware watchpoint snapshot;
- multiple samples from one behavior;
- multiple samples from a contrasting behavior.

Export/import one of them and confirm the imported representation matches the live capture.

## 22.7 Player/enemy-style comparison

When a suitable game scenario is available:

```text
Group A: Player
Group B: Enemy
```

Capture multiple samples and verify the comparer can correctly show:

- common stack frames;
- first differing frame if present;
- stable register differences if present;
- no false stable result when data actually varies.

The test does not require that every game exposes an obvious player/enemy discriminator. The required result is that the comparer reports the captured evidence correctly.

---

# 23. Performance Requirements

The feature should remain responsive.

## 23.1 Snapshot capture

A Standard Snapshot should be small and quick.

Target design:

- bounded register data;
- at most current call-stack depth;
- bounded disassembly windows;
- bounded stack-memory window;
- no unlimited memory scan.

## 23.2 Deep Snapshot

Deep capture may be slower, but must:

- be explicit;
- show progress if needed;
- support cancellation;
- enforce hard byte/read-count limits;
- never recursively walk arbitrary pointers without a user-defined bound.

## 23.3 Comparer

Comparison should run on captured local data.

Once snapshots exist, comparison should require no target traffic.

This means large multi-snapshot comparisons should be CPU/memory operations on the PC and should not affect the game.

## 23.4 Storage

Snapshot size is not expected to be a major problem if capture remains bounded.

Even hundreds of snapshots should be manageable.

Do not optimize prematurely by removing useful context.

---

# 24. Error Handling

All errors should be specific and actionable.

Examples:

```text
Cannot capture snapshot because the debugger is running.
```

```text
Cannot capture snapshot because the debugger session no longer matches the active target.
```

```text
Watchpoint trigger instruction could not be resolved safely.
```

```text
Snapshot file uses an unsupported schema version.
```

```text
Register section could not be captured; the rest of the snapshot was preserved.
```

```text
Imported snapshot is offline and cannot control the current debugger.
```

Do not silently replace missing data with zero/default values when that changes meaning.

---

# 25. Safety Rules

The following are mandatory.

- Snapshot capture is read-only.
- Comparison is read-only.
- Import is read-only.
- Import must never recreate live breakpoints/watchpoints automatically.
- Import must never reconnect to a target automatically.
- Trigger resolution must never write target memory.
- Deep memory capture must use bounded reads only.
- A stale debugger session must reject new live captures.
- Logical disassembly must continue masking debugger instrumentation from saved game-code bytes.
- Existing safe breakpoint/watchpoint cleanup behavior must not be weakened.
- Existing rev31 watchpoint detach cleanup must remain intact.
- Existing staged software-breakpoint cleanup must remain intact.
- Existing session-generation protections must remain intact.

---

# 26. Documentation Work Required During Implementation

Every implementation revision must update the normal project documentation.

The following documents should eventually reflect the completed design:

- `README.md`
- `CHANGELOG.md`
- `docs/TeeKay87_Memory_Engine_Full_Development_Action_Plan.md`
- debugger architecture documentation;
- disassembly architecture documentation;
- Universal Export documentation;
- Plugin SDK documentation if event contracts change;
- PS5 debugger documentation;
- UI/workspace documentation;
- testing/verification documents.

The Full Development Action Plan should be updated so the debugger section no longer describes finalization as only "universal debugger list export."

It should explicitly include:

- watchpoint trigger/current-instruction separation;
- complete Debugger Snapshot model;
- JSON snapshot export/import;
- Call Stack Comparer / Debugger Snapshot comparison;
- pairwise and grouped comparison;
- offline imported-snapshot comparison.

The action plan should continue to describe Universal Export for the individual debugger tables as a separate requirement.

---

# 27. Recommended Internal Types

The exact names may change after reviewing the current code, but the implementation should aim for a structure similar to:

```text
Plugin SDK
----------
DebuggerEvent
DebuggerTriggerResolution
DebuggerBreakpoint
DebuggerBreakpointRequest
DebuggerRegister
DebuggerCallFrame
...

Core
----
DebuggerSnapshot
DebuggerSnapshotMetadata
DebuggerSnapshotSectionStatus
DebuggerSnapshotEventContext
DebuggerSnapshotRegister
DebuggerSnapshotCallFrame
DebuggerSnapshotDisassembly
DebuggerSnapshotInstruction
DebuggerSnapshotBreakpoint
DebuggerSnapshotMemoryBlock

DebuggerSnapshotCaptureService
DebuggerWatchpointTriggerResolver
DebuggerSnapshotJsonSerializer
DebuggerSnapshotComparer
DebuggerSnapshotGroupComparer

App / WPF
---------
DebuggerViewModel
CallStackComparerViewModel
CallStackComparerWindow
SnapshotListItemViewModel
ComparisonResultViewModels
```

Avoid duplicating Plugin SDK models unnecessarily.

Where an existing immutable neutral model can safely be embedded in a snapshot, reuse it or map it through one centralized conversion path.

---

# 28. Recommended Snapshot JSON Shape

This is illustrative, not a frozen schema.

```json
{
  "schema": "teekay87-memory-engine-debugger-snapshot",
  "schemaVersion": 1,
  "snapshotId": "7f6d3a63-...",
  "capturedAtUtc": "2026-09-11T21:15:42.184Z",
  "label": "Player hit 1",
  "group": "Player",

  "source": {
    "applicationVersion": "0.1.7",
    "applicationRevision": 31,
    "pluginId": "...",
    "pluginName": "PlayStation 5",
    "pluginVersion": "0.1.0",
    "pluginRevision": 38,
    "processId": 1234,
    "processName": "eboot.bin",
    "architecture": "X64",
    "pointerWidth": 64,
    "endianness": "Little"
  },

  "event": {
    "kind": "Watchpoint",
    "stopReason": "Watchpoint",
    "threadId": 123,
    "instructionPointer": "0x8033D984",
    "triggerInstructionAddress": "0x8033D981",
    "triggerResolution": "DisassemblyDerived",
    "watchedAddress": "0x239629040",
    "watchpointAccess": "Write",
    "watchpointSize": 4
  },

  "registers": [
    {
      "name": "RAX",
      "role": "None",
      "bitWidth": 64,
      "rawBytes": "4090623902000000",
      "formattedValue": "0x0000000239629040"
    }
  ],

  "callStack": [
    {
      "index": 0,
      "instructionAddress": "0x8033D984",
      "module": "eboot.bin",
      "moduleOffset": "0x33D984"
    }
  ],

  "disassembly": {
    "instructions": [
      {
        "address": "0x8033D981",
        "bytes": "017040",
        "markers": ["Watchpoint hit"],
        "instruction": "add [rax+40h],esi"
      },
      {
        "address": "0x8033D984",
        "bytes": "488B4F30",
        "markers": [],
        "instruction": "mov rcx,[rdi+30h]"
      }
    ]
  }
}
```

The actual schema should be finalized only after reviewing all existing neutral debugger/disassembly models.

---

# 29. Implementation Sequence

The recommended order is deliberately dependency-driven.

## Phase A — Event semantics

Implement and verify:

- event-model fields;
- watchpoint trigger resolution;
- corrected Disassembler marker;
- event/export source updates required by new fields.

Do not begin snapshot persistence until this context is correct.

## Phase B — Snapshot Core model

Implement:

- immutable snapshot;
- section status;
- capture metadata;
- event/register/call-stack/disassembly/breakpoint context.

Add deterministic Mock capture tests.

## Phase C — Snapshot capture

Implement the shared live capture service.

Verify session consistency and cancellation.

## Phase D — JSON serializer/importer

Implement:

- schema;
- serializer;
- deserializer;
- validation;
- transactional export;
- exact round-trip tests.

## Phase E — Universal debugger list export

Complete the original development-plan requirement for:

- Threads;
- Registers;
- Breakpoints/Watchpoints;
- Call Stack;
- Events.

These exports remain independent from complete snapshot JSON.

## Phase F — Call Stack Comparer window

Implement the separate modeless window and snapshot collection.

Start with pairwise comparison.

## Phase G — Group comparison

Add:

- arbitrary user-defined groups;
- stable-within-group analysis;
- cross-group differences;
- summary/candidate view.

## Phase H — Optional Deep Snapshot comparison

Add bounded memory-context comparisons if the Standard Snapshot comparer is stable.

Do not block the core comparer on unlimited/deep memory analysis.

## Phase I — Final debugger regression

Run:

- full automated suite;
- complete Mock debugger cycle;
- complete live PS5 debugger cycle;
- shutdown/cleanup stress;
- stale-session stress;
- export/import round trip;
- comparer runtime/UI verification.

Only after this should the `0.1.7` Debugger block be considered finished and ready for the normal version-completion decision.

---

# 30. Definition of Done

The remaining Debugger block is complete when all of the following are true.

## Watchpoints

- `Watchpoint hit` identifies the triggering instruction when safely resolved.
- Real stop/current RIP remains separately available.
- Trigger resolution status is explicit.
- Unresolved cases do not lie.
- Software breakpoint presentation remains correct.
- Logical/original code remains authoritative.

## Snapshot model

- Complete neutral debugger snapshot exists.
- Standard capture works from a paused session.
- Snapshot is immutable.
- Optional unavailable sections are represented honestly.
- No snapshot operation writes target memory.

## Export/import

- Flat debugger lists support Universal Export.
- Complete debugger snapshots export to structured JSON.
- JSON imports offline.
- Export/import round trip preserves all schema data.
- 64-bit addresses remain exact.
- Files are transactionally published.

## Call Stack Comparer

- Opens in its own modeless window from the Call Stack workflow.
- Works with live-captured snapshots.
- Works with imported snapshots.
- Supports pairwise comparison.
- Supports user-defined groups.
- Identifies common and divergent call-stack paths.
- Compares registers using raw structured values.
- Compares trigger/current instruction context.
- Compares logical disassembly.
- Shows stable cross-group differences without inventing semantic meaning.
- Can export useful comparison data.

## Safety/regression

- Rev31 PS5 watchpoint detach cleanup still passes.
- Software breakpoint cleanup still passes.
- Step Into/Over/Out and Run to Address still pass.
- Breakpoint/watchpoint manager still passes.
- Threads/Registers/Call Stack remain correct.
- Disassembler logical-byte behavior remains correct.
- Modeless window lifecycle remains correct.
- Disconnect/reconnect stale-session behavior remains correct.
- Full automated suite passes.
- Full Mock runtime verification passes.
- Final live PS5 regression passes.

---

# 31. Final Development Handover Summary

The remaining debugger work should no longer be viewed as three unrelated features.

They form one pipeline:

```text
Correct Debugger Event Semantics
        |
        v
Complete Debugger Snapshot
        |
        +----------------------+
        |                      |
        v                      v
Persistent JSON            Live Comparison
        |                      |
        v                      |
JSON Import -------------------+
        |
        v
Call Stack Comparer
```

The watchpoint work must happen first because it defines what a correct debugger event actually means.

The snapshot model must happen before export because export should serialize the real debugger context rather than whatever happens to be visible in a particular table.

The comparer must consume the same snapshot model so live and imported data are indistinguishable to the comparison engine.

The user-facing **Call Stack Comparer** should be a separate window opened from the Call Stack panel. This keeps the main Debugger focused on live execution control while the comparer becomes a dedicated analysis workspace.

The resulting architecture provides a clean foundation not only for player/enemy analysis, but also for later features such as:

- Find What Writes / Reads / Accesses;
- Break and Trace;
- Structure Viewer integration;
- memory snapshot comparison;
- cheat-project creation;
- patch/injection analysis.

Most importantly, debugger data will have one consistent lifecycle:

```text
Capture once
    ->
Inspect live
    ->
Save
    ->
Reload
    ->
Compare
    ->
Use the evidence for reverse engineering
```

That should be the final direction for completing the current Debugger feature block.
