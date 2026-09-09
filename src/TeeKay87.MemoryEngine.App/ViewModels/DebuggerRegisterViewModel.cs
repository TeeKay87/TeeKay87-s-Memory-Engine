using System;
using TeeKay87.MemoryEngine.App.Debugging;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class DebuggerRegisterViewModel
{
    public DebuggerRegisterViewModel(DebuggerRegister register)
    {
        Register = register ?? throw new ArgumentNullException(nameof(register));
    }

    public DebuggerRegister Register { get; }

    public string Id => Register.Id;

    public string DisplayName => Register.DisplayName;

    public int BitWidth => Register.BitWidth;

    public string ValueText => DebuggerRegisterValueCodec.Format(Register);

    public string Group => Register.Group ?? string.Empty;

    public string Role => Register.Role == DebuggerRegisterRole.None
        ? string.Empty
        : Register.Role.ToString();

    public bool CanWrite => Register.CanWrite;
}
