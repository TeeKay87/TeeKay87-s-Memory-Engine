using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.App.Controls;

public sealed class DisassemblySyntaxTextBlock : TextBlock
{
    public static readonly DependencyProperty InstructionTextProperty = DependencyProperty.Register(
        nameof(InstructionText),
        typeof(string),
        typeof(DisassemblySyntaxTextBlock),
        new FrameworkPropertyMetadata(string.Empty, OnPresentationChanged));

    public static readonly DependencyProperty SyntaxTokensProperty = DependencyProperty.Register(
        nameof(SyntaxTokens),
        typeof(IReadOnlyList<DisassemblyTextToken>),
        typeof(DisassemblySyntaxTextBlock),
        new FrameworkPropertyMetadata(null, OnPresentationChanged));

    public static readonly DependencyProperty IsInstructionValidProperty = DependencyProperty.Register(
        nameof(IsInstructionValid),
        typeof(bool),
        typeof(DisassemblySyntaxTextBlock),
        new FrameworkPropertyMetadata(true, OnPresentationChanged));

    public string InstructionText
    {
        get => (string)GetValue(InstructionTextProperty);
        set => SetValue(InstructionTextProperty, value);
    }

    public IReadOnlyList<DisassemblyTextToken>? SyntaxTokens
    {
        get => (IReadOnlyList<DisassemblyTextToken>?)GetValue(SyntaxTokensProperty);
        set => SetValue(SyntaxTokensProperty, value);
    }

    public bool IsInstructionValid
    {
        get => (bool)GetValue(IsInstructionValidProperty);
        set => SetValue(IsInstructionValidProperty, value);
    }

    private static void OnPresentationChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is DisassemblySyntaxTextBlock textBlock)
        {
            textBlock.RebuildInlines();
        }
    }

    private void RebuildInlines()
    {
        Inlines.Clear();

        string instructionText = InstructionText ?? string.Empty;
        IReadOnlyList<DisassemblyTextToken>? tokens = SyntaxTokens;
        if (!IsInstructionValid)
        {
            Run invalidRun = new(instructionText);
            invalidRun.SetResourceReference(TextElement.ForegroundProperty, "ErrorTextBrush");
            Inlines.Add(invalidRun);
            return;
        }

        if (tokens is null || tokens.Count == 0 || !TokensMatchInstruction(tokens, instructionText))
        {
            Inlines.Add(new Run(instructionText));
            return;
        }

        foreach (DisassemblyTextToken token in tokens)
        {
            Run run = new(token.Text);
            string? brushResourceKey = GetBrushResourceKey(token.Kind);
            if (brushResourceKey is not null)
            {
                run.SetResourceReference(TextElement.ForegroundProperty, brushResourceKey);
            }

            Inlines.Add(run);
        }
    }

    private static bool TokensMatchInstruction(
        IReadOnlyList<DisassemblyTextToken> tokens,
        string instructionText)
    {
        StringBuilder builder = new();
        foreach (DisassemblyTextToken token in tokens)
        {
            if (token is null)
            {
                return false;
            }

            builder.Append(token.Text);
        }

        return string.Equals(builder.ToString(), instructionText, StringComparison.Ordinal);
    }

    private static string? GetBrushResourceKey(DisassemblyTextTokenKind kind)
    {
        return kind switch
        {
            DisassemblyTextTokenKind.Mnemonic => "DisassemblyMnemonicBrush",
            DisassemblyTextTokenKind.FlowControlMnemonic => "DisassemblyFlowControlBrush",
            DisassemblyTextTokenKind.Register => "DisassemblyRegisterBrush",
            DisassemblyTextTokenKind.Number => "DisassemblyNumberBrush",
            DisassemblyTextTokenKind.Keyword => "DisassemblyKeywordBrush",
            _ => null
        };
    }
}
