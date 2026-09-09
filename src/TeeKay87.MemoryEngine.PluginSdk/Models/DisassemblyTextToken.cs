using System;

namespace TeeKay87.MemoryEngine.PluginSdk.Models;

public sealed class DisassemblyTextToken
{
    public DisassemblyTextToken(string text, DisassemblyTextTokenKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            throw new ArgumentException("A disassembly text token cannot be empty.", nameof(text));
        }

        Text = text;
        Kind = kind;
    }

    public string Text { get; }

    public DisassemblyTextTokenKind Kind { get; }
}
