# Application 0.1.7.rev29 Source Review — Logical Disassembly and Debugger Markers

## Scope Reviewed

The supplied `0.1.7.rev28` package was used as the sole application baseline. Before editing, the complete repository documentation and source tree was traversed: root README/CHANGELOG, every Markdown document under `docs/`, application/Core/Plugin SDK/plugin C# sources, XAML/resources, project/build files, themes, tests, and scripts. The focused review then followed the complete path from `DebuggerViewModel` Software/Execute breakpoint creation and hardware-watchpoint events through `DebuggerSessionCoordinator`, `PluginViewModel`, Core `DisassemblyReader`, `DisassemblySnapshot`, the WPF Disassembler rows, and universal Disassembler export.

Rev28 is the accepted debugger runtime baseline. Its Windows suite passed **135/135**, and focused live PS5 acceptance verified logical software-breakpoint stop context, breakpoint-aware Step Over, natural and interrupted Step Out cleanup, successful and interrupted Run to Address, temporary-breakpoint retirement, safe staged-cleanup Detach without a game crash, and fresh Detach/Reattach state.

A real money-tracing workflow then exposed a presentation gap. A Hardware/Write watchpoint on the discovered money address identified the relevant execution context. The adjacent write instruction was:

```text
0x8033D981  01 70 40  add [rax+40h],esi
```

After a persistent Software/Execute breakpoint was placed at `0x8033D981`, ps5debug-NG correctly armed the breakpoint by replacing the first target byte with `CC`. The ordinary Disassembler subsequently read the physical target bytes and rendered:

```text
0x8033D981  CC       int3
0x8033D982  70 40    jo ...
```

That representation is mechanically correct for the instrumented target buffer but wrong for the intended debugger/cheat-development view. The `INT3` belongs to the debugger, not to the original game instruction, and the false second instruction is only a decode artifact caused by starting again at the remaining bytes. The user-visible Disassembler therefore needs a logical code view in which debugger-owned instrumentation is separated from the original instruction stream.

## Existing Functionality Reused

Rev26 already introduced `_softwareBreakpointInstructionCache` in `DebuggerViewModel` so Step Over can classify the original instruction even after the backend rearms an `INT3`. `CaptureSoftwareBreakpointInstructionAsync` reads the instruction before Software/Execute breakpoint installation through the existing neutral disassembly service. Rev29 deliberately reuses that capture instead of introducing another target read, another decoder, or PS5-specific instruction handling.

The existing debugger session coordinator also already owns plugin/process/connection-generation identity. The logical Disassembler state is therefore attached to the coordinator rather than stored globally or inferred from the currently selected platform.

No new public Plugin SDK contract is required. Plugin API remains `2.15.0`; Mock remains `1.0.0.rev15`; PS5 remains `0.1.0.rev36`.

## Core Logical Disassembly Overlay

### `DisassemblyOverlay`

Core gains a small presentation-neutral overlay model:

- `DisassemblyByteOverlay` associates an address with copied logical bytes;
- `DisassemblyMarker` associates an address with presentation metadata text; and
- `DisassemblyOverlay` carries zero or more byte overlays and markers.

The overlay does not expose debugger or platform transport concepts. It simply describes what a logical disassembly read should see and which address-scoped markers should accompany the snapshot.

### Local byte replacement only

`DisassemblyReader` keeps its previous public overloads and adds overloads that optionally accept a `DisassemblyOverlay`. The normal bounded `IMemoryReader` operation still executes first. If no byte overlay intersects the returned range, the original target buffer continues directly to the provider exactly as before.

When a byte overlay does intersect the range, Core creates a local copy of the returned bytes and replaces only the intersecting bytes in that copy. The plugin `IDisassemblerProvider` then decodes the logical buffer. Instruction raw-byte validation and `DisassemblySnapshot.Bytes` use the same logical buffer, so Address, Bytes, Instruction, copy operations, and export remain internally consistent.

This path never calls `IMemoryWriter` and never writes the logical bytes back to the target. An armed software breakpoint therefore remains fully active while the Disassembler can still display the original instruction.

### Snapshot marker metadata

`DisassemblySnapshot` now carries an immutable `Markers` collection. Markers must fall inside the snapshot byte range. Existing constructor usage remains source-compatible through the original constructor overload.

## Debugger Overlay State

Each `DebuggerSessionCoordinator` now owns one `DebuggerDisassemblyOverlayState`. Its lifetime is therefore identical to the attached debugger session and its target identity. Attach starts from a reset overlay; detach/disposal resets it again.

The state tracks:

- original Software/Execute instructions captured before breakpoint installation;
- current breakpoint records;
- Software/Execute addresses whose backend retirement is staged while Paused; and
- the current paused Hardware Watchpoint event instruction pointer.

For an enabled Software/Execute breakpoint with a captured original instruction, the generated overlay restores the complete original instruction bytes and adds a `Breakpoint` marker. A disabled Software/Execute record is represented by `Breakpoint (disabled)` but does not require a byte replacement after backend cleanup has completed.

