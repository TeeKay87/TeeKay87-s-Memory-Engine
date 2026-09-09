# TeeKay87's Memory Engine 0.1.3.rev17 Verification

## Revision Under Test

```text
Host application:             0.1.3.rev17
Feature:                      Saved Addresses
PlayStation 5 plugin:         0.1.0.rev15
In-Memory Test Target plugin: 1.0.0.rev3
Plugin API:                   2.3.0
```

## Purpose

Rev17 replaces the Saved Addresses placeholder with the first functional shared address table. It also adds the application-level Saved Addresses update interval requested for live value refresh and Frozen repeated writes.

The revision does not change Plugin API, PS5/Mock plugin code, native TurboScan, Core scan-result storage, or the verified disk-backed massive-result scan path.

## Static Review Performed Before Packaging

The preparation review checks the changed source paths rather than attempting a .NET runtime build in the preparation environment.

Confirm before packaging:

- all changed/new C# files have required explicit `using` directives;
- C# delimiter structure is balanced across the solution;
- all XAML parses as XML and every newly referenced XAML event handler exists in `MainWindow.xaml.cs` / `SettingsWindow.xaml.cs`;
- `AppInfo` reports `0.1.3.rev17` / `Saved Addresses`;
- Plugin API remains `2.3.0`;
- PS5 plugin remains `0.1.0.rev15` and Mock remains `1.0.0.rev3`;
- Core scanner/storage and both platform plugin source trees are unchanged from rev16;
- the legacy `2,000,000` limit and the `50,000` UI-preview ceiling remain in their established scanner paths;
- the old Saved Addresses placeholder/`Active` column is gone;
- no new host hardcoded Value Type catalog was introduced;
- Saved Addresses are not cleared by `New Scan`;
- freeze/read/write paths require the row's matching Active Target and disconnect disables active freezes;
- First Scan and Next Scan stop the Saved Addresses timer for the entire scan operation and resume it after scanning returns to idle;
- Disconnect, Set Active Target, and New Scan suppress Saved Address background traffic while changing target/session state;
- row removal is blocked while a Saved Address target transaction is active; otherwise removal clears Frozen before collection removal and timer snapshots skip rows removed after the snapshot was taken;
- Address/Type edits cannot mutate row interpretation while a Saved Address refresh/write is already in flight;
- obsolete/redundant paths introduced by the new implementation have been reviewed.

## Existing Deterministic Verification

The verification executable remains at 34 top-level checks. Rev17 does not add an App/WPF project reference to that executable. The existing plugin-settings persistence check is extended so a plugin-scoped write must preserve the new application-level Saved Addresses update-interval key.

Expected existing final line on the Windows development machine:

```text
All 34 checks passed.
```

## Focused Manual Verification

Keep runtime verification concise.

### 1. Save from Scan Results

1. Connect and set an Active Target.
2. Run a scan that returns visible results.
3. Double-click one Scan Result.
4. Confirm one row appears in Saved Addresses.
5. Right-click another Scan Result and choose **Save Address**.
6. Confirm the second row appears.
7. Save the first result again and confirm it selects/reuses the existing row rather than adding an accidental duplicate.

### 2. Direct column interaction

On a Saved Address:

1. change **Description** and confirm the text remains;
2. edit **Address** with a valid hexadecimal address and confirm the row refreshes that location;
3. enter invalid address text and confirm the previous valid address is restored with a controlled status message;
4. change **Type** and confirm the choices come from the active plugin's declared Value Types;
5. edit **Value** and confirm the target value is written and the row displays the written value.

Use a known safe address/value while verifying writes.

### 3. Frozen

1. Choose a known safe Saved Address.
2. Enable **Frozen**.
3. Change that value from the target/game side.
4. Confirm the Saved Address value is restored at the configured interval.
5. Disable **Frozen** and confirm repeated writes stop.
6. Enable Frozen again and remove the row; confirm it disappears and no further repeated write occurs.
7. Repeat with another Frozen row and disconnect; confirm the row becomes unfrozen rather than automatically resuming after a later connection.

### 4. Scan independence

1. Keep at least one Saved Address present.
2. Run Next Scan and then New Scan.
3. Confirm the Saved Address remains in the lower table even though temporary Scan Results are reset.


### 5. Scan-time refresh pause

1. Keep at least one Saved Address visible; use a safely writable Frozen row if practical.
2. Start First Scan or Next Scan and leave the scan running long enough to exceed several Saved Addresses update intervals.
3. Confirm Saved Address refresh/freeze traffic does not run while the scan is active.
4. Confirm Saved Address updates resume automatically after the scan completes or is cancelled and the scanner returns to idle.
5. Repeat once with **Pause target while scanning** enabled and confirm Saved Addresses still remain paused until target resume/scan teardown finishes.

### 6. Target safety

1. Save an address from one Active Target process.
2. Switch to a different Active Target.
3. Confirm the row remains visible but is inactive for the current target.
4. Confirm Frozen/direct writes are not sent to the different process.

### 7. Settings interval

1. Open **Settings**.
2. Confirm the Saved Addresses update interval defaults to `500` ms when no prior value exists.
3. Save another valid value, for example `1000` ms.
4. Confirm the Saved Addresses footer immediately shows the new interval.
5. Restart the application and confirm the saved value is restored.
6. Confirm values below `50`, above `10000`, and non-integers are rejected without closing Settings.

## Acceptance

Rev17 is ready to become the next verified baseline when:

- the Windows solution builds normally;
- the existing 34 deterministic checks still pass;
- both Scan Result save gestures work;
- all five Saved Address columns are directly usable;
- generic read/write and Frozen behavior work on a known safe target address;
- New Scan does not remove Saved Addresses;
- target switching does not redirect automatic writes;
- Saved Addresses refresh/freeze is paused for the full duration of First/Next Scan and resumes afterward;
- Saved Addresses update interval persists and applies immediately;
- existing massive-result First/Next Scan behavior remains unchanged.
