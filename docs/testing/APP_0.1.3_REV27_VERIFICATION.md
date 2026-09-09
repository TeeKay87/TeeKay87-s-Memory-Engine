# TeeKay87's Memory Engine 0.1.3.rev27 Verification

## Purpose

This checklist verifies **TeeKay87's Memory Engine 0.1.3.rev27 - Native Scan Mapping Compile Fix**. Rev27 is a compile-correction revision over rev26: it fixes nullable-flow metadata for the scan input list and intentionally leaves the completed Core Scan Type/native-mapping implementation unchanged.

The first priority is to prove that the Windows build blocker and its cascading XAML designer diagnostics are gone. After that, continue the complete functional scanner verification from `APP_0.1.3_REV26_VERIFICATION.md`; rev27 carries that feature set unchanged.

## Expected version boundary

After building this revision, confirm:

- application: `0.1.3.rev27`;
- feature title: `Native Scan Mapping Compile Fix`;
- Plugin API: `2.7.0`;
- PS5 plugin: `0.1.0.rev18`, targeting Plugin API `2.7.0`;
- Mock plugin: `1.0.0.rev4`, targeting Plugin API `2.0.0`.

No plugin revision or Plugin API bump is expected because the correction is confined to host nullable-flow metadata.

## 1. Clean Windows rebuild

1. Close any running Memory Engine instance.
2. In Visual Studio, clean and rebuild the complete solution.
3. Confirm the four rev26 `CS8604` diagnostics for `inputValues` are gone.
4. Confirm there are no new warnings promoted to errors.
5. Confirm the App project produces the `TeeKay87.MemoryEngine` assembly successfully.

Expected result: the complete solution builds without errors.

## 2. XAML designer cascade

After the successful build, reopen `MainWindow.xaml` if Visual Studio still has stale designer diagnostics. Confirm the rev26 cascade is gone:

- no `XSL0414` saying `System.Object` was not found;
- no `XDG0006` saying `ProportionalGridSplitter` is missing from `TeeKay87.MemoryEngine.App.Controls`;
- no `XDG0006` for `TextBoxInputFilter.Mode`;
- no `XDG0006` for `TextBoxInputFilter.MemoryValueType`;
- no follow-on `XDG0010 Operation is not valid due to the current state of the object` entries caused by the unresolved XAML types.

If Visual Studio retains only stale designer entries after the solution builds, close/reopen the XAML designer or restart Visual Studio before treating them as a source defect.

## 3. Startup smoke test

1. Start the application.
2. Confirm the title/version displays `0.1.3.rev27`.
3. Confirm both built-in plugins are discovered.
4. Open Settings and confirm the numeric interval TextBoxes render normally.
5. Return to the main workspace and confirm the proportional Scan Results/Saved Addresses splitter renders and can be dragged.
6. Confirm Saved Address Address/Value and Scan Value TextBoxes render normally.

These checks directly exercise the XAML types that Visual Studio could not resolve while rev26 failed to compile.

## 4. Nullable-flow correction regression

Run representative scan paths to confirm the compile-only annotation did not alter runtime behavior:

1. Mock -> **4 Bytes -> Exact Value** -> First Scan.
2. Run a Next Scan on the resulting session.
3. Run a scan that uses no operand, such as **Unknown Initial Value**, to confirm an empty-but-non-null input list still reaches the scanner correctly.
4. Run **Between** to exercise the two-input path.
5. If disk-backed storage is enabled/available, repeat a First/Next operation through the disk-backed path.

Expected result: all paths behave as documented in rev26.

## 5. Automated verification

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line remains:

```text
All 39 checks passed.
```

Rev27 adds no new production behavior requiring a new semantic test count; the existing 39 checks remain the scanner/native-mapping regression suite.


## Automated verification result - 2026-09-05

The user ran the rev27 verification executable on Windows after the compile correction. The run completed with **39/39 PASS** and ended with `All 39 checks passed.`. This confirms the existing scanner/native-mapping regression suite remained intact after the nullable-flow compile fix.

This result covers the automated suite only. The live/manual Scan Type workflow checks from the rev26/rev27 checklist remain separate; the post-scan Scan Type selector issue found afterward is corrected in rev28.

## 6. Continue rev26 functional verification

After sections 1-5 pass, execute the remaining live/runtime checks in:

- `APP_0.1.3_REV26_VERIFICATION.md`

Treat rev27 as the source package under test. The Core Scan Types, native mapping matrix, PS5 snapshot path, Core fallback boundaries, input validation, Saved Addresses coordination, and disk-backed scan behavior are all inherited unchanged from rev26.

## Static package-preparation result

Before packaging rev27, the source tree passed 25/25 static release checks. The review confirmed:

- the rev27 source diff is limited to `PluginViewModel.cs`, centralized `AppInfo`, README/CHANGELOG, the recorded rev26 compile result, and this rev27 verification file;
- Core, Plugin SDK, PS5 plugin, Mock plugin, and the verification-test project are byte-identical to rev26;
- `MainWindow.xaml`, `TextBoxInputFilter.cs`, and `ProportionalGridSplitter.cs` are byte-identical to rev26;
- all 14 XAML/project XML files and all 3 JSON files parse successfully;
- all 125 C# files pass the repository's structural delimiter/string/comment check;
- all relative links across 80 Markdown files resolve;
- no `bin`, `obj`, or `.vs` directories are present.

This is static source/package verification only and does not replace the required Windows build.

## Completion criteria

Rev27 can be accepted when:

- the complete Windows solution builds without the rev26 `CS8604` errors;
- the cascading XAML designer errors disappear after a successful build/refresh;
- the application starts as `0.1.3.rev27`;
- all 39 automated checks pass;
- representative zero-, one-, and two-input scan paths run normally;
- the full rev26 scanner/native-mapping verification is then completed against rev27.

Only after the rev24-rev27 input/scanner feature block is fully verified should the application version advance to the next feature version.
