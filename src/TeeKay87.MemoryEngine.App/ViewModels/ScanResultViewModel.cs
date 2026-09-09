using System;
using System.Windows.Input;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.Core.Scanning;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class ScanResultViewModel : ObservableObject
{
    private readonly Action<string> _copyTextAction;
    private readonly Action<ScanResultViewModel> _changeValueAction;
    private readonly Action<ScanResultViewModel> _saveAddressAction;

    public ScanResultViewModel(
        MemoryScanResult result,
        Action<string> copyTextAction,
        Action<ScanResultViewModel> changeValueAction,
        Action<ScanResultViewModel> saveAddressAction,
        Func<bool> canChangeValue)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        _copyTextAction = copyTextAction ?? throw new ArgumentNullException(nameof(copyTextAction));
        _changeValueAction = changeValueAction ?? throw new ArgumentNullException(nameof(changeValueAction));
        _saveAddressAction = saveAddressAction ?? throw new ArgumentNullException(nameof(saveAddressAction));
        ArgumentNullException.ThrowIfNull(canChangeValue);

        CopyAddressCommand = new RelayCommand(CopyAddress);
        CopyValueCommand = new RelayCommand(CopyValue);
        ChangeValueCommand = new RelayCommand(ChangeValue, canChangeValue);
        SaveAddressCommand = new RelayCommand(SaveAddress);
    }

    public MemoryScanResult Result { get; private set; }

    public string Address => $"0x{Result.Address:X}";

    public string Value => Result.CurrentValue.DisplayText;

    public string Previous => Result.PreviousValue?.DisplayText ?? string.Empty;

    public string Type => Result.CurrentValue.ValueTypeDisplayName;

    public string Region => !string.IsNullOrWhiteSpace(Result.ModuleName)
        ? Result.ModuleName!
        : !string.IsNullOrWhiteSpace(Result.RegionName)
            ? Result.RegionName!
            : string.Empty;

    public string Protection => Result.Protection.ToString();

    public ICommand CopyAddressCommand { get; }

    public ICommand CopyValueCommand { get; }

    public ICommand ChangeValueCommand { get; }

    public ICommand SaveAddressCommand { get; }

    public void UpdateProtection(MemoryProtection protection)
    {
        if (Result.Protection == protection)
        {
            return;
        }

        Result = Result with { Protection = protection };
        OnPropertyChanged(nameof(Protection));
    }

    public void UpdateCurrentValue(MemoryScanValue currentValue)
    {
        ArgumentNullException.ThrowIfNull(currentValue);
        if (!string.Equals(
                currentValue.ValueTypeId,
                Result.CurrentValue.ValueTypeId,
                StringComparison.OrdinalIgnoreCase) ||
            currentValue.Size != Result.CurrentValue.Size ||
            currentValue.Alignment != Result.CurrentValue.Alignment)
        {
            throw new ArgumentException(
                "The live value does not match the Scan Result's Value Type, size, and alignment.",
                nameof(currentValue));
        }

        Result = Result with { CurrentValue = currentValue };
        OnPropertyChanged(nameof(Value));
    }

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

    private void SaveAddress()
    {
        _saveAddressAction(this);
    }
}
