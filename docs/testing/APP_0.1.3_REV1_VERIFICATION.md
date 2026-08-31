# 0.1.3.rev1 PS5 Value Type Expansion Verification

## Purpose

This document defines verification for **TeeKay87's Memory Engine 0.1.3.rev1 - PS5 Value Type Expansion**.

The revision starts from the fully verified `0.1.2.rev5` baseline and expands Exact Value scanning from signed Int32 to every scan value type exposed by current ps5debug-NG. No new scan comparison mode is introduced.

## Version Boundary

```text
Host:        0.1.3.rev1
Feature:     PS5 Value Type Expansion
Plugin API:  1.2.0
PS5 plugin:  0.1.0.rev9
Mock plugin: 1.0.0.rev1
```

Plugin API remains `1.2.0`. The public `MemoryValueType` identifiers and generic `NativeValueScanRequest` already contained the types and payload fields required for this revision.

## Supported Exact Value Types

Verify the Value Type selector contains exactly:

| UI label | Core type | ps5debug-NG wire id |
| --- | --- | ---: |
| 1 Byte (Unsigned) | `UInt8` | 0 |
| 1 Byte (Signed) | `Int8` | 1 |
| 2 Bytes (Unsigned) | `UInt16` | 2 |
| 2 Bytes (Signed) | `Int16` | 3 |
| 4 Bytes (Unsigned) | `UInt32` | 4 |
| 4 Bytes (Signed) | `Int32` | 5 |
| 8 Bytes (Unsigned) | `UInt64` | 6 |
| 8 Bytes (Signed) | `Int64` | 7 |
| Float | `Float32` | 8 |
| Double | `Float64` | 9 |
| Array of Bytes | `ByteArray` | 10 |

No string type should appear in the scanner selector. `Ascii`, `Utf8`, and `Utf16` remain reserved common model values and are not ps5debug-NG scan value types.

## Input Verification

Use representative input for each type:

```text
UInt8:          250
Int8:          -100
UInt16:         60000
Int16:         -12345
UInt32:         4000000000
Int32:          -123456789
UInt64:         18364758544493064720
Int64:          -1234567890123456789
Float:          123.25
Double:         -9876.5
Array of Bytes: DE AD BE EF 01
```

Integer types should also accept appropriately sized `0x`-prefixed hexadecimal forms. Array of Bytes should accept both spaced and compact hexadecimal input. Its current maximum is 4,096 bytes.

## Value Type Session Rules

1. before First Scan, confirm Value Type can be changed;
2. run a successful First Scan;
3. confirm Value Type becomes locked for that scan session;
4. run Next Scan with a new value of the same type;
5. choose New Scan;
6. confirm Value Type is editable again;
7. for Array of Bytes, changing the sequence width during an existing session must not silently reinterpret the candidate set; start New Scan before changing width.

The rev5 Enter/Primary workflow must remain unchanged.

## Deterministic Windows Verification

Rebuild the solution and run:

```powershell
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

Expected result: **19 checks pass**.

New coverage must include:

- shared Core parsing/scanning for all eleven ps5debug-NG value types;
- PS5 TurboScan wire-value-type mapping for ids `0..10`;
- Array of Bytes all-ones mask framing;
- dynamic native GET record width;
- Float/Double resident-refinement guard: the plugin must close the resident session and request Core fallback instead of using ps5debug-NG's fuzzy floating-point resident equality;
- all established rev5 protocol/cancellation/process-control regressions.

## PS5 Live Verification

On a physical PS5, use values that can be changed or identified reliably and verify representative members of each width/family:

1. 1-byte signed or unsigned First Scan and Next Scan;
2. 2-byte signed or unsigned First Scan and Next Scan;
3. 4-byte signed/unsigned First Scan and Next Scan;
4. 8-byte signed/unsigned First Scan and Next Scan;
5. Float First Scan through native TurboScan, followed by a strict shared-Core Next Scan fallback;
6. Double First Scan through native TurboScan, followed by a strict shared-Core Next Scan fallback;
7. exact Array of Bytes First Scan and Next Scan.

For each test, confirm:

- result addresses are plausible and remain inside the selected process map;
- the **Type** column matches the selected Value Type;
- **Current** shows the current requested value;
- after Next Scan, **Previous** shows the preceding scan value;
- native PS5 operations retain the established indeterminate progress/elapsed-time behavior; Float/Double Next Scan switches to determinate Core progress after the plugin requests strict-exact fallback;
- New Scan can start another value type without reconnecting.

## Array of Bytes Boundary

Current Array of Bytes behavior is intentionally exact-only:

```text
value bytes: DE AD BE EF
mask bytes:  01 01 01 01
```

Do not treat `??`, `*`, or other wildcard syntax as supported in this revision. Masked/wildcard AOB scanning requires its own UI/parser semantics later.

## Raw Memory Write Regression

Raw Memory Write remains a signed Int32 diagnostic.

- Scan Results -> **Change value** must remain enabled for `Int32` results.
- It must be disabled for all other value types so selecting a wider/floating/unsigned/byte-array result cannot be reinterpreted as an Int32 write.
- Raw Memory Write's existing known-address behavior must otherwise remain unchanged.

## Pass Criteria

```text
Windows rebuild:                                  PENDING USER VERIFICATION
Verification executable (19/19):                  PENDING USER VERIFICATION
Value Type selector contains exactly 11 types:    PENDING USER VERIFICATION
Value Type locks after First Scan:                PENDING USER VERIFICATION
Value Type unlocks after New Scan:                PENDING USER VERIFICATION
Integer widths live PS5 scan/refinement:          PENDING USER VERIFICATION
Float native First/Core Next Scan:               PENDING USER VERIFICATION
Double native First/Core Next Scan:                PENDING USER VERIFICATION
Array of Bytes live PS5 scan/refinement:          PENDING USER VERIFICATION
Int32-only Change value guard:                    PENDING USER VERIFICATION
Established rev5 workflow regression:             PENDING USER VERIFICATION
