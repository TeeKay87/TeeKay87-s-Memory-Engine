using System.Collections.Generic;

namespace TeeKay87.MemoryEngine.App.Theming;

internal sealed class ThemeDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public Dictionary<string, string> Colors { get; set; } = new();
}
