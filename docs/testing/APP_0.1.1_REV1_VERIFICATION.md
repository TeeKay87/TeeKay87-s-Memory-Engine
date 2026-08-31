# 0.1.1.rev1 Application Verification

## Purpose

This document records project-wide verification requirements for **TeeKay87's Memory Engine 0.1.1.rev1 - PS5 Plugin and Connection Foundation**.

Plugin-specific verification is kept in each plugin's own documentation directory:

- `docs/plugins/Mock/FOUNDATION_VERIFICATION.md`;
- `docs/plugins/PS5/CONNECTION_VERIFICATION.md`.

## Project-Wide Checks

The revision should be verified for:

- all solution project paths resolving correctly;
- all project references resolving correctly;
- well-formed `.csproj`, `.props`, and XAML XML;
- Plugin API compatibility checks during discovery;
- independent host and plugin version displays;
- generic connection-setting rendering without platform checks in WPF;
- both built-in plugin assemblies being copied to the application's `Plugins` output directory;
- plugin reload disposing active target sessions before collectible plugin load contexts are unloaded;
- no PS5 protocol types appearing in Core or the WPF application;
- source archive exclusion of `bin`, `obj`, `.vs`, and user-specific build files.


## Preparation-Environment Results

| Check | Result |
| --- | --- |
| Complete supplied documentation review before code changes | PASS |
| Complete supplied source/project review before code changes | PASS |
| `.csproj`, `.props`, and XAML XML parsing | PASS |
| Solution project-path resolution | PASS |
| Project-reference path resolution | PASS |
| Host `AppInfo` / README / CHANGELOG version consistency | PASS |
| Dedicated `docs/plugins/Mock/` and `docs/plugins/PS5/` directories | PASS |
| PS5 capability limited to implemented `Connect` support | PASS |
| No ps5debug-NG command/socket implementation in Core or WPF C# | PASS |
| Release tree free of `bin`, `obj`, and `.vs` output | PASS |
| Automated repository/static verification set | PASS - 21 checks |
| Native .NET compilation | NOT RUN - SDK unavailable in preparation environment |
| WPF runtime launch | NOT RUN - Windows WPF runtime unavailable in preparation environment |
| Live PS5 connection | NOT RUN - physical target required |

## Static Verification Summary

The final preparation pass completed 21 repository-level checks covering XML/XAML parsing, solution and project-reference resolution, host version consistency, independent plugin version declarations, Plugin API version consistency, plugin documentation directories, PS5 protocol isolation from Core/WPF C#, PS5 capability scope, built-in plugin deployment configuration, verification-suite coverage, explicit using requirements in the new PS5 source, required root documentation, and exclusion of transient build/user artifacts.

These checks verify source-tree consistency only. They do not replace compilation or live-target testing.

## Native Verification Commands

On Windows with the .NET 9 SDK:

```powershell
dotnet build TeeKay87.MemoryEngine.sln -c Release
```

Then run:

```powershell
dotnet run --project tests/TeeKay87.MemoryEngine.Tests/TeeKay87.MemoryEngine.Tests.csproj -c Release
```

The verification executable should report all checks as `PASS`.

Finally launch the WPF application and verify that both **In-Memory Test Target** and **PlayStation 5** appear in the plugin list, with independent plugin versions and Plugin API information.

## Preparation Environment

The source-preparation environment does not contain the .NET SDK or Windows WPF toolchain. Native compilation and WPF execution therefore require the Windows development machine and must not be represented as completed until those steps have actually been run.
