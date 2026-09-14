# In-Memory Test Target Plugin

## Purpose

The In-Memory Test Target is the deterministic development plugin used by TeeKay87's Memory Engine to exercise platform-neutral Core and Plugin SDK behavior without requiring a physical target.

This directory contains documentation that belongs specifically to the mock plugin. General Plugin SDK architecture remains under `docs/architecture/`.

## Plugin Identity

| Property | Value |
| --- | --- |
| Plugin id | `platform.mock.in-memory` |
| Plugin version | `1.0.1.rev17` |
| Plugin API | `2.18.0` |
| Platform | Development |
| Backend | In-Memory |
| Architecture | Custom / Unknown CPU, 64-bit addresses, 64-bit pointers, little-endian |

The plugin version and revision are independent from the TeeKay87's Memory Engine host application version.

Current host application `0.1.7.rev35` uses Plugin API `2.18.0`. Mock plugin `1.0.1.rev17` preserves the deterministic debugger fixtures, including the rev32 `BackendExact` post-access watchpoint event used to distinguish trigger instruction from stop/current IP. Rev35 does not add a Mock-specific Disassembler watchpoint resolver because the Mock disassembler intentionally uses a synthetic non-x86 instruction set; absence of the optional resolver therefore leaves automatic Disassembler **Add Watchpoint** unavailable on Mock while the generic Add Breakpoint flow remains testable. The verified debugger lifecycle/thread/register/software-breakpoint/hardware-watchpoint/call-stack/step behavior is otherwise unchanged.

## Current Capabilities

The mock plugin currently advertises:

- target connection;
- process enumeration;
- foreground-process discovery;
- memory-region enumeration;
- memory read;
- memory write;
- architecture-neutral disassembly;
- deterministic debugger attach, pause, continue, detach, and neutral pause/resume events;
- deterministic debugger thread enumeration;
- individual debugger-thread suspend/resume while the overall target is Running;
- deterministic paused-thread register snapshots, safe writable-register verification, and read-only wide-register fixtures;
- deterministic persistent/temporary software execute breakpoint lifecycle and hit events;
- deterministic persistent/temporary Read, Write, and Read/Write hardware-watchpoint lifecycle and hit events with a four-slot hardware limit;
- deterministic three-frame paused-thread call stacks;
- deterministic native Step Into used by the host's shared Step Over, Step Out, and Run-to composition.

It intentionally does not advertise native-scanner, assembler, pointer-scanner, or cheat capability flags that have not been implemented. Shared scanning remains available through the neutral MemoryRead/MemoryRegionEnumeration path and the declarations below. Disassembly is supplied by a deterministic synthetic Mock provider used only for contract/Core regression testing; it is not an x86-64 decoder.

## Scanner Capability Declarations

Plugin `1.0.1.rev17` targets Plugin API `2.18.0` and supplies the same concrete Value Type definitions for the shared scanner: UInt8, Int8, UInt16, Int16, UInt32, Int32, UInt64, Int64, Float32, Float64, and ByteArray. The mock plugin currently reuses the optional standard definitions in the Plugin SDK.

Core now supplies the standard Scan Type catalog to Mock automatically. The Mock plugin continues to own only its Value Types and target behavior, which makes it the deterministic regression target for the same Scan Type semantics future platform plugins receive.

## Connection

The mock target requires no connection parameters. Its `ConnectionSettings` collection is empty, and connecting creates a new deterministic in-memory session.

## Deterministic Target Layout

The session exposes one process:

```text
Process:      TestGame.exe
Process ID:   1001
Memory base:  0x10000000
Memory size:  0x00010000
```

Known values are initialized at stable addresses:

| Value | Address | Type | Initial value |
| --- | --- | --- | ---: |
| Health | `0x10000100` | Float32 | `100.0` |
| Ammo | `0x10000104` | Int32 | `30` |
| Money | `0x10000108` | Int32 | `5000` |

A separate deterministic code fixture begins at:

```text
0x10000400
```

