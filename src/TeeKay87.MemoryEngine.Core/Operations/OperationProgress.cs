using System;

namespace TeeKay87.MemoryEngine.Core.Operations;

public sealed record OperationProgress
{
    public OperationProgress(string? status = null, double? fraction = null, string? detail = null)
    {
        if (fraction is < 0d or > 1d || double.IsNaN(fraction ?? 0d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fraction),
                fraction,
                "Progress fraction must be null for indeterminate progress or a value from 0.0 through 1.0.");
        }

        Status = status;
        Fraction = fraction;
        Detail = detail;
    }

    public string? Status { get; }

    public double? Fraction { get; }

    public string? Detail { get; }

    public bool IsIndeterminate => Fraction is null;

    public double Percentage => Fraction.GetValueOrDefault() * 100d;
}
