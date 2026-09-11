# Application 0.1.7.rev10 Source Review — PS5 Extended Register Transport Guard

## Status

**Static/source package-lock review.** Windows compilation, the packaged **110-check** suite, and live-PS5 runtime verification remain external acceptance gates.

This revision starts from the exact `0.1.7.rev9 - Extended Register State and Debugger Pane Splitter` package that passed **108/108** automated checks plus focused Mock Gates B-D. Rev9 live PS5 Gate E failed when Attach/Pause and thread enumeration succeeded but the register refresh remained pending at `Registers 0`. Rev10 is deliberately limited to guarding that PS5 optional-register transport boundary and synchronizing the resulting metadata/tests/documentation.

## Revision Boundary

Production changes are limited to the PS5 plugin plus centralized application/plugin metadata:

- carry the identified ps5debug-NG protocol/capability metadata into the debugger provider;
- retain mandatory GETREGS on the verified attached debugger-owner connection;
- move safe optional GETFPREGS and GETFSGSBASE reads to short-lived command connections;
- apply a two-second linked timeout only to those disposable optional probes;
- suppress paused GETDBREGS because the current upstream stop-detection/wait sequence can block after debugger Pause has already consumed the stop event;
- degrade each safe optional group independently and cache failed commands for the current attachment;
- preserve read-only PS5 register behavior;
- advance PS5 plugin metadata to `0.1.0.rev29` and application metadata to `0.1.7.rev10`.

Core, Plugin SDK, Mock production code, scanner, Memory Viewer, Disassembler, Saved Addresses, export, themes, and settings are outside the correction.

## Backend Source Contract Recheck

The current ps5debug-NG source was rechecked before locking the rev10 transport design.

- `debug_handle` contains GETFPREGS (`0xBDBB000A`), GETDBREGS (`0xBDBB000C`), and GETFSGSBASE (`0xBDBB000E`) handlers.
- Their fixed successful payload sizes are 832, 128, and 16 bytes respectively.
- Unknown debugger opcodes receive an error status rather than intentionally remaining silent.
- The main server allocates one `server_client` per TCP connection and only the connection performing Attach becomes the debugger-owning client.
- `free_client` invokes full debugger teardown only when that connection's `debugging` flag is set. Closing a non-owner command connection therefore does not deliberately detach the active debugger session.
- Debug commands remain protected by backend debugger/process mutexes. Live rev10 acceptance must therefore verify not only bounded host return but also that Continue/Detach remain usable after any safe optional timeout/fallback.
- The debugger Pause path sends `SIGSTOP` and consumes the stop event with a blocking `wait4()`. `is_process_stopped()` later uses another non-blocking `wait4()` as a state check. GETDBREGS treats a false result as a running target, sends another stop signal, and performs a blocking `wait4()`. If the process is already stopped and the original stop event has been consumed, that second wait can block while the shared debug locks are held.

The first five observations justify disposable optional probe connections for safe optional reads. The final observation is a separate backend defect and means GETDBREGS cannot be used from the current paused Register Refresh path. It is documented in `docs/bug-reports/ps5debug-ng-getdbregs-can-hang-after-pause.md`.

## Production Review

### Capability gate

`Ps5DebugConnectionInfo` exposes one plugin-private decision for automatic extended debugger reads. It parses dotted versions using `System.Version` and requires protocol `>= 1.3` plus capability level `>= 1.0`. Missing, blank, malformed, or older metadata disables optional probing. This uses information already obtained during target identification and does not add a new public Plugin SDK capability.

### Mandatory owner stream

The verified 176-byte GETREGS path remains unchanged on `Ps5DebuggerCommandClient`'s attached owner connection. It still serializes through the existing command gate and fully drains the fixed response. Attach/Detach, Pause/Continue, threads, and the mandatory register path are not migrated to probe connections.

### Optional probe isolation

Each safe optional group uses `Ps5DebuggerCommandClient.Probe*RegistersAsync`. A probe:

1. opens a fresh ps5debug-NG command connection;
2. sends one four-byte LWP-id request;
3. accepts only success plus the complete fixed-size payload or ordinary error/data-null fallback;
4. runs under a linked two-second timeout;
5. disposes the complete connection after success/failure.

