# Source Preflight

## Purpose

Application `0.1.5.rev2` introduced the repository-level source preflight after the first `0.1.5.rev1` Windows build exposed a WPF XAML compile failure that ordinary XML parsing did not detect. Application `0.1.5.rev3` hardens that gate after rev2 reached Roslyn and exposed `CS0136` from reusing the same `ContextMenu` pattern-variable name in overlapping declaration spaces.

The preflight is an additional release gate for source preparation. It is designed to catch recurring source-level mistakes before the project reaches the real C#/WPF compiler. It does **not** replace `dotnet build`, Visual Studio **Rebuild Solution**, or the automated verification executable.

The authoritative compile result remains a clean Windows/.NET/WPF build.

Further development of this helper is paused as of `0.1.5.rev4`. The existing files remain available, but current Memory Viewer revisions should not add new preflight rules unless that decision is explicitly revisited.

## Files

The preflight entry points are:

```text
tools/preflight/Run-SourcePreflight.cmd
tools/preflight/Invoke-SourcePreflight.ps1
```

Run from the repository root:

```text
tools\preflight\Run-SourcePreflight.cmd
```

The CMD wrapper starts the PowerShell script with no profile and returns the script exit code to the caller.

A successful run exits with code `0`. Any detected source error exits with code `1`; an invalid repository root exits with code `2`.

## Current Checks

The rev3 preflight validates the following source boundaries.

### XAML/XML structure

Every source `.xaml` file is parsed as XML before additional checks run. Each XAML file carrying `x:Class` must have a matching `.xaml.cs` file that declares the corresponding partial class.

### WPF event wiring

Known WPF event attributes are inspected for:

- a non-empty handler name;
- a matching handler method in the XAML file's code-behind;
- placement outside unsafe direct `Setter.Value` object graphs;
- compatibility with the concrete built-in WPF element type when `PresentationFramework` reflection is available.

The `Setter.Value` rule was added specifically because `0.1.5.rev1` placed `MenuItem.Click` code-behind handlers inside context menus created by `DataGridRow` style setters. The XAML remained valid XML, but the Windows WPF compiler reported `MC6007` and subsequently emitted cascading designer/type errors.

Rev2 moves the affected context menus to concrete `DataGrid.ContextMenu` properties. Row context is prepared when each menu opens, so code-behind events are attached to ordinary concrete XAML objects rather than the shared style-setter object graph.

Event checks intentionally concentrate on known application/WPF events rather than attempting to implement a second XAML compiler. The list can be extended whenever a new event family is introduced.

### C# pattern-variable declaration spaces

Rev3 adds a conservative source rule for pattern variables whose types use the project's normal PascalCase/WPF type naming. Within a single method, the same pattern-variable identifier must not be declared more than once. This intentionally stricter project rule catches the exact rev2 failure where `contextMenu` was introduced twice through `is ContextMenu contextMenu` / `is not ContextMenu contextMenu`; Roslyn rejected the overlapping declaration spaces with `CS0136`.

The check is deliberately narrow and is **not** a C# semantic compiler. It ignores comments and focuses on the repeated typed-pattern form used by the project. Roslyn remains authoritative for all language, nullable, overload, generic, and flow-analysis rules.

### Static resources

Simple named `{StaticResource Key}` references are checked against all source `x:Key` definitions. Implicit type-key styles such as `{StaticResource {x:Type TextBlock}}` are intentionally left to WPF because they are not simple named keys.

### Source-backed `clr-namespace` types

XAML references using project-owned `clr-namespace:` declarations are compared with the C# source type inventory. This is intended to catch mistakes such as a misspelled or accidentally removed custom control/attached-property owner before the WPF designer begins reporting secondary namespace failures.

### JSON and project structure

Bundled source JSON files must parse successfully. Every `.csproj` must parse as XML and every explicit `ProjectReference` path must resolve to an existing project.

### Version/revision consistency

The preflight reads centralized `AppInfo` and checks that:

- README identifies the same current version/revision/feature title;
- CHANGELOG contains the current revision heading;
- the current `docs/testing/APP_<version>_REV<revision>_VERIFICATION.md` file exists;
- when that verification file declares `Expected automated checks`, the automated test registry contains the same number of registered checks.

This does not replace the normal detailed documentation review; it prevents common stale-version packaging errors.

### Release-tree cleanliness

`bin`, `obj`, and `.vs` directories are rejected from the source release tree.

## What the Preflight Does Not Prove

A PASS does not prove that the program compiles or behaves correctly. In particular, source preflight cannot replace:

- full Roslyn language/nullable/type/overload/flow analysis beyond the narrow repeated-pattern-variable regression rule;
- WPF BAML generation and full markup-compiler semantics;
- NuGet/reference resolution;
- platform-specific Windows build behavior;
- the automated verification executable;
- Mock or live-target runtime verification.

The required order for release preparation is therefore:

```text
Source/documentation review
        ↓
Source preflight
        ↓
Clean Windows/WPF build
        ↓
Automated verification executable
        ↓
Feature-specific manual/live verification
        ↓
Release acceptance
```

## Maintenance Rule

The existing rules should remain unchanged while preflight development is paused. If the project later resumes work on this subsystem, any new rule should still be narrowly scoped and conservative; a check that produces routine false positives is worse than a focused check because developers will begin ignoring its result.

The tool belongs to repository/development infrastructure. It must not contain platform-specific PS5 assumptions and must not become part of the runtime application or Plugin SDK.
