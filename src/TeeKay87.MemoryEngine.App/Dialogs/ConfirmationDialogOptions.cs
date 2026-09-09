using System;

namespace TeeKay87.MemoryEngine.App.Dialogs;

public enum ConfirmationDialogTone
{
    Information,
    Warning,
    Danger
}

public sealed record ConfirmationDialogOptions
{
    public ConfirmationDialogOptions(
        string title,
        string message,
        string confirmButtonText = "OK",
        string cancelButtonText = "Cancel",
        ConfirmationDialogTone tone = ConfirmationDialogTone.Information,
        bool confirmIsDefault = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmButtonText);
        ArgumentException.ThrowIfNullOrWhiteSpace(cancelButtonText);

        Title = title;
        Message = message;
        ConfirmButtonText = confirmButtonText;
        CancelButtonText = cancelButtonText;
        Tone = tone;
        ConfirmIsDefault = confirmIsDefault;
    }

    public string Title { get; }

    public string Message { get; }

    public string ConfirmButtonText { get; }

    public string CancelButtonText { get; }

    public ConfirmationDialogTone Tone { get; }

    public bool ConfirmIsDefault { get; }
}
