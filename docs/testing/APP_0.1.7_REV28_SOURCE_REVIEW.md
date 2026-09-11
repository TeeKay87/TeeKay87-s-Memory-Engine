# Application 0.1.7.rev28 Source Review — Debugger Deferred Stop Verification Contract Fix

## Scope Reviewed

The supplied `0.1.7.rev27` candidate was used as the sole baseline. Before making the corrective change, the complete documentation and source tree was traversed again, including root README/CHANGELOG, every Markdown document under `docs/`, application/Core/Plugin SDK/plugin C# sources, XAML/resources, project/build files, themes, tests, and scripts. The focused review then compared the single failing Windows source contract with the rev27 `DebuggerViewModel` stop-event implementation and the new rev27 interrupted composed-operation contract.

Rev27's Windows run completed **134/135** checks. The only failure was `Debugger breakpoint manager source contract`, which still required the literal source shape `else if (_continueInProgress)`. Rev27 had intentionally widened that branch to `else if (_continueInProgress || _pendingInterruptedComposedExecutionOperation is not null)` so a Paused event remains deferred not only while Continue is completing, but also while an interrupted composed operation is awaiting safe temporary-breakpoint cleanup. The dedicated rev27 `Debugger interrupted composed-operation cleanup source contract` already required the widened condition and passed in the same run.

The failure is therefore a stale verifier assertion, not evidence of a production regression. No production debugger, Core, Plugin SDK, Mock, PS5 transport, breakpoint, stepping, cleanup, UI, or export code is changed by rev28.

## Verification Contract Correction

`VerifyDebuggerBreakpointManagerSourceAsync` now requires the actual rev27 deferred-stop condition:

```text
else if (_continueInProgress || _pendingInterruptedComposedExecutionOperation is not null)
```

The accompanying failure text is updated to describe both cases covered by the branch: a Paused breakpoint event arriving while Continue is still completing and a Paused interruption that must remain deferred until composed-operation cleanup can run safely.

This preserves the older breakpoint-manager invariant instead of weakening it. The breakpoint manager still requires `_deferredStopContextEvent`, `_continueInProgress`, and `RefreshDeferredStopContextIfNeeded()`. The corrected assertion now agrees with the stricter rev27 interruption-cleanup contract rather than contradicting it.

## Production Boundaries Preserved

The following remain byte-for-byte unchanged from rev27 production code:

- `DebuggerViewModel` composed-operation ownership, target-event matching, deferred cleanup, and status reconciliation;
- PS5 plugin `0.1.0.rev36` safe software-breakpoint restoration before detach/disposal;
- rev25 logical software-breakpoint register/frame stop-context reconciliation;
- rev26 breakpoint-aware Step Over original-instruction capture;
- Step Into, Step Over, Step Out, Run to Address, breakpoint/watchpoint management, Call Stack, Threads, and Registers behavior;
- Core debugger contracts and Plugin SDK `2.15.0`;
- Mock plugin `1.0.0.rev15`;
- scanner, Memory Viewer, Disassembler, Universal Export foundation, themes, status-bar layout, and modeless tool-window ownership.

PS5 plugin version remains `0.1.0.rev36` because no plugin source changes are made.

## Verification Registry

No new test is added. The registry remains **135** checks. Rev28 corrects one existing stale source assertion in place.

The expected Windows result is therefore:

```text
All 135 checks passed.
```

After that gate passes, runtime acceptance resumes with the rev27 correction-sensitive sequence: short Step Over/Step Out cleanup regression, controlled interrupted Run to Address with manual Pause, safe Detach/reconnect, normal successful Run to Address, and final cleanup/tool-window regression.

## Version and Compatibility

| Component | rev28 value | Change |
| --- | --- | --- |
| Application | `0.1.7.rev28` | Corrective verification-contract revision |
| Feature title | `Debugger Deferred Stop Verification Contract Fix` | New application title |
| Plugin API | `2.15.0` | Unchanged |
| Mock plugin | `1.0.0.rev15` | Unchanged |
| PS5 plugin | `0.1.0.rev36` | Unchanged |
| Automated registry | `135` | One stale existing source assertion corrected |

## Development Order

Rev27 production behavior remains the implementation under runtime acceptance. Rev28 is consumed only by the verifier correction required after the **134/135** Windows result. If rev28 passes the clean Windows **135/135** gate and the carried rev27 runtime gates without another code correction, Debugger **Integration, Export and Finalization** becomes `0.1.7.rev29`.
