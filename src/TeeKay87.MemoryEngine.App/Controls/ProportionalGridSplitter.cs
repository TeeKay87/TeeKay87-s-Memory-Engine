using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TeeKay87.MemoryEngine.App.Controls;

/// <summary>
/// Keeps a previous/next GridSplitter pair within a proportional range while
/// preserving star sizing so the split scales with the available workspace.
/// </summary>
public sealed class ProportionalGridSplitter : GridSplitter
{
    public static readonly DependencyProperty MinimumPreviousRatioProperty = DependencyProperty.Register(
        nameof(MinimumPreviousRatio),
        typeof(double),
        typeof(ProportionalGridSplitter),
        new FrameworkPropertyMetadata(0.2d, FrameworkPropertyMetadataOptions.None, null, CoerceMinimumPreviousRatio),
        ValidateRatio);

    public static readonly DependencyProperty MaximumPreviousRatioProperty = DependencyProperty.Register(
        nameof(MaximumPreviousRatio),
        typeof(double),
        typeof(ProportionalGridSplitter),
        new FrameworkPropertyMetadata(0.8d, FrameworkPropertyMetadataOptions.None, null, CoerceMaximumPreviousRatio),
        ValidateRatio);

    public ProportionalGridSplitter()
    {
        AddHandler(DragDeltaEvent, new DragDeltaEventHandler(HandleDragDelta), handledEventsToo: true);
        AddHandler(DragCompletedEvent, new DragCompletedEventHandler(HandleDragCompleted), handledEventsToo: true);
    }

    public double MinimumPreviousRatio
    {
        get => (double)GetValue(MinimumPreviousRatioProperty);
        set => SetValue(MinimumPreviousRatioProperty, value);
    }

    public double MaximumPreviousRatio
    {
        get => (double)GetValue(MaximumPreviousRatioProperty);
        set => SetValue(MaximumPreviousRatioProperty, value);
    }

    private static bool ValidateRatio(object value)
    {
        double ratio = (double)value;
        return !double.IsNaN(ratio) && ratio >= 0d && ratio <= 1d;
    }

    private static object CoerceMinimumPreviousRatio(DependencyObject dependencyObject, object baseValue)
    {
        ProportionalGridSplitter splitter = (ProportionalGridSplitter)dependencyObject;
        double minimum = (double)baseValue;
        return Math.Min(minimum, splitter.MaximumPreviousRatio);
    }

    private static object CoerceMaximumPreviousRatio(DependencyObject dependencyObject, object baseValue)
    {
        ProportionalGridSplitter splitter = (ProportionalGridSplitter)dependencyObject;
        double maximum = (double)baseValue;
        return Math.Max(maximum, splitter.MinimumPreviousRatio);
    }

    private void HandleDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!ShowsPreview)
        {
            NormalizeSplit();
        }
    }

    private void HandleDragCompleted(object sender, DragCompletedEventArgs e)
    {
        NormalizeSplit();
    }

    private void NormalizeSplit()
    {
        if (Parent is not Grid grid)
        {
            return;
        }

        if (ResizeDirection == GridResizeDirection.Rows)
        {
            NormalizeRows(grid);
            return;
        }

        if (ResizeDirection == GridResizeDirection.Columns)
        {
            NormalizeColumns(grid);
        }
    }

    private void NormalizeRows(Grid grid)
    {
        int splitterIndex = Grid.GetRow(this);
        int previousIndex = splitterIndex - 1;
        int nextIndex = splitterIndex + 1;

        if (previousIndex < 0 || nextIndex >= grid.RowDefinitions.Count)
        {
            return;
        }

        RowDefinition previous = grid.RowDefinitions[previousIndex];
        RowDefinition next = grid.RowDefinitions[nextIndex];
        NormalizeLengths(
            GetEffectiveLength(previous.Height, previous.ActualHeight),
            GetEffectiveLength(next.Height, next.ActualHeight),
            ratio => previous.Height = new GridLength(ratio, GridUnitType.Star),
            ratio => next.Height = new GridLength(ratio, GridUnitType.Star));
    }

    private void NormalizeColumns(Grid grid)
    {
        int splitterIndex = Grid.GetColumn(this);
        int previousIndex = splitterIndex - 1;
        int nextIndex = splitterIndex + 1;

        if (previousIndex < 0 || nextIndex >= grid.ColumnDefinitions.Count)
        {
            return;
        }

        ColumnDefinition previous = grid.ColumnDefinitions[previousIndex];
        ColumnDefinition next = grid.ColumnDefinitions[nextIndex];
        NormalizeLengths(
            GetEffectiveLength(previous.Width, previous.ActualWidth),
            GetEffectiveLength(next.Width, next.ActualWidth),
            ratio => previous.Width = new GridLength(ratio, GridUnitType.Star),
            ratio => next.Width = new GridLength(ratio, GridUnitType.Star));
    }

    private void NormalizeLengths(
        double previousLength,
        double nextLength,
        Action<double> setPrevious,
        Action<double> setNext)
    {
        double totalLength = previousLength + nextLength;
        if (totalLength <= 0d)
        {
            return;
        }

        double previousRatio = previousLength / totalLength;
        double boundedRatio = Math.Clamp(previousRatio, MinimumPreviousRatio, MaximumPreviousRatio);

        setPrevious(boundedRatio);
        setNext(1d - boundedRatio);
    }

    private static double GetEffectiveLength(GridLength length, double actualLength)
    {
        return length.IsStar && length.Value > 0d
            ? length.Value
            : actualLength;
    }
}
