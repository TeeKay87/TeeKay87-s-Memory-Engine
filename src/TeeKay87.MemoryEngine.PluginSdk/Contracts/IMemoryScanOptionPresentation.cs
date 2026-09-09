using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IMemoryScanOptionPresentation
{
    MemoryScanOptionPresentationKind PresentationKind { get; }

    string ToggleLabel { get; }

    string? CheckedChoiceId { get; }

    string? UncheckedChoiceId { get; }
}
