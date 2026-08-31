# 0.1.1.rev13 Raw Memory Read Compile Fix Verification

## Purpose

This document records the verification requirements for **TeeKay87's Memory Engine 0.1.1.rev13 - Raw Memory Read Compile Fix**.

Rev13 is intentionally a narrow host-side correction. It does not add a new memory feature and it does not change the PS5 protocol implementation introduced in rev12. Its purpose is to restore a clean Windows build so the already implemented rev11 memory-map and rev12 raw-memory-read paths can be runtime-verified together.

## Reported Failure

The first Windows build of rev12 reported:

```text
CS0177: The out parameter 'address' must be assigned to before control leaves the current method.
```

The error originated in `PluginViewModel.TryParseHexAddress(...)`. The method used this logical shape:

```text
non-empty candidate && TryParse(..., out address)
```

C# short-circuit evaluation allows the right-hand side to be skipped when the candidate is empty. That means the method could return without the `out` parameter being definitely assigned.

## Correction

The corrected method assigns the `out` parameter a safe default before processing the input. Empty normalized input is handled with an explicit `return false`, and only non-empty text is passed to `ulong.TryParse`.

This preserves the intended behavior:

- empty input is invalid;
- malformed hexadecimal input is invalid;
- valid hexadecimal input with or without a `0x` prefix is accepted;
- the caller always receives a defined `address` value even when parsing fails.

No warning suppression, nullable relaxation, compiler-option change, or XAML workaround is used.

## XAML Designer Errors Seen With the Failed Build

The same Visual Studio session displayed Designer errors including:

```text
XDG0008: ProportionalGridSplitter does not exist in TeeKay87.MemoryEngine.App.Controls
XDG0008: UiMetrics does not exist in TeeKay87.MemoryEngine.App.Application
XDG0010: DependencyProperty.UnsetValue is not a valid RuntimeHeight value
XLS0414: System.Object was not found
```

Source verification for rev13 confirms:

- `UiMetrics` is `public static` in `TeeKay87.MemoryEngine.App.Application`;
- `ProportionalGridSplitter` is `public sealed` in `TeeKay87.MemoryEngine.App.Controls`;
- `ControlStyles.xaml` maps the `Application` namespace correctly;
- `MainWindow.xaml` maps the `Controls` namespace correctly;
- the referenced source files are included by the SDK-style WPF project through normal default compile items.

These errors are therefore expected to disappear after the corrected host project builds and the XAML Designer reloads the resulting assembly. They should **not** be addressed by duplicating types, hardcoding UI heights, or removing the proportional splitter.

If Visual Studio continues to show stale Designer messages after a successful rebuild, close/reopen the solution or delete the local `bin`, `obj`, and `.vs` directories and rebuild before treating them as independent defects. Those local build artifacts are not part of the release package.

## Version Boundaries

Expected versions:

```text
Host application:  0.1.1.rev13
PS5 plugin:        0.1.0.rev4
Mock plugin:       1.0.0.rev1
Plugin API:        1.0.0
```

The PS5 plugin does not receive a new revision because no PS5-plugin code changes in rev13.

## Preparation Verification Result

The rev13 preparation audit completed successfully:

- all **64** C#/XAML/project/theme source files were read in full and structurally reviewed;
- all **31** README/CHANGELOG/docs Markdown files were read in full;
- all XAML/project XML and theme JSON files parsed successfully;
- all local Markdown links resolved;
- `UiMetrics` and `ProportionalGridSplitter` public type/namespace mappings were verified against their XAML declarations;
- the corrected `TryParseHexAddress(...)` was checked to assign `address` before every early-return path;
- Core, Plugin SDK, both platform plugins, protocol tests, themes, the proportional splitter implementation, and shared style resources were confirmed byte-for-byte unchanged from rev12.

Native compilation remains the only build-level check that cannot be performed in the preparation environment.

## Required Windows Verification

1. Run **Build -> Rebuild Solution**.
2. Confirm `CS0177` is gone.
3. Confirm no new C# compile errors or warnings are produced.
4. Confirm the XAML Designer errors disappear after the successful build/reload.
5. Run the verification executable and confirm all tests pass.
6. Continue directly with the combined rev11/rev12 live verification:
   - connect to ps5debug-NG;
   - enumerate processes;
   - set the game process as Active Target;
   - confirm a non-zero memory-map region count;
   - perform the default 64-byte Raw Memory Read;
   - confirm the hexadecimal/ASCII dump is populated;
   - repeat the read and verify refresh/disconnect behavior.

A successful result closes rev13's build fix and simultaneously allows rev11/rev12 runtime verification to proceed.

## Preparation-Environment Limitation

The preparation environment does not contain the .NET SDK or Windows WPF build toolchain. Static source verification can confirm the definite-assignment correction and type/namespace relationships, but the actual WPF build and Designer recovery must be verified in Visual Studio on Windows.

## Observed Windows Runtime Result

The Windows-side rev13 rebuild progressed past the `CS0177` compiler failure, confirming that the definite-assignment fix restored compilation far enough for WPF startup to create `MainWindow`. Startup then failed with a separate binding exception:

```text
System.InvalidOperationException:
A TwoWay or OneWayToSource binding cannot work on the read-only property
'MemoryReadResultText' of type 'TeeKay87.MemoryEngine.App.ViewModels.PluginViewModel'.
```

The exception is caused by the Raw Memory Read result `TextBox`. `TextBox.Text` uses a TwoWay binding by default, while `MemoryReadResultText` is intentionally host-output-only and exposes a public getter with a private setter. `IsReadOnly="True"` prevents user editing but does not change the binding mode.

This runtime result confirms that the rev13 compiler correction itself worked. It also identifies the next narrow host presentation defect before the combined rev11/rev12 live memory-map/raw-read verification could be completed. The binding is corrected in **0.1.1.rev14 - Raw Memory Read Result Binding Fix** by explicitly using `Mode=OneWay` for the result display.
