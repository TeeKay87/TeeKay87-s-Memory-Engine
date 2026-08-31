# In-Memory Test Target Plugin

## Purpose

The In-Memory Test Target is the deterministic development plugin used by TeeKay87's Memory Engine to exercise platform-neutral Core and Plugin SDK behavior without requiring a physical target.

This directory contains documentation that belongs specifically to the mock plugin. General Plugin SDK architecture remains under `docs/architecture/`.

## Plugin Identity

| Property | Value |
| --- | --- |
| Plugin id | `platform.mock.in-memory` |
| Plugin version | `1.0.0.rev1` |
| Plugin API | `1.0.0` |
| Platform | Development |
| Backend | In-Memory |
| Architecture | x64, 64-bit pointers, little-endian |

The plugin version and revision are independent from the TeeKay87's Memory Engine host application version.

## Current Capabilities

The mock plugin currently advertises:

- target connection;
- process enumeration;
- foreground-process discovery;
- memory-region enumeration;
- memory read;
- memory write.

It intentionally does not advertise debugger, scanner, assembler, disassembler, pointer-scanner, or cheat capabilities that have not been implemented.

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

These values are development fixtures. They are intended to support deterministic scanner, saved-address, freeze, memory-viewer, export, and regression tests as those shared subsystems are implemented.

## Preservation Rule

The mock plugin should remain available throughout development. New generic subsystems should use it when practical before live-platform verification so target-independent regressions can be reproduced without console availability.
