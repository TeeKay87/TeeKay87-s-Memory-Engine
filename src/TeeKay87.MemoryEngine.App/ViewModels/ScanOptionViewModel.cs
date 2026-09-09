using System;
using System.Collections.Generic;
using System.Linq;
using TeeKay87.MemoryEngine.App.Infrastructure;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.ViewModels;

public sealed class ScanOptionViewModel : ObservableObject
{
    private MemoryScanOptionChoice _selectedChoice;
    private bool _isEnabled;
    private bool _isVisible;

    public ScanOptionViewModel(IMemoryScanOption option)
    {
        ArgumentNullException.ThrowIfNull(option);

        Option = option;
        Choices = option.Choices.ToArray();
        if (Choices.Count == 0)
        {
            throw new ArgumentException($"Scan option '{option.DisplayName}' does not expose any choices.", nameof(option));
        }

        _selectedChoice = Choices.FirstOrDefault(choice =>
                string.Equals(choice.Id, option.DefaultChoiceId, StringComparison.OrdinalIgnoreCase))
            ?? Choices[0];

        PresentationKind = option is IMemoryScanOptionPresentation presentation
            ? presentation.PresentationKind
            : MemoryScanOptionPresentationKind.ChoiceList;

        if (PresentationKind == MemoryScanOptionPresentationKind.Toggle && option is IMemoryScanOptionPresentation togglePresentation)
        {
            ToggleLabel = togglePresentation.ToggleLabel;
            CheckedChoice = Choices.FirstOrDefault(choice =>
                string.Equals(choice.Id, togglePresentation.CheckedChoiceId, StringComparison.OrdinalIgnoreCase));
            UncheckedChoice = Choices.FirstOrDefault(choice =>
                string.Equals(choice.Id, togglePresentation.UncheckedChoiceId, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            ToggleLabel = option.DisplayName;
        }
    }

    public event EventHandler? SelectionChanged;

    public IMemoryScanOption Option { get; }

    public string Id => Option.Id;

    public string DisplayName => Option.DisplayName;

    public string Description => Option.Description;

    public IReadOnlyList<MemoryScanOptionChoice> Choices { get; }

    public MemoryScanOptionPresentationKind PresentationKind { get; }

    public bool IsChoiceList => PresentationKind == MemoryScanOptionPresentationKind.ChoiceList;

    public bool IsToggle => PresentationKind == MemoryScanOptionPresentationKind.Toggle;

    public string ToggleLabel { get; }

    public MemoryScanOptionChoice? CheckedChoice { get; }

    public MemoryScanOptionChoice? UncheckedChoice { get; }

    public MemoryScanOptionChoice SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (!Choices.Contains(value) || (!IsEnabled && !Equals(value, _selectedChoice)))
            {
                return;
            }

            if (SetProperty(ref _selectedChoice, value))
            {
                OnPropertyChanged(nameof(IsToggleChecked));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsToggleChecked
    {
        get => IsToggle && CheckedChoice is not null && Equals(_selectedChoice, CheckedChoice);
        set
        {
            if (!IsToggle || !IsEnabled)
            {
                return;
            }

            MemoryScanOptionChoice? target = value ? CheckedChoice : UncheckedChoice;
            if (target is not null)
            {
                SelectedChoice = target;
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        private set => SetProperty(ref _isEnabled, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    public void RefreshState(
        IMemoryValueType? valueType,
        IMemoryScanType? scanType,
        MemoryScanStage stage,
        bool isScanning,
        bool hasScanSession)
    {
        bool supportsValueType = valueType is not null && Option.SupportsValueType(valueType);
        bool supportsScanType = scanType is not null &&
                                (Option is not IMemoryScanOptionApplicability applicability ||
                                 applicability.SupportsScanType(scanType, stage));

        IsVisible = supportsValueType && supportsScanType;
        IsEnabled = IsVisible &&
                    !isScanning &&
                    (!hasScanSession || !Option.LockAfterFirstScan) &&
                    Choices.Count > 1;
    }
}
