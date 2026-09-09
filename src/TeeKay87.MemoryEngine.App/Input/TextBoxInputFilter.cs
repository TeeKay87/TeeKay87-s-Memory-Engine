using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TeeKay87.MemoryEngine.PluginSdk.Contracts;

namespace TeeKay87.MemoryEngine.App.Input;

public enum TextBoxInputFilterMode
{
    None,
    UnsignedInteger,
    HexAddress
}

public static class TextBoxInputFilter
{
    public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached(
        "Mode",
        typeof(TextBoxInputFilterMode),
        typeof(TextBoxInputFilter),
        new PropertyMetadata(TextBoxInputFilterMode.None, OnFilterPropertyChanged));

    public static readonly DependencyProperty MemoryValueTypeProperty = DependencyProperty.RegisterAttached(
        "MemoryValueType",
        typeof(IMemoryValueType),
        typeof(TextBoxInputFilter),
        new PropertyMetadata(null, OnFilterPropertyChanged));

    private static readonly DependencyProperty IsHookedProperty = DependencyProperty.RegisterAttached(
        "IsHooked",
        typeof(bool),
        typeof(TextBoxInputFilter),
        new PropertyMetadata(false));

    public static void SetMode(DependencyObject element, TextBoxInputFilterMode value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(ModeProperty, value);
    }

    public static TextBoxInputFilterMode GetMode(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (TextBoxInputFilterMode)element.GetValue(ModeProperty);
    }

    public static void SetMemoryValueType(DependencyObject element, IMemoryValueType? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(MemoryValueTypeProperty, value);
    }

    public static IMemoryValueType? GetMemoryValueType(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(MemoryValueTypeProperty) as IMemoryValueType;
    }

    private static void OnFilterPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not TextBox textBox)
        {
            return;
        }

        bool shouldBeHooked = GetMode(textBox) != TextBoxInputFilterMode.None ||
                              GetMemoryValueType(textBox) is not null;
        bool isHooked = (bool)textBox.GetValue(IsHookedProperty);

        if (shouldBeHooked == isHooked)
        {
            return;
        }

        if (shouldBeHooked)
        {
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            textBox.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(textBox, OnPaste);
        }
        else
        {
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            textBox.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(textBox, OnPaste);
        }

        textBox.SetValue(IsHookedProperty, shouldBeHooked);
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || e.Key != Key.Space)
        {
            return;
        }

        string candidate = BuildCandidateText(textBox, " ");
        if (!IsCandidateAllowed(textBox, candidate))
        {
            e.Handled = true;
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        string candidate = BuildCandidateText(textBox, e.Text);
        if (!IsCandidateAllowed(textBox, candidate))
        {
            e.Handled = true;
        }
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        string? pastedText = e.DataObject.GetData(DataFormats.UnicodeText) as string ??
                             e.DataObject.GetData(DataFormats.Text) as string;
        if (pastedText is null)
        {
            e.CancelCommand();
            return;
        }

        string candidate = BuildCandidateText(textBox, pastedText);
        if (!IsCandidateAllowed(textBox, candidate))
        {
            e.CancelCommand();
        }
    }

    private static string BuildCandidateText(TextBox textBox, string insertedText)
    {
        string currentText = textBox.Text ?? string.Empty;
        int selectionStart = Math.Clamp(textBox.SelectionStart, 0, currentText.Length);
        int selectionLength = Math.Clamp(textBox.SelectionLength, 0, currentText.Length - selectionStart);

        return currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, insertedText);
    }

    private static bool IsCandidateAllowed(TextBox textBox, string candidate)
    {
        IMemoryValueType? memoryValueType = GetMemoryValueType(textBox);
        if (memoryValueType is not null)
        {
            return memoryValueType is not IMemoryValueInputPolicy inputPolicy ||
                   inputPolicy.IsPotentiallyValidInput(candidate);
        }

        return GetMode(textBox) switch
        {
            TextBoxInputFilterMode.UnsignedInteger => IsPotentialUnsignedInteger(candidate),
            TextBoxInputFilterMode.HexAddress => IsPotentialHexAddress(candidate),
            _ => true
        };
    }

    private static bool IsPotentialUnsignedInteger(string text)
    {
        return text.Length == 0 || text.All(char.IsAsciiDigit);
    }

    private static bool IsPotentialHexAddress(string text)
    {
        if (text.Length == 0)
        {
            return true;
        }

        string digits = text;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            digits = text[2..];
        }
        else if (text.Contains('x') || text.Contains('X'))
        {
            return false;
        }

        return digits.Length <= 16 && digits.All(IsHexDigit);
    }

    private static bool IsHexDigit(char character)
    {
        return char.IsAsciiDigit(character) ||
               character is >= 'a' and <= 'f' ||
               character is >= 'A' and <= 'F';
    }
}
