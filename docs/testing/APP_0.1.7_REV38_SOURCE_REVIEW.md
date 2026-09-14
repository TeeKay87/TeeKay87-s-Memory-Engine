# Application 0.1.7.rev38 Source Review — Disassembler Arbitrary-Row Watchpoint Resolution

## Scope

Rev38 is built directly from the user-supplied `0.1.7.rev37` package. Before the code change, `README.md`, `CHANGELOG.md`, and every Markdown document under `docs/` were read, followed by a complete source/project scan. The change is intentionally host-side only.

## Existing implementation reused

The revision reuses the existing rev35 architecture rather than adding a second watchpoint path:

- `DisassemblerWindow` still requires exactly one selected row for debugger actions.
- `MainWindow` still locates the already attached Debugger for the same target/session.
- `PluginViewModel.ResolveDisassemblyWatchpointTarget(...)` still calls optional plugin-owned `IDisassemblyWatchpointResolver`.
- PS5 still owns x86-64/Iced operand, width, access, and effective-address derivation.
- `DebuggerViewModel.ValidateAddressActionRequest(...)` remains authoritative before `AddBreakpointAsync(...)` creates the Hardware request.

## Change

The previous `DebuggerViewModel.CanResolveDisassemblyWatchpointAt(ulong instructionAddress)` gate required the selected row to equal either the current stop IP or the current resolved trigger address. Live Gate 3 verification showed this prevented useful, otherwise safely resolvable memory-access instructions elsewhere in the same view from enabling Add Watchpoint.

Rev38 replaces that address-specific gate with `CanResolveDisassemblyWatchpoint()`. It requires:

- live matching target/session;
- debugger Paused;
- debugger not busy;
- non-empty current register snapshot.

The selected instruction is then evaluated by the existing platform resolver and backend validator. No x86 operand parsing was moved into WPF/Core and no plugin contract changed.

## Safety boundary

For a non-current instruction using base/index addressing, the resolved target is the address implied by the **current paused register snapshot**. The host does not claim that this is historical execution state. If operand structure, width, register availability, mapped range, alignment, access mode, or slot availability cannot be established safely, Add Watchpoint remains disabled.

`LEA`, non-memory instructions, ambiguous/multiple memory operands, unsupported widths/address registers, Running state, detached/stale sessions, and multi-selection remain rejected by the existing layers.

## Versioning

- Application: `0.1.7.rev38`
- Feature: `Disassembler Arbitrary-Row Watchpoint Resolution`
- Plugin API: unchanged at `2.18.0`
- Mock: unchanged at `1.0.1.rev17`
- PS5: unchanged at `0.1.2.rev39`
- Automated registry: unchanged at 152 checks

No plugin source changed, so plugin semantic versions do not advance.
