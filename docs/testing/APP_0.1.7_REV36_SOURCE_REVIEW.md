# Application 0.1.7.rev36 Source Review — PS5 Disassembly Watchpoint Resolver Compile Fix

## Baseline

Rev36 is built directly from the user-supplied `TK87ME_0.1.7.rev34___Debugger-Finalization-Verification-Test-Corrections(1).zip` lineage as advanced through rev35. The rev35 package introduced the optional PS5 `IDisassemblyWatchpointResolver` implementation, separate Disassembler Add Breakpoint/Add Watchpoint actions, and green Hit/Trigger plus yellow Stop/Current-IP highlighting. The first clean Windows build of rev35 failed before the automated suite could run.

The primary compiler error was:

```text
CS0177: The out parameter 'value' must be assigned before control leaves the current method
```

in `Ps5DisassemblyWatchpointResolver.TryReadAddressRegister(...)`. Visual Studio also reported `XLS0414` for `System.Object` in `MainWindow.xaml` and `CS0006` because `TeeKay87.MemoryEngine.Platform.PS5.dll` was not produced. Those two messages are treated as downstream build-chain failures, not independent XAML/reference defects.

## Source correction

The failing method previously returned the result of a short-circuit expression:

```csharp
return id is not null && TryReadRegister(registers, id, out value);
```

When `id` was `null`, the right side was not evaluated and `value` was never assigned. Rev36 changes only this control path:

```csharp
if (id is null)
{
    value = 0;
    return false;
}

return TryReadRegister(registers, id, out value);
```

This preserves the intended resolver semantics: unsupported address-register kinds remain unresolved and automatic Add Watchpoint stays disabled rather than guessing. Supported GPR paths still use the existing register reader unchanged.

## Boundary review

- No Core source changed.
- No WPF/XAML source changed.
- No Mock plugin source changed.
- No Plugin SDK public contract changed; API remains `2.18.0`.
- No ps5debug-NG transport, breakpoint/watchpoint command, detach cleanup, register transport, disassembly decode, snapshot, exporter/importer, or comparer behavior changed.
- Application metadata advances to `0.1.7.rev36 - PS5 Disassembly Watchpoint Resolver Compile Fix`.
- PS5 semantic plugin version advances from `0.1.1` to `0.1.2` because PS5 plugin source changed. Legacy revision metadata remains `39` for compatibility with the existing metadata model.
- Mock remains `1.0.1.rev17`.
- The existing plugin-version verification assertion is updated from `0.1.1.rev39` to `0.1.2.rev39`; no new test entry is added, so the registry remains 152 checks.

## Static review

Before packaging, all Markdown documentation was reread and the complete source/project tree was reviewed. The only production-code behavior change is the explicit `out` assignment described above. The new resolver file was also checked for other short-circuit `out` patterns of the same form; none remain. Project/XAML/JSON files remain structurally valid.

This environment does not contain the Windows/.NET WPF toolchain, so the authoritative compiler gate remains the user's clean Windows build. Rev36 is not verified until the build succeeds and the full **152/152** suite passes.
