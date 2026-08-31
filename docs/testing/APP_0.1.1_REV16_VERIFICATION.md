# 0.1.1.rev16 PS5 Raw Memory Write and Read-Back Verification

## Purpose

This document records release-preparation and runtime verification requirements for **TeeKay87's Memory Engine 0.1.1.rev16 - PS5 Raw Memory Write and Read-Back**.

The revision completes the implementation side of the initial PS5 low-level target-access chain by adding raw process-memory writes behind the existing neutral `IMemoryWriter` contract and adding generic host-side read-back verification.

## Verified Starting Baseline

The revision is based directly on the user-supplied complete **0.1.1.rev15 - TextBox Content Padding Fix** source ZIP. Before rev16 work began, the following runtime state was confirmed:

- rev15 standard TextBox presentation: PASS;
- real PS5 connection: PASS;
- real PS5 process enumeration: PASS;
- rev11 real PS5 memory-map enumeration: PASS;
- rev12 real PS5 raw-memory read: PASS;
- demonstrated Active Target `eboot.bin`: 8,750 memory regions;
- demonstrated raw read: 64 bytes at `0x400000`.

These previously verified paths are not reimplemented by rev16.

## Version Boundaries

```text
Host application:  0.1.1.rev16
PS5 plugin:        0.1.0.rev5
Mock plugin:       1.0.0.rev1
Plugin API:        1.0.0
```

The PS5 plugin revision advances because its backend/session/capability implementation changes. The Plugin API remains `1.0.0` because `IMemoryWriter` and `MemoryWrite` already existed in the public contract. The Mock plugin remains unchanged because it already implements deterministic memory writing.

## Added Implementation

### PS5 backend

- `CMD_PROC_WRITE` (`0xBDAA0003`) mapping;
- packed PID/address/length request serialization;
- first server success acknowledgement before payload transmission;
- exact raw payload transmission;
- final server success acknowledgement after payload transmission;
- `IMemoryWriter` exposure from `Ps5TargetSession`;
- `MemoryWrite` capability advertisement;
- PS5 plugin revision `0.1.0.rev5`.

### Generic WPF host

- capability-driven **Raw Memory Write** inspector;
- hexadecimal address parsing;
- hexadecimal byte-data parsing;
- 4096-byte interactive write limit;
- neutral writable-region validation through cached `MemoryRegion` data;
- original-byte capture when read-back is possible;
- post-write read-back through `IMemoryReader`;
- byte-for-byte PASS/FAIL verification output;
- mutually exclusive read/write/refresh/target-change command state while a write is active;
- disconnect/process/target cleanup for write UI state.

### Deterministic tests

- loopback protocol handling for `CMD_PROC_WRITE`;
- verification of both write success statuses;
- exact transmitted payload capture;
- immediate same-stream read-back of the written bytes;
- PS5 metadata/capability/service assertions updated for plugin rev5;
- verification executable expanded to 11 checks.

## Preservation Requirements

Release preparation must confirm that rev16 does not disturb the established behavior of:

- the 34-unit standard interactive-control height;
- rev15 TextBox padding/content-host fix;
- rev14 OneWay Raw Memory Read result binding;
- themes and theme ids;
- splitter behavior/ranges;
- plugin discovery and API compatibility rules;
- Active Target separation from Selected Process;
- PS5 connection/handshake;
- process enumeration;
- memory-map enumeration;
- raw-memory reads;
- Mock plugin layout/read/write behavior;
- Plugin SDK `1.0.0` contract.

## Static Release Verification

The source tree must be checked for:

- correct host/PS5/Mock/API version separation;
- `CMD_PROC_WRITE = 0xBDAA0003`;
- packed 16-byte request layout;
- two distinct write success-status reads;
- `Ps5TargetSession : ... IMemoryWriter`;
- PS5 capability set includes `MemoryWrite` but no unrelated unimplemented capabilities;
- host write UI is gated by `SupportsMemoryWrite`, not a platform-name check;
- writable-range checks use neutral `MemoryProtection.Write`;
- output-only `MemoryWriteResultText` binding is explicitly `Mode=OneWay`;
- all XAML/project XML and theme JSON parse successfully;
- local Markdown links resolve;
- no `bin`, `obj`, `.vs`, editor artifacts, or nested release ZIPs are included.

A .NET/WPF compiler is not available in the release-preparation environment used for this revision. Static validation cannot replace the required Windows build and runtime test.

## Preparation Result

Release preparation completed **484/484 static checks successfully**. The checks covered version-domain separation, the `CMD_PROC_WRITE` id and packed request path, both required write acknowledgements, `IMemoryWriter` session/capability exposure, generic writable-range/read-back host integration, explicit OneWay output binding, deterministic write/read-back fixture coverage, preservation of rev14/rev15 UI invariants, XML/XAML/project and theme-JSON parsing, solution/project-reference resolution, local Markdown links, intended-file diff boundaries, byte-for-byte preservation of unrelated verified Core/Plugin SDK/Mock/theme/splitter/style files, release-tree cleanliness, and lightweight delimiter-balance checks across all C# sources.

No native .NET/WPF compile result is claimed from this environment.

## Required Windows and Live Verification

1. **Build -> Rebuild Solution** with zero compile errors.
2. Run `TeeKay87.MemoryEngine.Tests` and confirm all 11 checks pass.
3. Confirm rev15 TextBox rendering remains correct.
4. Connect to a real PS5 and reconfirm process/map/read behavior.
5. When testing the original rev16 UI, use the controlled manual procedure documented for that baseline. When testing rev17 or later, use the preferred **Safe Write Test** procedure in `../plugins/PS5/MEMORY_WRITE_VERIFICATION.md`, which removes manual writable-address discovery and deliberate value mutation.
6. Confirm the chosen write path reports `Verification: PASS`.
7. Confirm a non-writable/unmapped range is blocked by the host and the connection remains alive.
8. Disconnect and confirm read/write/map/target state clears correctly.

## Runtime Status

At release preparation time, the rev16 implementation has deterministic protocol/read-back coverage but the controlled real-console write/read-back/restore test has not yet been performed.

**rev16 live PS5 raw-memory-write verification: PENDING.**
