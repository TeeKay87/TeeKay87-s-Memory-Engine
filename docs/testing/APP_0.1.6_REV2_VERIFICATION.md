# TeeKay87's Memory Engine 0.1.6.rev2 Verification

## Purpose

This document defines verification for application `0.1.6.rev2 - PS5 x86-64 Disassembly Provider`.

Rev2 starts from the user-verified `0.1.6.rev1` baseline that passed **62/62** automated checks. It adds the first real x86-64 disassembly provider inside the PS5 plugin, updates plugin dependency deployment for Iced, and removes the duplicate version/revision badge from the compact top application row.

Rev2 intentionally does not add the Disassembler WPF workspace yet.

## Expected Version State

```text
Application:                  0.1.6.rev2
Feature:                      PS5 x86-64 Disassembly Provider
Plugin API:                   2.10.0
In-Memory Test Target:        1.0.0.rev5 (targets API 2.10.0)
PlayStation 5 plugin:         0.1.0.rev23 (targets API 2.10.0)
Automated verification count: 64
Expected automated checks:     64
```

## 1. Clean Windows Build

On the Windows development machine:

1. extract the complete rev2 ZIP to a clean directory;
2. open `TeeKay87.MemoryEngine.sln`;
3. restore NuGet packages;
4. build the complete solution with .NET 9 / Visual Studio 2022;
5. confirm there are no errors or warnings;
6. confirm the application starts normally.

Warnings are treated as errors and must not be ignored.

## 2. Plugin Dependency Output

After the clean application build, inspect the application's output `Plugins` directory.

Confirm the PS5 plugin deployment contains:

```text
TeeKay87.MemoryEngine.Platform.PS5.dll
TeeKay87.MemoryEngine.Platform.PS5.deps.json
Iced.dll
```

Confirm the application can discover/load the PS5 plugin from that output without an assembly-resolution error. The verification executable should also contain its own `Plugins` subdirectory with the same dependency-complete PS5 layout; the existing `Plugin host assembly discovery` check now validates the `.deps.json`/Iced deployment before isolated discovery.

The PS5 plugin's private deployment must not replace the host's shared Plugin SDK contract with a second plugin-private copy.

## 3. Automated Verification Suite

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 64 checks passed.
```

The two rev2 checks added to the verified 62-check registry are:

```text
PS5 x86-64 disassembly decoding
PS5 x86-64 disassembly safety
```

All rev1 and earlier checks must continue to pass.

## 4. PS5 Plugin Metadata and Service Discovery

Verify the automated metadata/handshake checks confirm:

```text
Plugin version:  0.1.0.rev23
Plugin API:      2.10.0
Architecture:    x64 / 64-bit / little-endian
Capability:      Disassembly
Service:         IDisassemblerProvider
```

The existing PS5 connection, process, memory, scan, and process-control capabilities must remain present.

## 5. Deterministic x86-64 Decode

Verify the dedicated decode check covers and correctly returns:

- `nop`;
- register-to-register `mov`;
- `add`;
- `sub`;
- `cmp`;
- direct relative `call`;
- direct relative unconditional `jmp`;
- direct relative conditional jump;
- `ret`;
- RIP-relative memory addressing.

Confirm:

- decoded instruction order is stable;
- instruction lengths match the source stream;
- register operands are exposed as formatted operand text;
- the direct CALL/JMP/conditional-jump fixture resolves the expected absolute target;
- RET maps to neutral `Return`;
- valid fixture instructions are not marked invalid;
- the sum of instruction lengths consumes the exact source fixture once.

## 6. Provider Safety

Verify the PS5 provider:

- accepts the connected 64-bit little-endian x86-64 architecture;
- rejects 32-bit x86;
- rejects big-endian x86-64;
- rejects ARM64;
- represents a bounded truncated byte stream as an invalid instruction rather than reading beyond supplied bytes;
- preserves the truncated source byte in that invalid record;
- does not fabricate a branch target for an invalid instruction;
- classifies indirect `call rax` / `jmp rax` as Call/Jump without fabricating direct targets;
- observes a pre-cancelled decode request.

## 7. Core/Plugin Boundary

Statically confirm:

- `Ps5X64DisassemblerProvider` is located under the PS5 plugin;
- Iced is referenced by the PS5 plugin project and not by Core, App/WPF, or Plugin SDK;
- the provider receives caller-supplied bytes and does not call `Ps5DebugClient`;
- Core `DisassemblyReader` remains the owner of bounded `IMemoryReader` reads;
- no x86/x64 opcode table or PS5 condition has been introduced into Core/WPF;
- no second disassembly-only target architecture model exists.

## 8. ps5debug-NG Transport Regression

Rev2 provider decoding must not send `CMD_PROC_DISASM_REGION` or any other new ps5debug-NG command.

The provider is client-side decoding over Core-supplied bytes. Existing protocol flows must remain unchanged:

- connection handshake/liveness;
- process enumeration and preferred-process selection;
- memory map;
- raw memory read/write;
- concurrent Frozen writer;
- process suspend/resume;
- TurboScan authorization/capability/start/count/get/end paths.

The upstream server-side disassembly command is documented for future analysis features only.

## 9. Main-Window Version Presentation

Launch the WPF application and confirm all three locations:

1. **Native Windows title bar** — displays only `TeeKay87's Memory Engine`; no `0.1.6.rev2` text is appended.
2. **Compact top application row** — displays the application title, Settings, and Theme controls; no version/revision badge is visible.
3. **Permanent bottom status bar, right edge** — displays exactly the centralized `0.1.6.rev2` value.

