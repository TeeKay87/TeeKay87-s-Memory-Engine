# Plugin Settings Persistence

## Purpose

TeeKay87's Memory Engine `0.1.3.rev16` adds a host-managed settings service for platform plugins.

The design keeps persistence ownership outside plugin implementations. A plugin can request values from its own settings namespace and save values back to that namespace without knowing that the current host uses JSON, where the settings file is located, or how writes are made atomic.

## Ownership Boundary

```text
Platform plugin
    |
    | IPluginSettings
    | TryGetString / TrySetString / TryRemove
    v
Plugin SDK contract
    |
    v
Core PluginHost
    |
    | plugin-id-scoped settings object
    v
Core JsonSettingsStore
    |
    v
%LocalAppData%\TeeKay87\MemoryEngine\settings.json
```

The responsibilities are intentionally separated:

- plugins own the meaning and stable names of their settings keys;
- Plugin SDK defines only the neutral settings contract;
- Core owns plugin namespace isolation and persistence implementation;
- WPF does not need platform-specific file access or JSON logic;
- a plugin never receives the settings file path and cannot select another plugin's namespace through `IPluginSettings`.

## Plugin API

Plugin API `2.3.0` adds two optional contracts:

```csharp
public interface IPluginSettings
{
    bool TryGetString(string key, out string value);
    bool TrySetString(string key, string value);
    bool TryRemove(string key);
}

public interface IPluginSettingsConsumer
{
    void AttachSettings(IPluginSettings settings);
}
```

A plugin that does not need persisted plugin-specific state does not implement `IPluginSettingsConsumer`. Existing compatible Plugin API 2.x plugins therefore continue to load without changes.

The initial contract stores strings because the first real use is connection-field state and because connection values are already represented as strings by `TargetConnectionOptions`. Future typed helpers can be added without exposing the underlying file format.

## Discovery Lifecycle

For a plugin implementing `IPluginSettingsConsumer`, `PluginHost` performs the sequence:

1. construct the public `ITargetPlugin` entry type;
2. validate basic plugin metadata and Plugin API compatibility;
3. create an `IPluginSettings` scope bound to `PluginMetadata.Id`;
4. attach that scope to the plugin;
5. read and validate the plugin's connection and scan declarations;
6. expose the discovered plugin to the application.

Attaching settings before connection declarations are read is deliberate. It allows a plugin to use remembered values as connection-field defaults without moving platform-specific knowledge into WPF.

## JSON Layout

Application settings remain in the existing shared document. Plugin values are kept in a dedicated `plugins` object:

```json
{
  "themeId": "dark",
  "savedAddressesUpdateIntervalMilliseconds": "500",
  "frozenWriteIntervalMilliseconds": "100",
  "scanResultsStorageLocation": "C:\\Users\\User\\AppData\\Local\\TeeKay87\\MemoryEngine\\ScanResults",
  "plugins": {
    "platform.ps5.ps5debug-ng": {
      "connection.host": "192.168.1.50",
      "connection.port": "744"
    }
  }
}
```

The top-level application settings are preserved when plugin settings are written, and plugin namespaces are isolated by the stable plugin id. From application `0.1.3.rev17`, `savedAddressesUpdateIntervalMilliseconds` is an application-level value in the same document. Application `0.1.3.rev18` adds `frozenWriteIntervalMilliseconds`; refresh and Frozen write cadence are independent and neither value is plugin-owned. Application `0.1.3.rev19` changes only the default Frozen cadence from 500 ms to 100 ms; an existing explicitly saved user value is preserved.

Core uses the same atomic temporary-file-and-replace pattern that the existing settings persistence used previously. The App-side `ApplicationSettingsStore` delegates theme, Saved Addresses refresh interval, Frozen write interval, and scan-storage-location values to the same Core settings document implementation, preventing an application-level save from dropping plugin-owned settings.

## Failure Semantics

Plugin writes use `Try...` methods deliberately.

A plugin-setting persistence failure must not turn an otherwise successful target operation into a failure. For example, a PS5 connection can remain valid even if Windows temporarily prevents the remembered connection values from being written.

Reads that cannot resolve a stored value behave as a missing setting. The plugin then uses its own normal default.

## PS5 Connection Settings

PS5 plugin `0.1.0.rev16` continues as the first consumer of the new service.

Stable plugin-setting keys:

```text
connection.host
connection.port
```

On plugin discovery:

- a remembered non-empty host becomes the default value for the **PS5 IP address or host name** field;
- a remembered valid port in the range `1..65535` becomes the default **Port** value;
- otherwise the host remains empty and the port falls back to `744`.

After a PS5 connection successfully completes its ps5debug-NG handshake, the plugin stores the normalized host and port through `IPluginSettings`.

Values are not persisted merely because the user edits a textbox or attempts a failed connection. The remembered values therefore represent the most recent connection that actually succeeded.

## Platform Selector Presentation

The Platform selector is a presentation concern separate from plugin settings.

From `0.1.3.rev16`, the selector shows only `PluginMetadata.Name` (which each built-in plugin sources from its `xxPluginInfo.Name`). Backend remains available in the expandable **Plugin details** area and is not concatenated into the Platform dropdown label.

The removed `PluginViewModel.SelectorDisplay` property is no longer needed because the UI binds directly to `PluginViewModel.Name`.

## Extension Rules

When a plugin adds another persisted setting:

1. use a stable plugin-owned key;
2. read/write only through the injected `IPluginSettings` scope;
3. do not inspect or manipulate `settings.json` directly;
4. do not include the plugin id in the key because Core already scopes the setting to that plugin;
5. avoid persisting secrets unless the settings contract is deliberately extended with suitable secure-storage semantics;
6. choose whether to save on successful use, explicit user confirmation, or another meaningful lifecycle point rather than automatically persisting every intermediate UI edit.
