# 0.1.0.rev2 WPF Application Namespace Compile Fix

## Purpose

This document records the compile issue reported while building **TeeKay87's Memory Engine 0.1.0.rev1** in Visual Studio 2022 and the corrective change applied in **0.1.0.rev2 - WPF Application Namespace Compile Fix**.

## Reported Build Failure

Visual Studio reported the following compiler error in `src/TeeKay87.MemoryEngine.App/App.xaml.cs`:

```text
CS0118: 'Application' is a namespace but is used like a type
```

The failing declaration inherited from `Application` while the application project also contains the namespace:

```text
TeeKay87.MemoryEngine.App.Application
```

Within the parent namespace `TeeKay87.MemoryEngine.App`, the identifier `Application` could resolve to the child namespace instead of the WPF type imported from `System.Windows`. The compiler therefore treated `Application` as a namespace in the base-type position.

## Corrective Change

`App.xaml.cs` now aliases the WPF application type explicitly:

```csharp
using WpfApplication = System.Windows.Application;
```

The application class inherits from `WpfApplication`. This removes the ambiguity while preserving the existing `TeeKay87.MemoryEngine.App.Application` namespace used by `AppInfo` and avoids an unnecessary namespace or folder migration.

No Core, Plugin SDK, plugin-hosting, mock-target, memory-access, or UI behavior was changed by this fix.

## Version and Documentation Updates

The central `AppInfo` values were updated to:

```text
Version:  0.1.0
Revision: 2
Feature:  WPF Application Namespace Compile Fix
```

`README.md` and `CHANGELOG.md` were updated for the new revision. The rev1 verification document remains unchanged as a historical record of what was verified when that revision was prepared.

## Verification Performed

The complete rev1 documentation and source tree were reviewed before making the change.

The following static checks were completed after the fix:

- `App.xaml.cs` uses an explicit alias for `System.Windows.Application`;
- the ambiguous `public partial class App : Application` declaration is no longer present;
- `App.xaml` still references `TeeKay87.MemoryEngine.App.App` as its application class;
- no project references or plugin contracts were changed;
- `AppInfo` reports `0.1.0.rev2` and the current feature title;
- documentation references the current revision consistently while retaining historical rev1 records;
- XML project files and XAML files remain well-formed;
- the packaged archive contains source and documentation only and excludes build output.

## Native Build Status

The environment used to prepare this revision does not contain the .NET SDK or the Windows WPF build toolchain. A native `dotnet build` could therefore not be executed here.

The fix directly addresses the compiler resolution that produced CS0118. Final native verification should be performed on the Windows development machine by building the complete solution in Visual Studio 2022:

```text
Build > Build Solution
```

If another compiler error is reported, development should remain on version `0.1.0` and advance to the next revision until the foundation builds and runs successfully.
