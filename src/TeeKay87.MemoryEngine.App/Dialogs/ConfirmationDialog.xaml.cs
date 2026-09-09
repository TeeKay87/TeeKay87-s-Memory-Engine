using System;
using System.Windows;
using System.Windows.Controls;

namespace TeeKay87.MemoryEngine.App.Dialogs;

internal partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(ConfirmationDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        InitializeComponent();

        Title = options.Title;
        MessageTextBlock.Text = options.Message;
        ConfirmButton.Content = options.ConfirmButtonText;
        CancelButton.Content = options.CancelButtonText;
        ConfirmButton.IsDefault = options.ConfirmIsDefault;
        CancelButton.IsDefault = !options.ConfirmIsDefault;

        ApplyTone(options.Tone);
    }

    private void ApplyTone(ConfirmationDialogTone tone)
    {
        string badgeBackgroundResource;
        string badgeBorderResource;
        string glyphForegroundResource;
        string glyph;
        string confirmStyleResource;

        switch (tone)
        {
            case ConfirmationDialogTone.Warning:
                badgeBackgroundResource = "AccentMutedBrush";
                badgeBorderResource = "WarningTextBrush";
                glyphForegroundResource = "WarningTextBrush";
                glyph = "!";
                confirmStyleResource = "PrimaryButtonStyle";
                break;

            case ConfirmationDialogTone.Danger:
                badgeBackgroundResource = "DangerButtonBackgroundBrush";
                badgeBorderResource = "DangerButtonBorderBrush";
                glyphForegroundResource = "DangerButtonTextBrush";
                glyph = "!";
                confirmStyleResource = "DangerButtonStyle";
                break;

            default:
                badgeBackgroundResource = "AccentMutedBrush";
                badgeBorderResource = "AccentBrush";
                glyphForegroundResource = "AccentBrush";
                glyph = "i";
                confirmStyleResource = "PrimaryButtonStyle";
                break;
        }

        ToneBadge.SetResourceReference(Border.BackgroundProperty, badgeBackgroundResource);
        ToneBadge.SetResourceReference(Border.BorderBrushProperty, badgeBorderResource);
        ToneGlyph.SetResourceReference(TextBlock.ForegroundProperty, glyphForegroundResource);
        ToneGlyph.Text = glyph;
        ConfirmButton.Style = (Style)FindResource(confirmStyleResource);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
