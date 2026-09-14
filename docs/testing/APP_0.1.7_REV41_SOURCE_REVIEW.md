# Application 0.1.7.rev41 Source Review — Call Stack Comparer Snapshot Group Assignment

## Baseline

Rev41 was built directly from the rev40 package after reading the root `README.md`, `CHANGELOG.md`, every Markdown document under `docs/`, and the complete source/test/project text surface before code changes. Rev40 had already passed **152/152** on Windows. No platform-plugin source, Plugin API contract, Core comparison algorithm, snapshot schema, or export writer changed in rev41.

## Existing implementation reused

The requested presentation intentionally reuses the established Saved Addresses row pattern instead of introducing a new visual/editor framework:

- `DataGridTemplateColumn` for always-visible row controls;
- normal themed `TextBox`/`ComboBox` controls inside cell templates;
- centered read-only row content;
- the established per-row Danger **Remove** button dimensions and styling;
- the empty-list overlay pattern.

The existing immutable snapshot metadata update path (`DebuggerSnapshot.WithMetadata`) remains authoritative. The existing `DebuggerSnapshotComparer.CompareGroups` implementation remains the only group comparison engine.

## Session group catalog

`CallStackComparerViewModel` now owns one `ObservableCollection<string>` group catalog for the lifetime of that comparer workspace. `DebuggerSnapshotItemViewModel` receives the shared collection plus the central registration function.

Registration:

- trims whitespace;
- ignores empty names as new registrations;
- reuses an existing name case-insensitively;
- adds only genuinely new names;
- updates the snapshot through the existing immutable metadata-copy path.

Imported snapshots with Group metadata are passed through the same registration path when their row view model is created. No application settings or platform/plugin state is involved.

## Group selectors

The row Group editor is editable because it is the only place where new group names may be created. Its text binding commits on focus loss, while Enter and dropdown-close handlers explicitly update the source so both typed names and selected existing names are committed predictably.

Group A and Group B use non-editable ComboBoxes bound directly to the comparer-session catalog. The compare handler reads only their selected items, eliminating the previous free-form text path.

## Regression boundary

Rev41 does not change:

- snapshot capture composition or stale-stop safety;
- JSON schema/version or serializer validation;
- imported snapshot offline ownership;
- pairwise or grouped comparison semantics;
- Universal Export data models/writers;
- debugger lifecycle or target authority;
- Plugin API `2.18.0`;
- Mock `1.0.1.rev17`;
- PS5 `0.1.2.rev39`.

The existing Call Stack Comparer source-contract test is strengthened rather than adding another registry entry. The automated registry therefore remains **152 checks**.
