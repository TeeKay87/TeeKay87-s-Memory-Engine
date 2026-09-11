# Application 0.1.7.rev12 Source Review — Breakpoint Manager Compile Fix

## Status

**Historical static/source review; superseded by rev13 after the Windows test-project build failed.** Rev12 correctly fixed the two PS5 nullable lookup diagnostics from rev11, and the application itself could launch, but the packaged 114-check verification executable did not compile because breakpoint source-contract tests referenced a missing `AssertContains` helper.

The verified functional baseline therefore remains `0.1.7.rev10 - PS5 Extended Register Transport Guard` (**110/110 PASS** plus focused live-PS5 acceptance). Rev11 introduced the breakpoint layer; rev12 fixed its PS5 production compile issue; rev13 is required to make the verification harness build and to incorporate the requested Breakpoints/Events default split.

## Post-Package Windows Result

The user ran the documented verification command against rev12. Compilation stopped with repeated `CS0103` errors at `Program.cs` lines 2316-2340 because `VerifyDebuggerBreakpointManagerSourceAsync` and `VerifyBreakpointDialogSourceAsync` called `AssertContains`, while the helper section defined only `AssertTrue`, `AssertFalse`, `AssertEqual`, and exception helpers. The test registry therefore never executed and rev12 cannot be called verified.

## Reported Rev11 Build Failure

The first Windows build of rev11 reported two primary compiler errors:

```text
CS8600  Converting null literal or possible null value to non-nullable type.  Ps5DebuggerSession.cs
CS8600  Converting null literal or possible null value to non-nullable type.  Ps5DebuggerSession.cs
```

Visual Studio also displayed:

```text
XLS0414 The type 'System.Object' was not found. Verify that you are not missing an assembly reference and that all referenced assemblies have been built. MainWindow.xaml
```

`Directory.Build.props` enables nullable analysis and `TreatWarningsAsErrors`. The two `CS8600` diagnostics therefore stop the PS5 project build. The XAML diagnostic appeared after a referenced project failed to build and no independent `MainWindow.xaml` source change was implicated.

## Source-Level Cause

`Ps5DebuggerSession.RemoveBreakpointAsync` and `SetBreakpointEnabledAsync` each declared a non-nullable `SoftwareBreakpointEntry entry` and passed that variable directly as the `out` destination of `Dictionary<string, SoftwareBreakpointEntry>.TryGetValue`.

With nullable reference analysis enabled, the dictionary API can assign the default/null value when the lookup returns false. The following throw did protect runtime use, but the direct assignment itself was still a possible null-to-non-nullable conversion and produced `CS8600`.

## Corrective Change

Both affected lookups now:

1. receive the dictionary result into an explicitly nullable `SoftwareBreakpointEntry? foundEntry` local;
2. reject both the failed lookup and null value in one guard;
3. assign `foundEntry` to the existing non-nullable `entry` variable only after the guard proves it is non-null.

The existing `KeyNotFoundException` behavior and all subsequent breakpoint logic are unchanged. No null-forgiving operator or warning suppression is introduced.

## Revision Boundary

Production behavior changes are limited to the nullable-safe PS5 lookup form and metadata:

- application `0.1.7.rev12`, feature title `Breakpoint Manager Compile Fix`;
- PS5 plugin `0.1.0.rev31`;
- Plugin API remains `2.14.0`;
- Mock remains `1.0.0.rev12`;
- test expectation for PS5 plugin display version becomes `0.1.0.rev31`.

No public contract, Core service, WPF breakpoint command, Mock behavior, PS5 packet framing, breakpoint slot allocation, paused cleanup staging, SIGTRAP mapping, register transport, scan behavior, or export behavior is modified.

## Whole-Code Reuse Review

The complete source tree was reread before the correction. Existing breakpoint ownership remains correctly separated:

- Plugin SDK owns neutral breakpoint records/contracts;
- WPF owns generic Breakpoint Manager presentation/actions;
- Mock owns deterministic test behavior;
- PS5 owns ps5debug-NG slot ids, INT3 semantics, packet commands, event mapping, and backend-specific paused cleanup;
- Core contains no duplicate platform-specific breakpoint implementation.

The compile correction reuses the existing dictionary/state flow and introduces no second lookup/helper/service path.

## Required `using` Review

`Ps5DebuggerSession.cs` already imports `System.Collections.Generic`, which supplies both `Dictionary<TKey,TValue>` and `KeyNotFoundException`. The nullable-local correction adds no new namespace requirement. Existing required imports remain present.

## Verification Registry

Rev12 retains the **114** rev11 registrations. No new runtime feature was added, so no artificial test-count increase is made solely for the compile correction. Gate A must prove that the same source-contract/protocol suite can now build and execute.

## Documentation Result

Rev11 verification is explicitly marked superseded before verification. Rev12 was superseded before verification. The corrective rev13 remains inside the unfinished Breakpoint Manager feature block, so later debugger milestones shift again:

- rev14 — Hardware Watchpoints;
- rev15 — Call Stack and Call Frames;
- rev16 — Stepping and Run-to Operations;
- rev17 — Integration, Export and Finalization.

## Final Static Review Result

The locked rev12 work tree passed the required non-.NET source/package-preparation review before ZIP creation:

- **148** Markdown files reread successfully;
- **254** source/project/config text files reread successfully;
- **23** XAML/project/XML files parsed with **0** errors;
- **3** JSON files parsed with **0** errors;
- **51** relative Markdown links resolved with **0** broken links;
- **0** `bin`/`obj` directories are present;
- application/plugin/API metadata consistently identifies application `0.1.7.rev12`, Plugin API `2.14.0`, Mock `1.0.0.rev12`, and PS5 `0.1.0.rev31`;
- the packaged test registry contains exactly **114** unique registrations;
- the rev11-to-rev12 source comparison contains **2 added**, **15 changed**, and **0 removed** files;
- shared `TeeKay87.MemoryEngine.Core` and all unrelated production feature paths are byte-identical to rev11;
- all changed C# files were reviewed for required namespace imports and balanced source structure;
- the two reported direct `TryGetValue(..., out entry)` nullable assignments no longer exist in `Ps5DebuggerSession.cs`.

This static result does not replace the Windows compiler/test gate.

## Environment Limitation

No .NET/WPF build PASS is claimed by this source review. The authoritative Windows environment must build the packaged revision and run the 114-check suite.
