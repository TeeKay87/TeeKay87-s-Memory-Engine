# Combined 0.1.1.rev11 + 0.1.1.rev12 Live PS5 Verification

## Purpose

This document records the completed live-console verification for **0.1.1.rev11 - PS5 Memory Map Enumeration** and **0.1.1.rev12 - PS5 Raw Memory Read** after the rev13 compile fix and rev14 WPF binding fix made the complete path runnable.

The combined test was deliberately used to prove that the memory map is not only returned as a count, but is usable by the generic host to validate and perform a real memory read.

## Environment

- TeeKay87's Memory Engine host baseline: `0.1.1.rev14`
- PlayStation 5 plugin: `0.1.0.rev4`
- Plugin API: `1.0.0`
- Target: real PlayStation 5 running ps5debug-NG
- Verification date: 2026-08-30

## Completed Checks

The Windows/live-target verification was reported as completing exactly as specified in the rev11/rev12 procedure:

1. the application built and started successfully after the rev13/rev14 corrections;
2. the PS5 connection succeeded and the real process list loaded;
3. a real game process could be set as Active Target;
4. memory-map enumeration returned a non-zero region count;
5. the host cached the returned neutral memory regions without a memory-map error;
6. Raw Memory Read automatically selected a suitable readable mapped address;
7. the default read length remained 64 bytes;
8. the 64-byte read completed and produced the expected hexadecimal/ASCII result;
9. repeating the same read succeeded without corrupting or disconnecting the session;
10. an invalid/unmapped request was rejected by the host before the plugin memory-read call;
11. refreshing processes retained the same Active Target when it remained present, refreshed its memory map, and allowed another successful read;
12. disconnect cleared the process/Active Target/memory-map/raw-read state without an exception.

## Result

```text
rev11 live PS5 memory map:      PASS
rev12 live PS5 raw memory read: PASS
```

The read-only target-access chain is therefore live-verified through:

```text
Connect
  -> Process Enumeration
  -> Active Target
  -> Memory Map
  -> Readable-range validation
  -> Raw Memory Read
```

PS5 raw memory write and generic read-back verification are implemented in `0.1.1.rev16` through the existing neutral `IMemoryWriter` contract. That revision now requires its own controlled real-console write/read-back/restore verification before the low-level read/write target-access chain is considered live-verified end to end.
