# In-Memory Test Target Plugin

## Purpose

The In-Memory Test Target is the deterministic development plugin used by TeeKay87's Memory Engine to exercise platform-neutral Core and Plugin SDK behavior without requiring a physical target.

This directory contains documentation that belongs specifically to the mock plugin. General Plugin SDK architecture remains under `docs/architecture/`.

## Plugin Identity

| Property | Value |
| --- | --- |
| Plugin id | `platform.mock.in-memory` |
| Plugin version | `1.0.0.rev10` |
| Plugin API | `2.13.0` |
| Platform | Development |
| Backend | In-Memory |
| Architecture | Custom / Unknown CPU, 64-bit addresses, 64-bit pointers, little-endian |

The plugin version and revision are independent from the TeeKay87's Memory Engine host application version.

Host application `0.1.7.rev7` uses Plugin API `2.13.0`. The verified rev2 debugger lifecycle/event backend remains the base, while Mock plugin `1.0.0.rev10` is the first deterministic consumer of the thread contracts that were already public in API `2.13.0`. It advertises `Debugger`, `ThreadEnumeration`, `ThreadControl`, and `RegisterAccess`. The attached session exposes deterministic thread services plus a paused-thread register service; breakpoint/watchpoint/call-stack/step capabilities remain unadvertised.

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
- deterministic paused-thread register snapshots and safe writable-register verification.

It intentionally does not advertise native-scanner, breakpoint/watchpoint, call-stack, step, assembler, pointer-scanner, or cheat capability flags that have not been implemented. Shared scanning remains available through the neutral MemoryRead/MemoryRegionEnumeration path and the declarations below. Disassembly is supplied by a deterministic synthetic Mock provider used only for contract/Core regression testing; it is not an x86-64 decoder.

## Scanner Capability Declarations

Plugin `1.0.0.rev10` targets Plugin API `2.13.0` and supplies the same concrete Value Type definitions for the shared scanner: UInt8, Int8, UInt16, Int16, UInt32, Int32, UInt64, Int64, Float32, Float64, and ByteArray. The mock plugin currently reuses the optional standard definitions in the Plugin SDK.

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

All three enumerate as `Running` while the whole debugger target runs. Whole-target Pause maps ordinary rows to neutral `Stopped`; an individually suspended row remains `Suspended` across Pause/Continue until explicitly resumed. Per-thread Suspend/Resume is intentionally allowed only while the whole debugger session is `Running`, and the backend rejects unknown ids plus duplicate/invalid state transitions. Refresh re-enumerates the same fixture and lets the host verify selection preservation by neutral thread id. Rev7 adds deterministic 64-bit register snapshots for every Mock thread while Paused. The rows use neutral little-endian unsigned encoding, expose semantic frame/stack/instruction-pointer roles, and are writable so the shared Edit -> Write -> Refresh -> exact read-back verification flow can be tested safely. Register access is rejected while Running. Breakpoints/watchpoints, call stacks, and stepping remain deferred to their ordered `0.1.7` revisions. Disposing the target session disposes any active debugger session so a test attachment cannot outlive its in-memory target.

## Preservation Rule

The mock plugin should remain available throughout development. New generic subsystems should use it when practical before live-platform verification so target-independent regressions can be reproduced without console availability.
