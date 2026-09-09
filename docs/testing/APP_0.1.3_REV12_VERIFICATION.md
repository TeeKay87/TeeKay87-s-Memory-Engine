# TeeKay87's Memory Engine 0.1.3.rev12 Verification

## Revision Under Test

```text
Host application:             0.1.3.rev12
Feature:                      Settings Storage Panel Auto Height Fix
PlayStation 5 plugin:         0.1.0.rev12
In-Memory Test Target plugin: 1.0.0.rev3
Plugin API:                   2.1.0
```

This revision is a narrowly scoped WPF layout correction for the application-level Settings surface introduced by the Scan Result Storage Foundation. It does not change settings persistence, storage lifecycle behavior, scanner behavior, plugin contracts, or disk-backed massive-result functionality.

## Defect Corrected

Rev11 successfully built and launched on the Windows development machine, and its WPF accessibility correction allowed the Settings window to open. Runtime inspection then showed that the **Scan Results Storage Location** card could clip its final wrapped explanatory text.

The cause was the combination of:

```xml
Height="390"
```

on `SettingsWindow` and a star-sized main content row:

```xml
<RowDefinition Height="*" />
```

The storage card's inner `StackPanel` correctly measured its wrapped content, but the surrounding Grid assigned only the remaining fixed window height to that row. Content taller than the assigned row therefore had no layout space into which the card could grow.

Rev12 replaces the fixed initial height with:

```xml
SizeToContent="Height"
```

and changes the Settings content row to:

```xml
<RowDefinition Height="Auto" />
```

The card and window can therefore use their desired vertical size. Wrapped descriptive text, long active storage paths, and visible validation errors can increase the Settings height instead of being clipped.

## Build Verification

On Windows with the .NET 9 SDK installed:

```powershell
dotnet clean TeeKay87.MemoryEngine.sln
dotnet build TeeKay87.MemoryEngine.sln -c Release
```

Expected:

- zero compiler errors;
- zero warnings because warnings are treated as errors;
- no recurrence of the rev10/rev11 WPF accessibility errors;
- `SettingsWindow.xaml` compiles with `x:ClassModifier="internal"`, `SizeToContent="Height"`, and an Auto-sized content row;
- all projects build without unrelated changes.

## Automated Verification

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 30 checks passed.
```

No automated scanner/storage check is removed or weakened in rev12.

## Manual Settings Layout Verification

1. Launch the application and open **Settings**.
2. Confirm the complete **Scan Results Storage Location** card is visible without clipping.
3. Confirm the final text beginning with `A changed location is saved...` is fully readable.
4. Confirm the Settings window initially grows vertically enough to contain the card and the Save/Cancel row.
5. Choose a storage path long enough to wrap the **Active for this application session** path and confirm the card/window expands rather than clipping it.
6. Enter or select an invalid/unwritable path and press **Save** so the validation error becomes visible. Confirm the error text is fully visible and the content-driven window height accommodates it.
7. Confirm **Browse...**, **Use Default**, **Save**, and **Cancel** retain their established 34-DIP shared control height and theme styling.
8. Check Light, Dimmed, and Dark themes for readable card borders, text, inputs, and buttons.
9. Confirm the window remains resizable and respects its existing minimum width/height.

## Foundation Regression Verification

Rev12 must preserve the existing Scan Result Storage Foundation behavior:

- selected storage path persists through the shared application settings file;
- theme persistence remains intact when storage settings are saved;
- a changed storage path becomes active on the next application session rather than migrating an in-use session;
- application-session and scan-session GUID isolation remains unchanged;
- New Scan invalidation, startup stale cleanup, shutdown cleanup, and deletion-independent correctness remain unchanged;
- incomplete/failed/cancelled storage sessions cannot become active;
- reusable modal operation progress behavior remains unchanged.

## Scanner and Plugin Regression Boundary

Rev12 must preserve:

```text
Application safety limit: 2,000,000 scan results
UI presentation cap:      50,000 rows
Plugin API:               2.1.0
PS5 plugin:               0.1.0.rev12
Mock plugin:              1.0.0.rev3
```

No Core scanner source, Plugin SDK contract, PS5 plugin source, Mock plugin source, TurboScan behavior, Endianness, Alignment, Floating-point rounding, Pause target behavior, raw-memory behavior, Scan-panel overflow behavior, or tooltip behavior is changed by this revision.

The known PS5 `1 Byte (Signed)` / value `50` scan returning approximately 16.2 million matches remains expected to stop at the two-million application-level safety limit. Disk-backed massive-result work remains deferred until this foundation revision is verified.

## Completion Record

Do not mark rev12 as the verified Scan Result Storage Foundation baseline until the Windows Release build succeeds, all 30 automated checks pass, and the manual Settings/storage/progress verification is completed.

## Reported Runtime Result

On 2026-09-02, rev12 was rebuilt/launched on the Windows development machine and the corrected Settings window was reported to look correct. The **Scan Results Storage Location** panel no longer clipped its lower explanatory text after the content-driven height change.

This records the reported runtime result for the defect rev12 was created to correct. No separate `All 30 checks passed.` transcript was supplied with that report, so the automated-suite status should not be inferred from the UI confirmation alone.