The revision feature title `PS5 x86-64 Disassembly Provider` must not appear as persistent title/status chrome.

## 10. Existing UI/Workflow Regression

Confirm previously verified behavior is unchanged, especially:

- Connect/Disconnect and process refresh;
- explicit Active Target workflow;
- full Scan panel gating until an Active Target exists;
- Core-owned Scan Types and plugin-owned Value Types/options;
- First/Next/New/Cancel Scan;
- resident/disk-backed result handling;
- Saved Addresses refresh/edit/freeze;
- universal export for Scan Results/Saved Addresses;
- Memory Viewer navigation/history/bookmarks/region controls;
- Memory Viewer safe editing and read-back verification;
- Danger-button theme semantics;
- Light/Dimmed/Dark theme switching.

## 11. Mock Regression

Mock plugin must remain:

```text
1.0.0.rev5 / API 2.10.0
```

Its existing synthetic disassembly fixture/provider and all previously verified process/memory/scan/read/write behavior must remain unchanged.

## 12. Disassembler UI Must Still Be Absent

Rev2 is a provider milestone. Do not expect or require:

- a Disassembler window;
- `Disassemble Here` context-menu items;
- branch-follow navigation;
- disassembly row selection/copy/export.

Those belong to the next `0.1.6` workspace milestone.

## 13. Documentation and Package Review

Confirm:

- `AppInfo` is `0.1.6.rev2` with feature title `PS5 x86-64 Disassembly Provider`;
- `CHANGELOG.md` begins with the detailed rev2 entry;
- `README.md` describes current rev2 functionality rather than presenting the workspace as implemented;
- `docs/architecture/DISASSEMBLY_ARCHITECTURE.md` describes the real PS5 provider and retains the neutral ownership rules;
- `docs/plugins/PS5/README.md` identifies PS5 plugin `0.1.0.rev23` / API `2.10.0` and advertises Disassembly;
- `docs/plugins/PS5/PS5_DISASSEMBLY_IMPLEMENTATION.md` matches the implementation;
- `docs/ui/MAIN_WORKSPACE.md` states that version/revision appears only in the permanent bottom status bar;
- the release ZIP is named exactly:

```text
TK87ME_0.1.6.rev2___PS5-x86-64-Disassembly-Provider.zip
```

## Acceptance

`0.1.6.rev2` is accepted when the clean Windows solution builds without warnings/errors, the PS5 plugin and Iced dependency load correctly from the application output, all **64/64** automated checks pass, the version/revision badge is absent from the native/top title areas and present only in the bottom status bar, and the existing application workflows show no regression.

A live Disassembler-window test on PS5 is not an acceptance criterion for rev2 because the WPF Disassembler workspace has not been implemented yet.
