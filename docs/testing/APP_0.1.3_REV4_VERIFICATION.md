# TeeKay87's Memory Engine 0.1.3.rev4 Verification

## Purpose

This checklist verifies the incremental source-package compile fix in `0.1.3.rev4` and reconfirms the plugin-owned scanner architecture introduced in rev3.

Rev3 removed six rev2 source files when the Core-catalog model was replaced by plugin-owned `IMemoryValueType` and `IMemoryScanType` definitions. A clean rev3 tree did not contain those files, but extracting rev3 over an existing rev2 working directory did not delete the old files. Because SDK-style projects compile matching source files automatically, a stale `Core.Scanning.MemoryScanValue` could remain in the project and shadow the new `PluginSdk.Models.MemoryScanValue`, causing the Core build to fail.

Rev4 keeps the obsolete source paths as inert upgrade tombstones. They contain comments only and define no classes, enums, catalogs, or codecs. This preserves the rev3 architecture while making an incremental archive extraction overwrite the obsolete rev2 implementations.

## Expected identities

| Component | Expected version |
| --- | --- |
| TeeKay87's Memory Engine | `0.1.3.rev4` |
| Plugin API | `2.0.0` |
| PlayStation 5 plugin | `0.1.0.rev11` |
| In-Memory Test Target plugin | `1.0.0.rev3` |

The plugin versions do not change because rev4 changes host/source-package migration safety only.

## 1. Incremental-upgrade source check

For the strongest reproduction of the reported failure:

1. Extract `0.1.3.rev2` into an empty temporary directory.
2. Extract `0.1.3.rev4` over that same directory and allow files to be overwritten.
3. Inspect these six paths:

```text
src/TeeKay87.MemoryEngine.Core/Scanning/MemoryScanValue.cs
src/TeeKay87.MemoryEngine.Core/Scanning/MemoryScanValueCodec.cs
src/TeeKay87.MemoryEngine.Core/Scanning/MemoryValueTypeCatalog.cs
src/TeeKay87.MemoryEngine.Core/Scanning/MemoryScanTypeCatalog.cs
src/TeeKay87.MemoryEngine.PluginSdk/Models/MemoryValueType.cs
src/TeeKay87.MemoryEngine.PluginSdk/Models/MemoryScanType.cs
```

Each must be an inert tombstone and must define no type.

There must be exactly one real `MemoryScanValue` implementation in the resulting source tree:

```text
src/TeeKay87.MemoryEngine.PluginSdk/Models/MemoryScanValue.cs
```

## 2. Clean and build

When testing inside an existing Visual Studio working directory, perform a clean rebuild so stale build products do not obscure the source result:

```powershell
dotnet clean .\TeeKay87.MemoryEngine.sln -c Release
dotnet build .\TeeKay87.MemoryEngine.sln -c Release
```

Required result: zero build errors.

The following rev3 failure signatures must be absent:

- CS0029 conversion between `TeeKay87.MemoryEngine.PluginSdk.Models.MemoryScanValue` and `TeeKay87.MemoryEngine.Core.Scanning.MemoryScanValue`;
- CS1503 arguments mixing the two `MemoryScanValue` types;
- CS1061 reporting that `MemoryScanValue` has no `ValueTypeId` member;
- downstream CS0006 missing `TeeKay87.MemoryEngine.Core.dll` errors caused by the failed Core build.

XAML designer errors that were downstream of a missing Core assembly should also disappear after the solution builds successfully.

## 3. Automated verification

Run:

```powershell
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Required result:

```text
All 22 checks passed.
```

The plugin-owned scan-definition contract check now explicitly verifies that these removed rev2 runtime types do not exist in compiled assemblies:

- `TeeKay87.MemoryEngine.Core.Scanning.MemoryScanValue`;
- `TeeKay87.MemoryEngine.Core.Scanning.MemoryValueTypeCatalog`;
- `TeeKay87.MemoryEngine.Core.Scanning.MemoryScanTypeCatalog`;
- `TeeKay87.MemoryEngine.Core.Scanning.MemoryScanValueCodec`;
- `TeeKay87.MemoryEngine.PluginSdk.Models.MemoryValueType`;
- `TeeKay87.MemoryEngine.PluginSdk.Models.MemoryScanType`.

It must still prove that Core can execute the test-only `test.byte` Value Type and `test.equals` Scan Type without any Core catalog registration.

## 4. Rev3 architecture regression

After the build and automated suite pass, confirm that the active PS5 plugin still supplies exactly:

### Value Types

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

### Scan Types

```text
Exact Value
```

Core must not add any concrete type that the plugin did not provide.

## 5. UI and live regression

The rev4 compile fix must not alter runtime behavior. Reconfirm at least:

- disabled controls remain visibly dimmed in the active theme;
- the PS5 Scan Type selector remains disabled while Exact Value is its only applicable choice;
- Value Type locks after First Scan and unlocks after New Scan;
- non-Int32 results keep **Change value** disabled while `4 Bytes (Signed)` keeps it enabled;
- one representative PS5 Exact Value First Scan and Next Scan succeeds;
- Cancel Scan, process pause/resume, Raw Memory Read/Write, and Safe Write behavior remain unchanged if a full regression is being performed.

## Known unchanged boundaries

The following remain unchanged from rev3 and are not rev4 failures:

- the 2,000,000-result safety limit;
- the 50,000-row UI presentation cap;
- 1-byte full scans may exceed the current result safety limit;
- live Array-of-Bytes verification remains deferred until a convenient known byte sequence is available;
- disk-backed massive-result storage and its Settings path are future work;
- the reusable modal progress dialog is future work;
- PS5 Scan-panel options for Endianness, Alignment, and Floating-point rounding are future work.

## Acceptance criteria

| Verification | Required result |
| --- | --- |
| Rev2 -> rev4 incremental extraction overwrites all six obsolete source implementations | PASS |
| Release build | PASS |
| Automated verification | 22/22 PASS |
| Application/plugin/API identities | PASS |
| No obsolete rev2 Core/SDK scanner types in compiled assemblies | PASS |
| Plugin-owned Value Type/Scan Type selectors remain correct | PASS |
| Disabled-state styling remains correct | PASS |
| Representative PS5 First/Next Scan | PASS |
