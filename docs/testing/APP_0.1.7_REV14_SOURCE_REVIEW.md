# Application 0.1.7.rev14 Source Review — PS5 Breakpoint Capability Verification Fix

## Scope

Rev14 is a narrowly scoped verification correction built from the supplied `0.1.7.rev13` package. The rev13 Windows run compiled and executed all 114 registered checks, then failed only `PS5 plugin metadata and connection settings`: the test expected the pre-breakpoint PS5 capability set while `Ps5TargetPlugin` correctly advertised `TargetCapabilities.Breakpoints`.

No breakpoint transport, plugin runtime service, WPF breakpoint workflow, splitter behavior, Core service, scanner, Memory Viewer, Disassembler, export path, or target-memory implementation is changed in rev14.

## Pre-change Review

Before modifying the candidate, the complete supplied tree was reviewed:

- root `README.md` and `CHANGELOG.md`;
- all Markdown files under `docs/`;
- the complete C#/XAML/project/configuration source inventory;
- PS5 and Mock capability declarations;
- debugger breakpoint contracts/services;
- all capability equality/flag assertions in the verification project.

The review confirmed that `Ps5TargetPlugin.Capabilities` intentionally includes `Breakpoints`, that the breakpoint-specific tests already require that capability, and that only `VerifyPs5PluginMetadataAsync` retained the older equality set. No duplicate capability declaration or alternate test helper is needed.

## Code Change

`tests/TeeKay87.MemoryEngine.Tests/Program.cs` now includes `TargetCapabilities.Breakpoints` in the exact PS5 capability set expected by `VerifyPs5PluginMetadataAsync`. The accompanying failure message also names software-breakpoint support so the assertion describes the current implementation accurately.

The registry remains **114 checks**. No check is removed, bypassed, weakened, or converted from exact equality to a looser assertion. The correction therefore preserves the original purpose of the metadata test: unexpected missing or extra PS5 capabilities still fail deterministically.

## Version Domains

| Component | rev14 value | Reason |
| --- | --- | --- |
| Application | `0.1.7.rev14` | Host verification/docs changed |
| Feature | `PS5 Breakpoint Capability Verification Fix` | Current revision scope |
| Plugin API | `2.14.0` | No public contract change |
| Mock plugin | `1.0.0.rev12` | No Mock source change |
| PS5 plugin | `0.1.0.rev31` | No PS5 production source change |
| Automated checks | `114` | Registry unchanged |

## Documentation Synchronization

The current README, Debugger architecture, development action plan, Mock/PS5 current-host documentation, and PS5 protocol mapping were synchronized to rev14. Rev13's verification record now contains the actual 113/114 Windows result and is explicitly superseded. The remaining debugger roadmap moves one revision: Hardware Watchpoints begins at rev15.

No external-tool bug report is added for this revision because the failure is in this repository's own verification expectation, not in ps5debug-NG or another external dependency. Existing external bug reports remain unchanged.

## Static Review Requirements

Before packaging, confirm:

- `AppInfo` reports `0.1.7.rev14` and the rev14 feature title;
- Plugin API remains `2.14.0`;
- Mock remains `1.0.0.rev12`;
- PS5 remains `0.1.0.rev31`;
- the PS5 metadata equality includes `Breakpoints`;
- the test registry still contains 114 unique registrations;
- all XAML/XML/project and JSON files parse;
- documentation relative links resolve;
- no `bin` or `obj` directories are packaged;
- the final ZIP contains exactly the locked work-tree files and matches them byte-for-byte.

Native .NET build/test execution remains the Windows verification gate and is not claimed by package preparation.

## Final Static Review Result

The locked rev14 work tree passed the non-.NET package-preparation checks before ZIP creation:

- **152** Markdown files were reread successfully;
- **252** source/project/config text files were reread successfully;
- **23** XAML/project/XML files parsed with **0** errors;
- **3** JSON files parsed with **0** errors;
- **51** relative Markdown links resolved with **0** broken links;
- **0** `bin`/`obj` directories are present;
- application/plugin/API metadata consistently identifies application `0.1.7.rev14`, Plugin API `2.14.0`, Mock `1.0.0.rev12`, and PS5 `0.1.0.rev31`;
- the test registry contains exactly **114** registrations and **114** unique names;
- the corrected PS5 metadata equality includes `TargetCapabilities.Breakpoints` while remaining an exact capability-set comparison;
- rev13-to-rev14 comparison contains **2 added**, **12 changed**, and **0 removed** files;
- the only changed production file under `src/` is `TeeKay87.MemoryEngine.App/Application/AppInfo.cs` for centralized application revision/title metadata;
- `TeeKay87.MemoryEngine.Core`, `TeeKay87.MemoryEngine.PluginSdk`, Mock production code, and PS5 production code are byte-identical to rev13;
- the only behavioral verification-code change is the PS5 capability expectation in `tests/TeeKay87.MemoryEngine.Tests/Program.cs`;
- the changed C# files require no new namespace imports.

This static review does not claim a .NET/WPF build or runtime PASS. The authoritative Windows gate remains `All 114 checks passed.` followed by the focused Mock/live-PS5 breakpoint acceptance.

## Package Verification

The final archive is named `TK87ME_0.1.7.rev14___PS5-Breakpoint-Capability-Verification-Fix.zip`. The locked work tree contains **406 files**. The packaged archive must contain the same 406 relative paths, pass ZIP CRC/integrity validation, contain no extra entries, and match every work-tree file byte-for-byte by SHA-256.

## Post-package Verification Result

The packaged rev14 candidate subsequently passed the full Windows gate at **114/114**. Comprehensive Mock and live-PS5 breakpoint runtime acceptance also passed the intended persistent/temporary hit, lifecycle, paused cleanup, Disassembler navigation, splitter, duplicate, and transport-regression workflows.

That runtime cycle exposed two implementation defects outside rev14's original verifier-only change: execute-breakpoint addresses were not rejected against target memory/protection before backend mutation, and an immediate breakpoint re-hit during Continue could leave Registers empty because the Paused event arrived while the host was still busy. Rev14 is therefore superseded by `0.1.7.rev15 - Breakpoint Runtime Validation Fixes`. The source-review/package-preparation findings above remain the historical record for the rev14 artifact itself.
