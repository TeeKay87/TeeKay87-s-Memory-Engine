# TeeKay87's Memory Engine 0.1.3.rev16 Verification

## Revision Under Test

```text
Host application:             0.1.3.rev16
Feature:                      Plugin Settings Persistence and Platform Selector
PlayStation 5 plugin:         0.1.0.rev15
In-Memory Test Target plugin: 1.0.0.rev3
Plugin API:                   2.3.0
```

## Purpose

Rev16 adds host-managed plugin settings and uses that infrastructure to remember the most recent successfully connected PS5 host/port. It also simplifies the Platform dropdown so it displays only the plugin name rather than a concatenated `Name · Backend` label.

This revision does not change the rev13-rev15 disk-backed scanner, TurboScan command flow, result format, process-control behavior, or scan-result limits.

## Automated Verification

The verification executable now contains 34 top-level checks. One new check covers plugin-scoped settings persistence and existing checks were extended to cover PS5 settings behavior and PluginHost injection.

Expected final output:

```text
All 34 checks passed.
```

Relevant checks:

```text
PASS  Plugin API and independent plugin versions
PASS  Plugin-scoped settings persistence
PASS  PS5 plugin metadata and connection settings
PASS  PS5 ps5debug-NG connection handshake
PASS  Plugin host assembly discovery
```

The checks verify:

- Plugin API reports `2.3.0`;
- Mock `2.0.0` remains compatible;
- PS5 plugin reports `0.1.0.rev15` and targets API `2.3.0`;
- application-level values survive plugin-setting writes;
- PS5 and Mock plugin namespaces cannot overwrite one another;
- plugin settings survive reopening the settings store;
- individual plugin keys can be removed;
- remembered PS5 host/port values become connection-field defaults after settings are attached;
- a successful PS5 handshake stores the host/port through `IPluginSettings`;
- PluginHost attaches the correct scoped settings object before reading connection declarations.

## Focused Manual Verification

Keep the manual verification short.

### Platform dropdown

1. Start the application with the built-in plugins available.
2. Open the Platform dropdown.
3. Confirm the visible entries are only the plugin names, for example:

```text
In-Memory Test Target
PlayStation 5
```

4. Confirm the selector does not append `In-Memory` or `ps5debug-NG`.
5. Expand **Plugin details** and confirm Backend is still shown there.

### PS5 remembered connection

1. Select PlayStation 5.
2. Enter a valid PS5 IP/hostname and port.
3. Connect successfully.
4. Close and reopen the application.
5. Select PlayStation 5 if it is not already selected.
6. Confirm both connection fields are pre-filled with the values from the successful connection.
7. Optionally enter deliberately invalid connection data and let that connection fail.
8. Restart again and confirm the last successful values remain the remembered defaults.

### Existing application settings

After using the PS5 persistence feature:

1. confirm the selected theme still persists across restart;
2. confirm Scan Results Storage Location still persists;
3. confirm Settings opens and saves normally.

## Acceptance

Rev16 is verified when:

- the solution builds normally on the Windows development machine;
- all 34 deterministic checks pass;
- Platform dropdown displays only plugin names;
- a successful PS5 host/port is restored after application restart;
- failed connection attempts do not replace the last successful remembered values;
- theme and scan-result-storage settings still persist;
- existing scan functionality remains unchanged.
