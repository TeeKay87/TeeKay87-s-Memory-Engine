# TeeKay87's Memory Engine 0.1.3.rev13 Verification

## Revision Under Test

```text
Host application:             0.1.3.rev13
Feature:                      Disk-Backed Massive Scan Results
PlayStation 5 plugin:         0.1.0.rev13
In-Memory Test Target plugin: 1.0.0.rev3
Plugin API:                   2.2.0
```

## Purpose

Rev13 connects the scan-result storage foundation to the scanner. The acceptance target is to retain complete very large result sets without materializing millions of WPF/runtime result objects and to ensure Next Scan always refines the complete previous set.

The primary live regression case remains:

```text
Platform:            PS5
Value Type:          1 Byte (Signed)
Value:               50
Observed native set: 16,211,407 matches
Old host limit:      2,000,000
```

The old two-million limit is retained only for legacy in-memory/list materialization paths. It must no longer reject the rev13 disk-backed streaming path.

## Build Verification

On Windows with .NET 9 SDK:

```powershell
dotnet clean TeeKay87.MemoryEngine.sln
dotnet build TeeKay87.MemoryEngine.sln -c Release
```

Required result:

- zero compiler errors;
- zero warnings (warnings are treated as errors);
- App, Core, Plugin SDK, Mock plugin, PS5 plugin, and Tests all build;
- WPF Settings/progress XAML continues to compile;
- plugin output is copied to the runtime `Plugins` directory as before.

## Automated Verification

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Required final line:

```text
All 33 checks passed.
```

### New rev13 checks

#### Disk-backed scan-result generation commits

The check:

1. creates a scan session;
2. writes/commits generation 1;
3. opens and validates generation 1;
4. starts a replacement generation and disposes it without commit;
5. confirms generation 1 is still active;
6. writes/commits generation 2;
7. confirms generation 2 becomes the active result set.

This guards the rule that incomplete Next Scan output cannot replace the last valid committed generation.

#### Disk-backed Next Scan refines hidden results

The deterministic Mock target contains 65,536 bytes. The check performs a one-byte First Scan for zero with a `50,000` preview ceiling, then selects an address at offset 60,000 that is outside the materialized preview. It changes that byte to `255` and runs Next Scan for `255` against the complete disk-backed result set.

Required result:

```text
First total:   > 50,000
First preview: 50,000
Hidden address visible in preview: no
Next total:    1
Next survivor: hidden address
```

This directly proves Next Scan does not use only the WPF rows.

#### Disk-backed native stream exceeds the legacy two-million limit

A synthetic native stream produces exactly:

```text
2,000,001 records
```

in 65,536-record batches while Core is asked to materialize only 32 preview rows.

Required result:

- no `ScanResultLimitExceededException`;
- total count is 2,000,001;
- preview count is 32;
- writer commits all 2,000,001 records;
- `IScanResultSet.Count` is 2,000,001;
- address-batch enumeration reads all 2,000,001 records;
- final stored address matches the synthetic source.

This is the deterministic application-level regression guard for removing the two-million limit from the disk-backed streaming path.

### Extended PS5 service checks

The existing protocol tests also verify:

- `INativeValueScanStreamProvider` and `INativeValueScanStreamRefiner` are hidden when TurboScan capabilities are absent;
- both streaming services are exposed when the test server advertises the required TurboScan capability;
- existing legacy native interfaces remain available for compatibility;
- existing TurboScan protocol/cancellation/refinement tests remain PASS.

## Storage File Verification

After a successful First Scan, inspect the active scan-session directory under the configured storage root while the application is running.

Expected structure:

```text
<application-session-guid>\
    session.json
    <scan-session-guid>\
        scan.json
        results-00000001.bin
```

Confirm `scan.json` is `Committed` and includes:

- result count;
- active result filename;
- result generation;
- result value size;
- result alignment;
- plugin/target/value/scan identity.

After a successful Next Scan, confirm a later generation is referenced. The previous result file should be removed when possible after the new generation is committed.

## UI Verification

### Bounded result display

Run a scan with more than 50,000 survivors.

Required UI behavior:

- true total count is displayed;
- only the first 50,000 result rows are materialized;
- footer states that the first 50,000 of the complete count are shown;
- scrolling/result interaction remains responsive;
- First/Next/New Scan button behavior remains correct.

### Large native commit progress

For a native result set larger than 50,000, confirm the reusable modal dialog appears during transfer/commit.

Required behavior:

- title is `Saving Scan Results`;
- status reports disk writing;
- detail reports stored and received/total counts;
- progress advances as GET batches are processed;
- main window is not interactable while modal;
- Cancel requests real scan cancellation and the dialog stays open until the operation reaches a safe cancellation boundary;
- theme styling is correct in Light, Dimmed, and Dark.

## Next Scan Complete-Set Verification

For a large First Scan where most results are not shown:

1. record the total count;
2. choose/refine a value expected to retain results beyond the first 50,000;
3. run Next Scan;
4. confirm the result can survive even if its address was absent from the First Scan preview;
5. confirm the complete prior result count, not `50,000`, is used as the Next Scan candidate count/progress basis.

The automated Mock test covers this deterministically; the Windows UI test confirms host integration.

