using TeeKay87.MemoryEngine.App.ViewModels;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal sealed record ManualSavedAddressDialogResult(
    ulong Address,
    string Description,
    ScanValueTypeViewModel ValueType,
    int ValueSize);
