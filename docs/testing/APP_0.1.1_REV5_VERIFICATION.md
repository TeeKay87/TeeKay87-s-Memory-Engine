# 0.1.1.rev5 Theme Manager Nullability Compile Fix Verification

## Purpose

This document records verification requirements for **TeeKay87's Memory Engine 0.1.1.rev5 - Theme Manager Nullability Compile Fix**.

The supplied `0.1.1.rev4` source reached the Windows compiler but produced two `CS8600` diagnostics in `ThemeManager.cs`. Because the repository intentionally enables nullable reference analysis and treats warnings as errors, those diagnostics prevent the host application from building.

This revision corrects only those nullable lookups. It does not redesign the workspace, theme system, plugin contracts, or PS5 functionality.

## Reported Build Failure

Visual Studio reported `CS8600` in `TeeKay87.MemoryEngine.App` at the two `ThemeManager` calls that used an explicitly non-nullable `LoadedTheme` variable as the `out` target of `Dictionary.TryGetValue`.

The affected operations were:

1. applying a theme by id;
2. finding a theme by id during startup/preference resolution.

With nullable annotations enabled, `TryGetValue` may assign the default value to its `out` argument when it returns `false`. The code therefore must not model that `out` value as unconditionally non-null.

## Correction

Both lookups now:

- receive the dictionary result into `LoadedTheme?`;
- verify that `TryGetValue` returned `true`;
- verify that the resulting value is non-null before dereferencing it.

`ApplyTheme` continues to return `false` for an invalid/missing id. `FindTheme` continues to return `null` for an invalid/missing id. Successful theme behavior is otherwise unchanged.

## Static Verification Requirements

The release source must satisfy all of the following:

1. `AppInfo` reports `0.1.1.rev5` and feature title `Theme Manager Nullability Compile Fix`;
2. both `TryGetValue` sites in `ThemeManager` use nullable `LoadedTheme?` out values and explicit non-null guards;
3. no nullable suppression operator is introduced to hide the diagnostics;
4. no project-level nullable or warning-as-error setting is weakened or disabled;
5. Light, Darker, and Darkest theme files remain unchanged;
6. the external theme schema remains unchanged;
7. Core and Plugin SDK contracts remain unchanged;
8. Mock plugin remains `1.0.0.rev1`;
9. PS5 plugin remains `0.1.0.rev2`;
10. Plugin API remains `1.0.0`;
11. existing PS5 connection/process functionality remains present;
12. all XAML, project XML, and JSON remain syntactically well formed;
13. release contents exclude `bin`, `obj`, `.vs`, `.user`, and `.suo` artifacts.

## Required Windows Build Verification

Open `TeeKay87.MemoryEngine.sln` in Visual Studio and run:

```text
Build > Rebuild Solution
```

Expected result:

- zero `CS8600` diagnostics in `ThemeManager.cs`;
- zero warnings;
- zero errors.

Warnings remain treated as errors. The fix must compile under the existing project policy rather than bypassing it.

## Required Runtime Regression Verification

After a successful build:

1. start the WPF application;
2. verify Light, Darker, and Darkest are still listed;
3. switch between all three themes and confirm the open workspace updates immediately;
4. restart the application and confirm the selected theme is restored;
5. connect to the PS5 and confirm the process list still loads;
6. select a process and set it as Active Target;
7. disconnect and confirm target/process state is cleared as before.

## Preparation-Environment Status

The source-preparation environment does not provide the Windows WPF build toolchain. Static source checks can validate the nullable guards and unchanged contracts, but Visual Studio on Windows remains the authoritative confirmation that the two reported `CS8600` errors are eliminated.
