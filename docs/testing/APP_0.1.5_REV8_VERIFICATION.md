# TeeKay87's Memory Engine 0.1.5.rev8 Verification

## Revision Under Test

```text
Host application:             0.1.5.rev8
Feature:                      UI State and Theme Consistency
Plugin API:                   2.9.0
PlayStation 5 plugin:         0.1.0.rev22
In-Memory Test Target plugin: 1.0.0.rev4
Expected automated checks:    54
```

## Purpose

Rev8 is a deliberately host/UI-scoped consistency revision following successful functional runtime testing of rev7 bookmarks, region navigation/history, and the Saved Addresses tooltip correction.

It must correct five observed UI/state issues without changing the verified Memory Viewer reader/writer/navigator behavior, scanner semantics, plugin contracts, PS5 protocol paths, export pipeline, Saved Address behavior, or `tools/preflight/`.

The required changes are:

1. all application-owned Remove/Cancel/Exit/Delete/Abort/Disconnect-style buttons use the theme-owned Danger semantic style;
2. every interactive control in the Scan panel is disabled until an explicit Active Target is set;
3. the permanent status bar shows host version/revision instead of the revision feature title;
4. the native main-window title bar no longer includes version/revision;
5. the Memory Viewer bookmark selector shows the selected bookmark's normal address/region label instead of the backing ViewModel type name.

## 1. Clean Windows Build

1. Delete stale `bin`/`obj` directories if necessary.
2. Open `TeeKay87.MemoryEngine.sln` in Visual Studio.
3. Choose **Rebuild Solution**.
4. Confirm there are no compiler/WPF markup errors and no warnings.

The real Windows/Roslyn/WPF build remains the authoritative compile gate.

## 2. Automated Verification Runner

Run `TeeKay87.MemoryEngine.Tests`.

Expected ending:

```text
All 54 checks passed.
```

Rev8 intentionally adds no Core/plugin automated check. The existing runner does not reference the WPF application project, and the five rev8 changes are presentation/state wiring around already-tested contracts. All previous 54 checks must therefore remain unchanged and pass.

## 3. Scan Panel Requires Explicit Active Target

Use either Mock Target or PS5.

1. Select a platform plugin and connect.
2. Refresh/select a process if necessary, but **do not** choose **Set Active Target** yet.
3. Confirm the process selector may show a selected process while the Active summary still says no active target.
4. Inspect the complete right-side Scan panel.
5. Confirm all interactive Scan controls are disabled as one surface:
   - Value / Value 1 / Value 2 inputs when visible;
   - Scan Type;
   - Value Type;
   - plugin choice-list Scan Options;
   - plugin toggle Scan Options;
   - Pause target while scanning when the plugin exposes it;
   - First Scan;
   - Next Scan;
   - New Scan;
   - Cancel Scan.
6. Choose **Set Active Target**.
7. Confirm the Scan controls become available according to their existing capability/session rules. Disabled workflow controls such as Next Scan before First Scan must remain disabled for their normal reason.
8. Disconnect or otherwise clear the Active Target and confirm the entire Scan control surface disables again.

Acceptance requires that merely browsing/selecting a process can never make Scan controls look usable before explicit Active Target promotion.

## 4. Main Window Title and Status Bar Identity

Start rev8 and inspect the main window.

Confirm:

- the native Windows title bar reads only **TeeKay87's Memory Engine**;
- it does **not** append `0.1.5.rev8`;
- the compact application bar retains its existing `0.1.5.rev8` version badge;
- the rightmost field of the permanent bottom status bar shows **0.1.5.rev8**;
- the feature title **UI State and Theme Consistency** is not displayed in the status bar.

Switch plugins, connect/disconnect, run a scan, and confirm the identity fields remain stable while ordinary status/scan text continues updating normally.

## 5. Memory Viewer Bookmark Selected-Item Display

1. Open Memory Viewer on Mock Target at `0x10000104`.
2. Choose **Add Current**.
3. Confirm the closed bookmark selector displays `0x10000104` (or the normal `address · region` label when a region/module name exists).
4. Add a second bookmark and open the dropdown.
5. Confirm the expanded items show their normal labels.
6. Select either bookmark and close the dropdown.
7. Confirm the selected/closed field shows that same normal label.
8. Confirm it never displays text such as:

```text
TeeKay87.MemoryEngine.App.ViewModels.MemoryViewerBookmarkViewModel
```

9. Use **Go** and **Remove** and confirm bookmark identity/history behavior remains identical to rev7.

Repeat once on PS5 where a bookmark has a named Region / Module if practical; the selected value should show the same address/region label seen in the expanded list.

