# 0.1.1.rev14 Raw Memory Read Result Binding Fix Verification

## Purpose

This document records the verification requirements for **TeeKay87's Memory Engine 0.1.1.rev14 - Raw Memory Read Result Binding Fix**.

Rev14 is a narrow WPF host correction. It does not alter the PS5 protocol implementation, Plugin SDK contracts, Core behavior, memory-map handling, raw-memory read implementation, themes, splitters, or the established main-workspace layout. Its purpose is to allow the application to complete WPF startup so the rev11 memory-map and rev12 raw-memory-read functionality can finally be verified on a real PS5.

## Reported Runtime Failure

After the rev13 compiler fix, the application reached `MainWindow` creation but WPF threw:

```text
System.InvalidOperationException:
A TwoWay or OneWayToSource binding cannot work on the read-only property
'MemoryReadResultText' of type 'TeeKay87.MemoryEngine.App.ViewModels.PluginViewModel'.
```

The Raw Memory Read result surface is a read-only `TextBox` bound to `PluginViewModel.MemoryReadResultText`. The property is intentionally output-only to the view: application logic updates it through a private setter and the user must not write it back through the binding engine.

## Root Cause

`TextBox.Text` has a default binding mode of TwoWay. Setting `IsReadOnly="True"` changes editing behavior but does not change the binding mode. Therefore the previous XAML implicitly asked WPF to write changes back into a property that has no public setter. WPF detects that invalid binding during layout/startup and throws before the main window can be used.

## Correction

The result binding is now explicit:

```xml
Text="{Binding SelectedPlugin.MemoryReadResultText, Mode=OneWay}"
```

The two editable Raw Memory Read inputs remain explicitly TwoWay:

- `MemoryReadAddressText`;
- `MemoryReadLengthText`.

A source audit confirmed that `MemoryReadResultText` is the only `TextBox` binding in the current main window that targets a read-only ViewModel property. No global TextBox binding behavior was changed.

## Version Boundaries

Expected versions for this revision:

```text
Host application:  0.1.1.rev14
PS5 plugin:        0.1.0.rev4
Mock plugin:       1.0.0.rev1
Plugin API:        1.0.0
```

The PS5 and Mock plugins do not receive new revisions because no plugin code changes in rev14.

## Preparation Verification Result

Release preparation completed **37 static checks successfully** before packaging. These checks cover XML/XAML and JSON parsing, the explicit OneWay result binding, preservation of TwoWay editable inputs, output-only ViewModel encapsulation, application/plugin/API version boundaries, local documentation links, release-tree cleanliness, and absence of build/user artifacts.

Byte-for-byte comparison also confirms that Core, Plugin SDK, both platform plugins, protocol tests, bundled themes, shared control styles, and the proportional splitter implementation are unchanged from rev13.

## Required Windows Verification

1. Run **Build -> Rebuild Solution** and confirm zero errors.
2. Start the WPF application and confirm the main window opens without the previous binding exception.
3. Confirm the Raw Memory Read expander can be opened and the result field is selectable/read-only.
4. Connect to the real PS5 running ps5debug-NG.
5. Load the process list and set the game process as Active Target.
6. Confirm a non-zero memory-map region count. This closes the outstanding rev11 live verification.
7. Perform the default 64-byte Raw Memory Read and confirm a hexadecimal/ASCII result appears. This closes the outstanding rev12 live verification.
8. Repeat a read and verify an invalid/unmapped range is rejected cleanly.
9. Refresh the process list and verify the retained Active Target can read again.
10. Disconnect and confirm target, map, and raw-read state clear without error.

## Preparation-Environment Limitation

The preparation environment does not provide the .NET SDK or Windows WPF toolchain, so native compilation and WPF startup cannot be executed here. The release preparation therefore verifies the XAML binding semantics, source structure, project/resource validity, version boundaries, documentation consistency, and release-tree integrity. The Windows steps above remain required for runtime closure.

## Windows Runtime Result - 2026-08-30

The rev14 application started successfully on the Windows development machine, confirming that the explicit OneWay result binding resolved the startup exception. The subsequent combined rev11/rev12 live PS5 verification then completed successfully, including non-zero memory-map enumeration, raw 64-byte reads, repeated reads, invalid-range rejection, refresh retention, and disconnect cleanup.

**rev14 startup/binding correction: PASS.**