## Cancellation and Failure Verification

### First Scan cancellation during result transfer

Cancel while a large native result set is being received/written.

Required:

- no partial result generation becomes committed;
- scan returns to valid New/First Scan state;
- target is resumed if Pause target while scanning was enabled;
- PS5 command stream remains reusable after safe protocol drain/cleanup;
- a subsequent scan can start without reconnecting solely because of cancellation.

### Next Scan cancellation

Start from a committed First Scan, begin a large Next Scan, then cancel before replacement commit.

Required:

- previous committed generation remains active;
- its result count remains unchanged;
- retrying Next Scan can use the previous complete set;
- partial replacement files are deleted when possible and are never referenced by committed metadata.

### Simulated incomplete generation

Create/interfere with a temporary/unreferenced result generation and restart the host.

Required:

- new application-session GUID is created;
- old application session is stale;
- incomplete/unreferenced file is never adopted;
- startup cleanup may remove the old managed directory;
- failure to delete it does not affect correctness.

## PS5 Live Massive-Result Test

Use the known real-console case when practical:

```text
Value Type: 1 Byte (Signed)
Scan Type:  Exact Value
Value:      50
```

Earlier result:

```text
16,211,407 matches
```

Rev13 acceptance requires:

- scan is not rejected because it exceeds 2,000,000;
- total count is retained exactly as reported by the accepted native stream after host filtering;
- UI shows no more than 50,000 rows;
- complete set is committed to disk;
- modal transfer progress behaves correctly;
- Next Scan operates on the complete set;
- New Scan invalidates/removes the session best effort;
- normal exit cleans the application session best effort.

If the live count differs because target/game memory changed, any real PS5 case above two million is acceptable for the scalability boundary, but record the exact value/count used.

## Plugin Architecture Regression

Confirm rev13 did not move PS5-specific behavior into Core:

- concrete Value Types remain plugin-owned;
- concrete Scan Types remain plugin-owned;
- Endianness, Alignment, Floating-point rounding remain PS5 plugin declarations;
- Pause target while scanning remains capability-driven;
- Core does not branch on concrete PS5 Value Type ids for storage decisions;
- storage root/lifecycle/progress UI remain host responsibilities.

## Preparation-Environment Verification Record

The rev13 package preparation environment does not contain `dotnet`, `csc`, `msbuild`, or a .NET 9 SDK. Native compilation and execution of the C# verification executable therefore cannot be truthfully recorded as completed there.

Repository-level checks performed before packaging include:

- full source/documentation inventory review;
- version-domain consistency checks (`0.1.3.rev13`, Plugin API `2.2.0`, PS5 `0.1.0.rev13`, Mock `1.0.0.rev3`);
- confirmation that the legacy 2,000,000 constant remains in the legacy Core/list path while rev13 disk-backed/streaming code contains no equivalent application-level rejection;
- confirmation that the WPF 50,000 preview ceiling remains separate from total stored count;
- changed-file review against rev12;
- targeted review of all new disk-backed, streaming-native, PS5 TurboScan, and WPF integration paths for nullable/accessibility/signature issues and obsolete or contradictory fallbacks;
- confirmation that disk-backed Next Scan uses only the streaming refiner as its native path while the legacy list refiner remains only for the in-memory compatibility path;
- confirmation that legacy materialized native results must be complete and are address-ordered before disk persistence;
- confirmation that incompatible PS5 native refinement releases resident TurboScan state before shared fallback;
- confirmation that the obsolete metadata-only scan-session commit path was removed, so `Committed` now always represents a published binary result generation;
- confirmation that storage-unavailable fallback cannot advertise a stream-only native path that the legacy in-memory fallback cannot actually consume;
- XAML/XML and JSON parse validation;
- package integrity/hash verification after ZIP creation.

## Completion Status

Do **not** designate rev13 as the verified runtime baseline until all of the following are recorded:

1. Windows Release build succeeds with zero warnings/errors;
2. verification executable reports `All 33 checks passed.`;
3. manual bounded-preview/full-set Next Scan behavior passes;
4. large-result modal/cancellation behavior passes;
5. a real PS5 >2,000,000 result scan succeeds and can be refined, preferably the known 16,211,407-result case.

## Actual Windows Build Result

The first Windows build attempt for the packaged rev13 source did **not** complete successfully. Visual Studio reported:

```text
CS8425  Async-iterator 'Program.SyntheticNativeValueScanResultStream.ReadBatchesAsync(CancellationToken)' has one or more parameters of type 'CancellationToken' but none of them is decorated with the 'EnumeratorCancellation' attribute, so the cancellation token parameter from the generated 'IAsyncEnumerable<>.GetAsyncEnumerator' will be unconsumed.
```

The solution uses `TreatWarningsAsErrors=true`, so this compiler diagnostic blocks the verification executable and therefore prevents rev13 from becoming the verified baseline. Visual Studio also showed a `System.Object` XAML-designer error in `MainWindow.xaml`; no evidence currently identifies an independent MainWindow defect, so that item is treated as downstream until the build-blocking test-project error is corrected.

The missing async-iterator cancellation annotation is corrected in **0.1.3.rev14 - Native Stream Enumerator Cancellation Compile Fix**. Rev13 remains unverified.