`MockDisassemblerProvider` interprets that fixture with a small synthetic Mock instruction set that exercises ordinary instructions, direct call/conditional-jump targets, return, no-op, and invalid/truncated bytes. Rev6 also emits deterministic neutral syntax tokens for mnemonics, flow-control mnemonics, the synthetic `r0` register, numbers, and plain separators, allowing the host renderer to be regression-tested without x86-64 knowledge. The fixture exists to test the neutral `IDisassemblerProvider`/Core pipeline and deliberately does not model real x86-64 opcodes. Rev7 therefore declares `CpuArchitecture.Unknown`; `MockDisassemblerProvider` accepts that custom 64-bit little-endian descriptor and rejects a true X64 descriptor.

These values are development fixtures. They are intended to support deterministic scanner, saved-address, freeze, memory-viewer, export, disassembly, and regression tests as those shared subsystems are implemented.


## Deterministic Debugger Backend

Rev2 adds the first `IDebuggerProvider` implementation without changing the deterministic target layout. The provider accepts only the single `TestGame.exe` process and permits one active debugger attachment at a time.

The attached session begins in `Running`. Its deterministic control flow is:

```text
Attach    -> Running
Pause     -> Paused
Continue  -> Running
Detach    -> Detached
```

Pause emits exactly one neutral debugger event with:

```text
Kind:                 Paused
Execution state:      Paused
Stop reason:          PauseRequested
Thread ID:            1
Instruction pointer:  0x10000400
Message:              Mock target paused.
```

Continue emits exactly one `Resumed` event with execution state `Running`, the same deterministic thread/instruction-pointer context, and message `Mock target resumed.`.

Rev6 adds the first optional attached-session debugger services through a deterministic three-thread fixture:

```text
0x1  Main
0x2  Worker
0x3  Render
```

All three enumerate as `Running` while the whole debugger target runs. Whole-target Pause maps ordinary rows to neutral `Stopped`; an individually suspended row remains `Suspended` across Pause/Continue until explicitly resumed. Per-thread Suspend/Resume is intentionally allowed only while the whole debugger session is `Running`, and the backend rejects unknown ids plus duplicate/invalid state transitions. Refresh re-enumerates the same fixture and lets the host verify selection preservation by neutral thread id. Rev7 added deterministic 64-bit register snapshots for every Mock thread while Paused. Those General/Control rows use neutral little-endian unsigned encoding, expose semantic frame/stack/instruction-pointer roles, and remain writable so the shared Edit -> Write -> Refresh -> exact read-back verification flow can be tested safely.

Mock rev11 added four read-only extended fixtures to each paused-thread snapshot without assigning a real CPU architecture to the Mock target:

| Register | Width | Group | Purpose |
| --- | ---: | --- | --- |
| `FP0` | 80-bit | Floating Point | exercises an x87-style non-power-of-two register width |
| `V0` | 128-bit | SIMD | exercises 128-bit vector presentation |
| `V1` | 256-bit | SIMD | exercises 256-bit vector presentation |
| `D0` | 64-bit | Debug | exercises a non-General read-only register group |

The values are deterministic byte patterns derived from the selected thread's existing register seed. They use the same neutral `UnsignedLittleEndian` encoding as the ordinary numeric rows, are intentionally not writable, and exist only to prove that Core/WPF handle arbitrary backend-provided width/group metadata without architecture-specific logic. Register access remains rejected while Running. Hardware watchpoints remain implemented as verified rev14 behavior. Rev15 adds the call-stack and native Step Into fixtures described below. Disposing the target session disposes any active debugger session so a test attachment cannot outlive its in-memory target.


### Deterministic Software Breakpoints

Mock `1.0.0.rev13` implements `IDebuggerBreakpointService` and `IDebuggerBreakpointStateService` and advertises `Breakpoints`. It accepts the one-byte Software/Execute request shape, keeps persistent and temporary lifetimes, rejects duplicate addresses, and provides deterministic Enable/Disable/Remove state without exposing any platform-specific instruction encoding. Rev13 additionally validates that the requested address belongs to the mock target's mapped range and the explicit `CodeBytes` fixture; mapped data and unmapped addresses are rejected.

When Continue is requested and at least one enabled breakpoint exists, the deterministic backend schedules a hit on the first enabled breakpoint by address/id. The session returns to Paused, Main-thread RIP is updated to the breakpoint address, and one neutral `Breakpoint` event is emitted. Persistent records remain in the manager; temporary records disappear after their first hit. This behavior is intentionally synthetic and exists only to verify the generic host/Core/Plugin SDK workflow.

