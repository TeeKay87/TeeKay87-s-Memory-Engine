# Application 0.1.7.rev40 Source Review — Universal Export Option Display Fix

## Baseline

Rev40 was built directly from the user-supplied rev39 package after reading the root documentation, all Markdown under `docs/`, and reviewing the complete source/test tree. The visible defect is centralized in the reusable `DataExportDialog`; no export consumer requires a private fix.

## Root cause and correction

Both shared ComboBoxes supplied record objects as items. Although `DisplayMemberPath="DisplayName"` was declared, runtime presentation showed the records' diagnostic string representation. Rev40 uses explicit `ComboBox.ItemTemplate` / `TextBlock` bindings to `DisplayName` for Scope and Format. This is a presentation-only change.

## Regression boundary

No Core export writer, schema, data source, scope construction, destination picker, debugger snapshot code, Plugin API, or platform plugin source changed. The existing source-contract test now consumes the shared dialog XAML fixture and requires the explicit templates. Registry count remains 152.
