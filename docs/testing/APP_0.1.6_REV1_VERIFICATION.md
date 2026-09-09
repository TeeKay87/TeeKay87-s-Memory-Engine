# TeeKay87's Memory Engine 0.1.6.rev1 Verification

## Purpose

This document defines verification for application `0.1.6.rev1 - Disassembly Contracts and Core Foundation`.

Rev1 introduces the architecture-neutral Plugin SDK/Core disassembly foundation and a deterministic Mock provider. It intentionally does not add a Disassembler WPF window or PS5 x86-64 provider.

## Expected Version State

```text
Application:                  0.1.6.rev1
Feature:                      Disassembly Contracts and Core Foundation
Plugin API:                   2.10.0
In-Memory Test Target:        1.0.0.rev5 (targets API 2.10.0)
PlayStation 5 plugin:         0.1.0.rev22 (targets API 2.9.0)
Automated verification count: 62
```

The PS5 plugin is expected to remain API `2.9.0` in this revision because host compatibility permits older minor versions and PS5 disassembly is not implemented until the next feature milestone.

## 1. Clean Windows Build

On the Windows development machine:

1. extract the complete rev1 ZIP to a clean directory;
2. open `TeeKay87.MemoryEngine.sln`;
3. build the complete solution with .NET 9 / Visual Studio 2022;
4. confirm there are no errors or warnings;
5. confirm the application starts normally.

Warnings are treated as errors and must not be ignored.

## 2. Automated Verification Suite

Run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

Expected final line:

```text
All 62 checks passed.
```

The eight rev1 checks added to the existing 54-check registry are:

```text
Disassembly instruction model
Mock disassembly capability and provider
Core disassembly bounded read
Core disassembly region-boundary handling
Core disassembly unreadable-region rejection
Core disassembly instruction-order validation
Core disassembly cancellation
Disassembly session target identity
```

All previously existing checks must continue to pass unchanged.

## 3. Instruction Model

Verify the automated instruction-model check confirms:

- Address is retained;
- RawBytes are defensively copied;
- Length matches RawBytes length;
- Mnemonic and Operands are retained independently;
- FlowControl is retained;
- a direct Call target is retained;
- validity state is retained;
- a non-branch flow-control type cannot claim a direct branch target.

## 4. Capability and Provider Discovery

Verify Mock plugin metadata advertises:

```text
MemoryRead
MemoryWrite
Disassembly
```

and still does **not** advertise `Debugger`.

After connecting to Mock Target, confirm `ITargetSession.GetRequiredService<IDisassemblerProvider>()` succeeds and the provider accepts the target's declared architecture.

No PS5 `Disassembly` capability should be added in rev1.

## 5. Deterministic Mock Decode

The Mock target exposes a known byte fixture at `MockTargetLayout.CodeAddress`.

Verify the provider returns the expected ordered instruction sequence and specifically preserves:

- a direct Call classification and target;
- a direct ConditionalJump classification and target;
- Return classification;
- raw bytes matching the memory fixture.

The Mock provider is a synthetic test decoder, not an x86-64 implementation.

## 6. Bounded Core Read

Verify `DisassemblyReader`:

- begins at the requested decode address;
- reads only the requested byte count when it fits the region;
- returns the exact target bytes read;
- retains containing `MemoryRegion` context;
- retains `TargetArchitecture` context;
- returns provider instructions in stable address order.

## 7. Region-Boundary Handling

Use a synthetic readable region that ends five bytes after `MockTargetLayout.CodeAddress` and request the normal 512-byte window.

Verify:

- only five bytes are read;
- `DisassemblySnapshot.EndAddressExclusive` equals the region end;
- no read crosses into bytes outside that region;
- the final truncated two-byte Mock opcode is represented as one invalid instruction using only the available source byte.

## 8. Unreadable and Guarded Regions

Verify Core rejects a disassembly request when the supplied containing region is:

- readable but `Guard` protected;
- write-only without `Read` permission.

The provider must not be used to bypass memory-map read eligibility.

## 9. Provider Result Validation

The dedicated ordering check supplies a deliberately invalid provider that returns instruction records in reverse address order.

Verify Core rejects that provider output rather than publishing it as a snapshot.

The same Core path also validates that instruction ranges remain inside the actual memory bytes and that each instruction's RawBytes match those bytes.

## 10. Cancellation

Verify a pre-cancelled token causes the Core disassembly operation to end with cancellation rather than reading/decoding normally.

Later providers must continue to observe cancellation at safe boundaries.

## 11. Target/Connection Identity

Verify `DisassemblySessionIdentity` matches only when all stable identity fields remain current:

```text
PluginId
ProcessId
ProcessName
ConnectionGeneration
```

Changing display-only process text must not invalidate identity. Changing plugin id, process id/name, or connection generation must invalidate it.

This is the neutral foundation for the future workspace's stale-window safety; no WPF disassembler window exists in rev1.

## 12. Regression Checks

Confirm the existing application behavior remains unchanged:

- normal plugin discovery and connection;
- explicit Active Target workflow;
- scanner behavior and scan options;
- disk-backed and backend-resident result handling;
- Saved Addresses refresh/edit/freeze behavior;
- universal Scan Results/Saved Addresses export;
- Memory Viewer read/copy/history/bookmarks/region navigation;
- Memory Viewer safe writes;
- theme and Danger-button behavior from `0.1.5.rev8`;
- native main-window title and status-bar version presentation.

The added Mock code fixture must not change the existing health/ammo/money addresses or memory-region dimensions.

## 13. Static Architecture Review

Confirm the production source contains no:

```text
PS5 condition in Core disassembly
x86/x64 opcode table in Core or WPF
ps5debug command in Core or WPF
new UI-owned memory transport
parallel disassembly-only architecture model
```

Confirm all new C# files contain the required `using` directives for their referenced framework/project types.

## 14. Documentation and Package Review

Confirm:

- `AppInfo` is `0.1.6.rev1` with the correct feature title;
- `CHANGELOG.md` begins with the detailed rev1 entry;
- `README.md` describes the current rev1 functionality rather than presenting PS5/UI disassembly as implemented;
- `docs/architecture/DISASSEMBLY_ARCHITECTURE.md` matches the actual contract and Core behavior;
- Mock documentation identifies plugin `1.0.0.rev5` and Plugin API `2.10.0`;
- no PS5 disassembly implementation document is added before PS5 disassembly exists;
- the release ZIP is named exactly:

```text
TK87ME_0.1.6.rev1___Disassembly-Contracts-and-Core-Foundation.zip
```

## Acceptance

`0.1.6.rev1` is accepted when the Windows solution builds cleanly, all **62/62** automated checks pass, the application and all previously verified workflows still operate normally, and the new neutral disassembly contract/Core/Mock tests behave exactly as documented.

PS5 disassembly and the Disassembler UI are explicitly **not** acceptance requirements for rev1.
