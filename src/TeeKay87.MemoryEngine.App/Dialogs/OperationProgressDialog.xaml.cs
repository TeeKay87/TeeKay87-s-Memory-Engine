using System;
using System.ComponentModel;
using System.Windows;
using TeeKay87.MemoryEngine.Core.Operations;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal partial class OperationProgressDialog : Window
{
    private bool _operationCompleted;
    private bool _cancellationRequested;

    public OperationProgressDialog(
        string title,
        string initialStatus,
        bool allowCancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        InitializeComponent();

        Title = title;
        StatusTextBlock.Text = initialStatus ?? string.Empty;
        AllowCancellation = allowCancellation;
        CancelButton.Visibility = allowCancellation ? Visibility.Visible : Visibility.Collapsed;
        ApplyProgress(new OperationProgress(initialStatus, fraction: null));
    }

    public event EventHandler? CancellationRequested;

    public bool AllowCancellation { get; }

    public void ApplyProgress(OperationProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        if (progress.Status is not null)
        {
            StatusTextBlock.Text = progress.Status;
        }

        DetailTextBlock.Text = progress.Detail ?? string.Empty;
        ProgressBar.IsIndeterminate = progress.IsIndeterminate;

        if (progress.IsIndeterminate)
        {
            PercentageTextBlock.Text = string.Empty;
            PercentageTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        ProgressBar.Value = progress.Percentage;
        PercentageTextBlock.Text = $"{progress.Percentage:0}%";
        PercentageTextBlock.Visibility = Visibility.Visible;
    }

    public void CompleteAndClose()
    {
        _operationCompleted = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_operationCompleted)
        {
            e.Cancel = true;
            RequestCancellation();
            return;
        }

        base.OnClosing(e);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        RequestCancellation();
    }

    private void RequestCancellation()
    {
        if (!AllowCancellation || _cancellationRequested)
        {
            return;
        }

        _cancellationRequested = true;
        CancelButton.IsEnabled = false;
        CancellationStatusTextBlock.Visibility = Visibility.Visible;
        CancellationRequested?.Invoke(this, EventArgs.Empty);
    }
}
