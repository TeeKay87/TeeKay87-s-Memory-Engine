# Application 0.1.7.rev8 Source Review — Debugger Workspace Source Contract Fix

## Status

This source review covers the corrective rev8 package built directly from the exact user-tested `0.1.7.rev7 - Registers and Stop Context` archive. It records static/package checks only. The authoritative Windows result must come from the user's Windows/.NET environment.

## Baseline and Failure Reproduction

The rev7 Windows verification was run repeatedly and produced the same result each time: **106 of 107 checks passed**. The only failing registration was `Debugger workspace command and event source contract`, with the message `Debugger window is not bound to the current target connection generation.`

Source comparison against the verified rev6 implementation showed that the target-generation behavior had not been removed. Rev7 still performs all of the following in `MainWindow.OpenDebuggerButton_Click`:

- captures `plugin.ConnectionGeneration` into a local `connectionGeneration` value;
- constructs `DebuggerViewModel` with `plugin`, `targetProcess`, and the captured generation;
- reuses that same captured generation when validating debugger-to-Disassembler navigation.

The false negative was caused by source spelling only. Rev6 used `new DebuggerViewModel(...)`; rev7 introduced a typed local and target-typed `new(...)`. The existing verifier continued to require the literal substring `new DebuggerViewModel`.

## Corrective Change

Rev8 changes the existing source-contract assertion rather than production debugger behavior. The corrected check now requires the exact generation-capture statement and matches the `DebuggerViewModel` constructor binding in either of these equivalent C# forms:

```csharp
DebuggerViewModel viewModel = new(plugin, targetProcess, connectionGeneration);
```

or:

```csharp
DebuggerViewModel viewModel = new DebuggerViewModel(plugin, targetProcess, connectionGeneration);
```

The assertion still fails if the captured generation is not passed to the view model. No registration is removed, disabled, renamed, or replaced with an unconditional success path.

## Revision Boundary

Production functionality from rev7 is intentionally retained. The only production-source modification is centralized application metadata in `AppInfo` so the package presents `0.1.7.rev8 - Debugger Workspace Source Contract Fix`.

The following remain unchanged from rev7:

- all Core source;
- Plugin SDK `2.13.0` contracts and models;
- Mock plugin `1.0.0.rev10`;
- PS5 plugin `0.1.0.rev27`;
- Debugger window/view-model/register implementation;
- register value codec and edit dialog;
- PS5 GETREGS transport/mapping;
- scanner, Saved Addresses, Memory Viewer, Disassembler, export, themes, settings, and target transport behavior.

The external ps5debug-NG thread-control issue report remains under `docs/bug-reports/` unchanged.

## Documentation Review

Rev8 updates `CHANGELOG.md`, `README.md`, the full development action plan, the rev7 verification/source-review status notes, and adds this source review plus `APP_0.1.7_REV8_VERIFICATION.md`.

The README current-version/current-verification section is also corrected because stale rev5 references were discovered during the mandatory full-documentation pass. Those corrections describe the already-existing project state and do not alter production behavior.

The debugger development schedule is shifted by one corrective revision: Extended Register State is now planned for rev9, followed by software breakpoints in rev10, hardware watchpoints in rev11, call stack in rev12, stepping/run-to in rev13, and final integration/export in rev14.

## Final Static Review Results

The final pre-package review was run after all rev8 changes were complete.

### Diff boundary

- **7 existing files changed**, **2 files added**, and **0 files removed** relative to the exact user-tested rev7 package.
- Production source change: only `src/TeeKay87.MemoryEngine.App/Application/AppInfo.cs`, limited to application revision/feature metadata.
- Verification source change: only `tests/TeeKay87.MemoryEngine.Tests/Program.cs`, limited to the existing debugger workspace connection-generation source assertion.
- Documentation changes: `CHANGELOG.md`, `README.md`, the full development action plan, the rev7 verification/source-review status notes, and the two new rev8 testing documents.
- No Core, Plugin SDK, Mock plugin, PS5 plugin, transport, scanner, register, Memory Viewer, Disassembler, export, settings, or theme implementation file changed.

