# 0.1.1.rev3 PS5 Process Enumeration and Target Selection Verification

## Purpose

This document records project-wide verification requirements for **TeeKay87's Memory Engine 0.1.1.rev3 - PS5 Process Enumeration and Target Selection**.

The PS5 wire-protocol and live-target verification details belong in the PS5 plugin documentation directory:

- `docs/plugins/PS5/PS5DEBUG_NG_PROTOCOL_MAPPING.md`;
- `docs/plugins/PS5/PROCESS_ENUMERATION_VERIFICATION.md`.

## Scope

This revision adds the first live-target operation after connection while deliberately reusing the Plugin SDK contracts created in the foundation revision.

No Plugin SDK or Core contract change is required. `IProcessProvider` and `TargetProcess` already represent the necessary cross-platform operation and data. The revision therefore changes only:

- PS5 plugin process-list protocol handling and session service exposure;
- the PS5 plugin's own revision/capability metadata;
- generic WPF process-list, selection, refresh, and active-target workflow;
- deterministic protocol tests and documentation.

The existing Mock plugin process/memory implementation, Plugin SDK assemblies, Core plugin host, connection-setting model, and shared button templates are not redesigned.

## Static Verification Requirements

The prepared source tree should satisfy all of the following:

1. application `AppInfo` reports `0.1.1.rev3` and feature title `PS5 Process Enumeration and Target Selection`;
2. PS5 plugin reports independent version `0.1.0.rev2`;
3. Mock plugin remains `1.0.0.rev1`;
4. Plugin API remains `1.0.0`;
5. PS5 capabilities contain `Connect | ProcessEnumeration` and do not advertise foreground-process or memory operations;
6. `Ps5TargetSession` implements the pre-existing `IProcessProvider` contract;
7. Core and Plugin SDK source files remain unchanged from `0.1.1.rev2`;
8. `CMD_PROC_LIST` remains entirely inside the PS5 plugin/test fixture and does not appear in Core or WPF source;
9. the WPF process panel is gated by `TargetCapabilities.ProcessEnumeration`, not by a PlayStation platform-name check;
10. the process panel uses the existing shared Primary/Secondary button styles;
11. selected process and active target are separate view-model states;
12. disconnect clears process-list and active-target state;
13. refresh attempts to preserve selected/active identities only when the same PID/name pair remains present;
14. the loopback protocol fixture represents the documented status/count/36-byte-entry response;
15. verification tests expect PS5 `IProcessProvider` to be present and validate representative parsed processes;
16. all XAML, project XML, and root XML configuration remain well formed;
17. solution/project-reference paths continue to resolve;
18. release contents exclude `bin`, `obj`, `.vs`, `.user`, and `.suo` artifacts.

## Required Windows Build Verification

Build the complete solution in Visual Studio 2022:

```text
Build > Build Solution
```

No warnings or errors should be produced because warnings are treated as errors project-wide.

Then run the verification executable:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

The suite should report all checks as `PASS`, including the new PS5 process-enumeration protocol check.

## Required WPF Runtime Verification

### In-Memory Test Target

1. select **In-Memory Test Target**;
2. choose **Connect**;
3. verify the generic process panel displays the single `TestGame.exe` process;
4. verify the single row is selected automatically;
5. choose **Set Active Target** and confirm the Active target field updates;
6. choose **Refresh** and confirm the active target remains valid;
7. disconnect and confirm the process state is cleared.

This proves the process UI remains platform-neutral and reuses the existing mock implementation.

### PlayStation 5

Perform the live procedure in `docs/plugins/PS5/PROCESS_ENUMERATION_VERIFICATION.md` against a real PS5 running ps5debug-NG.

The application should remain connected even if a process-list request itself fails. A process-list failure belongs in the process panel and must not be presented as a failed connection after the connection handshake has already succeeded.


## Recorded Windows/PS5 Runtime Result

On 2026-08-30, the user reported successful runtime verification of the supplied `0.1.1.rev3` build on Windows with a physical PlayStation 5 running ps5debug-NG:

- the application runs successfully;
- the PlayStation 5 plugin connects successfully;
- the generic process workflow retrieves and displays the real process list from the PS5.

This confirms the central rev3 implementation goal of live PS5 process enumeration through the generic host workflow. The report did not separately confirm every detailed Refresh, Active Target persistence, disconnect/reconnect, or error-state check listed below, so those individual checks remain applicable regression tests rather than being marked as completed here.

## Preparation-Environment Status

The source-preparation environment does not contain the .NET SDK or Windows WPF runtime. Native build, executable test execution, and live process enumeration cannot therefore be completed here.

Static source, XML/XAML, protocol-reference, project-structure, version-consistency, and archive-integrity checks are performed before packaging. Final native/runtime results should be recorded after testing on the Windows development machine and physical PS5.
