using System;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class ConnectionSettingViewModel : ObservableObject
{
    private string _value;

    public ConnectionSettingViewModel(TargetConnectionSettingDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _value = definition.DefaultValue ?? string.Empty;
    }

    public TargetConnectionSettingDefinition Definition { get; }

    public string Key => Definition.Key;

    public string Label => Definition.IsRequired ? $"{Definition.Label} *" : Definition.Label;

    public string? Description => Definition.Description;

    public bool IsRequired => Definition.IsRequired;

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value ?? string.Empty);
    }
}
