# Application 0.1.7.rev13 Source Review — Breakpoint Test Build and Layout Fix

## Status

**Static/source candidate review.** Rev13 is built directly from the user-supplied `0.1.7.rev12 - Breakpoint Manager Compile Fix` package. Rev12 fixed the PS5 nullable production compile errors from rev11, but the packaged verification project then failed before execution because the new breakpoint source-contract tests referenced an `AssertContains` helper that did not exist. Rev13 corrects that test-harness build blocker and applies the requested initial Breakpoints/Events splitter position. The authoritative Windows build, **114-check** run, Mock runtime/UI acceptance, and live-PS5 breakpoint acceptance remain external gates.

The last fully verified debugger baseline remains `0.1.7.rev10 - PS5 Extended Register Transport Guard`, which passed **110/110** automated checks plus focused live-PS5 runtime/cleanup acceptance.

## Supplied Rev12 Windows Failure

The documented verification command was run against rev12:

```text
dotnet run --project .\tests\TeeKay87.MemoryEngine.Tests\TeeKay87.MemoryEngine.Tests.csproj
```

The test project failed to compile with repeated `CS0103` diagnostics:

```text
The name 'AssertContains' does not exist in the current context
```

The failures were reported in `tests/TeeKay87.MemoryEngine.Tests/Program.cs` at the breakpoint-manager and breakpoint-dialog source-contract assertions around lines 2316-2340. The verification executable never started, so no rev12 automated PASS is claimed. The supplied runtime screenshot shows that the rev12 application and Mock Debugger window itself could launch; the failure is isolated to the verification source.

## Test-Harness Cause

The rev11 breakpoint source-contract additions used a convenient `AssertContains(source, expected, message)` form, but the shared helper section still contained only the existing `AssertTrue`, `AssertFalse`, `AssertEqual`, and exception helpers. No method named `AssertContains` was defined anywhere else in the test project.

This is an internal test-source error. It is not a missing framework API, plugin dependency, XAML issue, or external backend defect.

## Corrective Test Change

Rev13 adds one shared helper:

```text
AssertContains(string source, string expected, string message)
```

The helper:

- rejects null arguments through the existing framework guard;
- performs an ordinal `string.Contains` check;
- reuses the existing `AssertTrue` failure path;
- keeps every existing breakpoint source-contract call and message unchanged.

No assertion is removed, bypassed, converted to a no-op, or hidden behind conditional compilation.

## Breakpoints/Events Layout Change

The supplied runtime reference shows the right-side Debugger splitter starting with more vertical space allocated to Breakpoints than Events. Rev12 source used `2* / 3*` star rows for that pair.

Rev13 changes only the initial row weights to:

```text
Breakpoints: 13*
Events:       7*
```

This gives an approximate **65/35** starting share before practical minimum heights are considered. The existing `ProportionalGridSplitter` remains in place with:

- `ResizeBehavior="PreviousAndNext"`;
- `MinimumPreviousRatio="0.2"`;
- `MaximumPreviousRatio="0.8"`;
- the existing theme-aware row splitter style;
- adaptive star sizing when the Debugger window is resized.

The Breakpoints row and splitter still collapse when the active backend does not advertise `TargetCapabilities.Breakpoints`. The left-side Threads/Registers splitter is not changed.

## Source-Contract Coverage

The existing **Debugger breakpoint manager source contract** registration now also requires the `13* / 7*` starting weights. This protects the requested layout without adding a synthetic new registry entry solely to increase the test count.

The verification registry therefore remains **114 unique checks**.

## Revision Boundary

Production changes relative to rev12 are intentionally limited to:

- application metadata in `AppInfo`;
- the right-side initial Breakpoints/Events row weights in `DebuggerWindow.xaml`.

Verification-source changes are limited to:

- the missing `AssertContains` helper;
- two layout assertions inside the existing breakpoint-manager source-contract check.

No Core source changes are required. No Plugin SDK source changes are required. No Mock production source changes are required. No PS5 production source changes are required.

Expected metadata is:

