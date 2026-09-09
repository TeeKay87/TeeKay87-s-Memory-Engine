# TeeKay87's Memory Engine 0.1.3.rev3 Verification

## Purpose

This checklist verifies the plugin-owned Value Type and Scan Type architecture introduced in `0.1.3.rev3` and the disabled-control presentation carried forward from the unverified `0.1.3.rev2` baseline.

`0.1.3.rev3` replaces the rev2 Core-catalog model before that model was runtime-verified. Core now knows only generic scanner contracts and orchestration. Each platform plugin supplies the concrete `IMemoryValueType` and `IMemoryScanType` definition objects that it can execute, and the Scan panel is populated directly from those objects.

This revision does **not** add a new PS5 scan algorithm. The current PlayStation 5 plugin still exposes the same eleven value representations and Exact Value behavior already implemented before rev3.

## Expected identities

Before functional testing, confirm the following versions are shown by the application/plugin metadata:

| Component | Expected version |
| --- | --- |
| TeeKay87's Memory Engine | `0.1.3.rev3` |
| Plugin API | `2.0.0` |
| PlayStation 5 plugin | `0.1.0.rev11` |
| In-Memory Test Target plugin | `1.0.0.rev3` |

## 1. Build and automated verification

From the repository root on Windows:

```powershell
dotnet build .\TeeKay87.MemoryEngine.sln -c Release
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Required result:

```text
All 22 checks passed.
```

The automated suite must verify at least:

- Plugin API `2.0.0` and independent plugin versions;
- the plugin-owned `IMemoryValueType` and `IMemoryScanType` contracts;
- absence of the removed Core `MemoryValueTypeCatalog`, `MemoryScanTypeCatalog`, and concrete scan-value codec registry;
- successful Core scanning with a test-only `test.byte` Value Type and `test.equals` Scan Type that are not registered in Core or in an SDK master catalog;
- PS5 and Mock plugin scanner-definition declarations;
- the existing mock target process/memory behavior;
- PS5 connection, process, memory-map, raw read/write, TurboScan mapping/refinement, cancellation, suspend/resume, and plugin discovery behavior.

A build failure or any failed verification check blocks revision acceptance.

## 2. Plugin-owned PS5 selectors

Connect to the PlayStation 5 plugin and select an Active Target.

### Value Type

The Value Type selector must contain exactly the eleven concrete definitions supplied by the PS5 plugin:

1. `1 Byte (Unsigned)`
2. `1 Byte (Signed)`
3. `2 Bytes (Unsigned)`
4. `2 Bytes (Signed)`
5. `4 Bytes (Unsigned)`
6. `4 Bytes (Signed)`
7. `8 Bytes (Unsigned)`
8. `8 Bytes (Signed)`
9. `Float`
10. `Double`
11. `Array of Bytes`

No additional host/Core type may appear. Core has no concrete Value Type catalog from which an unsupported item could be added.

### Scan Type

The Scan Type selector must contain only:

```text
Exact Value
```

No additional host/Core Scan Type may appear. Because the PS5 plugin supplies only one applicable Scan Type, the selector must be disabled and visually look disabled.

## 3. Scan-selector lifecycle

With the PS5 plugin active:

1. Before First Scan, Value Type must be selectable because the plugin supplies multiple Value Types.
2. Scan Type must be disabled because only Exact Value is available.
3. Run a successful First Scan.
4. Value Type must become locked for that scan session.
5. Scan Type must remain disabled for PS5 because Exact Value is still its only applicable Next Scan choice.
6. Run a Next Scan and verify the selected Value Type remains unchanged.
7. Choose **New Scan**.
8. Value Type must become selectable again.
9. Scan Type must still remain disabled because it still has only one applicable First Scan definition.

Value Type locking and Scan Type availability are separate rules. A future plugin may supply different First Scan and Next Scan definitions without requiring a Core catalog change.

Also verify with a development/custom plugin definition set that if one Value Type has no compatible Scan Type, the Value Type selector remains available before First Scan so another supported Value Type can be selected; the UI must not become trapped by the incompatible selection.

## 4. Disabled-state presentation carried from rev2

Because rev2 was not runtime-verified before rev3 replaced its scanner-catalog architecture, repeat the disabled-state visual checks in **Light**, **Dimmed**, and **Dark** themes.

Verify at least:

- a disabled ordinary button uses a visibly lower-contrast surface and dimmed text;
- the single-choice Scan Type ComboBox looks disabled;
- a disabled Value Type ComboBox after First Scan looks disabled;
- **Pause target while scanning** is visibly dimmed whenever present but unavailable;
- on a non-Int32 scan result, Scan Results -> **Change value** is visibly disabled and cannot be invoked;
- on a `4 Bytes (Signed)` / Int32 result, **Change value** remains visibly enabled and usable;
- enabled controls retain their normal hover/focus presentation.

The styling change must not alter command behavior.

## 5. Representative PS5 scanner regression

After the automated protocol tests pass, run a representative live PS5 regression:

1. New Scan -> `4 Bytes (Signed)` -> Exact Value.
2. Search a known value with First Scan.
3. Change or retain the target value as appropriate.
4. Run Next Scan.
5. Confirm results refine correctly and Current/Previous remain correct.
6. Confirm Enter in the Value field still invokes the highlighted First/Next Scan action.
7. Confirm New Scan resets the session without reconnecting.

A Float or Double First/Next Scan may also be repeated to reconfirm the established native-First/Core-refinement fallback path; rev3 does not intentionally change that path.

## 6. Existing workflow regression

Confirm the previously verified behavior remains intact:

- First Scan / Next Scan highlighting;
- Cancel Scan leaves the PS5 command stream/session reusable;
- **Pause target while scanning** remains Off by default and the process resumes after success, cancellation, or failure;
- preferred `eboot.bin` target behavior;
- Raw Memory Read;
- Raw Memory Write and Safe Write Test read-back verification.

## 7. Known unchanged boundaries

The following are **not failures of 0.1.3.rev3**:

- the current 2,000,000-result scan safety limit remains in place;
- the UI still displays at most the first 50,000 candidates;
- the `0.1.3.rev1` live 1-byte scan returned 16,211,407 native matches and was blocked by the existing result safety limit;
- live Array-of-Bytes verification remains deferred until there is a practical way to select a known byte sequence;
- disk-backed massive-result storage, configurable scan-result storage, stale-session cleanup, and the reusable modal progress dialog remain future work;
- PS5 Scan-panel options for Endianness, Alignment, and Floating-point rounding remain future plugin work.

## Acceptance criteria

`0.1.3.rev3` can be accepted when all of the following are true:

| Verification | Required result |
| --- | --- |
| Release build | PASS |
| Automated verification | 22/22 PASS |
| Application/plugin/API identities | PASS |
| Core has no concrete Value Type/Scan Type registry dependency | PASS |
| Custom test-only plugin definitions scan successfully through Core | PASS |
| PS5 Value Type list contains exactly its 11 supplied definitions | PASS |
| PS5 Scan Type list contains only Exact Value | PASS |
| Value Type locking / Scan Type stage filtering / New Scan lifecycle | PASS |
| Disabled-state styling in all three themes | PASS |
| Int32-only Change value behavior remains correct | PASS |
| Representative PS5 First/Next Scan | PASS |
| Existing workflow regression | PASS |