## 6. Danger Button Semantic Consistency

Check at least the bundled **Light**, **Dimmed**, and **Dark** themes.

Confirm the following application-owned buttons use each theme's Danger palette when enabled:

- main-window **Disconnect**;
- main-window **Cancel Scan**;
- Saved Addresses **Remove** and **Remove All**;
- Memory Viewer bookmark **Remove**;
- Settings **Cancel**;
- Data Export **Cancel**;
- Memory Edit **Cancel**;
- Confirmation dialog **Cancel**;
- operation-progress **Cancel** when cancellation is available.

Confirm affirmative/neutral neighboring actions retain their existing semantic styles; for example **Save**, **Continue**, **Write & Verify**, **Go To**, **Go**, **Refresh**, and **Export...** must not become Danger merely because a Danger action is present nearby.

Disabled Danger buttons must still use the shared disabled palette rather than staying red or falling back to operating-system chrome.

## 7. Rev7 Bookmark / Region Navigation Regression

Rev7's functional paths were reported working correctly before rev8. Recheck a compact regression set:

- Add Current creates an exact-address bookmark;
- duplicate current bookmark is rejected/disabled as before;
- bookmark Go navigates and enters Back/Forward history;
- bookmark Remove removes only the selected bookmark;
- Previous Region / Region Start / Region End / Next Region behave as before;
- Back/Forward still traverse successful bookmark/region destinations;
- the green origin row remains layout-neutral;
- Refresh does not create a history entry;
- bookmarks remain window-local.

No rev8 change should issue new target I/O for bookmark presentation or alter Core region-navigation behavior.

## 8. Saved Addresses Tooltip Regression

Reconfirm the rev7 tooltip correction:

- hovering empty Frozen cell space shows no blank tooltip;
- hovering Protection row/cell space with empty `StatusText` shows no blank tooltip;
- hovering empty Remove-column space shows no blank tooltip;
- hovering the Frozen checkbox still shows its meaningful tooltip;
- hovering the Remove button still shows its meaningful tooltip.

This behavior must remain unchanged after the rev8 button-style changes.

## 9. Scanner / Saved Address / Write Smoke Regression

Because the Scan control container now has a new parent enable gate, run at least one ordinary scan after setting Active Target:

1. perform a small First Scan;
2. perform a compatible Next Scan;
3. New Scan;
4. save at least one result;
5. verify Saved Address live refresh still works;
6. open Memory Viewer from the saved row;
7. on Mock Target, perform one controlled verified edit if desired.

The new panel gate must not alter scan command execution once Active Target exists.

## Static Preparation Review

Before packaging, verify:

- `AppInfo` reports `0.1.5.rev8` and feature title `UI State and Theme Consistency`;
- `AppInfo.WindowTitle` resolves to the application title without `DisplayVersion`;
- the main status bar binds its rightmost identity field to `DisplayVersion`;
- the top application-bar version badge remains present;
- `MainWindowViewModel` no longer exposes an unused FeatureTitle presentation property;
- `PluginViewModel.HasActiveTarget` is derived only from the existing `ActiveProcess` and raises property notification when Active Target changes;
- the entire Scan interaction column is gated by `HasActiveTarget` while existing individual command/option `IsEnabled` rules remain intact;
- `MemoryViewerBookmarkViewModel.ToString()` returns `DisplayText` and no bookmark navigation/storage data was changed;
- every application-owned literal Remove/Cancel/Disconnect-style Button uses `DangerButtonStyle`;
- every application-owned `IsCancel=True` Button uses `DangerButtonStyle`;
- no new theme key or hard-coded destructive color was introduced;
- Core is unchanged from rev7;
- Plugin API remains `2.9.0`;
- PS5 plugin remains `0.1.0.rev22`;
- Mock plugin remains `1.0.0.rev4`;
- automated registry contains exactly 54 checks;
- `tools/preflight/` is byte-identical to rev7;
- XAML/project XML and bundled JSON parse successfully;
- all relative Markdown links resolve;
- no `bin`, `obj`, or `.vs` directories are packaged;
- ZIP extraction reproduces the release tree byte-for-byte.

## Acceptance

`0.1.5.rev8` is accepted when the Windows solution builds cleanly, all **54/54** automated checks pass, Scan controls remain fully disabled until explicit Active Target selection, title/status-bar identity matches the centralized rev8 metadata, all destructive/dismissive buttons consistently follow the active theme's Danger semantics, the closed bookmark selector shows the correct bookmark label, and the otherwise successful rev7 Memory Viewer/Saved Addresses behavior remains unchanged.