| Component | Rev13 value |
| --- | --- |
| Application | `0.1.7.rev13` |
| Feature title | `Breakpoint Test Build and Layout Fix` |
| Plugin API | `2.14.0` |
| Mock plugin | `1.0.0.rev12` |
| PS5 plugin | `0.1.0.rev31` |
| Automated checks | `114` |

## Whole-Code Reuse Review

The complete supplied rev12 source tree was reviewed before the change. The correction reuses existing implementation instead of creating parallel paths:

- `ProportionalGridSplitter` already supplies the desired adaptive ratio behavior, so only initial star weights change;
- the test project already centralizes assertion helpers at the bottom of `Program.cs`, so `AssertContains` is added there rather than duplicating inline comparison logic eighteen times;
- neutral breakpoint ownership remains in Plugin SDK/WPF while backend-specific behavior remains in Mock/PS5;
- the rev10 register transport guard and all established target-generation safety remain untouched.

## Required `using` Review

No new `using` directive is required:

- `Program.cs` already imports `System`, which supplies `ArgumentNullException`, `StringComparison`, and `string` APIs;
- `DebuggerWindow.xaml` does not add a new CLR type or namespace;
- `AppInfo.cs` remains namespace-only metadata.

## Documentation Synchronization

Rev13 updates:

- root `CHANGELOG.md`;
- root `README.md`;
- the full development action plan;
- Debugger architecture;
- current Mock and PS5 host-version context;
- PS5 protocol/current-candidate context;
- rev12 source-review and verification status;
- this rev13 source review;
- the rev13 verification checklist.

Rev12 is now historical and explicitly superseded before verification. Hardware Watchpoints move to rev14, Call Stack/Frames to rev15, Stepping/Run-to to rev16, and Integration/Export/Finalization to rev17.

## Final Static Review Result

The locked rev13 work tree passed the non-.NET package-preparation checks before ZIP creation:

- **150** Markdown files were reread successfully;
- **254** source/project/config text files were reread successfully;
- **23** XAML/project/XML files parsed with **0** errors;
- **3** JSON theme/config files parsed with **0** errors;
- **51** relative Markdown links resolved with **0** broken links;
- **0** `bin`/`obj` directories are present;
- application/plugin/API metadata consistently identifies application `0.1.7.rev13`, Plugin API `2.14.0`, Mock `1.0.0.rev12`, and PS5 `0.1.0.rev31`;
- the test registry contains exactly **114** registrations and **114** unique names;
- `AssertContains` is now defined once in the shared helper section and uses ordinal containment through the existing assertion failure path;
- the existing breakpoint-manager source-contract registration now checks both `13*` and `7*` initial row weights;
- rev12-to-rev13 comparison contains **2 added**, **13 changed**, and **0 removed** files;
- the only changed production files under `src/` are `Application/AppInfo.cs` and `DebuggerWindow.xaml`;
- `TeeKay87.MemoryEngine.Core`, `TeeKay87.MemoryEngine.PluginSdk`, Mock production code, and PS5 production code are byte-identical to rev12;
- the changed C# files have balanced source structure and require no new namespace imports.

This static review does not claim a .NET/WPF compile or runtime PASS. Those remain the user's Windows verification gates.

## Package Verification

The final archive is named `TK87ME_0.1.7.rev13___Breakpoint-Test-Build-and-Layout-Fix.zip`. Package verification must be repeated after every documentation byte is finalized. The locked tree contains **404 files**; the final ZIP must contain the same 404 relative paths, pass the ZIP CRC/integrity check, contain no extra entries, and match every source file byte-for-byte by SHA-256.


## Subsequent Windows Result

After packaging, the Windows verification project compiled and ran all 114 registrations. The result was **113/114**: only `PS5 plugin metadata and connection settings` failed because its exact expected capability set omitted `TargetCapabilities.Breakpoints` while the PS5 production plugin correctly advertised that implemented capability. Rev13 is therefore superseded by rev14; the source-review conclusions for the rev13 production breakpoint/layout changes remain unchanged.
