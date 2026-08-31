using System;
using System.Windows.Input;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.Core.Scanning;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class ScanResultViewModel
{
    private readonly Action<string> _copyTextAction;
    private readonly Action<ScanResultViewModel> _changeValueAction;

    public ScanResultViewModel(
        MemoryScanResult result,
        Action<string> copyTextAction,
        Action<ScanResultViewModel> changeValueAction,
        Func<bool> canChangeValue)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        _copyTextAction = copyTextAction ?? throw new ArgumentNullException(nameof(copyTextAction));
        _changeValueAction = changeValueAction ?? throw new ArgumentNullException(nameof(changeValueAction));
        ArgumentNullException.ThrowIfNull(canChangeValue);

        CopyAddressCommand = new RelayCommand(CopyAddress);
        CopyValueCommand = new RelayCommand(CopyValue);
        ChangeValueCommand = new RelayCommand(ChangeValue, canChangeValue);
    }

    public MemoryScanResult Result { get; }

    public string Address => $"0x{Result.Address:X}";

    public string Value => Result.CurrentValue.DisplayText;

    public string Previous => Result.PreviousValue?.DisplayText ?? string.Empty;

    public string Type => MemoryScanValueCodec.GetDisplayName(Result.ValueType);

    public string Region => !string.IsNullOrWhiteSpace(Result.ModuleName)
        ? Result.ModuleName!
        : !string.IsNullOrWhiteSpace(Result.RegionName)
            ? Result.RegionName!
            : string.Empty;

    public ICommand CopyAddressCommand { get; }

    public ICommand CopyValueCommand { get; }

    public ICommand ChangeValueCommand { get; }

    private void CopyAddress()
    {
        _copyTextAction(Address);
    }

    private void CopyValue()
    {
        _copyTextAction(Value);
    }

    private void ChangeValue()
    {
        _changeValueAction(this);
    }
}
