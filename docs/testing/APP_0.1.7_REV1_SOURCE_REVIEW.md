# Application 0.1.7.rev1 Source Review — Debugger Contracts and Core Foundation

## Review Context

This source review was performed against the exact `0.1.6.rev14 - Disassembler Multi-Selection Copy Consistency` package used as the `0.1.7.rev1` development baseline.

The purpose of this review is to validate the revision boundary before packaging. It is **not** a substitute for the authoritative Windows Release build and automated verification executable documented in `APP_0.1.7_REV1_VERIFICATION.md`.

## Result

The rev1 source boundary passed the static review performed before packaging.

### Version and API metadata

Confirmed:

- application metadata is `0.1.7.rev1`;
- feature title is `Debugger Contracts and Core Foundation`;
- public Plugin API metadata is `2.12.0`;
- Mock plugin remains `1.0.0.rev7`, targeting Plugin API `2.11.0`;
- PS5 plugin remains `0.1.0.rev24`, targeting Plugin API `2.11.0`.

### Existing verification registry preservation

The `0.1.6.rev14` baseline contains 79 registered verification checks.

The rev1 registry contains 84 checks:

- all 79 baseline check names remain present;
- no baseline check was removed;
- exactly five debugger-foundation checks were added.

The added checks are:

1. `Debugger neutral model contracts`;
2. `Debugger optional service contracts`;
3. `Debugger session target identity`;
4. `Core debugger session lifecycle and event binding`;
5. `Core debugger attach safety and cleanup`.

This confirms the intended registry change from 79 to 84 without replacing an existing regression check.

### Platform-plugin preservation

A recursive byte comparison of the complete production source trees under `src/Plugins/` confirmed that the Mock and PS5 plugin production trees are unchanged from the supplied `0.1.6.rev14` baseline.

This is intentional. Rev1 introduces only the shared debugger contract/Core foundation. Neither plugin has a debugger backend yet and neither plugin receives a revision increase.

A source scan also confirmed that neither built-in plugin advertises any of these debugger-related capability flags:

- `Debugger`;
- `ThreadEnumeration`;
- `ThreadControl`;
- `RegisterAccess`;
- `Breakpoints`;
- `Watchpoints`;
- `CallStack`;
- `StepExecution`.

### Shared debugger architecture boundary

The new debugger production source under Plugin SDK/Core was scanned for platform-specific implementation terms associated with PS5/ps5debug-NG, x86/x64 registers, Xbox/XBDM/JRPC, and related backend concepts.

No platform-specific token was found in the new shared debugger source.

The shared implementation therefore remains within the intended ownership boundary:

- Plugin SDK defines neutral contracts/models/capabilities;
- Core coordinates neutral debugger session identity/lifecycle/events;
- platform debugger transport and backend behavior remain deferred to the platform plugin revisions that implement them.

### Thread-service granularity

The final rev1 contract separates:

- `IDebuggerThreadService` for thread enumeration;
- `IDebuggerThreadControlService` for individual-thread suspend/resume.

This matches the separate `ThreadEnumeration` and `ThreadControl` capabilities and allows a future backend to expose a thread list without being forced to implement unsupported thread-control operations.

### Project/document structure checks

The source review also confirmed:

- all project/props/targets and application XAML files parsed as well-formed XML;
- no `bin` or `obj` build-output directory is included in the source tree;
- all relative Markdown links in the packaged documentation resolve to existing files;
- the current README reports application `0.1.7.rev1` and host Plugin API `2.12.0`;
- the current development plan records `0.1.6.rev14` as fully tested/hardware-verified and `0.1.7.rev1` as the active candidate;
- historical testing/changelog records remain preserved rather than being rewritten as though they were authored after their original revision.

## Remaining Verification

The following items are intentionally still pending and must be performed on Windows:

1. clean Release build of the complete solution;
2. confirm no new compiler warning/error is introduced under the project's warnings-as-errors configuration;
3. run the verification executable;
4. confirm **84/84** checks pass;
5. perform the short existing Mock scanner/Memory Viewer/Disassembler regression smoke test described in the rev1 verification plan.

No new PS5 debugger hardware test applies to rev1 because no PS5 debugger transport/provider exists in this revision.

## Status

**Static source review: passed.**

**Windows build/automated rev1 verification: passed — 84/84 checks.**

The post-package Windows result closes the remaining automated gate described above. No new PS5 debugger hardware gate applied to rev1 because no platform debugger backend existed.