### Deterministic Hardware Watchpoints

Mock `1.0.0.rev14` reuses the same breakpoint service for `Hardware` requests and advertises `TargetCapabilities.Watchpoints`. Supported access modes are `Read`, `Write`, and `ReadWrite`. Supported widths are 1, 2, 4, and 8 bytes. The address must be naturally aligned to the selected width, and the entire watched range must remain inside the deterministic target memory map. Hardware records have a separate maximum of four slots so their resource model does not consume the 30 synthetic software-breakpoint slots. Equivalent Hardware requests at the same address/size/access are rejected.

A deterministic hardware-watchpoint hit models post-access stop semantics: `DebuggerEvent.InstructionPointer` is the synthetic stop/current instruction after the access, while `TriggerInstructionAddress` identifies the known synthetic accessing instruction and uses explicit trigger-resolution metadata. The watched memory condition remains separate in `TriggeredBreakpoint` / watched-address context. This exercises the neutral distinction between trigger instruction, stop instruction, and watched memory. Persistent records remain available after a hit; temporary records are removed after the first hit.


### Deterministic Call Stack and Stepping

Mock `1.0.0.rev16` implements `IDebuggerCallStackService` and `IDebuggerStepService` and advertises `TargetCapabilities.CallStack` plus `TargetCapabilities.StepExecution`. Both services remain attached-session services and therefore cannot outlive the debugger session that owns the current thread/register context.

Call Stack is available only while the debugger is Paused and only for a known selected thread. Each thread returns three deterministic neutral frames. The top frame uses that thread's current instruction, stack, and frame-pointer values; the second and third frames use stable synthetic caller/entry addresses inside the existing code fixture. Module and symbol text is deterministic so selection preservation and presentation can be tested. Requests while Running or for unknown thread ids are rejected.

Native stepping intentionally implements only `DebuggerStepKind.Into`. A Step Into request validates the selected thread and current Paused state, transitions the target to Running, emits the normal resumed transition, advances the selected thread's synthetic instruction pointer, and completes after a short deterministic delay with a Paused `StepCompleted` event. The updated register and call-stack snapshots therefore exercise the same stop-context refresh path used by a real backend.

Step Over, Step Out, and Run to Address are not separate Mock-specific algorithms. The host composes them from native Step Into, the existing neutral Disassembler, these neutral call frames, and temporary software execute breakpoints. This is deliberate: the Mock backend verifies the generic composition instead of hiding it behind test-only platform behavior.

## Preservation Rule

The mock plugin should remain available throughout development. New generic subsystems should use it when practical before live-platform verification so target-independent regressions can be reproduced without console availability.


## Rev30 Breakpoint Request Validation

Mock `1.0.0.rev16` exposes optional Plugin API `2.16.0` `IDebuggerBreakpointValidationService`. It preflights the same deterministic rules used by the existing add path: Software/Execute requests must resolve to the executable fixture, Hardware data watchpoints must use a supported width/access and natural alignment, equivalent active records are rejected, and the 30 software / 4 hardware slot limits are enforced. Validation does not mutate debugger state.

## 1.0.0.rev17 — Watchpoint Trigger Fixture

Mock now targets Plugin API `2.17.0`. Its deterministic hardware-watchpoint event models post-access stop semantics explicitly: the synthetic memory-access instruction is at `CodeAddress + 4`, while the authoritative stop/current instruction pointer advances to `CodeAddress + 6`. Because the Mock backend owns this deterministic fixture, it reports the trigger as `BackendExact`. This gives the automated suite a stable reference for verifying that trigger and stop addresses are not collapsed into one field.


## 1.0.1.rev17 — Plugin API 2.18 Compatibility

Rev35 advances the host Plugin API to `2.18.0` for the optional `IDisassemblyWatchpointResolver` contract. The Mock plugin advances its semantic version to `1.0.1` and API target to `2.18.0` without adding architecture-specific operand analysis to its synthetic disassembler. Its deterministic debugger/watchpoint fixtures remain unchanged, including the authoritative `BackendExact` trigger at `CodeAddress + 4` and stop/current IP at `CodeAddress + 6`.

The optional resolver is intentionally absent from Mock. Automatic Disassembler **Add Watchpoint** therefore remains disabled there rather than inventing memory-operand semantics for the custom fixture instruction set.
