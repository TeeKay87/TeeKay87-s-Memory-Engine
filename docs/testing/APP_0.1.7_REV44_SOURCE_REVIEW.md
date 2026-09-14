# TeeKay87's Memory Engine 0.1.7.rev44 Source Review

## Scope

Rev44 is a compile-fix revision built from the user-supplied rev43 package. Before editing, the repository documentation and source/project files were reviewed. The reported Windows errors were traced to `tests/TeeKay87.MemoryEngine.Tests/Program.cs`.

## Finding

`pluginViewModelSourcePath` and `mainWindowXamlPath` were declared inside `VerifyDisassemblerFollowTargetSourceAsync`, but consumed later inside `VerifyDisassemblerSelectionCopyExportSourceAsync`. Because local variables do not cross method scope, the test project produced `CS0103` for both names.

The XAML designer error `XLS0414` was reported at the same time. No independent rev44 XAML change was made for that diagnostic because the concrete C# compile failures prevented a clean project build and can cause secondary designer diagnostics. The Windows rebuild is the authoritative check for whether that designer diagnostic remains after the C# errors are removed.

## Correction

The two fixture-path locals now live in `VerifyDisassemblerSelectionCopyExportSourceAsync`, directly beside the other fixture paths consumed by that check. The unrelated Follow Target test no longer declares them. The test also verifies fixture existence before reading the files.

No runtime application behavior was changed.
