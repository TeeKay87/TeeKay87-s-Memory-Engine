# Application 0.1.7.rev32 Source Review — Watchpoint Trigger Resolution and Event Semantics

## Scope

Rev32 starts Phase A of the debugger-finalization handover from the verified rev31 baseline. The source review covered the complete repository documentation set and the application/Core/Plugin SDK/Mock/PS5/test source tree before the implementation was applied.

## Implemented design

- Plugin API `2.17.0` adds explicit watchpoint trigger-resolution semantics without removing the existing `DebuggerEvent` constructors.
- `InstructionPointer` remains the authoritative stop/current instruction pointer.
- `TriggerInstructionAddress` is independent and carries `BackendExact` or `DisassemblyDerived` status when resolved.
- Watchpoint address, access mode, and size remain neutral and are available directly from the event context.
- Core `DebuggerWatchpointTriggerResolver` accepts logical disassembly and resolves only a unique valid instruction whose end address equals the stop IP.
- The resolver never uses `RIP - 1` or architecture-specific decoding.
- Backend-exact trigger context is preserved unchanged.
- Disassembler overlays show `Watchpoint hit` only at a resolved trigger instruction; unresolved events show `Watchpoint stop (trigger unresolved)` at the real stop IP.
- The Debugger Events table exposes stop IP, trigger instruction, watched address, and trigger-resolution status separately.
- Mock models a deterministic post-access stop and supplies an exact trigger.
- PS5 keeps callback RIP as the stop/current IP and no longer describes it as the memory-accessing instruction before host resolution.
- Rev31 active/staged hardware-watchpoint cleanup remains unchanged.

## Source compatibility

The new event surface is additive. The pre-rev32 constructors remain present and produce `Unresolved` trigger state. Compatible older 2.x plugins therefore remain loadable under the existing major-version compatibility policy; they simply do not provide backend-exact trigger metadata unless rebuilt against the new surface.

## Static package review

The package was checked for application/plugin metadata consistency, project/XML/JSON parseability, source delimiter balance, test-registry count, and accidental build artifacts. The current environment does not provide the Windows/.NET toolchain, so a clean Windows build and the exact automated-suite result remain required before rev32 can be accepted.
