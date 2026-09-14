# Application 0.1.7.rev34 Source Review — Debugger Finalization Verification Test Corrections

## Baseline

Rev34 is built directly from the supplied `0.1.7.rev33` debugger-finalization candidate after its first Windows automated verification runs. Rev31 remains the latest fully verified live-PS5 teardown baseline. Rev32 and rev33 runtime acceptance remains pending.

## Findings from the first rev33 automated runs

Two failures reproduced across both runs and were traced to the new rev33 test assertions rather than product behavior.

### Snapshot JSON address-width validation

The negative test changed `addressWidth` from 64 to 32 while the fixture's addresses remained at or below `0x70000000`. Those addresses are valid 32-bit values, so `DebuggerSnapshotJsonSerializer` correctly accepted the document. Rev34 changes the negative fixture to 28 bits. The existing `0x10000000` code address then exceeds the declared maximum `0x0FFFFFFF`, exercising the importer's already implemented range rejection without changing serializer/runtime code.

### Snapshot comparer register lookup

The neutral snapshot fixture stores the canonical register identifier as lowercase `r12`. The rev33 assertion used case-sensitive `item.Name == "R12"`, so LINQ `Single(...)` failed even though the comparer had emitted the expected register row. The promoted-summary lookup contained the same casing assumption. Rev34 uses ordinal case-insensitive matching for both assertions, consistent with register lookup behavior elsewhere in the comparer.

## Intermittent PS5 register observation

The first complete rev33 run passed `PS5 debugger general register snapshot protocol`. The second returned 28 registers rather than 76. The existing PS5 implementation produces 28 registers when general registers and FS/GS are available but the optional FPU/SIMD probe returns unavailable; that exact fallback is intentionally covered by `PS5 debugger optional register timeout isolation`.

Rev33 did not modify the PS5 register transport/mapper, and the failure did not reproduce consistently. Rev34 therefore does not alter production timeout/fallback behavior. The full corrected suite must be rerun. A repeated 28-register failure in the normal register-snapshot test is a blocker and should be investigated separately before runtime acceptance.

## Changed files

- `src/TeeKay87.MemoryEngine.App/Application/AppInfo.cs`
- `tests/TeeKay87.MemoryEngine.Tests/Program.cs`
- `README.md`
- `CHANGELOG.md`
- `docs/TeeKay87_Memory_Engine_Full_Development_Action_Plan.md`
- `docs/testing/APP_0.1.7_REV34_SOURCE_REVIEW.md`
- `docs/testing/APP_0.1.7_REV34_VERIFICATION.md`

No Core snapshot serializer/comparer implementation, Plugin SDK contract, Mock plugin implementation, or PS5 plugin implementation is changed by rev34.

## Verification gate

The authoritative next step is a clean Windows build followed by the complete **152-check** suite. Runtime Gate 2 must not begin until all 152 checks pass.