### Full-tree review

- **PASS — documentation reread:** all **138 Markdown files** were read after the corrective changes.
- **PASS — source/project reread:** all **243** C#/XAML/project/JSON files under `src/` and `tests/` were read after the corrective changes.
- **PASS — XML/XAML/project structure:** all **22** `.xaml`, `.csproj`, `.props`, and `.targets` files parsed successfully.
- **PASS — Markdown links:** **44** relative links were resolved across the Markdown tree; none were broken.
- **PASS — build-output hygiene:** no `bin/` or `obj/` directory is present.
- **PASS — changed-source hygiene:** no `TODO`, `FIXME`, or `HACK` marker was introduced in changed C#/XAML source.

### Verification contract review

- **PASS — registry preservation:** the rev8 registry contains exactly **107 unique checks**, identical by registered name to rev7. No check was added, removed, skipped, or renamed.
- **PASS — failure cause reproduced statically:** the rev7 source still contains the correct generation capture and target-typed `DebuggerViewModel` constructor call, explaining why the old literal `new DebuggerViewModel` assertion was a false negative.
- **PASS — corrected assertion:** the rev8 check requires `long connectionGeneration = plugin.ConnectionGeneration;` and confirms that `plugin`, `targetProcess`, and the captured `connectionGeneration` are passed into `DebuggerViewModel`.
- **PASS — syntax tolerance:** the constructor matcher accepts both explicit `new DebuggerViewModel(...)` and target-typed `new(...)`; it does not depend on which equivalent C# spelling is used.
- **PASS — surrounding safety checks retained:** the same test still verifies capability-driven Debugger entry, coordinator/event identity routing, and absence of platform-specific debugger tokens in the host workspace.

### Architecture and metadata preservation

- **PASS — Core byte identity:** all **59 Core files** are byte-identical to rev7.
- **PASS — Plugin SDK byte identity:** all **77 Plugin SDK files** are byte-identical to rev7; Plugin API remains `2.13.0`.
- **PASS — plugin byte identity:** all **28 files** under `src/Plugins` are byte-identical to rev7; Mock remains `1.0.0.rev10` and PS5 remains `0.1.0.rev27`.
- **PASS — debugger/register implementation preservation:** `MainWindow.xaml.cs`, `DebuggerViewModel`, register presentation/editing, Mock register services, PS5 GETREGS transport/mapping, and all other rev7 production behavior are byte-identical to the user-tested rev7 package.
- **PASS — application metadata:** centralized `AppInfo` reports `0.1.7.rev8 - Debugger Workspace Source Contract Fix`.

### Documentation consistency

- **PASS — current README:** current build, current test count, and current verification/source-review links now point to rev8 rather than the stale rev5 references found during the full-documentation pass.
- **PASS — rev7 history:** rev7 verification/source-review documents now record the repeatable **106/107** Windows result and the source-contract false negative without rewriting the historical implementation review.
- **PASS — development plan:** rev8 is recorded as the corrective candidate. Extended Register State moves to rev9 and the remaining debugger revision numbers shift by one.
- **PASS — external bug-report policy:** no new external bug report is created for this issue because the defect is in this repository's own verification source, not in an external tool/payload. The existing ps5debug-NG report remains unchanged under `docs/bug-reports/`.

### Pre-lock archive check

- A complete pre-lock archive was created from the final tree before this result section was written.
- **Archive entries:** **387 files**.
- **Archive integrity:** **PASS** — Python `ZipFile.testzip()` returned no corrupt entry.
- The final archive is rebuilt after this source-review result is saved, then rechecked for integrity and byte-for-byte package content before delivery.

## Environment Limitation

The authoritative gate remains the user's Windows/.NET/WPF environment. Rev8 must not be called verified until the packaged verification executable reports **All 107 checks passed** and the remaining Registers and Stop Context runtime/hardware acceptance is completed.