Paused remove/disable is a special case. The PS5 implementation may intentionally defer physical backend retirement until a safe Continue/Detach boundary. During that staging window the breakpoint may already be absent or disabled in the visible manager while an `INT3` can still exist in target memory. Rev29 therefore retains the original-byte overlay until the staged retirement is known to have completed. The stale trap cannot leak into the visible Disassembler during that interval.

A paused hardware-watchpoint event adds `Watchpoint hit` at the event's semantic instruction pointer. Watchpoints do not patch code, so this marker has no accompanying byte overlay. Resume or a different paused stop clears the stale hit marker.

## Host Integration

`PluginViewModel` resolves the logical overlay only from an attached `DebuggerSessionCoordinator` whose plugin id, process identity, and connection generation match the Disassembler request. Every existing Disassembler entry point continues through the same `ReadDisassemblyAsync` path, so the logical view applies whether the window was opened from Debugger, Scan Results, Saved Addresses, Memory Viewer, or the main toolbar.

There is no host reference to `Ps5DebuggerSession` and no platform-name branch. A future PC or Xbox plugin can receive the same presentation behavior if its debugger implementation uses the shared neutral breakpoint contracts and original instruction capture.

## Disassembler `Markers` Column

The main instruction grid changes from:

```text
Address | Bytes | Instruction
```

to:

```text
Address | Bytes | Markers | Instruction
```

`DisassemblyInstructionViewModel` joins distinct address markers with ` · `. The column intentionally uses ordinary DataGrid text styling; no new hardcoded color or theme resource is introduced.

The initial marker vocabulary is:

- `Breakpoint`;
- `Breakpoint (disabled)`; and
- `Watchpoint hit`.

The column is intentionally generic. Future intentional patch, NOP, bookmark, current-instruction, trace, or other metadata can be added without changing the table shape, provided the marker reflects real application state rather than replacing instruction data.

## Export Integration

`DisassemblyExportSource` adds selectable structured column `markers` / `Markers` immediately after `bytes`. Displayed and Selected export scopes carry the current snapshot marker collection. Exported `Bytes` and `Instruction` remain the logical/original code view; debugger state is represented separately by `Markers`.

The disassembly export schema remains version `1` because the existing universal export format already supports additive selectable columns. Export still operates on materialized Disassembler data and introduces no additional target I/O.

## Boundary for Later Assembly and Cheat Construction

Rev29 does **not** implement NOP, assembler, instruction editing, or code patch persistence. It establishes the distinction those later features require:

- original/logical code describes the instruction the game owns;
- debugger instrumentation such as `INT3` is temporary runtime machinery and must not replace that visible instruction; and
- a future intentional user patch must be represented as a separate deliberate code state, not confused with either original code or debugger trap bytes.

That separation allows later cheat construction to reason about original bytes reliably and restore them safely when a patch is disabled.

## Automated Verification Changes

Three top-level checks raise the registry from **135** to **138**:

- **Core disassembly debugger-byte overlay** verifies that a physical target `CC` can be replaced only in the local logical read, decodes as the original instruction, preserves markers, and leaves target memory untouched;
- **Core debugger disassembly overlay lifecycle** verifies enabled breakpoint masking/marker state, paused staged retirement, retirement completion, watchpoint-hit marker lifetime, and resume cleanup; and
- **Disassembler debugger markers and logical instruction source contract** verifies Address / Bytes / Markers / Instruction order, row binding, debugger-state publication, matching-session overlay consumption, export propagation, and absence of a direct PS5 dependency in the host path.

The existing Disassembler export test is extended to require the `Markers` column and structured marker JSON.

## Pre-Package Static Validation

The completed rev29 tree was rechecked against the supplied rev28 baseline before packaging. The static gate passed: required current-revision documentation is present; no `bin`, `obj`, `.vs`, or cache directories are included; every XAML/project XML file parses; every JSON resource parses; changed text files are UTF-8 with final newlines and no trailing whitespace; changed/new C# files pass delimiter/namespace sanity checks; AppInfo reports `0.1.7.rev29` and the correct feature title; Mock, PS5, and Plugin SDK source trees remain byte-identical to rev28; the automated registry contains exactly **138** registered checks; the logical-disassembly/Markers source contracts are present; the host/Core overlay path contains no direct PS5 dependency; and maintained current-state documentation contains no stale rev28-current wording.

This is a static/package-readiness gate only. The clean Windows build and executable **138/138** test run remain part of runtime acceptance and are intentionally not claimed by this source review.

## Version and Compatibility

| Component | rev29 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev29` | Debugger/Disassembler integration revision |
| Feature title | `Logical Disassembly and Debugger Markers` | New application title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev36` | Unchanged |
| Automated registry | `138` | Three new checks plus extended Disassembler export coverage |

## Development Order

Rev29 remains part of the `0.1.7` Debugger block. If the clean Windows build, **138/138** automated gate, focused Mock logical-code/marker regression, and live PS5 breakpoint/watchpoint Disassembler acceptance all pass without another correction, the closing milestone becomes `0.1.7.rev30 — Integration, Export and Finalization`.
