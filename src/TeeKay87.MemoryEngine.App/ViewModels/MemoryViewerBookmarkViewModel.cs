namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class MemoryViewerBookmarkViewModel
{
    public MemoryViewerBookmarkViewModel(ulong address, string? regionName)
    {
        AddressValue = address;
        Address = $"0x{address:X}";
        RegionName = regionName ?? string.Empty;
        DisplayText = string.IsNullOrWhiteSpace(RegionName)
            ? Address
            : $"{Address} · {RegionName}";
    }

    public ulong AddressValue { get; }

    public string Address { get; }

    public string RegionName { get; }

    public string DisplayText { get; }

    public override string ToString() => DisplayText;
}
