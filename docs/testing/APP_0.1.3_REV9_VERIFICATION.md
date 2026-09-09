# TeeKay87's Memory Engine 0.1.3.rev9 Verification

## Revision Under Test

```text
Host application:             0.1.3.rev9
Feature:                      Scan Result Storage Foundation
PlayStation 5 plugin:         0.1.0.rev12
In-Memory Test Target plugin: 1.0.0.rev3
Plugin API:                   2.1.0
```

This revision is a storage/lifecycle foundation. It must be verified independently before the scanner is changed to store massive result sets on disk.

## Build Verification

On Windows with the .NET 9 SDK installed:

```powershell
dotnet build TeeKay87.MemoryEngine.sln -c Release
```

Expected:

- zero compiler errors;
- zero warnings because warnings are treated as errors;
- platform plugin assemblies copied to the application `Plugins` output directory;
- bundled JSON themes copied normally.

## Automated Verification

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 30 checks passed.
```

Rev9 adds five top-level checks to the 25-check rev8 baseline:

1. **Scan-result storage path validation** — valid writable roots are normalized/prepared and invalid file paths are rejected.
2. **Scan-result application-session isolation** — separate manager instances receive different application-session GUIDs and a new startup never adopts the previous instance's session.
3. **Scan-result stale-session cleanup safety** — valid managed stale data can be removed while an unrelated GUID directory without Memory Engine ownership metadata remains untouched.
4. **Scan-result scan-session lifecycle** — Creating/Writing/Committed metadata, committed result count, invalidation, fresh scan identity, Cancelled, and Failed paths are exercised.
5. **Generic operation progress contract** — indeterminate and determinate progress plus invalid fraction rejection are exercised independently of WPF.

The original 25 plugin/scanner/PS5 protocol checks must continue to pass unchanged.

## Settings Verification

1. Start the application and open **Settings** from the top application bar.
2. Confirm **Scan Results Storage Location** is visible and initially resolves to the configured path or the documented default:

   ```text
   %LocalAppData%\TeeKay87\MemoryEngine\ScanResults
   ```

3. Choose **Browse...** and select another writable directory.
4. Save. Confirm the dialog closes without changing the current application-session root shown during the running instance.
5. Close/restart the application and reopen Settings. Confirm the custom path persisted and is now the active application-session root.
6. Choose **Use Default**, save, restart, and confirm the default root is active again.
7. Confirm changing scan storage does not reset the selected color theme.
8. Change the theme, restart, and confirm saving theme preference does not remove the scan-storage path from `settings.json`.
9. Enter/select an unusable path and confirm Save produces a controlled validation error instead of silently switching to another location.

## Application-Session Verification

With the application running and scan storage initialized:

1. Inspect the active root and confirm a GUID application directory exists beneath it.
2. Confirm it contains `session.json` with the expected Memory Engine ownership signature, format version, and matching application-session GUID.
3. Record the GUID.
4. Exit normally.
5. When cleanup is not externally blocked, confirm that application-session directory has been removed.
6. Relaunch and confirm the new application-session GUID differs from the recorded GUID.

Physical cleanup success is a disk-hygiene check only. A failed deletion must not change the identity result in step 6.

## Scan-Session Verification

Use Mock for deterministic UI checks or PS5 after connecting/selecting an Active Target.

1. Run a successful First Scan.
2. While the scan session is active, confirm one GUID scan directory exists below the current application-session directory and that its `scan.json` is `Committed` after scan success.
3. Record its `scanSessionId`.
4. Run a compatible Next Scan and confirm the host does not create a replacement scan-session GUID.
5. Press **New Scan** and confirm the prior scan session is invalidated and its directory is deleted when possible.
6. Run another First Scan and confirm its scan-session GUID differs from the first.
7. Cancel a First Scan at a safe cancellation opportunity and confirm no cancelled scan session remains eligible/current.
8. Trigger a controlled scan failure if practical and confirm the failed session does not become committed/current.

## Stale / Incomplete Session Safety

Create a controlled stale test under a disposable scan-storage root.

1. Leave a valid Memory Engine application-session directory behind, or simulate a previous terminated instance using the same metadata schema.
2. Place a scan session in `Writing` state.
3. Start a new application/storage-manager instance against the same root.
4. Confirm a new application-session GUID is created and the old directory is treated as stale rather than adopted.
5. If deletion succeeds, confirm the stale directory is removed.
6. Repeat while externally preventing deletion where practical. Confirm the new application session still operates under a different GUID and never uses the old `Writing` session.
7. Add an unrelated GUID-named directory without valid Memory Engine `session.json` ownership metadata. Confirm startup does **not** delete it.

## Modal Progress Dialog Verification

The reusable component is introduced before its first production scan-result writer. Exercise the component from a development/debug invocation or its first consuming workflow before declaring the UI portion verified.

### Determinate

- Open the dialog with cancellation disabled.
- Report multiple fractions from 0 to 1.
- Confirm the progress bar/percentage update and status/detail text can change.
- Confirm the Cancel button is hidden.
- Confirm the owner window cannot be interacted with.

### Indeterminate

- Report an `OperationProgress` with a null fraction.
- Confirm the progress bar becomes indeterminate and percentage text is hidden.
- Switch to a determinate report and confirm the same dialog transitions correctly.

### Cancellation

- Open with cancellation enabled and an operation that observes its token.
- Press Cancel.
- Confirm the button disables and the dialog remains open while cancellation is being honored.
- Confirm the operation receives cancellation and the dialog closes only after the operation exits.

### Failure

- Throw a controlled exception from the operation.
- Confirm the dialog closes cleanly and the caller receives the original failure.

### Themes

Repeat representative determinate/indeterminate/cancellable checks in Light, Dimmed, and Dark. Confirm no system-light surfaces or unreadable text appear.

## Regression Verification

Rev9 must not change the following verified behavior:

- plugin-owned Value Type/Scan Type/Scan Option lists;
- PS5 Pause target while scanning;
- PS5 Endianness, Alignment, and Floating-point rounding semantics;
- native/shared First and Next Scan selection;
- cancellation/transport framing;
- raw memory read/write and Safe Write Test;
- rev8 long-tooltip wrapping;
- rev8 Scan-panel vertical overflow behavior;
- current 2,000,000-result safety limit;
- current 50,000-row display cap;
- Saved Addresses placeholder behavior.

For the current large-result PS5 regression case, a `1 Byte (Signed)` First Scan for `50` returning approximately 16.2 million native matches is still expected to be stopped by the existing application safety limit in rev9. Success beyond that limit belongs to the disk-backed massive-result revision.

## Actual Windows Build Result

Rev9 did **not** pass Windows build verification. Visual Studio reported the primary compiler error:

```text
CS1503 ScanResultStorageSession.cs(129): Argument 3: cannot convert from 'method group' to 'System.Action'
```

`TransitionToTerminalState` accepts a parameterless `Action`, while `ScanResultStorageManager.LogCommitCancelled` requires the current scan-session `Guid`. The App/Test metadata-reference failures and XAML designer errors shown in the same build were downstream consequences of Core failing to compile.

The compile defect is corrected in `0.1.3.rev10 - Scan Result Storage Compile Fix`. Rev9 must therefore remain **unverified** and must not be used as the verified baseline.

## Completion Record

Do not mark rev9 verified until the Windows build, all 30 automated checks, Settings/storage lifecycle checks, modal progress checks, and relevant UI regressions have passed. Record the actual runtime results here or in a follow-up verification update without converting README into version history.
