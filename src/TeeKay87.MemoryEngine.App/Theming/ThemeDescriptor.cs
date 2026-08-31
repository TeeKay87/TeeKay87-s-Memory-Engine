namespace TeeKay87.MemoryEngine.App.Theming;

public sealed record ThemeDescriptor(
    string Id,
    string Name,
    int Order)
{
    public override string ToString() => Name;
}