Timeout, socket/connection failure, ordinary rejection, or probe-local invalid/truncated response returns `null` for that group. Original caller cancellation is explicitly excluded from these fallback filters and propagates normally. GETDBREGS is intentionally not part of this probe set while the target is Paused.

Because the socket is disposable, the client never attempts to reuse a stream after canceling a possibly partial frame.

### Per-attachment failure cache

`Ps5DebuggerSession` tracks safe optional command ids that produced no usable block. A failed group is skipped on later refreshes during the same attachment. Healthy safe groups continue to probe/refresh. Detach and Dispose clear the set. The mapper receives successful FPU/SIMD and FS/GS blocks; its existing plugin-private Debug-register decoding remains retained but receives no paused GETDBREGS payload in rev10.

### Write boundary

PS5 `WriteRegisterAsync` remains `NotSupportedException`. No SETREGS/SETFPREGS/SETDBREGS/FS-GS write operation was added.

## Automated Coverage Review

The registry now contains **110** unique checks: all **108** rev9 registrations plus two focused PS5 transport checks.

### PS5 debugger extended register capability gating

The protocol fixture omits the branding capability suffix. The test requires:

- normal Attach/Pause;
- a 26-row general-only snapshot;
- preserved semantic instruction pointer;
- zero GETFPREGS/GETDBREGS/GETFSGSBASE probe traffic;
- normal Continue/Detach.

### PS5 debugger optional register timeout isolation

The protocol fixture accepts GETFPREGS but intentionally sends no status/payload until the probe client disconnects. The test requires:

- register refresh returns within six seconds;
- 26 general rows plus the successful 2 FS/GS rows remain (28 total);
- FPU/SIMD rows are absent;
- no Debug-register rows are present and GETDBREGS traffic remains zero;
- the failed FPU command is attempted once and then cached unavailable;
- the successful FS/GS group is read again on the next refresh;
- owner-stream Continue and Detach still complete within five seconds.

The fixture accepts optional probe connections separately from the attached debugger-owner connection so the regression test models the rev10 connection-ownership boundary.

## Required Static Package Checks

Before packaging:

- all documentation must be reread after final edits;
- all production/test source and project files must be reread;
- XAML/project XML must parse;
- relative Markdown links must resolve;
- no `bin/` or `obj/` output may be included;
- changed C# files must contain every required `using` directive;
- Core and Plugin SDK trees must remain byte-identical to the rev9 baseline;
- Mock production tree must remain byte-identical to rev9;
- application metadata must resolve to `0.1.7.rev10 - PS5 Extended Register Transport Guard`;
- PS5 metadata must resolve to `0.1.0.rev29` / API `2.13.0`;
- Mock metadata must remain `1.0.0.rev11` / API `2.13.0`;
- final ZIP must pass `ZipFile.testzip()` and hash-match the locked work tree.

## Final Static Review Result

The final pre-package static review completed successfully against the locked rev10 work tree:

- 143 Markdown documentation files reread;
- 249 source/project text files reread;
- 22 XAML/project XML files parsed successfully;
- 3 JSON files parsed successfully;
- 49 relative Markdown links resolved with no broken targets;
- application and plugin version metadata matched the intended rev10/rev29/rev11 values;
- all 59 Core files were byte-identical to the rev9 baseline;
- all 77 public Plugin SDK files were byte-identical to the rev9 baseline;
- all 9 Mock production files were byte-identical to the rev9 baseline;
- application production changes were limited to the central `AppInfo.cs` revision metadata update;
- the seven intended PS5 production files were the only PS5 production files changed from rev9;
- the paused PS5 register-read path contains no `GETDBREGS` request;
- the automated verification registry contains 110 unique checks;
- rev10 PS5 transport-guard assertions are present in the automated test source;
- all 10 changed C# files were reread for required imports and surrounding code-path consistency;
- root documentation metadata and current-document links are consistent;
- no public project text contains development-tool attribution;
- no `bin/` or `obj/` output is present in the work tree.

Archive integrity and byte-for-byte archive/work-tree comparison are performed after this document is locked and before the release ZIP is handed off.

## Environment Limitation

The authoritative .NET/WPF build and runtime gate remains the user's Windows environment. This review does not claim that the new **110/110** suite has passed. It records only source/static checks performed before packaging.
